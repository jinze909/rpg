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
                    boot.Driver.Facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.Clear);
                    boot.Driver.Facade.Atmosphere.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.Clear);
                    boot.Driver.Facade.Atmosphere.SetWeather(RpgWorldMapId.MineCamp, RpgWorld500xWeatherKind.GaleStorm);

                    // Hidden panel rejects Try* calls AND direct stale button callbacks cleanly.
                    Assert.IsFalse(panel.TrySelectTab(RpgWorld500xGazetteerHudTab.SearchResults));
                    Assert.IsFalse(panel.TrySearch("港区航标巡查"));
                    Assert.IsFalse(panel.TryClickToggleScope());
                    Assert.IsFalse(panel.TryToggleSafestRoute());
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
                    Assert.AreEqual(4, panel.LastCards.Count);
                    Assert.IsFalse(panel.PreferSafestRoute);
                    Assert.IsTrue(panel.StatusText.Contains("路径：最短耗时"));
                    Assert.IsTrue(panel.HeaderText.Contains("补给箱 42"));
                    Assert.IsTrue(panel.HeaderText.Contains("捷径 16"));
                    Assert.IsTrue(panel.HeaderText.Contains("悬赏 12"));

                    // Toggling safest-route mode updates live status label cleanly.
                    Assert.IsTrue(panel.TryToggleSafestRoute());
                    Assert.IsTrue(panel.PreferSafestRoute);
                    Assert.IsTrue(panel.StatusText.Contains("路径：避险优先"));

                    // Live route-risk & provisioning badges render on MineCamp row and update after auto-provisioning.
                    Text mineRowBefore = root.transform
                        .Find("World500xWorldGazetteerHudPanel/GazetteerModalBox/GazetteerRows/GazetteerRegion_MineCamp/Label")
                        .GetComponent<Text>();
                    Assert.IsTrue(mineRowBefore.text.Contains("2跳险途"));
                    Assert.IsTrue(mineRowBefore.text.Contains("盐银不足"));

                    boot.Driver.Facade.BountyAndSupply.AddSaltSilver(300);
                    RpgWorld500xProvisioningReceipt receipt = RpgWorld500xCampProvisioningService.ExecuteAutoProvisioning(
                        boot, RpgWorldMapId.MineCamp, allowPartialIfUnderBudget: false);
                    Assert.IsTrue(receipt.FullyProvisioned);
                    panel.Refresh();

                    Text mineRowAfter = root.transform
                        .Find("World500xWorldGazetteerHudPanel/GazetteerModalBox/GazetteerRows/GazetteerRegion_MineCamp/Label")
                        .GetComponent<Text>();
                    Assert.IsTrue(mineRowAfter.text.Contains("行囊已齐备"));

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

                    // Parent CanvasGroup lock blocks tab switch, search, scope toggle, route-mode toggle, clear, close, AND direct stale callbacks.
                    CanvasGroup group = root.AddComponent<CanvasGroup>();
                    group.interactable = false;
                    Assert.IsFalse(panel.TrySelectTab(RpgWorld500xGazetteerHudTab.RegionOverview));
                    Assert.IsFalse(panel.TrySearch("声井测绘初录"));
                    Assert.IsFalse(panel.TryClickToggleScope());
                    Assert.IsFalse(panel.TryToggleSafestRoute());
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
