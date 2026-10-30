using System;
using System.Collections.Generic;
using Demolition.Actions;
using Demolition.Core;
using Demolition.Generation;
using Demolition.Levels;
using Demolition.Ranked;
using Demolition.Run;
using NUnit.Framework;
using UnityEngine;

namespace Demolition.Tests
{
    public class RunVerifierTests
    {
        // A flat street, a brick column at x 20 with a 9x9 slab hanging off it (punch out its foot
        // and 90 cells come down: a full meter for any action), a little wall at x 40, a dynamite
        // pickup at x 10 and an axe at x 12. Spawn at 2, finish at 60.
        private static LevelLayout Street()
        {
            var layout = new LevelLayout(new LevelSpec { id = "test", scrollSpeed = 1.5f });
            for (int x = 0; x < 80; x++)
                layout.Ground.Add(0);
            layout.SpawnX = 2;
            layout.FinishX = 60;
            var column = new BuildingSpec("test.column", 1);
            for (int y = 0; y < 10; y++)
                column.TryPlace(TileKind.Brick, 0, y);
            for (int y = 1; y < 10; y++)
                for (int x = 1; x < 10; x++)
                    column.TryPlace(TileKind.Brick, x, y);
            layout.Buildings.Add(new BuildingPlacement { X = 20, GroundY = 0, Spec = column });
            var wall = new BuildingSpec("test.wall", 2);
            for (int y = 0; y < 2; y++)
                wall.TryPlace(TileKind.Brick, 0, y);
            layout.Buildings.Add(new BuildingPlacement { X = 40, GroundY = 0, Spec = wall });
            layout.Pickups.Add(new PickupPlacement { X = 10.5f, Y = 0.8f, Kind = PickupKind.Dynamite, Amount = 2 });
            layout.Pickups.Add(new PickupPlacement { X = 12.5f, Y = 0.8f, Kind = PickupKind.Axe, Amount = 3 });
            layout.Pickups.Add(new PickupPlacement { X = 14.5f, Y = 9f, Kind = PickupKind.Dynamite, Amount = 1 }); // high up: it falls
            return layout;
        }

        // A roof on two brick pillars at x 20 and 23, hanging on past the second: punch out the
        // first pillar's foot and what's left of it hangs from the roof, overloaded, and crumbles.
        // The finish is just before it.
        private static LevelLayout Bridge()
        {
            var layout = new LevelLayout(new LevelSpec { id = "bridge", scrollSpeed = 1.5f });
            for (int x = 0; x < 60; x++)
                layout.Ground.Add(0);
            layout.SpawnX = 2;
            layout.FinishX = 19;
            var spec = new BuildingSpec("test.bridge", 3);
            for (int y = 0; y <= 4; y++)
            {
                spec.TryPlace(TileKind.Brick, 0, y);
                spec.TryPlace(TileKind.Brick, 3, y);
            }
            for (int x = 1; x < 14; x++)
                spec.TryPlace(TileKind.Brick, x, 4);
            layout.Buildings.Add(new BuildingPlacement { X = 20, GroundY = 0, Spec = spec });
            return layout;
        }

        /// <summary>Writes a log the way the game would: poses every 0.1 s along a path, events at times.
        /// In co-op the second player keeps 1.5 behind the first.</summary>
        private sealed class Script
        {
            public readonly LevelLayout Layout;
            public readonly RunLog Log;
            public readonly RunScore Score;
            private readonly List<Vector3> _path = new List<Vector3> { new Vector3(0f, 2.5f, 0f) }; // (t, x, y)
            private readonly Dictionary<(int, int, int), float> _health = new Dictionary<(int, int, int), float>();

            public Script(LevelLayout layout = null, int players = 1)
            {
                Layout = layout ?? Street();
                WallDown = layout != null;
                Log = RunLog.For(players);
                Log.Mode = players > 1 ? RankedMode.Coop : RankedMode.Solo;
                Score = new RunScore(players);
                Log.Rules = RankedRules.Version;
                Log.LayoutHash = Layout.Hash();
            }

