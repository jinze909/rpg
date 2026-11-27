using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionCartographyPreviewE2ETests
    {
        [Test]
        public void AllFourRegions_CartographyPreviewIsReadOnlyAndMilestonesClaimIdempotently()
        {
            int verifiedRegions = 0;

            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
                Action<string, int> write = (k, v) => store[k] = v;
                int rewardTotal = 0;

                var tracker = new RpgWorld500xDistrictProgressTracker(
                    spec, read, write, m => rewardTotal += m.SaltSilverReward);

                // Below 25%: preview is strictly read-only and points to 25% threshold.
                RpgWorld500xCartographyProgressPreview early = tracker.BuildCartographyPreview(10);
                Assert.AreEqual(spec.MapId, early.MapId);
                Assert.IsTrue(early.HasNextThreshold);
                Assert.AreEqual(25, early.NextThresholdPercent);
                Assert.AreEqual(0, store.Count);
                Assert.AreEqual(0, rewardTotal);

                // At 65%: two milestones eligible, next threshold is 100%, still strictly read-only.
                RpgWorld500xCartographyProgressPreview ready = tracker.BuildCartographyPreview(65);
                Assert.IsTrue(ready.HasNextThreshold);
                Assert.AreEqual(100, ready.NextThresholdPercent);
                StringAssert.Contains("可领 2 项", ready.SummaryZh);
                Assert.AreEqual(0, store.Count);
                Assert.AreEqual(0, rewardTotal);

                // Claim all 3 milestones at 100% and verify idempotent completion.
                var claimed = tracker.ClaimEligibleMilestones(100);
                Assert.AreEqual(3, claimed.Count, "Expected 3 cartography milestones in " + spec.MapId);
                int expectedSilver = 0;
                for (int i = 0; i < claimed.Count; i++)
                {
                    expectedSilver += claimed[i].SaltSilverReward;
                }

                Assert.Greater(expectedSilver, 0);
                Assert.AreEqual(expectedSilver, rewardTotal);

                RpgWorld500xCartographyProgressPreview cleared = tracker.BuildCartographyPreview(100);
                Assert.IsFalse(cleared.HasNextThreshold);
                StringAssert.Contains("本境堪舆里程碑已全部结清", cleared.SummaryZh);

                // Repeat claim yields nothing and does not pay duplicate silver.
                Assert.IsEmpty(tracker.ClaimEligibleMilestones(100));
                Assert.AreEqual(expectedSilver, rewardTotal);

                verifiedRegions++;
            }

            Assert.AreEqual(4, verifiedRegions);
        }
    }
}
