using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionMenuRouterModalGuardE2ETests
    {
        [Test]
        public void AllFourRegions_MenuRouterEnforcesMutualExclusionAndSuppressesMovementWhileOpenWithZeroLeak()
        {
            // Warm up one-time UGUI/font caches before steady-state leak baseline.
            var warmupGo = new GameObject("World500xFourRegionMenuRouterWarmupHost");
            var warmupCtrl = warmupGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                warmupCtrl.Initialize(
                    overrideMap: RpgWorldMapId.Soundwell,
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });
                warmupCtrl.MenuRouter.OpenModal(RpgWorld500xModalKind.WorldAtlas);
                warmupCtrl.MenuRouter.OpenModal(RpgWorld500xModalKind.TravelItinerary);
                warmupCtrl.MenuRouter.OpenModal(RpgWorld500xModalKind.CampMerchant);
                warmupCtrl.MenuRouter.CloseAll();
            }
            finally
            {
                warmupCtrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(warmupGo);
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

            var hostGo = new GameObject("World500xFourRegionMenuRouterGuardE2EHost");
            var ctrl = hostGo.AddComponent<RpgWorld500xPlayModeController>();
            try
            {
                ctrl.Initialize(
                    overrideMap: regions[0],
                    saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                    overrideTileSlices: new Sprite[] { null });

                for (int i = 0; i < regions.Length; i++)
                {
                    RpgWorldMapId mapId = regions[i];
                    if (i > 0)
                    {
                        Assert.IsTrue(ctrl.Bootstrapper.SwitchMap(mapId), "Expected map switch to " + mapId);
                    }

                    var router = ctrl.MenuRouter;
                    Assert.IsFalse(router.HasAnyModalOpen);
                    Assert.AreEqual(RpgWorld500xModalKind.None, router.ActiveModal);

                    // Opening a modal suppresses movement and world interaction through StepFrameWithModalGuard.
                    Assert.IsTrue(router.OpenModal(RpgWorld500xModalKind.WorldAtlas));
                    Assert.IsTrue(router.HasAnyModalOpen);
                    Assert.AreEqual(RpgWorld500xModalKind.WorldAtlas, router.ActiveModal);
                    Assert.IsTrue(router.AtlasPanel.IsVisible);

                    Vector2 posBefore = ctrl.Bootstrapper.Driver.Facade.Session.Host.PlayerPosition;
                    router.StepFrameWithModalGuard(Vector2.right, 0.5f, sprinting: true, interactPressed: true);
                    Assert.AreEqual(posBefore, ctrl.Bootstrapper.Driver.Facade.Session.Host.PlayerPosition);

                    // Switching directly to another modal closes the previous panel cleanly.
                    Assert.IsTrue(router.OpenModal(RpgWorld500xModalKind.TravelItinerary));
                    Assert.AreEqual(RpgWorld500xModalKind.TravelItinerary, router.ActiveModal);
                    Assert.IsFalse(router.AtlasPanel.IsVisible);
                    Assert.IsTrue(router.ItineraryPanel.IsVisible);

                    // Toggling the active modal closes all panels.
                    Assert.IsTrue(router.ToggleModal(RpgWorld500xModalKind.TravelItinerary));
                    Assert.AreEqual(RpgWorld500xModalKind.None, router.ActiveModal);
                    Assert.IsFalse(router.HasAnyModalOpen);
                    Assert.IsFalse(router.ItineraryPanel.IsVisible);
                }
            }
            finally
            {
                ctrl.Shutdown();
                UnityEngine.Object.DestroyImmediate(hostGo);
            }

            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
        }
    }
}
