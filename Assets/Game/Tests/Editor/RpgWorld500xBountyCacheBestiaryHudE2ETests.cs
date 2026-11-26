using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xBountyCacheBestiaryHudE2ETests
    {
        [Test]
        public void PlayModeE2E_BountyCacheAndBestiaryHudUpgradesSyncLiveWorldAndZeroLeak()
        {
            var hostGo = new GameObject("World500xBountyCacheBestiaryE2EHost");
            var panelRoot = new GameObject("World500xBountyCacheBestiaryPanelRoot");
            var ctrl = hostGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                // Warm up one-time UGUI/font caches across all tested tabs before steady-state leak baseline.
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });
                using (var warmupCache = new RpgWorld500xCacheAndShortcutHudPanel(panelRoot.transform, ctrl.Bootstrapper))
                using (var warmupBounty = new RpgWorld500xBountyBoardHudPanel(panelRoot.transform, ctrl.Bootstrapper))
                using (var warmupBestiary = new RpgWorld500xBestiaryHudPanel(panelRoot.transform, ctrl.Bootstrapper))
                {
                    warmupCache.Show();
                    warmupCache.TrySelectTab(RpgWorld500xCacheShortcutTab.Shortcuts);
                    warmupCache.Hide();

                    warmupBounty.Show();
                    warmupBounty.TrySelectScope(RpgWorld500xBountyBoardScope.AllRegions);
                    warmupBounty.TryClickClose();

                    warmupBestiary.Show();
                    warmupBestiary.Hide();
                }

                ctrl.Shutdown();

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                DateTime clock = new DateTime(2026, 11, 26, 6, 0, 0, DateTimeKind.Utc);
                ctrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    utcNowProvider: () => clock,
                    overrideTileSlices: new Sprite[] { null });

                RpgWorld500xGrandRuntimeFacade facade = ctrl.Bootstrapper.Driver.Facade;

                // 1. CacheAndShortcutHudPanel: remaining silver, nearest target, and IncompleteOnly filter.
                using (var cachePanel = new RpgWorld500xCacheAndShortcutHudPanel(panelRoot.transform, ctrl.Bootstrapper))
                {
                    Assert.IsTrue(cachePanel.Show(), "CacheAndShortcutHudPanel.Show failed.");
                    Assert.AreEqual(12, cachePanel.DisplayedRowCount, "Expected 12 Soundwell caches.");
                    Assert.Greater(cachePanel.RemainingCacheSaltSilver, 0, "Expected positive remaining cache silver.");
                    Assert.IsFalse(string.IsNullOrEmpty(cachePanel.NearestIncompleteLabelZh), "Expected nearest cache label.");
                    StringAssert.Contains("未领补给盐银", cachePanel.SummaryText);

                    Assert.IsTrue(cachePanel.TrySelectTab(RpgWorld500xCacheShortcutTab.Shortcuts), "Switch to Shortcuts tab failed.");
                    Assert.AreEqual(4, cachePanel.DisplayedRowCount, "Expected 4 Soundwell shortcuts.");

                    IReadOnlyList<RpgWorldShortcutDef> shortcuts =
                        RpgWorld500xContentCatalog.GetShortcuts(RpgWorldMapId.Soundwell);
                    Assert.GreaterOrEqual(shortcuts.Count, 2);
                    for (int i = 0; i < 2; i++)
                    {
                        Vector2 pos = new Vector2(shortcuts[i].TriggerTile.x + 0.5f, shortcuts[i].TriggerTile.y + 0.5f);
                        Assert.IsTrue(facade.ActivityDirector.CachesAndShortcuts.TryUnlockNearbyShortcut(pos, 6f, out _),
                            "Failed to unlock shortcut index " + i);
                    }

                    cachePanel.Refresh();
                    Assert.AreEqual(2, cachePanel.UnlockedShortcutCount);
                    Assert.IsTrue(cachePanel.TrySetIncompleteOnly(true), "TrySetIncompleteOnly(true) failed.");
                    Assert.AreEqual(2, cachePanel.DisplayedRowCount);
                    Assert.IsTrue(cachePanel.Hide());
                }

                // 2. BountyBoardHudPanel: live BuildBountyBoardSummary readout and idempotent claim.
                int silverBeforeBountyClaim = facade.BountyAndSupply.SaltSilverBalance;
                using (var bountyPanel = new RpgWorld500xBountyBoardHudPanel(panelRoot.transform, ctrl.Bootstrapper))
                {
                    Assert.IsTrue(bountyPanel.Show(), "BountyBoardHudPanel.Show failed.");
                    Assert.AreEqual(1, bountyPanel.ClaimableCount);
                    Assert.AreEqual(60, bountyPanel.ClaimableSaltSilver);
                    Assert.AreEqual(150, bountyPanel.RemainingSaltSilver);
                    StringAssert.Contains("+60 盐银", bountyPanel.ClaimButtonLabelText);
                    StringAssert.Contains("可领 1 项（+60 盐银）", bountyPanel.ActiveMapSummaryZh);

                    Assert.IsTrue(bountyPanel.TryClickClaimEligible(), "TryClickClaimEligible failed.");
                    Assert.AreEqual(1, bountyPanel.ClaimedCount);
                    Assert.AreEqual(0, bountyPanel.ClaimableCount);
                    Assert.AreEqual(0, bountyPanel.ClaimableSaltSilver);
                    Assert.AreEqual(90, bountyPanel.RemainingSaltSilver);
                    Assert.AreEqual("领取本境已达标悬赏", bountyPanel.ClaimButtonLabelText);
                    Assert.AreEqual(silverBeforeBountyClaim + 60, facade.BountyAndSupply.SaltSilverBalance);
                    Assert.IsTrue(bountyPanel.TryClickClose());
                }

                // 3. BestiaryHudPanel: remaining study silver, UnstudiedOnly filter, and batch study.
                int silverBeforeBestiary = facade.BountyAndSupply.SaltSilverBalance;
                using (var bestiaryPanel = new RpgWorld500xBestiaryHudPanel(panelRoot.transform, ctrl.Bootstrapper))
                {
                    Assert.IsTrue(bestiaryPanel.Show(), "BestiaryHudPanel.Show failed.");
                    int totalDossiers = bestiaryPanel.TotalDossierCount;
                    Assert.Greater(totalDossiers, 1);
                    int totalStudySilver = bestiaryPanel.RemainingStudySilver;
                    Assert.Greater(totalStudySilver, 0);

                    List<RpgWorld500xDangerZoneDossier> dossiers =
                        facade.ThreatCodex.BuildMapDossiers(facade.Session.Host.Layout);
                    Assert.IsTrue(bestiaryPanel.TryClickStudyDossier(dossiers[0].MarkerId), "TryClickStudyDossier failed.");
                    Assert.IsTrue(bestiaryPanel.TrySetUnstudiedOnly(true), "TrySetUnstudiedOnly(true) failed.");
                    Assert.AreEqual(totalDossiers - 1, bestiaryPanel.DisplayedDossierRowCount);

                    Assert.IsTrue(bestiaryPanel.TryStudyAllUnstudied(out int batchCount, out _), "TryStudyAllUnstudied failed.");
                    Assert.AreEqual(totalDossiers - 1, batchCount);
                    Assert.AreEqual(totalDossiers, bestiaryPanel.StudiedCount);
                    Assert.AreEqual(0, bestiaryPanel.RemainingStudySilver);
                    Assert.AreEqual(0, bestiaryPanel.DisplayedDossierRowCount);
                    Assert.AreEqual(silverBeforeBestiary + totalStudySilver, facade.BountyAndSupply.SaltSilverBalance);
                    Assert.IsTrue(bestiaryPanel.Hide());
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