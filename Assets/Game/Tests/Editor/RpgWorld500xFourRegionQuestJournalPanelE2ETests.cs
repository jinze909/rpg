using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionQuestJournalPanelE2ETests
    {
        [Test]
        public void FourRegion_QuestJournalPanel_TabsTrackQuestAndBeaconFastTravelAcrossAllFourRegionsWithZeroSpriteLeak()
        {
            var root = new GameObject("World500xFourRegionQuestJournalRoot");
            try
            {
                IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
                Assert.AreEqual(4, specs.Count);

                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                for (int w = 0; w < specs.Count; w++)
                {
                    var warmupTracker = new RpgMapExplorationTracker(specs[w].MapId);
                    using (var warmupPanel = new RpgWorld500xQuestJournalPanel(root.transform, warmupTracker))
                    {
                        warmupPanel.Show();
                        warmupPanel.SetTab(RpgQuestJournalTab.FastTravelBeacons);
                        warmupPanel.Hide();
                    }
                }

                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                for (int i = 0; i < specs.Count; i++)
                {
                    RpgExpandedMapSpec spec = specs[i];
                    var regionRoot = new GameObject("RegionQuestJournal_" + spec.MapId);
                    regionRoot.transform.SetParent(root.transform, false);
                    try
                    {
                        var tracker = new RpgMapExplorationTracker(spec.MapId);
                        string lastTrackedQuestId = null;
                        int trackCallbacks = 0;
                        string lastTravelMarkerId = null;
                        Vector2Int lastTravelTile = default;
                        int travelCallbacks = 0;

                        using (var panel = new RpgWorld500xQuestJournalPanel(
                            regionRoot.transform,
                            tracker,
                            onFastTravelRequested: (id, tile) =>
                            {
                                lastTravelMarkerId = id;
                                lastTravelTile = tile;
                                travelCallbacks++;
                            },
                            onTrackedQuestChanged: id =>
                            {
                                lastTrackedQuestId = id;
                                trackCallbacks++;
                            }))
                        {
                            Assert.IsTrue(panel.Show(), $"QuestJournalPanel failed to show on {spec.MapId}.");
                            Assert.IsTrue(panel.IsVisible);
                            Assert.AreEqual(RpgQuestJournalTab.ActiveQuests, panel.CurrentTab);
                            Assert.Greater(tracker.TotalQuestCount, 0);
                            Assert.AreEqual(tracker.TotalQuestCount, panel.DisplayedRowCount);

                            ScrollRect scroll = panel.RootObject.GetComponentInChildren<ScrollRect>(true);
                            Assert.IsNotNull(scroll);
                            Assert.IsTrue(scroll.vertical);
                            Assert.IsFalse(scroll.horizontal);
                            Assert.AreEqual(tracker.TotalQuestCount, scroll.content.childCount);

                            // Extract first quest ID from spawned row and click Track.
                            string rowName = scroll.content.GetChild(0).name;
                            const string questPrefix = "QuestRow_";
                            StringAssert.StartsWith(questPrefix, rowName);
                            string firstQuestId = rowName.Substring(questPrefix.Length);

                            Assert.IsTrue(panel.TryClickTrackQuest(firstQuestId));
                            Assert.AreEqual(1, trackCallbacks);
                            Assert.AreEqual(firstQuestId, lastTrackedQuestId);
                            Assert.AreEqual(firstQuestId, tracker.TrackedQuestId);

                            // Switch to CompletedQuests tab via UGUI tab button.
                            Button completedTab = panel.RootObject.transform
                                .Find("JournalPanel/TabBar/Tab_CompletedQuests")
                                .GetComponent<Button>();
                            completedTab.onClick.Invoke();
                            Assert.AreEqual(RpgQuestJournalTab.CompletedQuests, panel.CurrentTab);
                            Assert.AreEqual(0, panel.DisplayedRowCount);

                            // Switch to FastTravelBeacons tab via UGUI tab button.
                            Button beaconsTab = panel.RootObject.transform
                                .Find("JournalPanel/TabBar/Tab_FastTravelBeacons")
                                .GetComponent<Button>();
                            beaconsTab.onClick.Invoke();
                            Assert.AreEqual(RpgQuestJournalTab.FastTravelBeacons, panel.CurrentTab);
                            Assert.Greater(panel.DisplayedRowCount, 0);

                            // Find first FastTravelBeacon marker and verify locked -> discovered fast travel.
                            IReadOnlyList<RpgMapLandmarkMarker> markers = RpgWorldLandmarkCatalog.GetMarkers(spec.MapId);
                            RpgMapLandmarkMarker beacon = default;
                            bool foundBeacon = false;
                            for (int m = 0; m < markers.Count; m++)
                            {
                                if (markers[m].Category == RpgMapMarkerCategory.FastTravelBeacon)
                                {
                                    beacon = markers[m];
                                    foundBeacon = true;
                                    break;
                                }
                            }

                            Assert.IsTrue(foundBeacon, $"Expected a FastTravelBeacon marker on {spec.MapId}.");
                            if (!tracker.IsMarkerDiscovered(beacon.Id))
                            {
                                Assert.IsFalse(panel.TryClickFastTravel(beacon.Id));
                                Assert.AreEqual(0, travelCallbacks);
                                Assert.IsTrue(tracker.TryDiscoverMarker(beacon.Id));
                                panel.Refresh();
                            }

                            Assert.IsTrue(panel.TryClickFastTravel(beacon.Id));
                            Assert.AreEqual(1, travelCallbacks);
                            Assert.AreEqual(beacon.Id, lastTravelMarkerId);
                            Assert.AreEqual(beacon.Tile, lastTravelTile);

                            Assert.IsTrue(panel.Hide());
                            Assert.IsFalse(panel.IsVisible);
                        }
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(regionRoot);
                    }
                }

                Assert.AreEqual(0, root.transform.childCount);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FourRegion_QuestJournalPanel_RejectsHiddenAncestorCanvasGroupLockAndPostDisposeActions()
        {
            var root = new GameObject("World500xFourRegionQuestJournalGuardRoot");
            RpgWorld500xQuestJournalPanel disposedPanel = null;
            try
            {
                var tracker = new RpgMapExplorationTracker(RpgWorldMapId.WhitewaterCrossing);
                int trackCallbacks = 0;

                using (var panel = new RpgWorld500xQuestJournalPanel(
                    root.transform,
                    tracker,
                    onTrackedQuestChanged: _ => trackCallbacks++))
                {
                    disposedPanel = panel;

                    ScrollRect scroll = panel.RootObject.GetComponentInChildren<ScrollRect>(true);
                    string firstQuestId = scroll.content.GetChild(0).name.Substring("QuestRow_".Length);
                    Button beaconsTab = panel.RootObject.transform
                        .Find("JournalPanel/TabBar/Tab_FastTravelBeacons")
                        .GetComponent<Button>();

                    // Hidden panel rejects Track and UGUI tab clicks.
                    Assert.IsFalse(panel.IsVisible);
                    Assert.IsFalse(panel.TryClickTrackQuest(firstQuestId));
                    beaconsTab.onClick.Invoke();
                    Assert.AreEqual(RpgQuestJournalTab.ActiveQuests, panel.CurrentTab);
                    Assert.AreEqual(0, trackCallbacks);

                    // Ancestor CanvasGroup lock blocks Track and UGUI tab clicks.
                    Assert.IsTrue(panel.Show());
                    CanvasGroup group = root.AddComponent<CanvasGroup>();
                    group.interactable = false;
                    Assert.IsFalse(panel.TryClickTrackQuest(firstQuestId));
                    beaconsTab.onClick.Invoke();
                    Assert.AreEqual(RpgQuestJournalTab.ActiveQuests, panel.CurrentTab);
                    Assert.AreEqual(0, trackCallbacks);

                    // Unlocking restores normal interaction.
                    group.interactable = true;
                    Assert.IsTrue(panel.TryClickTrackQuest(firstQuestId));
                    Assert.AreEqual(1, trackCallbacks);
                    beaconsTab.onClick.Invoke();
                    Assert.AreEqual(RpgQuestJournalTab.FastTravelBeacons, panel.CurrentTab);
                }

                Assert.IsNotNull(disposedPanel);
                Assert.IsTrue(disposedPanel.IsDisposed);
                Assert.IsFalse(disposedPanel.IsVisible);
                Assert.IsFalse(disposedPanel.SetTab(RpgQuestJournalTab.ActiveQuests));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
