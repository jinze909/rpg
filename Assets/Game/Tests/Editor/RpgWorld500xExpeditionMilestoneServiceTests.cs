using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xExpeditionMilestoneServiceTests
    {
        [Test]
        public void AllMilestones_HaveUniqueIdsSafeWorldKeysAndPositiveRewards()
        {
            IReadOnlyList<RpgWorld500xExpeditionMilestoneDef> defs =
                RpgWorld500xExpeditionMilestoneService.AllMilestones;
            Assert.AreEqual(8, defs.Count);

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < defs.Count; i++)
            {
                RpgWorld500xExpeditionMilestoneDef d = defs[i];
                Assert.IsTrue(ids.Add(d.Id), "Duplicate milestone id: " + d.Id);
                Assert.IsTrue(keys.Add(d.MakinomKey), "Duplicate milestone key: " + d.MakinomKey);
                Assert.IsTrue(RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey(d.MakinomKey), d.MakinomKey);
                Assert.IsFalse(string.IsNullOrWhiteSpace(d.TitleZh), d.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(d.DescriptionZh), d.Id);
                Assert.Greater(d.TargetCount, 0, d.Id);
                Assert.Greater(d.SaltSilverReward, 0, d.Id);
            }
        }

        [Test]
        public void CrossMapProgressAndMilestoneClaiming_AggregatesAllFourMapsIdempotently()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            int silverEarned = 0;
            var service = new RpgWorld500xExpeditionMilestoneService(
                getInt: k => store.TryGetValue(k, out int v) ? v : 0,
                setInt: (k, v) => store[k] = v,
                onMilestoneUnlocked: d => silverEarned += d.SaltSilverReward);

            // Empty store baseline verifies canonical 4-map totals.
            List<RpgWorld500xExpeditionMilestoneDef> noneClaimed =
                service.EvaluateAndClaimAll(out RpgWorld500xCrossMapProgressSnapshot empty);
            Assert.AreEqual(0, noneClaimed.Count);
            Assert.AreEqual(42, empty.TotalCaches);
            Assert.AreEqual(16, empty.TotalShortcuts);
            Assert.AreEqual(24, empty.TotalFieldNpcs);
            Assert.AreEqual(16, empty.TotalStorySpots);
            Assert.AreEqual(12, empty.TotalBounties);
            Assert.AreEqual(12, empty.TotalCartographyMilestones);
            Assert.AreEqual(4, empty.TotalFactions);
            Assert.AreEqual(0, empty.OverallWorldCompletionPercent);

            // Seed 10 caches across Soundwell + RedSaltHarbor and Sworn reputation on all 4 maps.
            IReadOnlyList<RpgSupplyCacheDef> swCaches = RpgWorld500xContentCatalog.GetCaches(RpgWorldMapId.Soundwell);
            for (int i = 0; i < 10 && i < swCaches.Count; i++)
            {
                store[swCaches[i].MakinomKey] = 1;
            }

            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            for (int i = 0; i < specs.Count; i++)
            {
                store[RpgWorld500xRegionalReputationService.ReputationKey(specs[i].MapId)] =
                    RpgWorld500xRegionalReputationService.SwornThreshold;
            }

            List<RpgWorld500xExpeditionMilestoneDef> firstClaim =
                service.EvaluateAndClaimAll(out RpgWorld500xCrossMapProgressSnapshot progress);
            Assert.AreEqual(2, firstClaim.Count);
            Assert.AreEqual(10, progress.LootedCaches);
            Assert.AreEqual(4, progress.SwornFactions);
            Assert.AreEqual(360, silverEarned); // 60 (first_cache_sweep) + 300 (four_regions_sworn)
            Assert.Greater(progress.OverallWorldCompletionPercent, 0);

            // Duplicate evaluation is strictly idempotent.
            List<RpgWorld500xExpeditionMilestoneDef> secondClaim = service.EvaluateAndClaimAll(out _);
            Assert.AreEqual(0, secondClaim.Count);
            Assert.AreEqual(360, silverEarned);

            // Status list reflects claimed flags accurately.
            List<RpgWorld500xMilestoneStatus> statuses = service.BuildStatusList(out _);
            Assert.AreEqual(8, statuses.Count);
            int claimedCount = 0;
            for (int i = 0; i < statuses.Count; i++)
            {
                if (statuses[i].IsClaimed)
                {
                    claimedCount++;
                }
            }
            Assert.AreEqual(2, claimedCount);
        }

        [Test]
        public void ReadOnlyMilestoneServiceCannotEmitUnpersistedRewards()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            var caches = RpgWorld500xContentCatalog.GetCaches(RpgWorldMapId.Soundwell);
            for (int i = 0; i < 10; i++)
                store[caches[i].MakinomKey] = 1;

            Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
            int silver = 0;
            var preview = new RpgWorld500xExpeditionMilestoneService(
                getInt: read, onMilestoneUnlocked: d => silver += d.SaltSilverReward);
            Assert.AreEqual(1, preview.BuildMilestoneSummary().ClaimableCount);
            Assert.AreEqual(0, preview.EvaluateAndClaimAll(out var progress).Count);
            Assert.AreEqual(10, progress.LootedCaches);
            Assert.AreEqual(0, preview.EvaluateAndClaimAll(out _).Count);
            Assert.AreEqual(0, silver);
            Assert.AreEqual(10, store.Count);
            Assert.AreEqual(1, preview.BuildMilestoneSummary().ClaimableCount);

            var writable = new RpgWorld500xExpeditionMilestoneService(
                read, (k, v) => store[k] = v, d => silver += d.SaltSilverReward);
            Assert.AreEqual(1, writable.EvaluateAndClaimAll(out _).Count);
            Assert.AreEqual(60, silver);
            Assert.AreEqual(0, writable.EvaluateAndClaimAll(out _).Count);
            Assert.AreEqual(60, silver);
            Assert.AreEqual(0, preview.BuildMilestoneSummary().ClaimableCount);
        }


        [Test]
        public void RejectedMilestoneClaimRemainsPendingWithoutPayingRepeatableRewards()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            var caches = RpgWorld500xContentCatalog.GetCaches(RpgWorldMapId.Soundwell);
            for (int i = 0; i < 10; i++)
                store[caches[i].MakinomKey] = 1;
            foreach (var spec in RpgExpandedWorldScale.AllSpecs)
                store[RpgWorld500xRegionalReputationService.ReputationKey(spec.MapId)] =
                    RpgWorld500xRegionalReputationService.SwornThreshold;

            bool rejectCacheClaim = true;
            string cacheClaimKey = "rpg.return.world.milestone.first_cache_sweep";
            int silver = 0;
            int rewardCalls = 0;
            var service = new RpgWorld500xExpeditionMilestoneService(
                k => store.TryGetValue(k, out int v) ? v : 0,
                (k, v) =>
                {
                    if (!rejectCacheClaim || k != cacheClaimKey)
                        store[k] = v;
                },
                d => { silver += d.SaltSilverReward; rewardCalls++; });

            // A failed claim must not prevent another eligible claim from succeeding.
            var first = service.EvaluateAndClaimAll(out _);
            Assert.AreEqual(1, first.Count);
            Assert.AreEqual("four_regions_sworn", first[0].Id);
            Assert.AreEqual(300, silver);
            Assert.AreEqual(1, rewardCalls);
            Assert.IsFalse(store.ContainsKey(cacheClaimKey));
            Assert.AreEqual(1, service.BuildMilestoneSummary().ClaimableCount);
            Assert.AreEqual(0, service.EvaluateAndClaimAll(out _).Count);
            Assert.AreEqual(300, silver);

            rejectCacheClaim = false;
            var retried = service.EvaluateAndClaimAll(out _);
            Assert.AreEqual(1, retried.Count);
            Assert.AreEqual("first_cache_sweep", retried[0].Id);
            Assert.AreEqual(1, store[cacheClaimKey]);
            Assert.AreEqual(360, silver);
            Assert.AreEqual(2, rewardCalls);
            Assert.AreEqual(0, service.EvaluateAndClaimAll(out _).Count);
            Assert.AreEqual(0, service.BuildMilestoneSummary().ClaimableCount);
            Assert.AreEqual(360, silver);
        }

        [Test]
        public void ReentrantMilestoneClaimDuringStorageAndRewardCallbacksSettlesOnlyOnce()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            var caches = RpgWorld500xContentCatalog.GetCaches(RpgWorldMapId.Soundwell);
            for (int i = 0; i < 10; i++)
                store[caches[i].MakinomKey] = 1;

            RpgWorld500xExpeditionMilestoneService service = null;
            bool readReentered = false;
            bool writeReentered = false;
            int rewards = 0;
            int paidSilver = 0;
            int claimWrites = 0;
            service = new RpgWorld500xExpeditionMilestoneService(
                k =>
                {
                    if (!readReentered)
                    {
                        readReentered = true;
                        Assert.IsEmpty(service.EvaluateAndClaimAll(out _));
                    }
                    return store.TryGetValue(k, out int v) ? v : 0;
                },
                (k, v) =>
                {
                    claimWrites++;
                    if (!writeReentered)
                    {
                        writeReentered = true;
                        Assert.IsEmpty(service.EvaluateAndClaimAll(out var nestedProgress));
                        Assert.AreEqual(10, nestedProgress.LootedCaches);
                    }
                    store[k] = v;
                },
                d =>
                {
                    rewards++;
                    paidSilver += d.SaltSilverReward;
                    Assert.IsEmpty(service.EvaluateAndClaimAll(out _));
                });

            var claimed = service.EvaluateAndClaimAll(out var progress);
            Assert.IsTrue(readReentered);
            Assert.IsTrue(writeReentered);
            Assert.AreEqual(10, progress.LootedCaches);
            Assert.AreEqual(1, claimed.Count);
            Assert.AreEqual("first_cache_sweep", claimed[0].Id);
            Assert.AreEqual(1, claimWrites);
            Assert.AreEqual(1, rewards);
            Assert.AreEqual(60, paidSilver);
            Assert.AreEqual(1, store[claimed[0].MakinomKey]);
            Assert.IsEmpty(service.EvaluateAndClaimAll(out _));
            Assert.AreEqual(1, rewards);
        }

        [Test]
        public void MilestoneClaimReservationReleasesAfterStorageException()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            var caches = RpgWorld500xContentCatalog.GetCaches(RpgWorldMapId.Soundwell);
            for (int i = 0; i < 10; i++)
                store[caches[i].MakinomKey] = 1;
            bool failWrite = true;
            int rewards = 0;
            var service = new RpgWorld500xExpeditionMilestoneService(
                k => store.TryGetValue(k, out int v) ? v : 0,
                (k, v) =>
                {
                    if (failWrite)
                        throw new InvalidOperationException("Storage unavailable");
                    store[k] = v;
                },
                _ => rewards++);

            Assert.Throws<InvalidOperationException>(() => service.EvaluateAndClaimAll(out _));
            Assert.AreEqual(0, rewards);
            failWrite = false;
            Assert.AreEqual(1, service.EvaluateAndClaimAll(out _).Count);
            Assert.AreEqual(1, rewards);
            Assert.IsEmpty(service.EvaluateAndClaimAll(out _));
        }

        [Test]
        public void BuildMilestoneSummary_PreviewsUnclaimedSilverAndClosestNextTargetWithoutMutatingState()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            var service = new RpgWorld500xExpeditionMilestoneService(
                getInt: k => store.TryGetValue(k, out int v) ? v : 0,
                setInt: (k, v) => store[k] = v);

            // Partial progress: 3 shortcuts (3/8 = 37.5%) -> closest next target is pathfinder_shortcuts.
            IReadOnlyList<RpgWorldShortcutDef> swShortcuts = RpgWorld500xContentCatalog.GetShortcuts(RpgWorldMapId.Soundwell);
            for (int i = 0; i < 3 && i < swShortcuts.Count; i++)
            {
                store[swShortcuts[i].MakinomKey] = 1;
            }

            RpgWorld500xMilestoneSummary inProgress = service.BuildMilestoneSummary();
            Assert.AreEqual(8, inProgress.TotalMilestones);
            Assert.AreEqual(0, inProgress.ClaimedCount);
            Assert.AreEqual(0, inProgress.ClaimableCount);
            Assert.AreEqual(0, inProgress.UnclaimedSaltSilverReward);
            Assert.IsTrue(inProgress.HasNextMilestone);
            Assert.AreEqual("pathfinder_shortcuts", inProgress.NextMilestone.Definition.Id);
            Assert.IsTrue(inProgress.SummaryLineZh.Contains("最近目标「逢山开路」3/8"));

            // Satisfy first_cache_sweep (10 caches, +60 Salt-Silver) -> read-only summary previews claimable reward without claiming.
            IReadOnlyList<RpgSupplyCacheDef> swCaches = RpgWorld500xContentCatalog.GetCaches(RpgWorldMapId.Soundwell);
            for (int i = 0; i < 10 && i < swCaches.Count; i++)
            {
                store[swCaches[i].MakinomKey] = 1;
            }

            RpgWorld500xMilestoneSummary claimable = service.BuildMilestoneSummary();
            Assert.AreEqual(0, claimable.ClaimedCount);
            Assert.AreEqual(1, claimable.ClaimableCount);
            Assert.AreEqual(60, claimable.UnclaimedSaltSilverReward);
            Assert.IsTrue(claimable.SummaryLineZh.Contains("待领 1 项（+60 盐银）"));
        }
    }
}
