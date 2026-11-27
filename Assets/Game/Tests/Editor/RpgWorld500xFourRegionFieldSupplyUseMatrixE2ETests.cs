using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionFieldSupplyUseMatrixE2ETests
    {
        [Test]
        public void FieldSupplyUseService_AcrossAllFourRegions_ConsumesSuppliesUpdatesReadinessAndRoundTripsBuffPersistence()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            for (int i = 0; i < specs.Count; i++)
            {
                var mapId = specs[i].MapId;
                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
                Action<string, int> write = (k, v) => store[k] = v;

                var shop = new RpgWorld500xFieldBountyAndSupplyService(read, write);
                shop.AddSaltSilver(300);
                Assert.That(shop.TryPurchaseSupply("gauze", 1), Is.EqualTo(RpgCampSupplyPurchaseStatus.Succeeded));
                Assert.That(shop.TryPurchaseSupply("lamp_oil", 1), Is.EqualTo(RpgCampSupplyPurchaseStatus.Succeeded));
                Assert.That(shop.TryPurchaseSupply("flare", 1), Is.EqualTo(RpgCampSupplyPurchaseStatus.Succeeded));

                int totalHealPercent = 0;
                var service = new RpgWorld500xFieldSupplyUseService(
                    getInt: read,
                    setInt: write,
                    onPartyHealPercent: (_, pct) => totalHealPercent += pct);

                var healResult = service.TryConsumeSupply("gauze", 10f);
                Assert.IsTrue(healResult.Succeeded);
                Assert.That(healResult.PartyHealPercent, Is.GreaterThan(0));
                Assert.That(totalHealPercent, Is.EqualTo(healResult.PartyHealPercent));
                Assert.That(service.GetOwnedCount("gauze"), Is.EqualTo(0));
                Assert.That(service.GetRemainingBuffSeconds("gauze", 10f), Is.EqualTo(0f));

                Assert.IsTrue(service.TryConsumeSupply("lamp_oil", 10f).Succeeded);
                Assert.IsTrue(service.TryConsumeSupply("flare", 10f).Succeeded);
                Assert.That(service.BuildInventorySnapshot(10f).ActiveBuffCount, Is.EqualTo(2));
                Assert.That(service.GetRemainingBuffSeconds("lamp_oil", 10f), Is.GreaterThan(0f));
                Assert.That(service.GetRemainingBuffSeconds("flare", 10f), Is.GreaterThan(0f));

                var exported = service.ExportRemainingBuffSeconds(20f);
                Assert.That(exported.Count, Is.EqualTo(2));

                var restored = new RpgWorld500xFieldSupplyUseService(getInt: read, setInt: write);
                Assert.That(restored.RestoreRemainingBuffSeconds(exported, 20f), Is.EqualTo(2));
                Assert.That(
                    restored.GetRemainingBuffSeconds("lamp_oil", 20f),
                    Is.EqualTo(service.GetRemainingBuffSeconds("lamp_oil", 20f)).Within(0.01f));
                Assert.That(
                    restored.GetRemainingBuffSeconds("flare", 20f),
                    Is.EqualTo(service.GetRemainingBuffSeconds("flare", 20f)).Within(0.01f));

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: mapId,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    boot.Driver.Facade.Atmosphere.SetWeather(mapId, RpgWorld500xWeatherKind.GaleStorm);
                    var baseline = RpgWorld500xExpeditionReadinessService.EvaluateReadiness(boot, elapsedSeconds: 20f);
                    var withBuffs = RpgWorld500xExpeditionReadinessService.EvaluateReadiness(
                        boot, elapsedSeconds: 20f, supplyUseService: restored);
                    Assert.That(withBuffs.MapId, Is.EqualTo(mapId));
                    Assert.That(withBuffs.Score, Is.GreaterThanOrEqualTo(baseline.Score));
                }
            }
        }

        [Test]
        public void FieldSupplyUseService_RejectsReadOnlyWriteInvalidClockUnknownAndUnownedItemsWithoutMutatingState()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
            Action<string, int> write = (k, v) => store[k] = v;

            var shop = new RpgWorld500xFieldBountyAndSupplyService(read, write);
            shop.AddSaltSilver(300);
            Assert.That(shop.TryPurchaseSupply("gauze", 1), Is.EqualTo(RpgCampSupplyPurchaseStatus.Succeeded));
            Assert.That(shop.TryPurchaseSupply("lamp_oil", 1), Is.EqualTo(RpgCampSupplyPurchaseStatus.Succeeded));

            int readOnlyHeals = 0;
            var readOnlyService = new RpgWorld500xFieldSupplyUseService(
                getInt: read,
                onPartyHealPercent: (_, pct) => readOnlyHeals += pct);

            var writeFailed = readOnlyService.TryConsumeSupply("gauze", 5f);
            Assert.IsFalse(writeFailed.Succeeded);
            Assert.That(writeFailed.Status, Is.EqualTo(RpgFieldSupplyUseStatus.InventoryWriteFailed));
            Assert.That(readOnlyHeals, Is.EqualTo(0));
            Assert.That(shop.GetOwnedSupplyCount("gauze"), Is.EqualTo(1));

            var service = new RpgWorld500xFieldSupplyUseService(getInt: read, setInt: write);
            Assert.That(service.TryConsumeSupply("nonexistent_supply", 5f).Status, Is.EqualTo(RpgFieldSupplyUseStatus.UnknownItem));
            Assert.That(service.TryConsumeSupply("flare", 5f).Status, Is.EqualTo(RpgFieldSupplyUseStatus.NotOwned));
            Assert.That(service.TryConsumeSupply("lamp_oil", -1f).Status, Is.EqualTo(RpgFieldSupplyUseStatus.InvalidTimestamp));
            Assert.That(service.TryConsumeSupply("lamp_oil", float.NaN).Status, Is.EqualTo(RpgFieldSupplyUseStatus.InvalidTimestamp));
            Assert.That(shop.GetOwnedSupplyCount("lamp_oil"), Is.EqualTo(1));

            Assert.IsTrue(service.TryConsumeSupply("lamp_oil", 10f).Succeeded);
            Assert.That(service.RestoreRemainingBuffSeconds(null, -1f), Is.EqualTo(0));
            Assert.That(service.RestoreRemainingBuffSeconds(null, float.NaN), Is.EqualTo(0));
            Assert.That(service.GetRemainingBuffSeconds("lamp_oil", 20f), Is.GreaterThan(0f));

            var restoreWithInstant = new Dictionary<string, float>(StringComparer.Ordinal)
            {
                ["lamp_oil"] = 60f,
                ["gauze"] = 30f,
                ["rations"] = 40f
            };
            Assert.That(service.RestoreRemainingBuffSeconds(restoreWithInstant, 25f), Is.EqualTo(1));
            Assert.That(service.GetRemainingBuffSeconds("gauze", 25f), Is.EqualTo(0f));
            Assert.That(service.GetRemainingBuffSeconds("rations", 25f), Is.EqualTo(0f));
            Assert.That(service.GetRemainingBuffSeconds("lamp_oil", 25f), Is.EqualTo(60f).Within(0.01f));
        }
    }
}
