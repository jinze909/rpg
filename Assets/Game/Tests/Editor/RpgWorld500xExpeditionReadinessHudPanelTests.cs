using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xExpeditionReadinessHudPanelTests
    {
        [Test]
        public void ExpeditionReadinessHudPanel_AllFourRegions_RejectsReopenAfterOwnerDisposal()
        {
            foreach (var spec in RpgExpandedWorldScale.AllSpecs)
            {
                var root = new GameObject("ReadinessDisposedOwner_" + spec.MapId);
                try
                {
                    using (var boot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: spec.MapId, overrideTileSlices: new Sprite[] { null }))
                    using (var panel = new RpgWorld500xExpeditionReadinessHudPanel(root.transform, boot))
                    {
                        Assert.IsTrue(panel.Show(5f), spec.MapId.ToString());
                        var canvas = root.transform.Find("World500xExpeditionReadinessHudPanel");
                        var modal = canvas.Find("ReadinessModalBox");
                        var buy = modal.Find("BuyRecommendedSuppliesButton").GetComponent<Button>().onClick;
                        var refresh = modal.Find("RefreshReadinessButton").GetComponent<Button>().onClick;
                        var close = modal.Find("CloseReadinessButton").GetComponent<Button>().onClick;
                        Assert.IsTrue(panel.Hide());
                        var report = panel.LastReport;
                        var receipt = panel.LastProvisioningReceipt;
                        string header = panel.HeaderText;
                        string banner = panel.GradeBannerText;
                        string feedback = panel.FeedbackText;
                        int rows = panel.DisplayedRowCount;

                        boot.Dispose();
                        Assert.IsFalse(panel.Show(12f), spec.MapId.ToString());
                        Assert.IsFalse(panel.IsVisible);
                        Assert.IsFalse(canvas.gameObject.activeSelf);
                        buy.Invoke();
                        refresh.Invoke();
                        close.Invoke();
                        Assert.IsFalse(panel.TryClickBuyRecommended());
                        Assert.IsFalse(panel.TryPreviewTargetMap(RpgWorldMapId.MineCamp));
                        Assert.IsFalse(panel.TryAutoProvisionForDestination(RpgWorldMapId.MineCamp));
                        Assert.IsFalse(panel.TryClickRefresh(12f));
                        Assert.IsFalse(panel.TryClickClose());
                        Assert.AreEqual(string.Empty, panel.FormatProvisioningBreakdownForDestination(RpgWorldMapId.MineCamp));
                        Assert.AreEqual(report, panel.LastReport);
                        Assert.AreEqual(receipt, panel.LastProvisioningReceipt);
                        Assert.AreEqual(header, panel.HeaderText);
                        Assert.AreEqual(banner, panel.GradeBannerText);
                        Assert.AreEqual(feedback, panel.FeedbackText);
                        Assert.AreEqual(rows, panel.DisplayedRowCount);
                    }

                    Assert.AreEqual(0, root.transform.childCount);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        [Test]
        public void ExpeditionReadinessHudPanel_DisabledCanvasRejectsPurchasesAndPreviewUntilRestored()
        {
            var root = new GameObject("ReadinessDisabledCanvasRoot");
            try
            {
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xExpeditionReadinessHudPanel(root.transform, boot))
                {
                    var facade = boot.Driver.Facade;
                    var shop = facade.BountyAndSupply;
                    facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.Clear);
                    facade.Atmosphere.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.Clear);
                    facade.Atmosphere.SetWeather(RpgWorldMapId.MineCamp, RpgWorld500xWeatherKind.GaleStorm);
                    shop.AddSaltSilver(300);
                    Assert.IsTrue(panel.Show(5f));
                    Assert.Greater(panel.LastReport.RecommendedSupplyIds.Count, 0);
                    var canvas = root.transform.Find("World500xExpeditionReadinessHudPanel").GetComponent<Canvas>();
                    var modal = canvas.transform.Find("ReadinessModalBox");
                    var buy = modal.Find("BuyRecommendedSuppliesButton").GetComponent<Button>();
                    var refresh = modal.Find("RefreshReadinessButton").GetComponent<Button>();
                    var close = modal.Find("CloseReadinessButton").GetComponent<Button>();
                    int score = panel.LastReport.Score;
                    string header = panel.HeaderText;
                    string banner = panel.GradeBannerText;
                    string feedback = panel.FeedbackText;
                    string breakdown = panel.FormatProvisioningBreakdownForDestination(RpgWorldMapId.MineCamp);
                    var receipt = panel.LastProvisioningReceipt;

                    canvas.enabled = false;
                    Assert.IsTrue(buy.IsInteractable());
                    buy.onClick.Invoke();
                    refresh.onClick.Invoke();
                    close.onClick.Invoke();
                    Assert.IsFalse(panel.TryClickBuyRecommended());
                    Assert.IsFalse(panel.TryAutoProvisionForDestination(RpgWorldMapId.MineCamp, allowPartialIfUnderBudget: false));
                    Assert.IsFalse(panel.TryPreviewTargetMap(RpgWorldMapId.MineCamp));
                    Assert.IsFalse(panel.TryClickRefresh(10f));
                    Assert.IsFalse(panel.TryClickClose());
                    Assert.IsTrue(panel.IsVisible);
                    Assert.IsFalse(panel.IsPreviewingRemoteMap);
                    Assert.AreEqual(RpgWorldMapId.Soundwell, panel.EvaluatedMapId);
                    Assert.AreEqual(RpgWorldMapId.Soundwell, facade.CurrentMapId);
                    Assert.AreEqual(300, shop.SaltSilverBalance);
                    foreach (string id in panel.LastReport.RecommendedSupplyIds)
                        Assert.AreEqual(0, shop.GetOwnedSupplyCount(id));
                    Assert.AreEqual(receipt, panel.LastProvisioningReceipt);
                    Assert.AreEqual(score, panel.LastReport.Score);
                    Assert.AreEqual(4, panel.DisplayedRowCount);
                    Assert.AreEqual(header, panel.HeaderText);
                    Assert.AreEqual(banner, panel.GradeBannerText);
                    Assert.AreEqual(feedback, panel.FeedbackText);
                    Assert.AreEqual(breakdown, panel.FormatProvisioningBreakdownForDestination(RpgWorldMapId.MineCamp));

                    canvas.enabled = true;
                    Assert.IsTrue(panel.TryPreviewTargetMap(RpgWorldMapId.MineCamp));
                    Assert.IsTrue(panel.IsPreviewingRemoteMap);
                    Assert.AreEqual(RpgWorldMapId.MineCamp, panel.EvaluatedMapId);
                    Assert.IsTrue(panel.TryPreviewTargetMap(null));
                    Assert.IsTrue(panel.TryAutoProvisionForDestination(RpgWorldMapId.MineCamp, allowPartialIfUnderBudget: false));
                    Assert.IsTrue(panel.LastProvisioningReceipt.FullyProvisioned);
                    Assert.Greater(panel.LastProvisioningReceipt.TotalSpentSaltSilver, 0);
                    Assert.AreEqual(300 - panel.LastProvisioningReceipt.TotalSpentSaltSilver, shop.SaltSilverBalance);
                    Assert.AreEqual(2, shop.GetOwnedSupplyCount("rations"));
                    Assert.AreEqual(1, shop.GetOwnedSupplyCount("lamp_oil"));
                    Assert.AreEqual(1, shop.GetOwnedSupplyCount("flare"));
                    int balance = shop.SaltSilverBalance;
                    buy.onClick.Invoke();
                    Assert.AreEqual(balance, shop.SaltSilverBalance);
                    Assert.IsTrue(panel.TryClickRefresh(10f));
                    close.onClick.Invoke();
                    Assert.IsFalse(panel.IsVisible);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

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

                    // Hidden panel must reject cross-region auto-provisioning and remote preview cleanly.
                    Assert.IsFalse(panel.TryAutoProvisionForDestination(RpgWorldMapId.MineCamp, allowPartialIfUnderBudget: false));
                    Assert.IsFalse(panel.TryPreviewTargetMap(RpgWorldMapId.MineCamp));
                    Assert.AreEqual(300, facade.BountyAndSupply.SaltSilverBalance);

                    Assert.IsTrue(panel.Show(5f));
                    Assert.IsFalse(panel.IsPreviewingRemoteMap);
                    Assert.AreEqual(RpgWorldMapId.Soundwell, panel.EvaluatedMapId);

                    // Remote preview switches evaluation to stormy MineCamp without changing CurrentMapId.
                    Assert.IsTrue(panel.TryPreviewTargetMap(RpgWorldMapId.MineCamp));
                    Assert.IsTrue(panel.IsPreviewingRemoteMap);
                    Assert.AreEqual(RpgWorldMapId.MineCamp, panel.EvaluatedMapId);
                    Assert.AreEqual(RpgWorld500xReadinessGrade.Critical, panel.LastReport.Grade);
                    Assert.IsTrue(panel.HeaderText.Contains("【远征预检】"));
                    Assert.AreEqual(RpgWorldMapId.Soundwell, facade.CurrentMapId);

                    Assert.IsTrue(panel.TryPreviewTargetMap(null));
                    Assert.IsFalse(panel.IsPreviewingRemoteMap);
                    Assert.AreEqual(RpgWorldMapId.Soundwell, panel.EvaluatedMapId);

                    string breakdownBefore = panel.FormatProvisioningBreakdownForDestination(RpgWorldMapId.MineCamp);
                    Assert.IsTrue(breakdownBefore.Contains("合计"));
                    Assert.IsTrue(breakdownBefore.Contains("盐银"));

                    // Parent CanvasGroup lock must block cross-region auto-provisioning and remote preview cleanly.
                    CanvasGroup group = root.AddComponent<CanvasGroup>();
                    group.interactable = false;
                    Assert.IsFalse(panel.TryAutoProvisionForDestination(RpgWorldMapId.MineCamp, allowPartialIfUnderBudget: false));
                    Assert.IsFalse(panel.TryPreviewTargetMap(RpgWorldMapId.MineCamp));
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

                // Safest-route forwarding stocks direct clear WhitewaterCrossing -> MineCamp cleanly.
                using (var safeBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.WhitewaterCrossing,
                    overrideTileSlices: new Sprite[] { null }))
                using (var safePanel = new RpgWorld500xExpeditionReadinessHudPanel(root.transform, safeBoot))
                {
                    safeBoot.Driver.Facade.Atmosphere.SetWeather(RpgWorldMapId.WhitewaterCrossing, RpgWorld500xWeatherKind.Clear);
                    safeBoot.Driver.Facade.Atmosphere.SetWeather(RpgWorldMapId.MineCamp, RpgWorld500xWeatherKind.Clear);
                    safeBoot.Driver.Facade.Atmosphere.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.GaleStorm);
                    safeBoot.Driver.Facade.BountyAndSupply.AddSaltSilver(300);

                    Assert.IsTrue(safePanel.Show(5f));
                    Assert.IsTrue(safePanel.FormatProvisioningBreakdownForDestination(
                        RpgWorldMapId.MineCamp, preferSafestRoute: true).Contains("合计"));
                    Assert.IsTrue(safePanel.TryAutoProvisionForDestination(
                        RpgWorldMapId.MineCamp, allowPartialIfUnderBudget: false, preferSafestRoute: true));
                    Assert.IsTrue(safePanel.LastProvisioningReceipt.FullyProvisioned);
                    Assert.AreEqual(
                        "行囊已齐备（无需补购）",
                        safePanel.FormatProvisioningBreakdownForDestination(
                            RpgWorldMapId.MineCamp, preferSafestRoute: true));
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
