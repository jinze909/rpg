using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xCacheAndShortcutHudPanelTests
    {
        [Test]
        public void CacheAndShortcutHudPanel_ListsAllCachesAndShortcutsTracksProgressAndZeroLeak()
        {
            var root = new GameObject("World500xCacheAndShortcutHudPanelTestRoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupPanel = new RpgWorld500xCacheAndShortcutHudPanel(root.transform, warmupBoot))
                {
                    warmupPanel.Show();
                    warmupPanel.TrySelectTab(RpgWorld500xCacheShortcutTab.Shortcuts);
                    warmupPanel.Hide();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xCacheAndShortcutHudPanel(root.transform, boot))
                {
                    Assert.IsTrue(panel.Show());
                    Assert.AreEqual(RpgWorld500xCacheShortcutTab.HiddenCaches, panel.ActiveTab);
                    // Soundwell has 12 hidden caches and 4 shortcuts.
                    Assert.AreEqual(12, panel.DisplayedRowCount);
                    Assert.AreEqual(0, panel.LootedCacheCount);
                    Assert.AreEqual(0, panel.UnlockedShortcutCount);

                    // Parent CanvasGroup lock prevents tab switch cleanly.
                    CanvasGroup group = root.AddComponent<CanvasGroup>();
                    group.interactable = false;
                    Assert.IsFalse(panel.TrySelectTab(RpgWorld500xCacheShortcutTab.Shortcuts));
                    Assert.AreEqual(RpgWorld500xCacheShortcutTab.HiddenCaches, panel.ActiveTab);
                    group.interactable = true;

                    // Switch to Shortcuts tab -> 4 shortcut rows.
                    Assert.IsTrue(panel.TrySelectTab(RpgWorld500xCacheShortcutTab.Shortcuts));
                    Assert.AreEqual(RpgWorld500xCacheShortcutTab.Shortcuts, panel.ActiveTab);
                    Assert.AreEqual(4, panel.DisplayedRowCount);

                    // Unlock shortcut sw_sc_entry_north at (380, 920) and verify counter updates.
                    Assert.IsTrue(boot.Driver.Facade.ActivityDirector.CachesAndShortcuts
                        .TryUnlockNearbyShortcut(new Vector2(380.5f, 920.5f), 6f, out _));
                    panel.Refresh();
                    Assert.AreEqual(1, panel.UnlockedShortcutCount);
                    Assert.IsTrue(panel.HeaderText.Contains("捷径 1/4"));

                    Assert.IsTrue(panel.Hide());
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
