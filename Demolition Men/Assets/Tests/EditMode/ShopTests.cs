using System.Collections.Generic;
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
    }
}
