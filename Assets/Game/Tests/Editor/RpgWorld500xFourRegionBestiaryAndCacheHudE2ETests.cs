using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionBestiaryAndCacheHudE2ETests
    {
        [Test]
        public void BestiaryAndCacheShortcutHudPanels_TrackStudyLootAndShortcutProgressAcrossAllFourRegionsWithZeroLeak()
        {
            var root = new GameObject("World500xFourRegionBestiaryCacheE2ERoot");
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
                using (var bestiary = new RpgWorld500xBestiaryHudPanel(root.transform, boot))
                using (var cacheHud = new RpgWorld500xCacheAndShortcutHudPanel(root.transform, boot))
                {
                    for (int i = 0; i < regions.Length; i++)
                    {
                        RpgWorldMapId mapId = regions[i];
                        if (i > 0)
                        {
                            Assert.IsTrue(boot.SwitchMap(mapId));
                        }

                        // Bestiary: verify codex summary and batch study all unstudied dossiers in this region.
                        Assert.IsTrue(bestiary.Show());
                        int totalDossiers = bestiary.TotalDossierCount;
                        int expectedStudySilver = bestiary.RemainingStudySilver;
                        Assert.Greater(totalDossiers, 0);
                        Assert.Greater(expectedStudySilver, 0);
                        StringAssert.Contains("【图鉴考据】", bestiary.CodexSummaryText);
                        Assert.Greater(bestiary.RecommendedUnstudiedSupplyIds.Count, 0);

                        Assert.IsTrue(bestiary.TryStudyAllUnstudied(out int studiedNow, out int silverAwarded));
                        Assert.AreEqual(totalDossiers, studiedNow);
                        Assert.AreEqual(expectedStudySilver, silverAwarded);
                        Assert.AreEqual(0, bestiary.RemainingStudySilver);
                        Assert.AreEqual(0, bestiary.RecommendedUnstudiedSupplyIds.Count);
                        StringAssert.Contains("已全部收录", bestiary.CodexSummaryText);
                        Assert.IsTrue(bestiary.Hide());

                        // CacheAndShortcut: verify progress summary, loot first cache, unlock first shortcut.
                        Assert.IsTrue(cacheHud.Show());
                        int initialRemainingCacheSilver = cacheHud.RemainingCacheSaltSilver;
                        Assert.Greater(cacheHud.LastProgressSummary.TotalCacheCount, 0);
                        Assert.Greater(cacheHud.LastProgressSummary.TotalShortcutCount, 0);
                        Assert.Greater(initialRemainingCacheSilver, 0);
                        StringAssert.Contains("【秘藏与捷径】", cacheHud.ProgressSummaryText);
                        Assert.IsNotEmpty(cacheHud.NearestIncompleteId);

                        IReadOnlyList<RpgSupplyCacheDef> caches = RpgWorld500xContentCatalog.GetCaches(mapId);
                        IReadOnlyList<RpgWorldShortcutDef> shortcuts = RpgWorld500xContentCatalog.GetShortcuts(mapId);
                        var cacheCtrl = boot.Driver.Facade.ActivityDirector.CachesAndShortcuts;

                        Vector2 cachePos = new Vector2(caches[0].Tile.x + 0.5f, caches[0].Tile.y + 0.5f);
                        Assert.IsTrue(cacheCtrl.TryLootNearbyCache(cachePos, 6f, out _));
                        Vector2 shortcutPos = new Vector2(shortcuts[0].TriggerTile.x + 0.5f, shortcuts[0].TriggerTile.y + 0.5f);
                        Assert.IsTrue(cacheCtrl.TryUnlockNearbyShortcut(shortcutPos, 6f, out _));

                        cacheHud.Refresh();
                        Assert.AreEqual(1, cacheHud.LootedCacheCount);
                        Assert.AreEqual(1, cacheHud.UnlockedShortcutCount);
                        Assert.Less(cacheHud.RemainingCacheSaltSilver, initialRemainingCacheSilver);
                        Assert.IsTrue(cacheHud.Hide());
                    }
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
