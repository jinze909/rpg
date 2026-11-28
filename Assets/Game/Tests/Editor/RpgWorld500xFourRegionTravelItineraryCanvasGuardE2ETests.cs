using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionTravelItineraryCanvasGuardE2ETests
    {
        [Test]
        public void AllFourRegions_TravelItineraryRejectsDisabledCanvasLockedAndHiddenInputUntilRestored()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            foreach (RpgExpandedMapSpec spec in specs)
            {
                PickDestinations(spec.MapId, out RpgWorldMapId targetMap, out RpgWorldMapId altMap);

                var root = new GameObject("TravelItineraryCanvasGuard_" + spec.MapId);
                try
                {
                    using (var boot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: spec.MapId,
                        overrideTileSlices: new Sprite[] { null }))
                    using (var panel = new RpgWorld500xTravelItineraryHudPanel(root.transform, boot))
                    {
                        var facade = boot.Driver.Facade;
                        facade.Atmosphere.SetWeather(spec.MapId, RpgWorld500xWeatherKind.Clear);
                        facade.Atmosphere.SetWeather(altMap, RpgWorld500xWeatherKind.Clear);
                        facade.Atmosphere.SetWeather(targetMap, RpgWorld500xWeatherKind.GaleStorm);
                        facade.BountyAndSupply.AddSaltSilver(300);

                        Assert.IsTrue(panel.Show(targetMap), spec.MapId.ToString());
                        var canvas = root.transform.Find("World500xTravelItineraryHudPanel").GetComponent<Canvas>();
                        Assert.IsNotNull(canvas, spec.MapId.ToString());
                        var modal = canvas.transform.Find("ItineraryModalBox");
                        Assert.IsNotNull(modal, spec.MapId.ToString());

                        var provisionBtn = modal.Find("AutoProvisionItineraryButton").GetComponent<Button>();
                        var departBtn = modal.Find("DepartFirstLegButton").GetComponent<Button>();
                        var altDestBtn = modal.Find("Dest_" + altMap).GetComponent<Button>();
                        var closeBtn = modal.Find("CloseItineraryButton").GetComponent<Button>();
                        Assert.IsNotNull(provisionBtn, spec.MapId.ToString());
                        Assert.IsNotNull(departBtn, spec.MapId.ToString());
                        Assert.IsNotNull(altDestBtn, spec.MapId.ToString());
                        Assert.IsNotNull(closeBtn, spec.MapId.ToString());

                        string feedback = panel.FeedbackText;
                        string summary = panel.SummaryBannerText;
                        int legRows = panel.DisplayedLegRowCount;
                        int lines = panel.LastProvisioningPlan.Lines.Count;
                        int cost = panel.LastProvisioningPlan.TotalRequiredSaltSilver;
                        Assert.Greater(legRows, 0, spec.MapId.ToString());
                        Assert.Greater(lines, 0, spec.MapId.ToString());
                        Assert.Greater(cost, 0, spec.MapId.ToString());
                        Assert.IsTrue(provisionBtn.IsInteractable(), spec.MapId.ToString());
                        Assert.IsTrue(departBtn.IsInteractable(), spec.MapId.ToString());

                        // 1. Disabled owning Canvas rejects raw callbacks and public APIs.
                        canvas.enabled = false;
                        Assert.IsTrue(provisionBtn.IsInteractable(), spec.MapId.ToString());
                        AssertBlocked(panel, facade, spec.MapId, targetMap, altMap,
                            provisionBtn, departBtn, altDestBtn, closeBtn,
                            true, legRows, lines, cost, feedback, summary);
                        canvas.enabled = true;

                        // 2. Ancestor CanvasGroup lock rejects raw callbacks and public APIs.
                        var group = root.AddComponent<CanvasGroup>();
                        group.interactable = false;
                        AssertBlocked(panel, facade, spec.MapId, targetMap, altMap,
                            provisionBtn, departBtn, altDestBtn, closeBtn,
                            true, legRows, lines, cost, feedback, summary);
                        group.interactable = true;

                        // 3. Hidden panel rejects raw callbacks and public APIs.
                        Assert.IsTrue(panel.Hide(), spec.MapId.ToString());
                        AssertBlocked(panel, facade, spec.MapId, targetMap, altMap,
                            provisionBtn, departBtn, altDestBtn, closeBtn,
                            false, legRows, lines, cost, feedback, summary);
                        Assert.IsTrue(panel.Show(targetMap), spec.MapId.ToString());

                        // 4. Restored panel supports auto-provisioning, first-leg departure, and close.
                        Assert.IsTrue(panel.TryClickAutoProvision(), spec.MapId.ToString());
                        Assert.IsTrue(panel.LastProvisioningReceipt.FullyProvisioned, spec.MapId.ToString());
                        Assert.AreEqual(
                            300 - panel.LastProvisioningReceipt.TotalSpentSaltSilver,
                            facade.BountyAndSupply.SaltSilverBalance,
                            spec.MapId.ToString());
                        Assert.AreEqual(0, panel.LastProvisioningPlan.Lines.Count, spec.MapId.ToString());
                        Assert.IsFalse(panel.TryClickAutoProvision(), spec.MapId.ToString());

                        departBtn.onClick.Invoke();
                        Assert.AreNotEqual(spec.MapId, facade.CurrentMapId, spec.MapId.ToString());
                        Assert.AreEqual(legRows - 1, panel.DisplayedLegRowCount, spec.MapId.ToString());
                        Assert.IsTrue(panel.TryClickClose(), spec.MapId.ToString());
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

        private static void PickDestinations(
            RpgWorldMapId origin,
            out RpgWorldMapId targetMap,
            out RpgWorldMapId altMap)
        {
            switch (origin)
            {
                case RpgWorldMapId.Soundwell:
                    targetMap = RpgWorldMapId.MineCamp;
                    altMap = RpgWorldMapId.RedSaltHarbor;
                    return;
                case RpgWorldMapId.RedSaltHarbor:
                    targetMap = RpgWorldMapId.WhitewaterCrossing;
                    altMap = RpgWorldMapId.Soundwell;
                    return;
                case RpgWorldMapId.WhitewaterCrossing:
                    targetMap = RpgWorldMapId.MineCamp;
                    altMap = RpgWorldMapId.RedSaltHarbor;
                    return;
                default:
                    targetMap = RpgWorldMapId.Soundwell;
                    altMap = RpgWorldMapId.WhitewaterCrossing;
                    return;
            }
        }

        private static void AssertBlocked(
            RpgWorld500xTravelItineraryHudPanel panel,
            RpgWorld500xGrandRuntimeFacade facade,
            RpgWorldMapId origin,
            RpgWorldMapId targetMap,
            RpgWorldMapId altMap,
            Button provisionBtn,
            Button departBtn,
            Button altDestBtn,
            Button closeBtn,
            bool expectedVisible,
            int expectedLegRows,
            int expectedLines,
            int expectedCost,
            string expectedFeedback,
            string expectedSummary)
        {
            provisionBtn.onClick.Invoke();
            departBtn.onClick.Invoke();
            altDestBtn.onClick.Invoke();
            closeBtn.onClick.Invoke();
            Assert.IsFalse(panel.TryClickAutoProvision());
            Assert.IsFalse(panel.TryClickDepartFirstLeg());
            Assert.IsFalse(panel.TrySelectDestination(altMap));
            Assert.IsFalse(panel.TryClickClose());

            Assert.AreEqual(expectedVisible, panel.IsVisible);
            Assert.AreEqual(origin, facade.CurrentMapId);
            Assert.AreEqual(targetMap, panel.SelectedDestination);
            Assert.AreEqual(expectedLegRows, panel.DisplayedLegRowCount);
            Assert.AreEqual(expectedLines, panel.LastProvisioningPlan.Lines.Count);
            Assert.AreEqual(expectedCost, panel.LastProvisioningPlan.TotalRequiredSaltSilver);
            Assert.AreEqual(300, facade.BountyAndSupply.SaltSilverBalance);
            Assert.AreEqual(0, facade.BountyAndSupply.GetOwnedSupplyCount("rations"));
            Assert.AreEqual(0, facade.BountyAndSupply.GetOwnedSupplyCount("lamp_oil"));
            Assert.AreEqual(0, facade.BountyAndSupply.GetOwnedSupplyCount("flare"));
            Assert.AreEqual(expectedFeedback, panel.FeedbackText);
            Assert.AreEqual(expectedSummary, panel.SummaryBannerText);
        }
    }
}
