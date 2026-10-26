using System.Collections;
using System.Collections.Generic;
using Demolition.Run;
using UnityEngine;

namespace Demolition.Actions
{
    /// <summary>
    /// Controlled demolition: charges on the footing of the next building ahead (its lowest
    /// row, supports first, both ends and the middle), a blinking 3-2-1, then they go off one
    /// after another like dynamite (<see cref="Dynamite.Detonate"/>) and the charged blocks are
    /// wrecked outright, so the building drops into its own footprint.
    /// </summary>
    public sealed class ImplosionCharges : MonoBehaviour
    {
        private const float Reach = 14f; // how far ahead a building counts
        private const float Count = 0.5f; // seconds per beep of the 3-2-1: ActionCatalog's frames hold the plunger up as long
        private const float Ripple = 0.12f;

        private readonly List<BuildingBlock> _charges = new List<BuildingBlock>();
        private readonly List<Vector2> _spots = new List<Vector2>();
        private readonly List<SpriteRenderer> _lights = new List<SpriteRenderer>();
        private int _who;
        private bool _authoritative;

        public static void Place(Vector2 at, int facing, int who, bool authoritative)
        {
            var go = new GameObject("ImplosionCharges");
            ImplosionCharges charges = go.AddComponent<ImplosionCharges>();
            charges._who = who;
            charges._authoritative = authoritative;
            charges.Choose(at, facing);
            charges.StartCoroutine(charges.Countdown());
        }

        private void Choose(Vector2 at, int facing)
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
                var picked = new List<BuildingBlock>();
                foreach (BuildingBlock b in footing)
                    if (picked.Count < 3 && picked.TrueForAll(p => Mathf.Abs(p.transform.position.x - b.transform.position.x) > 1.5f))
                        picked.Add(b);
                foreach (BuildingBlock b in picked)
                {
                    _charges.Add(b);
                    _spots.Add(b.transform.position);
                }
            }
            if (_spots.Count == 0) // nothing standing ahead: one charge in front, for the bang
            {
                _charges.Add(null);
                _spots.Add(ActionStrikes.GroundBelow(at + new Vector2(2f * facing, 1f)) + new Vector2(0f, 0.3f));
            }

            foreach (Vector2 spot in _spots)
            {
                var go = new GameObject("Charge");
                go.transform.SetParent(transform, false);
                go.transform.position = spot;
                go.transform.localScale = Vector3.one * 0.35f;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = PrimitiveSprite.Disc();
                sr.color = new Color(0.9f, 0.1f, 0.1f);
                sr.sortingOrder = 62;
                _lights.Add(sr);
            }
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
            for (int i = 0; i < _spots.Count; i++)
            {
                _lights[i].enabled = false;
                BuildingBlock charged = _charges[i];
                if (charged != null && (_authoritative || ActionStrikes.IsLoose(charged)))
                    charged.TakeDamage(ActionStrikes.Wreck, _who);
                Dynamite.Detonate(prefab, _spots[i], _who); // a client's goes off as the show (Dynamite.Cosmetic)
                CameraShake.Add(0.3f);
                yield return new WaitForSeconds(Ripple);
            }
            ActionAudio.Slam();
            Destroy(gameObject, 1f);
        }
    }
}
