using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xBountyBoardAndMerchantLoopE2ETests
    {
        [Test]
        public void PlayModeController_DiscoverMarkersClaimBountySilverAndBuyCampSupplyWithZeroLeak()
        {
            var uiRoot = new GameObject("World500xBountyMerchantUiRoot");
            // Warm up one-time UGUI/font caches before steady-state leak baseline.
            var warmupGo = new GameObject("World500xBountyMerchantWarmupHost");
            var warmupCtrl = warmupGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                warmupCtrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });
                using (var warmupBounty = new RpgWorld500xBountyBoardHudPanel(uiRoot.transform, warmupCtrl.Bootstrapper))
                {
                    warmupBounty.Show();
                    warmupBounty.Hide();
                }

                warmupCtrl.MenuRouter.OpenModal(RpgWorld500xModalKind.CampMerchant);
                warmupCtrl.MenuRouter.CloseAll();
            }
            finally
            {
                warmupCtrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(warmupGo);
            }

            int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

            var hostGo = new GameObject("World500xBountyMerchantE2EHost");
            var ctrl = hostGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });

                var boot = ctrl.Bootstrapper;
                var economy = boot.Driver.Facade.BountyAndSupply;
                var tracker = boot.Driver.Facade.Session.Host.Tracker;
                int silverBefore = economy.SaltSilverBalance;

                // Discover all Soundwell landmarks to satisfy landmark-discovery bounty.
                IReadOnlyList<RpgMapLandmarkMarker> markers = RpgWorldLandmarkCatalog.GetMarkers(RpgWorldMapId.Soundwell);
                for (int i = 0; i < markers.Count; i++)
                {
                    tracker.TryDiscoverMarker(markers[i].Id);
                }

                using (var bountyPanel = new RpgWorld500xBountyBoardHudPanel(uiRoot.transform, boot))
                {
                    Assert.IsTrue(bountyPanel.Show());
                    Assert.AreEqual(12, bountyPanel.TotalBountyCount);
                    Assert.AreEqual(RpgWorld500xBountyBoardScope.ActiveMap, bountyPanel.ActiveScope);
                    Assert.AreEqual(3, bountyPanel.DisplayedRowCount);
                    Assert.GreaterOrEqual(bountyPanel.ClaimableCount, 1);
                    int claimableSilver = bountyPanel.ClaimableSaltSilver;
                    Assert.GreaterOrEqual(claimableSilver, 30);

                    // Toggle AllRegions <-> ActiveMap scopes.
                    Assert.IsTrue(bountyPanel.TrySelectScope(RpgWorld500xBountyBoardScope.AllRegions));
                    Assert.AreEqual(12, bountyPanel.DisplayedRowCount);
                    Assert.IsTrue(bountyPanel.TrySelectScope(RpgWorld500xBountyBoardScope.ActiveMap));
                    Assert.AreEqual(3, bountyPanel.DisplayedRowCount);

                    // Claim eligible bounty and verify idempotency.
                    Assert.IsTrue(bountyPanel.TryClickClaimEligible());
                    Assert.AreEqual(0, bountyPanel.ClaimableCount);
                    Assert.AreEqual(0, bountyPanel.ClaimableSaltSilver);
                    Assert.AreEqual(silverBefore + claimableSilver, economy.SaltSilverBalance);
                    Assert.IsFalse(bountyPanel.TryClickClaimEligible());
                    Assert.IsTrue(bountyPanel.Hide());
                }

                // Spend newly earned bounty silver at CampMerchant to purchase a flare.
                int unitCost = ctrl.CampHaven.GetDiscountedUnitCost(RpgWorldMapId.Soundwell, "flare");
                int balanceBeforeBuy = economy.SaltSilverBalance;
                Assert.GreaterOrEqual(balanceBeforeBuy, unitCost);
                Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.CampMerchant));
                Assert.IsTrue(ctrl.MenuRouter.MerchantPanel.TryClickBuySupply("flare"));
                Assert.AreEqual(balanceBeforeBuy - unitCost, economy.SaltSilverBalance);
                Assert.AreEqual(1, economy.GetOwnedSupplyCount("flare"));

                ctrl.MenuRouter.CloseAll();
                Assert.IsFalse(ctrl.MenuRouter.HasAnyModalOpen);
            }
            finally
            {
                ctrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(hostGo);
                UnityEngine.Object.DestroyImmediate(uiRoot);
            }

            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
        }
    }
}
