using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionDistrictFogBoundsAndCoverageE2ETests
    {
        [Test]
        public void FourRegion_DistrictProgressTracker_RejectsOutOfBoundsAndBoundsOversizedReveal()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            for (int r = 0; r < specs.Count; r++)
            {
                RpgExpandedMapSpec spec = specs[r];
                var tracker = new RpgWorld500xDistrictProgressTracker(spec);

                Vector2[] invalidTiles =
                {
                    new Vector2(-0.1f, 300f),
                    new Vector2(200f, -0.1f),
                    new Vector2(spec.ExpandedWidth, 300f),
                    new Vector2(200f, spec.ExpandedHeight),
                    new Vector2(float.MaxValue, 300f),
                    new Vector2(float.NaN, 300f),
                    new Vector2(200f, float.NegativeInfinity)
                };

                for (int i = 0; i < invalidTiles.Length; i++)
                {
                    Assert.IsFalse(tracker.RecordPlayerTile(invalidTiles[i], 1, out _),
                        $"Out-of-bounds/non-finite tile {invalidTiles[i]} should be rejected on {spec.MapId}.");
                    Assert.AreEqual(0, tracker.VisitedChunkCount);
                    Assert.IsEmpty(tracker.CurrentDistrictId);
                }

                Vector2 centerPos = new Vector2(spec.ExpandedWidth * 0.5f, spec.ExpandedHeight * 0.5f);
                Assert.IsTrue(tracker.RecordPlayerTile(centerPos, 0, out RpgWorld500xDistrictDef entered),
                    $"Expected district entry at center of {spec.MapId}.");
                Assert.AreEqual(1, tracker.VisitedChunkCount);
                Assert.IsFalse(string.IsNullOrEmpty(entered.Id));
                Assert.AreEqual(entered.Id, tracker.CurrentDistrictId);
                Assert.Greater(tracker.ComputeDistrictChunkCoverageRatio(entered.Id), 0f);
                Assert.LessOrEqual(tracker.ComputeDistrictChunkCoverageRatio(entered.Id), 1f);

                // Oversized radius on out-of-bounds tile must still fail closed without mutating visited chunks.
                Assert.IsFalse(tracker.RecordPlayerTile(new Vector2(spec.ExpandedWidth, 300f), int.MaxValue, out _));
                Assert.AreEqual(1, tracker.VisitedChunkCount);
                Assert.AreEqual(entered.Id, tracker.CurrentDistrictId);

                // Oversized radius on valid tile bounds chunk work cleanly to TotalChunks.
                Assert.IsFalse(tracker.RecordPlayerTile(centerPos, int.MaxValue, out _));
                Assert.AreEqual(tracker.TotalChunks, tracker.VisitedChunkCount);
                Assert.AreEqual(1f, tracker.ComputeDistrictChunkCoverageRatio(entered.Id), 0.0001f);
            }
        }

        [Test]
        public void FourRegion_DistrictProgressTracker_BuildCartographyPreviewIsReadOnlyAcrossAllFourRegions()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            for (int r = 0; r < specs.Count; r++)
            {
                RpgExpandedMapSpec spec = specs[r];
                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                var tracker = new RpgWorld500xDistrictProgressTracker(
                    spec,
                    getInt: k => store.TryGetValue(k, out int v) ? v : 0,
                    setInt: (k, v) => store[k] = v);

                RpgWorld500xCartographyProgressPreview preview0 = tracker.BuildCartographyPreview(0);
                RpgWorld500xCartographyProgressPreview preview50 = tracker.BuildCartographyPreview(50);
                RpgWorld500xCartographyProgressPreview preview100 = tracker.BuildCartographyPreview(100);

                Assert.AreEqual(0, store.Count, $"BuildCartographyPreview must be read-only on {spec.MapId}.");
                Assert.AreEqual(spec.MapId, preview0.MapId);
                Assert.AreEqual(spec.MapId, preview50.MapId);
                Assert.AreEqual(spec.MapId, preview100.MapId);
                Assert.Greater(preview0.RemainingSaltSilver, 0);
                StringAssert.Contains("【堪舆里程碑】", preview0.SummaryZh);
                StringAssert.Contains("【堪舆里程碑】", preview50.SummaryZh);
                StringAssert.Contains("【堪舆里程碑】", preview100.SummaryZh);
            }
        }
    }
}
