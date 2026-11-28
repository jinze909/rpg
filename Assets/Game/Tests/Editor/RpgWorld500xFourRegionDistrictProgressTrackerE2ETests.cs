using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    [TestFixture]
    public sealed class RpgWorld500xFourRegionDistrictProgressTrackerE2ETests
    {
        [Test]
        public void AllFourRegions_DistrictCoverageAndFogPersistence_IsolatesPerRegionAndRejectsInvalidIds()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count, "Expected exactly 4 regions in 500x world scale.");

            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
            Action<string, int> write = (k, v) => store[k] = v;

            var expectedVisitedByRegion = new Dictionary<RpgWorldMapId, int>();
            var expectedCoverageByRegion = new Dictionary<RpgWorldMapId, float>();
            var firstDistrictIdByRegion = new Dictionary<RpgWorldMapId, string>();

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var districts = RpgWorld500xDistrictCatalog.GetDistricts(spec.MapId);
                Assert.Greater(districts.Count, 0, $"Region {spec.MapId} should define at least one district.");

                var tracker = new RpgWorld500xDistrictProgressTracker(spec, read, write);
                Assert.AreEqual(0, tracker.VisitedChunkCount, $"Fresh tracker for {spec.MapId} should have 0 visited chunks.");
                Assert.AreEqual(0f, tracker.ComputeDistrictChunkCoverageRatio(null), 0.0001f);
                Assert.AreEqual(0f, tracker.ComputeDistrictChunkCoverageRatio("__unknown_district__"), 0.0001f);

                RpgWorld500xDistrictDef firstDistrict = districts[0];
                var centerTile = new Vector2(
                    (firstDistrict.BoundsTiles.xMin + firstDistrict.BoundsTiles.xMax) * 0.5f,
                    (firstDistrict.BoundsTiles.yMin + firstDistrict.BoundsTiles.yMax) * 0.5f);

                tracker.RecordPlayerTile(centerTile, 1, out RpgWorld500xDistrictDef entered);
                Assert.AreEqual(firstDistrict.Id, entered.Id, $"Expected to enter {firstDistrict.Id} in {spec.MapId}.");
                Assert.Greater(tracker.VisitedChunkCount, 0, $"VisitedChunkCount should increase after reveal in {spec.MapId}.");

                float coverage = tracker.ComputeDistrictChunkCoverageRatio(firstDistrict.Id);
                Assert.Greater(coverage, 0f, $"District coverage should be positive after reveal in {spec.MapId}.");

                expectedVisitedByRegion[spec.MapId] = tracker.VisitedChunkCount;
                expectedCoverageByRegion[spec.MapId] = coverage;
                firstDistrictIdByRegion[spec.MapId] = firstDistrict.Id;
            }

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var restored = new RpgWorld500xDistrictProgressTracker(spec, read, write);
                Assert.AreEqual(expectedVisitedByRegion[spec.MapId], restored.VisitedChunkCount, $"Restored VisitedChunkCount mismatch for {spec.MapId}.");
                Assert.AreEqual(expectedCoverageByRegion[spec.MapId], restored.ComputeDistrictChunkCoverageRatio(firstDistrictIdByRegion[spec.MapId]), 0.0001f, $"Restored district coverage mismatch for {spec.MapId}.");
            }
        }

        [Test]
        public void AllFourRegions_CartographyPreviewAndMilestoneClaims_AreReadOnlyUntilClaimedAndIdempotent()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count, "Expected exactly 4 regions in 500x world scale.");

            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
            int writeCount = 0;
            Action<string, int> write = (k, v) =>
            {
                store[k] = v;
                writeCount++;
            };

            foreach (RpgExpandedMapSpec spec in specs)
            {
                RpgWorld500xCartographyMilestone[] milestones = RpgWorld500xDistrictCatalog.GetMilestones(spec.MapId);
                Assert.Greater(milestones.Length, 0, $"Region {spec.MapId} should define cartography milestones.");

                int expectedTotalSilver = 0;
                for (int i = 0; i < milestones.Length; i++)
                {
                    expectedTotalSilver += milestones[i].SaltSilverReward;
                }

                int callbackSilver = 0;
                var tracker = new RpgWorld500xDistrictProgressTracker(spec, read, write, m => callbackSilver += m.SaltSilverReward);
                int writesBeforePreview = writeCount;

                RpgWorld500xCartographyProgressPreview zeroPreview = tracker.BuildCartographyPreview(0);
                Assert.AreEqual(spec.MapId, zeroPreview.MapId);
                Assert.AreEqual(0, zeroPreview.ClaimedCount, $"Region {spec.MapId} should start unclaimed in shared store.");
                Assert.AreEqual(milestones.Length, zeroPreview.TotalMilestones);
                Assert.AreEqual(0, zeroPreview.ClaimableCount);
                Assert.AreEqual(expectedTotalSilver, zeroPreview.RemainingSaltSilver);

                RpgWorld500xCartographyProgressPreview fullPreview = tracker.BuildCartographyPreview(100);
                Assert.AreEqual(milestones.Length, fullPreview.ClaimableCount);
                Assert.AreEqual(expectedTotalSilver, fullPreview.ClaimableSaltSilver);
                Assert.AreEqual(writesBeforePreview, writeCount, $"BuildCartographyPreview must be strictly read-only in {spec.MapId}.");
                Assert.AreEqual(0, callbackSilver, $"BuildCartographyPreview must not fire reward callbacks in {spec.MapId}.");

                List<RpgWorld500xCartographyMilestone> firstClaim = tracker.ClaimEligibleMilestones(100);
                Assert.AreEqual(milestones.Length, firstClaim.Count);
                Assert.AreEqual(expectedTotalSilver, callbackSilver);

                List<RpgWorld500xCartographyMilestone> secondClaim = tracker.ClaimEligibleMilestones(100);
                Assert.AreEqual(0, secondClaim.Count, $"Repeated ClaimEligibleMilestones must be idempotent in {spec.MapId}.");
                Assert.AreEqual(expectedTotalSilver, callbackSilver);

                RpgWorld500xCartographyProgressPreview cleared = tracker.BuildCartographyPreview(100);
                Assert.AreEqual(milestones.Length, cleared.ClaimedCount);
                Assert.AreEqual(0, cleared.ClaimableCount);
                Assert.AreEqual(0, cleared.RemainingSaltSilver);
                Assert.IsFalse(cleared.HasNextThreshold);
            }
        }
    }
}
