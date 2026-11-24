using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xCartographyReputationHudPanelTests
    {
        [Test]
        public void CartographyReputationHudPanel_RejectsHiddenDisabledAndDisposedRegionActions()
        {
            var root = new GameObject("CartographyTabLifetimeRoot");
            try
            {
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xCartographyReputationHudPanel(root.transform, boot))
                {
                    var button = root.transform
                        .Find("World500xCartographyReputationHudPanel/CartographyReputationModalBox/RegionTabs/Tab_RedSaltHarbor")
                        .GetComponent<UnityEngine.UI.Button>();
                    var action = button.onClick;
                    string header = panel.HeaderText;
                    string summary = panel.SummaryText;
                    int rows = panel.DisplayedRowCount;
                    void AssertUnchanged()
                    {
                        Assert.AreEqual(RpgWorldMapId.Soundwell, panel.SelectedMapId);
                        Assert.AreEqual(header, panel.HeaderText);
                        Assert.AreEqual(summary, panel.SummaryText);
                        Assert.AreEqual(rows, panel.DisplayedRowCount);
                    }

                    action.Invoke();
                    Assert.IsFalse(panel.TrySelectRegion(RpgWorldMapId.RedSaltHarbor));
                    AssertUnchanged();

                    Assert.IsTrue(panel.Show());
                    header = panel.HeaderText;
                    summary = panel.SummaryText;
                    rows = panel.DisplayedRowCount;
                    button.interactable = false;
                    action.Invoke();
                    Assert.IsFalse(panel.TrySelectRegion(RpgWorldMapId.RedSaltHarbor));
                    AssertUnchanged();

                    button.interactable = true;
                    button.enabled = false;
                    action.Invoke();
                    Assert.IsFalse(panel.TrySelectRegion(RpgWorldMapId.RedSaltHarbor));
                    AssertUnchanged();
                    button.enabled = true;

                    root.SetActive(false);
                    action.Invoke();
                    Assert.IsFalse(panel.TrySelectRegion(RpgWorldMapId.RedSaltHarbor));
                    AssertUnchanged();
                    root.SetActive(true);

                    Assert.IsTrue(panel.Hide());
                    action.Invoke();
                    Assert.IsFalse(panel.TrySelectRegion(RpgWorldMapId.RedSaltHarbor));
                    AssertUnchanged();

                    Assert.IsTrue(panel.Show());
                    action.Invoke();
                    Assert.AreEqual(RpgWorldMapId.RedSaltHarbor, panel.SelectedMapId);
                    Assert.IsTrue(panel.HeaderText.Contains("赤盐港渔户与货帮"));
                    Assert.IsTrue(panel.TrySelectRegion(RpgWorldMapId.MineCamp));
                    Assert.AreEqual(RpgWorldMapId.MineCamp, panel.SelectedMapId);

                    panel.Dispose();
                    action.Invoke();
                    Assert.IsFalse(panel.TrySelectRegion(RpgWorldMapId.RedSaltHarbor));
                    Assert.AreEqual(RpgWorldMapId.MineCamp, panel.SelectedMapId);
                    Assert.AreEqual(0, root.transform.childCount);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CartographyReputationHudPanel_SwitchesAcrossAllFourRegionsAndDisposesWithZeroLeak()
        {
            var root = new GameObject("World500xCartographyReputationHudPanelTestRoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupPanel = new RpgWorld500xCartographyReputationHudPanel(root.transform, warmupBoot))
                {
                    warmupPanel.Show();
                    warmupPanel.TrySelectRegion(RpgWorldMapId.RedSaltHarbor);
                    warmupPanel.Hide();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xCartographyReputationHudPanel(root.transform, boot))
                {
                    Assert.IsFalse(panel.IsVisible);
                    Assert.IsTrue(panel.Show());
                    Assert.IsTrue(panel.IsVisible);
                    Assert.AreEqual(RpgWorldMapId.Soundwell, panel.SelectedMapId);
                    Assert.IsTrue(panel.HeaderText.Contains("当前所在"));
                    Assert.IsTrue(panel.SummaryText.Contains("综合堪舆度"));
                    // Soundwell has 4 districts + 3 cartography milestones = 7 rows.
                    Assert.AreEqual(7, panel.DisplayedRowCount);

                    // Switch tab to WhitewaterCrossing (3 districts + 3 milestones = 6 rows).
                    Assert.IsTrue(panel.TrySelectRegion(RpgWorldMapId.WhitewaterCrossing));
                    Assert.AreEqual(RpgWorldMapId.WhitewaterCrossing, panel.SelectedMapId);
                    Assert.IsTrue(panel.HeaderText.Contains("白水渡工兵防线"));
                    Assert.AreEqual(6, panel.DisplayedRowCount);

                    // Switch tab to RedSaltHarbor (4 districts + 3 milestones = 7 rows).
                    Assert.IsTrue(panel.TrySelectRegion(RpgWorldMapId.RedSaltHarbor));
                    Assert.IsTrue(panel.HeaderText.Contains("赤盐港渔户与货帮"));
                    Assert.AreEqual(7, panel.DisplayedRowCount);

                    // Switch tab to MineCamp (4 districts + 3 milestones = 7 rows).
                    Assert.IsTrue(panel.TrySelectRegion(RpgWorldMapId.MineCamp));
                    Assert.IsTrue(panel.HeaderText.Contains("赤盐矿营互助会"));
                    Assert.AreEqual(7, panel.DisplayedRowCount);

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
