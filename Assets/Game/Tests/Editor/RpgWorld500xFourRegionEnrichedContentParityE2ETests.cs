using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    /// <summary>
    /// Four-region E2E regression tests verifying enriched content audit summaries,
    /// direct catalog parity, global Makinom key namespace isolation, rumor target
    /// integrity, and reward/ambient cycle invariants across Soundwell, RedSaltHarbor,
    /// WhitewaterCrossing, and MineCamp.
    /// </summary>
    public sealed class RpgWorld500xFourRegionEnrichedContentParityE2ETests
    {
        private static readonly RpgWorldMapId[] AllRegions =
        {
            RpgWorldMapId.Soundwell,
            RpgWorldMapId.RedSaltHarbor,
            RpgWorldMapId.WhitewaterCrossing,
            RpgWorldMapId.MineCamp
        };

        [Test]
        public void AuditAllMaps_PerRegionSummaryCountsMatchDirectCatalogsAcrossAllFourRegions()
        {
            RpgWorld500xContentAuditReport report = RpgWorld500xEnrichedContentAuditor.AuditAllMaps();

            Assert.IsTrue(report.IsValid,
                "500x enriched content audit failed:\n" + string.Join("\n", report.Errors));
            Assert.AreEqual(AllRegions.Length, report.Summaries.Count);

            var byMap = new Dictionary<RpgWorldMapId, RpgWorld500xMapContentSummary>();
            for (int i = 0; i < report.Summaries.Count; i++)
            {
                RpgWorld500xMapContentSummary summary = report.Summaries[i];
                Assert.IsFalse(byMap.ContainsKey(summary.MapId),
                    $"Duplicate audit summary for region {summary.MapId}.");
                byMap[summary.MapId] = summary;
            }

            int sumLandmarks = 0;
            int sumCaches = 0;
            int sumShortcuts = 0;
            int sumFieldNpcs = 0;
            int sumStorySpots = 0;
            int sumAmbientZones = 0;

            for (int i = 0; i < AllRegions.Length; i++)
            {
                RpgWorldMapId mapId = AllRegions[i];
                Assert.IsTrue(byMap.TryGetValue(mapId, out RpgWorld500xMapContentSummary summary),
                    $"Missing audit summary for region {mapId}.");

                RpgWorld500xMapLayout layout = RpgWorld500xMapLayout.Build(mapId);
                IReadOnlyList<RpgSupplyCacheDef> caches = RpgWorld500xContentCatalog.GetCaches(mapId);
                IReadOnlyList<RpgWorldShortcutDef> shortcuts = RpgWorld500xContentCatalog.GetShortcuts(mapId);
                IReadOnlyList<RpgFieldNpcDef> fieldNpcs = RpgWorld500xLivingWorldCatalog.GetFieldNpcs(mapId);
                IReadOnlyList<RpgStorySpotDef> storySpots = RpgWorld500xLivingWorldCatalog.GetStorySpots(mapId);
                IReadOnlyList<RpgAmbientMicroEventZoneDef> ambientZones = RpgWorld500xLivingWorldCatalog.GetAmbientZones(mapId);

                Assert.AreEqual(layout.Markers.Count, summary.LandmarkCount, $"Landmark parity mismatch on {mapId}.");
                Assert.AreEqual(caches.Count, summary.CacheCount, $"Cache parity mismatch on {mapId}.");
                Assert.AreEqual(shortcuts.Count, summary.ShortcutCount, $"Shortcut parity mismatch on {mapId}.");
                Assert.AreEqual(fieldNpcs.Count, summary.FieldNpcCount, $"FieldNpc parity mismatch on {mapId}.");
                Assert.AreEqual(storySpots.Count, summary.StorySpotCount, $"StorySpot parity mismatch on {mapId}.");
                Assert.AreEqual(ambientZones.Count, summary.AmbientZoneCount, $"AmbientZone parity mismatch on {mapId}.");

                int expectedInteractive =
                    summary.LandmarkCount +
                    summary.CacheCount +
                    summary.ShortcutCount +
                    summary.FieldNpcCount +
                    summary.StorySpotCount +
                    summary.AmbientZoneCount;
                Assert.AreEqual(expectedInteractive, summary.TotalInteractiveCount,
                    $"TotalInteractiveCount mismatch on {mapId}.");
                Assert.GreaterOrEqual(summary.TotalInteractiveCount, 35,
                    $"Region {mapId} interactive POI density fell below threshold.");

                sumLandmarks += summary.LandmarkCount;
                sumCaches += summary.CacheCount;
                sumShortcuts += summary.ShortcutCount;
                sumFieldNpcs += summary.FieldNpcCount;
                sumStorySpots += summary.StorySpotCount;
                sumAmbientZones += summary.AmbientZoneCount;
            }

            Assert.AreEqual(sumLandmarks, report.TotalLandmarks);
            Assert.AreEqual(sumCaches, report.TotalCaches);
            Assert.AreEqual(sumShortcuts, report.TotalShortcuts);
            Assert.AreEqual(sumFieldNpcs, report.TotalFieldNpcs);
            Assert.AreEqual(sumStorySpots, report.TotalStorySpots);
            Assert.AreEqual(sumAmbientZones, report.TotalAmbientZones);
            Assert.AreEqual(182,
                report.TotalLandmarks +
                report.TotalCaches +
                report.TotalShortcuts +
                report.TotalFieldNpcs +
                report.TotalStorySpots +
                report.TotalAmbientZones);
        }

        [Test]
        public void FourRegionEnrichedContent_AllMakinomKeysAreGloballyUniqueAndSafeWorldKeys()
        {
            var globalKeys = new HashSet<string>(StringComparer.Ordinal);
            int verifiedRumorTargets = 0;

            for (int r = 0; r < AllRegions.Length; r++)
            {
                RpgWorldMapId mapId = AllRegions[r];

                IReadOnlyList<RpgSupplyCacheDef> caches = RpgWorld500xContentCatalog.GetCaches(mapId);
                for (int i = 0; i < caches.Count; i++)
                {
                    RpgSupplyCacheDef cache = caches[i];
                    Assert.IsTrue(RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey(cache.MakinomKey),
                        $"Unsafe cache key {cache.MakinomKey} on {mapId}.");
                    Assert.IsTrue(globalKeys.Add(cache.MakinomKey),
                        $"Duplicate Makinom key {cache.MakinomKey} on {mapId}.");
                }

                IReadOnlyList<RpgWorldShortcutDef> shortcuts = RpgWorld500xContentCatalog.GetShortcuts(mapId);
                for (int i = 0; i < shortcuts.Count; i++)
                {
                    RpgWorldShortcutDef shortcut = shortcuts[i];
                    Assert.IsTrue(RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey(shortcut.MakinomKey),
                        $"Unsafe shortcut key {shortcut.MakinomKey} on {mapId}.");
                    Assert.IsTrue(globalKeys.Add(shortcut.MakinomKey),
                        $"Duplicate Makinom key {shortcut.MakinomKey} on {mapId}.");
                }

                IReadOnlyList<RpgFieldNpcDef> npcs = RpgWorld500xLivingWorldCatalog.GetFieldNpcs(mapId);
                for (int i = 0; i < npcs.Count; i++)
                {
                    RpgFieldNpcDef npc = npcs[i];
                    Assert.IsTrue(RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey(npc.RumorMakinomKey),
                        $"Unsafe rumor key {npc.RumorMakinomKey} on {mapId}.");
                    Assert.IsTrue(globalKeys.Add(npc.RumorMakinomKey),
                        $"Duplicate Makinom key {npc.RumorMakinomKey} on {mapId}.");

                    if (!string.IsNullOrEmpty(npc.RumorTargetMarkerId))
                    {
                        Assert.IsTrue(
                            RpgWorldLandmarkCatalog.TryGetMarker(mapId, npc.RumorTargetMarkerId, out _),
                            $"FieldNpc {npc.Id} rumor target '{npc.RumorTargetMarkerId}' missing on {mapId}.");
                        Assert.IsFalse(string.IsNullOrWhiteSpace(npc.RumorHintZh),
                            $"FieldNpc {npc.Id} rumor hint missing on {mapId}.");
                        verifiedRumorTargets++;
                    }
                }

                IReadOnlyList<RpgStorySpotDef> spots = RpgWorld500xLivingWorldCatalog.GetStorySpots(mapId);
                for (int i = 0; i < spots.Count; i++)
                {
                    RpgStorySpotDef spot = spots[i];
                    Assert.IsTrue(RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey(spot.MakinomKey),
                        $"Unsafe story spot key {spot.MakinomKey} on {mapId}.");
                    Assert.IsTrue(globalKeys.Add(spot.MakinomKey),
                        $"Duplicate Makinom key {spot.MakinomKey} on {mapId}.");
                }
            }

            Assert.AreEqual(98, globalKeys.Count,
                "Expected 42 caches + 16 shortcuts + 24 field NPCs + 16 story spots = 98 unique world Makinom keys.");
            Assert.Greater(verifiedRumorTargets, 0,
                "Expected at least one field NPC rumor target across the four regions.");
        }

        [Test]
        public void FourRegionEnrichedContent_CachesYieldPositiveSilverAndAmbientZonesHaveValidCycles()
        {
            for (int r = 0; r < AllRegions.Length; r++)
            {
                RpgWorldMapId mapId = AllRegions[r];

                IReadOnlyList<RpgSupplyCacheDef> caches = RpgWorld500xContentCatalog.GetCaches(mapId);
                Assert.Greater(caches.Count, 0, $"Region {mapId} should have supply caches.");
                for (int i = 0; i < caches.Count; i++)
                {
                    RpgSupplyCacheDef cache = caches[i];
                    Assert.Greater(cache.SaltSilverReward, 0, $"Cache {cache.Id} on {mapId} must yield positive silver.");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(cache.TitleZh), $"Cache {cache.Id} missing TitleZh.");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(cache.RewardSummaryZh), $"Cache {cache.Id} missing RewardSummaryZh.");
                }

                IReadOnlyList<RpgAmbientMicroEventZoneDef> zones = RpgWorld500xLivingWorldCatalog.GetAmbientZones(mapId);
                Assert.Greater(zones.Count, 0, $"Region {mapId} should have ambient micro-event zones.");
                for (int i = 0; i < zones.Count; i++)
                {
                    RpgAmbientMicroEventZoneDef zone = zones[i];
                    Assert.Greater(zone.RadiusTiles, 0f, $"AmbientZone {zone.Id} on {mapId} must have positive radius.");
                    Assert.Greater(zone.ActiveSeconds, 0f, $"AmbientZone {zone.Id} on {mapId} must have positive ActiveSeconds.");
                    Assert.Greater(zone.CycleSeconds, zone.ActiveSeconds,
                        $"AmbientZone {zone.Id} on {mapId} CycleSeconds must exceed ActiveSeconds.");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(zone.CaptionZh), $"AmbientZone {zone.Id} missing CaptionZh.");
                }
            }
        }
    }
}
