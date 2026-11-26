using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xBountyAndDigestHudE2ETests
    {
        [Test]
        public void BountyBoardClaimAndExpeditionDigestWeatherRisk_IntegrateEndToEndWithZeroLeak()
        {
            var root = new GameObject("World500xBountyDigestHudE2ERoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupBounty = new RpgWorld500xBountyBoardHudPanel(root.transform, warmupBoot))
                using (var warmupDigest = new RpgWorld500xExpeditionDigestHudPanel(root.transform, warmupBoot))
                {
                    warmupBounty.Show();
                    warmupBounty.Hide();
                    warmupDigest.Show(0f);
                    warmupDigest.TryClickClose();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var bountyPanel = new RpgWorld500xBountyBoardHudPanel(root.transform, boot))
                using (var digestPanel = new RpgWorld500xExpeditionDigestHudPanel(root.transform, boot))
                {
                    RpgWorld500xGrandRuntimeFacade facade = boot.Driver.Facade;
                    RpgMapExplorationTracker tracker = facade.Session.Host.Tracker;

                    // 1. Discover landmarks in Soundwell to satisfy at least one regional bounty.
                    IReadOnlyList<RpgMapLandmarkMarker> markers =
                        RpgWorldLandmarkCatalog.GetMarkers(RpgWorldMapId.Soundwell);
                    int discovered = 0;
                    for (int i = 0; i < markers.Count && discovered < 8; i++)
                    {
                        if (tracker.TryDiscoverMarker(markers[i].Id))
                        {
                            discovered++;
                        }
                    }

                    Assert.IsTrue(bountyPanel.Show());
                    Assert.AreEqual(RpgWorld500xBountyBoardScope.ActiveMap, bountyPanel.ActiveScope);
                    Assert.AreEqual(3, bountyPanel.DisplayedRowCount);
                    Assert.GreaterOrEqual(bountyPanel.ClaimableCount, 1);

                    int claimableSilver = bountyPanel.ClaimableSaltSilver;
                    int initialRemaining = bountyPanel.RemainingSaltSilver;
                    int initialBalance = facade.BountyAndSupply.SaltSilverBalance;
                    Assert.Greater(claimableSilver, 0);
                    Assert.GreaterOrEqual(initialRemaining, claimableSilver);
                    StringAssert.Contains("+" + claimableSilver + " 盐银", bountyPanel.ClaimButtonLabelText);
                    StringAssert.Contains("可领", bountyPanel.ActiveMapSummaryZh);

                    // Toggle scope ActiveMap (3) <-> AllRegions (12).
                    Assert.IsTrue(bountyPanel.TrySelectScope(RpgWorld500xBountyBoardScope.AllRegions));
                    Assert.AreEqual(12, bountyPanel.DisplayedRowCount);
                    Assert.IsTrue(bountyPanel.TrySelectScope(RpgWorld500xBountyBoardScope.ActiveMap));
                    Assert.AreEqual(3, bountyPanel.DisplayedRowCount);

                    // Claim eligible bounties and verify Salt-Silver balance & remaining summary update.
                    Assert.IsTrue(bountyPanel.TryClickClaimEligible());
                    Assert.GreaterOrEqual(bountyPanel.ClaimedCount, 1);
                    Assert.AreEqual(0, bountyPanel.ClaimableCount);
                    Assert.AreEqual(0, bountyPanel.ClaimableSaltSilver);
                    Assert.AreEqual(initialRemaining - claimableSilver, bountyPanel.RemainingSaltSilver);
                    Assert.AreEqual(initialBalance + claimableSilver, facade.BountyAndSupply.SaltSilverBalance);
                    Assert.IsFalse(bountyPanel.TryClickClaimEligible());
                    Assert.IsTrue(bountyPanel.Hide());

                    // 2. ExpeditionDigestHudPanel reflects updated Salt-Silver wallet and 4-region weather risk.
                    foreach (RpgWorldMapId map in Enum.GetValues(typeof(RpgWorldMapId)))
                    {
                        facade.Atmosphere.SetWeather(map, RpgWorld500xWeatherKind.Clear);
                    }

                    Assert.IsTrue(digestPanel.Show(10f));
                    StringAssert.Contains("盐银 " + facade.BountyAndSupply.SaltSilverBalance, digestPanel.StatusRibbonText);
                    StringAssert.Contains("【四境气象速报】", digestPanel.WorldWeatherBulletinText);
                    StringAssert.Contains("【四境声望总览】", digestPanel.WorldReputationSummaryText);
                    StringAssert.Contains("【远征里程碑】", digestPanel.MilestoneSummaryText);
                    StringAssert.Contains("高危区域", digestPanel.RiskBannerText);
                    Assert.AreEqual(RpgWorld500xTravelRiskLevel.Safe, digestPanel.LastDigest.TravelRiskLevel);

                    facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.GaleStorm);
                    Assert.IsTrue(digestPanel.TryClickRefresh(15f));
                    Assert.AreEqual(RpgWorld500xTravelRiskLevel.Hazardous, digestPanel.LastDigest.TravelRiskLevel);
                    Assert.GreaterOrEqual(digestPanel.HazardousRegionCount, 1);
                    StringAssert.Contains("高危", digestPanel.RiskBannerText);

                    Assert.IsTrue(digestPanel.TryClickClose());
                    Assert.IsFalse(digestPanel.IsVisible);
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
