using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xBestiaryAndCacheHudE2ETests
    {
        [Test]
        public void BestiaryCodexFilterAndCacheShortcutTracker_IntegrateEndToEndWithZeroLeak()
        {
            var root = new GameObject("World500xBestiaryCacheHudE2ERoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupBestiary = new RpgWorld500xBestiaryHudPanel(root.transform, warmupBoot))
                using (var warmupCache = new RpgWorld500xCacheAndShortcutHudPanel(root.transform, warmupBoot))
                {
                    warmupBestiary.Show();
                    warmupBestiary.Hide();
                    warmupCache.Show();
                    warmupCache.Hide();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var bestiary = new RpgWorld500xBestiaryHudPanel(root.transform, boot))
                using (var cachePanel = new RpgWorld500xCacheAndShortcutHudPanel(root.transform, boot))
                {
                    RpgWorld500xGrandRuntimeFacade facade = boot.Driver.Facade;

                    // 1. BestiaryHudPanel codex summary, tier filtering, and batch study reward accounting.
                    Assert.IsTrue(bestiary.Show());
                    int totalDossiers = bestiary.TotalDossierCount;
                    int initialStudySilver = bestiary.RemainingStudySilver;
                    Assert.GreaterOrEqual(totalDossiers, 2);
                    Assert.Greater(initialStudySilver, 0);
                    Assert.AreEqual(totalDossiers, bestiary.LastCodexSummary.TotalDossiers);
                    StringAssert.Contains("【图鉴考据】", bestiary.CodexSummaryText);
                    Assert.Greater(bestiary.RecommendedUnstudiedSupplyIds.Count, 0);

                    List<RpgWorld500xDangerZoneDossier> dossiers =
                        facade.ThreatCodex.BuildMapDossiers(facade.Session.Host.Layout);
                    Assert.IsTrue(bestiary.TryClickStudyDossier(dossiers[0].MarkerId));
                    Assert.AreEqual(1, bestiary.StudiedCount);
                    Assert.Less(bestiary.RemainingStudySilver, initialStudySilver);

                    Assert.IsTrue(bestiary.TrySetUnstudiedOnly(true));
                    Assert.AreEqual(totalDossiers - 1, bestiary.DisplayedDossierRowCount);

                    RpgWorld500xThreatTier sampleTier = dossiers[1].Profile.Tier;
                    Assert.IsTrue(bestiary.TrySetTierFilter(sampleTier));
                    Assert.AreEqual(sampleTier, bestiary.TierFilter);
                    Assert.Greater(bestiary.DisplayedDossierRowCount, 0);
                    Assert.LessOrEqual(bestiary.DisplayedDossierRowCount, totalDossiers - 1);

                    Assert.IsTrue(bestiary.TrySetTierFilter(null));
                    Assert.IsNull(bestiary.TierFilter);
                    Assert.AreEqual(totalDossiers - 1, bestiary.DisplayedDossierRowCount);

                    Assert.IsTrue(bestiary.TryStudyAllUnstudied(out int studiedNow, out int silverAwarded));
                    Assert.AreEqual(totalDossiers - 1, studiedNow);
                    Assert.Greater(silverAwarded, 0);
                    Assert.AreEqual(totalDossiers, bestiary.StudiedCount);
                    Assert.AreEqual(0, bestiary.RemainingStudySilver);
                    Assert.AreEqual(0, bestiary.DisplayedDossierRowCount);
                    Assert.AreEqual(0, bestiary.RecommendedUnstudiedSupplyIds.Count);
                    StringAssert.Contains("已全部收录", bestiary.CodexSummaryText);
                    Assert.AreEqual(initialStudySilver, facade.BountyAndSupply.SaltSilverBalance);

                    Assert.IsTrue(bestiary.Hide());
                    Assert.IsFalse(bestiary.IsVisible);

                    // 2. CacheAndShortcutHudPanel controller progress summary and nearest-target tracking.
                    Assert.IsTrue(cachePanel.Show());
                    Assert.AreEqual(RpgWorld500xCacheShortcutTab.HiddenCaches, cachePanel.ActiveTab);
                    Assert.AreEqual(12, cachePanel.LastProgressSummary.TotalCacheCount);
                    Assert.AreEqual(4, cachePanel.LastProgressSummary.TotalShortcutCount);
                    Assert.AreEqual(0, cachePanel.LootedCacheCount);
                    Assert.AreEqual(0, cachePanel.UnlockedShortcutCount);
                    StringAssert.Contains("【秘藏与捷径】", cachePanel.ProgressSummaryText);

                    int initialCacheSilver = cachePanel.RemainingCacheSaltSilver;
                    Assert.Greater(initialCacheSilver, 0);
                    Assert.IsNotEmpty(cachePanel.NearestIncompleteId);
                    Assert.GreaterOrEqual(cachePanel.NearestIncompleteDistanceTiles, 0f);

                    RpgWorld500xCachesAndShortcutsController cacheCtrl = facade.ActivityDirector.CachesAndShortcuts;
                    Vector2 playerPos = facade.Session.Host.PlayerPosition;
                    Assert.IsTrue(cacheCtrl.TryFindNearestUnlootedCache(playerPos, out RpgSupplyCacheDef nearestCache, out _));
                    Assert.AreEqual(nearestCache.Id, cachePanel.NearestIncompleteId);

                    Assert.IsTrue(cacheCtrl.TryLootNearbyCache(
                        new Vector2(nearestCache.Tile.x + 0.5f, nearestCache.Tile.y + 0.5f),
                        6f,
                        out _));
                    cachePanel.Refresh();
                    Assert.AreEqual(1, cachePanel.LootedCacheCount);
                    Assert.AreEqual(11, cachePanel.LastProgressSummary.RemainingCacheCount);
                    Assert.AreEqual(initialCacheSilver - nearestCache.SaltSilverReward, cachePanel.RemainingCacheSaltSilver);
                    Assert.AreNotEqual(nearestCache.Id, cachePanel.NearestIncompleteId);

                    Assert.IsTrue(cachePanel.TrySelectTab(RpgWorld500xCacheShortcutTab.Shortcuts));
                    Assert.AreEqual(RpgWorld500xCacheShortcutTab.Shortcuts, cachePanel.ActiveTab);
                    Assert.AreEqual(4, cachePanel.DisplayedRowCount);
                    Assert.IsTrue(cacheCtrl.TryFindNearestLockedShortcut(playerPos, out RpgWorldShortcutDef nearestSc, out _));
                    Assert.AreEqual(nearestSc.Id, cachePanel.NearestIncompleteId);

                    Assert.IsTrue(cacheCtrl.TryUnlockNearbyShortcut(
                        new Vector2(nearestSc.TriggerTile.x + 0.5f, nearestSc.TriggerTile.y + 0.5f),
                        6f,
                        out _));
                    cachePanel.Refresh();
                    Assert.AreEqual(1, cachePanel.UnlockedShortcutCount);
                    Assert.AreEqual(3, cachePanel.LastProgressSummary.LockedShortcutCount);

                    Assert.IsTrue(cachePanel.TrySetIncompleteOnly(true));
                    Assert.AreEqual(3, cachePanel.DisplayedRowCount);
                    Assert.AreNotEqual(nearestSc.Id, cachePanel.NearestIncompleteId);
                    Assert.IsFalse(string.IsNullOrEmpty(cachePanel.NearestIncompleteLabelZh));

                    Assert.IsTrue(cachePanel.Hide());
                    Assert.IsFalse(cachePanel.IsVisible);
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
