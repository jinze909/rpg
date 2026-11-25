using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

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
