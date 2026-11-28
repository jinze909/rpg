using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionPlayModeWeatherAndCampRestE2ETests
    {
        [Test]
        public void AllFourRegions_PlayModeControllerWeatherOverrideAndCampRestAutosavePersist()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var host = new GameObject("FourRegionWeatherCampRest_" + spec.MapId);
                var ctrl = host.AddComponent<RpgWorld500xPlayModeController>();
                try
                {
                    ctrl.Initialize(
                        overrideMap: spec.MapId,
                        saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                        overrideTileSlices: new Sprite[] { null });

                    var facade = ctrl.Bootstrapper.Driver.Facade;
                    Assert.IsFalse(
                        ctrl.SaveSlotManager.InspectSlot(RpgWorld500xSaveSlotId.AutoSave).IsOccupied,
                        spec.MapId.ToString());

                    // 1. Set GaleStorm on active map and tick controller to synchronize weather HUD.
                    facade.Atmosphere.SetWeather(spec.MapId, RpgWorld500xWeatherKind.GaleStorm);
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame(), 0.1f);
                    Assert.IsNotNull(ctrl.WeatherAdvisoryHud, spec.MapId.ToString());

                    // 2. Open WorldAtlas modal and invoke CampRestButton to rest and trigger AutoSave.
                    Assert.IsTrue(ctrl.MenuRouter.OpenModal(RpgWorld500xModalKind.WorldAtlas), spec.MapId.ToString());
                    var restBtn = host.transform
                        .Find("World500xPlayModeUiRoot/World500xAtlasHudPanel/AtlasModalBox/CampRestButton")
                        .GetComponent<Button>();
                    Assert.IsNotNull(restBtn, spec.MapId.ToString());
                    Assert.IsTrue(restBtn.IsInteractable(), spec.MapId.ToString());

                    int dayBefore = facade.Atmosphere.DayCount;
                    float minutesBefore = facade.Atmosphere.TimeOfDayMinutes;
                    restBtn.onClick.Invoke();

                    Assert.IsTrue(
                        ctrl.SaveSlotManager.InspectSlot(RpgWorld500xSaveSlotId.AutoSave).IsOccupied,
                        spec.MapId.ToString());
                    bool clockAdvanced = facade.Atmosphere.DayCount > dayBefore ||
                        !Mathf.Approximately(facade.Atmosphere.TimeOfDayMinutes, minutesBefore);
                    Assert.IsTrue(clockAdvanced, spec.MapId.ToString());

                    // 3. Verify AutoSave snapshot loads cleanly back into the same region.
                    Assert.IsTrue(
                        ctrl.SaveSlotManager.TryLoadSlot(
                            ctrl.Bootstrapper,
                            RpgWorld500xSaveSlotId.AutoSave,
                            out _,
                            out string loadErr),
                        loadErr);
                    Assert.AreEqual(spec.MapId, facade.CurrentMapId, spec.MapId.ToString());

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
