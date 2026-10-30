using System.Collections.Generic;
using Demolition.Ranked;
using Demolition.Run;
using UnityEngine;

namespace Demolition.Actions
{
    /// <summary>
    /// What each action does to the world when it strikes, and the area helpers they share.
    /// Standing blocks only ever take damage (never forces: the support graph decides what
    /// falls), and only from an <c>authoritative</c> strike: offline, or the host's run of it.
    /// Debris (falling or settled) is each device's own, so every strike shoves and breaks it.
    /// </summary>
    public static class ActionStrikes
    {
        /// <summary>More than any block's health: the burp and the slam don't care what it's made of.</summary>
        public const float Wreck = RankedRules.Wreck;

        private static readonly HashSet<BuildingBlock> Seen = new HashSet<BuildingBlock>();
        private static readonly List<BuildingBlock> Found = new List<BuildingBlock>();

        /// <summary>What a strike picks in the world, decided once by the deciding device (the host
        /// online) and handed to <see cref="Run"/> on both: the charges' spots, where the radio
        /// stands, where the pallet lands. Null for strikes that pick nothing.</summary>
        public static Vector2[] Pick(ActionKind kind, Vector2 at, int facing)
        {
            switch (kind)
            {
                case ActionKind.Implosion: return ImplosionCharges.Pick(at, facing);
                case ActionKind.SiteRadio: return new[] { SiteRadio.Pick(at) };
                case ActionKind.CraneDrop: return new[] { CraneCall.Pick(at) };
                default: return null;
            }
        }

        /// <param name="spots">What <see cref="Pick"/> chose on the deciding device.</param>
        public static void Run(ActionUser actor, ActionKind kind, Vector2 at, int facing, bool authoritative, Vector2[] spots)
        {
            Vector2 spot = spots != null && spots.Length > 0 ? spots[0] : at;
            int who = actor.PlayerIndex;
            ActionInfo info = ActionCatalog.Get(kind);
            switch (kind)
            {
                case ActionKind.Burp:
                    BurpWave.Spawn(at, facing, who, authoritative);
                    ActionAudio.Burp();
                    CameraShake.Add(0.35f);
                    break;
                case ActionKind.ButtSlam:
                    Slam(actor, at, who, authoritative);
                    break;
                case ActionKind.CraneDrop:
                    CranePallet.Call(at, spot, who, authoritative);
                    break;
                case ActionKind.Implosion:
                    ImplosionCharges.Place(spots ?? ImplosionCharges.Pick(at, facing), who, authoritative);
                    break;
                case ActionKind.CoffeeBreak:
                    actor.Coffee(info.Duration);
                    break;
                case ActionKind.Intern:
                    Intern.Spawn(actor, at, facing, info.Duration, authoritative);
                    break;
                case ActionKind.HiVis:
                    actor.Vest(info.Duration);
                    break;
                case ActionKind.ShovelLean:
                    actor.Rest(info.Hold, 12f);
                    RunController run = RunController.Current;
                    if (run != null)
                        foreach (RunController.Player p in run.Players)
                            if (p.Index != who && p.Action != null)
                                p.Action.Boost(info.Duration); // the partner's own device applies it
                    break;
                case ActionKind.SiteRadio:
                    SiteRadio.Place(spot, facing, who, info.Duration, info.Frames[info.Frames.Length - 1], authoritative);
                    break;
            }
        }

        // ---------------- The butt slam ----------------

        // Straight down through every floor under the player, a ring of damage where they land,
        // the loose rubble around thrown off, the partner knocked aside.
        private static void Slam(ActionUser actor, Vector2 at, int who, bool authoritative)
        {
            Hit(BlocksInBox(at + new Vector2(0f, -RankedRules.SlamDrop - RankedRules.SlamDepth * 0.5f),
                new Vector2(RankedRules.SlamHalfWidth * 2f, RankedRules.SlamDepth)), Wreck, who, authoritative, HitCause.ButtSlam, at);
            Hit(BlocksInCircle(at + new Vector2(0f, RankedRules.SlamRingRise), RankedRules.SlamRingRadius), RankedRules.SlamRingDamage,
                who, authoritative, HitCause.ButtSlam, at);
            Shove(at, 3.5f, 10f, new Vector2(0f, 4f));
            KnockPlayers(at, 2.8f, 9f, actor.transform);
            Particles.Burst(at, new Color(0.75f, 0.7f, 0.6f, 0.9f), 40, 6f, 0.3f, 0.9f);
            Ring(at + new Vector2(0f, 0.2f), 5f, new Color(1f, 0.95f, 0.8f, 0.8f), 0.35f);
            CameraShake.Add(0.7f);
            ActionAudio.Slam();
        }

