using System.Collections.Generic;
using Demolition.Actions;
using Demolition.Core;
using Demolition.Online;
using NUnit.Framework;
using UnityEngine;

namespace Demolition.Tests
{
    /// <summary>The online packing (docs/multiplayer.md §3) and the promise §4 rests on: a
    /// client fed the host's CellHealth stream ends with the host's standing cells.</summary>
    public class NetPackingTests
    {
        [Test]
        public void Health_RoundTrips_AndAliveNeverPacksToZero()
        {
            Assert.AreEqual(0, NetPacking.QuantizeHealth(0f));
            Assert.AreEqual(0, NetPacking.QuantizeHealth(-3f));
            Assert.AreEqual(NetPacking.HealthSteps, NetPacking.QuantizeHealth(1f));
            Assert.AreEqual(NetPacking.HealthSteps, NetPacking.QuantizeHealth(7f));
            Assert.AreEqual(1, NetPacking.QuantizeHealth(0.0001f), "a scratched-alive block stays alive");
            for (int i = 1; i <= 1000; i++)
            {
                float f = i / 1000f;
                float back = NetPacking.HealthFraction(NetPacking.QuantizeHealth(f));
                Assert.That(back, Is.InRange(f, f + 1f / NetPacking.HealthSteps), $"fraction {f}");
            }
        }

        [Test]
        public void CellHealthByte_RoundTrips()
        {
            for (int steps = 0; steps <= NetPacking.HealthSteps; steps++)
            {
                foreach (int player in new[] { 0, 1 })
                {
                    NetPacking.UnpackCellHealth(NetPacking.PackCellHealth(steps, player), out int s, out int p);
                    Assert.AreEqual(steps, s);
                    Assert.AreEqual(player, p);
                }
            }
            NetPacking.UnpackCellHealth(NetPacking.PackCellHealth(5, -1), out _, out int none);
            Assert.AreEqual(0, none, "no instigator packs as player 0");
        }

        [Test]
        public void CellIndex_RoundTrips()
        {
            for (int width = 1; width <= 24; width++)
                for (int y = 0; y < 30; y++)
                    for (int x = 0; x < width; x++)
                        Assert.AreEqual(new Vector2Int(x, y), NetPacking.Cell(NetPacking.CellIndex(new Vector2Int(x, y), width), width));
        }

        [Test]
        public void Pose_RoundTrips()
        {
            for (int weapon = 0; weapon <= NetPacking.MaxWeapon; weapon++)
            {
                for (int bits = 0; bits < 8; bits++)
                {
                    bool facing = (bits & 1) != 0, grounded = (bits & 2) != 0, swinging = (bits & 4) != 0;
                    NetPacking.UnpackPose(NetPacking.PackPose(facing, grounded, swinging, weapon), out bool f, out bool g, out bool s, out int w);
                    Assert.AreEqual((facing, grounded, swinging, weapon), (f, g, s, w));
                }
            }
        }

        [Test]
        public void Kit_RoundTrips_AndClamps()
        {
            foreach ((int weapon, int uses, int dynamite) in new[] { (0, 0, 0), (1, 7, 3), (2, NetPacking.MaxKitUses, NetPacking.MaxKitDynamite), (3, 1, 9) })
            {
                NetPacking.UnpackKit(NetPacking.PackKit(weapon, uses, dynamite), out int w, out int u, out int d);
                Assert.AreEqual((weapon, uses, dynamite), (w, u, d));
            }
            NetPacking.UnpackKit(NetPacking.PackKit(0, -1, 5000), out _, out int fists, out int sticks);
            Assert.AreEqual((0, NetPacking.MaxKitDynamite), (fists, sticks));
        }

        // Two columns, three slabs between them and a balcony on the middle one: cutting a
        // column or a slab end drops whole pieces, so the stream exercises detachment.
        private const int Width = 10, Height = 10;

        private static BuildingStructure Building()
        {
            var s = new BuildingStructure();
            for (int y = 0; y < Height; y++)
            {
                s.AddCell(new Vector2Int(0, y), isAnchor: y == 0);
                s.AddCell(new Vector2Int(7, y), isAnchor: y == 0);
            }
            foreach (int y in new[] { 3, 6, 9 })
                for (int x = 1; x < 7; x++)
                    s.AddCell(new Vector2Int(x, y));
            s.AddCell(new Vector2Int(8, 6));
            s.AddCell(new Vector2Int(9, 6));
            return s;
        }

