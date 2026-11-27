using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    /// <summary>
    /// Four-region E2E regression tests for RpgWorld500xFieldReconScannerService covering
    /// all five ping categories across Soundwell, RedSaltHarbor, WhitewaterCrossing, and
    /// MineCamp, completion progression exclusion, ambient-zone persistence, visibility
    /// modulation, and ascending distance/ID sort invariants.
    /// </summary>
    public sealed class RpgWorld500xFourRegionFieldReconMatrixE2ETests
    {
        private static readonly RpgWorld500xReconPingKind[] AllPingKinds =
        {
            RpgWorld500xReconPingKind.HiddenCache,
            RpgWorld500xReconPingKind.LockedShortcut,
            RpgWorld500xReconPingKind.UnheardRumorNpc,
            RpgWorld500xReconPingKind.UninspectedStorySpot,
            RpgWorld500xReconPingKind.AmbientEventZone
        };

        [Test]
        public void AllFourRegions_TryFindNearestByKind_CoversAllFivePingKindsWithoutTriggeringCooldown()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            var scanner = new RpgWorld500xFieldReconScannerService(cooldownSeconds: 4f);

            for (int r = 0; r < specs.Count; r++)
            {
                RpgExpandedMapSpec spec = specs[r];
                var center = new Vector2(spec.ExpandedWidth * 0.5f, spec.ExpandedHeight * 0.5f);

                for (int k = 0; k < AllPingKinds.Length; k++)
                {
                    RpgWorld500xReconPingKind kind = AllPingKinds[k];
                    Assert.IsTrue(
                        scanner.TryFindNearestByKind(spec.MapId, center, kind, out RpgWorld500xReconPing nearest, maxRadiusTiles: 3000f),
                        $"Expected nearest {kind} ping on {spec.MapId}.");

                    Assert.AreEqual(kind, nearest.Kind);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(nearest.EntityId), $"Empty EntityId for {kind} on {spec.MapId}.");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(nearest.TitleZh), $"Empty TitleZh for {kind} on {spec.MapId}.");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(nearest.DirectionZh), $"Empty DirectionZh for {kind} on {spec.MapId}.");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(nearest.FormattedPingZh), $"Empty FormattedPingZh for {kind} on {spec.MapId}.");
                    Assert.GreaterOrEqual(nearest.DistanceTiles, 0f);
                    Assert.AreEqual(0f, scanner.GetRemainingCooldown(10f), 0.001f,
                        "TryFindNearestByKind must never consume pulse cooldown.");
                }
            }
        }

        [Test]
        public void AllFourRegions_CompletionProgressionExcludesCompletedEntitiesAndPreservesAmbientZones()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            float clock = 10f;

            for (int r = 0; r < specs.Count; r++)
            {
                RpgExpandedMapSpec spec = specs[r];
                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                var scanner = new RpgWorld500xFieldReconScannerService(
                    getInt: key => store.TryGetValue(key, out int v) ? v : 0,
                    cooldownSeconds: 4f);

                RpgSupplyCacheDef cache = RpgWorld500xContentCatalog.GetCaches(spec.MapId)[0];
                RpgWorldShortcutDef shortcut = RpgWorld500xContentCatalog.GetShortcuts(spec.MapId)[0];
                RpgFieldNpcDef npc = RpgWorld500xLivingWorldCatalog.GetFieldNpcs(spec.MapId)[0];
                RpgStorySpotDef spot = RpgWorld500xLivingWorldCatalog.GetStorySpots(spec.MapId)[0];
                RpgAmbientMicroEventZoneDef zone = RpgWorld500xLivingWorldCatalog.GetAmbientZones(spec.MapId)[0];

                AssertExcludedAfterCompletion(scanner, store, spec.MapId, cache.Tile, cache.Id, cache.MakinomKey,
                    RpgWorld500xReconPingKind.HiddenCache, ref clock);
                AssertExcludedAfterCompletion(scanner, store, spec.MapId, shortcut.TriggerTile, shortcut.Id, shortcut.MakinomKey,
                    RpgWorld500xReconPingKind.LockedShortcut, ref clock);
                AssertExcludedAfterCompletion(scanner, store, spec.MapId, npc.Tile, npc.Id, npc.RumorMakinomKey,
                    RpgWorld500xReconPingKind.UnheardRumorNpc, ref clock);
                AssertExcludedAfterCompletion(scanner, store, spec.MapId, spot.Tile, spot.Id, spot.MakinomKey,
                    RpgWorld500xReconPingKind.UninspectedStorySpot, ref clock);

                // Ambient zones remain detectable regardless of completed stateful keys.
                var zonePos = new Vector2(zone.CenterTile.x + 0.5f, zone.CenterTile.y + 0.5f);
                Assert.IsTrue(scanner.TryFindNearestByKind(
                    spec.MapId, zonePos, RpgWorld500xReconPingKind.AmbientEventZone,
                    out RpgWorld500xReconPing ambientPing, maxRadiusTiles: 200f));
                Assert.AreEqual(zone.Id, ambientPing.EntityId);
                Assert.AreEqual("脚下", ambientPing.DirectionZh);
            }
        }

        [Test]
        public void AllFourRegions_VisibilityMultiplierClampingAndDistanceSorting()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;

            for (int r = 0; r < specs.Count; r++)
            {
                RpgExpandedMapSpec spec = specs[r];
                var center = new Vector2(spec.ExpandedWidth * 0.5f, spec.ExpandedHeight * 0.5f);
                var scanner = new RpgWorld500xFieldReconScannerService(cooldownSeconds: 4f);

                Assert.IsTrue(scanner.TryPulseScan(
                    spec.MapId,
                    center,
                    nowSeconds: 10f,
                    out List<RpgWorld500xReconPing> lowVisPings,
                    out string lowBanner,
                    baseRadiusTiles: 420f,
                    visibilityMultiplier: 0.20f, // clamps to 0.35f -> effective radius 147
                    maxResults: 32));
                Assert.IsFalse(string.IsNullOrWhiteSpace(lowBanner));
                AssertSortedAndWithinRadius(lowVisPings, 420f * 0.35f, spec.MapId);

                Assert.IsTrue(scanner.TryPulseScan(
                    spec.MapId,
                    center,
                    nowSeconds: 15f,
                    out List<RpgWorld500xReconPing> highVisPings,
                    out string highBanner,
                    baseRadiusTiles: 420f,
                    visibilityMultiplier: 2.50f, // clamps to 1.50f -> effective radius 630
                    maxResults: 32));
                Assert.IsFalse(string.IsNullOrWhiteSpace(highBanner));
                AssertSortedAndWithinRadius(highVisPings, 420f * 1.50f, spec.MapId);

                Assert.GreaterOrEqual(highVisPings.Count, lowVisPings.Count,
                    $"Higher visibility scan on {spec.MapId} must return at least as many pings as low visibility.");
            }
        }

        private static void AssertExcludedAfterCompletion(
            RpgWorld500xFieldReconScannerService scanner,
            Dictionary<string, int> store,
            RpgWorldMapId mapId,
            Vector2Int tile,
            string entityId,
            string makinomKey,
            RpgWorld500xReconPingKind kind,
            ref float clock)
        {
            var pos = new Vector2(tile.x + 0.5f, tile.y + 0.5f);

            Assert.IsTrue(scanner.TryFindNearestByKind(mapId, pos, kind, out RpgWorld500xReconPing before, maxRadiusTiles: 50f),
                $"Expected {kind} {entityId} before completion on {mapId}.");
            Assert.AreEqual(entityId, before.EntityId);
            Assert.AreEqual("脚下", before.DirectionZh);

            store[makinomKey] = 1;
            clock += 5f;

            Assert.IsTrue(scanner.TryPulseScan(
                mapId,
                pos,
                nowSeconds: clock,
                out List<RpgWorld500xReconPing> afterPings,
                out _,
                baseRadiusTiles: 300f,
                visibilityMultiplier: 1f,
                maxResults: 16,
                filterKind: kind));

            for (int i = 0; i < afterPings.Count; i++)
            {
                Assert.AreEqual(kind, afterPings[i].Kind);
                Assert.AreNotEqual(entityId, afterPings[i].EntityId,
                    $"Completed {kind} {entityId} must be excluded on {mapId}.");
            }
        }

        private static void AssertSortedAndWithinRadius(
            List<RpgWorld500xReconPing> pings,
            float expectedEffectiveRadius,
            RpgWorldMapId mapId)
        {
            for (int i = 0; i < pings.Count; i++)
            {
                Assert.LessOrEqual(pings[i].DistanceTiles, expectedEffectiveRadius + 0.001f,
                    $"Ping {pings[i].EntityId} exceeded effective radius {expectedEffectiveRadius} on {mapId}.");

                if (i > 0)
                {
                    int distCmp = pings[i - 1].DistanceTiles.CompareTo(pings[i].DistanceTiles);
                    if (distCmp == 0)
                    {
                        Assert.LessOrEqual(
                            string.Compare(pings[i - 1].EntityId, pings[i].EntityId, StringComparison.Ordinal),
                            0,
                            $"Equal-distance pings not ordinal-sorted by EntityId on {mapId}.");
                    }
                    else
                    {
                        Assert.Less(distCmp, 0, $"Pings not distance-sorted on {mapId}.");
                    }
                }
            }
        }
    }
}
