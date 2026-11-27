using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionDiscoveryBannerAndLedgerE2ETests
    {
        [Test]
        public void AllFourRegions_DiscoveryBannerQueueAndGlobalLedgerSummaryStayInSyncWithZeroLeak()
        {
            // Warm up one-time UGUI/font caches before steady-state leak baseline.
            var warmupRoot = new GameObject("World500xFourRegionBannerWarmupRoot");
            try
            {
                using (var warmupBanner = new RpgWorld500xDiscoveryBannerHud(warmupRoot.transform))
                {
                    warmupBanner.Enqueue("warmup", "sub", 1f);
                    warmupBanner.Step(2f);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(warmupRoot);
            }

            int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
            Action<string, int> write = (k, v) => store[k] = v;

            RpgWorld500xGlobalProgress baseline = RpgWorld500xLedgerSummary.Capture(read);
            Assert.AreEqual(4, baseline.PerMap.Count);
            Assert.AreEqual(0, baseline.DiscoveredMarkers);
            Assert.AreEqual(0, baseline.CompletedQuests);
            Assert.AreEqual(0, baseline.UnlockedBeacons);
            Assert.AreEqual(0, baseline.ClearedDangerZones);
            Assert.AreEqual(0f, baseline.CompletionPercent, 0.0001f);

            var root = new GameObject("World500xFourRegionBannerE2ERoot");
            try
            {
                using (var banner = new RpgWorld500xDiscoveryBannerHud(root.transform))
                {
                    Assert.IsFalse(banner.IsShowing);
                    Assert.IsFalse(banner.Enqueue("invalid", string.Empty, float.NaN));

                    int regionsProcessed = 0;
                    foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
                    {
                        var markers = RpgWorldLandmarkCatalog.GetMarkers(spec.MapId);
                        var quests = RpgWorldLandmarkCatalog.GetQuests(spec.MapId);
                        Assert.IsNotEmpty(markers, "Expected markers in " + spec.MapId);
                        Assert.IsNotEmpty(quests, "Expected landmark quests in " + spec.MapId);

                        RpgMapLandmarkMarker beacon = default;
                        bool foundBeacon = false;
                        for (int i = 0; i < markers.Count; i++)
                        {
                            if (markers[i].SupportsFastTravel)
                            {
                                beacon = markers[i];
                                foundBeacon = true;
                                break;
                            }
                        }

                        Assert.IsTrue(foundBeacon, "Expected fast-travel beacon in " + spec.MapId);
                        RpgLandmarkQuestDefinition quest = quests[0];

                        write(RpgMapExplorationTracker.MarkerDiscoveryKey(beacon.Id), 1);
                        write(RpgMapExplorationTracker.QuestProgressKey(quest.QuestId), quest.RequiredSteps);

                        Assert.IsTrue(banner.EnqueueLandmarkDiscovered(beacon));
                        Assert.IsTrue(banner.EnqueueQuestCompleted(new RpgLandmarkQuestRewardResult(
                            quest.QuestId,
                            quest.TitleZh,
                            RpgLandmarkQuestState.Completed,
                            true,
                            "阶段探索奖励已结算")));

                        Assert.IsTrue(banner.IsShowing);
                        Assert.AreEqual(1, banner.QueuedCount);
                        StringAssert.Contains(beacon.TitleZh, banner.CurrentHeadline);
                        StringAssert.Contains("快速传送灯标", banner.CurrentSubtitle);

                        // Non-finite step fails closed without clearing the active toast.
                        banner.Step(float.NaN);
                        Assert.IsTrue(banner.IsShowing);

                        banner.Step(4.0f);
                        Assert.IsTrue(banner.IsShowing);
                        Assert.AreEqual(0, banner.QueuedCount);
                        StringAssert.Contains(quest.TitleZh, banner.CurrentHeadline);

                        banner.Step(4.0f);
                        Assert.IsFalse(banner.IsShowing);

                        regionsProcessed++;
                        RpgWorld500xGlobalProgress progress = RpgWorld500xLedgerSummary.Capture(read);
                        Assert.AreEqual(regionsProcessed, progress.DiscoveredMarkers);
                        Assert.AreEqual(regionsProcessed, progress.UnlockedBeacons);
                        Assert.AreEqual(regionsProcessed, progress.CompletedQuests);
                        Assert.Greater(progress.CompletionPercent, 0f);
                    }

                    Assert.AreEqual(4, regionsProcessed);
                }

                RpgWorld500xGlobalProgress finalProgress = RpgWorld500xLedgerSummary.Capture(read);
                string formatted = RpgWorld500xLedgerSummary.FormatLedgerSummaryZh(finalProgress);
                StringAssert.Contains("地标 4/" + finalProgress.TotalMarkers, formatted);
                StringAssert.Contains("地标任务 4/" + finalProgress.TotalQuests, formatted);
                StringAssert.Contains("传送灯标 4/" + finalProgress.TotalBeacons, formatted);

                Assert.AreEqual(0, root.transform.childCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
        }
    }
}
