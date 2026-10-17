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
            bool jump = _motor.IsGrounded && !attack && (StepAhead(feet) || PitAhead(feet));

            if (feet.x > _progressX + 0.5f)
            {
                _progressX = feet.x;
                _progressTime = Time.time;
            }
            else if (Time.time - _progressTime > StuckSeconds && _motor.IsGrounded)
            {
                jump = true;
                _progressTime = Time.time;
            }

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
        private static bool StepAhead(Vector2 feet)
        {
            RaycastHit2D hit = Physics2D.Raycast(feet + new Vector2(0.9f, 0.3f), Vector2.right, 0.6f);
            return hit.collider != null && !hit.collider.isTrigger && hit.collider.GetComponentInParent<BuildingBlock>() == null
                   && hit.collider.GetComponentInParent<PlayerHealth>() == null;
        }

        private static bool PitAhead(Vector2 feet)
        {
            RaycastHit2D hit = Physics2D.Raycast(feet + new Vector2(1.2f, 0.2f), Vector2.down, 4f);
            return hit.collider == null;
        }
    }
}
