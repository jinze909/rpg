using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xSceneBootstrapperTests
    {
        [Test]
        public void SameMapLoadRollsBackDiscoveryAndEncounterState()
        {
            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                var host = boot.Driver.Facade.Session.Host;
                RpgMapLandmarkMarker danger = default;
                bool found = false;
                foreach (RpgMapLandmarkMarker marker in host.Layout.Markers)
                {
                    if (marker.Category == RpgMapMarkerCategory.DangerZone &&
                        !host.Tracker.IsMarkerDiscovered(marker.Id) &&
                        Vector2.Distance(host.PlayerPosition, marker.Tile) >
                            Mathf.Max(4f, marker.DiscoveryRadiusTiles) + 2f)
                    {
                        danger = marker;
                        found = true;
                        break;
                    }
                }

                Assert.IsTrue(found);
                string saved = boot.ExportSaveJson();
                Vector2 encounterPosition = new Vector2(danger.Tile.x + 0.5f, danger.Tile.y + 0.5f);
                Assert.IsTrue(host.Bridge.TryTriggerDangerZoneEncounter(danger.Id, encounterPosition));
                Assert.IsTrue(host.Bridge.TryResolveDangerZoneVictory(danger.Id, true, out _));
                Assert.IsTrue(host.Tracker.IsMarkerDiscovered(danger.Id));
                Assert.AreEqual(1, host.Bridge.ClearedDangerZoneCount);

                Assert.IsTrue(boot.TryImportSaveJson(saved, out string err), err);
                Assert.AreSame(host, boot.Driver.Facade.Session.Host);
                Assert.IsFalse(host.Tracker.IsMarkerDiscovered(danger.Id));
                Assert.AreEqual(0, host.Bridge.ClearedDangerZoneCount);
                Assert.IsTrue(host.Bridge.TryTriggerDangerZoneEncounter(danger.Id, encounterPosition));

                // Loading while a fight is in flight must not retain its transient lock.
                Assert.IsTrue(boot.TryImportSaveJson(saved, out err), err);
                Assert.IsFalse(host.Bridge.IsInDangerEncounter);
                Assert.IsFalse(host.Tracker.IsMarkerDiscovered(danger.Id));
                Assert.IsTrue(host.Bridge.TryTriggerDangerZoneEncounter(danger.Id, encounterPosition));
            }
        }

        [Test]
        public void LoadRestoresExactPlayerPositionAcrossMapsAndWithinCurrentMap()
        {
            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.RedSaltHarbor,
                overrideTileSlices: new Sprite[] { null }))
            {
                var host = boot.Driver.Facade.Session.Host;
                Vector2 original = host.PlayerPosition;
                Vector2 savedPosition = original + new Vector2(0.125f, 0.25f);
                Assert.IsTrue(host.TryPlaceAtWalkablePosition(savedPosition));
                boot.Driver.Facade.BountyAndSupply.AddSaltSilver(80);
                string saved = boot.ExportSaveJson();

                Assert.IsTrue(host.TryPlaceAtWalkablePosition(original));
                Assert.IsTrue(boot.TryImportSaveJson(saved, out string err), err);
                Assert.AreEqual(savedPosition, boot.Driver.Facade.Session.Host.PlayerPosition);

                Assert.IsTrue(boot.SwitchMap(RpgWorldMapId.Soundwell));
                Assert.IsTrue(boot.TryImportSaveJson(saved, out err), err);
                Assert.AreEqual(RpgWorldMapId.RedSaltHarbor, boot.Driver.Facade.CurrentMapId);
                Assert.AreEqual(savedPosition, boot.Driver.Facade.Session.Host.PlayerPosition);
                Assert.AreEqual(80, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);

                Assert.IsTrue(RpgWorld500xSaveSnapshotSerializer.TryDeserialize(
                    saved, out RpgWorld500xSaveSnapshot invalid, out err), err);
                invalid.playerTileX = -1f;
                string invalidJson = RpgWorld500xSaveSnapshotSerializer.Serialize(invalid);
                boot.Driver.Facade.BountyAndSupply.AddSaltSilver(10);
                Assert.IsFalse(boot.TryImportSaveJson(invalidJson, out err));
                Assert.AreEqual(savedPosition, boot.Driver.Facade.Session.Host.PlayerPosition);
                Assert.AreEqual(90, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
            }
        }

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
