using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionTravelItineraryHudE2ETests
    {
        [Test]
        public void TravelItineraryHudPanel_AcrossAllFourOrigins_PlansRoutesAutoProvisionsAndDepartsFirstLeg()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            for (int i = 0; i < specs.Count; i++)
            {
                var origin = specs[i].MapId;
                var destination = specs[(i + 2) % specs.Count].MapId;
                var root = new GameObject("FourRegionTravelItineraryRoot_" + origin);
                try
                {
                    using (var boot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: origin,
                        overrideTileSlices: new Sprite[] { null }))
                    using (var panel = new RpgWorld500xTravelItineraryHudPanel(root.transform, boot))
                    {
                        var facade = boot.Driver.Facade;
                        for (int j = 0; j < specs.Count; j++)
                        {
                            var mapId = specs[j].MapId;
                            facade.Atmosphere.SetWeather(
                                mapId,
                                mapId == destination ? RpgWorld500xWeatherKind.GaleStorm : RpgWorld500xWeatherKind.Clear);
                        }

                        facade.BountyAndSupply.AddSaltSilver(300);

                        Assert.IsTrue(panel.Show(destination));
                        Assert.IsTrue(panel.IsVisible);
                        Assert.That(panel.SelectedDestination, Is.EqualTo(destination));
                        Assert.IsTrue(panel.LastItinerary.IsReachable);
                        Assert.That(panel.LastItinerary.HopCount, Is.GreaterThanOrEqualTo(1));
                        Assert.That(panel.DisplayedLegRowCount, Is.EqualTo(panel.LastItinerary.HopCount));
                        StringAssert.Contains(specs[i].DisplayNameZh, panel.HeaderText);
                        Assert.That(panel.LastProvisioningPlan.Lines.Count, Is.GreaterThan(0));

                        Assert.IsTrue(panel.TryClickAutoProvision());
                        Assert.IsTrue(panel.LastProvisioningReceipt.FullyProvisioned);
                        Assert.That(panel.LastProvisioningReceipt.TotalSpentSaltSilver, Is.GreaterThan(0));
                        Assert.That(panel.LastProvisioningPlan.Lines.Count, Is.EqualTo(0));
                        Assert.IsFalse(panel.TryClickAutoProvision());

                        Assert.IsTrue(panel.TryClickDepartFirstLeg());
                        Assert.That(facade.CurrentMapId, Is.Not.EqualTo(origin));

                        Assert.IsTrue(panel.TryClickClose());
                        Assert.IsFalse(panel.IsVisible);
                    }

                    Assert.That(root.transform.childCount, Is.EqualTo(0));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        [Test]
        public void TravelItineraryHudPanel_AcrossAllFourOrigins_SameOriginRejectsDepartureAndParentCanvasGroupLockBlocksActions()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            for (int i = 0; i < specs.Count; i++)
            {
                var origin = specs[i].MapId;
                var destination = specs[(i + 1) % specs.Count].MapId;
                var root = new GameObject("FourRegionTravelItineraryGuard_" + origin);
                try
                {
                    using (var boot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: origin,
                        overrideTileSlices: new Sprite[] { null }))
                    using (var panel = new RpgWorld500xTravelItineraryHudPanel(root.transform, boot))
                    {
                        var facade = boot.Driver.Facade;
                        facade.Atmosphere.SetWeather(origin, RpgWorld500xWeatherKind.Clear);
                        facade.Atmosphere.SetWeather(destination, RpgWorld500xWeatherKind.GaleStorm);
                        facade.BountyAndSupply.AddSaltSilver(300);

                        Assert.IsTrue(panel.Show(origin));
                        Assert.That(panel.LastItinerary.HopCount, Is.EqualTo(0));
                        Assert.IsFalse(panel.TryClickDepartFirstLeg());

                        Assert.IsTrue(panel.TrySelectDestination(destination));
                        Assert.That(panel.SelectedDestination, Is.EqualTo(destination));
                        Assert.That(panel.LastItinerary.HopCount, Is.GreaterThanOrEqualTo(1));

                        var group = root.AddComponent<CanvasGroup>();
                        group.interactable = false;

                        Assert.IsFalse(panel.TrySelectDestination(origin));
                        Assert.IsFalse(panel.TryClickAutoProvision());
                        Assert.IsFalse(panel.TryClickDepartFirstLeg());
                        Assert.IsFalse(panel.TryClickClose());
                        Assert.That(facade.BountyAndSupply.SaltSilverBalance, Is.EqualTo(300));
                        Assert.That(facade.CurrentMapId, Is.EqualTo(origin));
                        Assert.IsTrue(panel.IsVisible);

                        group.interactable = true;
                        Assert.IsTrue(panel.TryClickClose());
                        Assert.IsFalse(panel.IsVisible);
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }
    }
}