        [Test]
        public void ClientFedTheHostsCellHealthStream_EndsWithTheSameStandingCells()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                BuildingStructure host = Building(), client = Building();
                var hostHealth = new Dictionary<Vector2Int, float>();
                foreach (Vector2Int cell in host.Cells)
                    hostHealth[cell] = 1f;

                // Host: random hits on standing cells; every change goes out as (cell index, byte).
                var stream = new List<(int index, byte packed)>();
                var rng = new System.Random(seed);
                for (int hit = 0; hit < 60 && host.CellCount > 0; hit++)
                {
                    var standing = new List<Vector2Int>(host.Cells);
                    standing.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x)); // HashSet order isn't part of the protocol
                    Vector2Int cell = standing[rng.Next(standing.Count)];
                    float health = Mathf.Max(0f, hostHealth[cell] - (float)rng.NextDouble() * 0.7f);
                    hostHealth[cell] = health;
                    stream.Add((NetPacking.CellIndex(cell, Width), NetPacking.PackCellHealth(NetPacking.QuantizeHealth(health), hit % 2)));
                    if (health <= 0f)
                        host.RemoveCells(new[] { cell });
                }

                // Client: applies the stream to its own copy, destroying at 0 (BuildingBlock.ApplyNetHealth).
                foreach ((int index, byte packed) in stream)
                {
                    Vector2Int cell = NetPacking.Cell(index, Width);
                    NetPacking.UnpackCellHealth(packed, out int steps, out _);
                    if (steps == 0 && client.HasCell(cell))
                        client.RemoveCells(new[] { cell });
                }

                Assert.Greater(Width * Height, host.CellCount);
                CollectionAssert.AreEquivalent(host.Cells, client.Cells, $"seed {seed}");
            }
        }

        [Test]
        public void Action_RoundTrips_WithItsSpots()
        {
            var call = new ActionCall
            {
                Phase = ActionPhase.Struck, Player = 1, Kind = ActionKind.Implosion, Facing = -1,
                At = new Vector2(41.5f, 3.25f), Charge = 12f,
                Spots = new[] { new Vector2(44f, 0.5f), new Vector2(47f, 0.5f), new Vector2(50f, 0.5f) },
            };
            Assert.IsTrue(NetPacking.TryUnpackAction(NetPacking.PackAction(call), out ActionCall back));
            Assert.AreEqual(call.Phase, back.Phase);
            Assert.AreEqual(call.Player, back.Player);
            Assert.AreEqual(call.Kind, back.Kind);
            Assert.AreEqual(call.Facing, back.Facing);
            Assert.AreEqual(call.At, back.At);
            Assert.AreEqual(call.Charge, back.Charge);
            CollectionAssert.AreEqual(call.Spots, back.Spots);

            Assert.IsTrue(NetPacking.TryUnpackAction(NetPacking.PackAction(new ActionCall { Phase = ActionPhase.Kind }), out back));
            Assert.AreEqual(0, back.Spots.Length);
        }

        [Test]
        public void Action_RejectsWhatNoDeviceSends()
        {
            byte[] good = NetPacking.PackAction(new ActionCall { Phase = ActionPhase.Start, Kind = ActionKind.Burp });
            Assert.IsFalse(NetPacking.TryUnpackAction(null, out _));
            Assert.IsFalse(NetPacking.TryUnpackAction(new byte[5], out _), "cut short");
            byte[] bad = (byte[])good.Clone();
            bad[0] = 99; // phase
            Assert.IsFalse(NetPacking.TryUnpackAction(bad, out _));
            bad = (byte[])good.Clone();
            bad[1] = 2; // player
            Assert.IsFalse(NetPacking.TryUnpackAction(bad, out _));
            bad = (byte[])good.Clone();
            bad[2] = 200; // kind
            Assert.IsFalse(NetPacking.TryUnpackAction(bad, out _));
            bad = (byte[])good.Clone();
            bad[bad.Length - 1] = ActionCall.MaxSpots + 1; // spots
            Assert.IsFalse(NetPacking.TryUnpackAction(bad, out _));
            bad = (byte[])good.Clone();
            System.BitConverter.GetBytes(float.NaN).CopyTo(bad, 4); // where
            Assert.IsFalse(NetPacking.TryUnpackAction(bad, out _));
        }
    }
}
