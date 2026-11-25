using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xExpeditionDigestHudPanelTests
    {
        [Test]
        public void ExpeditionDigestHudPanel_RendersLiveDigestRespectsCanvasGroupLockAndZeroLeak()
        {
            var root = new GameObject("World500xExpeditionDigestHudPanelTestRoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupPanel = new RpgWorld500xExpeditionDigestHudPanel(root.transform, warmupBoot))
                {
                    warmupPanel.Show(0f);
                    warmupPanel.TryClickRefresh(1f);
                    warmupPanel.TryClickClose();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                var supplyStore = new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["rpg.return.world.supply.item.lamp_oil"] = 1
                };
                var supplyUse = new RpgWorld500xFieldSupplyUseService(
                    k => supplyStore.TryGetValue(k, out int v) ? v : 0,
                    (k, v) => supplyStore[k] = v);

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xExpeditionDigestHudPanel(root.transform, boot, supplyUse))
                {
                    boot.Driver.Facade.BountyAndSupply.AddSaltSilver(88);
                    boot.Driver.Facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.Clear);

                    Assert.IsTrue(panel.Show(2f));
                    Assert.IsTrue(panel.IsVisible);
                    Assert.AreEqual(RpgWorldMapId.Soundwell, panel.LastDigest.MapId);
                    Assert.AreEqual(RpgWorld500xTravelRiskLevel.Safe, panel.LastDigest.TravelRiskLevel);
                    Assert.IsTrue(panel.HeaderText.Contains("远征营火手记"));
                    Assert.IsTrue(panel.StatusRibbonText.Contains("盐银 88"));
                    Assert.IsTrue(panel.RiskBannerText.Contains("平稳"));
                    Assert.IsTrue(panel.DigestBodyText.Contains("远征营火纪要"));

                    int refreshesBeforeLock = panel.RefreshCount;

                    // Parent CanvasGroup lock blocks refresh and close clicks cleanly.
                    CanvasGroup group = root.AddComponent<CanvasGroup>();
                    group.interactable = false;
                    Assert.IsFalse(panel.TryClickRefresh(10f));
                    Assert.IsFalse(panel.TryClickClose());
                    Assert.IsTrue(panel.IsVisible);
                    Assert.AreEqual(refreshesBeforeLock, panel.RefreshCount);
                    group.interactable = true;

                    // Activate lamp_oil buff and trigger GaleStorm -> Refresh updates risk & buff caption.
                    Assert.IsTrue(supplyUse.TryConsumeSupply("lamp_oil", 5f).Succeeded);
                    boot.Driver.Facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.GaleStorm);

                    Assert.IsTrue(panel.TryClickRefresh(12f));
                    Assert.AreEqual(refreshesBeforeLock + 1, panel.RefreshCount);
                    Assert.AreEqual(RpgWorld500xTravelRiskLevel.Hazardous, panel.LastDigest.TravelRiskLevel);
                    Assert.IsTrue(panel.RiskBannerText.Contains("高危"));
                    Assert.IsTrue(panel.DigestBodyText.Contains("灯油照明"));
                    Assert.IsTrue(panel.DigestBodyText.Contains("营火休整"));

                    Assert.IsTrue(panel.TryClickClose());
                    Assert.IsFalse(panel.IsVisible);
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
    }
}
