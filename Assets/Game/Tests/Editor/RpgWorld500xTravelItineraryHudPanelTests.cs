using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xTravelItineraryHudPanelTests
    {
        [Test]
        public void TravelItineraryHudPanel_PlansMultiHopDepartsFirstLegRespectsLockAndZeroLeak()
        {
            var root = new GameObject("World500xTravelItineraryHudPanelTestRoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupPanel = new RpgWorld500xTravelItineraryHudPanel(root.transform, warmupBoot))
                {
                    warmupPanel.Show(RpgWorldMapId.RedSaltHarbor);
                    warmupPanel.TryClickClose();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xTravelItineraryHudPanel(root.transform, boot))
                {
                    // Hidden panel rejects clicks cleanly.
                    Assert.IsFalse(panel.TryClickDepartFirstLeg());
                    Assert.IsFalse(panel.TrySelectDestination(RpgWorldMapId.MineCamp));

                    Assert.IsTrue(panel.Show(RpgWorldMapId.MineCamp));
                    Assert.AreEqual(2, panel.LastItinerary.HopCount);
                    Assert.AreEqual(2, panel.DisplayedLegRowCount);
                    Assert.AreEqual(270, panel.LastItinerary.TotalTravelMinutesInGame);

                    // Parent CanvasGroup lock blocks destination switch, depart, and close.
                    CanvasGroup group = root.AddComponent<CanvasGroup>();
                    group.interactable = false;
                    Assert.IsFalse(panel.TrySelectDestination(RpgWorldMapId.RedSaltHarbor));
                    Assert.IsFalse(panel.TryClickDepartFirstLeg());
                    Assert.IsFalse(panel.TryClickClose());
                    Assert.AreEqual(RpgWorldMapId.Soundwell, boot.Driver.Facade.CurrentMapId);
                    group.interactable = true;

                    // Depart first leg: Soundwell -> RedSaltHarbor, remaining itinerary becomes 1 hop to MineCamp.
                    Assert.IsTrue(panel.TryClickDepartFirstLeg());
                    Assert.AreEqual(RpgWorldMapId.RedSaltHarbor, boot.Driver.Facade.CurrentMapId);
                    Assert.AreEqual(1, panel.LastItinerary.HopCount);
                    Assert.AreEqual(1, panel.DisplayedLegRowCount);
                    Assert.IsTrue(panel.FeedbackText.Contains("已沿"));

                    // Selecting current map disables depart button cleanly.
                    Assert.IsTrue(panel.TrySelectDestination(RpgWorldMapId.RedSaltHarbor));
                    Assert.AreEqual(0, panel.LastItinerary.HopCount);
                    Assert.IsFalse(panel.TryClickDepartFirstLeg());

                    Assert.IsTrue(panel.TryClickClose());
                    Assert.IsFalse(panel.IsVisible);
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
