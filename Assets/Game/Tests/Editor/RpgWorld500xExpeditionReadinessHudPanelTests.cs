using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xExpeditionReadinessHudPanelTests
    {
        [Test]
        public void ExpeditionReadinessHudPanel_AutoRestocksRecommendedSuppliesRespectsCanvasGroupLockAndZeroLeak()
        {
            var root = new GameObject("World500xExpeditionReadinessHudPanelTestRoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupPanel = new RpgWorld500xExpeditionReadinessHudPanel(root.transform, warmupBoot))
                {
                    warmupPanel.Show(0f);
                    warmupPanel.TryClickRefresh(1f);
                    warmupPanel.TryClickClose();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xExpeditionReadinessHudPanel(root.transform, boot))
                {
                    boot.Driver.Facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.GaleStorm);
                    boot.Driver.Facade.BountyAndSupply.AddSaltSilver(300);

                    // An invisible modal must not spend currency, even via a stale click.
                    Assert.IsFalse(panel.TryClickBuyRecommended());
                    Assert.IsFalse(panel.TryClickRefresh());
                    Assert.IsFalse(panel.TryClickClose());
                    var modal = root.transform.Find("World500xExpeditionReadinessHudPanel");
                    Assert.IsNotNull(modal.GetComponent<GraphicRaycaster>());
                    var buy = modal.Find("ReadinessModalBox/BuyRecommendedSuppliesButton").GetComponent<Button>();
                    buy.onClick.Invoke();
                    Assert.AreEqual(300, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);

                    Assert.IsTrue(panel.Show(5f));
                    Assert.AreEqual(4, panel.DisplayedRowCount);
                    Assert.AreEqual(RpgWorld500xReadinessGrade.Critical, panel.LastReport.Grade);
                    Assert.Greater(panel.LastReport.RecommendedSupplyIds.Count, 0);

                    // Parent CanvasGroup lock prevents restock, refresh, and close clicks cleanly.
                    CanvasGroup group = root.AddComponent<CanvasGroup>();
                    group.interactable = false;
                    Assert.IsFalse(panel.TryClickBuyRecommended());
                    Assert.IsFalse(panel.TryClickRefresh(6f));
                    Assert.IsFalse(panel.TryClickClose());
                    buy.onClick.Invoke();
                    Assert.AreEqual(300, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                    group.interactable = true;

                    // A purchase made by another UI after Show must not be duplicated.
                    var shop = boot.Driver.Facade.BountyAndSupply;
                    Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded, shop.TryPurchaseSupply("gauze", 1));
                    Assert.AreEqual(1, shop.GetOwnedSupplyCount("gauze"));

                    // One-click restock buys only the still-missing supplies and upgrades readiness.
                    Assert.IsTrue(panel.TryClickBuyRecommended());
                    Assert.AreEqual(1, shop.GetOwnedSupplyCount("gauze"));
                    Assert.Less(boot.Driver.Facade.BountyAndSupply.SaltSilverBalance, 300);
                    Assert.AreEqual(0, panel.LastReport.RecommendedSupplyIds.Count);
                    Assert.GreaterOrEqual(panel.LastReport.Score, 70);
                    Assert.IsTrue(panel.FeedbackText.Contains("已补购"));
                    Assert.IsFalse(panel.TryClickBuyRecommended());

                    Assert.IsTrue(panel.TryClickClose());
                    Assert.IsFalse(panel.IsVisible);
                    Assert.IsFalse(panel.TryClickRefresh());
                    Assert.IsFalse(panel.TryClickClose());
                }

                Assert.AreEqual(0, root.transform.childCount);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ExpeditionReadinessHudPanel_AutoProvisionsCrossRegionHazardousTripAndRespectsVisibilityAndLock()
        {
            var root = new GameObject("World500xReadinessProvisioningHudTestRoot");
            try
            {
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xExpeditionReadinessHudPanel(root.transform, boot))
                {
                    RpgWorld500xGrandRuntimeFacade facade = boot.Driver.Facade;
                    facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.Clear);
                    facade.Atmosphere.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.Clear);
                    facade.Atmosphere.SetWeather(RpgWorldMapId.MineCamp, RpgWorld500xWeatherKind.GaleStorm);
                    facade.BountyAndSupply.AddSaltSilver(300);

                    // Hidden panel must reject cross-region auto-provisioning without spending Salt-Silver.
                    Assert.IsFalse(panel.TryAutoProvisionForDestination(RpgWorldMapId.MineCamp, allowPartialIfUnderBudget: false));
                    Assert.AreEqual(300, facade.BountyAndSupply.SaltSilverBalance);

                    Assert.IsTrue(panel.Show(5f));
                    string breakdownBefore = panel.FormatProvisioningBreakdownForDestination(RpgWorldMapId.MineCamp);
                    Assert.IsTrue(breakdownBefore.Contains("合计"));
                    Assert.IsTrue(breakdownBefore.Contains("盐银"));

                    // Parent CanvasGroup lock must block cross-region auto-provisioning cleanly.
                    CanvasGroup group = root.AddComponent<CanvasGroup>();
                    group.interactable = false;
                    Assert.IsFalse(panel.TryAutoProvisionForDestination(RpgWorldMapId.MineCamp, allowPartialIfUnderBudget: false));
                    Assert.AreEqual(300, facade.BountyAndSupply.SaltSilverBalance);
                    group.interactable = true;

                    // Strict cap below full cost rejects without spending; partial cap spends within 35 Salt-Silver.
                    Assert.IsTrue(panel.TryAutoProvisionForDestination(
                        RpgWorldMapId.MineCamp, allowPartialIfUnderBudget: false, maxBudgetSaltSilver: 35));
                    Assert.IsFalse(panel.LastProvisioningReceipt.FullyProvisioned);
                    Assert.AreEqual(0, panel.LastProvisioningReceipt.TotalSpentSaltSilver);
                    Assert.AreEqual(300, facade.BountyAndSupply.SaltSilverBalance);
                    Assert.IsTrue(panel.FeedbackText.Contains("超出预算上限"));

                    Assert.IsTrue(panel.TryAutoProvisionForDestination(
                        RpgWorldMapId.MineCamp, allowPartialIfUnderBudget: true, maxBudgetSaltSilver: 35));
                    Assert.IsFalse(panel.LastProvisioningReceipt.FullyProvisioned);
                    Assert.Greater(panel.LastProvisioningReceipt.TotalSpentSaltSilver, 0);
                    Assert.LessOrEqual(panel.LastProvisioningReceipt.TotalSpentSaltSilver, 35);

                    // Full cross-region provisioning stocks remaining items (2 rations + lamp_oil + flare + gauze + iron_clamps).
                    Assert.IsTrue(panel.TryAutoProvisionForDestination(RpgWorldMapId.MineCamp, allowPartialIfUnderBudget: false));
                    Assert.IsTrue(panel.LastProvisioningReceipt.FullyProvisioned);
                    Assert.Greater(panel.LastProvisioningReceipt.TotalSpentSaltSilver, 0);
                    Assert.AreEqual(2, facade.BountyAndSupply.GetOwnedSupplyCount("rations"));
                    Assert.AreEqual(1, facade.BountyAndSupply.GetOwnedSupplyCount("lamp_oil"));
                    Assert.AreEqual(1, facade.BountyAndSupply.GetOwnedSupplyCount("flare"));
                    Assert.AreEqual(RpgWorld500xReadinessGrade.WellPrepared, panel.LastReport.Grade);
                    Assert.IsTrue(panel.FeedbackText.Contains("全额备齐"));
                    Assert.AreEqual(
                        "行囊已齐备（无需补购）",
                        panel.FormatProvisioningBreakdownForDestination(RpgWorldMapId.MineCamp));
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
