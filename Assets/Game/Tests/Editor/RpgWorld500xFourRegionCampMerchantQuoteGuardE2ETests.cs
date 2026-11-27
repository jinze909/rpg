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
    /// RedSaltHarbor, WhitewaterCrossing, and MineCamp with clean UI disposal.
    /// </summary>
    public sealed class RpgWorld500xFourRegionCampMerchantQuoteGuardE2ETests
    {
        private const string FlareButtonPath =
            "World500xCampMerchantHudPanel/MerchantModalBox/MerchantRows/SupplyRow_flare/ActionButton";
        private const string FlareOwnedKey = "rpg.return.world.supply.item.flare";

        [Test]
        public void AllFourRegions_CampMerchantPanel_RejectsStaleReputationAndRegionQuotes()
        {
            var root = new GameObject("FourRegionCampMerchantQuoteRoot");
            try
            {
                IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
                Assert.AreEqual(4, specs.Count);

                for (int r = 0; r < specs.Count; r++)
                {
                    RpgExpandedMapSpec spec = specs[r];
                    RpgWorldMapId nextMap = specs[(r + 1) % specs.Count].MapId;
                    var regionRoot = new GameObject("RegionMerchant_" + spec.MapId);
                    regionRoot.transform.SetParent(root.transform, false);

                    try
                    {
                        using (var boot = new RpgWorld500xSceneBootstrapper(
                            initialMapId: spec.MapId,
                            overrideTileSlices: new Sprite[] { null }))
                        {
                            RpgWorld500xCampHavenService haven = boot.CreateCampHavenService();
                            boot.Driver.Facade.BountyAndSupply.AddSaltSilver(100);

                            using (var panel = new RpgWorld500xCampMerchantHudPanel(regionRoot.transform, boot, haven))
                            {
                                Assert.IsTrue(panel.Show(RpgCampMerchantTab.Supplies),
                                    $"CampMerchantHudPanel failed to show on {spec.MapId}.");
                                int baseCost = haven.GetDiscountedUnitCost(spec.MapId, "flare");
                                Assert.Greater(baseCost, 0);

                                // 1. Reputation changes while modal is open -> stale quote must reject charge.
                                Button.ButtonClickedEvent staleRepClick =
                                    regionRoot.transform.Find(FlareButtonPath).GetComponent<Button>().onClick;
                                haven.Reputation.AddPoints(spec.MapId, 800);
                                int discountedCost = haven.GetDiscountedUnitCost(spec.MapId, "flare");
                                Assert.Less(discountedCost, baseCost,
                                    $"Expected reputation discount on {spec.MapId}.");

                                staleRepClick.Invoke();
                                Assert.AreEqual(100, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance,
                                    $"Stale reputation quote charged silver on {spec.MapId}.");
                                Assert.IsFalse(boot.Store.ContainsKey(FlareOwnedKey));
                                StringAssert.Contains("报价已更新", panel.FeedbackText);
                                StringAssert.Contains("折扣 30%", panel.HeaderText);

                                // Renewed purchase succeeds at discounted price.
                                Assert.IsTrue(panel.TryClickBuySupply("flare"),
                                    $"Renewed discounted purchase failed on {spec.MapId}.");
                                int balanceAfterFirst = 100 - discountedCost;
                                Assert.AreEqual(balanceAfterFirst, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                                Assert.AreEqual(1, boot.Store[FlareOwnedKey]);

                                // 2. Region switches while modal is open -> stale regional quote must reject charge.
                                Button.ButtonClickedEvent staleRegionClick =
                                    regionRoot.transform.Find(FlareButtonPath).GetComponent<Button>().onClick;
                                Assert.IsTrue(boot.SwitchMap(nextMap));
                                int nextRegionCost = haven.GetDiscountedUnitCost(nextMap, "flare");
                                Assert.AreEqual(baseCost, nextRegionCost);

                                staleRegionClick.Invoke();
                                Assert.AreEqual(balanceAfterFirst, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance,
                                    $"Stale regional quote charged silver after {spec.MapId}->{nextMap}.");
                                Assert.AreEqual(1, boot.Store[FlareOwnedKey]);
                                StringAssert.Contains("报价已更新", panel.FeedbackText);

                                // Renewed purchase on new region succeeds at full price.
                                Assert.IsTrue(panel.TryClickBuySupply("flare"),
                                    $"Renewed purchase failed on {nextMap}.");
                                Assert.AreEqual(balanceAfterFirst - nextRegionCost,
                                    boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                                Assert.AreEqual(2, boot.Store[FlareOwnedKey]);

                                Assert.IsTrue(panel.Hide());
                                Assert.IsFalse(panel.IsVisible);
                            }
                        }
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(regionRoot);
                    }
                }

                Assert.AreEqual(0, root.transform.childCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
