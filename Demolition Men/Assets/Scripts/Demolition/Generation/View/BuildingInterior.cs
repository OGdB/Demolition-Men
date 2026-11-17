using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Demolition.Generation
{
    /// <summary>
    /// A generated building's back wall: one sprite per open or see-through cell and behind
    /// every window (<see cref="BuildingSpec.Interior"/>), so rooms read as rooms instead of a
    /// frame against the sky; one wallpaper per storey, and the props the archetype furnishes
    /// its rooms and its roof with (<see cref="BuildingSpec.Props"/>).
    /// Cosmetic only: each piece goes with a block cell and goes when that cell leaves the
    /// structure (<see cref="DestructibleBuilding.CellsRemoved"/>): in dust when the block broke,
    /// dropping and fading with the collapse when it came loose.
    /// </summary>
    public sealed class BuildingInterior : MonoBehaviour
    {
        /// <summary>Behind the blocks (0); in front of the trench back wall at the same order.</summary>
        private const int SortingOrder = -1;
        private const float Forward = -0.01f;
        /// <summary>A prop sits a hair before the wall pieces (same sorting order).</summary>
        private const float PropForward = -0.004f;
        private const int MaxDustPerRemoval = 8;
        private const float DropSeconds = 0.6f;
        private static readonly Color Dust = new Color(0.45f, 0.42f, 0.4f);

        private readonly Dictionary<Vector2Int, List<GameObject>> _byHost = new Dictionary<Vector2Int, List<GameObject>>();
        private DestructibleBuilding _building;

        /// <summary><paramref name="lights"/>: the building's electrics, to give lit props their
        /// glow; null where the theme's buildings are dark.</summary>
        public static void Build(DestructibleBuilding building, BuildingSpec spec, BuildingTileset tileset, BuildingLights lights = null)
        {
            List<InteriorCell> cells = spec.Interior();
            if (cells.Count == 0)
                return; // a prop, a gate: no rooms
            var interior = building.gameObject.AddComponent<BuildingInterior>();
            interior._building = building;
            var root = new GameObject("Interior").transform;
            root.SetParent(building.transform, false);
            foreach (InteriorCell cell in cells)
            {
                var go = new GameObject("Wall");
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3(cell.Cell.x + 0.5f, cell.Cell.y + 0.5f, Forward);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = tileset.InteriorFor(cell.Storey, cell.UnderCeiling, spec.Seed);
                sr.sortingOrder = SortingOrder;
                interior.Hang(cell.Host, go);
            }
            foreach (SpecProp prop in spec.Props)
            {
                BuildingTileset.Prop look = tileset.PropFor(prop.Name);
                if (look == null)
                    continue; // not drawn in this theme
                var go = new GameObject(prop.Name);
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3(prop.Rect.center.x, prop.Rect.center.y + prop.Lift, Forward + PropForward);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = look.sprite;
                sr.sortingOrder = SortingOrder;
                interior.Hang(prop.Host, go);
                if (lights != null && look.lit != null && spec.Glows(prop))
                    lights.Add(go.transform, prop, look, SortingOrder);
            }
            building.CellsRemoved += interior.OnCellsRemoved;
        }

        private void Hang(Vector2Int host, GameObject go)
        {
            if (!_byHost.TryGetValue(host, out List<GameObject> hung))
                _byHost[host] = hung = new List<GameObject>();
            hung.Add(go);
        }

        private void OnCellsRemoved(List<Vector2Int> cells)
        {
            int dust = 0;
            foreach (Vector2Int cell in cells)
            {
                if (!_byHost.TryGetValue(cell, out List<GameObject> hung))
                    continue;
                _byHost.Remove(cell);
                // Still registered: the host came loose (a broken one is already gone) and falls.
                bool fell = _building.TryGetBlock(cell, out _);
                foreach (GameObject go in hung)
                {
                    if (go == null)
                        continue;
                    if (fell)
                    {
                        StartCoroutine(Drop(go));
                        continue;
                    }
                    if (dust++ < MaxDustPerRemoval)
                        Particles.Burst(go.transform.position, Dust, 4, 2f, 0.12f, 0.5f);
                    Destroy(go);
                }
            }
        }

        // Down with the collapse, faster and fainter, then gone.
        private IEnumerator Drop(GameObject go)
        {
            Transform t = go.transform;
            SpriteRenderer[] renderers = go.GetComponentsInChildren<SpriteRenderer>();
            Vector3 start = t.localPosition;
            for (float age = 0f; age < DropSeconds && go != null; age += Time.deltaTime)
            {
                t.localPosition = start + Vector3.down * (0.5f * 9.81f * age * age);
                foreach (SpriteRenderer sr in renderers)
                {
                    Color c = sr.color;
                    c.a = 1f - age / DropSeconds;
                    sr.color = c;
                }
                yield return null;
            }
            if (go != null)
                Destroy(go);
        }
    }
}
