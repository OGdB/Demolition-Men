using System;
using System.Security.Cryptography;
using System.Text;
using Demolition.Actions;
using Demolition.Ranked;
using NUnit.Framework;

namespace Demolition.Tests
{
    public class RunLogTests
    {
        private static RunLog Sample()
        {
            RunLog log = RunLog.For(2);
            log.Rules = 3;
            log.Mode = RankedMode.Coop;
            log.Online = true;
            log.Day = 20261028;
            log.Template = "town-03";
            log.Seed = -12345;
            log.LayoutHash = int.MinValue + 7;
            log.Actions[1] = ActionKind.SiteRadio;
            for (int i = 0; i < 30; i++)
            {
                log.Poses[0].Add(new Pose(12.5f + i * 0.8f, 3f + (i % 4) * 0.25f, i % 7 == 0));
                log.Poses[1].Add(new Pose(10f - i * 0.1f, -2.5f, true));
            }
            log.Events.Add(new LogEvent { TimeMs = 120, Kind = LogKind.Pickup, Player = 0, Index = 4 });
            log.Events.Add(new LogEvent
            {
                TimeMs = 900, Kind = LogKind.Damage, Player = 1, Building = 3, CellX = -1, CellY = 6, Amount = 12.345678f,
                Cause = HitCause.Axe, Broke = true, X = 40.25f, Y = 1.5f,
            });
            log.Events.Add(new LogEvent { TimeMs = 900, Kind = LogKind.Damage, Building = 3, CellX = 2, CellY = 0, Amount = 0.001f, Cause = HitCause.Crumble });
            log.Events.Add(new LogEvent { TimeMs = 1500, Kind = LogKind.Throw, Player = 0, X = 1f, Y = 2f });
            log.Events.Add(new LogEvent { TimeMs = 2600, Kind = LogKind.Blast, Player = 0, Cause = HitCause.Dynamite, X = 7.5f, Y = 0.25f });
            log.Events.Add(new LogEvent { TimeMs = 3000, Kind = LogKind.ActionStart, Player = 1, Action = ActionKind.SiteRadio });
            log.Events.Add(new LogEvent
            {
                TimeMs = 3400, Kind = LogKind.ActionStrike, Player = 1, Action = ActionKind.SiteRadio, Facing = -1, X = 5f, Y = 1f,
                Picks = new[] { 6f, 0.5f },
            });
            log.Events.Add(new LogEvent { TimeMs = 5000, Kind = LogKind.Out, Player = 1, Rejoins = true });
            log.Events.Add(new LogEvent { TimeMs = 9000, Kind = LogKind.Rejoin, Player = 1, X = 30f, Y = 1f });
            log.Events.Add(new LogEvent { TimeMs = 60000, Kind = LogKind.Finish });
            log.Score = 26700;
            log.TimeMs = 60000;
            return log;
        }

        [Test]
        public void Log_RoundTrips()
        {
            RunLog log = Sample();
            RunLog back = RunLog.Decode(log.Encode());

            Assert.AreEqual(log.Rules, back.Rules);
            Assert.AreEqual(log.Mode, back.Mode);
            Assert.IsTrue(back.Online);
            Assert.AreEqual(log.Day, back.Day);
            Assert.AreEqual(log.Template, back.Template);
            Assert.AreEqual(log.Seed, back.Seed);
            Assert.AreEqual(log.LayoutHash, back.LayoutHash);
            CollectionAssert.AreEqual(log.Actions, back.Actions);
            for (int p = 0; p < 2; p++)
            {
                Assert.AreEqual(log.Poses[p].Count, back.Poses[p].Count);
                for (int i = 0; i < log.Poses[p].Count; i++)
                {
                    Assert.AreEqual(log.Poses[p][i].X, back.Poses[p][i].X, 1f / 64f);
                    Assert.AreEqual(log.Poses[p][i].Y, back.Poses[p][i].Y, 1f / 64f);
                    Assert.AreEqual(log.Poses[p][i].Left, back.Poses[p][i].Left);
                }
            }
            Assert.AreEqual(log.Events.Count, back.Events.Count);
            for (int i = 0; i < log.Events.Count; i++)
            {
                LogEvent a = log.Events[i], b = back.Events[i];
                Assert.AreEqual(a.TimeMs, b.TimeMs);
                Assert.AreEqual(a.Kind, b.Kind);
                Assert.AreEqual(a.Player, b.Player);
            }
            LogEvent hit = back.Events[1];
            Assert.AreEqual(12.345678f, hit.Amount, "damage stays exact: the server applies the same float");
            Assert.AreEqual(HitCause.Axe, hit.Cause);
            Assert.IsTrue(hit.Broke);
            Assert.AreEqual((3, -1, 6), (hit.Building, hit.CellX, hit.CellY));
            Assert.AreEqual(40.25f, hit.X);
            Assert.AreEqual(-1, back.Events[2].Player);
            Assert.AreEqual(-1, back.Events[6].Facing);
            CollectionAssert.AreEqual(new[] { 6f, 0.5f }, back.Events[6].Picks);
            Assert.IsTrue(back.Events[7].Rejoins);
            Assert.AreEqual(4, back.Events[0].Index);
            Assert.AreEqual(log.Score, back.Score);
            Assert.AreEqual(log.TimeMs, back.TimeMs);
        }

