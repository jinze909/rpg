using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionBestiaryCatalogCodexE2ETests
    {
        [Test]
        public void BestiaryCatalogAndThreatCodex_AcrossAllFourRegions_ResolveAllDangerZonesStudyIdempotentlyAndFilterCleanly()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            for (int i = 0; i < specs.Count; i++)
            {
                var mapId = specs[i].MapId;
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: mapId,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    var facade = boot.Driver.Facade;
                    var layout = facade.Session.Host.Layout;
                    var dossiers = facade.ThreatCodex.BuildMapDossiers(layout);
                    Assert.That(dossiers.Count, Is.GreaterThan(0));

                    int expectedTotalSilver = 0;
                    for (int d = 0; d < dossiers.Count; d++)
                    {
                        var profile = RpgWorld500xBestiaryCatalog.ResolveProfileForMarker(mapId, dossiers[d].MarkerId);
                        Assert.That(profile.CodexStudySilverBonus, Is.GreaterThan(0));
                        Assert.IsFalse(string.IsNullOrEmpty(profile.RecommendedSupplyId));
                        expectedTotalSilver += profile.CodexStudySilverBonus;
                    }

                    var initialSummary = facade.ThreatCodex.BuildMapCodexSummary(layout);
                    Assert.That(initialSummary.StudiedCount, Is.EqualTo(0));
                    Assert.That(initialSummary.TotalDossiers, Is.EqualTo(dossiers.Count));
                    Assert.That(initialSummary.RemainingStudySilverBonus, Is.EqualTo(expectedTotalSilver));
                    Assert.That(initialSummary.RecommendedSupplyIds.Count, Is.GreaterThan(0));
                    StringAssert.Contains("【图鉴考据】", initialSummary.SummaryZh);

                    for (int d = 0; d < dossiers.Count; d++)
                    {
                        string markerId = dossiers[d].MarkerId;
                        Assert.IsTrue(facade.ThreatCodex.TryRecordDangerZoneStudied(mapId, markerId, out var studied));
                        Assert.That(studied.MarkerId, Is.EqualTo(markerId));
                        Assert.IsFalse(facade.ThreatCodex.TryRecordDangerZoneStudied(mapId, markerId, out _));
                    }

                    var completedSummary = facade.ThreatCodex.BuildMapCodexSummary(layout);
                    Assert.That(completedSummary.StudiedCount, Is.EqualTo(dossiers.Count));
                    Assert.That(completedSummary.RemainingStudySilverBonus, Is.EqualTo(0));
                    Assert.IsEmpty(completedSummary.RecommendedSupplyIds);
                    StringAssert.Contains("已全部收录", completedSummary.SummaryZh);
                    Assert.IsEmpty(facade.ThreatCodex.FilterDossiers(layout, unstudiedOnly: true, tierFilter: null));
                }
            }
        }

        [Test]
        public void ThreatCodex_AcrossAllFourRegions_FiltersByThreatTierAndKeepsSiblingRegionsIsolated()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: specs[0].MapId,
                overrideTileSlices: new Sprite[] { null }))
            {
                var facade = boot.Driver.Facade;
                var firstLayout = facade.Session.Host.Layout;
                var firstDossiers = facade.ThreatCodex.BuildMapDossiers(firstLayout);
                var sampleTier = firstDossiers[0].Profile.Tier;

                var tierFiltered = facade.ThreatCodex.FilterDossiers(
                    firstLayout, unstudiedOnly: true, tierFilter: sampleTier);
                Assert.That(tierFiltered.Count, Is.GreaterThan(0));
                for (int i = 0; i < tierFiltered.Count; i++)
                {
                    Assert.IsFalse(tierFiltered[i].IsStudied);
                    Assert.That(tierFiltered[i].Profile.Tier, Is.EqualTo(sampleTier));
                }

                for (int i = 0; i < firstDossiers.Count; i++)
                {
                    Assert.IsTrue(facade.ThreatCodex.TryRecordDangerZoneStudied(
                        specs[0].MapId, firstDossiers[i].MarkerId, out _));
                }

                for (int i = 1; i < specs.Count; i++)
                {
                    Assert.IsTrue(boot.SwitchMap(specs[i].MapId));
                    var otherLayout = boot.Driver.Facade.Session.Host.Layout;
                    var otherSummary = boot.Driver.Facade.ThreatCodex.BuildMapCodexSummary(otherLayout);
                    Assert.That(otherSummary.StudiedCount, Is.EqualTo(0));
                    Assert.That(otherSummary.RemainingStudySilverBonus, Is.GreaterThan(0));
                }
            }
        }
    }
}