            /// <summary>The little wall at x 40 is down: the way to the finish needs no jump.</summary>
            public bool WallDown;

            public float T => _path[_path.Count - 1].x;
            public float X => _path[_path.Count - 1].y;

            public Script Walk(float x, float speed = 6f)
            {
                _path.Add(new Vector3(T + Mathf.Abs(x - X) / speed, x, 0f));
                return this;
            }

            /// <summary>Over the little wall: 2.5 up and down again on the way to <paramref name="x"/>.</summary>
            public Script Jump(float x, float speed = 6f)
            {
                float half = Mathf.Abs(x - X) / speed * 0.5f;
                _path.Add(new Vector3(T + half, (X + x) * 0.5f, 2.5f));
                _path.Add(new Vector3(T + half, x, 0f));
                return this;
            }

            public Script Wait(float seconds)
            {
                _path.Add(new Vector3(T + seconds, X, 0f));
                return this;
            }

            public LogEvent Add(LogEvent e)
            {
                e.TimeMs = Mathf.RoundToInt(T * 1000f);
                Log.Events.Add(e);
                return e;
            }

            /// <summary>Damage block (cx, cy) of building b; Broke as the game would see it.</summary>
            public LogEvent Hit(int building, int cx, int cy, float amount, HitCause cause, float ox, float oy, int player = 0)
            {
                var key = (building, cx, cy);
                if (!_health.TryGetValue(key, out float health))
                    health = BlockMaterialInfo.DefaultHealth(BlockMaterial.Brick);
                health -= amount;
                _health[key] = health;
                return Add(new LogEvent
                {
                    Kind = LogKind.Damage, Player = player, Building = building, CellX = cx, CellY = cy, Amount = amount,
                    Cause = cause, X = ox, Y = oy, Broke = health <= 0f,
                });
            }

            /// <summary>Stand at 18.3 and punch the column's foot out: ten fists, 0.2 s apart.</summary>
            public Script PunchOutColumn()
            {
                Walk(18.3f);
                for (int i = 0; i < 10; i++)
                {
                    Wait(0.2f);
                    Hit(0, 0, 0, 12f, HitCause.Fist, 18.3f + 1.25f, 1.3f);
                }
                Score.AddDestroyed(0, BlockMaterial.Brick, 1);
                var fallen = new List<BlockMaterial>();
                for (int i = 0; i < 90; i++)
                    fallen.Add(BlockMaterial.Brick);
                Score.AddCollapse(0, fallen);
                return this;
            }

            public RunLog Finish()
            {
                if (!WallDown && X < 40f)
                    Walk(38.5f).Jump(42.5f);
                Walk(Layout.FinishX + 0.5f);
                Add(new LogEvent { Kind = LogKind.Finish });
                Log.TimeMs = Mathf.RoundToInt(T * 1000f);
                Log.Score = Score.FinalTotal(Log.TimeMs / 1000f, Layout.Par);
                for (int k = 0; k * RunLog.PoseInterval <= T + 1e-4f; k++)
                {
                    float t = k * RunLog.PoseInterval;
                    int i = 1;
                    while (i < _path.Count - 1 && _path[i].x < t)
                        i++;
                    Vector3 a = _path[i - 1], b = _path[i];
                    float f = b.x > a.x ? Mathf.Clamp01((t - a.x) / (b.x - a.x)) : 1f;
                    for (int p = 0; p < Log.Players; p++)
                        Log.Poses[p].Add(new LogPose(Mathf.Lerp(a.y, b.y, f) - 1.5f * p, Mathf.Lerp(a.z, b.z, f), false));
                }
                return Log;
            }
        }

        private static Verdict Verify(Script script) => RunVerifier.Verify(RunLog.Decode(script.Log.Encode()), script.Layout);

        private static void AssertFails(Script script, string reason)
        {
            Verdict verdict = Verify(script);
            Assert.IsFalse(verdict.Ok, "should fail: " + reason);
            StringAssert.Contains(reason, verdict.Reason);
        }

