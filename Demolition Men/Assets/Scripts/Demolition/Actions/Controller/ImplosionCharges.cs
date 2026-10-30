using System.Collections;
using System.Collections.Generic;
using Demolition.Ranked;
using Demolition.Run;
using UnityEngine;

namespace Demolition.Actions
{
    /// <summary>
    /// Controlled demolition: charges on the footing of the next building ahead (its lowest
    /// row, supports first, both ends and the middle), a blinking 3-2-1, then they go off one
    /// after another like dynamite (<see cref="Dynamite.Detonate"/>) and the charged blocks are
    /// wrecked outright, so the building drops into its own footprint. The deciding device picks
    /// the charges (<see cref="Pick"/>); online the client places them where the host said.
    /// </summary>
    public sealed class ImplosionCharges : MonoBehaviour
    {
        private const float Reach = 14f; // how far ahead a building counts
        private const float Count = RankedRules.ChargeCountdown / 3f; // seconds per beep of the 3-2-1: ActionCatalog's frames hold the plunger up as long
        private const float Ripple = RankedRules.ChargeRipple;

        private Vector2[] _spots;
        private readonly List<SpriteRenderer> _lights = new List<SpriteRenderer>();
        private int _who;
        private bool _authoritative;

        /// <summary>Where the charges go: the footing blocks of the nearest building ahead, or one
        /// spot on the ground in front when nothing stands within reach.</summary>
        public static Vector2[] Pick(Vector2 at, int facing)
        {
            DestructibleBuilding target = null;
            float best = Reach;
            var blocks = new List<BuildingBlock>();
            foreach (DestructibleBuilding building in FindObjectsByType<DestructibleBuilding>(FindObjectsSortMode.None))
            {
                blocks.Clear();
                blocks.AddRange(building.StandingBlocks);
                if (blocks.Count == 0)
                    continue;
                float min = float.MaxValue, max = float.MinValue;
                foreach (BuildingBlock b in blocks)
                {
                    min = Mathf.Min(min, b.transform.position.x);
                    max = Mathf.Max(max, b.transform.position.x);
                }
                float ahead = facing > 0 ? min - at.x : at.x - max;
                bool inside = at.x >= min - 0.5f && at.x <= max + 0.5f;
                float distance = inside ? 0f : ahead;
                if (distance >= 0f && distance < best)
                {
                    best = distance;
                    target = building;
                }
            }

            var spots = new List<Vector2>();
            if (target != null)
            {
                blocks.Clear();
                blocks.AddRange(target.StandingBlocks);
                float floor = float.MaxValue;
                foreach (BuildingBlock b in blocks)
                    floor = Mathf.Min(floor, b.transform.position.y);
                var footing = blocks.FindAll(b => b.transform.position.y < floor + 0.6f && b.Material != BlockMaterial.Glass);
                // Supports carry the building: charge them first, then whatever is at the ends.
                footing.Sort((a, b) => Strength(b).CompareTo(Strength(a)));
                foreach (BuildingBlock b in footing)
                    if (spots.Count < ActionCall.MaxSpots && spots.TrueForAll(s => Mathf.Abs(s.x - b.transform.position.x) > 1.5f))
                        spots.Add(b.transform.position);
            }
            if (spots.Count == 0) // nothing standing ahead: one charge in front, for the bang
                spots.Add(ActionStrikes.GroundBelow(at + new Vector2(2f * facing, 1f)) + new Vector2(0f, 0.3f));
            return spots.ToArray();
        }

        public static void Place(Vector2[] spots, int who, bool authoritative)
        {
            if (spots == null || spots.Length == 0)
                return;
            var go = new GameObject("ImplosionCharges");
            ImplosionCharges charges = go.AddComponent<ImplosionCharges>();
            charges._spots = spots;
            charges._who = who;
            charges._authoritative = authoritative;
            foreach (Vector2 spot in spots)
            {
                var light = new GameObject("Charge");
                light.transform.SetParent(go.transform, false);
                light.transform.position = spot;
                light.transform.localScale = Vector3.one * 0.35f;
                var sr = light.AddComponent<SpriteRenderer>();
                sr.sprite = PrimitiveSprite.Disc();
                sr.color = new Color(0.9f, 0.1f, 0.1f);
                sr.sortingOrder = 62;
                charges._lights.Add(sr);
            }
            charges.StartCoroutine(charges.Countdown());
        }

        private static int Strength(BuildingBlock block) =>
            block.Material == BlockMaterial.Support ? 3 : block.Material == BlockMaterial.Metal ? 2 : 1;

        private IEnumerator Countdown()
        {
            for (int beep = 3; beep > 0; beep--)
            {
                ActionAudio.Beep(beep);
                foreach (SpriteRenderer light in _lights)
                    light.color = Color.red;
                yield return new WaitForSeconds(Count * 0.5f);
                foreach (SpriteRenderer light in _lights)
                    light.color = new Color(0.35f, 0.05f, 0.05f);
                yield return new WaitForSeconds(Count * 0.5f);
            }
            GameObject prefab = Levels.GameConfig.Instance != null ? Levels.GameConfig.Instance.dynamitePrefab : null;
            for (int i = 0; i < _spots.Length; i++)
            {
                _lights[i].enabled = false;
                // The block under the charge, whatever is there now: online the client's comes down with the host's cells.
                foreach (Collider2D hit in Physics2D.OverlapPointAll(_spots[i]))
                {
                    BuildingBlock charged = hit != null ? hit.GetComponentInParent<BuildingBlock>() : null;
                    if (charged != null && (_authoritative || ActionStrikes.IsLoose(charged)))
                        charged.TakeDamage(ActionStrikes.Wreck, _who, HitCause.Charge, _spots[i]);
                }
                Dynamite.Detonate(prefab, _spots[i], _who, 0f, HitCause.Charge); // a client's goes off as the show (Dynamite.Cosmetic)
                CameraShake.Add(0.3f);
                yield return new WaitForSeconds(Ripple);
            }
            ActionAudio.Slam();
            Destroy(gameObject, 1f);
        }
    }
}
