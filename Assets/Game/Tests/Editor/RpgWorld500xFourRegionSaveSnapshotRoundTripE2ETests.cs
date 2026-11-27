using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    /// <summary>
    /// Four-region E2E regression tests for RpgWorld500xSaveSnapshotSerializer covering
    /// deterministic checksum round-trips, narrative-key stripping, regional atmosphere
    /// persistence, and tamper/schema/map/duplicate-key rejection across Soundwell,
    /// RedSaltHarbor, WhitewaterCrossing, and MineCamp.
    /// </summary>
    public sealed class RpgWorld500xFourRegionSaveSnapshotRoundTripE2ETests
    {
        private static readonly string[] ReservedNarrativeKeys =
        {
            "rpg.return.m04.fog_focus",
            "rpg.return.m16.epilogue_settled",
            "rpg.return.st05.home_confirmed",
            "rpg.return.rc03.mine_resolved"
        };

        [Test]
        public void AllFourRegions_CaptureSerializeDeserializeAndApply_RoundTripsRegionStateAndStripsNarrativeKeys()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            var allWeathers = (RpgWorld500xWeatherKind[])Enum.GetValues(typeof(RpgWorld500xWeatherKind));
            Assert.Greater(allWeathers.Length, 0);

            for (int r = 0; r < specs.Count; r++)
            {
                RpgExpandedMapSpec spec = specs[r];
                RpgSupplyCacheDef firstCache = RpgWorld500xContentCatalog.GetCaches(spec.MapId)[0];
                RpgWorldShortcutDef firstShortcut = RpgWorld500xContentCatalog.GetShortcuts(spec.MapId)[0];

                int expectedSilver = 120 + (r * 35);
                var sourceStore = new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [firstCache.MakinomKey] = 1,
                    [firstShortcut.MakinomKey] = 1,
                    ["rpg.return.world.supply.salt_silver_balance"] = expectedSilver
                };
                for (int n = 0; n < ReservedNarrativeKeys.Length; n++)
                {
                    sourceStore[ReservedNarrativeKeys[n]] = 1;
                }

                int expectedDay = 2 + r;
                float expectedMinutes = 360f + (r * 210f);
                RpgWorld500xWeatherKind expectedWeather = allWeathers[r % allWeathers.Length];

                var sourceAtmo = new RpgWorld500xWeatherAndTimeOfDayService(
                    initialMinutes: expectedMinutes,
                    initialDay: expectedDay);
                sourceAtmo.SetWeather(spec.MapId, expectedWeather);

                var playerPos = new Vector2(spec.ExpandedWidth * 0.5f + 0.25f, spec.ExpandedHeight * 0.5f + 0.75f);

                RpgWorld500xSaveSnapshot snapA = RpgWorld500xSaveSnapshotSerializer.Capture(
                    spec.MapId, playerPos, sourceStore, sourceAtmo);
                RpgWorld500xSaveSnapshot snapB = RpgWorld500xSaveSnapshotSerializer.Capture(
                    spec.MapId, playerPos, sourceStore, sourceAtmo);

                Assert.AreEqual(snapA.checksum, snapB.checksum,
                    $"Checksum must be deterministic for {spec.MapId}.");
                Assert.AreEqual(3, snapA.worldIntFlags.Count,
                    $"Reserved narrative keys must be excluded on {spec.MapId}.");

                string json = RpgWorld500xSaveSnapshotSerializer.Serialize(snapA);
                Assert.IsTrue(RpgWorld500xSaveSnapshotSerializer.TryDeserialize(
                    json, out RpgWorld500xSaveSnapshot restored, out string error),
                    $"Deserialization failed on {spec.MapId}: {error}");

                var targetStore = new Dictionary<string, int>(StringComparer.Ordinal);
                var targetAtmo = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: 0f, initialDay: 1);
                RpgWorld500xSaveSnapshotSerializer.ApplyToStore(restored, (k, v) => targetStore[k] = v, targetAtmo);

                Assert.AreEqual((int)spec.MapId, restored.activeMapId);
                Assert.AreEqual(playerPos.x, restored.playerTileX, 0.01f);
                Assert.AreEqual(playerPos.y, restored.playerTileY, 0.01f);
                Assert.AreEqual(1, targetStore[firstCache.MakinomKey]);
                Assert.AreEqual(1, targetStore[firstShortcut.MakinomKey]);
                Assert.AreEqual(expectedSilver, targetStore["rpg.return.world.supply.salt_silver_balance"]);

                for (int n = 0; n < ReservedNarrativeKeys.Length; n++)
                {
                    Assert.IsFalse(targetStore.ContainsKey(ReservedNarrativeKeys[n]),
                        $"Narrative key {ReservedNarrativeKeys[n]} leaked into restored store on {spec.MapId}.");
                }

                Assert.AreEqual(expectedDay, targetAtmo.DayCount);
                Assert.AreEqual(expectedMinutes, targetAtmo.TimeOfDayMinutes, 0.05f);
                Assert.AreEqual(expectedWeather, targetAtmo.GetWeather(spec.MapId));
            }
        }

        [Test]
        public void AllFourRegions_TryDeserialize_RejectsTamperedChecksumBadSchemaBadMapAndDuplicateKeys()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            for (int r = 0; r < specs.Count; r++)
            {
                RpgExpandedMapSpec spec = specs[r];
                var store = new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["rpg.return.world.supply.salt_silver_balance"] = 75 + r
                };
                var atmo = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: 540f, initialDay: 1);
                var pos = new Vector2(spec.ExpandedWidth * 0.25f, spec.ExpandedHeight * 0.25f);

                RpgWorld500xSaveSnapshot snap = RpgWorld500xSaveSnapshotSerializer.Capture(
                    spec.MapId, pos, store, atmo);
                string canonical = RpgWorld500xSaveSnapshotSerializer.Serialize(snap);

                // 1. Tampered balance without re-signing fails checksum verification.
                string tampered = canonical.Replace(":" + (75 + r), ":9999");
                Assert.IsFalse(RpgWorld500xSaveSnapshotSerializer.TryDeserialize(
                    tampered, out _, out string checksumErr));
                StringAssert.Contains("checksum", checksumErr);

                // 2. Unsupported schemaVersion fails even if re-signed.
                RpgWorld500xSaveSnapshot badSchemaSnap = RpgWorld500xSaveSnapshotSerializer.Capture(
                    spec.MapId, pos, store, atmo);
                badSchemaSnap.schemaVersion = RpgWorld500xSaveSnapshot.CurrentSchemaVersion + 1;
                string badSchemaJson = RpgWorld500xSaveSnapshotSerializer.Serialize(badSchemaSnap);
                Assert.IsFalse(RpgWorld500xSaveSnapshotSerializer.TryDeserialize(
                    badSchemaJson, out _, out string schemaErr));
                StringAssert.Contains("Unsupported schemaVersion", schemaErr);

                // 3. Invalid activeMapId fails even if re-signed.
                RpgWorld500xSaveSnapshot badMapSnap = RpgWorld500xSaveSnapshotSerializer.Capture(
                    spec.MapId, pos, store, atmo);
                badMapSnap.activeMapId = 999;
                string badMapJson = RpgWorld500xSaveSnapshotSerializer.Serialize(badMapSnap);
                Assert.IsFalse(RpgWorld500xSaveSnapshotSerializer.TryDeserialize(
                    badMapJson, out _, out string mapErr));
                StringAssert.Contains("Invalid activeMapId", mapErr);

                // 4. Checksummed duplicate world int key is rejected.
                RpgWorld500xSaveSnapshot dupIntSnap = RpgWorld500xSaveSnapshotSerializer.Capture(
                    spec.MapId, pos, store, atmo);
                RpgWorld500xIntKeyEntry dupInt = dupIntSnap.worldIntFlags[0];
                dupInt.value = 500;
                dupIntSnap.worldIntFlags.Add(dupInt);
                string dupIntJson = RpgWorld500xSaveSnapshotSerializer.Serialize(dupIntSnap);
                Assert.IsFalse(RpgWorld500xSaveSnapshotSerializer.TryDeserialize(
                    dupIntJson, out _, out string dupIntErr));
                StringAssert.Contains("Duplicate world integer key", dupIntErr);

                // 5. Checksummed duplicate atmosphere key is rejected.
                RpgWorld500xSaveSnapshot dupAtmoSnap = RpgWorld500xSaveSnapshotSerializer.Capture(
                    spec.MapId, pos, store, atmo);
                Assert.Greater(dupAtmoSnap.atmosphereState.Count, 0);
                dupAtmoSnap.atmosphereState.Add(dupAtmoSnap.atmosphereState[0]);
                string dupAtmoJson = RpgWorld500xSaveSnapshotSerializer.Serialize(dupAtmoSnap);
                Assert.IsFalse(RpgWorld500xSaveSnapshotSerializer.TryDeserialize(
                    dupAtmoJson, out _, out string dupAtmoErr));
                StringAssert.Contains("Duplicate atmosphere key", dupAtmoErr);
            }
        }
    }
}
