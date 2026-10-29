using System;
using System.Collections.Generic;
using System.IO;
using Demolition.Actions;
using Demolition.Meta;
using Demolition.Run;
using NUnit.Framework;

namespace Demolition.Tests
{
    public class ShopTests
    {
        [Test]
        public void Actions_CostCash_ButTheBurp()
        {
            var save = new SaveData();
            Assert.IsTrue(ActionCatalog.Owns(save, ActionCatalog.Default));
            foreach (ActionInfo info in ActionCatalog.All)
                if (info.Kind != ActionCatalog.Default)
                    Assert.Greater(info.Price, 0, info.Id);

            ActionInfo slam = ActionCatalog.Get(ActionKind.ButtSlam);
            save.cash = slam.Price - 1;
            Assert.IsFalse(ActionCatalog.TryBuy(save, ActionKind.ButtSlam), "too expensive");
            ActionCatalog.Equip(save, 0, ActionKind.ButtSlam);
            Assert.AreEqual(ActionCatalog.Default, ActionCatalog.Equipped(save, 0), "not owned: the burp");

            save.cash = slam.Price;
            Assert.IsTrue(ActionCatalog.TryBuy(save, ActionKind.ButtSlam));
            Assert.AreEqual(0, save.cash);
            Assert.AreEqual(ActionKind.ButtSlam, ActionCatalog.Equipped(save, 0));
            save.cash = slam.Price;
            Assert.IsFalse(ActionCatalog.TryBuy(save, ActionKind.ButtSlam), "owned already");
            Assert.AreEqual(slam.Price, save.cash);

            save.ownedActions = null; // a profile saved before the shop sold actions
            Assert.AreEqual(ActionCatalog.Default, ActionCatalog.Equipped(save, 0));
            Assert.IsTrue(ActionCatalog.TryBuy(save, ActionKind.ButtSlam));
        }

        [Test]
        public void Looks_AreBoughtEarnedOrSupported_AndOnlyWornOnceOwned()
        {
            var vest = new LookInfo { Id = "orange", Source = LookSource.Shop, Price = 100 };
            var gold = new LookInfo { Id = "gold", Source = LookSource.JobSheets, Sheets = 1 };
            var supporter = new LookInfo { Id = "supporter", Source = LookSource.Supporter };
            var catalog = new List<LookInfo> { vest, gold, supporter };
            var save = new SaveData { cash = 150 };

            Assert.IsFalse(LookCatalog.Wear(save, 0, vest), "not owned");
            Assert.IsTrue(LookCatalog.TryBuy(save, vest));
            Assert.AreEqual(50, save.cash);
            Assert.IsFalse(LookCatalog.TryBuy(save, vest), "owned already");
            Assert.IsFalse(LookCatalog.TryBuy(save, gold), "earned, not sold");
            Assert.IsTrue(LookCatalog.Wear(save, 1, vest));
            Assert.AreSame(vest, LookCatalog.Worn(save, 1, catalog));
            Assert.IsNull(LookCatalog.Worn(save, 0, catalog), "P1 wears the crew's own");

            Assert.IsFalse(LookCatalog.Owns(save, gold));
            save.RecordRun("town-01", true, Grade.B, 0, 60f, objectivesMet: Objectives.AllDone);
            Assert.IsTrue(LookCatalog.Owns(save, gold));

            Assert.IsFalse(LookCatalog.Owns(save, supporter));
            save.adsRemoved = true;
            Assert.IsTrue(LookCatalog.Wear(save, 0, supporter));
            save.adsRemoved = false; // a profile copied without the purchase
            Assert.IsNull(LookCatalog.Worn(save, 0, catalog));

            Assert.IsTrue(LookCatalog.Wear(save, 1, null));
            Assert.IsNull(LookCatalog.Worn(save, 1, catalog));
        }

        [Test]
        public void LookCatalog_LoadsEveryLook_EachHadOneWay_AndRecolouringSomething()
        {
            LookCatalog.Load(File.ReadAllText("Assets/Resources/Looks.json"));
            Assert.GreaterOrEqual(LookCatalog.All.Count, 1);
            var ids = new HashSet<string>();
            int supporters = 0;
            foreach (LookInfo look in LookCatalog.All)
            {
                Assert.IsTrue(ids.Add(look.Id), "one id each: " + look.Id);
                Assert.IsFalse(string.IsNullOrEmpty(look.Name), look.Id);
                if (look.Source == LookSource.Shop)
                    Assert.Greater(look.Price, 0, look.Id);
                if (look.Source == LookSource.JobSheets)
                    Assert.Greater(look.Sheets, 0, look.Id);
                if (look.Source == LookSource.Supporter)
                    supporters++;
                Assert.Greater(LookPalette.Map(look, false).Count, 0, look.Id + " changes nothing on P1");
                Assert.Greater(LookPalette.Map(look, true).Count, 0, look.Id + " changes nothing on P2");
                Assert.AreEqual(look, LookCatalog.All[LookCatalog.IndexOf(look)]);
            }
            Assert.AreEqual(1, supporters, "REMOVE ADS comes with one look");
            Assert.AreEqual(-1, LookCatalog.IndexOf(null), "the crew's own is sent as none");

            Assert.Throws<FormatException>(() => LookCatalog.Load("{\"looks\":[{\"id\":\"x\",\"source\":\"Gift\"}]}"));
        }

