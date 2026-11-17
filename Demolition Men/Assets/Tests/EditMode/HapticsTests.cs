using Demolition.Meta;
using Demolition.Run;
using NUnit.Framework;

namespace Demolition.Tests
{
    public class HapticsTests
    {
        private const float Step = 0.001f;

        [Test]
        public void AHit_IsAShortTick_AndAWeaponsIsHarder()
        {
            Pulse fist = Haptics.Hit(false), weapon = Haptics.Hit(true);
            Assert.IsFalse(fist.IsNone);
            Assert.Less(fist.Seconds, 0.1f);
            Assert.Greater(weapon.Strength, fist.Strength);
            Assert.Less(weapon.Seconds, 0.1f);
        }

        [Test]
        public void ACollapse_GrowsWithItsCells_UpToTheBigOne()
        {
            Pulse one = Haptics.Collapse(1, true, 0f), ten = Haptics.Collapse(10, true, 0f), big = Haptics.Collapse(Haptics.BigCollapse, true, 0f);
            Assert.Greater(one.Strength, Haptics.Hit(false).Strength, "a block coming down beats the hit that did it");
            Assert.Greater(ten.Strength, one.Strength);
            Assert.Greater(ten.Seconds, one.Seconds);
            Assert.Greater(big.Strength, ten.Strength);
            Assert.AreEqual(1f, big.Strength, 1e-4f);

            Pulse street = Haptics.Collapse(130, true, 0f);
            Assert.AreEqual(big.Strength, street.Strength);
            Assert.AreEqual(big.Seconds, street.Seconds);
            Assert.LessOrEqual(street.Seconds, Haptics.MaxSeconds);
            Assert.Less(Haptics.MaxSeconds, 1f, "never seconds");
        }

        [Test]
        public void SomebodyElsesCollapse_FadesWithDistance_OnesOwnDoesNot()
        {
            Pulse own = Haptics.Collapse(20, true, 100f);
            Assert.AreEqual(Haptics.Collapse(20, true, 0f).Strength, own.Strength);

            Pulse beside = Haptics.Collapse(20, false, 0f), across = Haptics.Collapse(20, false, Haptics.CollapseRange * 0.5f);
            Assert.AreEqual(own.Strength, beside.Strength, 1e-4f);
            Assert.Less(across.Strength, beside.Strength);
            Assert.IsFalse(across.IsNone);
            Assert.IsTrue(Haptics.Collapse(Haptics.BigCollapse, false, Haptics.CollapseRange).IsNone, "out of range");
        }

        [Test]
        public void ABlast_FadesToNothingAtItsRange()
        {
            Assert.AreEqual(Haptics.BlastStrength, Haptics.Blast(0f).Strength, 1e-4f);
            Assert.Less(Haptics.Blast(5f).Strength, Haptics.Blast(1f).Strength);
            Assert.IsTrue(Haptics.Blast(Haptics.BlastRange).IsNone);
            Assert.IsTrue(Haptics.Blast(50f).IsNone);
        }

        [Test]
        public void DamageTaken_JoltsHarderTheMoreItIs_AndNoneForNone()
        {
            Assert.IsTrue(Haptics.Hurt(0f).IsNone);
            Assert.GreaterOrEqual(Haptics.Hurt(1f).Strength, Haptics.HurtMinStrength);
            Assert.Greater(Haptics.Hurt(20f).Strength, Haptics.Hurt(5f).Strength);
            Assert.AreEqual(1f, Haptics.Hurt(Haptics.HurtFull).Strength, 1e-4f);
            Assert.AreEqual(1f, Haptics.Hurt(500f).Strength, 1e-4f);
        }

        [Test]
        public void EveryPulse_StaysWithinTheCaps()
        {
            foreach (Pulse p in new[] { new Pulse(7f, 9f), Haptics.Collapse(100000, true, 0f), Haptics.Hurt(1e6f), Haptics.Blast(-3f) })
            {
                Assert.LessOrEqual(p.Strength, 1f);
                Assert.LessOrEqual(p.Seconds, Haptics.MaxSeconds);
            }
        }

        [Test]
        public void HitsWithoutEnd_StayTicks_NotAHum()
        {
            var hands = new HapticChannel();
            Buzz(hands, Haptics.Hit(false), 2000, out int played, out int on, out int longest);
            Assert.That(played, Is.InRange(10, 20), "ticks come through, a few a second");
            Assert.LessOrEqual(on, 2000 * 0.45f, "the hands are still more than they buzz");
            Assert.LessOrEqual(longest * Step, Haptics.HitSeconds + 2f * Step, "each tick ends as it would alone");
        }

