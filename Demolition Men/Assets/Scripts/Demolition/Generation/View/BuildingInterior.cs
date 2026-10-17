using System.Collections.Generic;
using UnityEngine;

namespace Demolition.Generation
{
    /// <summary>
    /// A generated building's back wall: one sprite per open or see-through cell
    /// (<see cref="BuildingSpec.Interior"/>), so rooms read as rooms instead of a frame
    /// against the sky. Cosmetic only: each piece hangs from a block cell and crumbles to
    /// dust when that cell leaves the structure (<see cref="DestructibleBuilding.CellsRemoved"/>).
    /// </summary>
    public sealed class BuildingInterior : MonoBehaviour
    {
        /// <summary>Behind the blocks (0); in front of the trench back wall at the same order.</summary>
        private const int SortingOrder = -1;
        private const float Forward = -0.01f;
        private const int MaxDustPerRemoval = 8;
        private static readonly Color Dust = new Color(0.45f, 0.42f, 0.4f);

        private readonly Dictionary<Vector2Int, List<GameObject>> _byHost = new Dictionary<Vector2Int, List<GameObject>>();

        public static void Build(DestructibleBuilding building, BuildingSpec spec, BuildingTileset tileset)
        {
            List<InteriorCell> cells = spec.Interior();
            if (cells.Count == 0)
                return; // a prop, a gate: no rooms
            var interior = building.gameObject.AddComponent<BuildingInterior>();
            var root = new GameObject("Interior").transform;
            root.SetParent(building.transform, false);
            foreach (InteriorCell cell in cells)
            {
                var go = new GameObject("Wall");
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3(cell.Cell.x + 0.5f, cell.Cell.y + 0.5f, Forward);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = cell.UnderCeiling ? tileset.InteriorTop : tileset.Interior;
                sr.sortingOrder = SortingOrder;
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
                foreach (GameObject go in hung)
                {
                    if (go == null)
                        continue;
                    if (dust++ < MaxDustPerRemoval)
                        Particles.Burst(go.transform.position, Dust, 4, 2f, 0.12f, 0.5f);
                    Destroy(go);
                }
            }
        }
    }
}
