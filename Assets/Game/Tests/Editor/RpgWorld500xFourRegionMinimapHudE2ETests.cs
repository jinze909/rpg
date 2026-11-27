using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionMinimapHudE2ETests
    {
        [Test]
        public void FourRegion_MinimapHud_RendersFixedResolutionTextureTracksDiscoveryOverviewAndDisposesCleanly()
        {
            var root = new GameObject("World500xFourRegionMinimapHudRoot");
            try
            {
                IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
                Assert.AreEqual(4, specs.Count);

                for (int i = 0; i < specs.Count; i++)
                {
                    RpgExpandedMapSpec spec = specs[i];
                    bool useHarborPalette =
                        spec.MapId == RpgWorldMapId.RedSaltHarbor ||
                        spec.MapId == RpgWorldMapId.WhitewaterCrossing;

                    var regionRoot = new GameObject("RegionMinimap_" + spec.MapId);
                    regionRoot.transform.SetParent(root.transform, false);
                    Texture2D ownedTexture = null;
                    RpgWorld500xMinimapHud disposedHud = null;

                    try
                    {
                        using (var boot = new RpgWorld500xSceneBootstrapper(
                            initialMapId: spec.MapId,
                            overrideTileSlices: new Sprite[] { null }))
                        {
                            RpgMapExplorationTracker tracker = boot.Driver.Facade.Session.Host.Tracker;
                            IReadOnlyList<RpgMapLandmarkMarker> markers = RpgWorldLandmarkCatalog.GetMarkers(spec.MapId);
                            Assert.Greater(markers.Count, 0, $"Expected landmark markers on {spec.MapId}.");

                            using (var hud = new RpgWorld500xMinimapHud(
                                regionRoot.transform,
                                spec,
                                (x, y) => useHarborPalette
                                    ? (y < 420 ? (int)HarborTileKind.WaterDeep : (int)HarborTileKind.QuayStone)
                                    : (x < 320 ? (int)SoundwellTileKind.WallTop : (int)SoundwellTileKind.BellPedestal),
                                useHarborPalette: useHarborPalette,
                                tracker: tracker))
                            {
                                disposedHud = hud;
                                ownedTexture = hud.MinimapTexture;

                                Assert.AreEqual(RpgWorld500xMinimapHud.DefaultTextureWidth, hud.TextureWidth);
                                Assert.AreEqual(RpgWorld500xMinimapHud.DefaultTextureHeight, hud.TextureHeight);
                                Assert.IsNotNull(ownedTexture);
                                Assert.AreEqual(FilterMode.Point, ownedTexture.filterMode);
                                Assert.AreEqual(tracker.TotalMarkerCount, hud.PinCount);

                                int compactInitial = hud.VisiblePinCount;
                                Assert.Less(compactInitial, hud.PinCount,
                                    $"Compact mode should hide undiscovered non-main-story pins on {spec.MapId}.");

                                // Find and discover an undiscovered non-MainStory marker to verify pin reveal.
                                RpgMapLandmarkMarker targetMarker = default;
                                bool foundTarget = false;
                                for (int m = 0; m < markers.Count; m++)
                                {
                                    if (markers[m].Category != RpgMapMarkerCategory.MainStory &&
                                        !tracker.IsMarkerDiscovered(markers[m].Id))
                                    {
                                        targetMarker = markers[m];
                                        foundTarget = true;
                                        break;
                                    }
                                }

                                Assert.IsTrue(foundTarget, $"Expected an undiscovered non-main-story marker on {spec.MapId}.");
                                Assert.IsTrue(tracker.TryDiscoverMarker(targetMarker.Id));

                                var playerPos = new Vector2(targetMarker.Tile.x + 0.5f, targetMarker.Tile.y + 0.5f);
                                Assert.IsTrue(hud.UpdatePlayerAndCompass(playerPos, spec.DisplayNameZh));
                                Assert.Greater(hud.VisiblePinCount, compactInitial);
                                StringAssert.Contains(spec.DisplayNameZh, hud.LastCompassText);
                                StringAssert.Contains(
                                    "地标 " + tracker.DiscoveredMarkerCount + "/" + tracker.TotalMarkerCount,
                                    hud.LastCompassText);

                                // Overview mode reveals all pins across the 500x map and collapses back cleanly.
                                Assert.IsTrue(hud.SetOverviewMode(true));
                                Assert.IsTrue(hud.IsOverviewMode);
                                Assert.AreEqual(hud.PinCount, hud.VisiblePinCount);

                                Assert.IsTrue(hud.SetOverviewMode(false));
                                Assert.IsFalse(hud.IsOverviewMode);
                                Assert.Less(hud.VisiblePinCount, hud.PinCount);

                                // Threat marker toggles cleanly with finite coordinates.
                                Assert.IsTrue(hud.UpdateThreatMarker(playerPos, true));
                                Assert.IsTrue(hud.UpdateThreatMarker(playerPos, false));
                            }

                            Assert.IsNotNull(disposedHud);
                            Assert.IsTrue(disposedHud.IsDisposed);
                            Assert.IsTrue(ownedTexture == null,
                                $"Owned minimap Texture2D must be destroyed on Dispose for {spec.MapId}.");
                        }
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(regionRoot);
                    }
                }

                Assert.AreEqual(0, root.transform.childCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FourRegion_MinimapHud_RejectsNonFiniteInputsAndPostTeardownMutations()
        {
            var root = new GameObject("World500xFourRegionMinimapGuardRoot");
            RpgWorld500xMinimapHud hud = null;
            Texture2D ownedTexture = null;
            try
            {
                RpgExpandedMapSpec spec = RpgExpandedWorldScale.GetSpec(RpgWorldMapId.MineCamp);
                var tracker = new RpgMapExplorationTracker(RpgWorldMapId.MineCamp);

                hud = new RpgWorld500xMinimapHud(
                    root.transform,
                    spec,
                    (x, y) => (int)SoundwellTileKind.WallTop,
                    useHarborPalette: false,
                    tracker: tracker);
                ownedTexture = hud.MinimapTexture;
                Assert.IsNotNull(ownedTexture);

                // Non-finite inputs fail closed without corrupting state.
                Assert.IsFalse(hud.UpdatePlayerAndCompass(new Vector2(float.NaN, 120f)));
                Assert.IsFalse(hud.UpdatePlayerAndCompass(new Vector2(120f, float.NegativeInfinity)));
                Assert.IsFalse(hud.UpdateThreatMarker(new Vector2(float.PositiveInfinity, 120f), true));
                Assert.IsTrue(hud.UpdateThreatMarker(new Vector2(float.NaN, 0f), false));

                // External parent teardown + Dispose releases owned Texture2D and blocks further mutations.
                UnityEngine.Object.DestroyImmediate(root);
                root = null;
                Assert.IsTrue(hud.IsDisposed);
                Assert.DoesNotThrow(() => hud.Dispose());
                Assert.IsTrue(ownedTexture == null, "Owned minimap Texture2D must be destroyed after external teardown + Dispose.");
                Assert.IsFalse(hud.SetOverviewMode(true));
                Assert.IsFalse(hud.UpdatePlayerAndCompass(new Vector2(100f, 100f)));
                Assert.IsFalse(hud.UpdateThreatMarker(new Vector2(100f, 100f), true));
            }
            finally
            {
                hud?.Dispose();
                if (root != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }
    }
}
