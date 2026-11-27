using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionExpeditionMilestoneServiceE2ETests
    {
        [Test]
        public void ExpeditionMilestoneService_AggregatesCrossFourRegionCachesShortcutsAndSwornReputationIdempotently()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            int cachesPerMap = 3;
            int shortcutsPerMap = 2;

            for (int i = 0; i < specs.Count; i++)
            {
                var mapId = specs[i].MapId;
                var caches = RpgWorld500xContentCatalog.GetCaches(mapId);
                for (int c = 0; c < cachesPerMap && c < caches.Count; c++)
                {
                    store[caches[c].MakinomKey] = 1;
                }

                var shortcuts = RpgWorld500xContentCatalog.GetShortcuts(mapId);
                for (int s = 0; s < shortcutsPerMap && s < shortcuts.Count; s++)
                {
                    store[shortcuts[s].MakinomKey] = 1;
                }

                store[RpgWorld500xRegionalReputationService.ReputationKey(mapId)] =
                    RpgWorld500xRegionalReputationService.SwornThreshold;
            }

            int initialStoreCount = store.Count;
            int awardedSilver = 0;
            int rewardCallbacks = 0;
            var service = new RpgWorld500xExpeditionMilestoneService(
                getInt: k => store.TryGetValue(k, out int v) ? v : 0,
                setInt: (k, v) => store[k] = v,
                onMilestoneUnlocked: def =>
                {
                    awardedSilver += def.SaltSilverReward;
                    rewardCallbacks++;
                });

            var preview = service.BuildMilestoneSummary();
            Assert.That(preview.TotalMilestones, Is.EqualTo(8));
            Assert.That(preview.ClaimedCount, Is.EqualTo(0));
            Assert.That(preview.ClaimableCount, Is.EqualTo(3));
            Assert.That(preview.UnclaimedSaltSilverReward, Is.GreaterThan(0));
            StringAssert.Contains("待领 3 项", preview.SummaryLineZh);
            Assert.That(store.Count, Is.EqualTo(initialStoreCount));
            Assert.That(rewardCallbacks, Is.EqualTo(0));

            var claimed = service.EvaluateAndClaimAll(out _);
            Assert.That(claimed.Count, Is.EqualTo(3));
            Assert.That(rewardCallbacks, Is.EqualTo(3));
            Assert.That(awardedSilver, Is.EqualTo(preview.UnclaimedSaltSilverReward));

            var after = service.BuildMilestoneSummary();
            Assert.That(after.ClaimedCount, Is.EqualTo(3));
            Assert.That(after.ClaimableCount, Is.EqualTo(0));
            Assert.That(after.UnclaimedSaltSilverReward, Is.EqualTo(0));
            Assert.IsTrue(after.HasNextMilestone);
            StringAssert.Contains("已领 3/8", after.SummaryLineZh);

            Assert.IsEmpty(service.EvaluateAndClaimAll(out _));
            Assert.That(rewardCallbacks, Is.EqualTo(3));
            Assert.That(awardedSilver, Is.EqualTo(preview.UnclaimedSaltSilverReward));
        }

        [Test]
        public void ExpeditionMilestoneService_ReadOnlyAndSelectiveWriteFailurePreventUnpersistedRewards()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < specs.Count; i++)
            {
                var mapId = specs[i].MapId;
                var caches = RpgWorld500xContentCatalog.GetCaches(mapId);
                for (int c = 0; c < 3 && c < caches.Count; c++)
                {
                    store[caches[c].MakinomKey] = 1;
                }

                store[RpgWorld500xRegionalReputationService.ReputationKey(mapId)] =
                    RpgWorld500xRegionalReputationService.SwornThreshold;
            }

            int readOnlyCallbacks = 0;
            var readOnlyService = new RpgWorld500xExpeditionMilestoneService(
                getInt: k => store.TryGetValue(k, out int v) ? v : 0,
                setInt: null,
                onMilestoneUnlocked: _ => readOnlyCallbacks++);

            Assert.That(readOnlyService.BuildMilestoneSummary().ClaimableCount, Is.EqualTo(2));
            Assert.IsEmpty(readOnlyService.EvaluateAndClaimAll(out _));
            Assert.That(readOnlyCallbacks, Is.EqualTo(0));

            bool blockSwornWrite = true;
            string swornKey = "rpg.return.world.milestone.four_regions_sworn";
            int partialCallbacks = 0;
            var partialService = new RpgWorld500xExpeditionMilestoneService(
                getInt: k => store.TryGetValue(k, out int v) ? v : 0,
                setInt: (k, v) =>
                {
                    if (!blockSwornWrite || k != swornKey)
                    {
                        store[k] = v;
                    }
                },
                onMilestoneUnlocked: _ => partialCallbacks++);

            var firstPass = partialService.EvaluateAndClaimAll(out _);
            Assert.That(firstPass.Count, Is.EqualTo(1));
            Assert.That(firstPass[0].Id, Is.EqualTo("first_cache_sweep"));
            Assert.That(partialCallbacks, Is.EqualTo(1));
            Assert.IsFalse(store.ContainsKey(swornKey));
            Assert.That(partialService.BuildMilestoneSummary().ClaimableCount, Is.EqualTo(1));

            blockSwornWrite = false;
            var secondPass = partialService.EvaluateAndClaimAll(out _);
            Assert.That(secondPass.Count, Is.EqualTo(1));
            Assert.That(secondPass[0].Id, Is.EqualTo("four_regions_sworn"));
            Assert.That(partialCallbacks, Is.EqualTo(2));
            Assert.That(partialService.BuildMilestoneSummary().ClaimableCount, Is.EqualTo(0));
            Assert.IsEmpty(partialService.EvaluateAndClaimAll(out _));
        }
    }
}
