using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorldLandmarksAndQuestsTests
    {
        [Test]
        public void AllFourMapsMeet500xAreaScaleAndContainValidUniqueLandmarksAndQuests()
        {
            Assert.That(RpgExpandedWorldScale.AllSpecs.Count, Is.EqualTo(4));

            var globalMarkerIds = new HashSet<string>(StringComparer.Ordinal);
            var globalQuestIds = new HashSet<string>(StringComparer.Ordinal);
            int totalMarkers = 0;
            int totalQuests = 0;

            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                Assert.That(spec.AreaMultiplier, Is.GreaterThanOrEqualTo(495f),
                    spec.DisplayNameEn + " must be ~500x legacy area.");
                Assert.That(spec.AreaMultiplier, Is.LessThanOrEqualTo(515f),
                    spec.DisplayNameEn + " must stay close to 500x legacy area.");
                Assert.That(spec.ExpandedWidth % RpgLargeMapChunkRenderer.ChunkSizeTiles, Is.Zero,
                    spec.DisplayNameEn + " width should align cleanly to 32-tile chunks.");
                Assert.That(spec.ExpandedHeight % RpgLargeMapChunkRenderer.ChunkSizeTiles, Is.Zero,
                    spec.DisplayNameEn + " height should align cleanly to 32-tile chunks.");

                IReadOnlyList<RpgMapLandmarkMarker> markers = RpgWorldLandmarkCatalog.GetMarkers(spec.MapId);
                IReadOnlyList<RpgLandmarkQuestDefinition> quests = RpgWorldLandmarkCatalog.GetQuests(spec.MapId);

                Assert.That(markers.Count, Is.GreaterThanOrEqualTo(16), spec.DisplayNameEn);
                Assert.That(quests.Count, Is.GreaterThanOrEqualTo(12), spec.DisplayNameEn);

                int fastTravelCount = 0;
                foreach (RpgMapLandmarkMarker marker in markers)
                {
                    Assert.That(globalMarkerIds.Add(marker.Id), Is.True, "Duplicate marker id: " + marker.Id);
                    Assert.That(marker.MapId, Is.EqualTo(spec.MapId), marker.Id);
                    Assert.That(string.IsNullOrWhiteSpace(marker.TitleZh), Is.False, marker.Id);
                    Assert.That(marker.Tile.x, Is.GreaterThanOrEqualTo(32), marker.Id);
                    Assert.That(marker.Tile.x, Is.LessThan(spec.ExpandedWidth - 32), marker.Id);
                    Assert.That(marker.Tile.y, Is.GreaterThanOrEqualTo(32), marker.Id);
                    Assert.That(marker.Tile.y, Is.LessThan(spec.ExpandedHeight - 32), marker.Id);
                    Assert.That(marker.DiscoveryRadiusTiles, Is.GreaterThan(0f), marker.Id);
                    if (marker.SupportsFastTravel)
                        fastTravelCount++;
                    if (!string.IsNullOrEmpty(marker.LinkedQuestId))
                    {
                        Assert.That(RpgWorldLandmarkCatalog.TryGetQuest(
                            spec.MapId, marker.LinkedQuestId, out _), Is.True, marker.Id);
                    }
                }

                Assert.That(fastTravelCount, Is.GreaterThanOrEqualTo(5),
                    spec.DisplayNameEn + " needs fast-travel beacons across 500x scale.");

                foreach (RpgLandmarkQuestDefinition quest in quests)
                {
                    Assert.That(globalQuestIds.Add(quest.QuestId), Is.True, "Duplicate quest id: " + quest.QuestId);
                    Assert.That(quest.MapId, Is.EqualTo(spec.MapId), quest.QuestId);
                    Assert.That(quest.RequiredSteps, Is.GreaterThanOrEqualTo(1), quest.QuestId);
                    Assert.That(RpgWorldLandmarkCatalog.TryGetMarker(
                        spec.MapId, quest.MarkerId, out RpgMapLandmarkMarker linked), Is.True, quest.QuestId);
                    Assert.That(quest.TargetTile, Is.EqualTo(linked.Tile), quest.QuestId);
                    Assert.That(string.IsNullOrWhiteSpace(quest.ObjectiveSummaryZh), Is.False, quest.QuestId);
                }

                totalMarkers += markers.Count;
                totalQuests += quests.Count;
            }

            Assert.That(totalMarkers, Is.GreaterThanOrEqualTo(70));
            Assert.That(totalQuests, Is.GreaterThanOrEqualTo(55));
        }

        [Test]
        public void CompletedTrackedQuestReleasesCompassAcrossAllFourRegions()
        {
            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                var tracker = new RpgMapExplorationTracker(spec.MapId);
                var quest = RpgWorldLandmarkCatalog.GetQuests(spec.MapId)[0];
                Vector2 atQuest = new Vector2(quest.TargetTile.x, quest.TargetTile.y);

                Assert.IsTrue(tracker.TrySetTrackedQuest(quest.QuestId));
                Assert.IsTrue(tracker.TryGetWayfindingReading(atQuest, out var before));
                Assert.AreEqual(quest.QuestId, before.TargetId);
                Assert.IsTrue(tracker.TryAdvanceQuest(quest.QuestId, quest.RequiredSteps));
                Assert.IsEmpty(tracker.TrackedQuestId);
                Assert.IsEmpty(tracker.TrackedMarkerId);
                Assert.IsFalse(tracker.TrySetTrackedQuest(quest.QuestId));
                Assert.IsTrue(tracker.TryGetWayfindingReading(atQuest, out var after));
                Assert.AreNotEqual(quest.MarkerId, after.TargetId);
                Assert.IsFalse(tracker.IsMarkerDiscovered(after.TargetId));

                // An intentional revisit to the old marker is still allowed.
                Assert.IsTrue(tracker.TrySetTrackedMarker(quest.MarkerId));
                Assert.IsEmpty(tracker.TrackedQuestId);
                Assert.IsTrue(tracker.TryGetWayfindingReading(atQuest, out var revisit));
                Assert.AreEqual(quest.MarkerId, revisit.TargetId);
            }
        }

        [Test]
        public void OversizedQuestAdvanceCompletesWithoutOverflowAndSurvivesReload()
        {
            var tracker = new RpgMapExplorationTracker(RpgWorldMapId.RedSaltHarbor);
            const string id = "q.rsh.compare-salt-prices";
            Assert.IsTrue(RpgWorldLandmarkCatalog.TryGetQuest(tracker.MapId, id, out var quest));
            Assert.Greater(quest.RequiredSteps, 1);
            Assert.IsTrue(tracker.TrySetTrackedQuest(id));
            Assert.IsTrue(tracker.TryAdvanceQuest(id, 1));
            Assert.IsFalse(tracker.TryAdvanceQuest(id, int.MinValue));
            Assert.AreEqual(1, tracker.GetQuestProgress(id));

            Assert.IsTrue(tracker.TryAdvanceQuest(id, int.MaxValue));
            Assert.AreEqual(quest.RequiredSteps, tracker.GetQuestProgress(id));
            Assert.AreEqual(RpgLandmarkQuestState.Completed, tracker.GetQuestState(id));
            Assert.IsEmpty(tracker.TrackedQuestId);
            Assert.IsFalse(tracker.TryAdvanceQuest(id, int.MaxValue));

            var store = new Dictionary<string, int>();
            Assert.IsTrue(tracker.TrySave((k, v) => store[k] = v));
            var restored = new RpgMapExplorationTracker(tracker.MapId);
            Assert.IsTrue(restored.TryLoad(k => store.TryGetValue(k, out int v) ? v : 0));
            Assert.AreEqual(quest.RequiredSteps, restored.GetQuestProgress(id));
            Assert.AreEqual(RpgLandmarkQuestState.Completed, restored.GetQuestState(id));
        }

        [Test]
        public void LoadingCompletedTrackedQuestReleasesOnlyFinishedObjective()
        {
            var tracker = new RpgMapExplorationTracker(RpgWorldMapId.RedSaltHarbor);
            var quests = RpgWorldLandmarkCatalog.GetQuests(tracker.MapId);
            var completed = quests[0];
            var unfinished = quests[1];
            var store = new Dictionary<string, int>
            {
                [RpgMapExplorationTracker.QuestProgressKey(completed.QuestId)] = completed.RequiredSteps
            };
            Func<string, int> read = key => store.TryGetValue(key, out int value) ? value : 0;

            Assert.IsTrue(tracker.TrySetTrackedQuest(completed.QuestId));
            Assert.IsTrue(tracker.TryLoad(read));
            Assert.IsEmpty(tracker.TrackedQuestId);
            Assert.IsEmpty(tracker.TrackedMarkerId);
            Assert.IsTrue(tracker.TrySetTrackedQuest(unfinished.QuestId));
            Assert.IsTrue(tracker.TryLoad(read));
            Assert.AreEqual(unfinished.QuestId, tracker.TrackedQuestId);
            Assert.AreEqual(unfinished.MarkerId, tracker.TrackedMarkerId);
        }

        [Test]
        public void ExplorationTrackerDiscoversLandmarksProgressesQuestsGatesFastTravelAndPersists()
        {
            var tracker = new RpgMapExplorationTracker(RpgWorldMapId.RedSaltHarbor);
            Assert.That(tracker.DiscoveredMarkerCount, Is.Zero);
            Assert.That(tracker.CompletedQuestCount, Is.Zero);

            // Fast travel is gated until the beacon marker is actually discovered.
            Assert.That(tracker.TryGetFastTravelDestination("rsh.outer-berth", out _), Is.False);
            Assert.That(tracker.DiscoverMarkersInRange(new Vector2(float.NaN, 540f)), Is.Zero);

            int found = tracker.DiscoverMarkersInRange(new Vector2(362f, 542f));
            Assert.That(found, Is.EqualTo(1));
            Assert.That(tracker.IsMarkerDiscovered("rsh.outer-berth"), Is.True);
            Assert.That(tracker.TryGetFastTravelDestination("rsh.outer-berth", out Vector2Int dest), Is.True);
            Assert.That(dest, Is.EqualTo(new Vector2Int(360, 540)));

            // Non-beacon markers never unlock fast travel even after discovery.
            Assert.That(tracker.TryDiscoverMarker("rsh.tide-gauge"), Is.True);
            Assert.That(tracker.TryGetFastTravelDestination("rsh.tide-gauge", out _), Is.False);

            // Quest tracking, wayfinding compass, and step completion.
            Assert.That(tracker.TrySetTrackedQuest("q.rsh.compare-salt-prices"), Is.True);
            Assert.That(tracker.TryGetWayfindingReading(new Vector2(360f, 540f), out RpgWayfindingReading reading), Is.True);
            Assert.That(reading.TargetId, Is.EqualTo("q.rsh.compare-salt-prices"));
            Assert.That(reading.DistanceTiles, Is.GreaterThan(1000f));
            Assert.That(reading.CompassDirectionZh, Is.EqualTo("东北"));

            Assert.That(tracker.TryAdvanceQuest("q.rsh.compare-salt-prices", 1), Is.True);
            Assert.That(tracker.GetQuestState("q.rsh.compare-salt-prices"), Is.EqualTo(RpgLandmarkQuestState.InProgress));
            Assert.That(tracker.TryAdvanceQuest("q.rsh.compare-salt-prices", 2), Is.True);
            Assert.That(tracker.GetQuestState("q.rsh.compare-salt-prices"), Is.EqualTo(RpgLandmarkQuestState.Completed));
            Assert.That(tracker.TryAdvanceQuest("q.rsh.compare-salt-prices", 1), Is.False);
            Assert.That(tracker.CompletedQuestCount, Is.EqualTo(1));
            Assert.That(tracker.IsMarkerDiscovered("rsh.salt-market"), Is.True);

            // Persist and reload cleanly.
            var store = new Dictionary<string, int>();
            Assert.That(tracker.TrySave((k, v) => store[k] = v), Is.True);

            var reloaded = new RpgMapExplorationTracker(RpgWorldMapId.RedSaltHarbor);
            Assert.That(reloaded.TryLoad(k => store.TryGetValue(k, out int v) ? v : 0), Is.True);
            Assert.That(reloaded.IsMarkerDiscovered("rsh.outer-berth"), Is.True);
            Assert.That(reloaded.IsMarkerDiscovered("rsh.tide-gauge"), Is.True);
            Assert.That(reloaded.IsMarkerDiscovered("rsh.salt-market"), Is.True);
            Assert.That(reloaded.GetQuestState("q.rsh.compare-salt-prices"), Is.EqualTo(RpgLandmarkQuestState.Completed));
            Assert.That(reloaded.TryGetWayfindingReading(new Vector2(float.PositiveInfinity, 0f), out _), Is.False);
        }
    }
}
