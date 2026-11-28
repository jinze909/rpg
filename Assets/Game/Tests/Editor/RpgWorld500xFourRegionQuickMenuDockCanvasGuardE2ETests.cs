using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    [TestFixture]
    public sealed class RpgWorld500xFourRegionQuickMenuDockCanvasGuardE2ETests
    {
        [Test]
        public void AllFourRegions_QuickMenuDock_ExposesAll16ButtonsAndSyncsActiveHighlightOnClick()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count, "Expected exactly 4 regions in 500x world scale.");

            RpgWorld500xModalKind[] sampleModals =
            {
                RpgWorld500xModalKind.QuestJournal,
                RpgWorld500xModalKind.WorldAtlas,
                RpgWorld500xModalKind.BountyBoard,
                RpgWorld500xModalKind.TravelItinerary
            };

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var uiRoot = new GameObject("FourRegionQuickMenuDock_" + spec.MapId);
                try
                {
                    using (var boot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: spec.MapId,
                        overrideTileSlices: new Sprite[] { null }))
                    using (var router = new RpgWorld500xMenuRouter(uiRoot.transform, boot, new RpgWorld500xCampHavenService()))
                    using (var dock = new RpgWorld500xQuickMenuDockHud(uiRoot.transform, router))
                    {
                        Assert.AreEqual(16, dock.DisplayedButtonCount, $"Expected 16 dock buttons in {spec.MapId}.");
                        Assert.AreEqual(RpgWorld500xModalKind.None, dock.ActiveHighlightedModal);

                        foreach (RpgWorld500xModalKind kind in sampleModals)
                        {
                            Assert.IsTrue(dock.TryClickModalButton(kind), $"Clicking dock button {kind} should succeed in {spec.MapId}.");
                            Assert.AreEqual(kind, router.ActiveModal);
                            Assert.AreEqual(kind, dock.ActiveHighlightedModal);
                        }

                        Assert.IsTrue(dock.TryClickModalButton(RpgWorld500xModalKind.TravelItinerary));
                        Assert.AreEqual(RpgWorld500xModalKind.None, router.ActiveModal);
                        Assert.AreEqual(RpgWorld500xModalKind.None, dock.ActiveHighlightedModal);
                    }

                    Assert.AreEqual(0, uiRoot.transform.childCount, $"Disposal should leave zero UI children in {spec.MapId}.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(uiRoot);
                }
            }
        }

        [Test]
        public void AllFourRegions_QuickMenuDock_BlocksCanvasGroupLockedClicksAndStaleCallbacks()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count, "Expected exactly 4 regions in 500x world scale.");

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var uiRoot = new GameObject("FourRegionQuickMenuDockLock_" + spec.MapId);
                try
                {
                    using (var boot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: spec.MapId,
                        overrideTileSlices: new Sprite[] { null }))
                    using (var router = new RpgWorld500xMenuRouter(uiRoot.transform, boot, new RpgWorld500xCampHavenService()))
                    using (var dock = new RpgWorld500xQuickMenuDockHud(uiRoot.transform, router))
                    {
                        Assert.IsTrue(dock.TryClickModalButton(RpgWorld500xModalKind.WorldGazetteer));
                        Assert.AreEqual(RpgWorld500xModalKind.WorldGazetteer, router.ActiveModal);

                        CanvasGroup group = uiRoot.AddComponent<CanvasGroup>();
                        group.interactable = false;

                        Assert.IsFalse(dock.TryClickModalButton(RpgWorld500xModalKind.WorldGazetteer));
                        Assert.IsFalse(dock.TryClickModalButton(RpgWorld500xModalKind.Bestiary));

                        Button gazetteerBtn = uiRoot.transform
                            .Find("World500xQuickMenuDockHud/QuickMenuDockPanel/DockBtn_WorldGazetteer")
                            .GetComponent<Button>();
                        Assert.IsNotNull(gazetteerBtn);
                        gazetteerBtn.onClick.Invoke();

                        Assert.AreEqual(RpgWorld500xModalKind.WorldGazetteer, router.ActiveModal,
                            $"Locked CanvasGroup must block stale dock button callback in {spec.MapId}.");
                        Assert.AreEqual(RpgWorld500xModalKind.WorldGazetteer, dock.ActiveHighlightedModal);

                        group.interactable = true;
                        Assert.IsTrue(dock.TryClickModalButton(RpgWorld500xModalKind.WorldGazetteer));
                        Assert.AreEqual(RpgWorld500xModalKind.None, router.ActiveModal);
                        Assert.AreEqual(RpgWorld500xModalKind.None, dock.ActiveHighlightedModal);
                    }

                    Assert.AreEqual(0, uiRoot.transform.childCount);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(uiRoot);
                }
            }
        }
    }
}
