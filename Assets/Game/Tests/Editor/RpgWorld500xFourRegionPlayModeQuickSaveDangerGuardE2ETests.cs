using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionPlayModeQuickSaveDangerGuardE2ETests
    {
        [Test]
        public void AllFourRegions_QuickSavePreservesCheckpointDuringEncounterAndRecoversAfterVictory()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);
            foreach (RpgExpandedMapSpec spec in specs)
            {
                var host = new GameObject("FourRegionQuickSaveDangerGuard_" + spec.MapId);
                var ctrl = host.AddComponent<RpgWorld500xPlayModeController>();
                var storage = new RpgWorld500xInMemorySaveSlotStorage();
                try
                {
                    ctrl.Initialize(
                        overrideMap: spec.MapId,
                        saveStorage: storage,
                        overrideTileSlices: new Sprite[] { null });

                    var facade = ctrl.Bootstrapper.Driver.Facade;
                    var bridge = facade.Session.Host.Bridge;
                    string key = RpgWorld500xSaveSlotManager.GetStorageKey(RpgWorld500xSaveSlotId.AutoSave);
                    facade.BountyAndSupply.AddSaltSilver(100);
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { QuickAutoSavePressed = true }, 0f);
                    StringAssert.Contains("自动存档成功", ctrl.LastAutoSaveStatusZh);
                    Assert.IsTrue(storage.TryRead(key, out string checkpoint));
                    Assert.IsNotEmpty(checkpoint);

                    // The live state differs from the checkpoint when combat begins.
                    facade.BountyAndSupply.AddSaltSilver(25);
                    Assert.IsTrue(TryFindDangerMarker(facade.Session.Host.Layout.Markers, out var marker));
                    var pos = new Vector2(marker.Tile.x + 0.5f, marker.Tile.y + 0.5f);
                    Assert.IsTrue(bridge.TryTriggerDangerZoneEncounter(marker.Id, pos), spec.MapId.ToString());
                    Assert.IsTrue(bridge.IsInDangerEncounter);

                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { QuickAutoSavePressed = true }, 0f);
                    StringAssert.Contains("自动存档失败", ctrl.LastAutoSaveStatusZh);
                    StringAssert.Contains("遭遇战进行中", ctrl.LastAutoSaveStatusZh);
                    Assert.IsTrue(storage.TryRead(key, out string duringEncounter));
                    Assert.AreEqual(checkpoint, duringEncounter, spec.MapId.ToString());
                    Assert.IsTrue(bridge.IsInDangerEncounter);
                    Assert.AreEqual(125, facade.BountyAndSupply.SaltSilverBalance);

                    Assert.IsTrue(ctrl.TryResolveActiveDangerVictory(true, out _, out _));
                    Assert.IsFalse(bridge.IsInDangerEncounter);
                    int settledBalance = facade.BountyAndSupply.SaltSilverBalance;
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { QuickAutoSavePressed = true }, 0f);
                    StringAssert.Contains("自动存档成功", ctrl.LastAutoSaveStatusZh);
                    Assert.IsTrue(storage.TryRead(key, out string afterVictory));
                    Assert.AreNotEqual(checkpoint, afterVictory);

                    // Read the newly saved checkpoint through the actual slot manager.
                    facade.BountyAndSupply.AddSaltSilver(1);
                    Assert.IsTrue(ctrl.SaveSlotManager.TryLoadSlot(ctrl.Bootstrapper,
                        RpgWorld500xSaveSlotId.AutoSave, out _, out string error), error);
                    Assert.AreEqual(settledBalance, facade.BountyAndSupply.SaltSilverBalance);
                    Assert.AreEqual(spec.MapId, facade.CurrentMapId);
                    ctrl.Shutdown();
                    Assert.AreEqual(0, host.transform.childCount);
                }
                finally
                {
                    ctrl.Shutdown();
                    Object.DestroyImmediate(host);
                }
            }
        }

        private static bool TryFindDangerMarker(
            IReadOnlyList<RpgMapLandmarkMarker> markers, out RpgMapLandmarkMarker marker)
        {
            for (int i = 0; i < markers.Count; i++)
            {
                if (markers[i].Category == RpgMapMarkerCategory.DangerZone)
                {
                    marker = markers[i];
                    return true;
                }
            }
            marker = default;
            return false;
        }
    }
}
