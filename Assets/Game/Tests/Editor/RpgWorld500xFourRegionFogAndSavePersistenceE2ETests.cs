using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionFogAndSavePersistenceE2ETests
    {
        [Test]
        public void FourRegionTraversal_PersistsDistrictFogWeatherAndEconomyAcrossSaveReloadWithZeroLeak()
        {
            // Warm up one-time runtime caches before steady-state leak baseline.
            using (var warmup = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                warmup.Driver.TickFrame(Vector2.right, 0.1f);
            }

            int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

            string savedJson;
            int soundwellChunksBefore;
            int mineCampChunksBefore;

            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                var facade = boot.Driver.Facade;
                facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.Clear);
                facade.Atmosphere.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.GaleStorm);
                facade.Atmosphere.SetWeather(RpgWorldMapId.WhitewaterCrossing, RpgWorld500xWeatherKind.Clear);
                facade.Atmosphere.SetWeather(RpgWorldMapId.MineCamp, RpgWorld500xWeatherKind.AshHaze);

                boot.Driver.TickFrame(Vector2.right, 0.25f);
                soundwellChunksBefore = facade.DistrictTracker.VisitedChunkCount;
                Assert.Greater(soundwellChunksBefore, 0);

                Assert.IsTrue(boot.SwitchMap(RpgWorldMapId.RedSaltHarbor));
                boot.Driver.TickFrame(Vector2.right, 0.25f);
                Assert.Greater(boot.Driver.Facade.DistrictTracker.VisitedChunkCount, 0);

                Assert.IsTrue(boot.SwitchMap(RpgWorldMapId.WhitewaterCrossing));
                boot.Driver.TickFrame(Vector2.right, 0.25f);
                Assert.Greater(boot.Driver.Facade.DistrictTracker.VisitedChunkCount, 0);

                Assert.IsTrue(boot.SwitchMap(RpgWorldMapId.MineCamp));
                boot.Driver.TickFrame(Vector2.right, 0.25f);
                mineCampChunksBefore = boot.Driver.Facade.DistrictTracker.VisitedChunkCount;
                Assert.Greater(mineCampChunksBefore, 0);

                boot.Driver.Facade.BountyAndSupply.AddSaltSilver(175);
                savedJson = boot.ExportSaveJson();
                Assert.IsTrue(
                    RpgWorld500xSaveSnapshotSerializer.TryDeserialize(savedJson, out _, out string deserErr),
                    deserErr);
            }

            using (var restored = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                Assert.IsTrue(restored.TryImportSaveJson(savedJson, out string importErr), importErr);
                var facade = restored.Driver.Facade;
                Assert.AreEqual(RpgWorldMapId.MineCamp, facade.CurrentMapId);
                Assert.AreEqual(175, facade.BountyAndSupply.SaltSilverBalance);
                Assert.AreEqual(RpgWorld500xWeatherKind.GaleStorm, facade.Atmosphere.GetWeather(RpgWorldMapId.RedSaltHarbor));
                Assert.AreEqual(RpgWorld500xWeatherKind.AshHaze, facade.Atmosphere.GetWeather(RpgWorldMapId.MineCamp));
                Assert.GreaterOrEqual(facade.DistrictTracker.VisitedChunkCount, mineCampChunksBefore);

                Assert.IsTrue(restored.SwitchMap(RpgWorldMapId.Soundwell));
                Assert.GreaterOrEqual(restored.Driver.Facade.DistrictTracker.VisitedChunkCount, soundwellChunksBefore);
            }

            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
        }
    }
}
