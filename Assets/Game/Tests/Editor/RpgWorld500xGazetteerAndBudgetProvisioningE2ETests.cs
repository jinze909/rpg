using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xGazetteerAndBudgetProvisioningE2ETests
    {
        [Test]
        public void PlayModeE2E_GazetteerLiveCardsSyncWithBudgetCappedProvisioningAndSaveSlotRollback()
        {
            var hostGo = new GameObject("World500xGazetteerBudgetE2ETestHost");
            var ctrl = hostGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });
                ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.WorldGazetteer);
                ctrl.Shutdown();

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                DateTime clock = new DateTime(2026, 11, 25, 21, 0, 0, DateTimeKind.Utc);
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    utcNowProvider: () => clock,
                    overrideTileSlices: new Sprite[] { null });

                RpgWorld500xGrandRuntimeFacade facade = ctrl.Bootstrapper.Driver.Facade;
                facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.Clear);
                facade.Atmosphere.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.Clear);
                facade.Atmosphere.SetWeather(RpgWorldMapId.MineCamp, RpgWorld500xWeatherKind.GaleStorm);

                // 1. Open WorldGazetteer (G) -> MineCamp shows 2-hop hazardous route & insufficient Salt-Silver.
                Assert.IsTrue(RpgWorld500xPlayModeController.TryResolveModalHotkey(KeyCode.G, out RpgWorld500xModalKind gazetteerModal));
                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { RequestedModalToggle = gazetteerModal }, 0.1f);
                Assert.AreEqual(RpgWorld500xModalKind.WorldGazetteer, ctrl.MenuRouter.ActiveModal);
                Assert.AreEqual(4, ctrl.MenuRouter.GazetteerPanel.LastCards.Count);

                RpgWorld500xRegionGazetteerCard mineInitial = FindCard(ctrl.MenuRouter.GazetteerPanel.LastCards, RpgWorldMapId.MineCamp);
                Assert.IsTrue(mineInitial.HasLiveProvisioningData);
                Assert.AreEqual(2, mineInitial.RouteHopCount);
                Assert.IsTrue(mineInitial.IsHazardousRoute);
                Assert.Greater(mineInitial.ProvisioningCostSaltSilver, 0);
                Assert.IsFalse(mineInitial.CanAffordProvisioning);
                Assert.IsTrue(mineInitial.FormattedCardZh.Contains("2跳险途"));
                Assert.IsTrue(mineInitial.FormattedCardZh.Contains("盐银不足"));

                // 2. Fund 300 Salt-Silver and open ExpeditionReadiness (P) -> verify breakdown, strict cap deny, and partial cap buy.
                facade.BountyAndSupply.AddSaltSilver(300);
                Assert.IsTrue(RpgWorld500xPlayModeController.TryResolveModalHotkey(KeyCode.P, out RpgWorld500xModalKind prepModal));
                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { RequestedModalToggle = prepModal }, 0.1f);
                Assert.AreEqual(RpgWorld500xModalKind.ExpeditionReadiness, ctrl.MenuRouter.ActiveModal);

                string breakdownBefore = ctrl.MenuRouter.ReadinessPanel.FormatProvisioningBreakdownForDestination(RpgWorldMapId.MineCamp);
                Assert.IsTrue(breakdownBefore.Contains("合计"));
                Assert.IsTrue(breakdownBefore.Contains("盐银"));

                Assert.IsTrue(ctrl.MenuRouter.ReadinessPanel.TryAutoProvisionForDestination(
                    RpgWorldMapId.MineCamp, allowPartialIfUnderBudget: false, maxBudgetSaltSilver: 35));
                Assert.AreEqual(0, ctrl.MenuRouter.ReadinessPanel.LastProvisioningReceipt.TotalSpentSaltSilver);
                Assert.AreEqual(300, facade.BountyAndSupply.SaltSilverBalance);
                Assert.IsTrue(ctrl.MenuRouter.ReadinessPanel.FeedbackText.Contains("超出预算上限"));

                Assert.IsTrue(ctrl.MenuRouter.ReadinessPanel.TryAutoProvisionForDestination(
                    RpgWorldMapId.MineCamp, allowPartialIfUnderBudget: true, maxBudgetSaltSilver: 35));
                int partialSpent = ctrl.MenuRouter.ReadinessPanel.LastProvisioningReceipt.TotalSpentSaltSilver;
                Assert.Greater(partialSpent, 0);
                Assert.LessOrEqual(partialSpent, 35);
                int partialBalance = facade.BountyAndSupply.SaltSilverBalance;
                Assert.AreEqual(300 - partialSpent, partialBalance);

                // 3. Save partial-provisioning checkpoint to Slot2 (O).
                Assert.IsTrue(RpgWorld500xPlayModeController.TryResolveModalHotkey(KeyCode.O, out RpgWorld500xModalKind saveModal));
                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { RequestedModalToggle = saveModal }, 0.1f);
                Assert.AreEqual(RpgWorld500xModalKind.SaveSlots, ctrl.MenuRouter.ActiveModal);
                Assert.IsTrue(ctrl.MenuRouter.SaveSlotPanel.TryClickSave(RpgWorld500xSaveSlotId.Slot2));

                // 4. Complete full provisioning for MineCamp -> breakdown and Gazetteer live card both flip to fully stocked.
                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { RequestedModalToggle = prepModal }, 0.1f);
                Assert.IsTrue(ctrl.MenuRouter.ReadinessPanel.TryAutoProvisionForDestination(
                    RpgWorldMapId.MineCamp, allowPartialIfUnderBudget: false));
                Assert.IsTrue(ctrl.MenuRouter.ReadinessPanel.LastProvisioningReceipt.FullyProvisioned);
                Assert.AreEqual(
                    "行囊已齐备（无需补购）",
                    ctrl.MenuRouter.ReadinessPanel.FormatProvisioningBreakdownForDestination(RpgWorldMapId.MineCamp));

                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { RequestedModalToggle = gazetteerModal }, 0.1f);
                RpgWorld500xRegionGazetteerCard mineStocked = FindCard(ctrl.MenuRouter.GazetteerPanel.LastCards, RpgWorldMapId.MineCamp);
                Assert.AreEqual(0, mineStocked.ProvisioningCostSaltSilver);
                Assert.IsTrue(mineStocked.CanAffordProvisioning);
                Assert.IsTrue(mineStocked.FormattedCardZh.Contains("行囊已齐备"));

                // 5. Reload Slot2 -> wallet rolls back to partialBalance and Gazetteer restores pending restock cost.
                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { RequestedModalToggle = saveModal }, 0.1f);
                Assert.IsTrue(ctrl.MenuRouter.SaveSlotPanel.TryClickLoad(RpgWorld500xSaveSlotId.Slot2));
                Assert.AreEqual(partialBalance, ctrl.Bootstrapper.Driver.Facade.BountyAndSupply.SaltSilverBalance);

                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { RequestedModalToggle = gazetteerModal }, 0.1f);
                RpgWorld500xRegionGazetteerCard mineRolledBack = FindCard(ctrl.MenuRouter.GazetteerPanel.LastCards, RpgWorldMapId.MineCamp);
                Assert.Greater(mineRolledBack.ProvisioningCostSaltSilver, 0);
                Assert.IsTrue(mineRolledBack.CanAffordProvisioning);
                Assert.IsTrue(mineRolledBack.FormattedCardZh.Contains("可备齐"));

                ctrl.Shutdown();
                Assert.AreEqual(0, hostGo.transform.childCount);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
            }
            finally
            {
                ctrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(hostGo);
            }
        }

        private static RpgWorld500xRegionGazetteerCard FindCard(
            System.Collections.Generic.IReadOnlyList<RpgWorld500xRegionGazetteerCard> cards,
            RpgWorldMapId mapId)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i].MapId == mapId)
                {
                    return cards[i];
                }
            }

            Assert.Fail("Expected gazetteer card for map: " + mapId);
            return default;
        }
    }
}
