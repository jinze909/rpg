using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionGazetteerLiveCardsE2ETests
    {
        [Test]
        public void AllFourRegions_GazetteerLiveCardsReflectRestoredFactsAndHonorExplicitReaderOverrideWithZeroLeak()
        {
            // Warm up one-time runtime caches before steady-state leak baseline.
            using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                RpgWorld500xWorldGazetteerService.BuildLiveWorldCards(warmupBoot);
            }

            int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                var facade = boot.Driver.Facade;
                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var spec in RpgExpandedWorldScale.AllSpecs)
                {
                    store[RpgWorld500xContentCatalog.GetCaches(spec.MapId)[0].MakinomKey] = 1;
                    store[RpgWorld500xContentCatalog.GetShortcuts(spec.MapId)[0].MakinomKey] = 1;
                    store[RpgWorld500xBountyAndSupplyCatalog.GetBounties(spec.MapId)[0].MakinomKey] = 1;
                    store[RpgWorld500xRegionalReputationService.ReputationKey(spec.MapId)] = 400;
                }

                var snapshot = RpgWorld500xSaveSnapshotSerializer.Capture(
                    RpgWorldMapId.Soundwell,
                    facade.Session.Host.PlayerPosition,
                    store,
                    facade.Atmosphere);
                Assert.IsTrue(
                    boot.TryImportSaveJson(RpgWorld500xSaveSnapshotSerializer.Serialize(snapshot), out string error),
                    error);

                string expectedAllyRank = RpgWorld500xRegionalReputationService.GetRankNameZh(
                    RpgRegionalReputationRank.Ally);

                var liveCards = RpgWorld500xWorldGazetteerService.BuildLiveWorldCards(boot);
                Assert.AreEqual(4, liveCards.Count);

                foreach (var spec in RpgExpandedWorldScale.AllSpecs)
                {
                    var card = liveCards.Find(c => c.MapId == spec.MapId);
                    Assert.AreEqual(spec.MapId, card.MapId);
                    Assert.AreEqual(1, card.LootedCaches, "Expected looted cache in " + spec.MapId);
                    Assert.AreEqual(1, card.UnlockedShortcuts, "Expected unlocked shortcut in " + spec.MapId);
                    Assert.AreEqual(1, card.ClaimedBounties, "Expected claimed bounty in " + spec.MapId);
                    Assert.Greater(card.CompletionPercent, 0, "Expected positive completion in " + spec.MapId);
                    Assert.AreEqual(expectedAllyRank, card.ReputationRankZh, "Expected Ally rank in " + spec.MapId);
                    Assert.Greater(card.CampDiscountPercent, 0, "Expected positive camp discount in " + spec.MapId);
                }

                // Explicit reader override renders zeroed snapshot without mutating live bootstrapper store.
                var overriddenCards = RpgWorld500xWorldGazetteerService.BuildLiveWorldCards(boot, _ => 0);
                Assert.AreEqual(4, overriddenCards.Count);
                foreach (var spec in RpgExpandedWorldScale.AllSpecs)
                {
                    var overridden = overriddenCards.Find(c => c.MapId == spec.MapId);
                    Assert.AreEqual(0, overridden.CompletionPercent, "Expected zeroed override completion in " + spec.MapId);
                    Assert.AreEqual(0, overridden.CampDiscountPercent, "Expected zeroed override discount in " + spec.MapId);
                    Assert.AreEqual(1, boot.Store[RpgWorld500xContentCatalog.GetCaches(spec.MapId)[0].MakinomKey]);
                }
            }

            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
        }
    }
}
