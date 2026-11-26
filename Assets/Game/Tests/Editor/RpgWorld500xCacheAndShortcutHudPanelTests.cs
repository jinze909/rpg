using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

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
                    // Hidden panel rejects tab switch, incomplete filter, AND direct stale tab callbacks cleanly.
                    Assert.IsFalse(panel.TrySelectTab(RpgWorld500xCacheShortcutTab.Shortcuts));
                    Assert.IsFalse(panel.TrySetIncompleteOnly(true));
                    Button shortcutsTabBtn = root.transform
                        .Find("World500xCacheAndShortcutHudPanel/CacheShortcutModalBox/Tab_Shortcuts")
                        .GetComponent<Button>();
                    shortcutsTabBtn.onClick.Invoke();
                    Assert.AreEqual(RpgWorld500xCacheShortcutTab.HiddenCaches, panel.ActiveTab);

                    Assert.IsTrue(panel.Show());
                    Assert.AreEqual(RpgWorld500xCacheShortcutTab.HiddenCaches, panel.ActiveTab);
                    Assert.IsFalse(panel.IncompleteOnly);
                    // Soundwell has 12 hidden caches and 4 shortcuts.
                    Assert.AreEqual(12, panel.DisplayedRowCount);
                    Assert.AreEqual(0, panel.LootedCacheCount);
                    Assert.AreEqual(0, panel.UnlockedShortcutCount);
                    Assert.Greater(panel.RemainingCacheSaltSilver, 0);
                    Assert.IsFalse(string.IsNullOrEmpty(panel.NearestIncompleteLabelZh));
                    StringAssert.Contains("未领补给盐银", panel.SummaryText);

                    // Parent CanvasGroup lock prevents tab switch, incomplete filter, AND direct stale tab callbacks cleanly.
                    CanvasGroup group = root.AddComponent<CanvasGroup>();
                    group.interactable = false;
                    Assert.IsFalse(panel.TrySelectTab(RpgWorld500xCacheShortcutTab.Shortcuts));
                    Assert.IsFalse(panel.TrySetIncompleteOnly(true));
                    shortcutsTabBtn.onClick.Invoke();
                    Assert.AreEqual(RpgWorld500xCacheShortcutTab.HiddenCaches, panel.ActiveTab);
                    Assert.IsFalse(panel.IncompleteOnly);
                    group.interactable = true;

                    // Switch to Shortcuts tab -> 4 shortcut rows.
                    Assert.IsTrue(panel.TrySelectTab(RpgWorld500xCacheShortcutTab.Shortcuts));
                    Assert.AreEqual(RpgWorld500xCacheShortcutTab.Shortcuts, panel.ActiveTab);
                    Assert.AreEqual(4, panel.DisplayedRowCount);

                    // Unlock shortcut sw_sc_entry_north at (380, 920) and verify counter & IncompleteOnly filter updates.
                    Assert.IsTrue(boot.Driver.Facade.ActivityDirector.CachesAndShortcuts
                        .TryUnlockNearbyShortcut(new Vector2(380.5f, 920.5f), 6f, out _));
                    panel.Refresh();
                    Assert.AreEqual(1, panel.UnlockedShortcutCount);
                    Assert.AreEqual(4, panel.DisplayedRowCount);
                    Assert.IsTrue(panel.HeaderText.Contains("捷径 1/4"));

                    Assert.IsTrue(panel.TrySetIncompleteOnly(true));
                    Assert.IsTrue(panel.IncompleteOnly);
                    Assert.AreEqual(3, panel.DisplayedRowCount);
                    Assert.IsFalse(string.IsNullOrEmpty(panel.NearestIncompleteLabelZh));

                    Assert.IsTrue(panel.TrySetIncompleteOnly(false));
                    Assert.AreEqual(4, panel.DisplayedRowCount);

                    Assert.IsTrue(panel.Hide());
                    Assert.IsFalse(panel.IsVisible);
                    Assert.IsFalse(panel.TrySelectTab(RpgWorld500xCacheShortcutTab.HiddenCaches));
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