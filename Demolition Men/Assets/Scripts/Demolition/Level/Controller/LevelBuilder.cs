using System.Collections.Generic;
using Demolition.Generation;
using Demolition.Run;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Demolition.Levels
{
    /// <summary>
    /// <see cref="LevelLayout"/> → scene. The ground is one tilemap built up front (cheap);
    /// buildings, pickups and the finish stream in per segment as the camera nears them
    /// and are destroyed once they are far behind the left edge, where nobody can be.
    /// </summary>
    public sealed class LevelBuilder : MonoBehaviour
    {
        /// <summary>Segments spawn this far (units) ahead of the camera's right edge.</summary>
        private const float LookAhead = 30f;
        /// <summary>...and despawn this far behind its left edge.</summary>
        private const float KeepBehind = 25f;
        /// <summary>Solid ground below the lowest surface, so pits and sewers have walls.</summary>
        private const int GroundDepth = 8;

        private LevelLayout _layout;
        private LevelTheme _theme;
        private Pickup _pickupPrefab;
        private bool _coop;
        private Transform[] _segments;
        private bool[] _despawned;
        private Camera _camera;

        public LevelLayout Layout => _layout;

        public void Build(LevelLayout layout, LevelTheme theme, Pickup pickupPrefab, bool coop)
        {
            _layout = layout;
            _theme = theme;
            _pickupPrefab = pickupPrefab;
            _coop = coop;
            _segments = new Transform[layout.Segments.Count];
            _despawned = new bool[layout.Segments.Count];
            BuildGround();
            BuildWalls();
        }

        private void Update()
        {
            if (_layout == null)
                return;
            if (_camera == null)
                _camera = Camera.main;
            float left = 0f, right = float.MaxValue;
            if (_camera != null)
            {
                float halfWidth = _camera.orthographicSize * _camera.aspect;
                left = _camera.transform.position.x - halfWidth;
                right = _camera.transform.position.x + halfWidth;
            }
            Stream(left, right);
        }

        /// <summary>Spawn everything that should exist with the camera spanning [left, right].</summary>
        public void Stream(float left, float right)
        {
            for (int i = 0; i < _layout.Segments.Count; i++)
            {
                LevelSegment segment = _layout.Segments[i];
                if (_segments[i] == null && !_despawned[i] && segment.X0 < right + LookAhead)
                    _segments[i] = Spawn(i);
                if (_segments[i] != null && segment.X1 < left - KeepBehind)
                {
                    Destroy(_segments[i].gameObject);
                    _segments[i] = null;
                    _despawned[i] = true;
                }
            }
        }

        // ---------------- Ground ----------------

        private void BuildGround()
        {
            var grid = new GameObject("Ground", typeof(Grid));
            grid.transform.SetParent(transform, false);
            var map = new GameObject("Tilemap", typeof(Tilemap), typeof(TilemapRenderer));
            map.transform.SetParent(grid.transform, false);
            var tilemap = map.GetComponent<Tilemap>();
            map.GetComponent<TilemapRenderer>().sortingOrder = -10;

            int bottom = _layout.LowestGround - GroundDepth;
            for (int x = 0; x < _layout.Width; x++)
            {
                int top = _layout.Ground[x];
                if (top == LevelLayout.NoGround)
                    continue;
                for (int y = bottom; y < top; y++)
                    tilemap.SetTile(new Vector3Int(x, y, 0), y == top - 1 ? _theme.groundTop : _theme.groundFill);
            }

            var body = map.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            var collider = map.AddComponent<TilemapCollider2D>();
            collider.compositeOperation = Collider2D.CompositeOperation.Merge;
            map.AddComponent<CompositeCollider2D>();
        }

        // Invisible walls before the start and after the end: nobody walks off the level.
        private void BuildWalls()
        {
            int top = 60;
            Wall("WallStart", new Vector2(-0.5f, top * 0.5f - GroundDepth), new Vector2(1f, top));
            Wall("WallEnd", new Vector2(_layout.Width + 0.5f, top * 0.5f - GroundDepth), new Vector2(1f, top));
        }

        private void Wall(string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(BoxCollider2D));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = position;
            go.GetComponent<BoxCollider2D>().size = size;
        }

        // ---------------- Segments ----------------

        private Transform Spawn(int index)
        {
            LevelSegment segment = _layout.Segments[index];
            var root = new GameObject($"Segment {index} {segment.Kind}").transform;
            root.SetParent(transform, false);

            foreach (BuildingPlacement placement in _layout.Buildings)
            {
                if (placement.Segment != index)
                    continue;
                DestructibleBuilding building = BuildingBuilder.Build(placement.Spec, _theme.tileset,
                    transform.position + new Vector3(placement.X, placement.GroundY, 0f), root);
                if (placement.Spec.Archetype == "gate")
                    building.CrumbleDetached = true;
            }

            foreach (PickupPlacement placement in _layout.Pickups)
            {
                if (placement.Segment != index || _pickupPrefab == null || (placement.Kind == PickupKind.Resurrect && !_coop))
                    continue;
                Pickup pickup = Instantiate(_pickupPrefab, transform.position + new Vector3(placement.X, placement.Y, 0f), Quaternion.identity, root);
                pickup.Configure(placement.Kind, placement.Amount);
            }

            if (_layout.FinishX >= segment.X0 && _layout.FinishX < segment.X1)
            {
                var finish = new GameObject("Finish", typeof(FinishLine), typeof(FinishLineView));
                finish.transform.SetParent(root, false);
                finish.transform.localPosition = new Vector3(_layout.FinishX, _layout.GroundAt(_layout.FinishX), 0f);
            }
            return root;
        }
    }
}
