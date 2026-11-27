using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionReputationDiscountMatrixE2ETests
    {
        [Test]
        public void AllFourRegions_ReputationDiscountsRoundUpExactlyStayIsolatedAndSaturateSafelyWithZeroLeak()
        {
            // Warm up one-time UGUI/font caches before steady-state leak baseline.
            var warmupGo = new GameObject("World500xFourRegionRepMatrixWarmupHost");
            var warmupCtrl = warmupGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                warmupCtrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });
            }
            finally
            {
                warmupCtrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(warmupGo);
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
            int[] prices = { 1, 9, 10, 15, 22, 30, 50, 100, 16777217, int.MaxValue };

            var hostGo = new GameObject("World500xFourRegionRepMatrixE2EHost");
            var ctrl = hostGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                ctrl.Initialize(
                    overrideMap: regions[0],
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });

                var rep = ctrl.CampHaven.Reputation;
                int swornPct = RpgWorld500xRegionalReputationService.GetCampSupplyDiscountPercent(
                    RpgRegionalReputationRank.Sworn);

                for (int r = 0; r < regions.Length; r++)
                {
                    RpgWorldMapId mapId = regions[r];

                    // Before any grant, every region starts at 0% discount.
                    Assert.AreEqual(30, rep.ComputeDiscountedCostForMap(mapId, 30));
                    Assert.AreEqual(0, rep.ComputeDiscountedCostForMap(mapId, 0));
                    Assert.AreEqual(0, rep.ComputeDiscountedCostForMap(mapId, -1));

                    // Grant Sworn threshold on this region only; unvisited regions must remain unaffected.
                    rep.AddPoints(mapId, RpgWorld500xRegionalReputationService.SwornThreshold);
                    for (int other = r + 1; other < regions.Length; other++)
                    {
                        Assert.AreEqual(30, rep.ComputeDiscountedCostForMap(regions[other], 30),
                            "Reputation grant on " + mapId + " must not leak to " + regions[other]);
                    }

                    // Verify exact integer ceiling rounding across boundary prices up to int.MaxValue.
                    for (int p = 0; p < prices.Length; p++)
                    {
                        int price = prices[p];
                        int expected = (int)(((long)price * (100 - swornPct) + 99L) / 100L);
                        int actual = rep.ComputeDiscountedCostForMap(mapId, price);
                        Assert.AreEqual(expected, actual, mapId + " price " + price);
                        Assert.GreaterOrEqual(actual, 1);
                        Assert.LessOrEqual(actual, price);
                    }

                    // Overflow saturation and negative-grant rejection preserve Sworn standing.
                    rep.AddPoints(mapId, int.MaxValue);
                    rep.AddPoints(mapId, -100);
                    Assert.AreEqual(21, rep.ComputeDiscountedCostForMap(mapId, 30));
                }
            }
            finally
            {
                ctrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(hostGo);
            }

            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
        }
    }
}
