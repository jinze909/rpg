using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    [TestFixture]
    public sealed class RpgWorld500xFourRegionLandmarkQuestSavePersistenceE2ETests
    {
        [Test]
        public void AllFourRegions_LandmarkQuestCompletionAndSharedStorePersistence_IsolatesPerRegion()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count, "Expected exactly 4 regions in 500x world scale.");

            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
            Action<string, int> write = (k, v) => store[k] = v;

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var quests = RpgWorldLandmarkCatalog.GetQuests(spec.MapId);
                Assert.GreaterOrEqual(quests.Count, 2, $"Region {spec.MapId} should define at least 2 landmark quests.");

                RpgLandmarkQuestDefinition first = quests[0];
                RpgLandmarkQuestDefinition second = quests[1];
                var tracker = new RpgMapExplorationTracker(spec.MapId);

                Assert.IsTrue(tracker.TryAdvanceQuest(first.QuestId, int.MaxValue), $"Oversized advance should saturate and complete '{first.QuestId}' in {spec.MapId}.");
                Assert.AreEqual(first.RequiredSteps, tracker.GetQuestProgress(first.QuestId));
                Assert.AreEqual(RpgLandmarkQuestState.Completed, tracker.GetQuestState(first.QuestId));
                Assert.IsTrue(tracker.IsMarkerDiscovered(first.MarkerId));
                Assert.IsFalse(tracker.TryAdvanceQuest(first.QuestId, 1), $"Completed quest '{first.QuestId}' must reject further advances in {spec.MapId}.");
                Assert.IsFalse(tracker.TrySetTrackedQuest(first.QuestId), $"Completed quest '{first.QuestId}' must not be trackable in {spec.MapId}.");

                Assert.IsTrue(tracker.TrySetTrackedQuest(second.QuestId), $"Unfinished quest '{second.QuestId}' should be trackable in {spec.MapId}.");
                Assert.AreEqual(second.QuestId, tracker.TrackedQuestId);
                Assert.AreEqual(second.MarkerId, tracker.TrackedMarkerId);

                Assert.IsTrue(tracker.TrySave(write), $"TrySave should succeed for {spec.MapId}.");
            }

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var quests = RpgWorldLandmarkCatalog.GetQuests(spec.MapId);
                RpgLandmarkQuestDefinition first = quests[0];
                RpgLandmarkQuestDefinition second = quests[1];

                var restored = new RpgMapExplorationTracker(spec.MapId);
                Assert.IsTrue(restored.TryLoad(read), $"TryLoad should succeed for {spec.MapId}.");
                Assert.AreEqual(first.RequiredSteps, restored.GetQuestProgress(first.QuestId));
                Assert.AreEqual(RpgLandmarkQuestState.Completed, restored.GetQuestState(first.QuestId));
                Assert.IsTrue(restored.IsMarkerDiscovered(first.MarkerId));
                Assert.AreEqual(0, restored.GetQuestProgress(second.QuestId), $"Unfinished quest '{second.QuestId}' should remain at 0 progress after reload in {spec.MapId}.");
                Assert.AreNotEqual(RpgLandmarkQuestState.Completed, restored.GetQuestState(second.QuestId));
            }
        }

        [Test]
        public void AllFourRegions_NegativeOrZeroQuestAdvance_AndForeignQuestIds_AreRejected()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count, "Expected exactly 4 regions in 500x world scale.");

            var questIdsByRegion = new Dictionary<RpgWorldMapId, HashSet<string>>();
            foreach (RpgExpandedMapSpec spec in specs)
            {
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var quest in RpgWorldLandmarkCatalog.GetQuests(spec.MapId))
                {
                    ids.Add(quest.QuestId);
                }

                questIdsByRegion[spec.MapId] = ids;
            }

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var tracker = new RpgMapExplorationTracker(spec.MapId);
                var ownQuest = RpgWorldLandmarkCatalog.GetQuests(spec.MapId)[0];
                int initialDiscovered = tracker.DiscoveredMarkerCount;

                Assert.IsFalse(tracker.TryAdvanceQuest(ownQuest.QuestId, 0));
                Assert.IsFalse(tracker.TryAdvanceQuest(ownQuest.QuestId, -1));
                Assert.IsFalse(tracker.TryAdvanceQuest(ownQuest.QuestId, int.MinValue));
                Assert.AreEqual(0, tracker.GetQuestProgress(ownQuest.QuestId));

                Assert.IsFalse(tracker.TryAdvanceQuest(null, 1));
                Assert.IsFalse(tracker.TryAdvanceQuest(string.Empty, 1));
                Assert.IsFalse(tracker.TryAdvanceQuest("__unknown_quest_id__", 1));
                Assert.IsFalse(tracker.TrySetTrackedQuest("__unknown_quest_id__"));

                var ownIds = questIdsByRegion[spec.MapId];
                foreach (RpgExpandedMapSpec otherSpec in specs)
                {
                    if (otherSpec.MapId == spec.MapId)
                    {
                        continue;
                    }

                    foreach (string foreignId in questIdsByRegion[otherSpec.MapId])
                    {
                        if (ownIds.Contains(foreignId))
                        {
                            continue;
                        }

                        Assert.IsFalse(tracker.TryAdvanceQuest(foreignId, 1), $"Foreign quest '{foreignId}' from {otherSpec.MapId} must be rejected in {spec.MapId}.");
                        Assert.IsFalse(tracker.TrySetTrackedQuest(foreignId), $"Foreign quest '{foreignId}' from {otherSpec.MapId} must not be trackable in {spec.MapId}.");
                    }
                }

                Assert.AreEqual(initialDiscovered, tracker.DiscoveredMarkerCount, $"Rejected quest calls must not mutate discovered markers in {spec.MapId}.");
            }
        }
    }
}
