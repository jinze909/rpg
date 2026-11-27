using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionWorldGazetteerHudE2ETests
    {
        [Test]
        public void FourRegion_WorldGazetteerHudPanel_OverviewSafestRouteToggleAndSearchAcrossAllFourRegionsWithZeroSpriteLeak()
        {
            var root = new GameObject("World500xFourRegionGazetteerHudRoot");
            try
            {
                IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
                Assert.AreEqual(4, specs.Count);

                // Warm up one-time UGUI/font and per-region caches before steady-state leak baseline.
                for (int w = 0; w < specs.Count; w++)
                {
                    using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: specs[w].MapId,
                        overrideTileSlices: new Sprite[] { null }))
                    using (var warmupPanel = new RpgWorld500xWorldGazetteerHudPanel(root.transform, warmupBoot))
                    {
                        warmupPanel.Show();
                        warmupPanel.TryToggleSafestRoute();
                        warmupPanel.TryClickClose();
                    }
                }

                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                for (int i = 0; i < specs.Count; i++)
                {
                    RpgExpandedMapSpec spec = specs[i];
                    var regionRoot = new GameObject("RegionGazetteer_" + spec.MapId);
                    regionRoot.transform.SetParent(root.transform, false);
                    try
                    {
                        using (var boot = new RpgWorld500xSceneBootstrapper(
                            initialMapId: spec.MapId,
                            overrideTileSlices: new Sprite[] { null }))
                        using (var panel = new RpgWorld500xWorldGazetteerHudPanel(regionRoot.transform, boot))
                        {
                            Assert.IsTrue(panel.Show(), $"WorldGazetteerHudPanel failed to show on {spec.MapId}.");
                            Assert.IsTrue(panel.IsVisible);
                            Assert.AreEqual(RpgWorld500xGazetteerHudTab.RegionOverview, panel.ActiveTab);
                            Assert.AreEqual(4, panel.DisplayedRowCount);
                            Assert.AreEqual(4, panel.LastCards.Count);
                            Assert.IsFalse(panel.PreferSafestRoute);
                            StringAssert.Contains(spec.DisplayNameZh, panel.StatusText);
                            StringAssert.Contains("路径：最短耗时", panel.StatusText);
                            StringAssert.Contains("补给箱 42", panel.HeaderText);
                            StringAssert.Contains("捷径 16", panel.HeaderText);
                            StringAssert.Contains("悬赏 12", panel.HeaderText);

                            // Toggle shortest-time <-> safest-route mode cleanly.
                            Assert.IsTrue(panel.TryToggleSafestRoute());
                            Assert.IsTrue(panel.PreferSafestRoute);
                            StringAssert.Contains("路径：避险优先", panel.StatusText);
                            Assert.IsTrue(panel.TrySetPreferSafestRoute(false));
                            Assert.IsFalse(panel.PreferSafestRoute);
                            StringAssert.Contains("路径：最短耗时", panel.StatusText);

                            // Cross-region search, active-map scope toggle, and clear search round-trip.
                            Assert.IsTrue(panel.TrySearch("港区航标巡查", filterActiveMapOnly: false));
                            Assert.AreEqual(RpgWorld500xGazetteerHudTab.SearchResults, panel.ActiveTab);
                            Assert.IsFalse(panel.FilterActiveMapOnly);
                            Assert.Greater(panel.LastSearchHitCount, 0);
                            Assert.AreEqual(panel.LastSearchHitCount, panel.DisplayedRowCount);

                            Assert.IsTrue(panel.TryClickToggleScope());
                            Assert.IsTrue(panel.FilterActiveMapOnly);

                            Assert.IsTrue(panel.TryClickClearSearch());
                            Assert.AreEqual(string.Empty, panel.SearchQuery);
                            Assert.AreEqual(0, panel.LastSearchHitCount);
                            Assert.AreEqual(0, panel.DisplayedRowCount);

                            // Return to RegionOverview and close cleanly.
                            Assert.IsTrue(panel.TrySelectTab(RpgWorld500xGazetteerHudTab.RegionOverview));
                            Assert.AreEqual(4, panel.DisplayedRowCount);
                            Assert.IsTrue(panel.TryClickClose());
                            Assert.IsFalse(panel.IsVisible);
                        }
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(regionRoot);
                    }
                }

                Assert.AreEqual(0, root.transform.childCount);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FourRegion_WorldGazetteerHudPanel_RespectsHiddenAncestorCanvasGroupLockAndPostDisposeGuard()
        {
            var root = new GameObject("World500xFourRegionGazetteerGuardRoot");
            RpgWorld500xWorldGazetteerHudPanel disposedPanel = null;
            try
            {
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.WhitewaterCrossing,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xWorldGazetteerHudPanel(root.transform, boot))
                {
                    disposedPanel = panel;

                    // Hidden panel rejects all Try* actions cleanly.
                    Assert.IsFalse(panel.IsVisible);
                    Assert.IsFalse(panel.TrySelectTab(RpgWorld500xGazetteerHudTab.SearchResults));
                    Assert.IsFalse(panel.TrySearch("港区航标巡查"));
                    Assert.IsFalse(panel.TryClickToggleScope());
                    Assert.IsFalse(panel.TryToggleSafestRoute());
                    Assert.IsFalse(panel.TryClickClearSearch());
                    Assert.IsFalse(panel.TryClickClose());

                    Assert.IsTrue(panel.Show());
                    CanvasGroup group = root.AddComponent<CanvasGroup>();
                    group.interactable = false;

                    // Ancestor CanvasGroup lock blocks every interactive action without mutating state.
                    Assert.IsFalse(panel.TrySelectTab(RpgWorld500xGazetteerHudTab.SearchResults));
                    Assert.IsFalse(panel.TrySearch("港区航标巡查"));
                    Assert.IsFalse(panel.TryClickToggleScope());
                    Assert.IsFalse(panel.TryToggleSafestRoute());
                    Assert.IsFalse(panel.TryClickClearSearch());
                    Assert.IsFalse(panel.TryClickClose());
                    Assert.AreEqual(RpgWorld500xGazetteerHudTab.RegionOverview, panel.ActiveTab);
                    Assert.IsFalse(panel.PreferSafestRoute);
                    Assert.IsTrue(panel.IsVisible);

                    // Unlocking restores normal interaction.
                    group.interactable = true;
                    Assert.IsTrue(panel.TryToggleSafestRoute());
                    Assert.IsTrue(panel.PreferSafestRoute);
                    Assert.IsTrue(panel.TryClickClose());
                    Assert.IsFalse(panel.IsVisible);
                }

                Assert.IsNotNull(disposedPanel);
                Assert.IsTrue(disposedPanel.IsDisposed);
                Assert.IsFalse(disposedPanel.IsVisible);
                Assert.IsFalse(disposedPanel.TrySelectTab(RpgWorld500xGazetteerHudTab.SearchResults));
                Assert.IsFalse(disposedPanel.TryToggleSafestRoute());
                Assert.IsFalse(disposedPanel.TryClickClose());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