        [Test]
        public void HonestRun_Passes_WithTheScoreTheGameCounts()
        {
            var script = new Script().PunchOutColumn();
            script.Finish();
            Verdict verdict = Verify(script);

            Assert.IsTrue(verdict.Ok, verdict.Reason);
            Assert.IsTrue(verdict.Finished);
            Assert.AreEqual(script.Log.Score, verdict.Score);
            Assert.Greater(verdict.Score, 0);
            Assert.AreEqual(91f / 93f, verdict.Destroyed, 1e-4f);
        }

        [Test]
        public void MadeUpScore_Fails()
        {
            var script = new Script().PunchOutColumn();
            script.Finish();
            script.Log.Score += 1000;
            AssertFails(script, "score");
        }

        [Test]
        public void HarderHits_Fail()
        {
            var script = new Script().Walk(18.3f).Wait(0.2f);
            script.Hit(0, 0, 0, 60f, HitCause.Fist, 19.55f, 1.3f);
            script.Finish();
            AssertFails(script, "too hard");
        }

        [Test]
        public void HitsFromAfar_Fail()
        {
            var script = new Script().Walk(10f).Wait(0.2f);
            script.Hit(0, 0, 0, 12f, HitCause.Fist, 19.55f, 1.3f);
            script.Finish();
            AssertFails(script, "reach");

            script = new Script().Walk(18.3f).Wait(0.2f);
            script.Hit(0, 0, 5, 12f, HitCause.Fist, 19.55f, 1.3f); // the swing is low, the block high up
            script.Finish();
            AssertFails(script, "outside the swing");
        }

        [Test]
        public void FastSwings_Fail()
        {
            var script = new Script().Walk(18.3f);
            for (int i = 0; i < 9; i++)
            {
                script.Wait(0.05f);
                script.Hit(0, 0, 0, 12f, HitCause.Fist, 19.55f, 1.3f);
            }
            script.Finish();
            AssertFails(script, "too fast");
        }

        [Test]
        public void BrokenBlocks_MustBreakInTheReplay()
        {
            var script = new Script().Walk(18.3f).Wait(0.2f);
            script.Hit(0, 0, 0, 12f, HitCause.Fist, 19.55f, 1.3f).Broke = true;
            script.Finish();
            AssertFails(script, "broke on the device");
        }

        [Test]
        public void Teleporting_AndWalkingThroughWalls_Fail()
        {
            var script = new Script().Walk(18.3f).Walk(35f);
            script.Finish();
            AssertFails(script, "inside a wall");

            script = new Script().PunchOutColumn().Wait(0.5f).Walk(35f, speed: 200f);
            script.Finish();
            AssertFails(script, "moved");
        }

        [Test]
        public void FinishingAwayFromTheLine_Fails()
        {
            var script = new Script().Walk(15f);
            script.Add(new LogEvent { Kind = LogKind.Finish });
            script.Finish();
            AssertFails(script, "away from the finish");
        }

        [Test]
        public void Dynamite_OnlyFromPickups()
        {
            var script = new Script().Walk(17f);
            script.Add(new LogEvent { Kind = LogKind.Throw, Player = 0, X = 18f, Y = 1.3f });
            script.Finish();
            AssertFails(script, "didn't have");

            // Picked up on the way, thrown at the column: its foot goes, the slab comes down.
            script = new Script().Walk(10.5f);
            script.Add(new LogEvent { Kind = LogKind.Pickup, Player = 0, Index = 0 });
            script.Walk(17f);
            script.Add(new LogEvent { Kind = LogKind.Throw, Player = 0, X = 18f, Y = 1.3f });
            script.Wait(0.4f);
            script.Add(new LogEvent { Kind = LogKind.Blast, Player = 0, Cause = HitCause.Dynamite, X = 19.8f, Y = 0.6f });
            script.Hit(0, 0, 0, RankedRules.BlastDamage, HitCause.Dynamite, 19.8f, 0.6f);
            script.Score.AddDestroyed(0, BlockMaterial.Brick, 1);
            var fallen = new List<BlockMaterial>();
            for (int i = 0; i < 90; i++)
                fallen.Add(BlockMaterial.Brick);
            script.Score.AddCollapse(0, fallen);
            script.Finish();
            Verdict verdict = Verify(script);
            Assert.IsTrue(verdict.Ok, verdict.Reason);

            // A second throw needs the second stick; a blast needs a throw.
            script = new Script().Walk(17f);
            script.Add(new LogEvent { Kind = LogKind.Blast, Player = 0, Cause = HitCause.Dynamite, X = 19.8f, Y = 0.6f });
            script.Finish();
            AssertFails(script, "no stick thrown");
        }

