using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    [TestFixture]
    public sealed class RpgWorld500xFourRegionMapExplorationTrackerE2ETests
    {
        [Test]
        public void AllFourRegions_LandmarkDiscoveryExhaustion_TracksCountAndIdempotency()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count, "Expected exactly 4 regions in 500x world scale.");

            foreach (var spec in specs)
            {
                var markers = RpgWorldLandmarkCatalog.GetMarkers(spec.MapId);
                Assert.Greater(markers.Count, 0, $"Region {spec.MapId} should define at least one landmark marker.");

                var tracker = new RpgMapExplorationTracker(spec.MapId);
                Assert.AreEqual(markers.Count, tracker.TotalMarkerCount, $"TotalMarkerCount mismatch for {spec.MapId}.");
                Assert.GreaterOrEqual(tracker.DiscoveredMarkerCount, 0, $"DiscoveredMarkerCount should be non-negative for {spec.MapId}.");
                Assert.LessOrEqual(tracker.DiscoveredMarkerCount, tracker.TotalMarkerCount, $"DiscoveredMarkerCount should not exceed TotalMarkerCount for {spec.MapId}.");

                var seenIds = new HashSet<string>();
                foreach (var marker in markers)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(marker.Id), $"Marker Id should not be empty in {spec.MapId}.");
                    Assert.IsTrue(seenIds.Add(marker.Id), $"Duplicate marker Id '{marker.Id}' in {spec.MapId}.");

                    bool wasDiscovered = tracker.IsMarkerDiscovered(marker.Id);
                    int beforeCount = tracker.DiscoveredMarkerCount;

                    bool firstDiscover = tracker.TryDiscoverMarker(marker.Id);
                    Assert.AreEqual(!wasDiscovered, firstDiscover, $"TryDiscoverMarker return value mismatch for '{marker.Id}' in {spec.MapId}.");
                    Assert.IsTrue(tracker.IsMarkerDiscovered(marker.Id), $"Marker '{marker.Id}' should be discovered after TryDiscoverMarker in {spec.MapId}.");
                    Assert.AreEqual(wasDiscovered ? beforeCount : beforeCount + 1, tracker.DiscoveredMarkerCount, $"DiscoveredMarkerCount update mismatch for '{marker.Id}' in {spec.MapId}.");

                    bool secondDiscover = tracker.TryDiscoverMarker(marker.Id);
                    Assert.IsFalse(secondDiscover, $"Repeated TryDiscoverMarker for '{marker.Id}' in {spec.MapId} must be idempotent.");
                }

                Assert.AreEqual(tracker.TotalMarkerCount, tracker.DiscoveredMarkerCount, $"All landmarks should be discovered after exhaustion in {spec.MapId}.");
            }
        }

        [Test]
        public void AllFourRegions_UnknownOrCrossRegionMarkers_AreRejectedWithoutMutatingCount()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count, "Expected exactly 4 regions in 500x world scale.");

            var markerIdsByRegion = new Dictionary<RpgWorldMapId, HashSet<string>>();
            foreach (var spec in specs)
            {
                var ids = new HashSet<string>();
                foreach (var marker in RpgWorldLandmarkCatalog.GetMarkers(spec.MapId))
                {
                    ids.Add(marker.Id);
                }

                markerIdsByRegion[spec.MapId] = ids;
            }

            foreach (var spec in specs)
            {
                var tracker = new RpgMapExplorationTracker(spec.MapId);
                int initialCount = tracker.DiscoveredMarkerCount;

                Assert.IsFalse(tracker.TryDiscoverMarker(null), $"Null marker should be rejected in {spec.MapId}.");
                Assert.IsFalse(tracker.TryDiscoverMarker(string.Empty), $"Empty marker should be rejected in {spec.MapId}.");
                Assert.IsFalse(tracker.TryDiscoverMarker("   "), $"Whitespace marker should be rejected in {spec.MapId}.");
                Assert.IsFalse(tracker.TryDiscoverMarker("__unknown_landmark_id__"), $"Unknown marker should be rejected in {spec.MapId}.");
                Assert.IsFalse(tracker.IsMarkerDiscovered("__unknown_landmark_id__"), $"Unknown marker must not report discovered in {spec.MapId}.");
                Assert.AreEqual(initialCount, tracker.DiscoveredMarkerCount, $"Invalid marker attempts must not mutate count in {spec.MapId}.");

                var ownIds = markerIdsByRegion[spec.MapId];
                foreach (var otherSpec in specs)
                {
                    if (otherSpec.MapId == spec.MapId)
                    {
                        continue;
                    }

                    foreach (var foreignId in markerIdsByRegion[otherSpec.MapId])
                    {
                        if (ownIds.Contains(foreignId))
                        {
                            continue;
                        }

                        Assert.IsFalse(tracker.TryDiscoverMarker(foreignId), $"Foreign marker '{foreignId}' from {otherSpec.MapId} should be rejected in {spec.MapId}.");
                        Assert.IsFalse(tracker.IsMarkerDiscovered(foreignId), $"Foreign marker '{foreignId}' should not report discovered in {spec.MapId}.");
                        Assert.AreEqual(initialCount, tracker.DiscoveredMarkerCount, $"Foreign marker attempt must not mutate count in {spec.MapId}.");
                    }
                }
            }
        }
    }
}
