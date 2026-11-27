using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    /// <summary>
    /// Four-region E2E regression tests for RpgWorld500xCampMerchantHudPanel verifying
    /// that stale reputation-discount quotes and stale cross-region quotes reject charges
    /// and force quote refresh before allowing renewed purchase across Soundwell,
    /// RedSaltHarbor, WhitewaterCrossing, and MineCamp with zero native asset leaks.
    /// </summary>
    public sealed class RpgWorld500xFourRegionCampMerchantQuoteGuardE2ETests
    {
        private const string FlareButtonPath =
            "World500xPlayModeUiRoot/World500xCampMerchantHudPanel/MerchantModalBox/MerchantRows/SupplyRow_flare/ActionButton";

        [Test]
        public void AllFourRegions_CampMerchantPanel_RejectsStaleReputationAndRegionQuotesWithZeroLeak()
        {
            var root = new GameObject("FourRegionCampMerchantQuoteRoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                var warmupRoot = new GameObject("FourRegionCampMerchantWarmup");
                var warmupCtrl = warmupRoot.AddComponent<RpgWorld500xPlayModeController>();
                try
                {
                    warmupCtrl.Initialize(
                        overrideMap: RpgWorldMapId.Soundwell,
                        saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                        overrideTileSlices: new Sprite[] { null });
                    Assert.IsTrue(warmupCtrl.MenuRouter.OpenModal(RpgWorld500xModalKind.CampMerchant));
                    warmupCtrl.MenuRouter.CloseAll();
                }
                finally
                {
                    warmupCtrl.Shutdown();
                    UnityEngine.Object.DestroyImmediate(warmupRoot);
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
                Assert.AreEqual(4, specs.Count);

                for (int r = 0; r < specs.Count; r++)
                {
                    RpgExpandedMapSpec spec = specs[r];
                    RpgWorldMapId nextMap = specs[(r + 1) % specs.Count].MapId;
                    var regionRoot = new GameObject("RegionMerchant_" + spec.MapId);
                    regionRoot.transform.SetParent(root.transform, false);

                    var ctrl = regionRoot.AddComponent<RpgWorld500xPlayModeController>();
                    try
                    {
                        ctrl.Initialize(
                            overrideMap: spec.MapId,
                            saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                            overrideTileSlices: new Sprite[] { null });
                        var boot = ctrl.Bootstrapper;
                        var haven = ctrl.CampHaven;
                        boot.Driver.Facade.BountyAndSupply.AddSaltSilver(100);
                        Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.CampMerchant),
                            $"CampMerchantHudPanel failed to show on {spec.MapId}.");
                        var panel = ctrl.MenuRouter.MerchantPanel;
                        Assert.AreEqual(30, haven.GetDiscountedUnitCost(spec.MapId, "flare"));

                        // 1. Reputation changes while modal is open -> stale quote must reject charge.
                        Button.ButtonClickedEvent staleRepClick =
                            regionRoot.transform.Find(FlareButtonPath).GetComponent<Button>().onClick;
                        haven.Reputation.AddPoints(spec.MapId, 800);
                        staleRepClick.Invoke();

                        Assert.AreEqual(100, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance,
                            $"Stale reputation quote charged silver on {spec.MapId}.");
                        Assert.AreEqual(0, boot.Driver.Facade.BountyAndSupply.GetOwnedSupplyCount("flare"));
                        StringAssert.Contains("报价已更新", panel.FeedbackText);
                        StringAssert.Contains("折扣 30%", panel.HeaderText);

                        // Renewed purchase succeeds at discounted price (21 silver).
                        Assert.IsTrue(panel.TryClickBuySupply("flare"),
                            $"Renewed discounted purchase failed on {spec.MapId}.");
                        Assert.AreEqual(79, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                        Assert.AreEqual(1, boot.Driver.Facade.BountyAndSupply.GetOwnedSupplyCount("flare"));

                        // 2. Region switches while modal is open -> stale regional quote must reject charge.
                        Button.ButtonClickedEvent staleRegionClick =
                            regionRoot.transform.Find(FlareButtonPath).GetComponent<Button>().onClick;
                        Assert.IsTrue(boot.SwitchMap(nextMap));
                        Assert.AreEqual(30, haven.GetDiscountedUnitCost(nextMap, "flare"));
                        staleRegionClick.Invoke();

                        Assert.AreEqual(79, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance,
                            $"Stale regional quote charged silver after {spec.MapId}->{nextMap}.");
                        Assert.AreEqual(1, boot.Driver.Facade.BountyAndSupply.GetOwnedSupplyCount("flare"));
                        StringAssert.Contains("报价已更新", panel.FeedbackText);

                        // Renewed purchase on new region succeeds at full price (30 silver).
                        Assert.IsTrue(panel.TryClickBuySupply("flare"),
                            $"Renewed purchase failed on {nextMap}.");
                        Assert.AreEqual(49, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                        Assert.AreEqual(2, boot.Driver.Facade.BountyAndSupply.GetOwnedSupplyCount("flare"));

                        ctrl.MenuRouter.CloseAll();
                        Assert.IsFalse(panel.IsVisible);
                    }
                    finally
                    {
                        ctrl.Shutdown();
                        UnityEngine.Object.DestroyImmediate(regionRoot);
                    }
                }

                Assert.AreEqual(0, root.transform.childCount);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore,
                    "Texture2D leak detected after four-region CampMerchantHudPanel sweep.");
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore,
                    "Sprite leak detected after four-region CampMerchantHudPanel sweep.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
