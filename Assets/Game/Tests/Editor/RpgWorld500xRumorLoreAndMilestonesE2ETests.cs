using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xRumorLoreAndMilestonesE2ETests
    {
        [Test]
        public void FieldRumorLoreTrackerAndExpeditionMilestones_IntegrateEndToEndWithZeroLeak()
        {
            var root = new GameObject("World500xRumorLoreMilestonesE2ERoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupRumor = new RpgWorld500xFieldRumorAndLoreHudPanel(root.transform, warmupBoot))
                using (var warmupMilestones = new RpgWorld500xExpeditionMilestonesHudPanel(
                    root.transform, warmupBoot, _ => 0, (_, __) => { }))
                {
                    warmupRumor.Show();
                    warmupRumor.Hide();
                    warmupMilestones.Show();
                    warmupMilestones.Hide();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                var overlayStore = new Dictionary<string, int>(StringComparer.Ordinal);

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    Func<string, int> read = k =>
                        overlayStore.TryGetValue(k, out int ov)
                            ? ov
                            : (boot.Store.TryGetValue(k, out int bv) ? bv : 0);
                    Action<string, int> write = (k, v) => overlayStore[k] = v;

                    using (var rumorPanel = new RpgWorld500xFieldRumorAndLoreHudPanel(root.transform, boot))
                    using (var milestonesPanel = new RpgWorld500xExpeditionMilestonesHudPanel(
                        root.transform, boot, read, write))
                    {
                        RpgWorld500xGrandRuntimeFacade facade = boot.Driver.Facade;
                        RpgWorld500xLivingWorldService livingWorld = facade.ActivityDirector.LivingWorld;
                        Vector2 playerPos = facade.Session.Host.PlayerPosition;

                        // 1. FieldRumorAndLoreHudPanel exposes LivingWorld progress summary & nearest targets.
                        Assert.IsTrue(rumorPanel.Show());
                        Assert.AreEqual(6, rumorPanel.LastProgressSummary.TotalRumorCount);
                        Assert.AreEqual(4, rumorPanel.LastProgressSummary.TotalStorySpotCount);
                        StringAssert.Contains("【风土与传闻】", rumorPanel.ProgressSummaryText);
                        Assert.IsNotEmpty(rumorPanel.NearestUndiscoveredId);

                        Assert.IsTrue(livingWorld.TryFindNearestUnheardRumorNpc(
                            playerPos, out RpgFieldNpcDef nearestNpc, out _));
                        Assert.AreEqual(nearestNpc.Id, rumorPanel.NearestUndiscoveredId);

                        Assert.IsTrue(livingWorld.TryTalkToNearbyNpc(
                            new Vector2(nearestNpc.Tile.x + 0.5f, nearestNpc.Tile.y + 0.5f),
                            4f,
                            out _,
                            out _,
                            out _));
                        rumorPanel.Refresh();
                        Assert.AreEqual(1, rumorPanel.HeardRumorCount);
                        Assert.AreEqual(5, rumorPanel.LastProgressSummary.RemainingRumorCount);
                        Assert.AreNotEqual(nearestNpc.Id, rumorPanel.NearestUndiscoveredId);
                        Assert.IsTrue(rumorPanel.TryClickTrackRumorTarget(nearestNpc.Id));
                        Assert.AreEqual(nearestNpc.RumorTargetMarkerId, facade.Session.Host.Tracker.TrackedMarkerId);

                        // Switch to StorySpots tab, inspect nearest spot, verify UndiscoveredOnly filter & nearest switch.
                        Assert.IsTrue(rumorPanel.TrySelectTab(RpgWorld500xRumorLoreTab.StorySpots));
                        Assert.IsTrue(livingWorld.TryFindNearestUninspectedStorySpot(
                            playerPos, out RpgStorySpotDef nearestSpot, out _));
                        Assert.AreEqual(nearestSpot.Id, rumorPanel.NearestUndiscoveredId);

                        Assert.IsTrue(livingWorld.TryInspectNearbyStorySpot(
                            new Vector2(nearestSpot.Tile.x + 0.5f, nearestSpot.Tile.y + 0.5f),
                            4f,
                            out _,
                            out _));
                        rumorPanel.Refresh();
                        Assert.AreEqual(1, rumorPanel.InspectedStorySpotCount);
                        Assert.AreEqual(3, rumorPanel.LastProgressSummary.RemainingStorySpotCount);
                        Assert.AreNotEqual(nearestSpot.Id, rumorPanel.NearestUndiscoveredId);

                        Assert.IsTrue(rumorPanel.TrySetUndiscoveredOnly(true));
                        Assert.AreEqual(3, rumorPanel.DisplayedRowCount);
                        Assert.IsTrue(rumorPanel.Hide());

                        // 2. ExpeditionMilestonesHudPanel reflects live story-spot progress and claims eligible rewards.
                        Assert.IsTrue(milestonesPanel.Show());
                        Assert.AreEqual(8, milestonesPanel.DisplayedMilestoneRowCount);
                        Assert.AreEqual(0, milestonesPanel.ClaimableCount);
                        StringAssert.Contains("风物轶事 1/", milestonesPanel.SummaryText);
                        StringAssert.Contains("【远征里程碑】", milestonesPanel.MilestoneSummaryLineZh);

                        IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
                        for (int i = 0; i < specs.Count; i++)
                        {
                            write(RpgWorld500xRegionalReputationService.ReputationKey(specs[i].MapId),
                                RpgWorld500xRegionalReputationService.MaxReputationPoints);
                        }

                        milestonesPanel.Refresh();
                        Assert.AreEqual(1, milestonesPanel.ClaimableCount);
                        Assert.AreEqual(300, milestonesPanel.ClaimableSaltSilver);
                        Assert.IsTrue(milestonesPanel.TryClickClaimAllEligible());
                        Assert.AreEqual(1, milestonesPanel.ClaimedCount);
                        Assert.AreEqual(0, milestonesPanel.ClaimableCount);
                        Assert.AreEqual(0, milestonesPanel.ClaimableSaltSilver);
                        Assert.AreEqual(300, facade.BountyAndSupply.SaltSilverBalance);

                        Assert.IsTrue(milestonesPanel.TrySetUnclaimedOnly(true));
                        Assert.AreEqual(7, milestonesPanel.DisplayedMilestoneRowCount);
                        Assert.IsTrue(milestonesPanel.Hide());
                    }
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
