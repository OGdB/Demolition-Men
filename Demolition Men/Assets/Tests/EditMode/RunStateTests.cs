using System.Collections.Generic;
using Demolition.Run;
using NUnit.Framework;

namespace Demolition.Tests
{
    public class RunStateTests
    {
        private static RunState Playing(int players)
        {
            var state = new RunState(players, RunRules.Default);
            state.Tick(RunRules.Default.countdown);
            Assert.AreEqual(RunPhase.Playing, state.Phase);
            return state;
        }

        private static void Step(RunState state, float seconds)
        {
            for (float t = 0f; t < seconds; t += 0.1f)
                state.Tick(0.1f);
        }

        [Test]
        public void Countdown_ThenPlaying_TimerOnlyRunsWhilePlaying()
        {
            var state = new RunState(1, RunRules.Default);

            state.Tick(1f);
            Assert.AreEqual(RunPhase.Countdown, state.Phase);
            Assert.AreEqual(0f, state.Elapsed);

            state.Tick(2f);
            state.Tick(1f);
            Assert.AreEqual(RunPhase.Playing, state.Phase);
            Assert.AreEqual(1f, state.Elapsed, 1e-5f);
        }

        [Test]
        public void OutOfBounds_ReturningWithinGrace_CancelsTheCountdown()
        {
            var state = Playing(1);

            state.SetInBounds(0, false);
            Step(state, 2f);
            Assert.Greater(state.OutOfBoundsRemaining(0), 0f);

            state.SetInBounds(0, true);
            state.Tick(0.1f);
            Assert.AreEqual(-1f, state.OutOfBoundsRemaining(0));
            Assert.AreEqual(PlayerStatus.Alive, state.Status(0));
        }

        [Test]
        public void Solo_OutOfBoundsPastGrace_FailsTheRun()
        {
            var state = Playing(1);

            state.SetInBounds(0, false);
            Step(state, 3.2f);

            Assert.AreEqual(PlayerStatus.Out, state.Status(0));
            Assert.AreEqual(RunPhase.Failed, state.Phase);
        }

        [Test]
        public void Coop_FirstOutIsFree_RejoinsAfterDelay()
        {
            var state = Playing(2);
            var outs = new List<(int, bool)>();
            var rejoined = new List<int>();
            state.PlayerOut += (p, willRejoin) => outs.Add((p, willRejoin));
            state.PlayerRejoined += rejoined.Add;

            state.ReportDepleted(1);
            Assert.AreEqual(PlayerStatus.Rejoining, state.Status(1));
            CollectionAssert.AreEqual(new[] { (1, true) }, outs);

            Step(state, 5.1f);
            Assert.AreEqual(PlayerStatus.Alive, state.Status(1));
            CollectionAssert.AreEqual(new[] { 1 }, rejoined);
            Assert.IsFalse(state.HasFreeRespawn(1));
        }

        [Test]
        public void Coop_SecondOutIsFinal_UntilPartnerResurrects()
        {
            var state = Playing(2);
            state.ReportDepleted(1);
            Step(state, 5.1f);

            state.ReportDepleted(1);
            Assert.AreEqual(PlayerStatus.Out, state.Status(1));
            Step(state, 10f);
            Assert.AreEqual(PlayerStatus.Out, state.Status(1));
            Assert.AreEqual(RunPhase.Playing, state.Phase);

            Assert.IsTrue(state.TryResurrect(0));
            Assert.AreEqual(PlayerStatus.Alive, state.Status(1));
        }

        [Test]
        public void Resurrect_WithNobodyOut_IsNotConsumed()
        {
            var state = Playing(2);

            Assert.IsFalse(state.TryResurrect(0));
        }

        [Test]
        public void Coop_NobodyAlive_Fails_EvenWithARejoinPending()
        {
            var state = Playing(2);

            state.ReportDepleted(0);
            state.ReportDepleted(1);

            Assert.AreEqual(RunPhase.Failed, state.Phase);
        }

        [Test]
        public void Finish_WhenEveryLivingPlayerCrossed()
        {
            var state = Playing(2);

            state.ReportCrossedFinish(0);
            Assert.AreEqual(RunPhase.Playing, state.Phase);
            Assert.AreEqual(RunRules.Default.lastCall, state.LastCallRemaining, 1e-5f);

            state.ReportCrossedFinish(1);
            Assert.AreEqual(RunPhase.Finished, state.Phase);
        }

        [Test]
        public void Finish_LastCallExpires_WithoutTheStraggler()
        {
            var state = Playing(2);

            state.ReportCrossedFinish(0);
            Step(state, 5.1f);

            Assert.AreEqual(RunPhase.Finished, state.Phase);
            Assert.IsFalse(state.HasCrossed(1));
        }

        [Test]
        public void Finish_IgnoresAPartnerWhoIsOut()
        {
            var state = Playing(2);
            state.ReportDepleted(1); // rejoining, not alive

            state.ReportCrossedFinish(0);

            Assert.AreEqual(RunPhase.Finished, state.Phase);
        }

        [Test]
        public void CrossedPlayers_AreSafeFromOutOfBounds()
        {
            var state = Playing(2);
            state.ReportCrossedFinish(0);

            state.SetInBounds(0, false);
            Step(state, 4f);

            Assert.AreEqual(PlayerStatus.Alive, state.Status(0));
        }

        [Test]
        public void Client_MirrorsTheHostsSnapshots_WithTheSameEvents()
        {
            var host = Playing(2);
            var client = new RunState(2, RunRules.Default);
            var clientEvents = new System.Collections.Generic.List<string>();
            client.PhaseChanged += p => clientEvents.Add("phase " + p);
            client.PlayerOut += (p, rejoin) => clientEvents.Add($"out {p} {rejoin}");
            client.PlayerRejoined += p => clientEvents.Add("rejoin " + p);

            client.ApplySnapshot(host.Snapshot());
            Assert.AreEqual(RunPhase.Playing, client.Phase);

            host.ReportDepleted(1); // free respawn: rejoining
            client.ApplySnapshot(host.Snapshot());
            Step(host, RunRules.Default.rejoinDelay + 0.2f);
            client.ApplySnapshot(host.Snapshot());
            host.ReportDepleted(1); // second time: out for good
            client.ApplySnapshot(host.Snapshot());
            host.ReportDepleted(0); // nobody left: failed
            client.ApplySnapshot(host.Snapshot());

            CollectionAssert.AreEqual(new[] { "phase Playing", "out 1 True", "rejoin 1", "out 1 False", "out 0 True", "phase Failed" }, clientEvents);
            Assert.AreEqual(host.Elapsed, client.Elapsed);
            for (int i = 0; i < 2; i++)
            {
                Assert.AreEqual(host.Status(i), client.Status(i));
                Assert.AreEqual(host.HasFreeRespawn(i), client.HasFreeRespawn(i));
            }

            var score = new RunScore(2);
            score.Restore(new[] { 120, 45 }, 7);
            Assert.AreEqual(172, score.Total);
        }
    }
}
