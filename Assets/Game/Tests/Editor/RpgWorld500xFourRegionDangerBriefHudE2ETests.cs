using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionDangerBriefHudE2ETests
    {
        [Test]
        public void DangerTacticalBriefHudPanel_TracksRemainingPayoutAndFirstClearReputationAcrossAllFourRegionsWithZeroLeak()
        {
            var root = new GameObject("World500xFourRegionDangerBriefE2ERoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupPanel = new RpgWorld500xDangerTacticalBriefHudPanel(root.transform, warmupBoot))
                {
                    warmupPanel.Show();
                    warmupPanel.Hide();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                var clearFlags = new Dictionary<string, int>(StringComparer.Ordinal);
                Func<string, int> readFlags = k => clearFlags.TryGetValue(k, out int v) ? v : 0;

                RpgWorldMapId[] regions =
                {
                    RpgWorldMapId.Soundwell,
                    RpgWorldMapId.RedSaltHarbor,
                    RpgWorldMapId.WhitewaterCrossing,
                    RpgWorldMapId.MineCamp
                };

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: regions[0],
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xDangerTacticalBriefHudPanel(
                    root.transform, boot, overrideGetInt: readFlags))
                {
                    Assert.IsTrue(panel.Show());

                    for (int i = 0; i < regions.Length; i++)
                    {
                        if (i > 0)
                        {
                            Assert.IsTrue(boot.SwitchMap(regions[i]));
                            Assert.IsTrue(panel.TryClickRefresh(5f + i));
                        }

                        int totalZones = panel.LastBrief.TotalDangerZones;
                        Assert.Greater(totalZones, 0, "Expected DangerZones in " + regions[i]);
                        Assert.AreEqual(totalZones, panel.DisplayedRowCount);
                        Assert.AreEqual(totalZones, panel.LastFirstClearSummary.RemainingDangerZones);
                        Assert.Greater(panel.RemainingFirstClearLockBreakReputation,
                            panel.RemainingFirstClearStandardReputation);
                        StringAssert.Contains("清剿收益预估", panel.PayoutSummaryText);
                        StringAssert.Contains("【首破悬赏余量】", panel.FirstClearSummaryText);
                        StringAssert.Contains("余声望", panel.HeaderText);

                        string firstMarkerId = panel.LastBrief.NearestUnclearedZone.MarkerId;
                        int initialSilver = panel.LastPayoutSummary.RemainingStandardSilver;
                        int initialRep = panel.RemainingFirstClearStandardReputation;

                        clearFlags[RpgWorld500xDangerVictoryRewardService.FirstClearKey(firstMarkerId)] = 1;
                        Assert.IsTrue(panel.TryClickRefresh(10f + i));
                        Assert.AreEqual(totalZones - 1, panel.DisplayedRowCount);
                        Assert.AreEqual(1, panel.LastFirstClearSummary.ClearedDangerZones);
                        Assert.Less(panel.LastPayoutSummary.RemainingStandardSilver, initialSilver);
                        Assert.Less(panel.RemainingFirstClearStandardReputation, initialRep);
                    }

                    Assert.IsTrue(panel.Hide());
                    Assert.IsFalse(panel.IsVisible);
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
