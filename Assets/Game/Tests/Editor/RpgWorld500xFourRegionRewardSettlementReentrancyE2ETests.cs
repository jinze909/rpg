using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    [TestFixture]
    public sealed class RpgWorld500xFourRegionRewardSettlementReentrancyE2ETests
    {
        [Test]
        public void AllFourRegions_BountyClaimReentryAndRejectedWrite_NeverDoublePayAndRetryCleanly()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count, "Expected exactly 4 regions in 500x world scale.");

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                var bounties = RpgWorld500xBountyAndSupplyCatalog.GetBounties(spec.MapId);
                Assert.Greater(bounties.Count, 1, $"Region {spec.MapId} should define multiple bounties.");

                RpgWorld500xFieldBountyDef blocked = bounties[0];
                bool blockFirst = true;
                bool reentered = false;
                RpgWorld500xFieldBountyAndSupplyService service = null;

                var completed = new RpgWorld500xMapCompletionSummary(
                    spec.MapId,
                    visitedChunks: 200, totalChunks: 2814,
                    discoveredMarkers: 12, totalMarkers: 18,
                    lootedCaches: 8, totalCaches: 12,
                    unlockedShortcuts: 4, totalShortcuts: 4,
                    inspectedStorySpots: 4, totalStorySpots: 4);

                service = new RpgWorld500xFieldBountyAndSupplyService(
                    k => store.TryGetValue(k, out int v) ? v : 0,
                    (k, v) =>
                    {
                        if (!reentered)
                        {
                            reentered = true;
                            Assert.AreEqual(0, service.ClaimCompletedBounties(completed).Count,
                                $"Synchronous reentry during bounty write must return empty in {spec.MapId}.");
                        }

                        if (blockFirst && k == blocked.MakinomKey)
                        {
                            return;
                        }

                        store[k] = v;
                    });

                int fullSilver = service.BuildBountyBoardSummary(completed).ClaimableSaltSilver;
                List<RpgWorld500xFieldBountyDef> firstPass = service.ClaimCompletedBounties(completed);
                Assert.IsTrue(reentered);
                Assert.AreEqual(bounties.Count - 1, firstPass.Count);
                Assert.IsFalse(service.IsBountyClaimed(blocked));
                Assert.AreEqual(fullSilver - blocked.SaltSilverReward, service.SaltSilverBalance);

                blockFirst = false;
                List<RpgWorld500xFieldBountyDef> retryPass = service.ClaimCompletedBounties(completed);
                Assert.AreEqual(1, retryPass.Count);
                Assert.AreEqual(blocked.Id, retryPass[0].Id);
                Assert.AreEqual(fullSilver, service.SaltSilverBalance);
                Assert.AreEqual(0, service.ClaimCompletedBounties(completed).Count);
            }
        }

        [Test]
        public void AllFourRegions_ExpeditionMilestoneReentryAndExceptionRecovery_SettleOnceWithoutLeakingLock()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                var caches = RpgWorld500xContentCatalog.GetCaches(spec.MapId);
                for (int i = 0; i < Math.Min(3, caches.Count); i++)
                {
                    store[caches[i].MakinomKey] = 1;
                }
            }

            bool failWrite = true;
            bool reenteredCallback = false;
            int unlockedCallbacks = 0;
            RpgWorld500xExpeditionMilestoneService service = null;

            service = new RpgWorld500xExpeditionMilestoneService(
                k => store.TryGetValue(k, out int v) ? v : 0,
                (k, v) =>
                {
                    if (failWrite)
                    {
                        throw new InvalidOperationException("Transient storage failure");
                    }

                    store[k] = v;
                },
                _ =>
                {
                    unlockedCallbacks++;
                    if (!reenteredCallback)
                    {
                        reenteredCallback = true;
                        Assert.AreEqual(0, service.EvaluateAndClaimAll(out _).Count);
                    }
                });

            Assert.Throws<InvalidOperationException>(() => service.EvaluateAndClaimAll(out _));
            Assert.AreEqual(0, unlockedCallbacks);

            failWrite = false;
            List<RpgWorld500xExpeditionMilestoneDef> claimed = service.EvaluateAndClaimAll(out RpgWorld500xCrossMapProgressSnapshot progress);
            Assert.GreaterOrEqual(progress.LootedCaches, 10);
            Assert.GreaterOrEqual(claimed.Count, 1);
            Assert.IsTrue(reenteredCallback);
            Assert.AreEqual(claimed.Count, unlockedCallbacks);
            Assert.AreEqual(0, service.EvaluateAndClaimAll(out _).Count);
            Assert.AreEqual(claimed.Count, unlockedCallbacks);
        }
    }
}
