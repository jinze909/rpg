using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    /// <summary>
    /// Four-region E2E regression tests for RpgWorld500xQuestJournalPanel verifying
    /// live beacon-network summaries, Active/Completed tab row synchronization,
    /// beacon unlock counter updates, and zero native Texture2D/Sprite leaks across
    /// Soundwell, RedSaltHarbor, WhitewaterCrossing, and MineCamp.
    /// </summary>
    public sealed class RpgWorld500xFourRegionQuestJournalSummaryE2ETests
    {
        [Test]
        public void AllFourRegions_QuestJournalPanel_TracksBeaconNetworkSummaryTabsAndLiveCountersWithZeroLeak()
        {
            var root = new GameObject("FourRegionQuestJournalSummaryRoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupPanel = new RpgWorld500xQuestJournalPanel(
                    root.transform, new RpgMapExplorationTracker(RpgWorldMapId.Soundwell)))
                {
                    warmupPanel.Show();
                    warmupPanel.Hide();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
                Assert.AreEqual(4, specs.Count);

                for (int r = 0; r < specs.Count; r++)
                {
                    RpgExpandedMapSpec spec = specs[r];
                    var tracker = new RpgMapExplorationTracker(spec.MapId);
                    IReadOnlyList<RpgLandmarkQuestDefinition> quests = RpgWorldLandmarkCatalog.GetQuests(spec.MapId);
                    IReadOnlyList<RpgMapLandmarkMarker> markers = RpgWorldLandmarkCatalog.GetMarkers(spec.MapId);

                    using (var panel = new RpgWorld500xQuestJournalPanel(root.transform, tracker))
                    {
                        Assert.IsTrue(panel.Show(RpgQuestJournalTab.ActiveQuests),
                            $"Failed to show QuestJournalPanel on {spec.MapId}.");

                        int initialUnlockedBeacons = panel.UnlockedBeaconCount;
                        int totalBeacons = panel.TotalBeaconCount;
                        int totalQuests = panel.TotalQuestCount;

                        Assert.Greater(totalBeacons, 1, $"Expected multiple fast-travel beacons on {spec.MapId}.");
                        Assert.Greater(totalQuests, 0, $"Expected landmark quests on {spec.MapId}.");
                        Assert.AreEqual(totalBeacons, panel.LastBeaconNetworkSummary.TotalBeaconCount);
                        Assert.AreEqual(initialUnlockedBeacons, panel.LastBeaconNetworkSummary.DiscoveredBeaconCount);
                        StringAssert.Contains(spec.DisplayNameZh, panel.HeaderText);
                        StringAssert.Contains("【传送烽火】", panel.BeaconNetworkSummaryText);
                        StringAssert.Contains(
                            "快速传送灯标 " + initialUnlockedBeacons + "/" + totalBeacons,
                            panel.BodySummaryText);
                        Assert.AreEqual(0, panel.CompletedQuestCount);
                        Assert.AreEqual(totalQuests, panel.ActiveQuestCount);
                        Assert.AreEqual(totalQuests, panel.DisplayedRowCount);
                        Assert.AreEqual(tracker.TotalMarkerCount, panel.TotalMarkerCount);

                        // Complete first quest and verify active/completed counters and tabs update.
                        RpgLandmarkQuestDefinition firstQuest = quests[0];
                        Assert.IsTrue(tracker.TryAdvanceQuest(firstQuest.QuestId, firstQuest.RequiredSteps));
                        panel.Refresh();

                        Assert.AreEqual(1, panel.CompletedQuestCount, $"CompletedQuestCount mismatch on {spec.MapId}.");
                        Assert.AreEqual(totalQuests - 1, panel.ActiveQuestCount, $"ActiveQuestCount mismatch on {spec.MapId}.");
                        Assert.AreEqual(totalQuests - 1, panel.DisplayedRowCount, $"Active tab row count mismatch on {spec.MapId}.");
                        Assert.IsTrue(panel.SetTab(RpgQuestJournalTab.CompletedQuests));
                        Assert.AreEqual(1, panel.DisplayedRowCount, $"Completed tab row count mismatch on {spec.MapId}.");

                        // Discover one still-locked fast-travel beacon and verify summary counters increment.
                        int unlockedBeforeBeacon = panel.UnlockedBeaconCount;
                        int discoveredBeforeBeacon = panel.DiscoveredMarkerCount;
                        string lockedBeaconId = string.Empty;
                        for (int m = 0; m < markers.Count; m++)
                        {
                            if (markers[m].SupportsFastTravel && !tracker.IsMarkerDiscovered(markers[m].Id))
                            {
                                lockedBeaconId = markers[m].Id;
                                break;
                            }
                        }

                        Assert.IsFalse(string.IsNullOrEmpty(lockedBeaconId),
                            $"Expected at least one undiscovered fast-travel beacon on {spec.MapId}.");
                        Assert.IsTrue(tracker.TryDiscoverMarker(lockedBeaconId));
                        panel.Refresh();

                        Assert.AreEqual(unlockedBeforeBeacon + 1, panel.UnlockedBeaconCount);
                        Assert.AreEqual(unlockedBeforeBeacon + 1, panel.LastBeaconNetworkSummary.DiscoveredBeaconCount);
                        Assert.AreEqual(discoveredBeforeBeacon + 1, panel.DiscoveredMarkerCount);
                        StringAssert.Contains(
                            "快速传送灯标 " + (unlockedBeforeBeacon + 1) + "/" + totalBeacons,
                            panel.BodySummaryText);

                        Assert.IsTrue(panel.Hide());
                        Assert.IsFalse(panel.IsVisible);
                    }

                    Assert.AreEqual(0, root.transform.childCount,
                        $"Leaked UI child GameObjects after disposing QuestJournalPanel on {spec.MapId}.");
                }

                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore,
                    "Texture2D leak detected after four-region QuestJournalPanel sweep.");
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore,
                    "Sprite leak detected after four-region QuestJournalPanel sweep.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
