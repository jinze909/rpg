using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    /// <summary>
    /// Four-region E2E regression tests for RpgMapExplorationTracker verifying
    /// overflow-safe quest step saturation, non-positive delta rejection, save/load
    /// round-trip persistence across all landmark quests, and selective release of
    /// completed quest tracking on load across Soundwell, RedSaltHarbor,
    /// WhitewaterCrossing, and MineCamp.
    /// </summary>
    public sealed class RpgWorld500xFourRegionQuestPersistenceAndOverflowE2ETests
    {
        [Test]
        public void AllFourRegions_OversizedQuestAdvanceSaturatesWithoutOverflowAndSurvivesSaveLoadRoundTrip()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            int totalQuestsVerified = 0;

            for (int r = 0; r < specs.Count; r++)
            {
                RpgExpandedMapSpec spec = specs[r];
                IReadOnlyList<RpgLandmarkQuestDefinition> quests = RpgWorldLandmarkCatalog.GetQuests(spec.MapId);
                Assert.Greater(quests.Count, 0, $"Region {spec.MapId} must have landmark quests.");

                var tracker = new RpgMapExplorationTracker(spec.MapId);

                for (int i = 0; i < quests.Count; i++)
                {
                    RpgLandmarkQuestDefinition quest = quests[i];
                    totalQuestsVerified++;

                    Assert.IsTrue(tracker.TrySetTrackedQuest(quest.QuestId),
                        $"Failed to track quest {quest.QuestId} on {spec.MapId}.");
                    Assert.IsFalse(tracker.TryAdvanceQuest(quest.QuestId, 0));
                    Assert.IsFalse(tracker.TryAdvanceQuest(quest.QuestId, int.MinValue));

                    if (quest.RequiredSteps > 1)
                    {
                        Assert.IsTrue(tracker.TryAdvanceQuest(quest.QuestId, 1));
                        Assert.AreEqual(1, tracker.GetQuestProgress(quest.QuestId));
                        Assert.IsFalse(tracker.TryAdvanceQuest(quest.QuestId, int.MinValue));
                        Assert.AreEqual(1, tracker.GetQuestProgress(quest.QuestId));
                    }

                    // Oversized increment must saturate to RequiredSteps without int overflow.
                    Assert.IsTrue(tracker.TryAdvanceQuest(quest.QuestId, int.MaxValue),
                        $"Oversized advance failed for {quest.QuestId} on {spec.MapId}.");
                    Assert.AreEqual(quest.RequiredSteps, tracker.GetQuestProgress(quest.QuestId));
                    Assert.AreEqual(RpgLandmarkQuestState.Completed, tracker.GetQuestState(quest.QuestId));
                    Assert.IsTrue(tracker.IsMarkerDiscovered(quest.MarkerId));
                    Assert.IsEmpty(tracker.TrackedQuestId);
                    Assert.IsEmpty(tracker.TrackedMarkerId);
                    Assert.IsFalse(tracker.TryAdvanceQuest(quest.QuestId, int.MaxValue));
                }

                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                Assert.IsTrue(tracker.TrySave((k, v) => store[k] = v));

                var restored = new RpgMapExplorationTracker(spec.MapId);
                Assert.IsTrue(restored.TryLoad(k => store.TryGetValue(k, out int v) ? v : 0));

                for (int i = 0; i < quests.Count; i++)
                {
                    RpgLandmarkQuestDefinition quest = quests[i];
                    Assert.AreEqual(quest.RequiredSteps, restored.GetQuestProgress(quest.QuestId),
                        $"Restored progress mismatch for {quest.QuestId} on {spec.MapId}.");
                    Assert.AreEqual(RpgLandmarkQuestState.Completed, restored.GetQuestState(quest.QuestId),
                        $"Restored state mismatch for {quest.QuestId} on {spec.MapId}.");
                    Assert.IsTrue(restored.IsMarkerDiscovered(quest.MarkerId),
                        $"Restored marker discovery missing for {quest.MarkerId} on {spec.MapId}.");
                }
            }

            Assert.GreaterOrEqual(totalQuestsVerified, 55);
        }

        [Test]
        public void AllFourRegions_TryLoadCompletedQuestReleasesFinishedTrackingWhilePreservingUnfinishedObjective()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            for (int r = 0; r < specs.Count; r++)
            {
                RpgExpandedMapSpec spec = specs[r];
                IReadOnlyList<RpgLandmarkQuestDefinition> quests = RpgWorldLandmarkCatalog.GetQuests(spec.MapId);
                Assert.GreaterOrEqual(quests.Count, 2, $"Region {spec.MapId} needs at least 2 quests.");

                RpgLandmarkQuestDefinition completed = quests[0];
                RpgLandmarkQuestDefinition unfinished = quests[1];

                var store = new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RpgMapExplorationTracker.QuestProgressKey(completed.QuestId)] = completed.RequiredSteps
                };
                Func<string, int> read = key => store.TryGetValue(key, out int value) ? value : 0;

                var tracker = new RpgMapExplorationTracker(spec.MapId);
                Assert.IsTrue(tracker.TrySetTrackedQuest(completed.QuestId));
                Assert.IsTrue(tracker.TryLoad(read));
                Assert.IsEmpty(tracker.TrackedQuestId, $"Completed tracked quest was not released on {spec.MapId}.");
                Assert.IsEmpty(tracker.TrackedMarkerId, $"Completed tracked marker was not released on {spec.MapId}.");
                Assert.IsFalse(tracker.TrySetTrackedQuest(completed.QuestId));

                // Revisit marker selection stays allowed without re-tracking the completed quest.
                Assert.IsTrue(tracker.TrySetTrackedMarker(completed.MarkerId));
                Assert.AreEqual(completed.MarkerId, tracker.TrackedMarkerId);
                Assert.IsEmpty(tracker.TrackedQuestId);

                // Unfinished tracked quest survives load intact.
                Assert.IsTrue(tracker.TrySetTrackedQuest(unfinished.QuestId));
                Assert.IsTrue(tracker.TryLoad(read));
                Assert.AreEqual(unfinished.QuestId, tracker.TrackedQuestId,
                    $"Unfinished tracked quest was dropped on load in {spec.MapId}.");
                Assert.AreEqual(unfinished.MarkerId, tracker.TrackedMarkerId);
            }
        }
    }
}
