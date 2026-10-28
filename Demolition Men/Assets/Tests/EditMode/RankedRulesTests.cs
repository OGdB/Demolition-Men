using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Demolition.Ranked;
using NUnit.Framework;

namespace Demolition.Tests
{
    /// <summary>The server checks ranked runs with RankedRules: they must be the numbers the game plays by.</summary>
    public class RankedRulesTests
    {
        private static float Field(string yaml, string name)
        {
            MatchCollection found = Regex.Matches(yaml, $@"^\s*{Regex.Escape(name)}: ([-0-9.eE]+)\s*$", RegexOptions.Multiline);
            Assert.AreEqual(1, found.Count, $"one '{name}'");
            return float.Parse(found[0].Groups[1].Value, CultureInfo.InvariantCulture);
        }

        private static float[] Profile(string yaml, string name)
        {
            Match m = Regex.Match(yaml, $@"{name}:\s*\n\s*wood: (\S+)\s*\n\s*brick: (\S+)\s*\n\s*metal: (\S+)\s*\n\s*glass: (\S+)\s*\n\s*support: (\S+)");
            Assert.IsTrue(m.Success, name);
            float F(int g) => float.Parse(m.Groups[g].Value, CultureInfo.InvariantCulture);
            return new[] { F(2), F(3), F(1), F(4), F(5) }; // BlockMaterial order: brick, metal, wood, glass, support
        }

        [TestCase("Assets/Prefabs/Demolition/Player_1.prefab")]
        [TestCase("Assets/Prefabs/Demolition/Player_2.prefab")]
        public void Players_MatchThePrefab(string path)
        {
            string yaml = File.ReadAllText(path);
            Assert.AreEqual(RankedRules.RunSpeed, Field(yaml, "maxSpeed"));
            Assert.AreEqual(RankedRules.JumpSpeed, Field(yaml, "jumpImpulse") / Field(yaml, "m_Mass"), 1e-4f);
            Assert.AreEqual(RankedRules.Gravity, 9.81f * Field(yaml, "m_GravityScale"), 1e-4f);
            Assert.AreEqual(RankedRules.AttackRadius, Field(yaml, "attackRadius"));
            StringAssert.Contains($"attackOffset: {{x: {RankedRules.AttackOffsetX.ToString(CultureInfo.InvariantCulture)}, y: {RankedRules.AttackOffsetY.ToString(CultureInfo.InvariantCulture)}}}", yaml);
            Assert.AreEqual(1f, Field(yaml, "damageMultiplier"));
            Assert.AreEqual(RankedRules.HeadbuttFactor, Field(yaml, "headbuttFactor"));
            Assert.AreEqual(RankedRules.HeadHeight, Field(yaml, "headHeight"));
            Assert.AreEqual(RankedRules.MaxDynamite, Field(yaml, "maxDynamite"));
            CollectionAssert.AreEqual(RankedRules.Fist, Profile(yaml, "fistDamage"));
            CollectionAssert.AreEqual(RankedRules.Axe, Profile(yaml, "axeDamage"));
            CollectionAssert.AreEqual(RankedRules.WreckingBall, Profile(yaml, "wreckingDamage"));
        }

        [Test]
        public void Dynamite_AndTheRun_MatchTheirPrefabs()
        {
            string stick = File.ReadAllText("Assets/Prefabs/Demolition/Dynamite.prefab");
            Assert.AreEqual(RankedRules.BlastDamage, Field(stick, "damage"));
            Assert.AreEqual(RankedRules.BlastRadius, Field(stick, "areaOfEffect") * 0.5f);
            Assert.AreEqual(RankedRules.Fuse, Field(stick, "fuseTime"));

            string run = File.ReadAllText("Assets/Prefabs/Run/Run.prefab");
            Assert.AreEqual(RankedRules.RejoinPenalty, Field(run, "rejoinPenalty"));
            StringAssert.Contains($"rejoinOffset: {{x: {RankedRules.RejoinOffsetX.ToString(CultureInfo.InvariantCulture)}, y: {RankedRules.RejoinOffsetY.ToString(CultureInfo.InvariantCulture)}}}", run);
        }

        // The swing clips' applyDamage events decide how often a hit lands.
        [TestCase("Punching", RankedRules.FistInterval)]
        [TestCase("Axing", RankedRules.ToolInterval)]
        [TestCase("Wrecking", RankedRules.ToolInterval)]
        public void SwingClips_HitAsOftenAsTheRulesSay(string clip, float interval)
        {
            foreach (string player in new[] { "1", "2" })
            {
                string yaml = File.ReadAllText($"Assets/Animations/Player_{player}/{clip}_{player}.anim");
                float length = Field(yaml, "m_StopTime");
                float[] hits = Regex.Matches(yaml, @"- time: ([0-9.]+)\s*\n\s*functionName: applyDamage")
                    .Cast<Match>().Select(m => float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).ToArray();
                Assert.Greater(hits.Length, 0, clip);
                Assert.AreEqual(interval, length / hits.Length, 1e-4f, $"{clip}_{player}: hits per loop");
                for (int i = 1; i < hits.Length; i++)
                    Assert.AreEqual(interval, hits[i] - hits[i - 1], 1e-4f, $"{clip}_{player}: evenly spaced");
            }
        }
    }
}
