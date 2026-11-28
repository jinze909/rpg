using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionDangerTacticalBriefCanvasGuardE2ETests
    {
        [Test]
        public void AllFourRegions_DangerBriefRejectsDisabledCanvasLockedAndHiddenInputUntilRestored()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var root = new GameObject("DangerBriefCanvasGuard_" + spec.MapId);
                try
                {
                    var clearFlags = new Dictionary<string, int>(StringComparer.Ordinal);
                    Func<string, int> read = k => clearFlags.TryGetValue(k, out int v) ? v : 0;

                    using (var boot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: spec.MapId,
                        overrideTileSlices: new Sprite[] { null }))
                    using (var panel = new RpgWorld500xDangerTacticalBriefHudPanel(root.transform, boot, read))
                    {
                        Assert.IsTrue(panel.Show(), spec.MapId.ToString());
                        var canvas = root.transform.Find("World500xDangerTacticalBriefHudPanel").GetComponent<Canvas>();
                        Assert.IsNotNull(canvas, spec.MapId.ToString());
                        var modal = canvas.transform.Find("DangerBriefModalBox");
                        Assert.IsNotNull(modal, spec.MapId.ToString());

                        var allBtn = modal.Find("Tab_AllZones").GetComponent<Button>();
                        var refreshBtn = modal.Find("RefreshDangerBriefButton").GetComponent<Button>();
                        var closeBtn = modal.Find("CloseDangerBriefButton").GetComponent<Button>();
                        Assert.IsNotNull(allBtn, spec.MapId.ToString());
                        Assert.IsNotNull(refreshBtn, spec.MapId.ToString());
                        Assert.IsNotNull(closeBtn, spec.MapId.ToString());

                        int total = panel.LastBrief.TotalDangerZones;
                        int silver = panel.LastPayoutSummary.RemainingStandardSilver;
                        int reputation = panel.RemainingFirstClearStandardReputation;
                        string header = panel.HeaderText;
                        string ribbon = panel.SummaryRibbonText;
                        Assert.Greater(total, 0, spec.MapId.ToString());
                        string marker = panel.LastBrief.NearestUnclearedZone.MarkerId;

                        // 1. Disabled owning Canvas rejects raw callbacks and public APIs while keeping state stable.
                        canvas.enabled = false;
                        Assert.IsTrue(refreshBtn.IsInteractable(), spec.MapId.ToString());
                        clearFlags[RpgWorld500xDangerVictoryRewardService.FirstClearKey(marker)] = 1;
                        AssertBlocked(panel, allBtn, refreshBtn, closeBtn, true, total, silver, reputation, header, ribbon);

                        // Programmatic synchronization remains valid while input is disabled.
                        panel.Refresh(5f);
                        int syncedRows = panel.DisplayedRowCount;
                        int syncedSilver = panel.LastPayoutSummary.RemainingStandardSilver;
                        int syncedReputation = panel.RemainingFirstClearStandardReputation;
                        string syncedHeader = panel.HeaderText;
                        string syncedRibbon = panel.SummaryRibbonText;
                        Assert.AreEqual(total - 1, syncedRows, spec.MapId.ToString());
                        Assert.Less(syncedSilver, silver, spec.MapId.ToString());
                        Assert.Less(syncedReputation, reputation, spec.MapId.ToString());
                        canvas.enabled = true;

                        // 2. Ancestor CanvasGroup lock rejects raw callbacks and public APIs.
                        var group = root.AddComponent<CanvasGroup>();
                        group.interactable = false;
                        AssertBlocked(panel, allBtn, refreshBtn, closeBtn,
                            true, syncedRows, syncedSilver, syncedReputation, syncedHeader, syncedRibbon);
                        group.interactable = true;

                        // 3. Hidden panel rejects raw callbacks and public APIs.
                        Assert.IsTrue(panel.Hide(), spec.MapId.ToString());
                        AssertBlocked(panel, allBtn, refreshBtn, closeBtn,
                            false, syncedRows, syncedSilver, syncedReputation, syncedHeader, syncedRibbon);
                        Assert.IsTrue(panel.Show(), spec.MapId.ToString());

                        // 4. Restored panel accepts tab switch, threat criteria filtering, refresh, and close.
                        allBtn.onClick.Invoke();
                        Assert.AreEqual(RpgWorld500xDangerBriefFilter.AllZones, panel.ActiveFilter, spec.MapId.ToString());
                        Assert.AreEqual(total, panel.DisplayedRowCount, spec.MapId.ToString());
                        Assert.IsTrue(panel.TrySetThreatCriteria(1, panel.LastBrief.Entries[0].Tier), spec.MapId.ToString());
                        Assert.IsTrue(panel.TrySetThreatCriteria(), spec.MapId.ToString());
                        Assert.IsTrue(panel.TryClickRefresh(6f), spec.MapId.ToString());
                        closeBtn.onClick.Invoke();
                        Assert.IsFalse(panel.IsVisible, spec.MapId.ToString());
                    }

                    Assert.AreEqual(0, root.transform.childCount, spec.MapId.ToString());
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static void AssertBlocked(
            RpgWorld500xDangerTacticalBriefHudPanel panel,
            Button allBtn,
            Button refreshBtn,
            Button closeBtn,
            bool expectedVisible,
            int expectedRows,
            int expectedSilver,
            int expectedReputation,
            string expectedHeader,
            string expectedRibbon)
        {
            refreshBtn.onClick.Invoke();
            allBtn.onClick.Invoke();
            closeBtn.onClick.Invoke();
            Assert.IsFalse(panel.TryClickRefresh(5f));
            Assert.IsFalse(panel.TrySelectFilter(RpgWorld500xDangerBriefFilter.AllZones));
            Assert.IsFalse(panel.TrySetThreatCriteria(4, RpgWorld500xThreatTier.HazardBoss));
            Assert.IsFalse(panel.TryClickClose());

            Assert.AreEqual(expectedVisible, panel.IsVisible);
            Assert.AreEqual(RpgWorld500xDangerBriefFilter.UnclearedOnly, panel.ActiveFilter);
            Assert.AreEqual(1, panel.MinEffectiveThreatLevel);
            Assert.IsNull(panel.TierFilter);
            Assert.AreEqual(expectedRows, panel.DisplayedRowCount);
            Assert.AreEqual(expectedSilver, panel.LastPayoutSummary.RemainingStandardSilver);
            Assert.AreEqual(expectedReputation, panel.RemainingFirstClearStandardReputation);
            Assert.AreEqual(expectedHeader, panel.HeaderText);
            Assert.AreEqual(expectedRibbon, panel.SummaryRibbonText);
        }
    }
}
