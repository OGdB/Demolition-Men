using Demolition.Run;
using UnityEngine;

namespace Demolition
{
    public enum PickupKind { Axe, WreckingBall, Dynamite, Resurrect }

    /// <summary>
    /// Collected by touching it. Weapons add landed hits, dynamite adds sticks (not past
    /// the carry limit), Resurrect brings back a partner who is out. A pickup that can't
    /// be used right now stays where it is. It drops when whatever it rests on is gone,
    /// so pickups on a collapsing roof come down with it.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class Pickup : MonoBehaviour
    {
        [SerializeField] private PickupKind kind = PickupKind.Dynamite;
        [Tooltip("Weapon hits or dynamite sticks granted.")]
        [SerializeField] private int amount = 2;
        [SerializeField] private float gravity = 30f;
        [Tooltip("Distance from the pivot to the bottom of the pickup.")]
        [SerializeField] private float footOffset = 0.5f;

        private readonly RaycastHit2D[] _hits = new RaycastHit2D[8];
        private float _fallSpeed;
        private bool _collected;

        public PickupKind Kind => kind;

        public void Configure(PickupKind pickupKind, int pickupAmount)
        {
            kind = pickupKind;
            amount = pickupAmount;
        }

        private void FixedUpdate()
        {
            float step = (_fallSpeed + gravity * Time.fixedDeltaTime) * Time.fixedDeltaTime;
            Vector2 foot = (Vector2)transform.position + Vector2.down * footOffset;
            float ground = GroundDistance(foot, step + 0.05f);
            if (ground >= 0f)
            {
                _fallSpeed = 0f;
                transform.position += Vector3.down * Mathf.Max(0f, ground - 0.01f);
                return;
            }
            _fallSpeed += gravity * Time.fixedDeltaTime;
            transform.position += Vector3.down * step;
            if (transform.position.y < KillY)
                Destroy(gameObject); // fell into the void
        }

        private const float KillY = -100f;

        // Nearest solid surface below (ignoring players and triggers), -1 if none in range.
        private float GroundDistance(Vector2 from, float range)
        {
            int count = Physics2D.Raycast(from, Vector2.down, new ContactFilter2D { useTriggers = false }, _hits, range);
            float nearest = -1f;
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].collider.GetComponentInParent<PlayerHealth>() != null)
                    continue;
                if (nearest < 0f || _hits[i].distance < nearest)
                    nearest = _hits[i].distance;
            }
            return nearest;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            // A player has several colliders, so this can fire more than once per step.
            if (_collected)
                return;
            var combat = other.GetComponentInParent<PlayerCombat>();
            if (combat == null || !combat.isActiveAndEnabled || !TryApply(combat))
                return;
            _collected = true;
            Particles.Burst(transform.position, new Color(1f, 0.9f, 0.4f), 14, 4f, 0.12f, 0.5f);
            Destroy(gameObject);
        }

        private bool TryApply(PlayerCombat combat)
        {
            switch (kind)
            {
                case PickupKind.Axe:
                    combat.AddWeapon(WeaponType.Axe, amount);
                    return true;
                case PickupKind.WreckingBall:
                    combat.AddWeapon(WeaponType.WreckingBall, amount);
                    return true;
                case PickupKind.Dynamite:
                    return combat.AddDynamite(amount);
                case PickupKind.Resurrect:
                    return RunController.Current != null && RunController.Current.TryResurrect(combat.PlayerIndex);
                default:
                    return false;
            }
        }
    }
}
