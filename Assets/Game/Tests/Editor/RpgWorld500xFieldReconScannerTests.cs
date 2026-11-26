using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFieldReconScannerTests
    {
        [Test]
        public void TryPulseScan_FindsSortedUncompletedTargetsRespectsCooldownAndFiltersCompleted()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            var scanner = new RpgWorld500xFieldReconScannerService(
                getInt: k => store.TryGetValue(k, out int v) ? v : 0,
                cooldownSeconds: 4f);

            // Near Soundwell entry (260, 720): multiple caches, NPCs, story spots, shortcuts within 420 tiles.
            Assert.IsTrue(scanner.TryPulseScan(
                RpgWorldMapId.Soundwell,
                new Vector2(260f, 720f),
                nowSeconds: 10f,
                out List<RpgWorld500xReconPing> pings,
                out string banner,
                baseRadiusTiles: 420f,
                visibilityMultiplier: 1f,
                maxResults: 6));

            Assert.Greater(pings.Count, 0);
            Assert.LessOrEqual(pings.Count, 6);
            Assert.IsTrue(banner.Contains("勘测发现"));

            // Results must be sorted by ascending distance.
            for (int i = 1; i < pings.Count; i++)
            {
                Assert.LessOrEqual(pings[i - 1].DistanceTiles, pings[i].DistanceTiles);
                Assert.IsFalse(string.IsNullOrWhiteSpace(pings[i].DirectionZh));
            }

            // Immediate re-scan within cooldown is rejected.
            Assert.IsFalse(scanner.TryPulseScan(
                RpgWorldMapId.Soundwell,
                new Vector2(260f, 720f),
                nowSeconds: 12f,
                out _,
                out string cdBanner));
            Assert.IsTrue(cdBanner.Contains("冷却"));
            Assert.AreEqual(2f, scanner.GetRemainingCooldown(12f), 0.01f);

            // Mark the closest non-ambient entity completed, scan again after cooldown -> excluded.
            IReadOnlyList<RpgFieldNpcDef> npcs = RpgWorld500xLivingWorldCatalog.GetFieldNpcs(RpgWorldMapId.Soundwell);
            RpgFieldNpcDef npc0 = npcs[0]; // (250, 690)
            store[npc0.RumorMakinomKey] = 1;

            Assert.IsTrue(scanner.TryPulseScan(
                RpgWorldMapId.Soundwell,
                new Vector2(npc0.Tile.x + 0.5f, npc0.Tile.y + 0.5f),
                nowSeconds: 15f,
                out List<RpgWorld500xReconPing> afterDone,
                out _,
                baseRadiusTiles: 200f,
                maxResults: 10));

            for (int i = 0; i < afterDone.Count; i++)
            {
                Assert.AreNotEqual(npc0.Id, afterDone[i].EntityId);
            }

            // Non-finite coordinates are rejected safely.
            Assert.IsFalse(scanner.TryPulseScan(
                RpgWorldMapId.Soundwell,
                new Vector2(float.NaN, 0f),
                nowSeconds: 30f,
                out _,
                out _));
        }

        [Test]
        public void TryPulseScanWithFilterAndTryFindNearestByKind_ReturnOnlyRequestedCategoryWithoutTriggeringCooldown()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            var scanner = new RpgWorld500xFieldReconScannerService(
                getInt: k => store.TryGetValue(k, out int v) ? v : 0,
                cooldownSeconds: 4f);

            // Cooldown-free nearest lookup finds an unlooted HiddenCache and leaves pulse cooldown at 0.
            Assert.IsTrue(scanner.TryFindNearestByKind(
                RpgWorldMapId.Soundwell,
                new Vector2(260f, 720f),
                RpgWorld500xReconPingKind.HiddenCache,
                out RpgWorld500xReconPing nearestCache));
            Assert.AreEqual(RpgWorld500xReconPingKind.HiddenCache, nearestCache.Kind);
            Assert.AreEqual(0f, scanner.GetRemainingCooldown(10f), 0.001f);

            // Filtered pulse scan returns only LockedShortcut pings.
            Assert.IsTrue(scanner.TryPulseScan(
                RpgWorldMapId.Soundwell,
                new Vector2(260f, 720f),
                nowSeconds: 10f,
                out List<RpgWorld500xReconPing> shortcutsOnly,
                out _,
                baseRadiusTiles: 1600f,
                maxResults: 10,
                filterKind: RpgWorld500xReconPingKind.LockedShortcut));
            Assert.Greater(shortcutsOnly.Count, 0);
            for (int i = 0; i < shortcutsOnly.Count; i++)
            {
                Assert.AreEqual(RpgWorld500xReconPingKind.LockedShortcut, shortcutsOnly[i].Kind);
            }
        }

        [Test]
        public void InvalidRegionAndNegativeClock_DoNotConsumeOrResetPulseCooldown()
        {
            var scanner = new RpgWorld500xFieldReconScannerService(cooldownSeconds: 4f);
            var invalidMap = (RpgWorldMapId)255;
            var player = new Vector2(260f, 720f);

            Assert.IsFalse(scanner.TryPulseScan(invalidMap, player, 10f, out var invalidPings, out string invalidBanner));
            Assert.IsEmpty(invalidPings);
            StringAssert.Contains("区域无效", invalidBanner);
            Assert.IsFalse(scanner.TryFindNearestByKind(invalidMap, player,
                RpgWorld500xReconPingKind.HiddenCache, out _));
            Assert.AreEqual(0f, scanner.GetRemainingCooldown(10f));

            Assert.IsFalse(scanner.TryPulseScan(RpgWorldMapId.Soundwell, player, -1f, out var negativePings, out _));
            Assert.IsEmpty(negativePings);
            Assert.AreEqual(0f, scanner.GetRemainingCooldown(10f));

            Assert.IsTrue(scanner.TryPulseScan(RpgWorldMapId.Soundwell, player, 10f, out _, out _));
            Assert.IsFalse(scanner.TryPulseScan(RpgWorldMapId.Soundwell, player, -1f, out _, out _));
            Assert.IsFalse(scanner.TryPulseScan(invalidMap, player, 11f, out _, out _));
            Assert.AreEqual(4f, scanner.GetRemainingCooldown(-1f));
            Assert.AreEqual(2f, scanner.GetRemainingCooldown(12f), 0.001f);
            Assert.IsFalse(scanner.TryPulseScan(RpgWorldMapId.Soundwell, player, 12f, out _, out _));
            Assert.IsTrue(scanner.TryPulseScan(RpgWorldMapId.Soundwell, player, 14f, out _, out _));
        }


        [Test]
        public void InvalidCoordinatesAndFilterCannotSpendOrResetReconPulseCooldown()
        {
            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                var scanner = new RpgWorld500xFieldReconScannerService(cooldownSeconds: 4f);
                var player = new Vector2(260f, 720f);
                var invalidKind = (RpgWorld500xReconPingKind)255;
                var invalidTiles = new[]
                {
                    new Vector2(-0.1f, player.y),
                    new Vector2(player.x, -0.1f),
                    new Vector2(spec.ExpandedWidth, player.y),
                    new Vector2(player.x, spec.ExpandedHeight),
                    new Vector2(float.MaxValue, player.y)
                };

                foreach (Vector2 invalid in invalidTiles)
                {
                    Assert.IsFalse(scanner.TryPulseScan(spec.MapId, invalid, 10f,
                        out var pings, out string banner));
                    Assert.IsEmpty(pings);
                    StringAssert.Contains("坐标超出区域", banner);
                    Assert.IsFalse(scanner.TryFindNearestByKind(spec.MapId, invalid,
                        RpgWorld500xReconPingKind.HiddenCache, out _));
                    Assert.AreEqual(0f, scanner.GetRemainingCooldown(10f));
                }

                Assert.IsFalse(scanner.TryPulseScan(spec.MapId, player, 10f,
                    out var invalidFilterPings, out string invalidFilterBanner, filterKind: invalidKind));
                Assert.IsEmpty(invalidFilterPings);
                StringAssert.Contains("分类无效", invalidFilterBanner);
                Assert.IsFalse(scanner.TryFindNearestByKind(spec.MapId, player, invalidKind, out _));
                Assert.AreEqual(0f, scanner.GetRemainingCooldown(10f));

                Assert.IsTrue(scanner.TryPulseScan(spec.MapId, player, 10f, out _, out _));
                Assert.IsFalse(scanner.TryPulseScan(spec.MapId, invalidTiles[0], 11f, out _, out _));
                Assert.IsFalse(scanner.TryPulseScan(spec.MapId, player, 12f,
                    out _, out _, filterKind: invalidKind));
                Assert.AreEqual(1f, scanner.GetRemainingCooldown(13f), 0.001f);
                Assert.IsTrue(scanner.TryPulseScan(spec.MapId, player, 14f, out _, out _,
                    filterKind: RpgWorld500xReconPingKind.HiddenCache));
            }
        }

        [Test]
        public void ResolveDirection8Zh_CoversAllEightCompassSectorsAndUnderfoot()
        {
            var origin = new Vector2(100.5f, 100.5f);
            Assert.AreEqual("脚下", RpgWorld500xFieldReconScannerService.ResolveDirection8Zh(origin, new Vector2Int(100, 100)));
            Assert.AreEqual("正东", RpgWorld500xFieldReconScannerService.ResolveDirection8Zh(origin, new Vector2Int(150, 100)));
            Assert.AreEqual("东北", RpgWorld500xFieldReconScannerService.ResolveDirection8Zh(origin, new Vector2Int(150, 150)));
            Assert.AreEqual("正北", RpgWorld500xFieldReconScannerService.ResolveDirection8Zh(origin, new Vector2Int(100, 150)));
            Assert.AreEqual("西北", RpgWorld500xFieldReconScannerService.ResolveDirection8Zh(origin, new Vector2Int(50, 150)));
            Assert.AreEqual("正西", RpgWorld500xFieldReconScannerService.ResolveDirection8Zh(origin, new Vector2Int(50, 100)));
            Assert.AreEqual("西南", RpgWorld500xFieldReconScannerService.ResolveDirection8Zh(origin, new Vector2Int(50, 50)));
            Assert.AreEqual("正南", RpgWorld500xFieldReconScannerService.ResolveDirection8Zh(origin, new Vector2Int(100, 50)));
            Assert.AreEqual("东南", RpgWorld500xFieldReconScannerService.ResolveDirection8Zh(origin, new Vector2Int(150, 50)));
        }
    }
}
