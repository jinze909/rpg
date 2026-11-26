using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionRumorAndLoreHudE2ETests
    {
        [Test]
        public void FieldRumorAndLoreHudPanel_TracksHeardRumorsAndInspectedStorySpotsAcrossAllFourRegionsWithZeroLeak()
        {
            var root = new GameObject("World500xFourRegionRumorLoreE2ERoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupPanel = new RpgWorld500xFieldRumorAndLoreHudPanel(root.transform, warmupBoot))
                {
                    warmupPanel.Show();
                    warmupPanel.Hide();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                RpgWorldMapId[] regions =
                {
                    RpgWorldMapId.Soundwell,
                    RpgWorldMapId.RedSaltHarbor,
                    RpgWorldMapId.WhitewaterCrossing,
                    RpgWorldMapId.MineCamp
                };

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: regions[0],
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xFieldRumorAndLoreHudPanel(root.transform, boot))
                {
                    Assert.IsTrue(panel.Show());

                    for (int i = 0; i < regions.Length; i++)
                    {
                        RpgWorldMapId mapId = regions[i];
                        if (i > 0)
                        {
                            Assert.IsTrue(boot.SwitchMap(mapId));
                            Assert.IsTrue(panel.TrySelectTab(RpgWorld500xRumorLoreTab.FieldRumors));
                        }

                        Assert.AreEqual(6, panel.TotalRumorCount);
                        Assert.AreEqual(4, panel.TotalStorySpotCount);
                        StringAssert.Contains("【风土与传闻】", panel.ProgressSummaryText);
                        Assert.IsNotEmpty(panel.NearestUndiscoveredId);

                        // Hear first NPC rumor in this region and track its target landmark.
                        IReadOnlyList<RpgFieldNpcDef> npcs = RpgWorld500xLivingWorldCatalog.GetFieldNpcs(mapId);
                        RpgFieldNpcDef firstNpc = npcs[0];
                        Assert.IsTrue(boot.Driver.Facade.ActivityDirector.LivingWorld.TryTalkToNearbyNpc(
                            new Vector2(firstNpc.Tile.x + 0.5f, firstNpc.Tile.y + 0.5f),
                            4f,
                            out _,
                            out _,
                            out _));

                        panel.Refresh();
                        Assert.AreEqual(1, panel.HeardRumorCount);
                        Assert.AreEqual(5, panel.LastProgressSummary.RemainingRumorCount);
                        Assert.IsTrue(panel.TryClickTrackRumorTarget(firstNpc.Id));
                        Assert.AreEqual(firstNpc.RumorTargetMarkerId, boot.Driver.Facade.Session.Host.Tracker.TrackedMarkerId);

                        // Switch to StorySpots tab, inspect nearest uninspected spot, verify counter & nearest update.
                        Assert.IsTrue(panel.TrySelectTab(RpgWorld500xRumorLoreTab.StorySpots));
                        Assert.AreEqual(4, panel.DisplayedRowCount);
                        string nearestSpotId = panel.NearestUndiscoveredId;
                        Assert.IsNotEmpty(nearestSpotId);

                        IReadOnlyList<RpgStorySpotDef> spots = RpgWorld500xLivingWorldCatalog.GetStorySpots(mapId);
                        RpgStorySpotDef targetSpot = spots[0];
                        for (int s = 0; s < spots.Count; s++)
                        {
                            if (spots[s].Id == nearestSpotId)
                            {
                                targetSpot = spots[s];
                                break;
                            }
                        }

                        Assert.IsTrue(boot.Driver.Facade.ActivityDirector.LivingWorld.TryInspectNearbyStorySpot(
                            new Vector2(targetSpot.Tile.x + 0.5f, targetSpot.Tile.y + 0.5f),
                            4f,
                            out _,
                            out _));

                        panel.Refresh();
                        Assert.AreEqual(1, panel.InspectedStorySpotCount);
                        Assert.AreEqual(3, panel.LastProgressSummary.RemainingStorySpotCount);
                        Assert.AreNotEqual(nearestSpotId, panel.NearestUndiscoveredId);
                    }

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
