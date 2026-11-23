using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xSceneBootstrapperTests
    {
        [Test]
        public void SceneBootstrapper_RunsFullLoopSaveLoadRoundTripAndDisposesWithZeroLeak()
        {
            // Warm up one-time Unity UGUI/font caches before steady-state leak baseline.
            using (var warmup = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                warmup.StepFrame(Vector2.right, 0.1f);
                warmup.SwitchMap(RpgWorldMapId.RedSaltHarbor);
            }

            int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

            string savedJson;
            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                boot.StepFrame(Vector2.right, 0.25f);
                boot.Driver.Facade.BountyAndSupply.AddSaltSilver(80);
                Assert.IsTrue(boot.SwitchMap(RpgWorldMapId.RedSaltHarbor));
                boot.StepFrame(Vector2.zero, 0.1f);

                Assert.IsTrue(boot.StatusHud.WaypointAndEconomyReadout.Contains("盐银 80"));
                savedJson = boot.ExportSaveJson();
                Assert.IsFalse(string.IsNullOrEmpty(savedJson));
            }

            // Restore into a fresh bootstrapper starting on Soundwell -> switches to RedSaltHarbor & restores wallet.
            using (var restored = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                Assert.IsTrue(restored.TryImportSaveJson(savedJson, out string err), err);
                Assert.AreEqual(RpgWorldMapId.RedSaltHarbor, restored.Driver.Facade.CurrentMapId);
                Assert.AreEqual(80, restored.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                Assert.IsTrue(restored.StatusHud.WaypointAndEconomyReadout.Contains("盐银 80"));

                // Tampered JSON is rejected safely.
                Assert.IsFalse(restored.TryImportSaveJson(savedJson.Replace("80", "9999"), out _));
            }

            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
        }
    }
}
