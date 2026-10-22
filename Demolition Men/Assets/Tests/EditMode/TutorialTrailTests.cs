using Demolition.Run;
using NUnit.Framework;

namespace Demolition.Tests
{
    public class TutorialTrailTests
    {
        private static TutorialTrail Trail() => new TutorialTrail(new[] { 2f, 20f, 40f });

        [Test]
        public void NothingShown_BeforeTheFirstMarker()
        {
            TutorialTrail trail = Trail();
            Assert.IsFalse(trail.Reach(0f));
            Assert.AreEqual(-1, trail.Current);
            Assert.IsFalse(trail.IsShown(0));
        }

        [Test]
        public void CurrentSign_FollowsTheFrontPlayer_InsideItsStretch()
        {
            TutorialTrail trail = Trail();
            Assert.IsTrue(trail.Reach(2f));
            Assert.AreEqual(0, trail.Current);
            Assert.AreEqual(5f, trail.SignX(0, 2f, 3f), 1e-5f, "ahead of the player until it fits");
            Assert.AreEqual(10f, trail.SignX(0, 10f, 3f), 1e-5f, "over the player");
            Assert.AreEqual(17f, trail.SignX(0, 19f, 3f), 1e-5f, "its right edge stops at the next marker");
        }

        [Test]
        public void PassedSign_StaysAtTheNextMarker_AndTheNextTakesOver()
        {
            TutorialTrail trail = Trail();
            trail.Reach(10f);
            Assert.IsTrue(trail.Reach(21f));
            Assert.AreEqual(1, trail.Current);
            Assert.IsTrue(trail.IsShown(0));
            Assert.AreEqual(20f, trail.End(0));
            Assert.AreEqual(17f, trail.SignX(0, 21f, 3f), 1e-5f);
            Assert.AreEqual(17f, trail.SignX(0, 35f, 3f), 1e-5f, "it doesn't move on with the player");
            Assert.AreEqual(23f, trail.SignX(1, 21f, 3f), 1e-5f, "the next starts past the boundary");
        }

        [Test]
        public void WalkingBack_DoesNotBringAnEarlierSignBack()
        {
            TutorialTrail trail = Trail();
            trail.Reach(25f);
            Assert.IsFalse(trail.Reach(15f));
            Assert.AreEqual(1, trail.Current);
            Assert.AreEqual(23f, trail.SignX(1, 15f, 3f), 1e-5f);
        }

        [Test]
        public void PassingSeveralMarkersAtOnce_SkipsToTheFurthest()
        {
            TutorialTrail trail = Trail();
            Assert.IsTrue(trail.Reach(45f));
            Assert.AreEqual(2, trail.Current);
            Assert.IsTrue(trail.IsShown(0) && trail.IsShown(1));
            Assert.AreEqual(37f, trail.SignX(1, 45f, 3f), 1e-5f);
        }

        [Test]
        public void NarrowStretch_CentresTheSign()
        {
            TutorialTrail trail = Trail();
            trail.Reach(3f);
            Assert.AreEqual(11f, trail.SignX(0, 3f, 12f), 1e-5f);
        }

        [Test]
        public void LastSign_FollowsToTheEnd()
        {
            TutorialTrail trail = Trail();
            trail.Reach(41f);
            Assert.AreEqual(float.PositiveInfinity, trail.End(2));
            Assert.AreEqual(float.PositiveInfinity, trail.Width(2));
            Assert.AreEqual(18f, trail.Width(0));
            Assert.AreEqual(43f, trail.SignX(2, 41f, 3f), 1e-5f);
            Assert.AreEqual(500f, trail.SignX(2, 500f, 3f), 1e-5f);
        }
    }
}
