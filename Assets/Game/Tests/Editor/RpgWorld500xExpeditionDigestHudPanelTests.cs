using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xExpeditionDigestHudPanelTests
    {
        [UnityTest]
        public IEnumerator ExpeditionDigestRiskBannerCapturesSafeAndHazardousGameView()
        {
            yield return new EnterPlayMode(expectDomainReload: true);
            yield return null;
            var root = new GameObject("World500xExpeditionDigestVisualEvidenceRoot");
            try
            {
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xExpeditionDigestHudPanel(root.transform, boot))
                {
                    var atmosphere = boot.Driver.Facade.Atmosphere;
                    foreach (RpgWorldMapId map in Enum.GetValues(typeof(RpgWorldMapId)))
                        atmosphere.SetWeather(map, RpgWorld500xWeatherKind.Clear);

                    Assert.IsTrue(panel.Show(0f));
                    yield return null;
                    var canvas = root.transform.Find("World500xExpeditionDigestHudPanel").GetComponent<Canvas>();
                    var banner = canvas.transform.Find("DigestModalBox/RiskBannerBox/RiskBannerLabel").GetComponent<Text>();
                    Font expectedFont = Resources.Load<Font>("Soundwell/Fonts/SoundwellChinese");
                    Assert.IsNotNull(expectedFont);
                    foreach (Text label in canvas.GetComponentsInChildren<Text>(true))
                        Assert.AreSame(expectedFont, label.font, label.name + " must render Chinese glyphs.");
                    foreach (char glyph in "远征营火手记行旅风险高危区域刷新合上")
                        Assert.IsTrue(expectedFont.HasCharacter(glyph), "Missing glyph: " + glyph);
                    Assert.AreEqual(RpgWorld500xTravelRiskLevel.Safe, panel.LastDigest.TravelRiskLevel);
                    StringAssert.Contains("高危区域 " + panel.HazardousRegionCount + " 境", banner.text);
                    AssertReadableLayout(canvas);
                    SoundwellSmokeHost.CaptureStoryCanvas(canvas, "world500x-expedition-digest-risk-safe");

                    atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.GaleStorm);
                    atmosphere.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.GaleStorm);
                    Assert.IsTrue(panel.TryClickRefresh(1f));
                    yield return null;
                    Assert.AreEqual(RpgWorld500xTravelRiskLevel.Hazardous, panel.LastDigest.TravelRiskLevel);
                    Assert.GreaterOrEqual(panel.HazardousRegionCount, 2);
                    StringAssert.Contains("高危区域 " + panel.HazardousRegionCount + " 境", banner.text);
                    AssertReadableLayout(canvas);
                    SoundwellSmokeHost.CaptureStoryCanvas(canvas, "world500x-expedition-digest-risk-hazardous");
                    Assert.IsTrue(panel.TryClickClose());
                }
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
            }
            yield return null;
            yield return new ExitPlayMode();
        }

        private static void AssertReadableLayout(Canvas canvas)
        {
            Canvas.ForceUpdateCanvases();
            Assert.IsTrue(canvas.pixelPerfect);
            Assert.AreEqual(new Vector2(960f, 540f), canvas.GetComponent<CanvasScaler>().referenceResolution);
            foreach (Text label in canvas.GetComponentsInChildren<Text>(true))
            {
                Assert.GreaterOrEqual(label.fontSize, 16, label.name + " is too small at the native reference size.");
                Assert.IsFalse(label.resizeTextForBestFit, label.name + " must not silently shrink text.");
                Assert.LessOrEqual(label.preferredHeight, label.rectTransform.rect.height + 1f,
                    label.name + " clips wrapped text.");
            }
        }

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

                    // Hidden panel rejects Try* calls AND direct stale button callbacks cleanly.
                    int refreshesWhileHidden = panel.RefreshCount;
                    Assert.IsFalse(panel.TryClickRefresh(1f));
                    Assert.IsFalse(panel.TryClickClose());
                    Button refreshBtn = root.transform
                        .Find("World500xExpeditionDigestHudPanel/DigestModalBox/RefreshDigestButton")
                        .GetComponent<Button>();
                    refreshBtn.onClick.Invoke();
                    Assert.AreEqual(refreshesWhileHidden, panel.RefreshCount);

                    Assert.IsTrue(panel.Show(2f));
                    Assert.IsTrue(panel.IsVisible);
                    Assert.AreEqual(RpgWorldMapId.Soundwell, panel.LastDigest.MapId);
                    Assert.AreEqual(RpgWorld500xTravelRiskLevel.Safe, panel.LastDigest.TravelRiskLevel);
                    Assert.Greater(panel.LastDigest.ReadinessScore, 0);
                    Assert.IsTrue(panel.HeaderText.Contains("远征营火手记"));
                    Assert.IsTrue(panel.StatusRibbonText.Contains("盐银 88"));
                    Assert.IsTrue(panel.RiskBannerText.Contains("平稳"));
                    Assert.IsTrue(panel.RiskBannerText.Contains("高危区域"));
                    Assert.IsTrue(panel.WorldWeatherBulletinText.Contains("【四境气象速报】"));
                    Assert.IsTrue(panel.WorldReputationSummaryText.Contains("【四境声望总览】"));
                    Assert.IsTrue(panel.MilestoneSummaryText.Contains("【远征里程碑】"));
                    Assert.IsTrue(panel.DigestBodyText.Contains("远征营火纪要"));
                    Assert.IsTrue(panel.DigestBodyText.Contains("整备状态："));
                    Assert.IsTrue(panel.DigestBodyText.Contains("本境待补"));

                    int refreshesBeforeLock = panel.RefreshCount;

                    // Parent CanvasGroup lock blocks refresh, close, AND direct stale callbacks cleanly.
                    CanvasGroup group = root.AddComponent<CanvasGroup>();
                    group.interactable = false;
                    Assert.IsFalse(panel.TryClickRefresh(10f));
                    Assert.IsFalse(panel.TryClickClose());
                    refreshBtn.onClick.Invoke();
                    Assert.IsTrue(panel.IsVisible);
                    Assert.AreEqual(refreshesBeforeLock, panel.RefreshCount);
                    group.interactable = true;

                    // Activate lamp_oil buff and trigger GaleStorm -> Refresh updates risk & buff caption.
                    Assert.IsTrue(supplyUse.TryConsumeSupply("lamp_oil", 5f).Succeeded);
                    boot.Driver.Facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.GaleStorm);

                    Assert.IsTrue(panel.TryClickRefresh(12f));
                    Assert.AreEqual(refreshesBeforeLock + 1, panel.RefreshCount);
                    Assert.AreEqual(RpgWorld500xTravelRiskLevel.Hazardous, panel.LastDigest.TravelRiskLevel);
                    Assert.GreaterOrEqual(panel.HazardousRegionCount, 1);
                    Assert.IsTrue(panel.RiskBannerText.Contains("高危"));
                    Assert.IsTrue(panel.DigestBodyText.Contains("灯油照明"));
                    Assert.IsTrue(panel.DigestBodyText.Contains("营火休整"));

                    Assert.IsTrue(panel.TryClickClose());
                    Assert.IsFalse(panel.IsVisible);
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
    }
}
