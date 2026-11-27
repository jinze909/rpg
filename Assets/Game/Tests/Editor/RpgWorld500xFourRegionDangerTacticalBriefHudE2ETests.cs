using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionDangerTacticalBriefHudE2ETests
    {
        [Test]
        public void DangerTacticalBriefHudPanel_AcrossAllFourRegions_TracksFirstClearPayoutsReputationFiltersAndZeroLeakDispose()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            for (int i = 0; i < specs.Count; i++)
            {
                var mapId = specs[i].MapId;
                var clearFlags = new Dictionary<string, int>(StringComparer.Ordinal);
                Func<string, int> read = k => clearFlags.TryGetValue(k, out int v) ? v : 0;
                var root = new GameObject("FourRegionDangerBriefRoot_" + mapId);
                try
                {
                    using (var boot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: mapId,
                        overrideTileSlices: new Sprite[] { null }))
                    using (var panel = new RpgWorld500xDangerTacticalBriefHudPanel(root.transform, boot, read))
                    {
                        Assert.IsFalse(panel.TrySelectFilter(RpgWorld500xDangerBriefFilter.AllZones));
                        Assert.IsFalse(panel.TrySetThreatCriteria(3, RpgWorld500xThreatTier.ElitePatrol));
                        Assert.IsFalse(panel.TryClickRefresh(1f));
                        Assert.IsFalse(panel.TryClickClose());

                        Assert.IsTrue(panel.Show());
                        int totalZones = panel.LastBrief.TotalDangerZones;
                        Assert.That(totalZones, Is.GreaterThan(0));
                        Assert.That(panel.DisplayedRowCount, Is.EqualTo(totalZones));
                        Assert.That(panel.LastPayoutSummary.RemainingZones, Is.EqualTo(totalZones));
                        Assert.That(
                            panel.LastPayoutSummary.RemainingLockBreakSilver,
                            Is.GreaterThan(panel.LastPayoutSummary.RemainingStandardSilver));
                        Assert.That(
                            panel.RemainingFirstClearLockBreakReputation,
                            Is.GreaterThan(panel.RemainingFirstClearStandardReputation));
                        StringAssert.Contains(specs[i].DisplayNameZh, panel.HeaderText);
                        StringAssert.Contains("清剿收益预估", panel.PayoutSummaryText);
                        StringAssert.Contains("【首破悬赏余量】", panel.FirstClearSummaryText);

                        string firstMarkerId = panel.LastBrief.NearestUnclearedZone.MarkerId;
                        int initialSilver = panel.LastPayoutSummary.RemainingStandardSilver;
                        int initialRep = panel.RemainingFirstClearStandardReputation;
                        clearFlags[RpgWorld500xDangerVictoryRewardService.FirstClearKey(firstMarkerId)] = 1;

                        Assert.IsTrue(panel.TryClickRefresh(5f));
                        Assert.That(panel.DisplayedRowCount, Is.EqualTo(totalZones - 1));
                        Assert.That(panel.LastPayoutSummary.RemainingStandardSilver, Is.LessThan(initialSilver));
                        Assert.That(panel.LastFirstClearSummary.ClearedDangerZones, Is.EqualTo(1));
                        Assert.That(panel.RemainingFirstClearStandardReputation, Is.LessThan(initialRep));

                        Assert.IsTrue(panel.TrySelectFilter(RpgWorld500xDangerBriefFilter.AllZones));
                        Assert.That(panel.DisplayedRowCount, Is.EqualTo(totalZones));
                        StringAssert.Contains("已首破 1/" + totalZones, panel.HeaderText);

                        var sampleTier = panel.LastBrief.Entries[0].Tier;
                        Assert.IsTrue(panel.TrySetThreatCriteria(minEffectiveThreatLevel: 1, tierFilter: sampleTier));
                        Assert.That(panel.TierFilter, Is.EqualTo(sampleTier));
                        Assert.That(panel.DisplayedRowCount, Is.GreaterThan(0));
                        Assert.That(panel.DisplayedRowCount, Is.LessThanOrEqualTo(totalZones));

                        Assert.IsTrue(panel.TrySetThreatCriteria(minEffectiveThreatLevel: 1, tierFilter: null));
                        Assert.IsNull(panel.TierFilter);
                        Assert.That(panel.DisplayedRowCount, Is.EqualTo(totalZones));

                        Assert.IsTrue(panel.TryClickClose());
                        Assert.IsFalse(panel.IsVisible);
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
        public void DangerTacticalBriefHudPanel_AcrossAllFourRegions_ParentCanvasGroupLockBlocksFilterCriteriaRefreshAndClose()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            for (int i = 0; i < specs.Count; i++)
            {
                var mapId = specs[i].MapId;
                var root = new GameObject("FourRegionDangerBriefGuard_" + mapId);
                try
                {
                    using (var boot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: mapId,
                        overrideTileSlices: new Sprite[] { null }))
                    using (var panel = new RpgWorld500xDangerTacticalBriefHudPanel(root.transform, boot))
                    {
                        Assert.IsTrue(panel.Show());
                        var group = root.AddComponent<CanvasGroup>();
                        group.interactable = false;

                        Assert.IsFalse(panel.TrySelectFilter(RpgWorld500xDangerBriefFilter.AllZones));
                        Assert.IsFalse(panel.TrySetThreatCriteria(4, RpgWorld500xThreatTier.HazardBoss));
                        Assert.IsFalse(panel.TryClickRefresh(6f));
                        Assert.IsFalse(panel.TryClickClose());
                        Assert.That(panel.ActiveFilter, Is.EqualTo(RpgWorld500xDangerBriefFilter.UnclearedOnly));
                        Assert.That(panel.MinEffectiveThreatLevel, Is.EqualTo(1));
                        Assert.IsNull(panel.TierFilter);
                        Assert.IsTrue(panel.IsVisible);

                        group.interactable = true;
                        Assert.IsTrue(panel.TryClickClose());
                        Assert.IsFalse(panel.IsVisible);
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
