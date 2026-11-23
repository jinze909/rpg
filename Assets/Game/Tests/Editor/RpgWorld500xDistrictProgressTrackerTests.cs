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
    }
}
