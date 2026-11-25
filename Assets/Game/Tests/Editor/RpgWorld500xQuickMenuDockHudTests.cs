using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xQuickMenuDockHudTests
    {
        [Test]
        public void QuickMenuDockHud_ExposesAll16ModalsTogglesActiveHighlightRespectsLockAndZeroLeak()
        {
            var root = new GameObject("World500xQuickMenuDockHudTestRoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupRouter = new RpgWorld500xMenuRouter(root.transform, warmupBoot, new RpgWorld500xCampHavenService()))
                using (var warmupDock = new RpgWorld500xQuickMenuDockHud(root.transform, warmupRouter))
                {
                    warmupDock.TryClickModalButton(RpgWorld500xModalKind.WorldAtlas);
                    warmupDock.TryClickModalButton(RpgWorld500xModalKind.WorldAtlas);
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var router = new RpgWorld500xMenuRouter(root.transform, boot, new RpgWorld500xCampHavenService()))
                using (var dock = new RpgWorld500xQuickMenuDockHud(root.transform, router))
                {
                    Assert.AreEqual(16, dock.DisplayedButtonCount);
                    Assert.AreEqual(RpgWorld500xModalKind.None, dock.ActiveHighlightedModal);

                    // Clicking BountyBoard opens it and highlights the dock button.
                    Assert.IsTrue(dock.TryClickModalButton(RpgWorld500xModalKind.BountyBoard));
                    Assert.AreEqual(RpgWorld500xModalKind.BountyBoard, router.ActiveModal);
                    Assert.AreEqual(RpgWorld500xModalKind.BountyBoard, dock.ActiveHighlightedModal);

                    // Clicking TravelItinerary switches modals cleanly.
                    Assert.IsTrue(dock.TryClickModalButton(RpgWorld500xModalKind.TravelItinerary));
                    Assert.AreEqual(RpgWorld500xModalKind.TravelItinerary, router.ActiveModal);
                    Assert.AreEqual(RpgWorld500xModalKind.TravelItinerary, dock.ActiveHighlightedModal);

                    // Parent CanvasGroup lock blocks dock clicks cleanly.
                    CanvasGroup group = root.AddComponent<CanvasGroup>();
                    group.interactable = false;
                    Assert.IsFalse(dock.TryClickModalButton(RpgWorld500xModalKind.TravelItinerary));
                    Assert.AreEqual(RpgWorld500xModalKind.TravelItinerary, router.ActiveModal);
                    group.interactable = true;

                    // Clicking the active modal button again toggles it closed.
                    Assert.IsTrue(dock.TryClickModalButton(RpgWorld500xModalKind.TravelItinerary));
                    Assert.AreEqual(RpgWorld500xModalKind.None, router.ActiveModal);
                    Assert.AreEqual(RpgWorld500xModalKind.None, dock.ActiveHighlightedModal);
                }

                Assert.AreEqual(0, root.transform.childCount);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
