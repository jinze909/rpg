using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionPlayModeDangerModalLockE2ETests
    {
        [Test]
        public void AllFourRegions_ActiveDangerEncounterBlocksAtlasTravelAndCampRestUntilVictory()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            foreach (RpgExpandedMapSpec spec in specs)
            {
                RpgWorldMapId targetMap = spec.MapId == RpgWorldMapId.RedSaltHarbor
                    ? RpgWorldMapId.Soundwell
                    : RpgWorldMapId.RedSaltHarbor;

                var host = new GameObject("FourRegionDangerModalLock_" + spec.MapId);
                var ctrl = host.AddComponent<RpgWorld500xPlayModeController>();
                try
                {
                    ctrl.Initialize(
                        overrideMap: spec.MapId,
                        saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                        overrideTileSlices: new Sprite[] { null });

                    var facade = ctrl.Bootstrapper.Driver.Facade;
                    Assert.IsTrue(
                        TryFindFirstDangerMarker(facade.Session.Host.Layout.Markers, out RpgMapLandmarkMarker marker),
                        spec.MapId.ToString());

                    Vector2 pos = new Vector2(marker.Tile.x + 0.5f, marker.Tile.y + 0.5f);
                    Assert.IsTrue(
                        facade.Session.Host.Bridge.TryTriggerDangerZoneEncounter(marker.Id, pos),
                        spec.MapId.ToString());
                    Assert.IsTrue(facade.Session.Host.Bridge.IsInDangerEncounter, spec.MapId.ToString());

                    // 1. During active danger encounter, WorldAtlas travel and camp rest must be blocked.
                    Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.WorldAtlas), spec.MapId.ToString());
                    var modal = host.transform.Find("World500xPlayModeUiRoot/World500xAtlasHudPanel/AtlasModalBox");
                    Assert.IsNotNull(modal, spec.MapId.ToString());

                    var travelBtn = modal.Find("RegionRows/RegionRow_" + targetMap + "/TravelButton")
                        .GetComponent<Button>();
                    var restBtn = modal.Find("CampRestButton").GetComponent<Button>();
                    Assert.IsNotNull(travelBtn, spec.MapId.ToString());
                    Assert.IsNotNull(restBtn, spec.MapId.ToString());

                    travelBtn.onClick.Invoke();
                    restBtn.onClick.Invoke();
                    Assert.AreEqual(spec.MapId, facade.CurrentMapId, spec.MapId.ToString());
                    Assert.IsFalse(
                        ctrl.SaveSlotManager.InspectSlot(RpgWorld500xSaveSlotId.AutoSave).IsOccupied,
                        spec.MapId.ToString());

                    // 2. Resolving the danger victory clears encounter state; reopening WorldAtlas refreshes buttons.
                    Assert.IsTrue(
                        ctrl.TryResolveActiveDangerVictory(false, out var encounter, out var reward),
                        spec.MapId.ToString());
                    Assert.AreEqual(marker.Id, encounter.MarkerId, spec.MapId.ToString());
                    Assert.Greater(reward.SaltSilverReward, 0, spec.MapId.ToString());
                    Assert.IsFalse(facade.Session.Host.Bridge.IsInDangerEncounter, spec.MapId.ToString());

                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { CloseModalPressed = true }, 0.05f);
                    Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.WorldAtlas), spec.MapId.ToString());

                    restBtn = modal.Find("CampRestButton").GetComponent<Button>();
                    Assert.IsTrue(restBtn.IsInteractable(), spec.MapId.ToString());
                    restBtn.onClick.Invoke();
                    Assert.IsTrue(
                        ctrl.SaveSlotManager.InspectSlot(RpgWorld500xSaveSlotId.AutoSave).IsOccupied,
                        spec.MapId.ToString());

                    travelBtn = modal.Find("RegionRows/RegionRow_" + targetMap + "/TravelButton")
                        .GetComponent<Button>();
                    Assert.IsTrue(travelBtn.IsInteractable(), spec.MapId.ToString());
                    travelBtn.onClick.Invoke();
                    Assert.AreEqual(targetMap, facade.CurrentMapId, spec.MapId.ToString());

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

        private static bool TryFindFirstDangerMarker(
            IReadOnlyList<RpgMapLandmarkMarker> markers,
            out RpgMapLandmarkMarker marker)
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
