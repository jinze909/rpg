using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionContentAuditorCatalogE2ETests
    {
        [Test]
        public void FourRegionCatalogs_HaveExpectedCountsUniqueIdsAndStrictlySafeWorldKeys()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            var seenNpcIds = new HashSet<string>(StringComparer.Ordinal);
            var seenBountyIds = new HashSet<string>(StringComparer.Ordinal);

            int totalCaches = 0;
            int totalShortcuts = 0;
            int totalFieldNpcs = 0;
            int totalBounties = 0;

            for (int i = 0; i < specs.Count; i++)
            {
                var mapId = specs[i].MapId;
                string repKey = RpgWorld500xRegionalReputationService.ReputationKey(mapId);
                Assert.IsTrue(RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey(repKey), repKey);
                Assert.IsTrue(seenKeys.Add(repKey), "Duplicate key: " + repKey);

                var caches = RpgWorld500xContentCatalog.GetCaches(mapId);
                Assert.That(caches.Count, Is.GreaterThan(0));
                totalCaches += caches.Count;
                for (int c = 0; c < caches.Count; c++)
                {
                    string key = caches[c].MakinomKey;
                    Assert.IsTrue(RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey(key), key);
                    Assert.IsTrue(seenKeys.Add(key), "Duplicate cache key: " + key);
                }

                var shortcuts = RpgWorld500xContentCatalog.GetShortcuts(mapId);
                Assert.That(shortcuts.Count, Is.GreaterThan(0));
                totalShortcuts += shortcuts.Count;
                for (int s = 0; s < shortcuts.Count; s++)
                {
                    string key = shortcuts[s].MakinomKey;
                    Assert.IsTrue(RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey(key), key);
                    Assert.IsTrue(seenKeys.Add(key), "Duplicate shortcut key: " + key);
                }

                var npcs = RpgWorld500xLivingWorldCatalog.GetFieldNpcs(mapId);
                Assert.That(npcs.Count, Is.GreaterThan(0));
                totalFieldNpcs += npcs.Count;
                for (int n = 0; n < npcs.Count; n++)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(npcs[n].Id));
                    Assert.IsTrue(seenNpcIds.Add(npcs[n].Id), "Duplicate NPC id: " + npcs[n].Id);
                }

                var bounties = RpgWorld500xBountyAndSupplyCatalog.GetBounties(mapId);
                Assert.That(bounties.Count, Is.EqualTo(3));
                totalBounties += bounties.Count;
                for (int b = 0; b < bounties.Count; b++)
                {
                    var bounty = bounties[b];
                    Assert.That(bounty.MapId, Is.EqualTo(mapId));
                    Assert.That(bounty.TargetCount, Is.GreaterThan(0));
                    Assert.That(bounty.SaltSilverReward, Is.GreaterThan(0));
                    Assert.IsTrue(seenBountyIds.Add(bounty.Id), "Duplicate bounty id: " + bounty.Id);
                    Assert.IsTrue(RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey(bounty.MakinomKey), bounty.MakinomKey);
                    Assert.IsTrue(seenKeys.Add(bounty.MakinomKey), "Duplicate bounty key: " + bounty.MakinomKey);
                }
            }

            Assert.That(totalCaches, Is.EqualTo(42));
            Assert.That(totalShortcuts, Is.EqualTo(16));
            Assert.That(totalFieldNpcs, Is.EqualTo(24));
            Assert.That(totalBounties, Is.EqualTo(12));

            var supplies = RpgWorld500xBountyAndSupplyCatalog.GetCampSupplies();
            Assert.That(supplies.Count, Is.EqualTo(6));
            for (int i = 0; i < supplies.Count; i++)
            {
                var item = supplies[i];
                Assert.That(item.SaltSilverCost, Is.GreaterThan(0));
                Assert.That(item.MaxStock, Is.GreaterThan(0));
                Assert.IsTrue(RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey(item.OwnedCountKey), item.OwnedCountKey);
                Assert.IsTrue(seenKeys.Add(item.OwnedCountKey), "Duplicate supply key: " + item.OwnedCountKey);
            }
        }

        [Test]
        public void EnrichedContentAuditor_IsSafeWorldKey_RejectsNarrativeAndBlankKeysAcrossAllProtectedNamespaces()
        {
            string[] forbidden =
            {
                null,
                string.Empty,
                "   ",
                "rpg.return.m00.start",
                "rpg.return.m04.fog_focus",
                "rpg.return.m16.ending",
                "rpg.return.st03.bathhouse",
                "rpg.return.rc01.letter",
                "arbitrary.key"
            };

            for (int i = 0; i < forbidden.Length; i++)
            {
                Assert.IsFalse(RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey(forbidden[i]), forbidden[i] ?? "<null>");
            }

            Assert.IsTrue(RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey("rpg.return.world.supply.salt_silver_balance"));
            Assert.IsTrue(RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey("rpg.return.world.cartography.fog.soundwell.block.0"));
        }
    }
}
