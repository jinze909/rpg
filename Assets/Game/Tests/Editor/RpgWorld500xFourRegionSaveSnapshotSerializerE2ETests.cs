using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionSaveSnapshotSerializerE2ETests
    {
        [Test]
        public void SaveSnapshotSerializer_AcrossAllFourRegions_RoundTripsMapStoreAndWeatherIntoLiveBootstrapper()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            for (int i = 0; i < specs.Count; i++)
            {
                var targetMap = specs[i].MapId;
                var initialMap = specs[(i + 1) % specs.Count].MapId;
                int expectedSilver = 120 + i * 25;
                string cacheKey = RpgWorld500xContentCatalog.GetCaches(targetMap)[0].MakinomKey;
                string repKey = RpgWorld500xRegionalReputationService.ReputationKey(targetMap);

                var store = new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["rpg.return.world.supply.salt_silver_balance"] = expectedSilver,
                    [cacheKey] = 1,
                    [repKey] = 200 + i * 50
                };
                var atmosphere = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: 600f, initialDay: 1);
                for (int j = 0; j < specs.Count; j++)
                {
                    atmosphere.SetWeather(
                        specs[j].MapId,
                        specs[j].MapId == targetMap ? RpgWorld500xWeatherKind.GaleStorm : RpgWorld500xWeatherKind.Clear);
                }

                var snap = RpgWorld500xSaveSnapshotSerializer.Capture(
                    targetMap, new Vector2(100f + i * 10f, 120f + i * 10f), store, atmosphere);
                string json = RpgWorld500xSaveSnapshotSerializer.Serialize(snap);
                Assert.IsTrue(RpgWorld500xSaveSnapshotSerializer.TryDeserialize(json, out var parsed, out string parseError), parseError);
                Assert.That(parsed, Is.Not.Null);

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: initialMap,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    Assert.IsTrue(boot.TryImportSaveJson(json, out string importError), importError);
                    var facade = boot.Driver.Facade;
                    Assert.That(facade.CurrentMapId, Is.EqualTo(targetMap));
                    Assert.That(facade.BountyAndSupply.SaltSilverBalance, Is.EqualTo(expectedSilver));
                    Assert.That(boot.Store[cacheKey], Is.EqualTo(1));
                    Assert.That(boot.Store[repKey], Is.EqualTo(200 + i * 50));
                    Assert.That(facade.Atmosphere.GetWeather(targetMap), Is.EqualTo(RpgWorld500xWeatherKind.GaleStorm));
                }
            }
        }

        [Test]
        public void SaveSnapshotSerializer_AcrossAllFourRegions_RejectsDuplicateKeysAndTamperedPayloadsWithoutMutatingLiveState()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            for (int i = 0; i < specs.Count; i++)
            {
                var mapId = specs[i].MapId;
                var store = new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["rpg.return.world.supply.salt_silver_balance"] = 120
                };
                var atmosphere = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: 600f, initialDay: 1);
                var snap = RpgWorld500xSaveSnapshotSerializer.Capture(
                    mapId, new Vector2(100f, 200f), store, atmosphere);
                string canonical = RpgWorld500xSaveSnapshotSerializer.Serialize(snap);
                Assert.IsTrue(RpgWorld500xSaveSnapshotSerializer.TryDeserialize(canonical, out _, out string okError), okError);

                var dupInt = snap.worldIntFlags[0];
                dupInt.value = 999;
                snap.worldIntFlags.Add(dupInt);
                string dupIntJson = RpgWorld500xSaveSnapshotSerializer.Serialize(snap);
                Assert.IsFalse(RpgWorld500xSaveSnapshotSerializer.TryDeserialize(dupIntJson, out var rejectedInt, out string intErr));
                Assert.IsNull(rejectedInt);
                StringAssert.Contains("Duplicate world integer key", intErr);

                snap.worldIntFlags.RemoveAt(snap.worldIntFlags.Count - 1);
                var dupAtmo = snap.atmosphereState[0];
                snap.atmosphereState.Add(dupAtmo);
                string dupAtmoJson = RpgWorld500xSaveSnapshotSerializer.Serialize(snap);
                Assert.IsFalse(RpgWorld500xSaveSnapshotSerializer.TryDeserialize(dupAtmoJson, out var rejectedAtmo, out string atmoErr));
                Assert.IsNull(rejectedAtmo);
                StringAssert.Contains("Duplicate atmosphere key", atmoErr);

                string tampered = canonical.Replace("120", "999");
                Assert.IsFalse(RpgWorld500xSaveSnapshotSerializer.TryDeserialize(tampered, out var rejectedTamper, out _));
                Assert.IsNull(rejectedTamper);

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: mapId,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    boot.Driver.Facade.BountyAndSupply.AddSaltSilver(42);
                    Assert.IsFalse(boot.TryImportSaveJson(dupIntJson, out _));
                    Assert.IsFalse(boot.TryImportSaveJson(tampered, out _));
                    Assert.That(boot.Driver.Facade.CurrentMapId, Is.EqualTo(mapId));
                    Assert.That(boot.Driver.Facade.BountyAndSupply.SaltSilverBalance, Is.EqualTo(42));
                }
            }
        }
    }
}
