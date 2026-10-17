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
        /// <summary>...and despawn this far behind its left edge, at least (more if the camera
        /// can zoom out further than it shows now, so nothing visibly pops away).</summary>
        private const float KeepBehind = 25f;
        /// <summary>Solid ground below the lowest surface, so pits and sewers have walls.</summary>
        private const int GroundDepth = 8;
        /// <summary>Street dressing sorts with the building back walls, a hair in front of them.</summary>
        private const int DecorOrder = -1;
        private const float DecorDepth = -0.02f;

        private LevelLayout _layout;
        private LevelTheme _theme;
        private Pickup _pickupPrefab;
        private bool _coop;
        private Transform[] _segments;
        private bool[] _despawned;
        private Camera _camera;
        private float _maxHalfWidth;

        public LevelLayout Layout => _layout;

        /// <summary>World y of the lowest ground between two world x (pits excluded),
        /// +infinity if there is none: what the camera needs to show floors below the street.</summary>
        public float LowestGroundBetween(float worldX0, float worldX1)
        {
            float ox = transform.position.x;
            int low = _layout.LowestGroundIn(Mathf.FloorToInt(worldX0 - ox), Mathf.FloorToInt(worldX1 - ox));
            return low == LevelLayout.NoGround ? float.PositiveInfinity : transform.position.y + low;
        }

        public void Build(LevelLayout layout, LevelTheme theme, Pickup pickupPrefab, bool coop)
        {
            _layout = layout;
            _theme = theme;
            _pickupPrefab = pickupPrefab;
            _coop = coop;
            _segments = new Transform[layout.Segments.Count];
            _despawned = new bool[layout.Segments.Count];
            Pickup.ForgetTaken();
            BuildGround();
            BuildWalls();
        }

        private void Update()
        {
            if (_layout == null)
                return;
            if (_camera == null)
            {
                _camera = Camera.main;
                ScrollCamera scroll = _camera != null ? _camera.GetComponent<ScrollCamera>() : null;
                _maxHalfWidth = scroll != null ? scroll.MaxZoom * _camera.aspect : 0f;
            }
            float left = 0f, right = float.MaxValue, behind = KeepBehind;
            if (_camera != null)
            {
                float halfWidth = _camera.orthographicSize * _camera.aspect;
                left = _camera.transform.position.x - halfWidth;
                right = _camera.transform.position.x + halfWidth;
                behind = Mathf.Max(KeepBehind, _maxHalfWidth * 2f - halfWidth);
            }
            Stream(left, right, behind);
        }

        /// <summary>Spawn everything that should exist with the camera spanning [left, right].</summary>
        public void Stream(float left, float right, float behind = KeepBehind)
        {
            for (int i = 0; i < _layout.Segments.Count; i++)
            {
                LevelSegment segment = _layout.Segments[i];
                if (_segments[i] == null && !_despawned[i] && segment.X0 < right + LookAhead)
                    _segments[i] = Spawn(i);
                if (_segments[i] != null && segment.X1 < left - behind)
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
            // Same order as the hand-made level parts: in front of the sky (-5) and backdrop (-3..-1).
            map.GetComponent<TilemapRenderer>().sortingOrder = 0;

            int bottom = _layout.LowestGround - GroundDepth;
            for (int x = 0; x < _layout.Width; x++)
            {
                int top = _layout.Ground[x];
                if (top == LevelLayout.NoGround)
                    continue;
                for (int y = bottom; y < top; y++)
                    tilemap.SetTile(new Vector3Int(x, y, 0), y == top - 1 ? _theme.groundTop : _theme.groundFill);
            }

            BuildBackWall(grid.transform, bottom);

            var body = map.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            var collider = map.AddComponent<TilemapCollider2D>();
            collider.compositeOperation = Collider2D.CompositeOperation.Merge;
            // Solid polygons, not outline edges: anything spawned or knocked inside the
            // ground surface is pushed out instead of falling through a hollow shell.
            map.AddComponent<CompositeCollider2D>().geometryType = CompositeCollider2D.GeometryType.Polygons;
        }

        // Trenches cut below the street would show sky (the skyline starts at the street): a
        // darker, collider-free wall of fill tiles closes them from behind. Pits must read
        // differently from trenches: below their lips they are near-black, bottomless holes,
        // and both lips get hazard stripes.
        private void BuildBackWall(Transform grid, int bottom)
        {
            int street = _layout.GroundAt(_layout.SpawnX);
            Tilemap wall = BackTilemap(grid, "BackWall", new Color(0.55f, 0.55f, 0.6f));
            Tilemap abyss = BackTilemap(grid, "Abyss", new Color(0.06f, 0.05f, 0.08f));
            for (int x = 0; x < _layout.Width; x++)
            {
                int top = _layout.Ground[x];
                if (top != LevelLayout.NoGround)
                {
                    for (int y = top; y < street; y++)
                        wall.SetTile(new Vector3Int(x, y, 0), _theme.groundFill);
                    continue;
                }
                int lip = PitLip(x);
                for (int y = bottom; y < Mathf.Max(lip, street); y++)
                    (y < lip ? abyss : wall).SetTile(new Vector3Int(x, y, 0), _theme.groundFill);
            }
            MarkPitEdges();
        }

        private static Tilemap BackTilemap(Transform grid, string name, Color tint)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(grid, false);
            var tilemap = go.GetComponent<Tilemap>();
            tilemap.color = tint;
            go.GetComponent<TilemapRenderer>().sortingOrder = -1;
            return tilemap;
        }

        // The lower of the ground heights on either side of the pit column x.
        private int PitLip(int x)
        {
            int left = x, right = x;
            while (left >= 0 && _layout.Ground[left] == LevelLayout.NoGround) left--;
            while (right < _layout.Width && _layout.Ground[right] == LevelLayout.NoGround) right++;
            int l = left >= 0 ? _layout.Ground[left] : int.MaxValue;
            int r = right < _layout.Width ? _layout.Ground[right] : int.MaxValue;
            return Mathf.Min(l, r);
        }

        // Hazard stripes along the walkable top of the columns either side of every pit.
        private void MarkPitEdges()
        {
            for (int x = 0; x < _layout.Width; x++)
            {
                bool pit = _layout.Ground[x] == LevelLayout.NoGround;
                bool leftLip = !pit && x + 1 < _layout.Width && _layout.Ground[x + 1] == LevelLayout.NoGround;
                bool rightLip = !pit && x > 0 && _layout.Ground[x - 1] == LevelLayout.NoGround;
                if (!leftLip && !rightLip)
                    continue;
                var go = new GameObject("PitEdge", typeof(SpriteRenderer));
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(x + 0.5f, _layout.Ground[x] - 0.125f, 0f);
                var sr = go.GetComponent<SpriteRenderer>();
                sr.sprite = PrimitiveSprite.Hazard();
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.size = new Vector2(1f, 0.25f);
                sr.sortingOrder = 1;
            }
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

            for (int b = 0; b < _layout.Buildings.Count; b++)
            {
                BuildingPlacement placement = _layout.Buildings[b];
                if (placement.Segment != index)
                    continue;
                DestructibleBuilding building = BuildingBuilder.Build(placement.Spec, _theme.tileset,
                    transform.position + new Vector3(placement.X, placement.GroundY, 0f), root);
                building.NetIndex = b;
                if (placement.Spec.Archetype == "gate")
                    building.CrumbleDetached = true;
            }

            for (int p = 0; p < _layout.Pickups.Count; p++)
            {
                PickupPlacement placement = _layout.Pickups[p];
                if (placement.Segment != index || _pickupPrefab == null || (placement.Kind == PickupKind.Resurrect && !_coop) || Pickup.WasTaken(p))
                    continue;
                Pickup pickup = Instantiate(_pickupPrefab, transform.position + new Vector3(placement.X, placement.Y, 0f), Quaternion.identity, root);
                pickup.Configure(placement.Kind, placement.Amount);
                pickup.NetIndex = p;
            }

            foreach (DecorPlacement decor in _layout.Decor)
            {
                if (decor.Segment != index)
                    continue;
                Sprite sprite = _theme.Decor(decor.Kind, decor.Variant);
                if (sprite == null)
                    continue;
                var go = new GameObject(decor.Kind.ToString(), typeof(SpriteRenderer));
                go.transform.SetParent(root, false);
                // Standing on the ground, behind the players and the blocks (cosmetic: no collider).
                go.transform.localPosition = new Vector3(decor.X + 0.5f, decor.GroundY + sprite.bounds.extents.y - sprite.bounds.center.y, DecorDepth);
                var sr = go.GetComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = DecorOrder;
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