        [Test]
        public void Weapons_OnlyFromPickups()
        {
            var script = new Script().Walk(18.3f).Wait(0.5f);
            script.Hit(0, 0, 0, 40f, HitCause.Axe, 19.55f, 1.3f);
            script.Finish();
            AssertFails(script, "axe the player didn't have");

            script = new Script().Walk(12.5f);
            script.Add(new LogEvent { Kind = LogKind.Pickup, Player = 0, Index = 1 });
            script.Walk(18.3f);
            for (int i = 0; i < 3; i++)
            {
                script.Wait(0.5f);
                script.Hit(0, 0, 0, 40f, HitCause.Axe, 19.55f, 1.3f);
            }
            script.Score.AddDestroyed(0, BlockMaterial.Brick, 1);
            var fallen = new List<BlockMaterial>();
            for (int i = 0; i < 90; i++)
                fallen.Add(BlockMaterial.Brick);
            script.Score.AddCollapse(0, fallen);
            script.Walk(38.2f).Wait(0.5f);
            script.Hit(1, 0, 0, 40f, HitCause.Axe, 39.45f, 1.3f); // a fourth hit: the axe had three
            script.Finish();
            AssertFails(script, "axe the player didn't have");
        }

        [Test]
        public void AWeaponPickedUpMidPunch_LandsAtThePunchesPace()
        {
            var script = new Script().Walk(12.5f);
            script.Add(new LogEvent { Kind = LogKind.Pickup, Player = 0, Index = 1 });
            script.Walk(18.3f);
            for (int i = 0; i < 3; i++)
            {
                script.Wait(0.2f);
                script.Hit(0, 0, 0, 40f, HitCause.Axe, 19.55f, 1.3f);
            }
            script.Score.AddDestroyed(0, BlockMaterial.Brick, 1);
            var fallen = new List<BlockMaterial>();
            for (int i = 0; i < 90; i++)
                fallen.Add(BlockMaterial.Brick);
            script.Score.AddCollapse(0, fallen);
            script.Finish();
            Verdict verdict = Verify(script);
            Assert.IsTrue(verdict.Ok, verdict.Reason);
        }

        [Test]
        public void APickupFallsFromItsSpot_ButStaysInItsColumn()
        {
            var script = new Script().Walk(14.5f);
            script.Add(new LogEvent { Kind = LogKind.Pickup, Player = 0, Index = 2 });
            script.PunchOutColumn().Finish();
            Verdict verdict = Verify(script);
            Assert.IsTrue(verdict.Ok, verdict.Reason);

            script = new Script().Walk(10f);
            script.Add(new LogEvent { Kind = LogKind.Pickup, Player = 0, Index = 2 });
            script.Finish();
            AssertFails(script, "out of reach");
        }

