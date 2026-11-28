using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xDistrictProgressTrackerTests
    {
        [Test]
        public void All15Districts_PartitionEvery500xMapWithoutOverlapOrGaps()
        {
            var globalIds = new HashSet<string>(StringComparer.Ordinal);
            int totalDistricts = 0;

            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                IReadOnlyList<RpgWorld500xDistrictDef> districts = RpgWorld500xDistrictCatalog.GetDistricts(spec.MapId);
                Assert.GreaterOrEqual(districts.Count, 3, spec.DisplayNameEn);
                totalDistricts += districts.Count;

                long coveredArea = 0;
                for (int i = 0; i < districts.Count; i++)
                {
                    RpgWorld500xDistrictDef a = districts[i];
                    Assert.IsTrue(globalIds.Add(a.Id), a.Id);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(a.DisplayNameZh), a.Id);
                    Assert.GreaterOrEqual(a.BoundsTiles.xMin, 0, a.Id);
                    Assert.GreaterOrEqual(a.BoundsTiles.yMin, 0, a.Id);
                    Assert.LessOrEqual(a.BoundsTiles.xMax, spec.ExpandedWidth, a.Id);
                    Assert.LessOrEqual(a.BoundsTiles.yMax, spec.ExpandedHeight, a.Id);
                    coveredArea += (long)a.BoundsTiles.width * a.BoundsTiles.height;

                    for (int j = i + 1; j < districts.Count; j++)
                    {
                        Assert.IsFalse(a.BoundsTiles.Overlaps(districts[j].BoundsTiles),
                            $"District overlap on {spec.DisplayNameEn}: {a.Id} vs {districts[j].Id}");
                    }
                }

                long expectedArea = (long)spec.ExpandedWidth * spec.ExpandedHeight;
                Assert.AreEqual(expectedArea, coveredArea,
                    $"Districts on {spec.DisplayNameEn} must cover 100% of map area.");
            }

            Assert.AreEqual(15, totalDistricts);
        }

        [Test]
        public void FogOfWarDistrictTransitionAndMilestonesWorkIdempotently()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
            Action<string, int> write = (k, v) => store[k] = v;

            RpgWorld500xMapLayout soundwell = RpgWorld500xMapLayout.Build(RpgWorldMapId.Soundwell);
            var exploration = new RpgMapExplorationTracker(RpgWorldMapId.Soundwell);
            var cachesCtrl = new RpgWorld500xCachesAndShortcutsController(soundwell, read, write);

            int rewardTotal = 0;
            var tracker = new RpgWorld500xDistrictProgressTracker(
                soundwell.Spec, read, write, m => rewardTotal += m.SaltSilverReward);

            // Entering SW district reveals chunks and fires district entry once
            Assert.IsTrue(tracker.RecordPlayerTile(new Vector2(200.5f, 300.5f), 1, out RpgWorld500xDistrictDef first));
            Assert.AreEqual("sw_dist_sw", first.Id);
            Assert.IsTrue(tracker.IsChunkVisited(6, 9));
            Assert.Greater(tracker.VisitedChunkCount, 0);

            // Staying in same district does not re-fire transition
            Assert.IsFalse(tracker.RecordPlayerTile(new Vector2(220.5f, 300.5f), 1, out _));

            // Crossing into NE district fires transition
            Assert.IsTrue(tracker.RecordPlayerTile(new Vector2(1800.5f, 900.5f), 1, out RpgWorld500xDistrictDef second));
            Assert.AreEqual("sw_dist_ne", second.Id);

            // Claim 25% & 50% milestones once; duplicate call grants nothing
            List<RpgWorld500xCartographyMilestone> firstClaim = tracker.ClaimEligibleMilestones(55);
            Assert.AreEqual(2, firstClaim.Count);
            Assert.AreEqual(135, rewardTotal);
            for (int i = 0; i < firstClaim.Count; i++)
            {
                Assert.IsTrue(firstClaim[i].MakinomKey.StartsWith("rpg.return.world.cartography.", StringComparison.Ordinal));
            }

            List<RpgWorld500xCartographyMilestone> secondClaim = tracker.ClaimEligibleMilestones(55);
            Assert.AreEqual(0, secondClaim.Count);
            Assert.AreEqual(135, rewardTotal);

            // Summary computation returns valid bounded ratios
            RpgWorld500xMapCompletionSummary summary = tracker.ComputeCompletionSummary(soundwell, exploration, cachesCtrl);
            Assert.AreEqual(soundwell.Markers.Count, summary.TotalMarkers);
            Assert.AreEqual(12, summary.TotalCaches);
            Assert.AreEqual(4, summary.TotalShortcuts);
            Assert.AreEqual(4, summary.TotalStorySpots);
            Assert.GreaterOrEqual(summary.OverallCompletionPercent, 0);
            Assert.LessOrEqual(summary.OverallCompletionPercent, 100);
        }

        [Test]
        public void CartographyClaimRequiresPersistedFlagAndRetriesRejectedWritesWithoutDuplicateReward()
        {
            foreach (var spec in RpgExpandedWorldScale.AllSpecs)
            {
                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
                var milestones = RpgWorld500xDistrictCatalog.GetMilestones(spec.MapId);
                int silver = 0;
                int callbacks = 0;
                Action<RpgWorld500xCartographyMilestone> reward = m =>
                {
                    silver += m.SaltSilverReward;
                    callbacks++;
                };

                var readOnly = new RpgWorld500xDistrictProgressTracker(spec, read, onMilestoneClaimed: reward);
                Assert.IsEmpty(readOnly.ClaimEligibleMilestones(100));
                Assert.AreEqual(0, store.Count);
                Assert.AreEqual(0, silver);
                Assert.AreEqual(0, callbacks);

                bool rejectFirst = true;
                var tracker = new RpgWorld500xDistrictProgressTracker(spec, read,
                    (k, v) =>
                    {
                        if (!rejectFirst || k != milestones[0].MakinomKey)
                            store[k] = v;
                    }, reward);

                var first = tracker.ClaimEligibleMilestones(55);
                Assert.AreEqual(1, first.Count);
                Assert.AreEqual(milestones[1].MakinomKey, first[0].MakinomKey);
                Assert.AreEqual(90, silver);
                Assert.AreEqual(1, callbacks);
                Assert.IsFalse(store.ContainsKey(milestones[0].MakinomKey));
                Assert.AreEqual(1, tracker.BuildCartographyPreview(55).ClaimableCount);
                Assert.IsEmpty(tracker.ClaimEligibleMilestones(55));
                Assert.AreEqual(90, silver);

                rejectFirst = false;
                var retry = tracker.ClaimEligibleMilestones(55);
                Assert.AreEqual(1, retry.Count);
                Assert.AreEqual(milestones[0].MakinomKey, retry[0].MakinomKey);
                Assert.AreEqual(1, store[milestones[0].MakinomKey]);
                Assert.AreEqual(135, silver);
                Assert.AreEqual(2, callbacks);
                Assert.IsEmpty(tracker.ClaimEligibleMilestones(55));

                var restored = new RpgWorld500xDistrictProgressTracker(spec, read, (k, v) => store[k] = v, reward);
                Assert.IsEmpty(restored.ClaimEligibleMilestones(55));
                Assert.AreEqual(1, restored.ClaimEligibleMilestones(100).Count);
                Assert.AreEqual(315, silver);
                Assert.AreEqual(3, callbacks);
                Assert.IsEmpty(restored.ClaimEligibleMilestones(100));
                Assert.AreEqual(0, restored.BuildCartographyPreview(100).ClaimableCount);
            }
        }

        [Test]
        public void FogCoveragePersistsInCompactRegionalBlocksWithoutRepeatWrites()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
            int writes = 0;
            Action<string, int> write = (k, v) => { store[k] = v; writes++; };
            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                var tracker = new RpgWorld500xDistrictProgressTracker(spec, read, write);
                Assert.AreEqual(0, tracker.VisitedChunkCount);
                tracker.RecordPlayerTile(new Vector2(200.5f, 300.5f), 1, out _);
                int visited = tracker.VisitedChunkCount;
                int priorWrites = writes;
                tracker.RecordPlayerTile(new Vector2(200.5f, 300.5f), 1, out _);
                Assert.AreEqual(priorWrites, writes);
                var restored = new RpgWorld500xDistrictProgressTracker(spec, read, write);
                Assert.AreEqual(visited, restored.VisitedChunkCount);
                Assert.IsTrue(restored.IsChunkVisited(6, 9));
                Assert.IsFalse(restored.IsChunkVisited(0, 0));
                Assert.IsEmpty(restored.CurrentDistrictId);
            }
            foreach (var entry in store)
            {
                StringAssert.StartsWith("rpg.return.world.cartography.fog.", entry.Key);
                Assert.Greater(entry.Value, 0);
            }
            Assert.Less(store.Count, 4 * 9);
        }

        [Test]
        public void RecordPlayerTile_RejectsOutOfBoundsAndBoundsOversizedRevealWork()
        {
            RpgExpandedMapSpec spec = RpgExpandedWorldScale.GetSpec(RpgWorldMapId.Soundwell);
            var tracker = new RpgWorld500xDistrictProgressTracker(spec);
            var invalid = new[]
            {
                new Vector2(-0.1f, 300f),
                new Vector2(200f, -0.1f),
                new Vector2(spec.ExpandedWidth, 300f),
                new Vector2(200f, spec.ExpandedHeight),
                new Vector2(float.MaxValue, 300f),
                new Vector2(float.NaN, 300f)
            };
            foreach (Vector2 tile in invalid)
            {
                Assert.IsFalse(tracker.RecordPlayerTile(tile, 1, out _));
                Assert.AreEqual(0, tracker.VisitedChunkCount);
                Assert.IsEmpty(tracker.CurrentDistrictId);
            }

            Assert.IsTrue(tracker.RecordPlayerTile(new Vector2(200.5f, 300.5f), 0, out var entered));
            Assert.AreEqual(1, tracker.VisitedChunkCount);
            Assert.IsFalse(tracker.RecordPlayerTile(invalid[2], int.MaxValue, out _));
            Assert.AreEqual(1, tracker.VisitedChunkCount);
            Assert.AreEqual(entered.Id, tracker.CurrentDistrictId);

            // Valid positions retain square reveal semantics, with work bounded to map size.
            Assert.IsFalse(tracker.RecordPlayerTile(new Vector2(200.5f, 300.5f), int.MaxValue, out _));
            Assert.AreEqual(tracker.TotalChunks, tracker.VisitedChunkCount);
        }

        [Test]
        public void BuildCartographyPreviewAndDistrictCoverage_AreReadOnlyAndTrackThresholdsAccurately()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
            Action<string, int> write = (k, v) => store[k] = v;

            RpgWorld500xMapLayout soundwell = RpgWorld500xMapLayout.Build(RpgWorldMapId.Soundwell);
            int rewardTotal = 0;
            var tracker = new RpgWorld500xDistrictProgressTracker(
                soundwell.Spec, read, write, m => rewardTotal += m.SaltSilverReward);

            // District coverage starts at 0, handles unknown/empty ids safely, and increases on visit.
            Assert.AreEqual(0f, tracker.ComputeDistrictChunkCoverageRatio(null), 0.0001f);
            Assert.AreEqual(0f, tracker.ComputeDistrictChunkCoverageRatio("unknown_district"), 0.0001f);
            Assert.AreEqual(0f, tracker.ComputeDistrictChunkCoverageRatio("sw_dist_sw"), 0.0001f);
            tracker.RecordPlayerTile(new Vector2(200.5f, 300.5f), 1, out _);
            Assert.Greater(tracker.ComputeDistrictChunkCoverageRatio("sw_dist_sw"), 0f);
            Assert.AreEqual(0f, tracker.ComputeDistrictChunkCoverageRatio("sw_dist_ne"), 0.0001f);

            int persistedFogFacts = store.Count;
            Assert.Greater(persistedFogFacts, 0);

            // Below 25%: next threshold is 25%, remaining silver is 45 + 90 + 180 = 315.
            RpgWorld500xCartographyProgressPreview early = tracker.BuildCartographyPreview(10);
            Assert.AreEqual(RpgWorldMapId.Soundwell, early.MapId);
            Assert.AreEqual(0, early.ClaimedCount);
            Assert.AreEqual(3, early.TotalMilestones);
            Assert.AreEqual(0, early.ClaimableCount);
            Assert.AreEqual(315, early.RemainingSaltSilver);
            Assert.IsTrue(early.HasNextThreshold);
            Assert.AreEqual(25, early.NextThresholdPercent);
            StringAssert.Contains("下一阶 25%", early.SummaryZh);

            // At 55%: 25% & 50% are claimable (+135), strictly read-only (no writes or callbacks).
            RpgWorld500xCartographyProgressPreview ready = tracker.BuildCartographyPreview(55);
            Assert.AreEqual(0, ready.ClaimedCount);
            Assert.AreEqual(2, ready.ClaimableCount);
            Assert.AreEqual(135, ready.ClaimableSaltSilver);
            Assert.AreEqual(315, ready.RemainingSaltSilver);
            Assert.IsTrue(ready.HasNextThreshold);
            Assert.AreEqual(100, ready.NextThresholdPercent);
            StringAssert.Contains("可领 2 项（+135 盐银）", ready.SummaryZh);
            Assert.AreEqual(persistedFogFacts, store.Count);
            Assert.AreEqual(0, rewardTotal);

            // After claiming all 3 at 100%: 3/3 claimed, 0 remaining, cleared copy.
            Assert.AreEqual(3, tracker.ClaimEligibleMilestones(100).Count);
            Assert.AreEqual(315, rewardTotal);
            RpgWorld500xCartographyProgressPreview cleared = tracker.BuildCartographyPreview(100);
            Assert.AreEqual(3, cleared.ClaimedCount);
            Assert.AreEqual(0, cleared.ClaimableCount);
            Assert.AreEqual(0, cleared.RemainingSaltSilver);
            Assert.IsFalse(cleared.HasNextThreshold);
            StringAssert.Contains("本境堪舆里程碑已全部结清", cleared.SummaryZh);
        }
    }
}
