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
                    var hostBefore = boot.Driver.Facade.Session.Host;
                    var trackerBefore = boot.Driver.Facade.DistrictTracker;

                    Assert.IsTrue(boot.SwitchMap(origin));
                    Assert.IsFalse(boot.SwitchMap((RpgWorldMapId)255));
                    Assert.AreSame(hostBefore, boot.Driver.Facade.Session.Host);
                    Assert.AreSame(trackerBefore, boot.Driver.Facade.DistrictTracker);
                    Assert.That(boot.Driver.Facade.CurrentMapId, Is.EqualTo(origin));
                    Assert.That(boot.Driver.Facade.BountyAndSupply.SaltSilverBalance, Is.EqualTo(55));
                }
            }
        }
    }
}
