using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionGazetteerCanvasGuardE2ETests
    {
        [Test]
        public void AllFourRegions_GazetteerRejectsHiddenDisabledAndLockedInputWithoutChangingSearchState()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            foreach (var spec in specs)
            {
                var root = new GameObject("GazetteerCanvasGuard_" + spec.MapId);
                try
                {
                    using (var boot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: spec.MapId, overrideTileSlices: new Sprite[] { null }))
                    using (var panel = new RpgWorld500xWorldGazetteerHudPanel(root.transform, boot))
                    {
                        Assert.IsTrue(panel.Show());
                        Assert.IsTrue(panel.TrySearch("港区航标巡查", false));
                        Assert.IsTrue(panel.TrySetPreferSafestRoute(true));
                        Assert.Greater(panel.LastSearchHitCount, 0);

                        var canvas = root.transform.Find("World500xWorldGazetteerHudPanel").GetComponent<Canvas>();
                        var modal = canvas.transform.Find("GazetteerModalBox");
                        string[] buttonNames = { "Tab_RegionOverview", "Tab_SearchResults",
                            "ToggleSearchScopeButton", "ClearSearchButton", "CloseGazetteerButton" };
                        var buttons = new Button[buttonNames.Length];
                        for (int i = 0; i < buttonNames.Length; i++)
                        {
                            buttons[i] = modal.Find(buttonNames[i]).GetComponent<Button>();
                            Assert.IsNotNull(buttons[i]);
                        }

                        int rows = panel.DisplayedRowCount;
                        int hits = panel.LastSearchHitCount;
                        string status = panel.StatusText;
                        string header = panel.HeaderText;

                        canvas.enabled = false;
                        AssertBlocked(panel, buttons, true, rows, hits, status, header);
                        canvas.enabled = true;

                        var group = root.AddComponent<CanvasGroup>();
                        group.interactable = false;
                        AssertBlocked(panel, buttons, true, rows, hits, status, header);
                        group.interactable = true;

                        Assert.IsTrue(panel.Hide());
                        AssertBlocked(panel, buttons, false, rows, hits, status, header);
                        Assert.IsTrue(panel.Show());

                        Assert.IsTrue(panel.TryClickToggleScope());
                        Assert.IsTrue(panel.FilterActiveMapOnly);
                        Assert.IsTrue(panel.TrySetPreferSafestRoute(false));
                        Assert.IsFalse(panel.PreferSafestRoute);
                        Assert.IsTrue(panel.TryClickClearSearch());
                        Assert.AreEqual(string.Empty, panel.SearchQuery);
                        Assert.AreEqual(0, panel.LastSearchHitCount);
                        Assert.IsTrue(panel.TrySelectTab(RpgWorld500xGazetteerHudTab.RegionOverview));
                        Assert.AreEqual(4, panel.DisplayedRowCount);
                        Assert.IsTrue(panel.TryClickClose());
                        Assert.IsFalse(panel.IsVisible);
                    }

                    Assert.AreEqual(0, root.transform.childCount, spec.MapId.ToString());
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        private static void AssertBlocked(RpgWorld500xWorldGazetteerHudPanel panel,
            Button[] buttons, bool visible, int rows, int hits, string status, string header)
        {
            Assert.IsFalse(panel.TrySelectTab(RpgWorld500xGazetteerHudTab.RegionOverview));
            Assert.IsFalse(panel.TrySearch("different", true));
            Assert.IsFalse(panel.TryToggleSafestRoute());
            Assert.IsFalse(panel.TrySetPreferSafestRoute(false));
            Assert.IsFalse(panel.TryClickToggleScope());
            Assert.IsFalse(panel.TryClickClearSearch());
            Assert.IsFalse(panel.TryClickClose());
            foreach (var button in buttons)
                button.onClick.Invoke();

            Assert.AreEqual(visible, panel.IsVisible);
            Assert.AreEqual(RpgWorld500xGazetteerHudTab.SearchResults, panel.ActiveTab);
            Assert.AreEqual("港区航标巡查", panel.SearchQuery);
            Assert.IsFalse(panel.FilterActiveMapOnly);
            Assert.IsTrue(panel.PreferSafestRoute);
            Assert.AreEqual(rows, panel.DisplayedRowCount);
            Assert.AreEqual(hits, panel.LastSearchHitCount);
            Assert.AreEqual(status, panel.StatusText);
            Assert.AreEqual(header, panel.HeaderText);
        }
    }
}
