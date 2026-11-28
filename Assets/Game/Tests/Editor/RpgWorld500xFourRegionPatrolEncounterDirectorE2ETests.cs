using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    [TestFixture]
    public sealed class RpgWorld500xFourRegionPatrolEncounterDirectorE2ETests
    {
        [Test]
        public void AllFourRegions_PatrolDirector_SpawnsActiveThreatsAndTriggersContactEncounter()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count, "Expected exactly 4 regions in 500x world scale.");

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                RpgWorld500xMapLayout map = RpgWorld500xMapLayout.Build(spec.MapId);
                var bridge = new RpgWorldOrkExplorationBridge(
                    spec.MapId,
                    k => store.TryGetValue(k, out int v) ? v : 0,
                    (k, v) => store[k] = v);
                var director = new RpgWorld500xPatrolAndEncounterDirector(map, bridge);

                var spawn = new Vector2(map.PlayerSpawn.x + 0.5f, map.PlayerSpawn.y + 0.5f);
                Assert.IsTrue(director.TryGetNearestActiveThreat(spawn, out RpgWorldPatrolSnapshot nearest), $"Region {spec.MapId} should spawn at least one active patrol threat.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(nearest.MarkerId), $"Nearest patrol MarkerId should not be empty in {spec.MapId}.");
                Assert.AreEqual(RpgWorldPatrolState.Patrolling, nearest.State, $"Initial patrol state should be Patrolling in {spec.MapId}.");
                Assert.IsFalse(bridge.IsInDangerEncounter);

                string triggered = director.Step(nearest.Position, playerSprinting: true, deltaSeconds: 0.1f);
                Assert.AreEqual(nearest.MarkerId, triggered, $"Direct contact with '{nearest.MarkerId}' should trigger danger encounter in {spec.MapId}.");
                Assert.IsTrue(bridge.IsInDangerEncounter, $"Bridge should enter danger encounter after patrol contact in {spec.MapId}.");
            }
        }

        [Test]
        public void AllFourRegions_PatrolDirector_RemainsPatrollingWithoutEncounterWhenPlayerIsFarOutsideLeash()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count, "Expected exactly 4 regions in 500x world scale.");

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                RpgWorld500xMapLayout map = RpgWorld500xMapLayout.Build(spec.MapId);
                var bridge = new RpgWorldOrkExplorationBridge(
                    spec.MapId,
                    k => store.TryGetValue(k, out int v) ? v : 0,
                    (k, v) => store[k] = v);
                var director = new RpgWorld500xPatrolAndEncounterDirector(map, bridge);

                var spawn = new Vector2(map.PlayerSpawn.x + 0.5f, map.PlayerSpawn.y + 0.5f);
                Assert.IsTrue(director.TryGetNearestActiveThreat(spawn, out RpgWorldPatrolSnapshot nearest));

                var farOutside = new Vector2(-1000f, -1000f);
                for (int i = 0; i < 5; i++)
                {
                    string triggered = director.Step(farOutside, playerSprinting: true, deltaSeconds: 0.1f);
                    Assert.IsEmpty(triggered, $"No encounter should trigger when player is far outside leash in {spec.MapId}.");
                    Assert.IsFalse(bridge.IsInDangerEncounter, $"Bridge must not enter danger encounter when player is far outside leash in {spec.MapId}.");
                }

                Assert.IsTrue(director.TryGetNearestActiveThreat(nearest.Position, out RpgWorldPatrolSnapshot after));
                Assert.AreEqual(RpgWorldPatrolState.Patrolling, after.State, $"Patrol should remain Patrolling when player stays outside leash in {spec.MapId}.");
            }
        }
    }
}
