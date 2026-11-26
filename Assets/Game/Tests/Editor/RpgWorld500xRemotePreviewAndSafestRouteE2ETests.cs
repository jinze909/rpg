using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xRemotePreviewAndSafestRouteE2ETests
    {
        [Test]
        public void PlayModeE2E_RemoteReadinessPreviewSafestGazetteerAndAutoProvisioningWithZeroLeak()
        {
            var hostGo = new GameObject("World500xRemotePreviewSafestE2ETestHost");
            var ctrl = hostGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.WhitewaterCrossing,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });
                ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.ExpeditionReadiness);
                ctrl.Shutdown();

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                DateTime clock = new DateTime(2026, 11, 25, 23, 30, 0, DateTimeKind.Utc);
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.WhitewaterCrossing,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    utcNowProvider: () => clock,
                    overrideTileSlices: new Sprite[] { null });

                RpgWorld500xGrandRuntimeFacade facade = ctrl.Bootstrapper.Driver.Facade;
                facade.Atmosphere.SetWeather(RpgWorldMapId.WhitewaterCrossing, RpgWorld500xWeatherKind.Clear);
                facade.Atmosphere.SetWeather(RpgWorldMapId.MineCamp, RpgWorld500xWeatherKind.Clear);
                facade.Atmosphere.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.GaleStorm);

                // 1. Open ExpeditionReadiness (P) -> preview stormy RedSaltHarbor remotely without leaving WhitewaterCrossing.
                Assert.IsTrue(RpgWorld500xPlayModeController.TryResolveModalHotkey(KeyCode.P, out RpgWorld500xModalKind prepModal));
                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { RequestedModalToggle = prepModal }, 0.1f);
                Assert.AreEqual(RpgWorld500xModalKind.ExpeditionReadiness, ctrl.MenuRouter.ActiveModal);
                Assert.IsFalse(ctrl.MenuRouter.ReadinessPanel.IsPreviewingRemoteMap);

                Assert.IsTrue(ctrl.MenuRouter.ReadinessPanel.TryPreviewTargetMap(RpgWorldMapId.RedSaltHarbor));
                Assert.IsTrue(ctrl.MenuRouter.ReadinessPanel.IsPreviewingRemoteMap);
                Assert.AreEqual(RpgWorldMapId.RedSaltHarbor, ctrl.MenuRouter.ReadinessPanel.EvaluatedMapId);
                Assert.AreEqual(RpgWorld500xReadinessGrade.Critical, ctrl.MenuRouter.ReadinessPanel.LastReport.Grade);
                Assert.IsTrue(ctrl.MenuRouter.ReadinessPanel.HeaderText.Contains("【远征预检】"));
                Assert.AreEqual(RpgWorldMapId.WhitewaterCrossing, facade.CurrentMapId);

                // Reset preview back to local WhitewaterCrossing.
                Assert.IsTrue(ctrl.MenuRouter.ReadinessPanel.TryPreviewTargetMap(null));
                Assert.IsFalse(ctrl.MenuRouter.ReadinessPanel.IsPreviewingRemoteMap);
                Assert.AreEqual(RpgWorldMapId.WhitewaterCrossing, ctrl.MenuRouter.ReadinessPanel.EvaluatedMapId);

                // 2. Live Gazetteer with preferSafestRoute=true confirms clear 1-hop MineCamp route and stormy RedSaltHarbor.
                List<RpgWorld500xRegionGazetteerCard> safestBefore =
                    RpgWorld500xWorldGazetteerService.BuildLiveWorldCards(
                        ctrl.Bootstrapper, elapsedSeconds: 5f, preferSafestRoute: true);
                RpgWorld500xRegionGazetteerCard mineBefore = FindCard(safestBefore, RpgWorldMapId.MineCamp);
                RpgWorld500xRegionGazetteerCard harborBefore = FindCard(safestBefore, RpgWorldMapId.RedSaltHarbor);
                Assert.AreEqual(1, mineBefore.RouteHopCount);
                Assert.IsFalse(mineBefore.IsHazardousRoute);
                Assert.IsTrue(mineBefore.FormattedCardZh.Contains("1跳可达"));
                Assert.IsTrue(harborBefore.IsHazardousRoute);
                Assert.IsTrue(harborBefore.FormattedCardZh.Contains("1跳险途"));

                // 3. Fund Salt-Silver and auto-provision for the safe MineCamp route -> live Gazetteer flips to fully stocked.
                facade.BountyAndSupply.AddSaltSilver(300);
                RpgWorld500xProvisioningReceipt receipt = RpgWorld500xCampProvisioningService.ExecuteAutoProvisioning(
                    ctrl.Bootstrapper,
                    RpgWorldMapId.MineCamp,
                    allowPartialIfUnderBudget: false,
                    elapsedSeconds: 5f,
                    preferSafestRoute: true);
                Assert.IsTrue(receipt.FullyProvisioned);

                List<RpgWorld500xRegionGazetteerCard> safestAfter =
                    RpgWorld500xWorldGazetteerService.BuildLiveWorldCards(
                        ctrl.Bootstrapper, elapsedSeconds: 5f, preferSafestRoute: true);
                RpgWorld500xRegionGazetteerCard mineAfter = FindCard(safestAfter, RpgWorldMapId.MineCamp);
                Assert.AreEqual(0, mineAfter.ProvisioningCostSaltSilver);
                Assert.IsTrue(mineAfter.FormattedCardZh.Contains("行囊已齐备"));

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
            IReadOnlyList<RpgWorld500xRegionGazetteerCard> cards,
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
