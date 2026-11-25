using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xWorldGazetteerHudPanelTests
    {
        [Test]
        public void WorldGazetteerHudPanel_ListsAllFourRegionsSearchesWithScopeToggleRespectsLockAndZeroLeak()
        {
            var root = new GameObject("World500xWorldGazetteerHudPanelTestRoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupPanel = new RpgWorld500xWorldGazetteerHudPanel(root.transform, warmupBoot))
                {
                    warmupPanel.Show();
                    warmupPanel.TrySearch("声井测绘初录");
                    warmupPanel.TryClickClose();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xWorldGazetteerHudPanel(root.transform, boot))
                {
                    // Hidden panel rejects Try* calls AND direct stale button callbacks cleanly.
                    Assert.IsFalse(panel.TrySelectTab(RpgWorld500xGazetteerHudTab.SearchResults));
                    Assert.IsFalse(panel.TrySearch("港区航标巡查"));
                    Assert.IsFalse(panel.TryClickToggleScope());
                    Assert.IsFalse(panel.TryClickClose());
                    Button searchTabBtn = root.transform
                        .Find("World500xWorldGazetteerHudPanel/GazetteerModalBox/Tab_SearchResults")
                        .GetComponent<Button>();
                    Button toggleScopeBtn = root.transform
                        .Find("World500xWorldGazetteerHudPanel/GazetteerModalBox/ToggleSearchScopeButton")
                        .GetComponent<Button>();
                    searchTabBtn.onClick.Invoke();
                    toggleScopeBtn.onClick.Invoke();
                    Assert.AreEqual(RpgWorld500xGazetteerHudTab.RegionOverview, panel.ActiveTab);
                    Assert.IsFalse(panel.FilterActiveMapOnly);

                    Assert.IsTrue(panel.Show());
                    Assert.AreEqual(RpgWorld500xGazetteerHudTab.RegionOverview, panel.ActiveTab);
                    Assert.AreEqual(4, panel.DisplayedRowCount);
                    Assert.IsTrue(panel.HeaderText.Contains("补给箱 42"));
                    Assert.IsTrue(panel.HeaderText.Contains("捷径 16"));
                    Assert.IsTrue(panel.HeaderText.Contains("悬赏 12"));

                    // Search for a RedSaltHarbor bounty across all 4 regions -> found!
                    Assert.IsTrue(panel.TrySearch("港区航标巡查", filterActiveMapOnly: false));
                    Assert.AreEqual(RpgWorld500xGazetteerHudTab.SearchResults, panel.ActiveTab);
                    Assert.GreaterOrEqual(panel.LastSearchHitCount, 1);
                    Assert.GreaterOrEqual(panel.DisplayedRowCount, 1);

                    // Toggle scope to ActiveMapOnly (Soundwell) -> RedSaltHarbor bounty filtered out.
                    Assert.IsTrue(panel.TryClickToggleScope());
                    Assert.IsTrue(panel.FilterActiveMapOnly);
                    Assert.AreEqual(0, panel.LastSearchHitCount);
                    Assert.AreEqual(0, panel.DisplayedRowCount);

                    // Parent CanvasGroup lock blocks tab switch, search, scope toggle, clear, close, AND direct stale callbacks.
                    CanvasGroup group = root.AddComponent<CanvasGroup>();
                    group.interactable = false;
                    Assert.IsFalse(panel.TrySelectTab(RpgWorld500xGazetteerHudTab.RegionOverview));
                    Assert.IsFalse(panel.TrySearch("声井测绘初录"));
                    Assert.IsFalse(panel.TryClickToggleScope());
                    Assert.IsFalse(panel.TryClickClearSearch());
                    Assert.IsFalse(panel.TryClickClose());
                    toggleScopeBtn.onClick.Invoke();
                    Assert.IsTrue(panel.FilterActiveMapOnly);
                    group.interactable = true;

                    Assert.IsTrue(panel.TryClickClearSearch());
                    Assert.AreEqual(string.Empty, panel.SearchQuery);
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