        [Test]
        public void Log_IsSmall()
        {
            // About a minute and a half of solo play: 900 poses and 1500 hits.
            RunLog log = RunLog.For(1);
            for (int i = 0; i < 900; i++)
                log.Poses[0].Add(new Pose(i * 0.75f, 2f + (i % 10) * 0.1f, false));
            for (int i = 0; i < 1500; i++)
                log.Events.Add(new LogEvent { TimeMs = i * 60, Kind = LogKind.Damage, Player = 0, Building = i / 40, CellX = i % 8, CellY = i % 5, Amount = 12f, Cause = HitCause.Fist, X = i * 0.05f, Y = 1.3f });
            Assert.Less(log.Encode().Length, 24 * 1024);
        }

        [Test]
        public void Decode_RefusesAnythingOff()
        {
            byte[] good = Sample().Encode();
            Assert.DoesNotThrow(() => RunLog.Decode(good));
            for (int cut = 0; cut < good.Length; cut += 7)
            {
                var truncated = new byte[cut];
                Array.Copy(good, truncated, cut);
                Assert.Throws<FormatException>(() => RunLog.Decode(truncated), $"cut at {cut}");
            }
            var longer = new byte[good.Length + 1];
            Array.Copy(good, longer, good.Length);
            Assert.Throws<FormatException>(() => RunLog.Decode(longer));
            var other = (byte[])good.Clone();
            other[3] = RunLog.Format + 1;
            Assert.Throws<FormatException>(() => RunLog.Decode(other));
            Assert.Throws<FormatException>(() => RunLog.Decode(null));

            // Every single flipped byte either still decodes or is a FormatException, nothing else.
            var random = new Random(7);
            for (int i = 0; i < 2000; i++)
            {
                var flipped = (byte[])good.Clone();
                flipped[random.Next(flipped.Length)] ^= (byte)(1 + random.Next(255));
                try
                {
                    RunLog.Decode(flipped);
                }
                catch (FormatException)
                {
                }
            }

            RunLog solo = RunLog.For(2);
            solo.Mode = RankedMode.Solo;
            Assert.Throws<FormatException>(() => RunLog.Decode(solo.Encode()), "a solo run has one player");
        }

        [Test]
        public void Sha256_AndHmac_MatchDotNet()
        {
            var random = new Random(3);
            foreach (int length in new[] { 0, 1, 55, 56, 63, 64, 65, 119, 128, 1000, 4097 })
            {
                var data = new byte[length];
                random.NextBytes(data);
                using (SHA256 sha = SHA256.Create())
                    CollectionAssert.AreEqual(sha.ComputeHash(data), Seal.Sha256(data), $"sha {length}");
                foreach (int keyLength in new[] { 16, 64, 100 })
                {
                    var key = new byte[keyLength];
                    random.NextBytes(key);
                    using (var hmac = new HMACSHA256(key))
                        CollectionAssert.AreEqual(hmac.ComputeHash(data), Seal.Hmac(key, data), $"hmac {length}/{keyLength}");
                }
            }
            Assert.AreEqual("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
                BitConverter.ToString(Seal.Sha256(Encoding.ASCII.GetBytes("abc"))).Replace("-", "").ToLowerInvariant());
        }

        [Test]
        public void Seal_HidesTheLog_AndOnlyItsKeyOpensIt_Unchanged()
        {
            byte[] plain = Sample().Encode();
            byte[] key = Encoding.ASCII.GetBytes("attempt key 0123456789abcdef");
            byte[] sealedLog = Seal.Close(plain, key);

            Assert.AreEqual(plain.Length + Seal.TagBytes, sealedLog.Length);
            Assert.AreNotEqual(plain[0], sealedLog[0], "the magic isn't readable");
            CollectionAssert.AreEqual(plain, Seal.Open(sealedLog, key));

            byte[] otherKey = (byte[])key.Clone();
            otherKey[0] ^= 1;
            Assert.IsNull(Seal.Open(sealedLog, otherKey), "another attempt's key");
            for (int i = 0; i < sealedLog.Length; i += 5)
            {
                var changed = (byte[])sealedLog.Clone();
                changed[i] ^= 0x20;
                Assert.IsNull(Seal.Open(changed, key), $"byte {i} changed");
            }
            Assert.IsNull(Seal.Open(new byte[10], key));
        }
    }
}
