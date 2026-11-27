using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionCacheAndShortcutHudE2ETests
    {
        [Test]
        public void FourRegion_CacheAndShortcutHudPanel_TabsNearestTargetAndIncompleteOnlyAcrossAllFourRegionsWithZeroSpriteLeak()
        {
            var root = new GameObject("World500xFourRegionCacheShortcutHudRoot");
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
                    using (var warmupPanel = new RpgWorld500xCacheAndShortcutHudPanel(root.transform, warmupBoot))
                    {
                        warmupPanel.Show();
                        warmupPanel.TrySelectTab(RpgWorld500xCacheShortcutTab.Shortcuts);
                        warmupPanel.Hide();
                    }
                }

                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                for (int i = 0; i < specs.Count; i++)
                {
                    RpgExpandedMapSpec spec = specs[i];
                    var regionRoot = new GameObject("RegionCacheShortcut_" + spec.MapId);
                    regionRoot.transform.SetParent(root.transform, false);
                    try
                    {
                        IReadOnlyList<RpgSupplyCacheDef> caches = RpgWorld500xContentCatalog.GetCaches(spec.MapId);
                        IReadOnlyList<RpgWorldShortcutDef> shortcuts = RpgWorld500xContentCatalog.GetShortcuts(spec.MapId);
                        Assert.Greater(caches.Count, 0, $"Expected hidden caches on {spec.MapId}.");
                        Assert.Greater(shortcuts.Count, 0, $"Expected shortcuts on {spec.MapId}.");

                        using (var boot = new RpgWorld500xSceneBootstrapper(
                            initialMapId: spec.MapId,
                            overrideTileSlices: new Sprite[] { null }))
                        using (var panel = new RpgWorld500xCacheAndShortcutHudPanel(regionRoot.transform, boot))
                        {
                            Assert.IsTrue(panel.Show(), $"CacheAndShortcutHudPanel failed to show on {spec.MapId}.");
                            Assert.IsTrue(panel.IsVisible);
                            Assert.AreEqual(RpgWorld500xCacheShortcutTab.HiddenCaches, panel.ActiveTab);
                            Assert.IsFalse(panel.IncompleteOnly);
                            Assert.AreEqual(caches.Count, panel.DisplayedRowCount);
                            Assert.AreEqual(caches.Count, panel.LastProgressSummary.TotalCacheCount);
                            Assert.AreEqual(shortcuts.Count, panel.LastProgressSummary.TotalShortcutCount);
                            Assert.AreEqual(0, panel.LootedCacheCount);
                            Assert.AreEqual(0, panel.UnlockedShortcutCount);
                            Assert.Greater(panel.RemainingCacheSaltSilver, 0);
                            Assert.AreEqual(panel.LastProgressSummary.RemainingCacheSaltSilver, panel.RemainingCacheSaltSilver);
                            StringAssert.Contains(spec.DisplayNameZh, panel.HeaderText);
                            StringAssert.Contains("【秘藏与捷径】", panel.ProgressSummaryText);
                            StringAssert.Contains("未领补给盐银", panel.SummaryText);
                            Assert.IsNotEmpty(panel.NearestIncompleteId);
                            Assert.GreaterOrEqual(panel.NearestIncompleteDistanceTiles, 0f);
                            Assert.IsFalse(string.IsNullOrEmpty(panel.NearestIncompleteLabelZh));

                            // Switch to Shortcuts tab and unlock first shortcut dynamically.
                            Assert.IsTrue(panel.TrySelectTab(RpgWorld500xCacheShortcutTab.Shortcuts));
                            Assert.AreEqual(RpgWorld500xCacheShortcutTab.Shortcuts, panel.ActiveTab);
                            Assert.AreEqual(shortcuts.Count, panel.DisplayedRowCount);
                            Assert.IsNotEmpty(panel.NearestIncompleteId);

                            var triggerPos = new Vector2(
                                shortcuts[0].TriggerTile.x + 0.5f,
                                shortcuts[0].TriggerTile.y + 0.5f);
                            Assert.IsTrue(boot.Driver.Facade.ActivityDirector.CachesAndShortcuts
                                .TryUnlockNearbyShortcut(triggerPos, 6f, out _));

                            panel.Refresh();
                            Assert.AreEqual(1, panel.UnlockedShortcutCount);
                            Assert.AreEqual(1, panel.LastProgressSummary.UnlockedShortcutCount);
                            Assert.AreEqual(shortcuts.Count - 1, panel.LastProgressSummary.LockedShortcutCount);
                            Assert.AreEqual(shortcuts.Count, panel.DisplayedRowCount);
                            StringAssert.Contains("捷径 1/" + shortcuts.Count, panel.HeaderText);

                            // IncompleteOnly hides unlocked shortcut (N -> N - 1) and restores cleanly.
                            Assert.IsTrue(panel.TrySetIncompleteOnly(true));
                            Assert.IsTrue(panel.IncompleteOnly);
                            Assert.AreEqual(shortcuts.Count - 1, panel.DisplayedRowCount);

                            Assert.IsTrue(panel.TrySetIncompleteOnly(false));
                            Assert.IsFalse(panel.IncompleteOnly);
                            Assert.AreEqual(shortcuts.Count, panel.DisplayedRowCount);

                            Assert.IsTrue(panel.Hide());
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
        public void FourRegion_CacheAndShortcutHudPanel_RespectsHiddenAncestorCanvasGroupLockAndPostDisposeGuard()
        {
            var root = new GameObject("World500xFourRegionCacheShortcutGuardRoot");
            RpgWorld500xCacheAndShortcutHudPanel disposedPanel = null;
            try
            {
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.RedSaltHarbor,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xCacheAndShortcutHudPanel(root.transform, boot))
                {
                    disposedPanel = panel;

                    // Hidden panel rejects tab switch and IncompleteOnly toggle.
                    Assert.IsFalse(panel.IsVisible);
                    Assert.IsFalse(panel.TrySelectTab(RpgWorld500xCacheShortcutTab.Shortcuts));
                    Assert.IsFalse(panel.TrySetIncompleteOnly(true));

                    Assert.IsTrue(panel.Show());
                    CanvasGroup group = root.AddComponent<CanvasGroup>();
                    group.interactable = false;

                    Assert.IsFalse(panel.TrySelectTab(RpgWorld500xCacheShortcutTab.Shortcuts));
                    Assert.IsFalse(panel.TrySetIncompleteOnly(true));
                    Assert.AreEqual(RpgWorld500xCacheShortcutTab.HiddenCaches, panel.ActiveTab);
                    Assert.IsFalse(panel.IncompleteOnly);

                    group.interactable = true;
                    Assert.IsTrue(panel.TrySelectTab(RpgWorld500xCacheShortcutTab.Shortcuts));
                    Assert.IsTrue(panel.TrySetIncompleteOnly(true));
                    Assert.AreEqual(RpgWorld500xCacheShortcutTab.Shortcuts, panel.ActiveTab);
                    Assert.IsTrue(panel.IncompleteOnly);
                    Assert.IsTrue(panel.Hide());
                    Assert.IsFalse(panel.IsVisible);
                }

                Assert.IsNotNull(disposedPanel);
                Assert.IsTrue(disposedPanel.IsDisposed);
                Assert.IsFalse(disposedPanel.IsVisible);
                Assert.IsFalse(disposedPanel.TrySelectTab(RpgWorld500xCacheShortcutTab.HiddenCaches));
                Assert.IsFalse(disposedPanel.TrySetIncompleteOnly(false));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
