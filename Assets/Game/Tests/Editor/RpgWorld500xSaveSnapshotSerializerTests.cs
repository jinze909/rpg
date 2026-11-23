using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xSaveSnapshotSerializerTests
    {
        [Test]
        public void CaptureSerializeAndApply_RoundTripsWorldStateAndFiltersNarrativeKeys()
        {
            var sourceStore = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["rpg.return.world.cache.sw_c03"] = 1,
                ["rpg.return.world.supply.salt_silver_balance"] = 145,
                ["rpg.return.world.codex.threat.sw.echo-pit"] = 1,
                ["rpg.return.m04.fog_focus"] = 1,
                ["rpg.return.st03.bathhouse"] = 1
            };

            var sourceAtmo = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: 1140f, initialDay: 4);
            sourceAtmo.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.GaleStorm);

            RpgWorld500xSaveSnapshot captured = RpgWorld500xSaveSnapshotSerializer.Capture(
                RpgWorldMapId.RedSaltHarbor,
                new Vector2(1700.5f, 1260.25f),
                sourceStore,
                sourceAtmo);

            // Narrative M04/ST03 keys must be strictly excluded.
            Assert.AreEqual(3, captured.worldIntFlags.Count);
            for (int i = 0; i < captured.worldIntFlags.Count; i++)
            {
                Assert.IsTrue(captured.worldIntFlags[i].key.StartsWith("rpg.return.world.", StringComparison.Ordinal));
            }

            string json = RpgWorld500xSaveSnapshotSerializer.Serialize(captured);
            Assert.IsTrue(RpgWorld500xSaveSnapshotSerializer.TryDeserialize(
                json, out RpgWorld500xSaveSnapshot restored, out string error), error);

            var targetStore = new Dictionary<string, int>(StringComparer.Ordinal);
            var targetAtmo = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: 0f, initialDay: 1);
            RpgWorld500xSaveSnapshotSerializer.ApplyToStore(restored, (k, v) => targetStore[k] = v, targetAtmo);

            Assert.AreEqual((int)RpgWorldMapId.RedSaltHarbor, restored.activeMapId);
            Assert.AreEqual(1700.5f, restored.playerTileX, 0.01f);
            Assert.AreEqual(1260.25f, restored.playerTileY, 0.01f);
            Assert.AreEqual(1, targetStore["rpg.return.world.cache.sw_c03"]);
            Assert.AreEqual(145, targetStore["rpg.return.world.supply.salt_silver_balance"]);
            Assert.IsFalse(targetStore.ContainsKey("rpg.return.m04.fog_focus"));
            Assert.AreEqual(4, targetAtmo.DayCount);
            Assert.AreEqual(1140f, targetAtmo.TimeOfDayMinutes, 0.05f);
            Assert.AreEqual(RpgWorld500xWeatherKind.GaleStorm, targetAtmo.GetWeather(RpgWorldMapId.RedSaltHarbor));
        }

        [Test]
        public void TryDeserialize_RejectsTamperedOrMalformedSnapshotsCleanly()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["rpg.return.world.supply.salt_silver_balance"] = 50
            };

            RpgWorld500xSaveSnapshot snap = RpgWorld500xSaveSnapshotSerializer.Capture(
                RpgWorldMapId.Soundwell, new Vector2(100f, 200f), store);
            string json = RpgWorld500xSaveSnapshotSerializer.Serialize(snap);

            // Tamper with salt silver balance without updating checksum
            string tampered = json.Replace(":50", ":9999");
            Assert.IsFalse(RpgWorld500xSaveSnapshotSerializer.TryDeserialize(tampered, out _, out string checksumErr));
            Assert.IsTrue(checksumErr.Contains("checksum"));

            Assert.IsFalse(RpgWorld500xSaveSnapshotSerializer.TryDeserialize("", out _, out _));
            Assert.IsFalse(RpgWorld500xSaveSnapshotSerializer.TryDeserialize("{not valid json", out _, out _));
        }
    }
}
