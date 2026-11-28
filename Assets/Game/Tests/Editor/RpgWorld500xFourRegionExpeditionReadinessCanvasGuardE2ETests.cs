using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionExpeditionReadinessCanvasGuardE2ETests
    {
        [Test]
        public void AllFourRegions_ExpeditionReadinessRejectsDisabledCanvasLockedAndHiddenInputUntilRestored()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            foreach (RpgExpandedMapSpec spec in specs)
            {
                RpgWorldMapId targetMap = spec.MapId == RpgWorldMapId.MineCamp
                    ? RpgWorldMapId.Soundwell
                    : RpgWorldMapId.MineCamp;

                var root = new GameObject("ReadinessCanvasGuard_" + spec.MapId);
                try
                {
                    using (var boot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: spec.MapId,
                        overrideTileSlices: new Sprite[] { null }))
                    using (var panel = new RpgWorld500xExpeditionReadinessHudPanel(root.transform, boot))
                    {
                        var facade = boot.Driver.Facade;
                        var shop = facade.BountyAndSupply;
                        facade.Atmosphere.SetWeather(spec.MapId, RpgWorld500xWeatherKind.Clear);
                        facade.Atmosphere.SetWeather(targetMap, RpgWorld500xWeatherKind.GaleStorm);
                        shop.AddSaltSilver(300);

                        Assert.IsTrue(panel.Show(5f), spec.MapId.ToString());
                        Assert.Greater(panel.LastReport.RecommendedSupplyIds.Count, 0, spec.MapId.ToString());

                        var canvas = root.transform.Find("World500xExpeditionReadinessHudPanel").GetComponent<Canvas>();
                        Assert.IsNotNull(canvas, spec.MapId.ToString());
                        var modal = canvas.transform.Find("ReadinessModalBox");
                        Assert.IsNotNull(modal, spec.MapId.ToString());

                        var buyBtn = modal.Find("BuyRecommendedSuppliesButton").GetComponent<Button>();
                        var refreshBtn = modal.Find("RefreshReadinessButton").GetComponent<Button>();
                        var closeBtn = modal.Find("CloseReadinessButton").GetComponent<Button>();
                        Assert.IsNotNull(buyBtn, spec.MapId.ToString());
                        Assert.IsNotNull(refreshBtn, spec.MapId.ToString());
                        Assert.IsNotNull(closeBtn, spec.MapId.ToString());

                        int score = panel.LastReport.Score;
                        int rows = panel.DisplayedRowCount;
                        string header = panel.HeaderText;
                        string banner = panel.GradeBannerText;
                        string feedback = panel.FeedbackText;
                        var receipt = panel.LastProvisioningReceipt;

                        // 1. Disabled owning Canvas rejects raw callbacks and public APIs.
                        canvas.enabled = false;
                        Assert.IsTrue(buyBtn.IsInteractable(), spec.MapId.ToString());
                        AssertBlocked(panel, facade, shop, spec.MapId, targetMap, buyBtn, refreshBtn, closeBtn,
                            true, score, rows, header, banner, feedback, receipt);
                        canvas.enabled = true;

                        // 2. Ancestor CanvasGroup lock rejects raw callbacks and public APIs.
                        var group = root.AddComponent<CanvasGroup>();
                        group.interactable = false;
                        AssertBlocked(panel, facade, shop, spec.MapId, targetMap, buyBtn, refreshBtn, closeBtn,
                            true, score, rows, header, banner, feedback, receipt);
                        group.interactable = true;

                        // 3. Hidden panel rejects raw callbacks and public APIs.
                        Assert.IsTrue(panel.Hide(), spec.MapId.ToString());
                        AssertBlocked(panel, facade, shop, spec.MapId, targetMap, buyBtn, refreshBtn, closeBtn,
                            false, score, rows, header, banner, feedback, receipt);
                        Assert.IsTrue(panel.Show(5f), spec.MapId.ToString());

                        // 4. Restored panel supports remote preview, auto-provisioning, refresh, and close.
                        Assert.IsTrue(panel.TryPreviewTargetMap(targetMap), spec.MapId.ToString());
                        Assert.IsTrue(panel.IsPreviewingRemoteMap, spec.MapId.ToString());
                        Assert.AreEqual(targetMap, panel.EvaluatedMapId, spec.MapId.ToString());
                        Assert.IsTrue(panel.TryPreviewTargetMap(null), spec.MapId.ToString());

                        Assert.IsTrue(
                            panel.TryAutoProvisionForDestination(targetMap, allowPartialIfUnderBudget: false),
                            spec.MapId.ToString());
                        Assert.IsTrue(panel.LastProvisioningReceipt.FullyProvisioned, spec.MapId.ToString());
                        Assert.Greater(panel.LastProvisioningReceipt.TotalSpentSaltSilver, 0, spec.MapId.ToString());
                        Assert.AreEqual(
                            300 - panel.LastProvisioningReceipt.TotalSpentSaltSilver,
                            shop.SaltSilverBalance,
                            spec.MapId.ToString());

                        Assert.IsTrue(panel.TryClickRefresh(10f), spec.MapId.ToString());
                        closeBtn.onClick.Invoke();
                        Assert.IsFalse(panel.IsVisible, spec.MapId.ToString());
                    }

                    Assert.AreEqual(0, root.transform.childCount, spec.MapId.ToString());
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        private static void AssertBlocked(
            RpgWorld500xExpeditionReadinessHudPanel panel,
            RpgWorld500xGrandRuntimeFacade facade,
            RpgWorld500xFieldBountyAndSupplyService shop,
            RpgWorldMapId activeMap,
            RpgWorldMapId targetMap,
            Button buyBtn,
            Button refreshBtn,
            Button closeBtn,
            bool expectedVisible,
            int expectedScore,
            int expectedRows,
            string expectedHeader,
            string expectedBanner,
            string expectedFeedback,
            RpgWorld500xCampProvisioningReceipt expectedReceipt)
        {
            buyBtn.onClick.Invoke();
            refreshBtn.onClick.Invoke();
            closeBtn.onClick.Invoke();
            Assert.IsFalse(panel.TryClickBuyRecommended());
            Assert.IsFalse(panel.TryAutoProvisionForDestination(targetMap, allowPartialIfUnderBudget: false));
            Assert.IsFalse(panel.TryPreviewTargetMap(targetMap));
            Assert.IsFalse(panel.TryClickRefresh(10f));
            Assert.IsFalse(panel.TryClickClose());

            Assert.AreEqual(expectedVisible, panel.IsVisible);
            Assert.IsFalse(panel.IsPreviewingRemoteMap);
            Assert.AreEqual(activeMap, panel.EvaluatedMapId);
            Assert.AreEqual(activeMap, facade.CurrentMapId);
            Assert.AreEqual(300, shop.SaltSilverBalance);
            foreach (string id in panel.LastReport.RecommendedSupplyIds)
                Assert.AreEqual(0, shop.GetOwnedSupplyCount(id));
            Assert.AreEqual(expectedReceipt, panel.LastProvisioningReceipt);
            Assert.AreEqual(expectedScore, panel.LastReport.Score);
            Assert.AreEqual(expectedRows, panel.DisplayedRowCount);
            Assert.AreEqual(expectedHeader, panel.HeaderText);
            Assert.AreEqual(expectedBanner, panel.GradeBannerText);
            Assert.AreEqual(expectedFeedback, panel.FeedbackText);
        }
    }
}
