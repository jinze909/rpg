using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionMilestoneClaimGuardE2ETests
    {
        [Test]
        public void AllFourRegions_MilestoneClaimsGuardAgainstRejectedWritesReadOnlyAndDoublePayout()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var spec in RpgExpandedWorldScale.AllSpecs)
            {
                foreach (var cache in RpgWorld500xContentCatalog.GetCaches(spec.MapId))
                    store[cache.MakinomKey] = 1;
                foreach (var shortcut in RpgWorld500xContentCatalog.GetShortcuts(spec.MapId))
                    store[shortcut.MakinomKey] = 1;
                foreach (var npc in RpgWorld500xLivingWorldCatalog.GetFieldNpcs(spec.MapId))
                    store[npc.RumorMakinomKey] = 1;
                foreach (var spot in RpgWorld500xLivingWorldCatalog.GetStorySpots(spec.MapId))
                    store[spot.MakinomKey] = 1;
                store[RpgWorld500xRegionalReputationService.ReputationKey(spec.MapId)] =
                    RpgWorld500xRegionalReputationService.SwornThreshold;
            }

            Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
            bool acceptWrites = false;
            int paidSilver = 0;
            int rewardCalls = 0;

            var service = new RpgWorld500xExpeditionMilestoneService(
                read,
                (k, v) =>
                {
                    if (acceptWrites)
                    {
                        store[k] = v;
                    }
                },
                def =>
                {
                    paidSilver += def.SaltSilverReward;
                    rewardCalls++;
                });

            // Read-only preview service cannot mutate state or emit unpersisted rewards.
            int readOnlyRewards = 0;
            var readOnlyService = new RpgWorld500xExpeditionMilestoneService(
                read, setInt: null, onMilestoneUnlocked: _ => readOnlyRewards++);
            Assert.IsEmpty(readOnlyService.EvaluateAndClaimAll(out _));
            Assert.AreEqual(0, readOnlyRewards);

            // Rejected writes must not grant rewards or clear ClaimableCount.
            int expectedClaimable = service.BuildMilestoneSummary().ClaimableCount;
            Assert.Greater(expectedClaimable, 0);
            Assert.IsEmpty(service.EvaluateAndClaimAll(out _));
            Assert.AreEqual(0, paidSilver);
            Assert.AreEqual(0, rewardCalls);
            Assert.AreEqual(expectedClaimable, service.BuildMilestoneSummary().ClaimableCount);

            // Restoring writes claims all eligible 4-region milestones exactly once.
            acceptWrites = true;
            var claimed = service.EvaluateAndClaimAll(out _);
            Assert.AreEqual(expectedClaimable, claimed.Count);
            Assert.AreEqual(expectedClaimable, rewardCalls);
            Assert.Greater(paidSilver, 0);

            int expectedTotalSilver = 0;
            for (int i = 0; i < claimed.Count; i++)
            {
                Assert.AreEqual(1, store[claimed[i].MakinomKey]);
                expectedTotalSilver += claimed[i].SaltSilverReward;
            }

            Assert.AreEqual(expectedTotalSilver, paidSilver);
            Assert.AreEqual(0, service.BuildMilestoneSummary().ClaimableCount);

            // Subsequent evaluation is strictly idempotent with zero duplicate payout.
            Assert.IsEmpty(service.EvaluateAndClaimAll(out _));
            Assert.AreEqual(expectedTotalSilver, paidSilver);
            Assert.AreEqual(expectedClaimable, rewardCalls);
        }
    }
}
