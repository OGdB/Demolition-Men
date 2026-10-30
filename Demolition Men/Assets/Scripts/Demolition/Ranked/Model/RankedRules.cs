namespace Demolition.Ranked
{
    /// <summary>
    /// The game's numbers the server checks a ranked run with (docs/ranked.md §4, §7): copied from
    /// the player prefab, the swing clips and the controllers, which can't be read outside Unity.
    /// RankedRulesTests reads the prefab and the clips and fails when they drift apart. Change one
    /// here and in the game together, and bump <see cref="Version"/>: the server checks with one
    /// version and sends older builds to the store.
    /// </summary>
    public static class RankedRules
    {
        public const int Version = 1;

        // ---------------- Players (Player_1.prefab: PlayerMotor, PlayerCombat; Run.prefab) ----------------

        public const float RunSpeed = 8f;
        /// <summary>Jump impulse over mass: the take-off speed.</summary>
        public const float JumpSpeed = 45f / 3f;
        public const float Gravity = 9.81f * 2.2f;
        public const float AttackOffsetX = 1.25f, AttackOffsetY = 1.3f, AttackRadius = 1f;
        public const float HeadbuttFactor = 2f, HeadHeight = 1.2f;
        public const int MaxDynamite = 9;
        /// <summary>Where a stick leaves the hand, in front of the feet.</summary>
        public const float ThrowOffsetX = 1f, ThrowOffsetY = 1.3f;
        public const int RejoinPenalty = 500;
        public const float RejoinOffsetX = -1.5f, RejoinOffsetY = 1f;

        /// <summary>Damage per material in <see cref="BlockMaterial"/> order (brick, metal, wood, glass,
        /// support), as PlayerCombat's profiles.</summary>
        public static readonly float[] Fist = { 12f, 5f, 35f, 25f, 8f };
        public static readonly float[] Axe = { 40f, 20f, 70f, 25f, 25f };
        public static readonly float[] WreckingBall = { 65f, 40f, 45f, 25f, 50f };

        /// <summary>Seconds between hits: the applyDamage events of Punching (two per 0.4 s loop),
        /// Axing and Wrecking (one per 0.44 s loop).</summary>
        public const float FistInterval = 0.2f, ToolInterval = 4f / 9f;

        // ---------------- Dynamite (Dynamite.prefab) ----------------

        public const float BlastDamage = 300f;
        /// <summary>Blocks take a blast within half its area of effect.</summary>
        public const float BlastRadius = 1.5f;
        /// <summary>A gas tank's and a charge's blast (Dynamite.Detonate) reach this much further than a stick's.</summary>
        public const float GasReach = 1.25f;
        public const float Fuse = 2f;

        // ---------------- Actions (ActionUser, ActionStrikes and each strike's controller) ----------------

        /// <summary>ActionStrikes.Wreck: more than any block's health.</summary>
        public const float Wreck = 1000f;
        public const float CoffeeSpeed = 1.35f, CoffeeDamage = 1.5f, CoffeeSwing = 1.5f, BoostDamage = 2f;
        public const float BurpRange = 7f, BurpSpeed = 16f, BurpHalfHeight = 1f, BurpSpread = 0.12f, BurpDrop = 0.35f;
        public const float SlamDepth = 12f, SlamHalfWidth = 0.7f, SlamDrop = 0.05f, SlamRingRadius = 2.2f, SlamRingRise = 0.4f, SlamRingDamage = 150f;
        public const float CraneDelay = 0.9f, CraneRadius = 2.3f;
        public const int CraneImpacts = 3;
        /// <summary>The implosion's 3-2-1 and the ripple between its charges.</summary>
        public const float ChargeCountdown = 1.5f, ChargeRipple = 0.12f;
        public const float InternRunSpeed = 6f, InternReach = 0.9f * 0.8f + 0.2f, InternDamage = 28f, InternMetalDamage = 12f;
        public const float RadioReach = 6f, RadioRise = 0.4f, RadioBeat = 0.5f, RadioRattle = 10f;

        // ---------------- Buildings (DestructibleBuilding, StructureDefaults) ----------------

        public const float CrumbleTick = 0.25f;

        // ---------------- The camera (Main Camera 1.prefab: ScrollCamera; LevelBootstrap; Run.prefab) ----------------

        public const float PushLine = 0.7f;
        /// <summary>LevelBootstrap shows nothing left of the level: the view's left edge starts here or further along.</summary>
        public const float CameraStartLeft = -0.5f;
        public const float OutOfBoundsGrace = 3f;

        // ---------------- Slack (docs/ranked.md §4: set from honest logs) ----------------

        /// <summary>A pose between two samples: how far off an interpolated one can be.</summary>
        public const float PoseSlack = 0.6f;
        /// <summary>Online, the host sees its partner this much late.</summary>
        public const float OnlineLag = 0.5f;
        /// <summary>Two punches are never closer than this share of their clip's interval (a hit lands
        /// up to a frame late, never early).</summary>
        public const float SwingSlack = 0.5f;
        /// <summary>Movement over a sample: this much over the run speed, plus the margin.</summary>
        public const float SpeedSlack = 1.3f, MoveMargin = 0.35f;
        /// <summary>After a blast, a slam or a pallet nearby, a player may fly this long.</summary>
        public const float KnockbackWindow = 1.5f, KnockbackRange = 5f;
        /// <summary>How far one knockback can throw a player ahead of where running gets them.</summary>
        public const float KnockbackGain = 6f;
        /// <summary>A frame at a slow frame rate: the device's timers can run this much past the log's clock.</summary>
        public const float FrameSlack = 0.1f;
        /// <summary>Overload a crumbling block may lack on the server (float maths differ by platform).</summary>
        public const float CrumbleMargin = 0.2f;
        /// <summary>Seconds an action's damage may come after its strike beyond its own timing.</summary>
        public const float ActionSlack = 0.6f;
        public const float PickupReach = 2.5f;
        /// <summary>Crumble ticks are at least <see cref="CrumbleTick"/> apart; the log's clock rounds.</summary>
        public const float CrumbleTickSlack = 0.02f;
        /// <summary>Half the view of the widest screen zoomed out over the tallest building: the camera's edge
        /// is at least this much (times the push line's) behind the front player.</summary>
        public const float WidestView = 45f;
        /// <summary>How far past the camera's edge a player can stand while it shakes.</summary>
        public const float CameraMargin = 2f;
        /// <summary>The front player can push the camera slower than it scrolls by walking on the push line.</summary>
        public const float CameraCoopMargin = 4f;
        /// <summary>Columns a jump off a roof carries a player, and how far over a jump a pile of debris can lift one.</summary>
        public const int JumpAcross = 8;
        public const float JumpMargin = 3f;
    }
}
