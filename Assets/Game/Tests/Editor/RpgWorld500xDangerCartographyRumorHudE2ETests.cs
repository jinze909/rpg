using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xDangerCartographyRumorHudE2ETests
    {
        [Test]
        public void PlayModeE2E_DangerCartographyAndRumorHudUpgradesSyncLiveWorldAndZeroLeak()
        {
            var hostGo = new GameObject("World500xDangerCartoRumorE2EHost");
            var panelRoot = new GameObject("World500xDangerCartoRumorPanelRoot");
            var ctrl = hostGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                // Warm up one-time UGUI/font caches across all tested tabs before steady-state leak baseline.
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });
                using (var warmupDanger = new RpgWorld500xDangerTacticalBriefHudPanel(panelRoot.transform, ctrl.Bootstrapper))
                using (var warmupCarto = new RpgWorld500xCartographyReputationHudPanel(panelRoot.transform, ctrl.Bootstrapper))
                using (var warmupRumor = new RpgWorld500xFieldRumorAndLoreHudPanel(panelRoot.transform, ctrl.Bootstrapper))
                {
                    warmupDanger.Show();
                    warmupDanger.TrySelectFilter(RpgWorld500xDangerBriefFilter.AllZones);
                    warmupDanger.TryClickClose();

                    warmupCarto.Show();
                    warmupCarto.TrySelectRegion(RpgWorldMapId.RedSaltHarbor);
                    warmupCarto.Hide();

                    warmupRumor.Show();
                    warmupRumor.TrySelectTab(RpgWorld500xRumorLoreTab.StorySpots);
                    warmupRumor.Hide();
                }

                ctrl.Shutdown();

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                DateTime clock = new DateTime(2026, 11, 26, 6, 30, 0, DateTimeKind.Utc);
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    utcNowProvider: () => clock,
                    overrideTileSlices: new Sprite[] { null });

                RpgWorld500xGrandRuntimeFacade facade = ctrl.Bootstrapper.Driver.Facade;

                // 1. DangerTacticalBriefHudPanel: payout summary & threat/tier criteria filter.
                using (var dangerPanel = new RpgWorld500xDangerTacticalBriefHudPanel(panelRoot.transform, ctrl.Bootstrapper))
                {
                    Assert.IsTrue(dangerPanel.Show(elapsedSeconds: 5f), "DangerTacticalBriefHudPanel.Show failed.");
                    int totalZones = dangerPanel.LastBrief.TotalDangerZones;
                    Assert.Greater(totalZones, 0);
                    Assert.AreEqual(totalZones, dangerPanel.LastPayoutSummary.RemainingZones);
                    Assert.Greater(dangerPanel.LastPayoutSummary.RemainingLockBreakSilver,
                        dangerPanel.LastPayoutSummary.RemainingStandardSilver);
                    StringAssert.Contains("清剿收益预估", dangerPanel.PayoutSummaryText);
                    StringAssert.Contains("余赏", dangerPanel.HeaderText);

                    RpgWorld500xThreatTier sampleTier = dangerPanel.LastBrief.Entries[0].Tier;
                    Assert.IsTrue(dangerPanel.TrySetThreatCriteria(minEffectiveThreatLevel: 1, tierFilter: sampleTier),
                        "TrySetThreatCriteria(tier) failed.");
                    Assert.Greater(dangerPanel.DisplayedRowCount, 0);
                    Assert.LessOrEqual(dangerPanel.DisplayedRowCount, totalZones);
                    Assert.IsTrue(dangerPanel.TrySetThreatCriteria(minEffectiveThreatLevel: 1, tierFilter: null),
                        "TrySetThreatCriteria(reset) failed.");
                    Assert.AreEqual(totalZones, dangerPanel.DisplayedRowCount);
                    Assert.IsTrue(dangerPanel.TryClickClose());
                }

                // 2. CartographyReputationHudPanel: 4-region overview & jump to highest-standing faction.
                ctrl.CampHaven.Reputation.AddPoints(RpgWorldMapId.RedSaltHarbor, 400);
                using (var cartoPanel = new RpgWorld500xCartographyReputationHudPanel(panelRoot.transform, ctrl.Bootstrapper))
                {
                    Assert.IsTrue(cartoPanel.Show(), "CartographyReputationHudPanel.Show failed.");
                    Assert.AreEqual(RpgWorldMapId.Soundwell, cartoPanel.SelectedMapId);
                    Assert.AreEqual(RpgWorldMapId.RedSaltHarbor, cartoPanel.LastWorldReputationOverview.HighestStandingMapId);
                    StringAssert.Contains("【四境声望总览】", cartoPanel.WorldReputationSummaryZh);
                    StringAssert.Contains("【四境声望总览】", cartoPanel.SummaryText);
                    Assert.Greater(cartoPanel.RemainingMilestoneSaltSilver, 0);

                    Assert.IsTrue(cartoPanel.TrySelectHighestStandingRegion(), "TrySelectHighestStandingRegion failed.");
                    Assert.AreEqual(RpgWorldMapId.RedSaltHarbor, cartoPanel.SelectedMapId);
                    StringAssert.Contains("赤盐港渔户与货帮", cartoPanel.HeaderText);
                    Assert.IsTrue(cartoPanel.Hide());
                }

                // 3. FieldRumorAndLoreHudPanel: counters, nearest guidance, UndiscoveredOnly filter & tracking.
                using (var rumorPanel = new RpgWorld500xFieldRumorAndLoreHudPanel(panelRoot.transform, ctrl.Bootstrapper))
                {
                    Assert.IsTrue(rumorPanel.Show(), "FieldRumorAndLoreHudPanel.Show failed.");
                    Assert.AreEqual(0, rumorPanel.HeardRumorCount);
                    Assert.AreEqual(6, rumorPanel.TotalRumorCount);
                    Assert.AreEqual(4, rumorPanel.TotalStorySpotCount);
                    Assert.IsFalse(string.IsNullOrEmpty(rumorPanel.NearestUndiscoveredLabelZh));

                    IReadOnlyList<RpgFieldNpcDef> npcs =
                        RpgWorld500xLivingWorldCatalog.GetFieldNpcs(RpgWorldMapId.Soundwell);
                    RpgFieldNpcDef firstNpc = npcs[0];
                    Assert.IsTrue(facade.ActivityDirector.LivingWorld.TryTalkToNearbyNpc(
                        new Vector2(firstNpc.Tile.x + 0.5f, firstNpc.Tile.y + 0.5f),
                        4f,
                        out _,
                        out _,
                        out _), "TryTalkToNearbyNpc failed.");

                    rumorPanel.Refresh();
                    Assert.AreEqual(1, rumorPanel.HeardRumorCount);
                    Assert.IsTrue(rumorPanel.TrySetUndiscoveredOnly(true), "TrySetUndiscoveredOnly(true) failed.");
                    Assert.AreEqual(5, rumorPanel.DisplayedRowCount);
                    Assert.IsTrue(rumorPanel.TrySetUndiscoveredOnly(false), "TrySetUndiscoveredOnly(false) failed.");
                    Assert.AreEqual(6, rumorPanel.DisplayedRowCount);
                    Assert.IsTrue(rumorPanel.TryClickTrackRumorTarget(firstNpc.Id), "TryClickTrackRumorTarget failed.");
                    Assert.AreEqual(firstNpc.RumorTargetMarkerId, facade.Session.Host.Tracker.TrackedMarkerId);
                    Assert.IsTrue(rumorPanel.Hide());
                }

                ctrl.Shutdown();
                Assert.AreEqual(0, panelRoot.transform.childCount);
                Assert.AreEqual(0, hostGo.transform.childCount);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
            }
            finally
            {
                ctrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(panelRoot);
                UnityEngine.Object.DestroyImmediate(hostGo);
            }
        }
    }
}