        [Test]
        public void Actions_NeedAFullMeter_AndHitOnlyWhereTheyReach()
        {
            var script = new Script().Walk(10f);
            script.Add(new LogEvent { Kind = LogKind.ActionStart, Player = 0, Action = ActionKind.Burp });
            script.Finish();
            AssertFails(script, "meter");

            // The slab's 90 cells fill the burp's meter; the burp wrecks the wall in front.
            script = new Script().PunchOutColumn().Walk(37.5f);
            script.Add(new LogEvent { Kind = LogKind.ActionStart, Player = 0, Action = ActionKind.Burp });
            script.Wait(1.1f);
            script.Add(new LogEvent { Kind = LogKind.ActionStrike, Player = 0, Action = ActionKind.Burp, X = 38.2f, Y = 1.5f });
            script.Wait(0.15f);
            script.Hit(1, 0, 0, RankedRules.Wreck, HitCause.Burp, 0f, 0f);
            script.Score.AddDestroyed(0, BlockMaterial.Brick, 1);
            script.Score.AddCollapse(0, new List<BlockMaterial> { BlockMaterial.Brick });
            script.WallDown = true;
            script.Walk(41f); // through where the wall was
            script.Finish();
            Verdict verdict = Verify(script);
            Assert.IsTrue(verdict.Ok, verdict.Reason);

            // Burp damage with no burp.
            script = new Script().Walk(18.3f).Wait(0.2f);
            script.Hit(0, 0, 0, RankedRules.Wreck, HitCause.Burp, 0f, 0f);
            script.Finish();
            AssertFails(script, "no strike to match");
        }

        [Test]
        public void Crumble_OnlyOnOverloadedBlocks()
        {
            var script = new Script().Walk(18.3f).Wait(0.25f);
            script.Hit(0, 0, 3, 5f, HitCause.Crumble, 0f, 0f, player: -1);
            script.Finish();
            AssertFails(script, "isn't overloaded");
        }

        // The overload past baseline of the bridge's hanging pillar once its foot is out, as the game counts it.
        private static float HangingOverload(LevelLayout layout)
        {
            BuildingStructure structure = layout.Buildings[0].Spec.ToStructure();
            float Over(Dictionary<Vector2Int, CellStress> stress) => stress[new Vector2Int(0, 1)].Overload;
            float baseline = Over(structure.ComputeStress(StructureDefaults.StressScanRange, StructureDefaults.ColumnLoadCapacity, StructureDefaults.ColumnMomentCapacity));
            structure.RemoveCells(new List<Vector2Int> { new Vector2Int(0, 0) });
            return Over(structure.ComputeStress(StructureDefaults.StressScanRange, StructureDefaults.ColumnLoadCapacity, StructureDefaults.ColumnMomentCapacity)) - baseline;
        }

        private static Script CrumbleRun(float speedUp)
        {
            var script = new Script(Bridge());
            float over = HangingOverload(script.Layout);
            Assert.Greater(over, StructureDefaults.OverloadTolerance, "the test needs an overloaded pillar");
            script.Walk(18.3f);
            for (int i = 0; i < 10; i++)
            {
                script.Wait(0.2f);
                script.Hit(0, 0, 0, 12f, HitCause.Fist, 19.55f, 1.3f);
            }
            // As DestructibleBuilding.Update: a tick every 0.25 s, the excess times the rate times the time.
            float tick = (over - StructureDefaults.OverloadTolerance) * StructureDefaults.CrumbleDamageRate * 0.25f * speedUp;
            bool down = false;
            for (int i = 0; i < 40 && !down; i++)
            {
                script.Wait(0.25f);
                down = script.Hit(0, 0, 1, tick, HitCause.Crumble, 0f, 0f, player: -1).Broke;
            }
            Assert.IsTrue(down, "the pillar crumbled");
            script.Finish();
            return script;
        }

        [Test]
        public void Crumble_AsTheGameDoesIt_Passes_AndFaster_Fails()
        {
            Script script = CrumbleRun(1f);
            script.Log.Score = Verify(script).Score; // what it scores is the replay's business; that it passes is this test's
            Verdict verdict = Verify(script);
            Assert.IsTrue(verdict.Ok, verdict.Reason);

            AssertFails(CrumbleRun(3f), "crumbling too fast");
        }

