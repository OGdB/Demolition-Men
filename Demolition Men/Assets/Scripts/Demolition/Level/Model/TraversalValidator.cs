using UnityEngine;

namespace Demolition.Levels
{
    /// <summary>
    /// A player's jump as numbers, matching <see cref="PlayerMotor"/> and the player
    /// prefabs' Rigidbody2D (mass 3, linear damping 1, gravity scale 2.2): impulse up,
    /// world gravity scaled, extra "better jump" gravity on the way down, Box2D-style damping.
    /// </summary>
    public struct JumpProfile
    {
        public float RunSpeed;
        public float JumpImpulse;
        public float Mass;
        public float Gravity;
        public float GravityScale;
        public float LinearDamping;
        public float FallMultiplier;
        public float Step;

        public static JumpProfile Default => new JumpProfile
        {
            RunSpeed = 8f,
            JumpImpulse = 45f,
            Mass = 3f,
            Gravity = 9.81f,
            GravityScale = 2.2f,
            LinearDamping = 1f,
            FallMultiplier = 3.8f,
            Step = 0.02f,
        };

        /// <summary>Highest the feet get above take-off.</summary>
        public float Apex
        {
            get
            {
                Simulate(float.NegativeInfinity, out float apex);
                return apex;
            }
        }

        /// <summary>Horizontal distance run-jumping covers before landing <paramref name="rise"/>
        /// units above take-off (negative = below); -1 when that height is out of reach.</summary>
        public float ReachAt(float rise) => Simulate(rise, out _);

        private float Simulate(float rise, out float apex)
        {
            float x = 0f, y = 0f, vy = JumpImpulse / Mass;
            apex = 0f;
            float damping = 1f / (1f + Step * LinearDamping);
            for (int i = 0; i < 1000; i++)
            {
                if (vy < 0f)
                    vy -= Gravity * (FallMultiplier - 1f) * Step;
                vy = (vy - Gravity * GravityScale * Step) * damping;
                y += vy * Step;
                x += RunSpeed * Step;
                apex = Mathf.Max(apex, y);
                if (vy < 0f && y <= rise)
                    return apex >= rise ? x : -1f;
                if (y < -50f)
                    break;
            }
            return -1f;
        }
    }

    public sealed class TraversalReport
    {
        public bool Passable = true;
        public int MaxRise;
        public int MaxGap;
        public string Problem;

        public override string ToString() => Passable
            ? $"passable (max rise {MaxRise}, max gap {MaxGap})"
            : $"BLOCKED: {Problem}";
    }

    /// <summary>
    /// Can a player walk and jump the ground from spawn to finish? Every step up must be
    /// within a safe share of the jump's apex, every pit within a safe share of the run-jump
    /// reach. Buildings don't count as obstacles: players demolish their way through.
    /// </summary>
    public static class TraversalValidator
    {
        /// <summary>Use only this share of the ideal jump, so touch players make it too.</summary>
        public const float Safety = 0.75f;
        /// <summary>Roughly the player's width: a landing needs the whole body past the edge.</summary>
        public const float BodyWidth = 0.8f;

        public static int MaxRise(JumpProfile jump) => Mathf.FloorToInt(jump.Apex * Safety);

        /// <summary>Widest pit (in columns) that can be cleared landing <paramref name="rise"/> higher.</summary>
        public static int MaxGap(JumpProfile jump, int rise)
        {
            float reach = jump.ReachAt(rise);
            return reach < 0f ? -1 : Mathf.FloorToInt(reach * Safety - BodyWidth);
        }

        public static TraversalReport Validate(LevelLayout layout, JumpProfile jump)
        {
            var report = new TraversalReport();
            int maxRise = MaxRise(jump);
            int x = layout.SpawnX;
            if (layout.GroundAt(x) == LevelLayout.NoGround)
                return Fail(report, $"no ground at spawn x={x}");

            while (x < layout.FinishX)
            {
                int here = layout.GroundAt(x);
                int next = x + 1;
                while (next < layout.Width && layout.GroundAt(next) == LevelLayout.NoGround)
                    next++;
                if (next >= layout.Width)
                    return Fail(report, $"no ground after x={x}");

                int rise = layout.GroundAt(next) - here;
                int gap = next - x - 1;
                report.MaxRise = Mathf.Max(report.MaxRise, rise);
                report.MaxGap = Mathf.Max(report.MaxGap, gap);
                if (rise > maxRise)
                    return Fail(report, $"step up of {rise} at x={x} (max {maxRise})");
                if (gap > 0 && gap > MaxGap(jump, rise))
                    return Fail(report, $"pit of {gap} at x={x + 1} (max {MaxGap(jump, rise)} rising {rise})");
                x = next;
            }
            return report;
        }

        private static TraversalReport Fail(TraversalReport report, string problem)
        {
            report.Passable = false;
            report.Problem = problem;
            return report;
        }
    }
}
