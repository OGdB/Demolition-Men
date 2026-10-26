using UnityEngine;

namespace Demolition.Actions
{
    /// <summary>
    /// The intern: a smaller, greener copy of the player (the same animator, so the same run and
    /// punch) who runs ahead and punches whatever is in the way until the shift ends. Its punches
    /// land on the Punching clip's own <c>applyDamage</c> events, credited to the player who sent it.
    /// Online the other device runs a copy that only looks busy (the host's does the damage).
    /// </summary>
    public sealed class Intern : MonoBehaviour
    {
        private const float Scale = 0.8f;
        private const float RunSpeed = 6f;
        private const float JumpSpeed = 13f;
        private static readonly Color Green = new Color(0.78f, 1f, 0.72f); // a groentje: the new kid on site

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int InAirParam = Animator.StringToHash("InAir");
        private static readonly int PunchParam = Animator.StringToHash("IsPunching");

        private Rigidbody2D _rb;
        private Animator _anim;
        private int _facing, _who;
        private float _damage;
        private bool _authoritative;
        private float _end, _stuckSince;
        private bool _punching;

        public static void Spawn(ActionUser sender, Vector2 at, int facing, float seconds, bool authoritative)
        {
            var go = new GameObject("Intern");
            go.transform.position = at + new Vector2(-0.6f * facing, 0.1f);
            go.transform.localScale = Vector3.one * Scale;
            var sr = go.AddComponent<SpriteRenderer>();
            SpriteRenderer look = sender.Sprite;
            if (look != null)
            {
                sr.sprite = look.sprite;
                sr.sortingLayerID = look.sortingLayerID;
                sr.sortingOrder = look.sortingOrder - 1;
            }
            else
            {
                sr.sprite = PrimitiveSprite.Unit();
            }
            sr.color = Green;
            sr.flipX = facing < 0;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.mass = 3f;
            rb.gravityScale = 3f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var body = go.AddComponent<BoxCollider2D>();
            var senderBody = sender.GetComponent<BoxCollider2D>();
            body.size = senderBody != null ? senderBody.size : new Vector2(0.8f, 1.6f);
            body.offset = senderBody != null ? senderBody.offset : new Vector2(0f, 0.8f);
            // The crew walk through each other.
            foreach (Collider2D own in go.GetComponents<Collider2D>())
                foreach (PlayerHealth player in FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None))
                    foreach (Collider2D theirs in player.GetComponentsInChildren<Collider2D>(true))
                        Physics2D.IgnoreCollision(own, theirs);

            Intern intern = go.AddComponent<Intern>();
            if (sender.Animator != null && sender.Animator.runtimeAnimatorController != null)
            {
                intern._anim = go.AddComponent<Animator>();
                intern._anim.runtimeAnimatorController = sender.Animator.runtimeAnimatorController;
            }
            intern._rb = rb;
            intern._facing = facing;
            intern._who = sender.PlayerIndex;
            intern._damage = sender.Combat != null ? sender.Combat.DamageMultiplier : 1f;
            intern._authoritative = authoritative;
            intern._end = Time.time + seconds;
            Particles.Burst(go.transform.position + Vector3.up, Green, 12, 3f, 0.14f, 0.5f);
            ActionAudio.Pop();
        }

        private Vector2 Reach => (Vector2)transform.position + new Vector2(_facing * 1f, 1.05f);

        private void FixedUpdate()
        {
            if (Time.time >= _end || transform.position.y < -40f)
            {
                Particles.Burst(transform.position + Vector3.up, Green, 16, 3.5f, 0.16f, 0.6f);
                ActionAudio.Pop();
                Destroy(gameObject);
                return;
            }

            _punching = false;
            foreach (BuildingBlock block in ActionStrikes.BlocksInBox(Reach, new Vector2(0.7f, 1.3f)))
                if (block != null && !block.IsFalling)
                    _punching = true;

            bool grounded = Mathf.Abs(_rb.linearVelocity.y) < 0.05f;
            if (_punching)
            {
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                _stuckSince = Time.time;
            }
            else
            {
                _rb.linearVelocity = new Vector2(_facing * RunSpeed, _rb.linearVelocity.y);
                if (Mathf.Abs(_rb.linearVelocity.x) > 1f || !grounded)
                    _stuckSince = Time.time;
                else if (Time.time - _stuckSince > 0.25f)
                {
                    _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, JumpSpeed); // a step or a kerb: hop it
                    _stuckSince = Time.time;
                }
            }

            if (_anim != null)
            {
                _anim.SetFloat(SpeedParam, _punching ? 0f : 1f);
                _anim.SetBool(InAirParam, !grounded);
                _anim.SetBool(PunchParam, _punching);
            }
        }

        // Animation event on the Punching clips (the name is the player's contract).
        private void applyDamage()
        {
            foreach (BuildingBlock block in ActionStrikes.BlocksInCircle(Reach, 0.9f * Scale + 0.2f))
            {
                if (block == null || (!_authoritative && !ActionStrikes.IsLoose(block)))
                    continue;
                float damage = block.Material == BlockMaterial.Metal || block.Material == BlockMaterial.Support ? 12f : 28f;
                block.TakeDamage(damage * _damage, _who);
            }
        }
    }
}
