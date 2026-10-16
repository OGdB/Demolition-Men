using System;
using System.Collections;
using Demolition.Run;
using UnityEngine;

namespace Demolition
{
    /// <summary>
    /// A spot in a level part that may spawn one random pickup on whatever is below it
    /// (ground or a roof). Resurrect is only rolled in co-op.
    /// shortcut: random placement per level part; replace with spec-driven placement in
    /// the level generator (roadmap M3).
    /// </summary>
    public sealed class PickupSlot : MonoBehaviour
    {
        [Serializable]
        private struct Entry
        {
            public PickupKind kind;
            public int amount;
            public float weight;
        }

        [SerializeField] private Pickup prefab;
        [SerializeField, Range(0f, 1f)] private float spawnChance = 0.7f;
        [SerializeField] private Entry[] table =
        {
            new Entry { kind = PickupKind.Dynamite, amount = 2, weight = 4f },
            new Entry { kind = PickupKind.Axe, amount = 25, weight = 3f },
            new Entry { kind = PickupKind.WreckingBall, amount = 20, weight = 2f },
            new Entry { kind = PickupKind.Resurrect, amount = 1, weight = 1f },
        };

        private IEnumerator Start()
        {
            // Buildings in this level part spawn in their own Start; let them land first.
            yield return null;
            if (prefab == null || UnityEngine.Random.value > spawnChance)
                yield break;

            bool coop = RunController.Current != null && RunController.Current.Players.Count > 1;
            float total = 0f;
            foreach (Entry e in table)
                if (coop || e.kind != PickupKind.Resurrect)
                    total += e.weight;

            float roll = UnityEngine.Random.value * total;
            foreach (Entry e in table)
            {
                if (!coop && e.kind == PickupKind.Resurrect)
                    continue;
                roll -= e.weight;
                if (roll > 0f)
                    continue;
                Pickup pickup = Instantiate(prefab, transform.position, Quaternion.identity);
                pickup.Configure(e.kind, e.amount);
                yield break;
            }
        }
    }
}
