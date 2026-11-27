using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionCampRestReputationSyncE2ETests
    {
        [Test]
        public void FourRegion_StandaloneCampHavenRest_SyncsReputationIntoLiveStoreAndRestoredSaveAcrossAllFourRegions()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            for (int r = 0; r < specs.Count; r++)
            {
                RpgWorldMapId region = specs[r].MapId;
                string repKey = RpgWorld500xRegionalReputationService.ReputationKey(region);

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: region,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    var host = boot.Driver.Facade.Session.Host;
                    IReadOnlyList<RpgMapLandmarkMarker> markers = RpgWorldLandmarkCatalog.GetMarkers(region);
                    Assert.Greater(markers.Count, 0, $"Expected landmark markers on {region}.");

                    // Discover at least 3 landmarks to earn regional reputation from completion.
                    int discovered = 0;
                    for (int m = 0; m < markers.Count && discovered < 3; m++)
                    {
                        if (!host.Tracker.IsMarkerDiscovered(markers[m].Id))
                        {
                            Assert.IsTrue(host.Tracker.TryDiscoverMarker(markers[m].Id));
                            discovered++;
                        }
                    }

                    Assert.Greater(discovered, 0, $"Expected at least one newly discovered marker on {region}.");

                    // Standalone CampHavenService (no external store delegates) must still sync
                    // authoritative reputation into the live bootstrapper store and exported save.
                    var standaloneHaven = new RpgWorld500xCampHavenService();
                    RpgWorld500xCampHavenSummary rested = standaloneHaven.RestAndSyncCampHaven(boot);

                    Assert.Greater(rested.Reputation.Points, 0, $"Expected positive synced reputation on {region}.");
                    Assert.IsTrue(boot.Store.TryGetValue(repKey, out int liveSavedPoints),
                        $"Expected reputation key '{repKey}' to be written into live bootstrapper store.");
                    Assert.AreEqual(rested.Reputation.Points, liveSavedPoints);
                    Assert.IsFalse(string.IsNullOrEmpty(rested.SaveSnapshotJson));

                    using (var restored = new RpgWorld500xSceneBootstrapper(
                        initialMapId: region,
                        overrideTileSlices: new Sprite[] { null }))
                    {
                        Assert.IsTrue(restored.TryImportSaveJson(rested.SaveSnapshotJson, out string err), err);
                        Assert.AreEqual(region, restored.Driver.Facade.CurrentMapId);
                        Assert.IsTrue(restored.Store.TryGetValue(repKey, out int reloadedPoints));
                        Assert.AreEqual(liveSavedPoints, reloadedPoints);

                        var reader = new RpgWorld500xRegionalReputationService(
                            k => restored.Store.TryGetValue(k, out int v) ? v : 0);
                        RpgRegionalReputationSnapshot reloadedRep = reader.GetSnapshot(region);
                        Assert.AreEqual(rested.Reputation.Points, reloadedRep.Points);
                        Assert.AreEqual(rested.Reputation.Rank, reloadedRep.Rank);
                        Assert.AreEqual(rested.Reputation.CampSupplyDiscountPercent, reloadedRep.CampSupplyDiscountPercent);
                    }
                }
            }
        }
    }
}
