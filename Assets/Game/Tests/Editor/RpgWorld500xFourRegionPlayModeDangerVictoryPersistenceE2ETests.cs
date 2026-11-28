using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionPlayModeDangerVictoryPersistenceE2ETests
    {
        [Test]
        public void AllFourRegions_PlayModeControllerDangerVictoryPersistsFirstClearLootAndRejectsDuplicate()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var host = new GameObject("FourRegionDangerVictory_" + spec.MapId);
                var ctrl = host.AddComponent<RpgWorld500xPlayModeController>();
                try
                {
                    ctrl.Initialize(
                        overrideMap: spec.MapId,
                        saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                        overrideTileSlices: new Sprite[] { null });

                    var facade = ctrl.Bootstrapper.Driver.Facade;
                    RpgMapLandmarkMarker dangerMarker = FindFirstDangerMarker(facade.Session.Host.Layout.Markers);
                    Assert.IsNotNull(dangerMarker, spec.MapId.ToString());

                    Vector2 pos = new Vector2(dangerMarker.Tile.x + 0.5f, dangerMarker.Tile.y + 0.5f);
                    Assert.IsTrue(
                        facade.Session.OrkBridge.TryTriggerDangerZoneEncounter(dangerMarker.Id, pos),
                        spec.MapId.ToString());

                    Assert.IsTrue(
                        ctrl.TryResolveActiveDangerVictory(true, out var encounter, out var reward),
                        spec.MapId.ToString());

                    Assert.AreEqual(dangerMarker.Id, encounter.MarkerId, spec.MapId.ToString());
                    Assert.IsTrue(reward.IsFirstClear, spec.MapId.ToString());
                    Assert.Greater(reward.SaltSilverReward, 0, spec.MapId.ToString());
                    Assert.Greater(reward.ReputationGain, 0, spec.MapId.ToString());
                    Assert.AreEqual(reward.SaltSilverReward, facade.BountyAndSupply.SaltSilverBalance, spec.MapId.ToString());
                    Assert.AreEqual(reward.ReputationGain, ctrl.CampHaven.Reputation.GetPoints(spec.MapId), spec.MapId.ToString());
                    StringAssert.Contains("破锁精通", ctrl.LastDangerVictoryBannerZh, spec.MapId.ToString());

                    string clearKey = RpgWorld500xDangerVictoryRewardService.FirstClearKey(encounter.MarkerId);
                    Assert.AreEqual(1, ctrl.Bootstrapper.Store[clearKey], spec.MapId.ToString());
                    Assert.AreEqual(1, reward.BonusSupplyQuantity, spec.MapId.ToString());
                    Assert.IsNotEmpty(reward.BonusSupplyId, spec.MapId.ToString());
                    Assert.AreEqual(
                        reward.BonusSupplyQuantity,
                        facade.BountyAndSupply.GetOwnedSupplyCount(reward.BonusSupplyId),
                        spec.MapId.ToString());

                    // Duplicate resolution must be rejected cleanly.
                    Assert.IsFalse(ctrl.TryResolveActiveDangerVictory(true, out _, out _), spec.MapId.ToString());

                    // Save-reload must preserve first-clear fact and bonus supply across all four regions.
                    string saved = ctrl.Bootstrapper.ExportSaveJson();
                    using (var restored = new RpgWorld500xSceneBootstrapper(
                        initialMapId: spec.MapId,
                        overrideTileSlices: new Sprite[] { null }))
                    {
                        Assert.IsTrue(restored.TryImportSaveJson(saved, out string error), error);
                        Assert.AreEqual(1, restored.Store[clearKey], spec.MapId.ToString());
                        Assert.AreEqual(
                            reward.BonusSupplyQuantity,
                            restored.Driver.Facade.BountyAndSupply.GetOwnedSupplyCount(reward.BonusSupplyId),
                            spec.MapId.ToString());

                        var preview = RpgWorld500xDangerVictoryRewardService.PreviewVictoryReward(
                            spec.MapId,
                            encounter.MarkerId,
                            false,
                            key => restored.Store.TryGetValue(key, out int value) ? value : 0);
                        Assert.IsFalse(preview.IsFirstClear, spec.MapId.ToString());
                        Assert.AreEqual(0, preview.BonusSupplyQuantity, spec.MapId.ToString());
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

        private static RpgMapLandmarkMarker FindFirstDangerMarker(IReadOnlyList<RpgMapLandmarkMarker> markers)
        {
            for (int i = 0; i < markers.Count; i++)
            {
                if (markers[i].Category == RpgMapMarkerCategory.DangerZone)
                    return markers[i];
            }
            return null;
        }
    }
}
