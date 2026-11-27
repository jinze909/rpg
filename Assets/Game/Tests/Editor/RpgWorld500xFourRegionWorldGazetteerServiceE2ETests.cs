using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionWorldGazetteerServiceE2ETests
    {
        [Test]
        public void BuildLiveWorldCards_AcrossAllFourRegions_ReflectsImportedSaveFactsAndHonorsExplicitReaderOverride()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));
            string allyRankZh = RpgWorld500xRegionalReputationService.GetRankNameZh(RpgRegionalReputationRank.Ally);

            for (int i = 0; i < specs.Count; i++)
            {
                var mapId = specs[i].MapId;
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: mapId,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    var facade = boot.Driver.Facade;
                    string cacheKey = RpgWorld500xContentCatalog.GetCaches(mapId)[0].MakinomKey;
                    var store = new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [cacheKey] = 1,
                        [RpgWorld500xContentCatalog.GetShortcuts(mapId)[0].MakinomKey] = 1,
                        [RpgWorld500xBountyAndSupplyCatalog.GetBounties(mapId)[0].MakinomKey] = 1,
                        [RpgWorld500xRegionalReputationService.ReputationKey(mapId)] = 400
                    };

                    var snapshot = RpgWorld500xSaveSnapshotSerializer.Capture(
                        mapId, facade.Session.Host.PlayerPosition, store, facade.Atmosphere);
                    Assert.IsTrue(
                        boot.TryImportSaveJson(RpgWorld500xSaveSnapshotSerializer.Serialize(snapshot), out string error),
                        error);

                    var cards = RpgWorld500xWorldGazetteerService.BuildLiveWorldCards(boot, elapsedSeconds: 5f);
                    Assert.That(cards.Count, Is.EqualTo(4));

                    var activeCard = cards.Find(c => c.MapId == mapId);
                    Assert.IsTrue(activeCard.HasLiveProvisioningData);
                    Assert.That(activeCard.RouteHopCount, Is.EqualTo(0));
                    Assert.That(activeCard.LootedCaches, Is.EqualTo(1));
                    Assert.That(activeCard.UnlockedShortcuts, Is.EqualTo(1));
                    Assert.That(activeCard.ClaimedBounties, Is.EqualTo(1));
                    Assert.That(activeCard.CompletionPercent, Is.GreaterThan(0));
                    Assert.That(activeCard.ReputationRankZh, Is.EqualTo(allyRankZh));
                    Assert.That(activeCard.CampDiscountPercent, Is.GreaterThan(0));

                    for (int j = 0; j < cards.Count; j++)
                    {
                        if (cards[j].MapId == mapId)
                        {
                            continue;
                        }

                        Assert.That(cards[j].RouteHopCount, Is.GreaterThanOrEqualTo(1));
                        Assert.That(cards[j].CompletionPercent, Is.EqualTo(0));
                    }

                    var overridden = RpgWorld500xWorldGazetteerService
                        .BuildLiveWorldCards(boot, getInt: _ => 0, elapsedSeconds: 5f)
                        .Find(c => c.MapId == mapId);
                    Assert.That(overridden.CompletionPercent, Is.EqualTo(0));
                    Assert.That(overridden.CampDiscountPercent, Is.EqualTo(0));
                    Assert.That(boot.Store[cacheKey], Is.EqualTo(1));
                }
            }
        }

        [Test]
        public void BuildLiveWorldCards_AcrossAllFourOrigins_FlagsStormyRemoteRoutesAndKeepsOriginZeroHop()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            for (int i = 0; i < specs.Count; i++)
            {
                var origin = specs[i].MapId;
                var remote = specs[(i + 2) % specs.Count].MapId;

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: origin,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    var facade = boot.Driver.Facade;
                    for (int j = 0; j < specs.Count; j++)
                    {
                        var mapId = specs[j].MapId;
                        facade.Atmosphere.SetWeather(
                            mapId,
                            mapId == remote ? RpgWorld500xWeatherKind.GaleStorm : RpgWorld500xWeatherKind.Clear);
                    }

                    var cards = RpgWorld500xWorldGazetteerService.BuildLiveWorldCards(
                        boot, elapsedSeconds: 5f, preferSafestRoute: true);
                    Assert.That(cards.Count, Is.EqualTo(4));

                    var originCard = cards.Find(c => c.MapId == origin);
                    var remoteCard = cards.Find(c => c.MapId == remote);

                    Assert.IsTrue(originCard.HasLiveProvisioningData);
                    Assert.That(originCard.RouteHopCount, Is.EqualTo(0));
                    Assert.IsFalse(originCard.IsHazardousRoute);

                    Assert.IsTrue(remoteCard.HasLiveProvisioningData);
                    Assert.That(remoteCard.RouteHopCount, Is.GreaterThanOrEqualTo(1));
                    Assert.IsTrue(remoteCard.IsHazardousRoute);
                }
            }
        }
    }
}
