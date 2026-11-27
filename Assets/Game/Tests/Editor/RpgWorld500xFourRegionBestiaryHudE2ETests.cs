using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionBestiaryHudE2ETests
    {
        [Test]
        public void BestiaryHudPanel_AcrossAllFourRegions_StudiesSingleAndBatchFiltersByTierAndUnstudiedAndCreditsSilver()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            for (int i = 0; i < specs.Count; i++)
            {
                var mapId = specs[i].MapId;
                var root = new GameObject("FourRegionBestiaryRoot_" + mapId);
                try
                {
                    using (var boot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: mapId,
                        overrideTileSlices: new Sprite[] { null }))
                    using (var panel = new RpgWorld500xBestiaryHudPanel(root.transform, boot))
                    {
                        var facade = boot.Driver.Facade;

                        Assert.IsFalse(panel.TrySetUnstudiedOnly(true));
                        Assert.IsFalse(panel.TrySetTierFilter(RpgWorld500xThreatTier.HazardBoss));
                        Assert.IsFalse(panel.TryStudyAllUnstudied(out _, out _));

                        Assert.IsTrue(panel.Show());
                        int total = panel.TotalDossierCount;
                        Assert.That(total, Is.GreaterThan(1));
                        Assert.That(panel.StudiedCount, Is.EqualTo(0));
                        int initialRemainingSilver = panel.RemainingStudySilver;
                        Assert.That(initialRemainingSilver, Is.GreaterThan(0));
                        StringAssert.Contains(specs[i].DisplayNameZh, panel.HeaderText);
                        StringAssert.Contains("【图鉴考据】", panel.CodexSummaryText);
                        Assert.That(panel.RecommendedUnstudiedSupplyIds.Count, Is.GreaterThan(0));

                        var dossiers = facade.ThreatCodex.BuildMapDossiers(facade.Session.Host.Layout);
                        int firstBonus = dossiers[0].Profile.CodexStudySilverBonus;
                        Assert.IsTrue(panel.TryClickStudyDossier(dossiers[0].MarkerId));
                        Assert.That(panel.StudiedCount, Is.EqualTo(1));
                        Assert.That(panel.RemainingStudySilver, Is.EqualTo(initialRemainingSilver - firstBonus));

                        Assert.IsTrue(panel.TrySetUnstudiedOnly(true));
                        Assert.IsTrue(panel.UnstudiedOnly);
                        Assert.That(panel.DisplayedDossierRowCount, Is.EqualTo(total - 1));

                        var sampleTier = dossiers[1].Profile.Tier;
                        Assert.IsTrue(panel.TrySetTierFilter(sampleTier));
                        Assert.That(panel.TierFilter, Is.EqualTo(sampleTier));
                        Assert.That(panel.DisplayedDossierRowCount, Is.GreaterThan(0));
                        Assert.That(panel.DisplayedDossierRowCount, Is.LessThanOrEqualTo(total - 1));

                        Assert.IsTrue(panel.TrySetTierFilter(null));
                        Assert.IsNull(panel.TierFilter);
                        Assert.That(panel.DisplayedDossierRowCount, Is.EqualTo(total - 1));

                        Assert.IsTrue(panel.TryStudyAllUnstudied(out int batchCount, out int batchSilver));
                        Assert.That(batchCount, Is.EqualTo(total - 1));
                        Assert.That(batchSilver, Is.EqualTo(initialRemainingSilver - firstBonus));
                        Assert.That(panel.StudiedCount, Is.EqualTo(total));
                        Assert.That(panel.RemainingStudySilver, Is.EqualTo(0));
                        Assert.That(panel.DisplayedDossierRowCount, Is.EqualTo(0));
                        Assert.That(panel.RecommendedUnstudiedSupplyIds.Count, Is.EqualTo(0));
                        StringAssert.Contains("已全部收录", panel.CodexSummaryText);
                        StringAssert.Contains("已批量研读", panel.DetailText);
                        Assert.That(facade.BountyAndSupply.SaltSilverBalance, Is.EqualTo(initialRemainingSilver));

                        Assert.IsFalse(panel.TryStudyAllUnstudied(out _, out _));

                        Assert.IsTrue(panel.TrySetUnstudiedOnly(false));
                        Assert.That(panel.DisplayedDossierRowCount, Is.EqualTo(total));
                    }

                    Assert.That(root.transform.childCount, Is.EqualTo(0));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        [Test]
        public void BestiaryHudPanel_AcrossAllFourRegions_CanvasGroupLockBlocksStudyAndFilterActionsUntilUnlocked()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            for (int i = 0; i < specs.Count; i++)
            {
                var mapId = specs[i].MapId;
                var root = new GameObject("FourRegionBestiaryGuard_" + mapId);
                try
                {
                    using (var boot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: mapId,
                        overrideTileSlices: new Sprite[] { null }))
                    using (var panel = new RpgWorld500xBestiaryHudPanel(root.transform, boot))
                    {
                        var facade = boot.Driver.Facade;
                        var dossiers = facade.ThreatCodex.BuildMapDossiers(facade.Session.Host.Layout);
                        var first = dossiers[0];

                        Assert.IsTrue(panel.Show());
                        var group = root.AddComponent<CanvasGroup>();
                        group.interactable = false;

                        Assert.IsFalse(panel.TryClickStudyDossier(first.MarkerId));
                        Assert.IsFalse(panel.TrySetUnstudiedOnly(true));
                        Assert.IsFalse(panel.TrySetTierFilter(first.Profile.Tier));
                        Assert.IsFalse(panel.TryStudyAllUnstudied(out _, out _));
                        Assert.That(panel.StudiedCount, Is.EqualTo(0));
                        Assert.That(facade.BountyAndSupply.SaltSilverBalance, Is.EqualTo(0));

                        group.interactable = true;
                        Assert.IsTrue(panel.TryClickStudyDossier(first.MarkerId));
                        Assert.That(panel.StudiedCount, Is.EqualTo(1));
                        Assert.That(facade.BountyAndSupply.SaltSilverBalance, Is.EqualTo(first.Profile.CodexStudySilverBonus));
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }
    }
}
