using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionCartographyPreviewPersistenceE2ETests
    {
        [Test]
        public void DistrictProgressTracker_AcrossAllFourRegions_PersistsFogBlocksAndPreviewsClaimsMilestonesIdempotently()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            for (int i = 0; i < specs.Count; i++)
            {
                var spec = specs[i];
                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                int writes = 0;
                Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
                Action<string, int> write = (k, v) =>
                {
                    store[k] = v;
                    writes++;
                };

                var tracker = new RpgWorld500xDistrictProgressTracker(spec, read, write);
                Assert.That(tracker.VisitedChunkCount, Is.EqualTo(0));

                tracker.RecordPlayerTile(new Vector2(200.5f, 300.5f), 1, out _);
                int visited = tracker.VisitedChunkCount;
                Assert.That(visited, Is.GreaterThan(0));
                Assert.IsTrue(tracker.IsChunkVisited(6, 9));
                Assert.IsFalse(tracker.IsChunkVisited(0, 0));

                int writesAfterFirstReveal = writes;
                tracker.RecordPlayerTile(new Vector2(200.5f, 300.5f), 1, out _);
                Assert.That(writes, Is.EqualTo(writesAfterFirstReveal));

                var restored = new RpgWorld500xDistrictProgressTracker(spec, read, write);
                Assert.That(restored.VisitedChunkCount, Is.EqualTo(visited));
                Assert.IsTrue(restored.IsChunkVisited(6, 9));
                Assert.IsFalse(restored.IsChunkVisited(0, 0));

                int storeBeforePreview = store.Count;
                var early = restored.BuildCartographyPreview(10);
                Assert.That(early.MapId, Is.EqualTo(spec.MapId));
                Assert.IsTrue(early.HasNextThreshold);
                Assert.That(early.NextThresholdPercent, Is.EqualTo(25));
                Assert.That(store.Count, Is.EqualTo(storeBeforePreview));

                var mid = restored.BuildCartographyPreview(60);
                Assert.IsTrue(mid.HasNextThreshold);
                Assert.That(mid.NextThresholdPercent, Is.EqualTo(100));
                StringAssert.Contains("可领 2 项", mid.SummaryZh);
                Assert.That(store.Count, Is.EqualTo(storeBeforePreview));

                var claimedMid = restored.ClaimEligibleMilestones(60);
                Assert.That(claimedMid.Count, Is.EqualTo(2));

                var claimedFinal = restored.ClaimEligibleMilestones(100);
                Assert.That(claimedFinal.Count, Is.EqualTo(1));

                var finished = restored.BuildCartographyPreview(100);
                Assert.IsFalse(finished.HasNextThreshold);
                StringAssert.Contains("本境堪舆里程碑已全部结清", finished.SummaryZh);
                Assert.IsEmpty(restored.ClaimEligibleMilestones(100));
            }
        }

        [Test]
        public void DistrictProgressTracker_AcrossAllFourRegions_KeepsRegionalFogAndMilestoneKeysIsolated()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            var sharedStore = new Dictionary<string, int>(StringComparer.Ordinal);
            Func<string, int> read = k => sharedStore.TryGetValue(k, out int v) ? v : 0;
            Action<string, int> write = (k, v) => sharedStore[k] = v;

            var firstTracker = new RpgWorld500xDistrictProgressTracker(specs[0], read, write);
            firstTracker.RecordPlayerTile(new Vector2(200.5f, 300.5f), 1, out _);
            Assert.That(firstTracker.ClaimEligibleMilestones(100).Count, Is.EqualTo(3));

            for (int i = 1; i < specs.Count; i++)
            {
                var otherTracker = new RpgWorld500xDistrictProgressTracker(specs[i], read, write);
                Assert.That(otherTracker.VisitedChunkCount, Is.EqualTo(0));
                Assert.IsFalse(otherTracker.IsChunkVisited(6, 9));
                var preview = otherTracker.BuildCartographyPreview(10);
                Assert.That(preview.MapId, Is.EqualTo(specs[i].MapId));
                Assert.IsTrue(preview.HasNextThreshold);
                Assert.That(preview.NextThresholdPercent, Is.EqualTo(25));
                Assert.That(otherTracker.ClaimEligibleMilestones(100).Count, Is.EqualTo(3));
            }
        }
    }
}
