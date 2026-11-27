using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xDangerTacticalBriefHudPanelTests
    {
        [Test]
        public void DangerTacticalBriefHudPanel_DisabledCanvasRejectsRefreshFiltersAndCloseUntilRestored()
        {
            var root = new GameObject("DangerBriefDisabledCanvasRoot");
            try
            {
                var clearFlags = new Dictionary<string, int>(StringComparer.Ordinal);
                Func<string, int> read = k => clearFlags.TryGetValue(k, out int v) ? v : 0;
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xDangerTacticalBriefHudPanel(root.transform, boot, read))
                {
                    Assert.IsTrue(panel.Show());
                    var canvas = root.transform.Find("World500xDangerTacticalBriefHudPanel").GetComponent<Canvas>();
                    var modal = canvas.transform.Find("DangerBriefModalBox");
                    var all = modal.Find("Tab_AllZones").GetComponent<Button>();
                    var refresh = modal.Find("RefreshDangerBriefButton").GetComponent<Button>();
                    var close = modal.Find("CloseDangerBriefButton").GetComponent<Button>();
                    int total = panel.LastBrief.TotalDangerZones;
                    int silver = panel.LastPayoutSummary.RemainingStandardSilver;
                    int reputation = panel.RemainingFirstClearStandardReputation;
                    string header = panel.HeaderText;
                    string ribbon = panel.SummaryRibbonText;
                    Assert.Greater(total, 0);
                    string marker = panel.LastBrief.NearestUnclearedZone.MarkerId;

                    canvas.enabled = false;
                    Assert.IsTrue(refresh.IsInteractable());
                    clearFlags[RpgWorld500xDangerVictoryRewardService.FirstClearKey(marker)] = 1;
                    refresh.onClick.Invoke();
                    all.onClick.Invoke();
                    close.onClick.Invoke();
                    Assert.IsFalse(panel.TryClickRefresh(5f));
                    Assert.IsFalse(panel.TrySelectFilter(RpgWorld500xDangerBriefFilter.AllZones));
                    Assert.IsFalse(panel.TrySetThreatCriteria(4, RpgWorld500xThreatTier.HazardBoss));
                    Assert.IsFalse(panel.TryClickClose());
                    Assert.IsTrue(panel.IsVisible);
                    Assert.AreEqual(RpgWorld500xDangerBriefFilter.UnclearedOnly, panel.ActiveFilter);
                    Assert.AreEqual(1, panel.MinEffectiveThreatLevel);
                    Assert.IsNull(panel.TierFilter);
                    Assert.AreEqual(total, panel.DisplayedRowCount);
                    Assert.AreEqual(silver, panel.LastPayoutSummary.RemainingStandardSilver);
                    Assert.AreEqual(reputation, panel.RemainingFirstClearStandardReputation);
                    Assert.AreEqual(header, panel.HeaderText);
                    Assert.AreEqual(ribbon, panel.SummaryRibbonText);

                    // Programmatic synchronization remains valid while input is disabled.
                    panel.Refresh(5f);
                    Assert.AreEqual(total - 1, panel.DisplayedRowCount);
                    Assert.Less(panel.LastPayoutSummary.RemainingStandardSilver, silver);
                    Assert.Less(panel.RemainingFirstClearStandardReputation, reputation);

                    canvas.enabled = true;
                    all.onClick.Invoke();
                    Assert.AreEqual(RpgWorld500xDangerBriefFilter.AllZones, panel.ActiveFilter);
                    Assert.AreEqual(total, panel.DisplayedRowCount);
                    Assert.IsTrue(panel.TrySetThreatCriteria(1, panel.LastBrief.Entries[0].Tier));
                    Assert.IsTrue(panel.TrySetThreatCriteria());
                    Assert.IsTrue(panel.TryClickRefresh(6f));
                    close.onClick.Invoke();
                    Assert.IsFalse(panel.IsVisible);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DangerTacticalBriefHudPanel_FiltersClearedZonesRespectsCanvasGroupLockAndZeroLeak()
        {
            var root = new GameObject("World500xDangerTacticalBriefHudPanelTestRoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupPanel = new RpgWorld500xDangerTacticalBriefHudPanel(root.transform, warmupBoot))
                {
                    warmupPanel.Show();
                    warmupPanel.TrySelectFilter(RpgWorld500xDangerBriefFilter.AllZones);
                    warmupPanel.TryClickClose();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                var clearFlags = new Dictionary<string, int>(StringComparer.Ordinal);
                Func<string, int> read = k => clearFlags.TryGetValue(k, out int v) ? v : 0;

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xDangerTacticalBriefHudPanel(root.transform, boot, read))
                {
                    // Hidden panel rejects Try* calls AND direct stale tab callbacks cleanly.
                    Assert.IsFalse(panel.TrySelectFilter(RpgWorld500xDangerBriefFilter.AllZones));
                    Assert.IsFalse(panel.TrySetThreatCriteria(3, RpgWorld500xThreatTier.ElitePatrol));
                    Assert.IsFalse(panel.TryClickRefresh(1f));
                    Assert.IsFalse(panel.TryClickClose());
                    Button allZonesBtn = root.transform
                        .Find("World500xDangerTacticalBriefHudPanel/DangerBriefModalBox/Tab_AllZones")
                        .GetComponent<Button>();
                    allZonesBtn.onClick.Invoke();
                    Assert.AreEqual(RpgWorld500xDangerBriefFilter.UnclearedOnly, panel.ActiveFilter);

                    Assert.IsTrue(panel.Show());
                    int totalZones = panel.LastBrief.TotalDangerZones;
                    Assert.Greater(totalZones, 0);
                    Assert.AreEqual(totalZones, panel.DisplayedRowCount);
                    Assert.IsTrue(panel.SummaryRibbonText.Contains("最近未清剿"));
                    Assert.AreEqual(totalZones, panel.LastPayoutSummary.RemainingZones);
                    Assert.Greater(panel.LastPayoutSummary.RemainingLockBreakSilver,
                        panel.LastPayoutSummary.RemainingStandardSilver);
                    StringAssert.Contains("清剿收益预估", panel.PayoutSummaryText);
                    StringAssert.Contains("余赏", panel.HeaderText);
                    Assert.AreEqual(totalZones, panel.LastFirstClearSummary.RemainingDangerZones);
                    Assert.Greater(panel.RemainingFirstClearLockBreakReputation,
                        panel.RemainingFirstClearStandardReputation);
                    StringAssert.Contains("【首破悬赏余量】", panel.FirstClearSummaryText);
                    StringAssert.Contains("余声望", panel.HeaderText);

                    // Mark first DangerZone cleared -> UnclearedOnly drops by 1; AllZones keeps full count.
                    string firstMarkerId = panel.LastBrief.NearestUnclearedZone.MarkerId;
                    int initialRemainingSilver = panel.LastPayoutSummary.RemainingStandardSilver;
                    int initialRemainingRep = panel.RemainingFirstClearStandardReputation;
                    clearFlags[RpgWorld500xDangerVictoryRewardService.FirstClearKey(firstMarkerId)] = 1;
                    Assert.IsTrue(panel.TryClickRefresh(5f));
                    Assert.AreEqual(totalZones - 1, panel.DisplayedRowCount);
                    Assert.Less(panel.LastPayoutSummary.RemainingStandardSilver, initialRemainingSilver);
                    Assert.AreEqual(1, panel.LastFirstClearSummary.ClearedDangerZones);
                    Assert.Less(panel.RemainingFirstClearStandardReputation, initialRemainingRep);

                    // Parent CanvasGroup lock prevents tab switch, criteria change, refresh, close, AND direct stale callbacks cleanly.
                    CanvasGroup group = root.AddComponent<CanvasGroup>();
                    group.interactable = false;
                    Assert.IsFalse(panel.TrySelectFilter(RpgWorld500xDangerBriefFilter.AllZones));
                    Assert.IsFalse(panel.TrySetThreatCriteria(4, RpgWorld500xThreatTier.HazardBoss));
                    Assert.IsFalse(panel.TryClickRefresh(6f));
                    Assert.IsFalse(panel.TryClickClose());
                    allZonesBtn.onClick.Invoke();
                    Assert.AreEqual(RpgWorld500xDangerBriefFilter.UnclearedOnly, panel.ActiveFilter);
                    Assert.AreEqual(1, panel.MinEffectiveThreatLevel);
                    Assert.IsNull(panel.TierFilter);
                    group.interactable = true;

                    Assert.IsTrue(panel.TrySelectFilter(RpgWorld500xDangerBriefFilter.AllZones));
                    Assert.AreEqual(totalZones, panel.DisplayedRowCount);
                    Assert.IsTrue(panel.HeaderText.Contains("已首破 1/" + totalZones));

                    // Apply threat tier filter matching first entry and then reset back to all entries.
                    RpgWorld500xThreatTier sampleTier = panel.LastBrief.Entries[0].Tier;
                    Assert.IsTrue(panel.TrySetThreatCriteria(minEffectiveThreatLevel: 1, tierFilter: sampleTier));
                    Assert.AreEqual(sampleTier, panel.TierFilter);
                    Assert.Greater(panel.DisplayedRowCount, 0);
                    Assert.LessOrEqual(panel.DisplayedRowCount, totalZones);

                    Assert.IsTrue(panel.TrySetThreatCriteria(minEffectiveThreatLevel: 1, tierFilter: null));
                    Assert.AreEqual(totalZones, panel.DisplayedRowCount);

                    Assert.IsTrue(panel.TryClickClose());
                    Assert.IsFalse(panel.IsVisible);
                    Assert.IsFalse(panel.TryClickClose());
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
