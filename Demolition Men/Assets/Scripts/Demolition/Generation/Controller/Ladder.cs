using System.Collections.Generic;
using UnityEngine;

namespace Demolition.Generation
{
    /// <summary>
    /// A ladder of a generated building (<see cref="SpecLadder"/>): a trigger a player climbs in
    /// by holding up (<see cref="PlayerMotor"/>), drawn rung by rung behind the blocks. Not a
    /// block: nothing hits it and it holds nothing up. It goes when the block cell it is fixed
    /// to leaves the structure.
    /// </summary>
    public sealed class Ladder : MonoBehaviour
    {
        private const float Width = 0.6f;
        /// <summary>The trigger reaches this far over the top, so a player standing there still holds on.</summary>
        private const float Over = 0.5f;
        private const int SortingOrder = -1;
        /// <summary>Before the back wall and its props, which share its sorting order.</summary>
        private const float Forward = -0.02f;
        private static readonly Color Dust = new Color(0.45f, 0.42f, 0.4f);

        private DestructibleBuilding _building;
        private Vector2Int _host;

        /// <summary>World height of the floor it leads to: a climber's feet stop here.</summary>
        public float Top { get; private set; }

        public static void Build(DestructibleBuilding building, SpecLadder spec, Sprite rung)
        {
            var go = new GameObject("Ladder");
            go.transform.SetParent(building.transform, false);
            go.transform.localPosition = new Vector3(spec.X + 0.5f, spec.Y, Forward);
            var ladder = go.AddComponent<Ladder>();
            ladder._building = building;
            ladder._host = spec.Host;
            ladder.Top = building.transform.position.y + spec.Top;

            float height = spec.Top - spec.Y + Over;
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(Width, height);
            box.offset = new Vector2(0f, height * 0.5f);

            for (int y = 0; rung != null && y < spec.Top - spec.Y; y++)
            {
                var piece = new GameObject("Rung", typeof(SpriteRenderer));
                piece.transform.SetParent(go.transform, false);
                piece.transform.localPosition = new Vector3(0f, y + 0.5f, 0f);
                var sr = piece.GetComponent<SpriteRenderer>();
                sr.sprite = rung;
                sr.sortingOrder = SortingOrder;
            }
            building.CellsRemoved += ladder.OnCellsRemoved;
        }

        private void OnDestroy()
        {
            if (_building != null)
                _building.CellsRemoved -= OnCellsRemoved;
        }

        private void OnCellsRemoved(List<Vector2Int> cells)
        {
            if (!cells.Contains(_host))
                return;
            Particles.Burst(transform.position + Vector3.up * (Top - transform.position.y) * 0.5f, Dust, 8, 2f, 0.12f, 0.5f);
            Destroy(gameObject);
        }
    }
}
