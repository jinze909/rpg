using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xQuestJournalSummaryTests
    {
        [Test]
        public void QuestJournalPanel_ExposesBeaconNetworkSummaryAndLiveProgressCountersWithZeroLeak()
        {
            var root = new GameObject("World500xQuestJournalSummaryTestRoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupPanel = new RpgWorld500xQuestJournalPanel(
                    root.transform, new RpgMapExplorationTracker(RpgWorldMapId.MineCamp)))
                {
                    warmupPanel.Show();
                    warmupPanel.Hide();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                var tracker = new RpgMapExplorationTracker(RpgWorldMapId.MineCamp);
                using (var panel = new RpgWorld500xQuestJournalPanel(root.transform, tracker))
                {
                    Assert.IsTrue(panel.Show(RpgQuestJournalTab.ActiveQuests));
                    int initialUnlockedBeacons = panel.UnlockedBeaconCount;
                    int totalBeacons = panel.TotalBeaconCount;
                    int initialDiscoveredMarkers = panel.DiscoveredMarkerCount;
                    int totalQuests = panel.TotalQuestCount;

                    Assert.Greater(totalBeacons, 1);
                    Assert.Greater(totalQuests, 0);
                    Assert.AreEqual(totalBeacons, panel.LastBeaconNetworkSummary.TotalBeaconCount);
                    Assert.AreEqual(initialUnlockedBeacons, panel.LastBeaconNetworkSummary.DiscoveredBeaconCount);
                    StringAssert.Contains("【传送烽火】", panel.BeaconNetworkSummaryText);
                    Assert.AreEqual(0, panel.CompletedQuestCount);
                    Assert.AreEqual(totalQuests, panel.ActiveQuestCount);
                    Assert.AreEqual(totalQuests, panel.DisplayedRowCount);
                    Assert.AreEqual(tracker.TotalMarkerCount, panel.TotalMarkerCount);
                    StringAssert.Contains(
                        "快速传送灯标 " + initialUnlockedBeacons + "/" + totalBeacons,
                        panel.BodySummaryText);

                    // Complete a quest and verify active/completed counters and tabs update.
                    Assert.IsTrue(tracker.TryAdvanceQuest("q.mc.inspect-main-pump", 2));
                    panel.Refresh();
                    Assert.AreEqual(1, panel.CompletedQuestCount);
                    Assert.AreEqual(totalQuests - 1, panel.ActiveQuestCount);
                    Assert.AreEqual(totalQuests - 1, panel.DisplayedRowCount);
                    Assert.IsTrue(panel.SetTab(RpgQuestJournalTab.CompletedQuests));
                    Assert.AreEqual(1, panel.DisplayedRowCount);

                    // Discover a locked fast-travel beacon and verify summary counters update.
                    Assert.IsTrue(tracker.TryDiscoverMarker("mc.south-gate"));
                    panel.Refresh();
                    Assert.AreEqual(initialUnlockedBeacons + 1, panel.UnlockedBeaconCount);
                    Assert.AreEqual(initialUnlockedBeacons + 1, panel.LastBeaconNetworkSummary.DiscoveredBeaconCount);
                    Assert.AreEqual(initialDiscoveredMarkers + 1, panel.DiscoveredMarkerCount);
                    StringAssert.Contains(
                        "快速传送灯标 " + (initialUnlockedBeacons + 1) + "/" + totalBeacons,
                        panel.BodySummaryText);

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
