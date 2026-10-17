using UnityEngine;

namespace Demolition
{
    /// <summary>
    /// A simple autopilot for a player (roadmap M3 bot playtester): runs right, hits
    /// whatever building block is in the way, jumps up steps and over pits, throws dynamite
    /// at load-bearing columns, and hops when it has been stuck for a moment. Drives the
    /// player through a <see cref="ScriptedInputSource"/>, exactly like a real input device.
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader), typeof(PlayerMotor), typeof(PlayerCombat))]
    public sealed class BotPilot : MonoBehaviour
    {
        private const float StuckSeconds = 1.2f;
        /// <summary>Hold jump this long: a one-frame tap can fall between physics steps.</summary>
        private const float JumpHold = 0.25f;

        private static readonly ContactFilter2D Solid = new ContactFilter2D { useTriggers = false };
        private readonly RaycastHit2D[] _hits = new RaycastHit2D[4];
        private float _jumpUntil;

        // A new jump only once the last one has been let go for a moment (a fresh press edge).
        private bool CanArmJump => Time.time >= _jumpUntil + 0.05f;

        private readonly ScriptedInputSource _source = new ScriptedInputSource();
        private PlayerMotor _motor;
        private PlayerCombat _combat;
        private float _progressX;
        private float _progressTime;
        private float _nextDynamite;
        private bool _jumpHeld;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _combat = GetComponent<PlayerCombat>();
        }

        // In Start: the run assigns devices to players when it wakes, after this was added.
        private void Start() => GetComponent<PlayerInputReader>().Source = _source;

        private void Update()
        {
            Vector2 feet = transform.position;
            BuildingBlock block = BlockAhead(feet);
            bool attack = block != null;
            if (CanArmJump && _motor.IsGrounded && !attack && (StepAhead(feet) || PitAhead(feet)))
                _jumpUntil = Time.time + JumpHold;

            if (feet.x > _progressX + 0.5f)
            {
                _progressX = feet.x;
                _progressTime = Time.time;
            }
            else if (Time.time - _progressTime > StuckSeconds && _motor.IsGrounded && CanArmJump)
            {
                _jumpUntil = Time.time + JumpHold;
                _progressTime = Time.time;
            }
            bool jump = Time.time < _jumpUntil;

            if (block != null && block.Material == BlockMaterial.Support && _combat.DynamiteCount > 0 && Time.time >= _nextDynamite)
            {
                _source.Next.DynamitePressed = true;
                _nextDynamite = Time.time + 3f;
            }

            _source.Next.Move = new Vector2(1f, 0f);
            _source.Next.AttackHeld = attack;
            _source.Next.JumpPressed = jump && !_jumpHeld;
            _source.Next.JumpReleased = !jump && _jumpHeld;
            _source.Next.JumpHeld = jump;
            _jumpHeld = jump;
        }

        // A standing block at body height just in front.
        private static BuildingBlock BlockAhead(Vector2 feet)
        {
            foreach (Collider2D hit in Physics2D.OverlapBoxAll(feet + new Vector2(1.1f, 1.1f), new Vector2(0.8f, 1.6f), 0f))
            {
                var block = hit.GetComponentInParent<BuildingBlock>();
                if (block != null && !block.IsFalling)
                    return block;
            }
            return null;
        }

        // Solid ground rising in front of the feet (not a building: those get hit).
        private bool StepAhead(Vector2 feet)
        {
            int count = Physics2D.Raycast(feet + new Vector2(0.9f, 0.3f), Vector2.right, Solid, _hits, 0.6f);
            for (int i = 0; i < count; i++)
                if (_hits[i].collider.GetComponentInParent<BuildingBlock>() == null
                    && _hits[i].collider.GetComponentInParent<PlayerHealth>() == null)
                    return true;
            return false;
        }

        private bool PitAhead(Vector2 feet) =>
            Physics2D.Raycast(feet + new Vector2(1.2f, 0.2f), Vector2.down, Solid, _hits, 4f) == 0;
    }
}
