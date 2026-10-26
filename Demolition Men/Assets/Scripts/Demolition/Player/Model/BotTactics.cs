using Demolition.Actions;

namespace Demolition
{
    /// <summary>What the bot sees once its action meter is full (<see cref="BotPilot"/> senses it).
    /// Distances are from the feet, ahead; infinity when there is nothing.</summary>
    public struct BotSight
    {
        /// <summary>Facing on, the way the level goes and the sensing looks.</summary>
        public bool FacingAhead;
        public bool Grounded;
        /// <summary>No floor over the head: out in the street, or up on a roof.</summary>
        public bool Headroom;
        /// <summary>Blocks coming down on it, or tumbling just ahead.</summary>
        public bool Debris;
        /// <summary>Backing off, or waiting for its own dynamite or strike to go off.</summary>
        public bool Retreating;
        /// <summary>Health left, 0–1.</summary>
        public float Health;
        /// <summary>To the first standing block ahead at body height.</summary>
        public float Wall;
        /// <summary>That block is part of a building, not a street prop.</summary>
        public bool WallIsBuilding;
        /// <summary>Standing blocks straight under the feet (up on a roof).</summary>
        public int FloorsBelow;
        /// <summary>A standing building where the crane's pallet would land.</summary>
        public bool CraneTarget;
        /// <summary>Charges the controlled demolition would set on a building (none: nothing standing ahead).</summary>
        public int Charges;
        /// <summary>To the nearest of those charges.</summary>
        public float NearestCharge;
        /// <summary>Running speed, units/s. The player coasts on while the frames play, so what is
        /// aimed at a spot ahead (the charges, the crane) waits for a standstill.</summary>
        public float Speed;
        /// <summary>Seconds it can stand still before the scrolling camera leaves it behind.</summary>
        public float Room;
        /// <summary>The partner is close by and hitting something.</summary>
        public bool PartnerWorking;
        /// <summary>Seconds since the meter filled.</summary>
        public float FullFor;
    }

    /// <summary>
    /// When the bot fires its action: where a player would, and never where it hurts itself.
    /// The burp a couple of units from a building, the radio, the coffee and the intern at one just ahead; the crane onto
    /// a roof ahead; the controlled demolition at a building far enough off that its charges
    /// don't reach it; the slam on a roof (or, after a while, at the foot of a wall); the vest
    /// when debris comes down; the shovel when hurt or the partner is hitting. The charges and
    /// the crane and the burp from a standstill (<see cref="BotSight.Speed"/>). Never in the
    /// middle of falling debris, while backing off, or when the camera would leave it behind
    /// while the frames play.
    /// </summary>
    public static class BotTactics
    {
        /// <summary>Dynamite's blast reaches 3 units: charges no nearer than this.</summary>
        public const float ChargeClear = 4.5f;
        public const float ChargeReach = 12f;
        /// <summary>A full meter waits this long for a roof before the slam goes at a wall.</summary>
        public const float SlamWait = 6f;
        public const float StandStill = 1f;

        public static bool Fire(ActionKind kind, BotSight s)
        {
            if (s.Room < LockSeconds(ActionCatalog.Get(kind)))
                return false;
            if (kind == ActionKind.HiVis)
                return s.Debris || (s.Health < 0.5f && s.Wall < 2f);
            if (!s.Grounded || s.Debris || s.Retreating)
                return false;
            if (!s.FacingAhead && Aimed(kind))
                return false;
            bool building = s.WallIsBuilding;
            switch (kind)
            {
                case ActionKind.Burp: // the band wrecks 7 units ahead at chest height: from outside and a couple of units off, or the floors drop on it
                    return s.Headroom && building && s.Wall >= 2f && s.Wall <= 4f && s.Speed < StandStill;
                case ActionKind.ButtSlam: // the hop needs the sky; down through the floors under it
                    return s.Headroom && (s.FloorsBelow >= 2 || (s.FullFor > SlamWait && building && s.Wall < 1.5f));
                case ActionKind.CraneDrop: // from the street: up on a roof the pallet smashes the one it stands on
                    return s.Headroom && s.FloorsBelow == 0 && s.CraneTarget && s.Speed < StandStill;
                case ActionKind.Implosion: // not a prop's single charge, not on the roof that drops
                    return s.Headroom && s.FloorsBelow == 0 && s.Charges >= 2
                        && s.NearestCharge >= ChargeClear && s.NearestCharge <= ChargeReach && s.Speed < StandStill;
                case ActionKind.SiteRadio: // its rattle would shake the roof under it loose too
                    return s.FloorsBelow == 0 && building && s.Wall <= 3f;
                case ActionKind.CoffeeBreak:
                    return building && s.Wall <= 3f;
                case ActionKind.Intern:
                    return building && s.Wall <= 5f;
                case ActionKind.ShovelLean:
                    return s.Health < 0.6f || s.PartnerWorking;
                default:
                    return false;
            }
        }

        /// <summary>The action goes the way the player faces.</summary>
        public static bool Aimed(ActionKind kind) =>
            kind == ActionKind.Burp || kind == ActionKind.CraneDrop || kind == ActionKind.Implosion
            || kind == ActionKind.Intern || kind == ActionKind.SiteRadio;

        /// <summary>Seconds the action's frames hold the controls: its frames, played over again
        /// until its hold is up (a pose held until landing counts a second).</summary>
        public static float LockSeconds(ActionInfo info)
        {
            float frames = 0f;
            foreach (float f in info.Frames)
                frames += f == ActionInfo.UntilLanded ? 1f : f;
            if (frames <= 0f)
                return 0f;
            return info.Hold > frames ? (float)System.Math.Ceiling(info.Hold / frames) * frames : frames;
        }
    }
}