        [Test]
        public void Coop_Rejoins_PayThePenalty_AndSoloHasNone()
        {
            var script = new Script(players: 2);
            script.Walk(10f);
            script.Add(new LogEvent { Kind = LogKind.Out, Player = 1, Rejoins = true });
            script.Wait(2f);
            script.Add(new LogEvent { Kind = LogKind.Rejoin, Player = 1, X = 10f + RankedRules.RejoinOffsetX, Y = RankedRules.RejoinOffsetY });
            script.PunchOutColumn();
            script.Finish();
            Verdict verdict = Verify(script);
            Assert.IsTrue(verdict.Ok, verdict.Reason);

            var solo = new Script().Walk(10f);
            solo.Add(new LogEvent { Kind = LogKind.Out, Player = 0, Rejoins = true });
            solo.Finish();
            AssertFails(solo, "rejoin in solo");
        }

        [Test]
        public void TheBoardsTime_IsTheFinishs()
        {
            var script = new Script().PunchOutColumn();
            script.Finish();
            script.Log.TimeMs -= 5000;
            AssertFails(script, "a time on the board");
        }

        [Test]
        public void AScoreAPointOff_IsRounding_AndTheReplaysCounts()
        {
            var script = new Script().PunchOutColumn();
            script.Finish();
            int replayed = script.Log.Score;
            script.Log.Score = replayed + 1;
            Verdict verdict = Verify(script);
            Assert.IsTrue(verdict.Ok, verdict.Reason);
            Assert.AreEqual(replayed, verdict.Score);
        }

        [Test]
        public void ASwing_HitsABlockOnce()
        {
            // Another block in the same swing is what a swing does: the slab's cell over the column's foot.
            var script = new Script(Bridge()).Walk(18.3f).Wait(0.2f);
            script.Hit(0, 0, 0, 12f, HitCause.Fist, 19.55f, 1.3f);
            script.Hit(0, 0, 1, 12f, HitCause.Fist, 19.55f, 1.3f);
            script.Finish();
            Verdict verdict = Verify(script);
            Assert.IsTrue(verdict.Ok, verdict.Reason);

            script = new Script(Bridge()).Walk(18.3f).Wait(0.2f);
            script.Hit(0, 0, 0, 12f, HitCause.Fist, 19.55f, 1.3f);
            script.Hit(0, 0, 0, 12f, HitCause.Fist, 19.55f, 1.3f);
            script.Finish();
            AssertFails(script, "hit a block twice");
        }

        [Test]
        public void Tools_NeverSwingFasterThanFists()
        {
            var script = new Script().Walk(12.5f);
            script.Add(new LogEvent { Kind = LogKind.Pickup, Player = 0, Index = 1 });
            script.Walk(18.3f).Wait(0.2f);
            script.Hit(0, 0, 0, 40f, HitCause.Axe, 19.55f, 1.3f);
            script.Wait(0.05f);
            script.Hit(0, 0, 0, 40f, HitCause.Axe, 19.55f, 1.3f);
            script.Finish();
            AssertFails(script, "swings too fast");
        }

        [Test]
        public void ABlast_HitsABlockOnce()
        {
            var script = new Script().Walk(10.5f);
            script.Add(new LogEvent { Kind = LogKind.Pickup, Player = 0, Index = 0 });
            script.Walk(17f);
            script.Add(new LogEvent { Kind = LogKind.Throw, Player = 0, X = 18f, Y = 1.3f });
            script.Wait(0.4f);
            script.Add(new LogEvent { Kind = LogKind.Blast, Player = 0, Cause = HitCause.Dynamite, X = 19.8f, Y = 0.6f });
            script.Hit(0, 0, 1, 1f, HitCause.Dynamite, 19.8f, 0.6f);
            script.Hit(0, 0, 1, 1f, HitCause.Dynamite, 19.8f, 0.6f);
            script.Finish();
            AssertFails(script, "a blast that hit a block twice");
        }

