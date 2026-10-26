using Demolition.Actions;
using NUnit.Framework;

namespace Demolition.Tests
{
    /// <summary>When the bot fires its action (<see cref="BotTactics"/>): where a player would,
    /// never where the action or the camera kills it.</summary>
    public class BotTacticsTests
    {
        // Out in the street, healthy, nothing ahead, all the time in the world.
        private static BotSight Clear() => new BotSight
        {
            FacingAhead = true, Grounded = true, Headroom = true, Health = 1f,
            Wall = float.PositiveInfinity, NearestCharge = float.PositiveInfinity, Room = float.PositiveInfinity,
        };

        private static BotSight AtBuilding(float wall)
        {
            BotSight s = Clear();
            s.Wall = wall;
            s.WallIsBuilding = true;
            return s;
        }

        [Test]
        public void Lock_CoversEveryPassOfAHeldPose()
        {
            Assert.AreEqual(1.6f, BotTactics.LockSeconds(ActionCatalog.Get(ActionKind.Burp)), 1e-4f);
            Assert.AreEqual(6.4f, BotTactics.LockSeconds(ActionCatalog.Get(ActionKind.ShovelLean)), 1e-4f, "5 s of 1.6 s passes");
            Assert.AreEqual(1.53f, BotTactics.LockSeconds(ActionCatalog.Get(ActionKind.ButtSlam)), 1e-4f, "the airborne pose as a second");
        }

        [Test]
        public void Burp_AtABuildingJustAhead_FromOutside()
        {
            Assert.IsTrue(BotTactics.Fire(ActionKind.Burp, AtBuilding(2f)));
            Assert.IsFalse(BotTactics.Fire(ActionKind.Burp, AtBuilding(6f)), "out of the band's reach");
            Assert.IsFalse(BotTactics.Fire(ActionKind.Burp, AtBuilding(1f)), "the floor over the doorway lands on its head");
            BotSight running = AtBuilding(3f);
            running.Speed = 8f;
            Assert.IsFalse(BotTactics.Fire(ActionKind.Burp, running), "at a run it coasts on under it");
            BotSight prop = AtBuilding(2f);
            prop.WallIsBuilding = false;
            Assert.IsFalse(BotTactics.Fire(ActionKind.Burp, prop), "not on a car");
            BotSight inside = AtBuilding(2f);
            inside.Headroom = false;
            Assert.IsFalse(BotTactics.Fire(ActionKind.Burp, inside), "under a floor it would drop on itself");
            BotSight facingBack = AtBuilding(2f);
            facingBack.FacingAhead = false;
            Assert.IsFalse(BotTactics.Fire(ActionKind.Burp, facingBack));
            BotSight debris = AtBuilding(2f);
            debris.Debris = true;
            Assert.IsFalse(BotTactics.Fire(ActionKind.Burp, debris));
        }

        [Test]
        public void Implosion_OnlyWhereItsChargesDontReach()
        {
            BotSight s = Clear();
            s.Charges = 3;
            s.NearestCharge = 6f;
            Assert.IsTrue(BotTactics.Fire(ActionKind.Implosion, s));
            s.Speed = 8f;
            Assert.IsFalse(BotTactics.Fire(ActionKind.Implosion, s), "at a run it coasts on into them");
            s.Speed = 0f;
            s.NearestCharge = 3f;
            Assert.IsFalse(BotTactics.Fire(ActionKind.Implosion, s), "inside the blast");
            s.NearestCharge = 6f;
            s.FloorsBelow = 3;
            Assert.IsFalse(BotTactics.Fire(ActionKind.Implosion, s), "on the roof that drops");
            s.FloorsBelow = 0;
            s.Charges = 1;
            Assert.IsFalse(BotTactics.Fire(ActionKind.Implosion, s), "a prop, or one charge on the street");
        }

        [Test]
        public void Slam_OnARoof_OrAtAWallOnceItWaitedForOne()
        {
            BotSight roof = Clear();
            roof.FloorsBelow = 2;
            Assert.IsTrue(BotTactics.Fire(ActionKind.ButtSlam, roof));
            BotSight street = AtBuilding(1f);
            Assert.IsFalse(BotTactics.Fire(ActionKind.ButtSlam, street));
            street.FullFor = BotTactics.SlamWait + 1f;
            Assert.IsTrue(BotTactics.Fire(ActionKind.ButtSlam, street));
            roof.Headroom = false;
            Assert.IsFalse(BotTactics.Fire(ActionKind.ButtSlam, roof), "the hop hits the ceiling");
        }

        [Test]
        public void Vest_WhenDebrisComesDown_EvenWhileBackingOff()
        {
            BotSight s = Clear();
            Assert.IsFalse(BotTactics.Fire(ActionKind.HiVis, s));
            s.Debris = true;
            s.Retreating = true;
            s.FacingAhead = false;
            s.Grounded = false;
            Assert.IsTrue(BotTactics.Fire(ActionKind.HiVis, s));
        }

        [Test]
        public void ShovelLean_WhenHurtOrThePartnerHits_WithTimeBeforeTheCameraLeaves()
        {
            BotSight s = Clear();
            Assert.IsFalse(BotTactics.Fire(ActionKind.ShovelLean, s), "nothing to heal");
            s.Health = 0.4f;
            Assert.IsTrue(BotTactics.Fire(ActionKind.ShovelLean, s));
            s.Room = 5f;
            Assert.IsFalse(BotTactics.Fire(ActionKind.ShovelLean, s), "6.4 s leaning: out of bounds");
            s = Clear();
            s.PartnerWorking = true;
            Assert.IsTrue(BotTactics.Fire(ActionKind.ShovelLean, s));
        }

        [Test]
        public void Crane_OntoARoofAhead()
        {
            BotSight s = Clear();
            Assert.IsFalse(BotTactics.Fire(ActionKind.CraneDrop, s));
            s.CraneTarget = true;
            Assert.IsTrue(BotTactics.Fire(ActionKind.CraneDrop, s));
            s.FloorsBelow = 2;
            Assert.IsFalse(BotTactics.Fire(ActionKind.CraneDrop, s), "onto the roof it stands on");
            s.FloorsBelow = 0;
            s.Retreating = true;
            Assert.IsFalse(BotTactics.Fire(ActionKind.CraneDrop, s), "its dynamite is still out");
        }
    }
}
