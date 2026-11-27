using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionCampfireDigestRoutingE2ETests
    {
        [Test]
        public void AllFourRegions_CampfireDigestRoutesToUnfinishedRegionAndCompletesCleanlyWithZeroLeak()
        {
            // Warm up one-time runtime caches before steady-state leak baseline.
            using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                RpgWorld500xCampfireJournalDigestService.BuildDigest(warmupBoot);
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

            int verifiedRegions = 0;
            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: regions[0],
                overrideTileSlices: new Sprite[] { null }))
            {
                var baseFlags = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var entry in boot.Store)
                {
                    baseFlags[entry.Key] = entry.Value;
                }

                foreach (var spec in RpgExpandedWorldScale.AllSpecs)
                {
                    foreach (var cache in RpgWorld500xContentCatalog.GetCaches(spec.MapId))
                        baseFlags[cache.MakinomKey] = 1;
                    foreach (var shortcut in RpgWorld500xContentCatalog.GetShortcuts(spec.MapId))
                        baseFlags[shortcut.MakinomKey] = 1;
                    foreach (var npc in RpgWorld500xLivingWorldCatalog.GetFieldNpcs(spec.MapId))
                        baseFlags[npc.RumorMakinomKey] = 1;
                    foreach (var spot in RpgWorld500xLivingWorldCatalog.GetStorySpots(spec.MapId))
                        baseFlags[spot.MakinomKey] = 1;
                }

                foreach (var milestone in RpgWorld500xExpeditionMilestoneService.AllMilestones)
                    baseFlags[milestone.MakinomKey] = 1;

                for (int i = 0; i < regions.Length; i++)
                {
                    RpgWorldMapId origin = regions[i];
                    RpgWorldMapId target = regions[(i + 1) % regions.Length];

                    var flags = new Dictionary<string, int>(baseFlags, StringComparer.Ordinal);
                    string targetCacheKey = RpgWorld500xContentCatalog.GetCaches(target)[0].MakinomKey;
                    flags[targetCacheKey] = 0;

                    if (boot.Driver.Facade.CurrentMapId != origin)
                    {
                        Assert.IsTrue(boot.SwitchMap(origin));
                    }

                    var facade = boot.Driver.Facade;
                    facade.Atmosphere.SetWeather(origin, RpgWorld500xWeatherKind.Clear);
                    Vector2 player = facade.Session.Host.PlayerPosition;

                    var snapshot = RpgWorld500xSaveSnapshotSerializer.Capture(
                        origin, player, flags, facade.Atmosphere);
                    Assert.IsTrue(
                        boot.TryImportSaveJson(RpgWorld500xSaveSnapshotSerializer.Serialize(snapshot), out string err),
                        err);

                    var guided = RpgWorld500xCampfireJournalDigestService.BuildDigest(boot);
                    StringAssert.Contains("本境暂无未探索野外目标", guided.NextObjectiveHintZh);
                    StringAssert.Contains(
                        RpgExpandedWorldScale.GetSpec(target).DisplayNameZh,
                        guided.NextObjectiveHintZh);

                    // Once the final remaining target is completed, digest reports all four regions finished.
                    flags[targetCacheKey] = 1;
                    snapshot = RpgWorld500xSaveSnapshotSerializer.Capture(
                        origin, player, flags, facade.Atmosphere);
                    Assert.IsTrue(
                        boot.TryImportSaveJson(RpgWorld500xSaveSnapshotSerializer.Serialize(snapshot), out err),
                        err);

                    var done = RpgWorld500xCampfireJournalDigestService.BuildDigest(boot);
                    StringAssert.Contains("四境暂无未探索野外目标", done.NextObjectiveHintZh);

                    verifiedRegions++;
                }
            }

            Assert.AreEqual(4, verifiedRegions);
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
        }
    }
}
