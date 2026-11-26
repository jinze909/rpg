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

                    // Mark first DangerZone cleared -> UnclearedOnly drops by 1; AllZones keeps full count.
                    string firstMarkerId = panel.LastBrief.NearestUnclearedZone.MarkerId;
                    clearFlags[RpgWorld500xDangerVictoryRewardService.FirstClearKey(firstMarkerId)] = 1;
                    Assert.IsTrue(panel.TryClickRefresh(5f));
                    Assert.AreEqual(totalZones - 1, panel.DisplayedRowCount);

                    // Parent CanvasGroup lock prevents tab switch, refresh, close, AND direct stale callbacks cleanly.
                    CanvasGroup group = root.AddComponent<CanvasGroup>();
                    group.interactable = false;
                    Assert.IsFalse(panel.TrySelectFilter(RpgWorld500xDangerBriefFilter.AllZones));
                    Assert.IsFalse(panel.TryClickRefresh(6f));
                    Assert.IsFalse(panel.TryClickClose());
                    allZonesBtn.onClick.Invoke();
                    Assert.AreEqual(RpgWorld500xDangerBriefFilter.UnclearedOnly, panel.ActiveFilter);
                    group.interactable = true;

                    Assert.IsTrue(panel.TrySelectFilter(RpgWorld500xDangerBriefFilter.AllZones));
                    Assert.AreEqual(totalZones, panel.DisplayedRowCount);
                    Assert.IsTrue(panel.HeaderText.Contains("已首破 1/" + totalZones));

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
