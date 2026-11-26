using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xDigestWorldWeatherAndReputationE2ETests
    {
        [Test]
        public void PlayModeE2E_GazetteerSafestToggleReadinessProvisioningAndWorldDigestZeroLeak()
        {
            var hostGo = new GameObject("World500xDigestWeatherRepE2ETestHost");
            var ctrl = hostGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.WhitewaterCrossing,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });
                ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.WorldGazetteer);
                ctrl.Shutdown();

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                DateTime clock = new DateTime(2026, 11, 26, 1, 30, 0, DateTimeKind.Utc);
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.WhitewaterCrossing,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    utcNowProvider: () => clock,
                    overrideTileSlices: new Sprite[] { null });

                RpgWorld500xGrandRuntimeFacade facade = ctrl.Bootstrapper.Driver.Facade;
                facade.Atmosphere.SetWeather(RpgWorldMapId.WhitewaterCrossing, RpgWorld500xWeatherKind.Clear);
                facade.Atmosphere.SetWeather(RpgWorldMapId.MineCamp, RpgWorld500xWeatherKind.Clear);
                facade.Atmosphere.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.GaleStorm);

                // 1. Open WorldGazetteer (G) and toggle safest-route mode -> MineCamp shows clear 1-hop route.
                Assert.IsTrue(RpgWorld500xPlayModeController.TryResolveModalHotkey(KeyCode.G, out RpgWorld500xModalKind gazetteerModal));
                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { RequestedModalToggle = gazetteerModal }, 0.1f);
                Assert.AreEqual(RpgWorld500xModalKind.WorldGazetteer, ctrl.MenuRouter.ActiveModal);
                Assert.IsTrue(ctrl.MenuRouter.GazetteerPanel.TryToggleSafestRoute());
                Assert.IsTrue(ctrl.MenuRouter.GazetteerPanel.PreferSafestRoute);
                Assert.IsTrue(ctrl.MenuRouter.GazetteerPanel.StatusText.Contains("路径：避险优先"));

                RpgWorld500xRegionGazetteerCard mineCard = FindCard(ctrl.MenuRouter.GazetteerPanel.LastCards, RpgWorldMapId.MineCamp);
                Assert.AreEqual(1, mineCard.RouteHopCount);
                Assert.IsFalse(mineCard.IsHazardousRoute);
                Assert.IsTrue(mineCard.FormattedCardZh.Contains("1跳可达"));

                // 2. Open ExpeditionReadiness (P), fund Salt-Silver, and auto-provision with preferSafestRoute=true.
                facade.BountyAndSupply.AddSaltSilver(300);
                Assert.IsTrue(RpgWorld500xPlayModeController.TryResolveModalHotkey(KeyCode.P, out RpgWorld500xModalKind prepModal));
                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { RequestedModalToggle = prepModal }, 0.1f);
                Assert.AreEqual(RpgWorld500xModalKind.ExpeditionReadiness, ctrl.MenuRouter.ActiveModal);
                Assert.IsTrue(ctrl.MenuRouter.ReadinessPanel.TryAutoProvisionForDestination(
                    RpgWorldMapId.MineCamp, allowPartialIfUnderBudget: false, preferSafestRoute: true));
                Assert.IsTrue(ctrl.MenuRouter.ReadinessPanel.LastProvisioningReceipt.FullyProvisioned);
                Assert.AreEqual(
                    "行囊已齐备（无需补购）",
                    ctrl.MenuRouter.ReadinessPanel.FormatProvisioningBreakdownForDestination(
                        RpgWorldMapId.MineCamp, preferSafestRoute: true));

                // 3. Build live Expedition Digest -> includes 4-region weather bulletin, world reputation overview, and milestone summary.
                RpgWorld500xExpeditionDigest digest = ctrl.BuildExpeditionDigest();
                Assert.AreEqual(RpgWorldMapId.WhitewaterCrossing, digest.MapId);
                Assert.GreaterOrEqual(digest.HazardousRegionCount, 1);
                Assert.IsTrue(digest.WorldWeatherBulletinZh.Contains("【四境气象速报】"));
                Assert.IsTrue(digest.WorldWeatherBulletinZh.Contains("高危预警"));
                Assert.IsTrue(digest.WorldReputationSummaryZh.Contains("【四境声望总览】"));
                Assert.IsTrue(digest.MilestoneSummaryZh.Contains("【远征里程碑】"));
                Assert.IsTrue(digest.FormattedDigestZh.Contains("【四境气象速报】"));
                Assert.IsTrue(digest.FormattedDigestZh.Contains("【四境声望总览】"));
                Assert.IsTrue(digest.FormattedDigestZh.Contains("【远征里程碑】"));

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