        [Test]
        public void LookPalette_RecoloursClothesAndSign_NeverSkinOrWeapons()
        {
            var look = new LookInfo { Id = "test", Trousers = 0x3A5C9E, Sign = 0x808080 };
            Dictionary<int, int> p1 = LookPalette.Map(look, false);
            Assert.AreEqual(0x3A5C9E, p1[0x222034], "P1's trousers");
            Assert.AreEqual(0x808080, p1[0xFFE08B], "P1's sign");
            Assert.AreEqual(0xACACAC, p1[0xFFECA4], "the sign's highlight a little lighter");
            Assert.IsFalse(p1.ContainsKey(0x663931), "shoes kept");
            Assert.AreEqual(0x3A5C9E, LookPalette.Map(look, true)[0xE1BB7A], "P2's trousers");
            Assert.AreEqual(0, LookPalette.Map(null, false).Count, "the crew's own look");

            int[] untouched =
            {
                0xEEC39A, 0xD9A066, 0xCDA681, 0xB36442, 0x7D450C, 0x985527, // skin, both players
                0x696A6A, 0x869F9F, 0x505757, 0xAC3232, 0xDF7D7D, 0xC4721F, 0xDFDDDB, 0xF1F1F1, 0xE0F6F6, // weapons
            };
            var all = new LookInfo { Trousers = 1, Shoes = 1, Sign = 1, SignShade = 1 };
            foreach (bool player2 in new[] { false, true })
                foreach (int colour in untouched)
                    Assert.IsFalse(LookPalette.Map(all, player2).ContainsKey(colour), colour.ToString("X6"));
        }

        [Test]
        public void LookExtras_ColourOnlyThePartsWorn_MetalShaded()
        {
            var bling = new LookInfo { Chain = 0xF2C230, ArmTattoo = 0xC8283C };
            Dictionary<int, int> extras = LookPalette.Extras(bling);
            Assert.AreEqual(0xF2C230, extras[LookPalette.ChainKey]);
            Assert.AreEqual(0xA98822, extras[LookPalette.ChainShadeKey], "the links' shade: 30% darker");
            Assert.AreEqual(0xC8283C, extras[LookPalette.ArmTattooKey]);
            Assert.IsFalse(extras.ContainsKey(LookPalette.BraceletKey), "no bracelets");
            Assert.IsFalse(extras.ContainsKey(LookPalette.ChestTattooKey), "no chest piece");
            Assert.IsFalse(extras.ContainsKey(LookPalette.KeepKey), "what's kept is never coloured");
            Assert.AreEqual(0, LookPalette.Extras(new LookInfo()).Count);
        }

        // Width and height from a PNG's header.
        private static (int, int) PngSize(string path)
        {
            byte[] b = File.ReadAllBytes(path);
            return (b[16] << 24 | b[17] << 16 | b[18] << 8 | b[19], b[20] << 24 | b[21] << 16 | b[22] << 8 | b[23]);
        }

        [Test]
        public void LookOverlays_EveryFrameHasOne_OfItsSize_Readable()
        {
            var frames = new List<(string frame, string overlay)>
            {
                ("Assets/Animations/Player_1/+Demolition_man.png", "Assets/Resources/LookOverlays/P1/+Demolition_man.png"),
                ("Assets/Animations/Player_2/+Demolition_man_Stop_V2.png", "Assets/Resources/LookOverlays/P2/+Demolition_man_Stop_V2.png"),
            };
            foreach (string player in new[] { "P1", "P2" })
                foreach (string path in Directory.GetFiles("Assets/Resources/Actions/" + player, "*.png"))
                    frames.Add((path, $"Assets/Resources/LookOverlays/{player}/{Path.GetFileName(path)}"));
            Assert.Greater(frames.Count, 40);
            foreach ((string frame, string overlay) in frames)
            {
                Assert.IsTrue(File.Exists(overlay), "no overlay: run tools/artpipe/accessories.py for " + frame);
                Assert.AreEqual(PngSize(frame), PngSize(overlay), overlay);
                StringAssert.Contains("isReadable: 1", File.ReadAllText(overlay + ".meta"), overlay);
                StringAssert.Contains("isReadable: 1", File.ReadAllText(frame + ".meta"), frame);
            }
        }
    }
}
