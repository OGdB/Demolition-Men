using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Demolition.Generation
{
    /// <summary>
    /// A generated building's back wall: one sprite per open or see-through cell and behind
    /// every window (<see cref="BuildingSpec.Interior"/>), so rooms read as rooms instead of a
    /// frame against the sky; one wallpaper per storey, a picture or a shelf on some room cells.
    /// Cosmetic only: each piece hangs from a block cell and goes when that cell leaves the
    /// structure (<see cref="DestructibleBuilding.CellsRemoved"/>): in dust when the block broke,
    /// dropping and fading with the collapse when it came loose.
    /// </summary>
    public sealed class BuildingInterior : MonoBehaviour
    {
        /// <summary>Behind the blocks (0); in front of the trench back wall at the same order.</summary>
        private const int SortingOrder = -1;
        private const float Forward = -0.01f;
        /// <summary>A detail sits a hair before its wall piece (same sorting order).</summary>
        private const float DetailForward = -0.004f;
        private const int MaxDustPerRemoval = 8;
        private const float DropSeconds = 0.6f;
        private static readonly Color Dust = new Color(0.45f, 0.42f, 0.4f);

        private readonly Dictionary<Vector2Int, List<GameObject>> _byHost = new Dictionary<Vector2Int, List<GameObject>>();
        private DestructibleBuilding _building;

        public static void Build(DestructibleBuilding building, BuildingSpec spec, BuildingTileset tileset)
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
                Sprite detail = cell.SeeThrough ? null : tileset.InteriorDetail(cell.Cell.x, cell.Cell.y, spec.Seed);
                if (detail != null)
                {
                    var d = new GameObject("Detail", typeof(SpriteRenderer));
                    d.transform.SetParent(go.transform, false);
                    d.transform.localPosition = new Vector3(0f, 0f, DetailForward);
                    d.GetComponent<SpriteRenderer>().sprite = detail;
                    d.GetComponent<SpriteRenderer>().sortingOrder = SortingOrder;
                }
                if (!interior._byHost.TryGetValue(cell.Host, out List<GameObject> hung))
                    interior._byHost[cell.Host] = hung = new List<GameObject>();
                hung.Add(go);
            }
            building.CellsRemoved += interior.OnCellsRemoved;
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
