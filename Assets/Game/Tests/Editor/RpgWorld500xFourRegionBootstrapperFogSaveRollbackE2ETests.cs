using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionBootstrapperFogSaveRollbackE2ETests
    {
        [Test]
        public void SceneBootstrapper_AcrossAllFourRegions_PreservesFogAcrossMapSwitchAndRollsBackWalletAndWeatherOnSaveImport()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            for (int i = 0; i < specs.Count; i++)
            {
                var origin = specs[i].MapId;
                var other = specs[(i + 1) % specs.Count].MapId;

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: origin,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    boot.StepFrame(Vector2.zero, 0.1f);
                    var facade = boot.Driver.Facade;
                    int initialVisited = facade.DistrictTracker.VisitedChunkCount;
                    Assert.That(initialVisited, Is.GreaterThan(0));
                    Vector2 savedPos = facade.Session.Host.PlayerPosition;

                    facade.BountyAndSupply.AddSaltSilver(100);
                    facade.Atmosphere.SetWeather(origin, RpgWorld500xWeatherKind.MistFog);
                    string saved = boot.ExportSaveJson();

                    facade.BountyAndSupply.AddSaltSilver(75);
                    facade.Atmosphere.SetWeather(origin, RpgWorld500xWeatherKind.GaleStorm);
                    Assert.That(facade.BountyAndSupply.SaltSilverBalance, Is.EqualTo(175));

                    Assert.IsTrue(boot.SwitchMap(other));
                    Assert.That(boot.Driver.Facade.CurrentMapId, Is.EqualTo(other));
                    Assert.IsTrue(boot.SwitchMap(origin));
                    Assert.That(boot.Driver.Facade.CurrentMapId, Is.EqualTo(origin));
                    Assert.That(boot.Driver.Facade.DistrictTracker.VisitedChunkCount, Is.GreaterThanOrEqualTo(initialVisited));

                    Assert.IsTrue(boot.SwitchMap(other));
                    Assert.IsTrue(boot.TryImportSaveJson(saved, out string error), error);

                    var restored = boot.Driver.Facade;
                    Assert.That(restored.CurrentMapId, Is.EqualTo(origin));
                    Assert.That(restored.BountyAndSupply.SaltSilverBalance, Is.EqualTo(100));
                    Assert.That(restored.Atmosphere.GetWeather(origin), Is.EqualTo(RpgWorld500xWeatherKind.MistFog));
                    Assert.That(restored.DistrictTracker.VisitedChunkCount, Is.EqualTo(initialVisited));
                    Assert.That(Vector2.Distance(restored.Session.Host.PlayerPosition, savedPos), Is.LessThan(0.05f));
                }
            }
        }

        [Test]
        public void SceneBootstrapper_HandlesSameMapNoOpAndRejectsInvalidMapSwitchWithoutMutatingActiveSessionAcrossAllFourRegions()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            for (int i = 0; i < specs.Count; i++)
            {
                var origin = specs[i].MapId;
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: origin,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    boot.StepFrame(Vector2.zero, 0.1f);
                    boot.Driver.Facade.BountyAndSupply.AddSaltSilver(55);
                    var facade = boot.Driver.Facade;
                    facade.Atmosphere.SetWeather(origin, RpgWorld500xWeatherKind.MistFog);
                    var hostBefore = facade.Session.Host;
                    var trackerBefore = facade.DistrictTracker;
                    var activityBefore = facade.ActivityDirector;
                    var propsBefore = facade.PropSpawner;
                    Vector2 positionBefore = hostBefore.PlayerPosition;
                    int visitedBefore = trackerBefore.VisitedChunkCount;
                    Assert.That(visitedBefore, Is.GreaterThan(0));

                    // Re-selecting the current map is a successful no-op.
                    // An invalid map is rejected without remounting any subsystem.
                    foreach (var target in new[] { origin, (RpgWorldMapId)255 })
                    {
                        Assert.That(boot.SwitchMap(target), Is.EqualTo(target == origin));
                        Assert.AreSame(facade, boot.Driver.Facade);
                        Assert.AreSame(hostBefore, facade.Session.Host);
                        Assert.AreSame(trackerBefore, facade.DistrictTracker);
                        Assert.AreSame(activityBefore, facade.ActivityDirector);
                        Assert.AreSame(propsBefore, facade.PropSpawner);
                        Assert.That(facade.CurrentMapId, Is.EqualTo(origin));
                        Assert.That(hostBefore.PlayerPosition, Is.EqualTo(positionBefore));
                        Assert.That(trackerBefore.VisitedChunkCount, Is.EqualTo(visitedBefore));
                        Assert.That(facade.Atmosphere.GetWeather(origin), Is.EqualTo(RpgWorld500xWeatherKind.MistFog));
                        Assert.That(facade.BountyAndSupply.SaltSilverBalance, Is.EqualTo(55));
                    }
                }
            }
        }
    }
}