        [Test]
        public void ASecondStick_WaitsForTheFirst()
        {
            var script = new Script().Walk(10.5f);
            script.Add(new LogEvent { Kind = LogKind.Pickup, Player = 0, Index = 0 });
            script.Walk(17f);
            script.Add(new LogEvent { Kind = LogKind.Throw, Player = 0, X = 18f, Y = 1.3f });
            script.Wait(0.1f);
            script.Add(new LogEvent { Kind = LogKind.Throw, Player = 0, X = 18f, Y = 1.3f });
            script.Finish();
            AssertFails(script, "a second stick");
        }

        [Test]
        public void ACrumbleTick_HitsABlockOnce_AndTicksKeepTheirPace()
        {
            // The first tick of a real crumble, then the same block again in that tick / right after it.
            foreach ((float gap, string reason) in new[] { (0f, "a crumble tick that hit a block twice"), (0.05f, "crumble ticks too close") })
            {
                var script = new Script(Bridge());
                float over = HangingOverload(script.Layout);
                float tick = (over - StructureDefaults.OverloadTolerance) * StructureDefaults.CrumbleDamageRate * 0.25f;
                script.Walk(18.3f);
                for (int i = 0; i < 10; i++)
                {
                    script.Wait(0.2f);
                    script.Hit(0, 0, 0, 12f, HitCause.Fist, 19.55f, 1.3f);
                }
                script.Wait(0.25f);
                script.Hit(0, 0, 1, tick * 0.5f, HitCause.Crumble, 0f, 0f, player: -1);
                if (gap > 0f)
                    script.Wait(gap);
                script.Hit(0, 0, 1, tick * 0.1f, HitCause.Crumble, 0f, 0f, player: -1);
                script.Finish();
                AssertFails(script, reason);
            }
        }

        private static Script Interning(Action<Script> punches)
        {
            var script = new Script().PunchOutColumn().Walk(36f);
            script.Log.Actions[0] = ActionKind.Intern;
            script.Add(new LogEvent { Kind = LogKind.ActionStart, Player = 0, Action = ActionKind.Intern });
            script.Wait(0.5f);
            script.Add(new LogEvent { Kind = LogKind.ActionStrike, Player = 0, Action = ActionKind.Intern, X = 36f, Y = 0f });
            script.Wait(0.5f);
            punches(script);
            script.Finish();
            return script;
        }

        [Test]
        public void TheIntern_PunchesOnTheClipsBeat()
        {
            Script script = Interning(s =>
            {
                for (int i = 0; i < 4; i++)
                {
                    s.Wait(0.2f);
                    s.Hit(1, 0, 0, RankedRules.InternDamage, HitCause.Intern, 40.2f, 1f);
                }
            });
            script.Log.Score = Verify(script).Score;
            Verdict verdict = Verify(script);
            Assert.IsTrue(verdict.Ok, verdict.Reason);

            AssertFails(Interning(s =>
            {
                s.Wait(0.2f);
                s.Hit(1, 0, 0, RankedRules.InternDamage, HitCause.Intern, 40.2f, 1f);
                s.Hit(1, 0, 0, RankedRules.InternDamage, HitCause.Intern, 40.2f, 1f);
            }), "an intern's punch that hit a block twice");

            AssertFails(Interning(s =>
            {
                for (int i = 0; i < 4; i++)
                {
                    s.Wait(0.04f);
                    s.Hit(1, 0, 0, RankedRules.InternDamage, HitCause.Intern, 40.2f, 1f);
                }
            }), "swings too fast");
        }

