using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    [TestFixture]
    public sealed class RpgWorld500xFourRegionFastTravelBeaconCanvasGuardE2ETests
    {
        private static RpgMapLandmarkMarker FindFastTravelBeacon(RpgWorld500xExplorationHost host)
        {
            var markers = RpgWorldLandmarkCatalog.GetMarkers(host.Layout.MapId);
            for (int i = 0; i < markers.Count; i++)
            {
                RpgMapLandmarkMarker m = markers[i];
                if (m.SupportsFastTravel &&
                    m.Tile != host.CurrentTile &&
                    host.Layout.IsWalkable(m.Tile.x, m.Tile.y, gateOpen: false))
                {
                    return m;
                }
            }

            Assert.Fail($"No walkable fast-travel beacon found for {host.Layout.MapId}.");
            return default;
        }

        [Test]
        public void AllFourRegions_FastTravelBeaconHudPanel_DisabledCanvasRejectsWarpUntilRestored()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count, "Expected exactly 4 regions in 500x world scale.");

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var root = new GameObject("FourRegionBeaconDisabledCanvas_" + spec.MapId);
                try
                {
                    using (var boot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: spec.MapId,
                        overrideTileSlices: new Sprite[] { null }))
                    using (var panel = new RpgWorld500xFastTravelBeaconHudPanel(root.transform, boot))
                    {
                        RpgWorld500xExplorationHost host = boot.Driver.Facade.Session.Host;
                        RpgMapLandmarkMarker target = FindFastTravelBeacon(host);
                        host.Tracker.TryDiscoverMarker(target.Id);

                        Assert.IsTrue(panel.Show());
                        var canvas = root.transform.Find("World500xFastTravelBeaconHudPanel").GetComponent<Canvas>();
                        var warpBtn = canvas.transform
                            .Find("BeaconWarpModalBox/BeaconRows/BeaconRow_" + target.Id + "/WarpButton")
                            .GetComponent<Button>();
                        Assert.IsNotNull(warpBtn);

                        Vector2 startPos = host.PlayerPosition;
                        string statusBefore = panel.StatusText;

                        canvas.enabled = false;
                        warpBtn.onClick.Invoke();
                        Assert.IsFalse(panel.TryClickWarp(target.Id));
                        Assert.AreEqual(startPos, host.PlayerPosition);
                        Assert.AreEqual(statusBefore, panel.StatusText);

                        canvas.enabled = true;
                        Assert.IsTrue(panel.TryClickWarp(target.Id));
                        Assert.AreEqual(target.Tile, host.CurrentTile);
                        StringAssert.Contains("已传送至", panel.StatusText);
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
        public void AllFourRegions_FastTravelBeaconHudPanel_HiddenAndCanvasGroupLockRejectWarpWithZeroLeak()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count, "Expected exactly 4 regions in 500x world scale.");

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var root = new GameObject("FourRegionBeaconCanvasGroupLock_" + spec.MapId);
                try
                {
                    using (var boot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: spec.MapId,
                        overrideTileSlices: new Sprite[] { null }))
                    using (var panel = new RpgWorld500xFastTravelBeaconHudPanel(root.transform, boot))
                    {
                        RpgWorld500xExplorationHost host = boot.Driver.Facade.Session.Host;
                        RpgMapLandmarkMarker target = FindFastTravelBeacon(host);
                        host.Tracker.TryDiscoverMarker(target.Id);

                        Vector2 startPos = host.PlayerPosition;
                        Assert.IsFalse(panel.TryClickWarp(target.Id));
                        Assert.AreEqual(startPos, host.PlayerPosition);

                        Assert.IsTrue(panel.Show());
                        CanvasGroup group = root.AddComponent<CanvasGroup>();
                        group.interactable = false;

                        var warpBtn = root.transform
                            .Find("World500xFastTravelBeaconHudPanel/BeaconWarpModalBox/BeaconRows/BeaconRow_" + target.Id + "/WarpButton")
                            .GetComponent<Button>();
                        Assert.IsNotNull(warpBtn);

                        Assert.IsFalse(panel.TryClickWarp(target.Id));
                        warpBtn.onClick.Invoke();
                        Assert.AreEqual(startPos, host.PlayerPosition);
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
