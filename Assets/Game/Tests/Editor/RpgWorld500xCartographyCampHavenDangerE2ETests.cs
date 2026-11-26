using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xCartographyCampHavenDangerE2ETests
    {
        [Test]
        public void CartographyPreviewCampHavenQuoteAndDangerVictoryPreview_IntegrateEndToEndWithZeroLeak()
        {
            var root = new GameObject("World500xCartographyCampHavenDangerE2ERoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupPanel = new RpgWorld500xCartographyReputationHudPanel(root.transform, warmupBoot))
                {
                    warmupPanel.Show();
                    warmupPanel.Hide();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                var metaStore = new Dictionary<string, int>(StringComparer.Ordinal);
                Func<string, int> read = k => metaStore.TryGetValue(k, out int v) ? v : 0;
                Action<string, int> write = (k, v) => metaStore[k] = v;

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var cartographyPanel = new RpgWorld500xCartographyReputationHudPanel(root.transform, boot))
                {
                    // 1. Cartography HUD panel exposes read-only milestone preview & district coverage.
                    Assert.IsTrue(cartographyPanel.Show());
                    Assert.IsTrue(cartographyPanel.HasCurrentMapCartographyPreview);
                    StringAssert.Contains("【堪舆里程碑】", cartographyPanel.CartographyPreviewSummaryZh);
                    Assert.AreEqual(RpgWorldMapId.Soundwell, cartographyPanel.LastCartographyPreview.MapId);
                    Assert.Greater(cartographyPanel.LastCartographyPreview.RemainingSaltSilver, 0);

                    boot.Driver.Facade.DistrictTracker.RecordPlayerTile(new Vector2(200.5f, 300.5f), 1, out _);
                    Assert.Greater(boot.Driver.Facade.DistrictTracker.ComputeDistrictChunkCoverageRatio("sw_dist_sw"), 0f);
                    cartographyPanel.Refresh();
                    Assert.GreaterOrEqual(cartographyPanel.ActiveDistrictCoverageRatio, 0f);
                    Assert.LessOrEqual(cartographyPanel.ActiveDistrictCoverageRatio, 1f);

                    // Switching to a remote region clears current-map preview cleanly, then jumps back.
                    Assert.IsTrue(cartographyPanel.TrySelectRegion(RpgWorldMapId.RedSaltHarbor));
                    Assert.IsFalse(cartographyPanel.HasCurrentMapCartographyPreview);
                    Assert.AreEqual(0f, cartographyPanel.ActiveDistrictCoverageRatio, 0.0001f);
                    Assert.IsTrue(cartographyPanel.TrySelectHighestStandingRegion());
                    Assert.IsTrue(cartographyPanel.HasCurrentMapCartographyPreview);

                    // 2. DangerZone first-clear preview & remaining reward summary track combat victory.
                    RpgWorld500xMapLayout layout = boot.Driver.Facade.Session.Host.Layout;
                    RpgWorld500xMapFirstClearSummary beforeSummary =
                        RpgWorld500xDangerVictoryRewardService.SummarizeRemainingFirstClearRewards(layout, read);
                    Assert.Greater(beforeSummary.TotalDangerZones, 0);
                    Assert.AreEqual(0, beforeSummary.ClearedDangerZones);
                    Assert.Greater(beforeSummary.RemainingLockBreakSaltSilver, beforeSummary.RemainingStandardSaltSilver);
                    StringAssert.Contains("【首破悬赏余量】", beforeSummary.SummaryZh);

                    string firstDangerId = string.Empty;
                    for (int i = 0; i < layout.Markers.Count; i++)
                    {
                        if (layout.Markers[i].Category == RpgMapMarkerCategory.DangerZone)
                        {
                            firstDangerId = layout.Markers[i].Id;
                            break;
                        }
                    }

                    Assert.IsNotEmpty(firstDangerId);
                    RpgWorld500xDangerVictoryReward previewBefore =
                        RpgWorld500xDangerVictoryRewardService.PreviewVictoryReward(
                            RpgWorldMapId.Soundwell, firstDangerId, lockBreakAchieved: true, read);
                    Assert.IsTrue(previewBefore.IsFirstClear);
                    Assert.AreEqual(0, metaStore.Count);

                    var campHaven = new RpgWorld500xCampHavenService(read, write);
                    RpgWorld500xDangerVictoryReward granted =
                        RpgWorld500xDangerVictoryRewardService.GrantVictoryReward(
                            boot, campHaven, RpgWorldMapId.Soundwell, firstDangerId, lockBreakAchieved: true, read, write);
                    Assert.AreEqual(previewBefore.SaltSilverReward, granted.SaltSilverReward);
                    Assert.AreEqual(previewBefore.ReputationGain, granted.ReputationGain);
                    Assert.IsTrue(RpgWorld500xDangerVictoryRewardService.IsFirstClearCompleted(firstDangerId, read));

                    RpgWorld500xMapFirstClearSummary afterSummary =
                        RpgWorld500xDangerVictoryRewardService.SummarizeRemainingFirstClearRewards(layout, read);
                    Assert.AreEqual(1, afterSummary.ClearedDangerZones);
                    Assert.AreEqual(beforeSummary.TotalDangerZones - 1, afterSummary.RemainingDangerZones);
                    Assert.Less(afterSummary.RemainingLockBreakSaltSilver, beforeSummary.RemainingLockBreakSaltSilver);

                    RpgWorld500xDangerVictoryReward previewAfter =
                        RpgWorld500xDangerVictoryRewardService.PreviewVictoryReward(
                            RpgWorldMapId.Soundwell, firstDangerId, lockBreakAchieved: true, read);
                    Assert.IsFalse(previewAfter.IsFirstClear);

                    // 3. CampHaven restock quote & discounted supply purchase consume earned Salt-Silver cleanly.
                    campHaven.Reputation.AddPoints(RpgWorldMapId.Soundwell, 800);
                    int earnedSilver = boot.Driver.Facade.BountyAndSupply.SaltSilverBalance;
                    metaStore[RpgWorld500xFieldBountyAndSupplyService.WalletSaltSilverKey] = earnedSilver;

                    RpgWorld500xCampSupplyQuote quote =
                        campHaven.BuildSupplyRestockQuote(RpgWorldMapId.Soundwell, "flare", 1);
                    Assert.IsTrue(quote.IsValidItem);
                    Assert.AreEqual(30, quote.DiscountPercent);
                    Assert.AreEqual(21, quote.DiscountedUnitCost);
                    Assert.AreEqual(9, quote.UnitSavings);
                    Assert.IsTrue(quote.CanFulfillRequested);
                    Assert.GreaterOrEqual(campHaven.GetMaxAffordableDiscountedQuantity(RpgWorldMapId.Soundwell, "flare"), 1);
                    StringAssert.Contains("【营地补给报价】", quote.SummaryZh);
                    // Read-only quote does not debit wallet.
                    Assert.AreEqual(earnedSilver, metaStore[RpgWorld500xFieldBountyAndSupplyService.WalletSaltSilverKey]);

                    Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded,
                        campHaven.TryPurchaseDiscountedSupply(RpgWorldMapId.Soundwell, "flare", 1, out int charged));
                    Assert.AreEqual(21, charged);
                    Assert.AreEqual(earnedSilver - 21, metaStore[RpgWorld500xFieldBountyAndSupplyService.WalletSaltSilverKey]);

                    Assert.IsTrue(cartographyPanel.Hide());
                }

                Assert.AreEqual(0, root.transform.childCount);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
