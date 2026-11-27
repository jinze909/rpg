using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionBountyAndSupplyLoopE2ETests
    {
        [Test]
        public void AllFourRegions_BountyMapScopingAndSupplyExchangeStayConsistentWithZeroLeak()
        {
            // Warm up one-time runtime caches before steady-state leak baseline.
            using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                Assert.AreEqual(RpgWorldMapId.Soundwell, warmupBoot.Driver.Facade.CurrentMapId);
            }

            int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

            RpgWorldMapId[] regions =
            {
                RpgWorldMapId.Soundwell,
                RpgWorldMapId.RedSaltHarbor,
                RpgWorldMapId.WhitewaterCrossing,
                RpgWorldMapId.MineCamp
            };

            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: regions[0],
                overrideTileSlices: new Sprite[] { null }))
            {
                var shop = boot.Driver.Facade.BountyAndSupply;
                Assert.AreEqual(0, shop.SaltSilverBalance);
                Assert.AreNotEqual(
                    RpgCampSupplyPurchaseStatus.Succeeded,
                    shop.TryPurchaseSupply("flare", 1));
                Assert.AreEqual(0, shop.GetOwnedSupplyCount("flare"));

                int expectedSilver = 0;
                for (int i = 0; i < regions.Length; i++)
                {
                    RpgWorldMapId mapId = regions[i];
                    RpgWorldMapId otherMapId = regions[(i + 1) % regions.Length];

                    if (i > 0)
                    {
                        Assert.IsTrue(boot.SwitchMap(mapId), "Expected map switch to " + mapId);
                    }

                    // Regional bounty progress stays strictly scoped to its owning map.
                    var fullSummary = new RpgWorld500xMapCompletionSummary(
                        mapId,
                        visitedChunks: 120, totalChunks: 2814,
                        discoveredMarkers: 12, totalMarkers: 18,
                        lootedCaches: 8, totalCaches: 12,
                        unlockedShortcuts: 4, totalShortcuts: 4,
                        inspectedStorySpots: 4, totalStorySpots: 4);

                    var ownBounties = RpgWorld500xBountyAndSupplyCatalog.GetBounties(mapId);
                    Assert.IsNotEmpty(ownBounties, "Expected bounties in " + mapId);
                    for (int b = 0; b < ownBounties.Count; b++)
                    {
                        int progress = RpgWorld500xFieldBountyAndSupplyService.EvaluateProgressCount(
                            ownBounties[b], fullSummary);
                        Assert.GreaterOrEqual(progress, ownBounties[b].TargetCount, ownBounties[b].Id);
                    }

                    var otherBounties = RpgWorld500xBountyAndSupplyCatalog.GetBounties(otherMapId);
                    for (int b = 0; b < otherBounties.Count; b++)
                    {
                        int crossProgress = RpgWorld500xFieldBountyAndSupplyService.EvaluateProgressCount(
                            otherBounties[b], fullSummary);
                        Assert.AreEqual(0, crossProgress, otherBounties[b].Id);
                    }

                    // Salt-Silver wallet and supply stock persist cleanly across region switches.
                    shop.AddSaltSilver(30);
                    expectedSilver += 30;
                    Assert.AreEqual(expectedSilver, shop.SaltSilverBalance);
                    Assert.AreEqual(
                        RpgCampSupplyPurchaseStatus.Succeeded,
                        shop.TryPurchaseSupply("flare", 1));
                    expectedSilver -= 30;
                    Assert.AreEqual(expectedSilver, shop.SaltSilverBalance);
                    Assert.AreEqual(i + 1, shop.GetOwnedSupplyCount("flare"));
                }
            }

            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
        }
    }
}
