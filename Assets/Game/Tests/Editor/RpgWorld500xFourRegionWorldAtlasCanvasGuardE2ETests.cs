using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionWorldAtlasCanvasGuardE2ETests
    {
        [Test]
        public void AllFourRegions_WorldAtlasRejectsDisabledCanvasLockedAndHiddenInputUntilRestored()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            foreach (RpgExpandedMapSpec spec in specs)
            {
                RpgWorldMapId targetMap = spec.MapId == RpgWorldMapId.RedSaltHarbor
                    ? RpgWorldMapId.Soundwell
                    : RpgWorldMapId.RedSaltHarbor;

                var root = new GameObject("WorldAtlasCanvasGuard_" + spec.MapId);
                try
                {
                    var manager = new RpgWorld500xSaveSlotManager(new RpgWorld500xInMemorySaveSlotStorage());
                    using (var boot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: spec.MapId,
                        overrideTileSlices: new Sprite[] { null }))
                    using (var panel = new RpgWorld500xWorldAtlasHudPanel(
                        root.transform,
                        boot,
                        saveSlotManager: manager))
                    {
                        Assert.IsTrue(panel.Show(), spec.MapId.ToString());
                        var canvas = root.transform.Find("World500xAtlasHudPanel").GetComponent<Canvas>();
                        Assert.IsNotNull(canvas, spec.MapId.ToString());
                        var modal = canvas.transform.Find("AtlasModalBox");
                        Assert.IsNotNull(modal, spec.MapId.ToString());

                        var travelBtn = modal.Find("RegionRows/RegionRow_" + targetMap + "/TravelButton")
                            .GetComponent<Button>();
                        var restBtn = modal.Find("CampRestButton").GetComponent<Button>();
                        Assert.IsNotNull(travelBtn, spec.MapId.ToString());
                        Assert.IsNotNull(restBtn, spec.MapId.ToString());

                        var facade = boot.Driver.Facade;
                        float minutes = facade.Atmosphere.TimeOfDayMinutes;
                        int day = facade.Atmosphere.DayCount;
                        Vector2 position = facade.Session.Host.PlayerPosition;
                        string header = panel.HeaderText;
                        string status = panel.StatusBannerText;
                        string saved = panel.LastCampSaveJson;
                        Assert.IsFalse(manager.InspectSlot(RpgWorld500xSaveSlotId.AutoSave).IsOccupied, spec.MapId.ToString());

                        // 1. Disabled owning Canvas rejects travel/rest callbacks and public APIs.
                        canvas.enabled = false;
                        Assert.IsTrue(travelBtn.IsInteractable(), spec.MapId.ToString());
                        Assert.IsTrue(restBtn.IsInteractable(), spec.MapId.ToString());
                        AssertBlocked(panel, facade, manager, spec.MapId, targetMap, travelBtn, restBtn,
                            true, position, minutes, day, header, status, saved);
                        canvas.enabled = true;

                        // 2. Ancestor CanvasGroup lock rejects travel/rest callbacks and public APIs.
                        var group = root.AddComponent<CanvasGroup>();
                        group.interactable = false;
                        AssertBlocked(panel, facade, manager, spec.MapId, targetMap, travelBtn, restBtn,
                            true, position, minutes, day, header, status, saved);
                        group.interactable = true;

                        // 3. Hidden panel rejects travel/rest callbacks and public APIs.
                        Assert.IsTrue(panel.Hide(), spec.MapId.ToString());
                        AssertBlocked(panel, facade, manager, spec.MapId, targetMap, travelBtn, restBtn,
                            false, position, minutes, day, header, status, saved);
                        Assert.IsTrue(panel.Show(), spec.MapId.ToString());

                        // 4. Restored panel supports travel and camp-rest autosave cleanly.
                        var restoredTravelBtn = modal.Find("RegionRows/RegionRow_" + targetMap + "/TravelButton")
                            .GetComponent<Button>();
                        restoredTravelBtn.onClick.Invoke();
                        Assert.AreEqual(targetMap, facade.CurrentMapId, spec.MapId.ToString());
                        StringAssert.Contains("已沿", panel.StatusBannerText, spec.MapId.ToString());

                        Assert.IsTrue(panel.TryClickCampRest(), spec.MapId.ToString());
                        Assert.IsTrue(manager.InspectSlot(RpgWorld500xSaveSlotId.AutoSave).IsOccupied, spec.MapId.ToString());
                        Assert.IsTrue(
                            RpgWorld500xSaveSnapshotSerializer.TryDeserialize(panel.LastCampSaveJson, out _, out _),
                            spec.MapId.ToString());
                        StringAssert.Contains("已存档", panel.StatusBannerText, spec.MapId.ToString());
                    }

                    Assert.AreEqual(0, root.transform.childCount, spec.MapId.ToString());
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        private static void AssertBlocked(
            RpgWorld500xWorldAtlasHudPanel panel,
            RpgWorld500xGrandRuntimeFacade facade,
            RpgWorld500xSaveSlotManager manager,
            RpgWorldMapId originMap,
            RpgWorldMapId targetMap,
            Button travelBtn,
            Button restBtn,
            bool expectedVisible,
            Vector2 expectedPosition,
            float expectedMinutes,
            int expectedDay,
            string expectedHeader,
            string expectedStatus,
            string expectedSaved)
        {
            travelBtn.onClick.Invoke();
            restBtn.onClick.Invoke();
            Assert.IsFalse(panel.TryClickTravelTo(targetMap));
            Assert.IsFalse(panel.TryClickCampRest());

            Assert.AreEqual(expectedVisible, panel.IsVisible);
            Assert.AreEqual(originMap, facade.CurrentMapId);
            Assert.AreEqual(expectedPosition, facade.Session.Host.PlayerPosition);
            Assert.AreEqual(expectedMinutes, facade.Atmosphere.TimeOfDayMinutes);
            Assert.AreEqual(expectedDay, facade.Atmosphere.DayCount);
            Assert.AreEqual(expectedHeader, panel.HeaderText);
            Assert.AreEqual(expectedStatus, panel.StatusBannerText);
            Assert.AreEqual(expectedSaved, panel.LastCampSaveJson);
            Assert.IsFalse(manager.InspectSlot(RpgWorld500xSaveSlotId.AutoSave).IsOccupied);
            Assert.AreEqual(4, panel.DisplayedRegionRowCount);
            Assert.IsTrue(panel.TryPreviewShortestRouteTo(targetMap, out _));
        }
    }
}
