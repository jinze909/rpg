using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xWorldGazetteerTests
    {
        [Test]
        public void BuildWorldCards_CoversAllFour500xRegionsAndReflectsLiveCompletionProgress()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;

            List<RpgWorld500xRegionGazetteerCard> emptyCards = RpgWorld500xWorldGazetteerService.BuildWorldCards(read);
            Assert.AreEqual(4, emptyCards.Count);

            int totalCaches = 0;
            int totalShortcuts = 0;
            int totalBounties = 0;
            int totalAmbients = 0;
            for (int i = 0; i < emptyCards.Count; i++)
            {
                RpgWorld500xRegionGazetteerCard c = emptyCards[i];
                Assert.AreEqual(0, c.CompletionPercent);
                Assert.IsFalse(string.IsNullOrWhiteSpace(c.FactionNameZh));
                Assert.Greater(c.ConnectedRouteCount, 0);
                Assert.IsTrue(c.FormattedCardZh.Contains(c.MapDisplayNameZh));
                Assert.IsFalse(c.HasLiveProvisioningData);
                totalCaches += c.TotalCaches;
                totalShortcuts += c.TotalShortcuts;
                totalBounties += c.TotalBounties;
                totalAmbients += c.AmbientZoneCount;
            }

            Assert.AreEqual(42, totalCaches);
            Assert.AreEqual(16, totalShortcuts);
            Assert.AreEqual(12, totalBounties);
            Assert.AreEqual(12, totalAmbients);

            // Mark one Soundwell cache, shortcut, and bounty completed -> completion rises cleanly.
            store[RpgWorld500xContentCatalog.GetCaches(RpgWorldMapId.Soundwell)[0].MakinomKey] = 1;
            store[RpgWorld500xContentCatalog.GetShortcuts(RpgWorldMapId.Soundwell)[0].MakinomKey] = 1;
            store[RpgWorld500xBountyAndSupplyCatalog.GetBounties(RpgWorldMapId.Soundwell)[0].MakinomKey] = 1;

            List<RpgWorld500xRegionGazetteerCard> updated = RpgWorld500xWorldGazetteerService.BuildWorldCards(read);
            RpgWorld500xRegionGazetteerCard soundwell = updated.Find(c => c.MapId == RpgWorldMapId.Soundwell);
            Assert.AreEqual(1, soundwell.LootedCaches);
            Assert.AreEqual(1, soundwell.UnlockedShortcuts);
            Assert.AreEqual(1, soundwell.ClaimedBounties);
            Assert.Greater(soundwell.CompletionPercent, 0);
        }

        [Test]
        public void BuildLiveWorldCards_AttachesLiveRouteRiskProvisioningCostAndReflectsAutoRestock()
        {
            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                RpgWorld500xGrandRuntimeFacade facade = boot.Driver.Facade;
                facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.Clear);
                facade.Atmosphere.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.Clear);
                facade.Atmosphere.SetWeather(RpgWorldMapId.MineCamp, RpgWorld500xWeatherKind.GaleStorm);

                List<RpgWorld500xRegionGazetteerCard> beforeCards =
                    RpgWorld500xWorldGazetteerService.BuildLiveWorldCards(boot, elapsedSeconds: 5f);
                Assert.AreEqual(4, beforeCards.Count);

                RpgWorld500xRegionGazetteerCard soundwell = beforeCards.Find(c => c.MapId == RpgWorldMapId.Soundwell);
                Assert.IsTrue(soundwell.HasLiveProvisioningData);
                Assert.AreEqual(0, soundwell.RouteHopCount);
                Assert.IsTrue(soundwell.FormattedCardZh.Contains("本境"));

                RpgWorld500xRegionGazetteerCard mineCamp = beforeCards.Find(c => c.MapId == RpgWorldMapId.MineCamp);
                Assert.IsTrue(mineCamp.HasLiveProvisioningData);
                Assert.AreEqual(2, mineCamp.RouteHopCount);
                Assert.IsTrue(mineCamp.IsHazardousRoute);
                Assert.Greater(mineCamp.ProvisioningCostSaltSilver, 0);
                Assert.IsFalse(mineCamp.CanAffordProvisioning);
                Assert.IsTrue(mineCamp.FormattedCardZh.Contains("2跳险途"));
                Assert.IsTrue(mineCamp.FormattedCardZh.Contains("盐银不足"));

                // Funding Salt-Silver marks the hazardous route affordable before purchase.
                facade.BountyAndSupply.AddSaltSilver(300);
                List<RpgWorld500xRegionGazetteerCard> fundedCards =
                    RpgWorld500xWorldGazetteerService.BuildLiveWorldCards(boot, elapsedSeconds: 5f);
                RpgWorld500xRegionGazetteerCard mineFunded = fundedCards.Find(c => c.MapId == RpgWorldMapId.MineCamp);
                Assert.IsTrue(mineFunded.CanAffordProvisioning);
                Assert.IsTrue(mineFunded.FormattedCardZh.Contains("可备齐"));

                // Executing auto-provisioning clears pending restock cost to zero.
                RpgWorld500xProvisioningReceipt receipt = RpgWorld500xCampProvisioningService.ExecuteAutoProvisioning(
                    boot, RpgWorldMapId.MineCamp, allowPartialIfUnderBudget: false, elapsedSeconds: 5f);
                Assert.IsTrue(receipt.FullyProvisioned);

                List<RpgWorld500xRegionGazetteerCard> afterCards =
                    RpgWorld500xWorldGazetteerService.BuildLiveWorldCards(boot, elapsedSeconds: 5f);
                RpgWorld500xRegionGazetteerCard mineAfter = afterCards.Find(c => c.MapId == RpgWorldMapId.MineCamp);
                Assert.AreEqual(0, mineAfter.ProvisioningCostSaltSilver);
                Assert.IsTrue(mineAfter.CanAffordProvisioning);
                Assert.IsTrue(mineAfter.FormattedCardZh.Contains("行囊已齐备"));
            }
        }

        [Test]
        public void SearchGazetteer_FindsCrossRegionMatchesSupportsMapFilterAndHandlesEmptyQuery()
        {
            Assert.AreEqual(0, RpgWorld500xWorldGazetteerService.SearchGazetteer("").Count);
            Assert.AreEqual(0, RpgWorld500xWorldGazetteerService.SearchGazetteer("   ").Count);

            List<RpgWorld500xGazetteerSearchHit> bountyHits =
                RpgWorld500xWorldGazetteerService.SearchGazetteer("声井测绘初录");
            Assert.GreaterOrEqual(bountyHits.Count, 1);
            Assert.AreEqual(RpgWorldMapId.Soundwell, bountyHits[0].MapId);
            Assert.AreEqual(RpgWorld500xGazetteerEntryKind.Bounty, bountyHits[0].Kind);

            // Filtering to RedSaltHarbor excludes Soundwell-only results.
            List<RpgWorld500xGazetteerSearchHit> filteredOut =
                RpgWorld500xWorldGazetteerService.SearchGazetteer("声井测绘初录", filterMapId: RpgWorldMapId.RedSaltHarbor);
            Assert.AreEqual(0, filteredOut.Count);
        }
    }
}