        // ---------------- Shared helpers ----------------

        /// <summary>Every block touching the box, once each.</summary>
        public static List<BuildingBlock> BlocksInBox(Vector2 centre, Vector2 size) =>
            Collect(Physics2D.OverlapBoxAll(centre, size, 0f));

        /// <summary>Every block touching the circle, once each.</summary>
        public static List<BuildingBlock> BlocksInCircle(Vector2 centre, float radius) =>
            Collect(Physics2D.OverlapCircleAll(centre, radius));

        private static List<BuildingBlock> Collect(Collider2D[] hits)
        {
            Seen.Clear();
            Found.Clear();
            foreach (Collider2D hit in hits)
            {
                BuildingBlock block = hit != null ? hit.GetComponentInParent<BuildingBlock>() : null;
                if (block != null && Seen.Add(block))
                    Found.Add(block);
            }
            return new List<BuildingBlock>(Found);
        }

        public static bool IsLoose(BuildingBlock block) => block.IsFalling || block.IsRubble;

        /// <summary>Damage the blocks: debris always (it's this device's), standing blocks only
        /// when <paramref name="authoritative"/> (a client's would reach the host twice).</summary>
        public static void Hit(List<BuildingBlock> blocks, float damage, int who, bool authoritative, HitCause cause, Vector2 from)
        {
            foreach (BuildingBlock block in blocks)
                if (block != null && (authoritative || IsLoose(block)))
                    block.TakeDamage(damage, who, cause, from);
        }

        /// <summary>Throw the debris in a circle away from its centre, harder nearer it, plus a lift.</summary>
        public static void Shove(Vector2 centre, float radius, float force, Vector2 lift)
        {
            foreach (BuildingBlock block in BlocksInCircle(centre, radius))
            {
                if (block == null || !IsLoose(block))
                    continue;
                Vector2 away = (Vector2)block.transform.position - centre;
                float near = 1f - Mathf.Clamp01(away.magnitude / radius) * 0.6f;
                block.Shove((away.normalized * force + lift) * near);
            }
        }

        /// <summary>Throw this device's players in a circle aside (not <paramref name="spare"/>): no
        /// damage, it's a shove. A copy of the other device's player is theirs to move.</summary>
        public static void KnockPlayers(Vector2 centre, float radius, float speed, Transform spare)
        {
            RunController run = RunController.Current;
            if (run == null)
                return;
            foreach (RunController.Player p in run.Players)
            {
                if (p.Transform == spare || p.Health.IsProxy || p.Health.IsDead)
                    continue;
                Vector2 away = (Vector2)p.Transform.position + Vector2.up - centre;
                if (away.magnitude > radius)
                    continue;
                away.y = Mathf.Max(away.y, 0.4f);
                p.Motor.Knockback(away.normalized * speed);
            }
        }

        /// <summary>An expanding, fading ring: a shock wave, a pulse of sound.</summary>
        public static void Ring(Vector2 at, float size, Color colour, float seconds)
        {
            var go = new GameObject("Ring");
            go.transform.position = at;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PrimitiveSprite.Ring();
            sr.color = colour;
            sr.sortingOrder = 61;
            go.AddComponent<SpriteFlash>().Configure(size, seconds);
        }

        /// <summary>Where a dropped thing lands below <paramref name="from"/>: the first solid,
        /// non-player surface, or <paramref name="from"/> itself if there's none within reach.</summary>
        public static Vector2 GroundBelow(Vector2 from, float reach = 30f)
        {
            foreach (RaycastHit2D hit in Physics2D.RaycastAll(from, Vector2.down, reach))
            {
                if (hit.collider == null || hit.collider.isTrigger || hit.collider.GetComponentInParent<PlayerHealth>() != null)
                    continue;
                Rigidbody2D body = hit.collider.attachedRigidbody;
                if (body == null || body.bodyType != RigidbodyType2D.Dynamic) // not something falling past
                    return hit.point;
            }
            return from;
        }
    }
}
