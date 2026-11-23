using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFieldRumorAndLoreHudPanelTests
    {
        [Test]
        public void RumorAndLoreHudPanel_TracksHeardRumorSwitchesToLoreTabAndDisposesWithZeroLeak()
        {
            var root = new GameObject("World500xFieldRumorAndLoreHudPanelTestRoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupPanel = new RpgWorld500xFieldRumorAndLoreHudPanel(root.transform, warmupBoot))
                {
                    warmupPanel.Show();
                    warmupPanel.TrySelectTab(RpgWorld500xRumorLoreTab.StorySpots);
                    warmupPanel.Hide();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xFieldRumorAndLoreHudPanel(root.transform, boot))
                {
                    Assert.IsTrue(panel.Show());
                    Assert.AreEqual(RpgWorld500xRumorLoreTab.FieldRumors, panel.ActiveTab);
                    Assert.AreEqual(6, panel.DisplayedRowCount);

                    IReadOnlyList<RpgFieldNpcDef> npcs =
                        RpgWorld500xLivingWorldCatalog.GetFieldNpcs(RpgWorldMapId.Soundwell);
                    RpgFieldNpcDef firstNpc = npcs[0];

                    // Unheard rumor cannot be tracked yet.
                    Assert.IsFalse(panel.TryClickTrackRumorTarget(firstNpc.Id));

                    // Talk to the first NPC via LivingWorldService to unlock rumor, then track from UI.
                    Assert.IsTrue(boot.Driver.Facade.ActivityDirector.LivingWorld.TryTalkToNearbyNpc(
                        new Vector2(firstNpc.Tile.x + 0.5f, firstNpc.Tile.y + 0.5f),
                        4f,
                        out _,
                        out _,
                        out _));

                    panel.Refresh();
                    Assert.IsTrue(panel.HeaderText.Contains("传闻 1/6"));
                    Assert.IsTrue(panel.TryClickTrackRumorTarget(firstNpc.Id));
                    Assert.AreEqual(firstNpc.RumorTargetMarkerId, boot.Driver.Facade.Session.Host.Tracker.TrackedMarkerId);
                    Assert.IsTrue(panel.StatusText.Contains("罗盘追踪"));

                    // Switch to StorySpots tab (4 spots per map).
                    Assert.IsTrue(panel.TrySelectTab(RpgWorld500xRumorLoreTab.StorySpots));
                    Assert.AreEqual(RpgWorld500xRumorLoreTab.StorySpots, panel.ActiveTab);
                    Assert.AreEqual(4, panel.DisplayedRowCount);

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
