using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionSceneBootstrapperRoundTripE2ETests
    {
        [Test]
        public void FourRegion_SceneBootstrapper_SameMapLoadRollsBackDiscoveryVictoryAndInFlightEncounter()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            for (int i = 0; i < specs.Count; i++)
            {
                RpgWorldMapId region = specs[i].MapId;
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: region,
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

                    Assert.IsTrue(found, $"Expected an undiscovered DangerZone marker outside spawn radius on {region}.");
                    string saved = boot.ExportSaveJson();
                    Vector2 encounterPosition = new Vector2(danger.Tile.x + 0.5f, danger.Tile.y + 0.5f);

                    Assert.IsTrue(host.Bridge.TryTriggerDangerZoneEncounter(danger.Id, encounterPosition));
                    Assert.IsTrue(host.Bridge.TryResolveDangerZoneVictory(danger.Id, true, out _));
                    Assert.IsTrue(host.Tracker.IsMarkerDiscovered(danger.Id));
                    Assert.AreEqual(1, host.Bridge.ClearedDangerZoneCount);

                    // Same-map import retains existing host and rolls back cached discovery/victory state.
                    Assert.IsTrue(boot.TryImportSaveJson(saved, out string err), err);
                    Assert.AreSame(host, boot.Driver.Facade.Session.Host);
                    Assert.IsFalse(host.Tracker.IsMarkerDiscovered(danger.Id));
                    Assert.AreEqual(0, host.Bridge.ClearedDangerZoneCount);

                    // Loading while a danger encounter is in flight clears transient lock cleanly.
                    Assert.IsTrue(host.Bridge.TryTriggerDangerZoneEncounter(danger.Id, encounterPosition));
                    Assert.IsTrue(host.Bridge.IsInDangerEncounter);
                    Assert.IsTrue(boot.TryImportSaveJson(saved, out err), err);
                    Assert.IsFalse(host.Bridge.IsInDangerEncounter);
                    Assert.IsFalse(host.Tracker.IsMarkerDiscovered(danger.Id));
                    Assert.IsTrue(host.Bridge.TryTriggerDangerZoneEncounter(danger.Id, encounterPosition));
                }
            }
        }

        [Test]
        public void FourRegion_SceneBootstrapper_CrossMapSaveImportRestoresRegionWalletAndRejectsCorruptPayload()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            RpgWorld500xSceneBootstrapper disposedBoot;
            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: specs[0].MapId,
                overrideTileSlices: new Sprite[] { null }))
            {
                disposedBoot = boot;
                string[] snapshots = new string[specs.Count];
                int[] expectedSilver = new int[specs.Count];

                for (int i = 0; i < specs.Count; i++)
                {
                    RpgWorldMapId region = specs[i].MapId;
                    if (boot.Driver.Facade.CurrentMapId != region)
                    {
                        Assert.IsTrue(boot.SwitchMap(region));
                    }

                    boot.Driver.Facade.BountyAndSupply.AddSaltSilver(150);
                    expectedSilver[i] = boot.Driver.Facade.BountyAndSupply.SaltSilverBalance;
                    snapshots[i] = boot.ExportSaveJson();
                    Assert.IsFalse(string.IsNullOrEmpty(snapshots[i]));
                }

                // Restore all 4 regional snapshots in reverse order.
                for (int i = specs.Count - 1; i >= 0; i--)
                {
                    Assert.IsTrue(boot.TryImportSaveJson(snapshots[i], out string err), err);
                    Assert.AreEqual(specs[i].MapId, boot.Driver.Facade.CurrentMapId);
                    Assert.AreEqual(expectedSilver[i], boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                }

                // Corrupt and empty save payloads fail closed without mutating active state.
                RpgWorldMapId mapBeforeCorrupt = boot.Driver.Facade.CurrentMapId;
                int silverBeforeCorrupt = boot.Driver.Facade.BountyAndSupply.SaltSilverBalance;

                Assert.IsFalse(boot.TryImportSaveJson("{corrupt-save", out string corruptErr));
                Assert.IsFalse(string.IsNullOrEmpty(corruptErr));
                Assert.IsFalse(boot.TryImportSaveJson(string.Empty, out string emptyErr));
                Assert.IsFalse(string.IsNullOrEmpty(emptyErr));
                Assert.AreEqual(mapBeforeCorrupt, boot.Driver.Facade.CurrentMapId);
                Assert.AreEqual(silverBeforeCorrupt, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
            }

            Assert.IsTrue(disposedBoot.IsDisposed);
            Assert.IsFalse(disposedBoot.SwitchMap(RpgWorldMapId.MineCamp));
            Assert.IsFalse(disposedBoot.TryImportSaveJson("{}", out _));
        }
    }
}
