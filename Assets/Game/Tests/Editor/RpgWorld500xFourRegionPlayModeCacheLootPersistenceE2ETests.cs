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

                    IReadOnlyList<RpgSupplyCacheDef> caches = RpgWorld500xContentCatalog.GetCaches(spec.MapId);
                    Assert.Greater(caches.Count, 0, spec.MapId.ToString());
                    // Use a reachable cache tile and the actual facade interaction path:
                    // the low-level cache controller records loot, but the facade credits the wallet.
                    RpgSupplyCacheDef cache = default;
                    bool placed = false;
                    foreach (var candidate in caches)
                    {
                        if (facade.Session.Host.TryPlaceAtWalkableTile(candidate.Tile))
                        {
                            cache = candidate;
                            placed = true;
                            break;
                        }
                    }

                    Assert.IsTrue(placed, "Expected a walkable cache tile in " + spec.MapId);
                    Vector2 cachePos = facade.Session.Host.PlayerPosition;

                    int beforeSilver = shop.SaltSilverBalance;
                    Assert.IsFalse(cachesSvc.IsCacheLooted(cache.Id), spec.MapId.ToString());

                    // 1. Loot the regional cache once and verify silver credit + looted state.
                    Assert.IsTrue(facade.TryInteractUnified(0.5f, out var looted, out _), spec.MapId.ToString());
                    Assert.AreEqual(RpgWorld500xInteractionKind.CacheLooted, looted.Kind, spec.MapId.ToString());
                    Assert.AreEqual(cache.Id, looted.EntityId, spec.MapId.ToString());
                    Assert.AreEqual(cache.SaltSilverReward, looted.SaltSilverGranted, spec.MapId.ToString());
                    Assert.IsTrue(cachesSvc.IsCacheLooted(cache.Id), spec.MapId.ToString());
                    int afterLootSilver = shop.SaltSilverBalance;
                    Assert.GreaterOrEqual(afterLootSilver, beforeSilver + cache.SaltSilverReward, spec.MapId.ToString());

                    // 2. Duplicate loot attempt must fail cleanly without double-crediting.
                    facade.TryInteractUnified(0.5f, out var repeated, out _);
                    Assert.AreNotEqual(RpgWorld500xInteractionKind.CacheLooted, repeated.Kind, spec.MapId.ToString());
                    Assert.IsFalse(cachesSvc.TryLootNearbyCache(cachePos, 0.5f, out _), spec.MapId.ToString());
                    Assert.AreEqual(afterLootSilver, shop.SaltSilverBalance, spec.MapId.ToString());

                    // 3. Export save snapshot and reload into a fresh bootstrapper initialized on another map
                    // so TryImportSaveJson rebuilds the target region facade from the imported save store.
                    string saved = ctrl.Bootstrapper.ExportSaveJson();
                    RpgWorldMapId otherMap = spec.MapId == RpgWorldMapId.Soundwell
                        ? RpgWorldMapId.RedSaltHarbor
                        : RpgWorldMapId.Soundwell;
                    using (var restored = new RpgWorld500xSceneBootstrapper(
                        initialMapId: otherMap,
                        overrideTileSlices: new Sprite[] { null }))
                    {
                        Assert.IsTrue(restored.TryImportSaveJson(saved, out string error), error);
                        Assert.AreEqual(spec.MapId, restored.Driver.Facade.CurrentMapId, spec.MapId.ToString());
                        Assert.AreEqual(1, restored.Store[cache.MakinomKey], spec.MapId.ToString());
                        var restoredCaches = restored.Driver.Facade.ActivityDirector.CachesAndShortcuts;
                        Assert.IsTrue(restoredCaches.IsCacheLooted(cache.Id), spec.MapId.ToString());
                        Assert.AreEqual(
                            afterLootSilver,
                            restored.Driver.Facade.BountyAndSupply.SaltSilverBalance,
                            spec.MapId.ToString());
                        Assert.IsTrue(restored.Driver.Facade.Session.Host.TryPlaceAtWalkableTile(cache.Tile));
                        restored.Driver.Facade.TryInteractUnified(0.5f, out var repeatedAfterLoad, out _);
                        Assert.AreNotEqual(RpgWorld500xInteractionKind.CacheLooted, repeatedAfterLoad.Kind, spec.MapId.ToString());
                        Assert.IsFalse(restoredCaches.TryLootNearbyCache(cachePos, 0.5f, out _), spec.MapId.ToString());
                        Assert.AreEqual(afterLootSilver, restored.Driver.Facade.BountyAndSupply.SaltSilverBalance, spec.MapId.ToString());
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
