using Demolition.Meta;
using NUnit.Framework;

namespace Demolition.Tests
{
    public class QualityPolicyTests
    {
        [Test]
        public void Auto_PcsAreHigh_WhateverTheyReport()
        {
            Assert.AreEqual(QualityTier.High, QualityPolicy.AutoTier(false, false, 2048, 2));
        }

        [Test]
        public void Auto_BudgetPhones_AreLow()
        {
            Assert.AreEqual(QualityTier.Low, QualityPolicy.AutoTier(true, false, 3700, 8), "a 4 GB phone");
            Assert.AreEqual(QualityTier.Low, QualityPolicy.AutoTier(true, false, 8000, 4), "four cores");
            Assert.AreEqual(QualityTier.High, QualityPolicy.AutoTier(true, false, 7600, 8), "an 8 GB phone");
        }

        [Test]
        public void Auto_PhoneBrowsersAreLow_UnknownSpecsCountAsEnough()
        {
            Assert.AreEqual(QualityTier.Low, QualityPolicy.AutoTier(true, true, 8000, 8));
            Assert.AreEqual(QualityTier.High, QualityPolicy.AutoTier(true, false, 0, 0));
        }

        [Test]
        public void Choice_OverridesAuto()
        {
            Assert.AreEqual(QualityTier.Low, QualityPolicy.Resolve(QualityChoice.Auto, QualityTier.Low));
            Assert.AreEqual(QualityTier.High, QualityPolicy.Resolve(QualityChoice.High, QualityTier.Low));
            Assert.AreEqual(QualityTier.Low, QualityPolicy.Resolve(QualityChoice.Low, QualityTier.High));
        }

        [Test]
        public void RenderScale_CapsTheShortSide_PerTier()
        {
            Assert.AreEqual(1f, QualityPolicy.RenderScale(QualityTier.High, false, 2160), "PCs render at full resolution");
            Assert.AreEqual(1080f / 1344f, QualityPolicy.RenderScale(QualityTier.High, true, 1344), 1e-5f);
            Assert.AreEqual(1f, QualityPolicy.RenderScale(QualityTier.High, true, 1080), "never above 1");
            Assert.AreEqual(720f / 1080f, QualityPolicy.RenderScale(QualityTier.Low, true, 1080), 1e-5f);
            Assert.AreEqual(1f, QualityPolicy.RenderScale(QualityTier.Low, true, 720));
            Assert.AreEqual(0.5f, QualityPolicy.RenderScale(QualityTier.Low, false, 1440), 1e-5f, "Low applies on PC too");
        }

        [Test]
        public void FallingLimit_OnlyLowCaps()
        {
            Assert.AreEqual(QualityPolicy.LowFallingLimit, QualityPolicy.FallingLimit(QualityTier.Low));
            Assert.AreEqual(int.MaxValue, QualityPolicy.FallingLimit(QualityTier.High));
        }
    }
}
