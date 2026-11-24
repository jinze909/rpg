using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFieldNpcsAndAmbientEventsTests
    {
        [Test]
        public void All24Npcs16StorySpotsAnd12AmbientZonesAreValidWalkableAndReferentiallySound()
        {
            var globalIds = new HashSet<string>(StringComparer.Ordinal);
            var globalKeys = new HashSet<string>(StringComparer.Ordinal);
            int totalNpcs = 0, totalSpots = 0, totalZones = 0;

            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                RpgWorld500xMapLayout map = RpgWorld500xMapLayout.Build(spec.MapId);
                IReadOnlyList<RpgFieldNpcDef> npcs = RpgWorld500xLivingWorldCatalog.GetFieldNpcs(spec.MapId);
                IReadOnlyList<RpgStorySpotDef> spots = RpgWorld500xLivingWorldCatalog.GetStorySpots(spec.MapId);
                IReadOnlyList<RpgAmbientMicroEventZoneDef> zones = RpgWorld500xLivingWorldCatalog.GetAmbientZones(spec.MapId);

                Assert.That(npcs.Count, Is.EqualTo(6), spec.DisplayNameEn);
                Assert.That(spots.Count, Is.EqualTo(4), spec.DisplayNameEn);
                Assert.That(zones.Count, Is.EqualTo(3), spec.DisplayNameEn);

                foreach (RpgFieldNpcDef npc in npcs)
                {
                    Assert.That(globalIds.Add(npc.Id), Is.True, npc.Id);
                    Assert.That(globalKeys.Add(npc.RumorMakinomKey), Is.True, npc.RumorMakinomKey);
                    Assert.That(map.IsWalkable(npc.Tile.x, npc.Tile.y, gateOpen: true), Is.True,
                        spec.DisplayNameEn + " NPC must stand on walkable tile: " + npc.Id);
                    if (!string.IsNullOrEmpty(npc.RumorTargetMarkerId))
                    {
                        Assert.That(RpgWorldLandmarkCatalog.TryGetMarker(
                            spec.MapId, npc.RumorTargetMarkerId, out _), Is.True,
                            "Rumor target must exist on same map: " + npc.Id);
                    }
                }

                foreach (RpgStorySpotDef spot in spots)
                {
                    Assert.That(globalIds.Add(spot.Id), Is.True, spot.Id);
                    Assert.That(globalKeys.Add(spot.MakinomKey), Is.True, spot.MakinomKey);
                    Assert.That(map.IsWalkable(spot.Tile.x, spot.Tile.y, gateOpen: true), Is.True,
                        spec.DisplayNameEn + " story spot must be walkable: " + spot.Id);
                }

                foreach (RpgAmbientMicroEventZoneDef z in zones)
                {
                    Assert.That(globalIds.Add(z.Id), Is.True, z.Id);
                    Assert.That(z.CycleSeconds, Is.GreaterThan(z.ActiveSeconds), z.Id);
                    Assert.That(z.ActiveSeconds, Is.GreaterThan(0f), z.Id);
                }

                totalNpcs += npcs.Count;
                totalSpots += spots.Count;
                totalZones += zones.Count;
            }

            Assert.That(totalNpcs, Is.EqualTo(24));
            Assert.That(totalSpots, Is.EqualTo(16));
            Assert.That(totalZones, Is.EqualTo(12));
        }

        [Test]
        public void AllRegionalRumorsCountOnceWhenLandmarkDiscoveredFirst()
        {
            AllRegionalRumorsCountOnceInEitherDiscoveryOrderAndAdvanceNavigation(true);
        }

        [Test]
        public void AllRegionalRumorsCountOnceWhenConversationFirst()
        {
            AllRegionalRumorsCountOnceInEitherDiscoveryOrderAndAdvanceNavigation(false);
        }

        private void AllRegionalRumorsCountOnceInEitherDiscoveryOrderAndAdvanceNavigation(bool discoverFirst)
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            int writes = 0;
            Func<string, int> read = key => store.TryGetValue(key, out int value) ? value : 0;
            Action<string, int> write = (key, value) => { store[key] = value; writes++; };

            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                var layout = RpgWorld500xMapLayout.Build(spec.MapId);
                var tracker = new RpgMapExplorationTracker(spec.MapId);
                var service = new RpgWorld500xLivingWorldService(spec.MapId, tracker, read, write);
                foreach (RpgFieldNpcDef npc in RpgWorld500xLivingWorldCatalog.GetFieldNpcs(spec.MapId))
                {
                    Vector2 pos = new Vector2(npc.Tile.x + 0.5f, npc.Tile.y + 0.5f);
                    Assert.IsTrue(RpgWorld500xWaypointNavigator.TryFindNearestTarget(
                        layout, pos, RpgWorld500xWaypointCategory.FieldNpc,
                        out RpgWorld500xWaypointGuidance before, tracker, getInt: read));
                    Assert.AreEqual(npc.Id, before.EntityId);
                    if (discoverFirst)
                        tracker.TryDiscoverMarker(npc.RumorTargetMarkerId);
                    string trackedBefore = tracker.TrackedMarkerId;
                    int previousWrites = writes;
                    Assert.IsTrue(service.TryTalkToNearbyNpc(pos, 6f, out _, out string hint, out string revealed));
                    Assert.AreEqual(npc.RumorHintZh, hint);
                    Assert.AreEqual(1, read(npc.RumorMakinomKey));
                    Assert.AreEqual(previousWrites + 1, writes);
                    Assert.AreEqual(discoverFirst ? string.Empty : npc.RumorTargetMarkerId, revealed);
                    Assert.AreEqual(discoverFirst ? trackedBefore : npc.RumorTargetMarkerId, tracker.TrackedMarkerId);

                    // Repeat conversations remain available, but never count twice.
                    tracker.TryDiscoverMarker(npc.RumorTargetMarkerId);
                    Assert.IsTrue(service.TryTalkToNearbyNpc(pos, 6f, out _, out string ambient, out string repeated));
                    Assert.AreEqual(npc.AmbientLineZh, ambient);
                    Assert.IsEmpty(repeated);
                    Assert.AreEqual(previousWrites + 1, writes);
                    var nearby = RpgWorld500xWaypointNavigator.FindTopNearbyPointsOfInterest(
                        layout, pos, 100, tracker, getInt: read);
                    Assert.IsFalse(nearby.Exists(p => p.Category == RpgWorld500xWaypointCategory.FieldNpc &&
                        p.EntityId == npc.Id));
                }

                Assert.IsFalse(RpgWorld500xWaypointNavigator.TryFindNearestTarget(
                    layout, Vector2.zero, RpgWorld500xWaypointCategory.FieldNpc, out _, tracker, getInt: read));
            }

            Assert.AreEqual(24, writes);
            var milestones = new RpgWorld500xExpeditionMilestoneService(read);
            Assert.AreEqual(24, milestones.ComputeCrossMapProgress().HeardRumors);
        }

        [Test]
        public void RumorBrokerTracksUndiscoveredLandmarkStorySpotsPersistAndAmbientCycleActivatesDeterministically()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
            Action<string, int> write = (k, v) => store[k] = v;

            var tracker = new RpgMapExplorationTracker(RpgWorldMapId.WhitewaterCrossing);
            var service = new RpgWorld500xLivingWorldService(
                RpgWorldMapId.WhitewaterCrossing, tracker, read, write);

            // Talking to rumor broker reveals and tracks undiscovered landmark.
            Assert.That(service.TryTalkToNearbyNpc(new Vector2(280.5f, 520.5f), 6f,
                out RpgFieldNpcDef npc, out string line, out string revealedId), Is.True);
            Assert.That(npc.Id, Is.EqualTo("ww_npc_01"));
            Assert.That(revealedId, Is.EqualTo("ww.supply-wagons"));
            Assert.That(tracker.TrackedMarkerId, Is.EqualTo("ww.supply-wagons"));
            Assert.That(line, Does.Contain("辎重车阵"));

            // Once landmark is discovered, broker switches to ambient chatter.
            Assert.That(tracker.TryDiscoverMarker("ww.supply-wagons"), Is.True);
            Assert.That(service.TryTalkToNearbyNpc(new Vector2(280.5f, 520.5f), 6f,
                out _, out string ambientLine, out string revealedAgain), Is.True);
            Assert.That(revealedAgain, Is.Empty);
            Assert.That(ambientLine, Does.Contain("浮桥主缆"));

            // Inspect story spot: first time sets Makinom key, second time reports already inspected.
            Assert.That(service.TryInspectNearbyStorySpot(new Vector2(1400.5f, 280.5f), 6f,
                out RpgStorySpotDef spot, out bool first), Is.True);
            Assert.That(spot.Id, Is.EqualTo("ww_lore_03"));
            Assert.That(first, Is.True);
            Assert.That(service.TryInspectNearbyStorySpot(new Vector2(1400.5f, 280.5f), 6f,
                out _, out bool second), Is.True);
            Assert.That(second, Is.False);

            // Ambient FogGust on pontoon activates in [0, 12s) and turns neutral in [12s, 38s).
            RpgActiveAmbientState activeFog = service.EvaluateAmbientEvent(new Vector2(896f, 496f), 5f);
            Assert.That(activeFog.IsActive, Is.True);
            Assert.That(activeFog.Kind, Is.EqualTo(RpgAmbientMicroEventKind.FogGust));
            Assert.That(activeFog.PatrolSightMultiplier, Is.LessThan(1f));

            RpgActiveAmbientState calm = service.EvaluateAmbientEvent(new Vector2(896f, 496f), 20f);
            Assert.That(calm.IsActive, Is.False);
            Assert.That(calm.PatrolSightMultiplier, Is.EqualTo(1f));

            Assert.That(service.EvaluateAmbientEvent(new Vector2(float.NaN, 0f), 5f).IsActive, Is.False);
        }
    }
}