        [Test]
        public void ThePallet_CanLandHigherThanItsSpot()
        {
            // The spot only looks 20 up; a tower reaches past that, and the pallet comes from the sky.
            LevelLayout layout = Street();
            var tower = new BuildingSpec("test.tower", 3);
            for (int y = 0; y < 25; y++)
                tower.TryPlace(TileKind.Brick, 0, y);
            layout.Buildings.Add(new BuildingPlacement { X = 70, GroundY = 0, Spec = tower });

            var script = new Script(layout);
            script.Walk(18.3f);
            for (int i = 0; i < 10; i++)
            {
                script.Wait(0.2f);
                script.Hit(0, 0, 0, 12f, HitCause.Fist, 19.55f, 1.3f);
            }
            script.Log.Actions[0] = ActionKind.CraneDrop;
            script.Walk(38.5f).Jump(42.5f).Walk(60f);
            script.Add(new LogEvent { Kind = LogKind.ActionStart, Player = 0, Action = ActionKind.CraneDrop });
            script.Wait(0.5f);
            script.Add(new LogEvent { Kind = LogKind.ActionStrike, Player = 0, Action = ActionKind.CraneDrop, X = 65f, Y = 0f, Picks = new[] { 65f, 0f } });
            script.Wait(RankedRules.CraneDelay + 1f);
            script.Hit(2, 0, 22, RankedRules.Wreck, HitCause.Crane, 69.5f, 22f);
            script.Finish();
            script.Log.Score = Verify(script).Score;
            Verdict verdict = Verify(script);
            Assert.IsTrue(verdict.Ok, verdict.Reason);
        }

        private static void Lift(Script script, int from, int samples, Func<int, float> height)
        {
            List<LogPose> poses = script.Log.Poses[0];
            for (int k = 0; k < samples; k++)
                poses[from + k] = new LogPose(poses[from + k].X, poses[from + k].Y + height(k), false);
        }

        [Test]
        public void Flying_Fails_WithOrWithoutARestInTheClimb()
        {
            var script = new Script(LongStreet()).Walk(15f).Wait(4f);
            script.Finish();
            Lift(script, 30, 30, k => k * 0.2f);
            AssertFails(script, "kept rising");

            script = new Script(LongStreet()).Walk(15f).Wait(4f);
            script.Finish();
            Lift(script, 30, 30, k => (k - k / 8) * 1.2f);
            AssertFails(script, "flew");
        }

        [Test]
        public void DownAPit_AndStillInPlay_Fails()
        {
            var script = new Script(LongStreet()).Walk(15f).Wait(5f);
            script.Finish();
            Lift(script, 30, 40, k => -40f);
            AssertFails(script, "fell out of the level");
        }

        [Test]
        public void FinishingSoonerThanRunningThereTakes_Fails()
        {
            var script = new Script(LongStreet()).Walk(301f, speed: 9.5f);
            script.Finish();
            AssertFails(script, "sooner than running");
        }

        // A long street with nothing on it: the camera has the time to leave someone behind.
        private static LevelLayout LongStreet()
        {
            var layout = new LevelLayout(new LevelSpec { id = "long", scrollSpeed = 2f });
            for (int x = 0; x < 320; x++)
                layout.Ground.Add(0);
            layout.SpawnX = 2;
            layout.FinishX = 300;
            return layout;
        }

        [Test]
        public void StayingBehindTheCamera_Fails_AndKeepingUpOrGoingOutDoesnt()
        {
            var ahead = new Script(LongStreet()).Walk(301f, speed: 7.5f);
            ahead.Finish();
            Verdict verdict = Verify(ahead);
            Assert.IsTrue(verdict.Ok, verdict.Reason);

            // Off the edge of the camera for a minute and a half, and in play.
            var behind = new Script(LongStreet()).Walk(100f, speed: 7.5f).Wait(90f).Walk(301f, speed: 7.5f);
            behind.Finish();
            AssertFails(behind, "behind the camera");

            // The same wait, but the player's partner has the camera and the player goes out in time.
            var coop = new Script(LongStreet(), players: 2).Walk(100f, speed: 7.5f).Wait(90f);
            coop.Add(new LogEvent { Kind = LogKind.Out, Player = 0 });
            coop.Finish();
            AssertFails(coop, "behind the camera"); // the wait came first: too late for the Out
        }

        [Test]
        public void AnotherLevel_OrOtherRules_Fail()
        {
            var script = new Script().PunchOutColumn();
            script.Finish();
            script.Log.LayoutHash ^= 1;
            AssertFails(script, "another level");

            script = new Script().PunchOutColumn();
            script.Finish();
            script.Log.Rules = RankedRules.Version + 1;
            AssertFails(script, "rules version");
        }
    }
}
