using System;
using System.IO;
using Demolition.Actions;
using UnityEngine;

namespace Demolition.Online
{
    /// <summary>
    /// How online messages squeeze state into bytes (docs/multiplayer.md §3): a block's
    /// health in 7 bits plus the instigating player in the 8th, a player's pose in one byte,
    /// a building's cell as one small index. Both devices and the tests use these.
    /// </summary>
    public static class NetPacking
    {
        public const int HealthSteps = 127;
        public const int MaxWeapon = 31;
        public const int MaxKitUses = 1023, MaxKitDynamite = 255;

        /// <summary>Health fraction 0..1 as 0..127. A block that is still alive never packs
        /// to 0, which would destroy it on the other device.</summary>
        public static int QuantizeHealth(float fraction)
        {
            if (!(fraction > 0f))
                return 0;
            return Math.Min(HealthSteps, Math.Max(1, (int)Math.Ceiling(fraction * HealthSteps)));
        }

        public static float HealthFraction(int steps) => Math.Max(0, Math.Min(HealthSteps, steps)) / (float)HealthSteps;

        /// <summary>One CellHealth byte. Two players, so one bit names the instigator; no
        /// instigator (overload crumble) packs as player 0, since only the host scores.</summary>
        public static byte PackCellHealth(int steps, int instigator) =>
            (byte)((steps & HealthSteps) | (instigator == 1 ? 0x80 : 0));

        public static void UnpackCellHealth(byte packed, out int steps, out int instigator)
        {
            steps = packed & HealthSteps;
            instigator = packed >> 7;
        }

        /// <summary>A cell's index in its building, row by row over the building's width
        /// (<c>BuildingSpec.Width</c>; generated buildings start at (0,0)). Small numbers
        /// pack into one or two varint bytes.</summary>
        public static int CellIndex(Vector2Int cell, int width) => cell.y * width + cell.x;

        public static Vector2Int Cell(int index, int width) => new Vector2Int(index % width, index / width);

        /// <summary>A player's pose in one byte: facing, grounded, swinging, weapon (0..31).</summary>
        public static byte PackPose(bool facingRight, bool grounded, bool swinging, int weapon) =>
            (byte)((facingRight ? 1 : 0) | (grounded ? 2 : 0) | (swinging ? 4 : 0) | (Math.Max(0, Math.Min(MaxWeapon, weapon)) << 3));

        public static void UnpackPose(byte pose, out bool facingRight, out bool grounded, out bool swinging, out int weapon)
        {
            facingRight = (pose & 1) != 0;
            grounded = (pose & 2) != 0;
            swinging = (pose & 4) != 0;
            weapon = pose >> 3;
        }

        /// <summary>What a player carries, for the other device's HUD: weapon (0..3), its hits
        /// left (0..1023; fists' -1 packs as 0) and sticks of dynamite (0..255).</summary>
        public static int PackKit(int weapon, int uses, int dynamite) =>
            (weapon & 3) | (Math.Max(0, Math.Min(MaxKitUses, uses)) << 2) | (Math.Max(0, Math.Min(MaxKitDynamite, dynamite)) << 12);

        public static void UnpackKit(int kit, out int weapon, out int uses, out int dynamite)
        {
            weapon = kit & 3;
            uses = (kit >> 2) & MaxKitUses;
            dynamite = (kit >> 12) & MaxKitDynamite;
        }

        /// <summary>An action message: phase, player, kind and facing a byte each, where it struck,
        /// the meter, then up to <see cref="ActionCall.MaxSpots"/> picked spots. 24 bytes, 8 per spot.</summary>
        public static byte[] PackAction(ActionCall call)
        {
            using var stream = new MemoryStream();
            using var w = new BinaryWriter(stream);
            int spots = call.Spots != null ? Math.Min(ActionCall.MaxSpots, call.Spots.Length) : 0;
            w.Write((byte)call.Phase);
            w.Write((byte)call.Player);
            w.Write((byte)call.Kind);
            w.Write((sbyte)(call.Facing < 0 ? -1 : 1));
            w.Write(call.At.x);
            w.Write(call.At.y);
            w.Write(call.Charge);
            w.Write((byte)spots);
            for (int i = 0; i < spots; i++)
            {
                w.Write(call.Spots[i].x);
                w.Write(call.Spots[i].y);
            }
            return stream.ToArray();
        }

        /// <summary>The message back, or false for anything malformed (unknown phase or kind, a
        /// player other than 0 or 1, too many spots, numbers that aren't, cut short).</summary>
        public static bool TryUnpackAction(byte[] data, out ActionCall call)
        {
            call = default;
            if (data == null)
                return false;
            try
            {
                using var r = new BinaryReader(new MemoryStream(data));
                byte phase = r.ReadByte(), player = r.ReadByte(), kind = r.ReadByte();
                sbyte facing = r.ReadSByte();
                float x = r.ReadSingle(), y = r.ReadSingle(), charge = r.ReadSingle();
                int count = r.ReadByte();
                if (phase > (byte)ActionPhase.Struck || player > 1 || kind >= ActionCatalog.All.Count || count > ActionCall.MaxSpots
                    || !Finite(x) || !Finite(y) || !Finite(charge))
                    return false;
                var spots = new Vector2[count];
                for (int i = 0; i < count; i++)
                {
                    spots[i] = new Vector2(r.ReadSingle(), r.ReadSingle());
                    if (!Finite(spots[i].x) || !Finite(spots[i].y))
                        return false;
                }
                call = new ActionCall
                {
                    Phase = (ActionPhase)phase, Player = player, Kind = (ActionKind)kind, Facing = facing < 0 ? -1 : 1,
                    At = new Vector2(x, y), Charge = charge, Spots = spots,
                };
                return true;
            }
            catch (EndOfStreamException)
            {
                return false;
            }
        }

        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
