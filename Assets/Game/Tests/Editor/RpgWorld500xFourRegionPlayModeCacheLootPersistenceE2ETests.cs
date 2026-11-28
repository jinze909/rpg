using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionPlayModeCacheLootPersistenceE2ETests
    {
        [Test]
        public void AllFourRegions_PlayModeControllerCacheLootIsIdempotentAndPersistsAcrossSaveReload()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var host = new GameObject("FourRegionCacheLootPersist_" + spec.MapId);
                var ctrl = host.AddComponent<RpgWorld500xPlayModeController>();
                try
                {
                    ctrl.Initialize(
                        overrideMap: spec.MapId,
                        saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                        overrideTileSlices: new Sprite[] { null });

                    var facade = ctrl.Bootstrapper.Driver.Facade;
                    var cachesSvc = facade.ActivityDirector.CachesAndShortcuts;
                    var shop = facade.BountyAndSupply;

                    IReadOnlyList<RpgWorldCacheDef> caches = RpgWorld500xContentCatalog.GetCaches(spec.MapId);
                    Assert.Greater(caches.Count, 0, spec.MapId.ToString());
                    RpgWorldCacheDef cache = caches[0];
                    Vector2 cachePos = new Vector2(cache.Tile.x + 0.5f, cache.Tile.y + 0.5f);

                    int beforeSilver = shop.SaltSilverBalance;
                    Assert.IsFalse(cachesSvc.IsCacheLooted(cache.Id), spec.MapId.ToString());

                    // 1. Loot the regional cache once and verify silver credit + looted state.
                    Assert.IsTrue(cachesSvc.TryLootNearbyCache(cachePos, 6f, out var looted), spec.MapId.ToString());
                    Assert.AreEqual(cache.Id, looted.Id, spec.MapId.ToString());
                    Assert.IsTrue(cachesSvc.IsCacheLooted(cache.Id), spec.MapId.ToString());
                    int afterLootSilver = shop.SaltSilverBalance;
                    Assert.Greater(afterLootSilver, beforeSilver, spec.MapId.ToString());

                    // 2. Duplicate loot attempt must fail cleanly without double-crediting.
                    Assert.IsFalse(cachesSvc.TryLootNearbyCache(cachePos, 6f, out _), spec.MapId.ToString());
                    Assert.AreEqual(afterLootSilver, shop.SaltSilverBalance, spec.MapId.ToString());

                    // 3. Export save snapshot and reload into a fresh bootstrapper; cache stays looted.
                    string saved = ctrl.Bootstrapper.ExportSaveJson();
                    using (var restored = new RpgWorld500xSceneBootstrapper(
                        initialMapId: spec.MapId,
                        overrideTileSlices: new Sprite[] { null }))
                    {
                        Assert.IsTrue(restored.TryImportSaveJson(saved, out string error), error);
                        var restoredCaches = restored.Driver.Facade.ActivityDirector.CachesAndShortcuts;
                        Assert.IsTrue(restoredCaches.IsCacheLooted(cache.Id), spec.MapId.ToString());
                        Assert.AreEqual(
                            afterLootSilver,
                            restored.Driver.Facade.BountyAndSupply.SaltSilverBalance,
                            spec.MapId.ToString());
                        Assert.IsFalse(restoredCaches.TryLootNearbyCache(cachePos, 6f, out _), spec.MapId.ToString());
                    }

                    ctrl.Shutdown();
                    Assert.AreEqual(0, host.transform.childCount, spec.MapId.ToString());
                }
                finally
                {
                    ctrl.Shutdown();
                    Object.DestroyImmediate(host);
                }
            }
        }
    }
}
