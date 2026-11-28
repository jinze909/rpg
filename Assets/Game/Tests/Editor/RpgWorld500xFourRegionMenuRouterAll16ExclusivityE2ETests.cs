using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    [TestFixture]
    public sealed class RpgWorld500xFourRegionMenuRouterAll16ExclusivityE2ETests
    {
        [Test]
        public void AllFourRegions_MenuRouter_CyclesAll16ModalsWithMutualExclusionAndInputBlocking()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count, "Expected exactly 4 regions in 500x world scale.");

            var allKinds = (RpgWorld500xModalKind[])Enum.GetValues(typeof(RpgWorld500xModalKind));

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var uiRoot = new GameObject("FourRegionMenuRouterAll16_" + spec.MapId);
                try
                {
                    using (var boot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: spec.MapId,
                        overrideTileSlices: new Sprite[] { null }))
                    using (var router = new RpgWorld500xMenuRouter(uiRoot.transform, boot,
                            new RpgWorld500xCampHavenService(),
                            new RpgWorld500xSaveSlotManager(new RpgWorld500xInMemorySaveSlotStorage())))
                    {
                        Assert.AreEqual(RpgWorld500xModalKind.None, router.ActiveModal);
                        Assert.IsFalse(router.HasAnyModalOpen);

                        int modalCount = 0;
                        foreach (RpgWorld500xModalKind kind in allKinds)
                        {
                            if (kind == RpgWorld500xModalKind.None)
                            {
                                continue;
                            }

                            modalCount++;
                            Assert.IsTrue(router.OpenModal(kind), $"OpenModal({kind}) should succeed in {spec.MapId}.");
                            Assert.AreEqual(kind, router.ActiveModal, $"ActiveModal mismatch for {kind} in {spec.MapId}.");
                            Assert.IsTrue(router.HasAnyModalOpen);

                            Vector2 posBefore = boot.Driver.Facade.Session.Host.PlayerPosition;
                            router.StepFrameWithModalGuard(Vector2.right, 0.25f, sprinting: true, interactPressed: true);
                            Assert.AreEqual(posBefore, boot.Driver.Facade.Session.Host.PlayerPosition,
                                $"Movement must be suppressed while modal {kind} is open in {spec.MapId}.");
                        }

                        Assert.AreEqual(16, modalCount, "Expected exactly 16 non-None modal kinds in RpgWorld500xModalKind.");

                        router.CloseAll();
                        Assert.AreEqual(RpgWorld500xModalKind.None, router.ActiveModal);
                        Assert.IsFalse(router.HasAnyModalOpen);
                    }

                    Assert.AreEqual(0, uiRoot.transform.childCount, $"MenuRouter disposal should leave zero UI children in {spec.MapId}.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(uiRoot);
                }
            }
        }

        [Test]
        public void AllFourRegions_MenuRouter_ToggleModalAndDisposeRestoreDriverAutoUpdatesCleanly()
        {
            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                var uiRoot = new GameObject("FourRegionMenuRouterToggle_" + spec.MapId);
                try
                {
                    using (var boot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: spec.MapId,
                        overrideTileSlices: new Sprite[] { null }))
                    {
                        bool initialAuto = boot.Driver.AutomaticFrameUpdates;
                        using (var router = new RpgWorld500xMenuRouter(uiRoot.transform, boot,
                            new RpgWorld500xCampHavenService(),
                            new RpgWorld500xSaveSlotManager(new RpgWorld500xInMemorySaveSlotStorage())))
                        {
                            Assert.IsFalse(boot.Driver.AutomaticFrameUpdates, $"MenuRouter should pause driver auto-updates in {spec.MapId}.");

                            Assert.IsTrue(router.ToggleModal(RpgWorld500xModalKind.TravelItinerary));
                            Assert.AreEqual(RpgWorld500xModalKind.TravelItinerary, router.ActiveModal);
                            Assert.IsTrue(router.ItineraryPanel.IsVisible);

                            Assert.IsTrue(router.ToggleModal(RpgWorld500xModalKind.TravelItinerary));
                            Assert.AreEqual(RpgWorld500xModalKind.None, router.ActiveModal);
                            Assert.IsFalse(router.ItineraryPanel.IsVisible);
                            Assert.IsFalse(router.HasAnyModalOpen);
                        }

                        Assert.AreEqual(initialAuto, boot.Driver.AutomaticFrameUpdates,
                            $"Disposing MenuRouter must restore AutomaticFrameUpdates in {spec.MapId}.");
                        Assert.AreEqual(0, uiRoot.transform.childCount);
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(uiRoot);
                }
            }
        }
    }
}
