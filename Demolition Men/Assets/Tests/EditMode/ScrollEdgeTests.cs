using Demolition.Run;
using NUnit.Framework;

namespace Demolition.Tests
{
    public class ScrollEdgeTests
    {
        private static void Run(ScrollEdge edge, float seconds, float frontX)
        {
            for (int i = 0; i < seconds * 10f; i++)
                edge.Tick(0.1f, frontX);
        }

        [Test]
        public void StartLeft_KeepsTheSpawnInView_AndNothingLeftOfTheLevel()
        {
            Assert.AreEqual(36.5f - ScrollEdge.SpawnRoom, ScrollEdge.StartLeft(36.5f), 0.001f);
            Assert.AreEqual(ScrollEdge.LevelLeft, ScrollEdge.StartLeft(2f), 0.001f);
        }

        [Test]
        public void StaysPut_UntilTheFrontPlayerReachesThePushLine()
        {
            var edge = new ScrollEdge(10f, 2f);
            Run(edge, 5f, 10f + ScrollEdge.PushDistance - 0.1f);
            Assert.AreEqual(10f, edge.Left);
            Assert.IsFalse(edge.Pushed);
        }

        [Test]
        public void ThePush_KeepsTheFrontPlayerOnThePushLine_ThenItScrollsOnItsOwn()
        {
            var edge = new ScrollEdge(10f, 2f);
            edge.Tick(0.1f, 50f);
            Assert.AreEqual(50f - ScrollEdge.PushDistance, edge.Left, 0.001f);
            Assert.IsTrue(edge.Pushed);

            Run(edge, 5f, 40f); // the front player has gone back: the edge doesn't wait
            Assert.AreEqual(50f - ScrollEdge.PushDistance + 10f, edge.Left, 0.01f);
        }

        [Test]
        public void GoesOnScrolling_WithNobodyInFront()
        {
            var edge = new ScrollEdge(10f, 2f);
            edge.Tick(0.1f, 50f);
            float left = edge.Left;
            Run(edge, 1f, float.NegativeInfinity);
            Assert.AreEqual(left + 2f, edge.Left, 0.01f);
        }

        [Test]
        public void StopsWithTheFinishOnThePushLine_AndNeverGoesBack()
        {
            var edge = new ScrollEdge(10f, 2f, 100f);
            Run(edge, 1f, 90f);
            Run(edge, 60f, 90f);
            Assert.AreEqual(100f - ScrollEdge.PushDistance, edge.Left, 0.001f);

            edge.FinishX = 50f; // a finish behind the edge doesn't pull it back
            edge.Tick(0.1f, 90f);
            Assert.AreEqual(100f - ScrollEdge.PushDistance, edge.Left, 0.001f);
        }

        [Test]
        public void FollowingTheHost_CatchesUpWhenBehind_WithoutJumping()
        {
            var edge = new ScrollEdge(10f, 2f);
            edge.Follow(20f);
            edge.Tick(0.1f, float.NegativeInfinity);
            Assert.AreEqual(10f + ScrollEdge.CatchUpSpeed * 0.1f, edge.Left, 0.001f);
            Assert.IsTrue(edge.Pushed);

            Run(edge, 2f, float.NegativeInfinity);
            Assert.AreEqual(20f + ScrollEdge.FollowLead, edge.Left, 0.25f); // then scrolls up to the lead, and waits
        }

        [Test]
        public void FollowingTheHost_WaitsWhenAhead_ButItsOwnPlayerStillPushes()
        {
            var edge = new ScrollEdge(10f, 2f);
            edge.Follow(10f);
            edge.Tick(0.1f, 40f);
            float pushed = 40f - ScrollEdge.PushDistance;
            Assert.AreEqual(pushed, edge.Left, 0.001f);

            Run(edge, 5f, 0f); // far ahead of the host's: it holds, and never goes back
            Assert.AreEqual(pushed, edge.Left, 0.001f);

            edge.Tick(0.1f, 60f);
            Assert.AreEqual(60f - ScrollEdge.PushDistance, edge.Left, 0.001f);
        }
    }
}
