using Demolition.Online;
using NUnit.Framework;

namespace Demolition.Tests
{
    /// <summary>The relay link watchdog (docs/multiplayer.md O4), ticked at 60 fps.</summary>
    public class LinkWatchTests
    {
        private const float Frame = 1f / 60f;

        // Ticks every frame from `from` to `to`; the peer is heard every second when `peerAlive`.
        private static LinkVerdict Run(LinkWatch watch, float from, float to, bool paired, bool peerAlive)
        {
            float lastPing = from;
            for (float t = from; t <= to; t += Frame)
            {
                if (peerAlive && t - lastPing >= 1f)
                {
                    watch.Heard(t);
                    lastPing = t;
                }
                LinkVerdict verdict = watch.Tick(t, paired);
                if (verdict != LinkVerdict.Ok)
                    return verdict;
            }
            return LinkVerdict.Ok;
        }

        [Test]
        public void NoAnswerFromTheRelay_IsUnreachable_AfterTheTimeout()
        {
            var watch = new LinkWatch();
            watch.Opened(0f);
            Assert.AreEqual(LinkVerdict.Ok, Run(watch, 0f, LinkWatch.Timeout - 0.5f, false, false));
            Assert.AreEqual(LinkVerdict.Unreachable, Run(watch, LinkWatch.Timeout - 0.5f, LinkWatch.Timeout + 1f, false, false));
        }

        [Test]
        public void HostWaitingInTheLobby_NeverTimesOut()
        {
            var watch = new LinkWatch();
            watch.Opened(0f);
            watch.Ready();
            Assert.AreEqual(LinkVerdict.Ok, Run(watch, 0f, 120f, paired: false, peerAlive: false));
        }

        [Test]
        public void PingingPeer_StaysOk_AndASilentOneIsDroppedAfterTheTimeout()
        {
            var watch = new LinkWatch();
            watch.Opened(0f);
            watch.Ready();
            Assert.AreEqual(LinkVerdict.Ok, Run(watch, 0f, 60f, true, true));
            Assert.AreEqual(LinkVerdict.PeerSilent, Run(watch, 60f, 60f + LinkWatch.Timeout + 1f, true, false));
        }

        [Test]
        public void ThisDeviceFrozenPastTheTimeout_IsAway()
        {
            var watch = new LinkWatch();
            watch.Opened(0f);
            watch.Ready();
            Assert.AreEqual(LinkVerdict.Ok, Run(watch, 0f, 5f, true, true));
            Assert.AreEqual(LinkVerdict.Away, watch.Tick(5f + LinkWatch.Timeout + 1f, true));
        }

        [Test]
        public void OwnHitch_IsForgiven_SoQueuedPingsCanArrive()
        {
            var watch = new LinkWatch();
            watch.Opened(0f);
            watch.Ready();
            watch.Heard(1f);
            watch.Tick(1f, true);
            // A 9 s load on this device: nothing was heard meanwhile, but the hitch was ours.
            Assert.AreEqual(LinkVerdict.Ok, watch.Tick(10f, true));
            Assert.AreEqual(LinkVerdict.Ok, Run(watch, 10f + Frame, 19.5f, true, false), "silence is measured from the end of the hitch");
            Assert.AreEqual(LinkVerdict.PeerSilent, Run(watch, 19.5f, 21f, true, false));
        }
    }
}
