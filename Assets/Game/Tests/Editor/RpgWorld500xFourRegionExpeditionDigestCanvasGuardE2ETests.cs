using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    [TestFixture]
    public sealed class RpgWorld500xFourRegionExpeditionDigestCanvasGuardE2ETests
    {
        [Test]
        public void AllFourRegions_ExpeditionDigestHudPanel_DisabledCanvasRejectsRefreshAndCloseUntilRestored()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count, "Expected exactly 4 regions in 500x world scale.");

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var root = new GameObject("FourRegionDigestDisabledCanvas_" + spec.MapId);
                try
                {
                    using (var boot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: spec.MapId,
                        overrideTileSlices: new Sprite[] { null }))
                    using (var panel = new RpgWorld500xExpeditionDigestHudPanel(root.transform, boot))
                    {
                        boot.Driver.Facade.Atmosphere.SetWeather(spec.MapId, RpgWorld500xWeatherKind.Clear);
                        Assert.IsTrue(panel.Show(2f));
                        Assert.IsTrue(panel.IsVisible);
                        Assert.AreEqual(spec.MapId, panel.LastDigest.MapId);
                        Assert.AreEqual(RpgWorld500xTravelRiskLevel.Safe, panel.LastDigest.TravelRiskLevel);

                        var canvas = root.transform.Find("World500xExpeditionDigestHudPanel").GetComponent<Canvas>();
                        var refresh = canvas.transform.Find("DigestModalBox/RefreshDigestButton").GetComponent<Button>();
                        var close = canvas.transform.Find("DigestModalBox/CloseDigestButton").GetComponent<Button>();
                        int before = panel.RefreshCount;

                        canvas.enabled = false;
                        boot.Driver.Facade.Atmosphere.SetWeather(spec.MapId, RpgWorld500xWeatherKind.GaleStorm);
                        refresh.onClick.Invoke();
                        close.onClick.Invoke();
                        Assert.IsFalse(panel.TryClickRefresh(9f));
                        Assert.IsFalse(panel.TryClickClose());
                        Assert.IsTrue(panel.IsVisible);
                        Assert.AreEqual(before, panel.RefreshCount);
                        Assert.AreEqual(2f, panel.LastElapsedSeconds);
                        Assert.AreEqual(RpgWorld500xTravelRiskLevel.Safe, panel.LastDigest.TravelRiskLevel);

                        canvas.enabled = true;
                        Assert.IsTrue(panel.TryClickRefresh(12f));
                        Assert.AreEqual(before + 1, panel.RefreshCount);
                        Assert.AreEqual(12f, panel.LastElapsedSeconds);
                        Assert.AreEqual(RpgWorld500xTravelRiskLevel.Hazardous, panel.LastDigest.TravelRiskLevel);
                        Assert.IsTrue(panel.TryClickClose());
                        Assert.IsFalse(panel.IsVisible);
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
        public void AllFourRegions_ExpeditionDigestHudPanel_HiddenAndCanvasGroupLockedRejectClicksWithZeroLeak()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count, "Expected exactly 4 regions in 500x world scale.");

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var root = new GameObject("FourRegionDigestCanvasGroupLock_" + spec.MapId);
                try
                {
                    using (var boot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: spec.MapId,
                        overrideTileSlices: new Sprite[] { null }))
                    using (var panel = new RpgWorld500xExpeditionDigestHudPanel(root.transform, boot))
                    {
                        var refresh = root.transform
                            .Find("World500xExpeditionDigestHudPanel/DigestModalBox/RefreshDigestButton")
                            .GetComponent<Button>();
                        int hiddenCount = panel.RefreshCount;

                        Assert.IsFalse(panel.TryClickRefresh(1f));
                        Assert.IsFalse(panel.TryClickClose());
                        refresh.onClick.Invoke();
                        Assert.AreEqual(hiddenCount, panel.RefreshCount);

                        Assert.IsTrue(panel.Show(3f));
                        int lockedCount = panel.RefreshCount;

                        CanvasGroup group = root.AddComponent<CanvasGroup>();
                        group.interactable = false;
                        Assert.IsFalse(panel.TryClickRefresh(5f));
                        Assert.IsFalse(panel.TryClickClose());
                        refresh.onClick.Invoke();
                        Assert.IsTrue(panel.IsVisible);
                        Assert.AreEqual(lockedCount, panel.RefreshCount);

                        group.interactable = true;
                        Assert.IsTrue(panel.TryClickClose());
                        Assert.IsFalse(panel.IsVisible);
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
}
