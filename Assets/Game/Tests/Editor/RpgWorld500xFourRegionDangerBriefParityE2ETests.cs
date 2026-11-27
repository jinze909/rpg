using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionDangerBriefParityE2ETests
    {
        [Test]
        public void AllFourRegions_DangerBriefAndFirstClearPreviewSharePersistedCompletionRuleWithZeroLeak()
        {
            // Warm up one-time runtime caches before steady-state leak baseline.
            using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                RpgWorld500xDangerZoneTacticalBriefService.BuildRegionBrief(warmupBoot);
            }

            int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

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
            {
                for (int r = 0; r < regions.Length; r++)
                {
                    RpgWorldMapId mapId = regions[r];
                    if (r > 0)
                    {
                        Assert.IsTrue(boot.SwitchMap(mapId), "Expected switch to " + mapId);
                    }

                    var flags = new Dictionary<string, int>(StringComparer.Ordinal);
                    Func<string, int> read = k => flags.TryGetValue(k, out int v) ? v : 0;

                    var baseline = RpgWorld500xDangerZoneTacticalBriefService.BuildRegionBrief(boot, overrideGetInt: read);
                    Assert.Greater(baseline.TotalDangerZones, 0, "Expected danger zones in " + mapId);
                    string markerId = baseline.NearestUnclearedZone.MarkerId;
                    string key = RpgWorld500xDangerVictoryRewardService.FirstClearKey(markerId);

                    foreach (int persistedValue in new[] { 0, 1, 2, -1 })
                    {
                        flags[key] = persistedValue;

                        var brief = RpgWorld500xDangerZoneTacticalBriefService.BuildRegionBrief(boot, overrideGetInt: read);
                        var preview = RpgWorld500xDangerVictoryRewardService.PreviewVictoryReward(
                            mapId, markerId, false, read);
                        var remaining = RpgWorld500xDangerVictoryRewardService.SummarizeRemainingFirstClearRewards(
                            boot.Driver.Facade.Session.Host.Layout, read);
                        var payouts = RpgWorld500xDangerZoneTacticalBriefService.SummarizeRemainingPayouts(brief);

                        Assert.AreEqual(persistedValue != 0, !preview.IsFirstClear);
                        Assert.AreEqual(remaining.ClearedDangerZones, brief.ClearedDangerZones);
                        Assert.AreEqual(remaining.RemainingDangerZones, payouts.RemainingZones);
                        Assert.AreEqual(remaining.RemainingStandardSaltSilver, payouts.RemainingStandardSilver);
                        Assert.AreEqual(remaining.RemainingLockBreakSaltSilver, payouts.RemainingLockBreakSilver);

                        bool matchedEntry = false;
                        for (int i = 0; i < brief.Entries.Count; i++)
                        {
                            var entry = brief.Entries[i];
                            if (entry.MarkerId != markerId) continue;
                            matchedEntry = true;
                            Assert.AreEqual(!preview.IsFirstClear, entry.IsCleared);
                            Assert.AreEqual(preview.SaltSilverReward, entry.EstimatedStandardSilver);
                        }

                        Assert.IsTrue(matchedEntry, "Expected marker entry in brief for " + markerId);
                        Assert.AreEqual(persistedValue, flags[key], "Preview must not rewrite saved flags.");
                    }
                }
            }

            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
        }
    }
}
