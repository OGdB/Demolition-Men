using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Demolition.Tests
{
    /// <summary>
    /// The ranked server builds a day's level from Content/&lt;Theme&gt;/Levels and its theme's
    /// Archetypes folder (server/ranked/Ranked/Levels.cs); the game from GameConfig's themes and what
    /// each theme asset lists. These must be the same files, or the two build different levels.
    /// </summary>
    public class RankedContentTests
    {
        private static string Guid(string path) => Regex.Match(File.ReadAllText(path + ".meta"), @"guid: ([0-9a-f]{32})").Groups[1].Value;

        private static HashSet<string> Listed(string yaml, string field)
        {
            Match block = Regex.Match(yaml, $@"\n  {field}:\s*\n((?:  - \{{[^\n]*\n)*)");
            return new HashSet<string>(Regex.Matches(block.Groups[1].Value, @"guid: ([0-9a-f]{32})").Cast<Match>().Select(m => m.Groups[1].Value));
        }

        [Test]
        public void EveryThemeFolder_IsWhatTheGameLists()
        {
            string config = File.ReadAllText("Assets/Resources/GameConfig.asset");
            int themes = 0;
            foreach (string theme in Directory.GetDirectories("Assets/Content"))
            {
                string levels = Path.Combine(theme, "Levels"), archetypes = Path.Combine(theme, "Archetypes");
                if (!Directory.Exists(levels))
                    continue;
                string name = Path.GetFileName(theme);
                string asset = Path.Combine(theme, name + "Theme.asset");
                Assert.IsTrue(File.Exists(asset), asset);
                StringAssert.Contains(Guid(asset), config, $"GameConfig lists the {name} theme");
                string yaml = File.ReadAllText(asset);
                CollectionAssert.AreEquivalent(Directory.GetFiles(levels, "*.json").Select(Guid), Listed(yaml, "levels"), $"{name}: levels");
                CollectionAssert.AreEquivalent(Directory.GetFiles(archetypes, "*.json").Select(Guid), Listed(yaml, "archetypes"), $"{name}: archetypes");
                themes++;
            }
            Assert.Greater(themes, 0);
        }
    }
}
