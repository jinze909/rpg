using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xReconBountyAndDangerPayoutE2ETests
    {
        [Test]
        public void PlayModeE2E_ReconCategoryFilterBountySummaryAffordabilityAndDangerPayoutZeroLeak()
        {
            var hostGo = new GameObject("World500xReconBountyDangerE2ETestHost");
            var ctrl = hostGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });
                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { ReconPulsePressed = true }, 0.1f);
                ctrl.Shutdown();

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                DateTime clock = new DateTime(2026, 11, 26, 5, 0, 0, DateTimeKind.Utc);
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    utcNowProvider: () => clock,
                    overrideTileSlices: new Sprite[] { null });

                RpgWorld500xGrandRuntimeFacade facade = ctrl.Bootstrapper.Driver.Facade;
                facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.Clear);

                // 1. Live ReconPulseHudBar category filter + cooldown-free nearest preview.
                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { ReconPulsePressed = true }, 0.1f);
                Assert.Greater(ctrl.ReconPulseHud.LastPingCount, 0);
                RpgWorld500xReconPingKind inRangeKind = ctrl.ReconPulseHud.LastPings[0].Kind;

                // Advance session clock past default scan cooldown.
                ctrl.ProcessInputFrame(default, 12.0f);
                Assert.IsTrue(ctrl.ReconPulseHud.IsScanButtonInteractable);
                Assert.IsTrue(ctrl.ReconPulseHud.TrySetFilterKind(inRangeKind));
                Assert.AreEqual(inRangeKind, ctrl.ReconPulseHud.ActiveFilterKind);
                Assert.IsTrue(ctrl.ReconPulseHud.TryPreviewNearestForActiveFilter(out RpgWorld500xReconPing nearest));
                Assert.AreEqual(inRangeKind, nearest.Kind);
                Assert.IsTrue(ctrl.ReconPulseHud.IsScanButtonInteractable);

                ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { ReconPulsePressed = true }, 0.1f);
                Assert.Greater(ctrl.ReconPulseHud.LastPingCount, 0);
                StringAssert.Contains("[" + ctrl.ReconPulseHud.ActiveFilterLabelZh + "]", ctrl.ReconPulseHud.BannerText);
                for (int i = 0; i < ctrl.ReconPulseHud.LastPings.Count; i++)
                {
                    Assert.AreEqual(inRangeKind, ctrl.ReconPulseHud.LastPings[i].Kind);
                }

                // 2. Unlock 2 Soundwell shortcuts -> read-only BuildBountyBoardSummary -> claim -> GetMaxAffordableQuantity.
                IReadOnlyList<RpgWorldShortcutDef> shortcuts =
                    RpgWorld500xContentCatalog.GetShortcuts(RpgWorldMapId.Soundwell);
                Assert.GreaterOrEqual(shortcuts.Count, 2);
                for (int i = 0; i < 2; i++)
                {
                    Vector2 pos = new Vector2(shortcuts[i].TriggerTile.x + 0.5f, shortcuts[i].TriggerTile.y + 0.5f);
                    Assert.IsTrue(facade.ActivityDirector.CachesAndShortcuts.TryUnlockNearbyShortcut(pos, 6f, out _));
                }

                RpgWorld500xBountyBoardSummary beforeClaim =
                    facade.BountyAndSupply.BuildBountyBoardSummary(facade.GetCompletionSummary());
                Assert.AreEqual(0, beforeClaim.ClaimedCount);
                Assert.AreEqual(1, beforeClaim.ClaimableCount);
                Assert.AreEqual(60, beforeClaim.ClaimableSaltSilver);
                Assert.AreEqual(150, beforeClaim.RemainingSaltSilver);
                Assert.AreEqual(0, facade.BountyAndSupply.SaltSilverBalance);

                List<RpgWorld500xFieldBountyDef> claimed =
                    facade.BountyAndSupply.ClaimCompletedBounties(facade.GetCompletionSummary());
                Assert.AreEqual(1, claimed.Count);
                Assert.AreEqual(60, facade.BountyAndSupply.SaltSilverBalance);
                Assert.AreEqual(3, facade.BountyAndSupply.GetMaxAffordableQuantity("gauze"));       // 60 / 18 = 3
                Assert.AreEqual(1, facade.BountyAndSupply.GetMaxAffordableQuantity("iron_clamps")); // 60 / 35 = 1
                Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded,
                    facade.BountyAndSupply.TryPurchaseSupply("iron_clamps", 1));
                Assert.AreEqual(25, facade.BountyAndSupply.SaltSilverBalance);
                Assert.AreEqual(0, facade.BountyAndSupply.GetMaxAffordableQuantity("iron_clamps"));

                RpgWorld500xBountyBoardSummary afterClaim =
                    facade.BountyAndSupply.BuildBountyBoardSummary(facade.GetCompletionSummary());
                Assert.AreEqual(1, afterClaim.ClaimedCount);
                Assert.AreEqual(0, afterClaim.ClaimableCount);
                Assert.AreEqual(90, afterClaim.RemainingSaltSilver);

                // 3. DangerZoneTacticalBriefService SummarizeRemainingPayouts & FilterEntries.
                RpgWorld500xRegionThreatBrief brief =
                    RpgWorld500xDangerZoneTacticalBriefService.BuildRegionBrief(ctrl.Bootstrapper, elapsedSeconds: 5f);
                RpgWorld500xDangerClearancePayoutSummary payout =
                    RpgWorld500xDangerZoneTacticalBriefService.SummarizeRemainingPayouts(brief);
                Assert.AreEqual(brief.TotalDangerZones, payout.RemainingZones);
                Assert.Greater(payout.RemainingStandardSilver, 0);
                Assert.Greater(payout.RemainingLockBreakSilver, payout.RemainingStandardSilver);
                Assert.AreEqual(payout.RemainingLockBreakSilver - payout.RemainingStandardSilver,
                    payout.LockBreakBonusSilver);
                StringAssert.Contains("清剿收益预估", payout.SummaryZh);

                RpgWorld500xThreatTier sampleTier = brief.Entries[0].Tier;
                int sampleLevel = brief.Entries[0].EffectiveThreatLevel;
                List<RpgWorld500xDangerZoneTacticalEntry> filtered =
                    RpgWorld500xDangerZoneTacticalBriefService.FilterEntries(
                        brief,
                        unclearedOnly: true,
                        minEffectiveThreatLevel: sampleLevel,
                        tierFilter: sampleTier);
                Assert.Greater(filtered.Count, 0);
                for (int i = 0; i < filtered.Count; i++)
                {
                    Assert.IsFalse(filtered[i].IsCleared);
                    Assert.AreEqual(sampleTier, filtered[i].Tier);
                    Assert.GreaterOrEqual(filtered[i].EffectiveThreatLevel, sampleLevel);
                }

                ctrl.Shutdown();
                Assert.AreEqual(0, hostGo.transform.childCount);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
            }
            finally
            {
                ctrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(hostGo);
            }
        }
    }
}