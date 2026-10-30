using Demolition.Ranked;
using NUnit.Framework;

namespace Demolition.Tests
{
    public class PlayerNameTests
    {
        [TestCase("  Big  bad-wolf ", "BIG BAD-WOLF")]
        [TestCase("sloper_007!", "SLOPER007")]
        [TestCase("éé", "")]
        [TestCase("ABCDEFGHIJKLMNOPQRSTUVWXYZ", "ABCDEFGHIJKLMNOP")]
        [TestCase("ABCDEFGHIJKLMNO P", "ABCDEFGHIJKLMNO")]
        [TestCase(null, "")]
        public void Clean_KeepsWhatTheFontAndTheBoardsShow(string typed, string kept)
        {
            Assert.AreEqual(kept, PlayerName.Clean(typed));
        }

        [TestCase("ODB")]
        [TestCase("SCUNTHORPE")]
        [TestCase("SCRAPER")]
        [TestCase("CATERING CREW")]
        [TestCase("PIKACHU")]
        [TestCase("DICKENS")]
        [TestCase("ANALYST 9")]
        public void Allowed_LetsOrdinaryWordsThrough(string name)
        {
            Assert.IsTrue(PlayerName.Allowed(name), name);
        }

        [TestCase("AB")]
        [TestCase("lower")]
        [TestCase("TWO  SPACES")]
        [TestCase("FUCKFACE")]
        [TestCase("SH1T HEAD")]
        [TestCase("BIG DICK")]
        [TestCase("KANKERLIJER")]
        [TestCase("TERING")]
        [TestCase("H1TL3R")]
        [TestCase("N-A-Z-I")]
        public void Allowed_TurnsDownShortRawAndOffensiveNames(string name)
        {
            Assert.IsFalse(PlayerName.Allowed(name), name);
        }

        [Test]
        public void Default_IsAllowed_AndTheSameForTheSameId()
        {
            for (int i = 0; i < 500; i++)
            {
                string id = i.ToString("x32");
                string name = PlayerName.Default(id);
                Assert.IsTrue(PlayerName.Allowed(name), name);
                Assert.AreEqual(name, PlayerName.Default(id));
            }
            Assert.AreNotEqual(PlayerName.Default("a"), PlayerName.Default("b"));
        }
    }
}