        [Test]
        public void TheStrongestPlays_AndNothingAddsUp()
        {
            var hands = new HapticChannel();
            Pulse collapse = Haptics.Collapse(Haptics.BigCollapse, true, 0f);
            Assert.IsTrue(hands.Play(Haptics.Hit(false), 0f));
            Assert.IsTrue(hands.Play(collapse, 0.01f), "the collapse takes over from the tick");
            Assert.AreEqual(collapse.Strength, hands.StrengthAt(0.02f));

            Assert.IsFalse(hands.Play(Haptics.Hit(true), 0.1f), "a tick doesn't cut it short");
            Assert.IsFalse(hands.Play(collapse, 0.2f), "nor does one as strong make it longer");
            Assert.AreEqual(collapse.Strength, hands.StrengthAt(0.01f + collapse.Seconds - 0.01f));
            Assert.AreEqual(0f, hands.StrengthAt(0.01f + collapse.Seconds + 0.001f), "it ends when the first one would have");
        }

        [Test]
        public void AStreetComingDownInStages_IsFeltInBursts()
        {
            var hands = new HapticChannel();
            Buzz(hands, Haptics.Collapse(130, true, 0f), 3000, out int played, out int on, out int longest);
            Assert.AreEqual(4, played);
            Assert.LessOrEqual(longest * Step, Haptics.MaxSeconds + 2f * Step);
            Assert.LessOrEqual(on, 3000 * 0.7f, "a third of the time is rest");
        }

        // Asks for the same pulse every millisecond: how many played, for how many milliseconds
        // the hands buzzed, and the longest they did without a break.
        private static void Buzz(HapticChannel hands, Pulse pulse, int milliseconds, out int played, out int on, out int longest)
        {
            played = on = longest = 0;
            for (int i = 0, stretch = 0; i < milliseconds; i++)
            {
                if (hands.Play(pulse, i * Step))
                    played++;
                stretch = hands.StrengthAt(i * Step) > 0f ? stretch + 1 : 0;
                longest = System.Math.Max(longest, stretch);
                if (stretch > 0)
                    on++;
            }
        }

        [Test]
        public void AfterTheRest_TheNextOnePlays_AndStopSilencesAtOnce()
        {
            var hands = new HapticChannel();
            Pulse tick = Haptics.Hit(false);
            Assert.IsTrue(hands.Play(tick, 0f));
            Assert.IsFalse(hands.Play(tick, tick.Seconds + Haptics.MinRest * 0.5f), "resting");
            Assert.IsTrue(hands.Play(Haptics.Hurt(20f), tick.Seconds + Haptics.MinRest * 0.5f), "a stronger one breaks the rest");
            hands.Stop();
            Assert.AreEqual(0f, hands.StrengthAt(tick.Seconds + Haptics.MinRest * 0.5f));
            Assert.IsTrue(hands.Play(tick, tick.Seconds + Haptics.MinRest * 0.5f), "and nothing is owed after a stop");
            Assert.IsFalse(hands.Play(new Pulse(0f, 1f), 10f));
        }

        [Test]
        public void AGamepadsHeavyMotor_SitsOutTheTicks()
        {
            Assert.AreEqual(0f, Haptics.Hit(false).Heavy);
            Assert.Greater(Haptics.Collapse(10, true, 0f).Heavy, 0f);
            Assert.AreEqual(1f, Haptics.Collapse(Haptics.BigCollapse, true, 0f).Heavy, 1e-4f);
        }

        [Test]
        public void APhone_GetsMillisecondsAndAnAmplitude_OrALengthAlone()
        {
            Pulse big = Haptics.Collapse(Haptics.BigCollapse, true, 0f), tick = Haptics.Hit(false);
            Assert.AreEqual(500, big.Milliseconds);
            Assert.AreEqual(255, big.Amplitude);
            Assert.AreEqual(50, tick.Milliseconds);
            Assert.That(tick.Amplitude, Is.InRange(1, 254));
            Assert.AreEqual(1, new Pulse(0.001f, 0.1f).Amplitude, "0 would be an error on Android");

            Assert.AreEqual(500, big.OnOffMilliseconds);
            Assert.Less(tick.OnOffMilliseconds, tick.Milliseconds, "weaker is shorter where the motor is only on or off");
            Assert.GreaterOrEqual(tick.OnOffMilliseconds, Haptics.ShortestOnOff);
        }

        [Test]
        public void Vibration_IsOnByDefault() => Assert.IsTrue(new GameSettings().vibration);
    }
}
