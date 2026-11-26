using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xPatrolAndEncounterDirectorTests
    {
        [Test]
        public void PursuitNeverOvershootsLeashAndReturnsToPatrolAtBoundary()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            var map = RpgWorld500xMapLayout.Build(RpgWorldMapId.Soundwell);
            var bridge = new RpgWorldOrkExplorationBridge(RpgWorldMapId.Soundwell,
                k => store.TryGetValue(k, out int v) ? v : 0, (k, v) => store[k] = v);
            var director = new RpgWorld500xPatrolAndEncounterDirector(map, bridge);
            var anchor = new Vector2(680.5f, 640.5f);

            Vector2 direction = Vector2.zero;
            foreach (var candidate in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down })
            {
                bool walkable = true;
                foreach (float distance in new[] { 18f, 34f, 36f, 50f })
                {
                    Vector2 point = anchor + candidate * distance;
                    walkable &= map.IsWalkable(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y), gateOpen: true);
                }
                if (walkable)
                {
                    direction = candidate;
                    break;
                }
            }
            Assert.AreNotEqual(Vector2.zero, direction, "Fixture needs a walkable pursuit direction.");

            foreach (float distance in new[] { 18f, 34f, 50f })
            {
                Assert.IsEmpty(director.Step(anchor + direction * distance, true, 10f));
                Assert.IsTrue(director.TryGetNearestActiveThreat(anchor, out var threat));
                Assert.AreEqual("sw.sunken-drain", threat.MarkerId);
                Assert.LessOrEqual(Vector2.Distance(anchor, threat.Position),
                    RpgWorld500xPatrolAndEncounterDirector.LeashRadiusTiles + 0.01f);
            }

            Assert.IsTrue(director.TryGetNearestActiveThreat(anchor, out var edge));
            Assert.AreEqual(RpgWorld500xPatrolAndEncounterDirector.LeashRadiusTiles,
                Vector2.Distance(anchor, edge.Position), 0.01f);
            // Small real-frame steps used to alternate patrol and chase:
            // after one inward movement the outside target became eligible again.
            Assert.IsEmpty(director.Step(anchor + direction * 50f, true, 0.1f));
            Assert.IsTrue(director.TryGetNearestActiveThreat(anchor, out var returned));
            Assert.AreEqual(RpgWorldPatrolState.Patrolling, returned.State);
            float previousDistance = Vector2.Distance(anchor, returned.Position);
            Assert.Less(previousDistance, RpgWorld500xPatrolAndEncounterDirector.LeashRadiusTiles);
            for (int i = 0; i < 10; i++)
            {
                Assert.IsEmpty(director.Step(anchor + direction * 50f, true, 0.1f));
                Assert.IsTrue(director.TryGetNearestActiveThreat(anchor, out returned));
                Assert.AreEqual(RpgWorldPatrolState.Patrolling, returned.State);
                float distance = Vector2.Distance(anchor, returned.Position);
                Assert.LessOrEqual(distance, previousDistance + 0.001f);
                previousDistance = distance;
            }
            Assert.IsFalse(bridge.IsInDangerEncounter);

            // A player returning inside the leash can draw attention again.
            Assert.IsEmpty(director.Step(anchor + direction * 18f, true, 0.1f));
            Assert.IsTrue(director.TryGetNearestActiveThreat(anchor, out var reacquired));
            Assert.AreEqual(RpgWorldPatrolState.AlertChasing, reacquired.State);
        }

        [Test]
        public void PatrolDirectorAlertsOnSprintTriggersEncounterSyncsClearedStateAndUpdatesHud()
        {
            var root = new GameObject("PatrolDirectorTestRoot");
            RpgWorld500xMinimapHud hud = null;
            try
            {
                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                RpgWorld500xMapLayout map = RpgWorld500xMapLayout.Build(RpgWorldMapId.Soundwell);
                var bridge = new RpgWorldOrkExplorationBridge(
                    RpgWorldMapId.Soundwell,
                    k => store.TryGetValue(k, out int v) ? v : 0,
                    (k, v) => store[k] = v);
                hud = new RpgWorld500xMinimapHud(
                    root.transform, map.Spec, map.GetTileId, map.UsesHarborPalette, bridge.Tracker);

                var director = new RpgWorld500xPatrolAndEncounterDirector(map, bridge);
                Assert.That(director.TotalUnitCount, Is.EqualTo(3));
                Assert.That(director.ActiveUnitCount, Is.EqualTo(3));

                // Non-finite inputs fail closed.
                Assert.That(director.Step(new Vector2(float.NaN, 0f), false, 0.2f, hud), Is.Empty);
                Assert.That(director.Step(new Vector2(680f, 640f), false, float.NaN, hud), Is.Empty);

                // Outside base detection (18 tiles away): walking stays suspicious/patrol, sprinting alerts.
                Vector2 nearDrain = new Vector2(698.5f, 640.5f);
                director.Step(nearDrain, playerSprinting: true, deltaSeconds: 0.25f, optionalHud: hud);
                Assert.That(director.TryGetNearestActiveThreat(nearDrain, out RpgWorldPatrolSnapshot threat), Is.True);
                Assert.That(threat.MarkerId, Is.EqualTo("sw.sunken-drain"));
                Assert.That(threat.State, Is.EqualTo(RpgWorldPatrolState.AlertChasing));

                // Contact triggers ORK danger encounter.
                string triggered = director.Step(threat.Position, playerSprinting: false, deltaSeconds: 0.1f, optionalHud: hud);
                Assert.That(triggered, Is.EqualTo("sw.sunken-drain"));
                Assert.That(bridge.IsInDangerEncounter, Is.True);

                // Resolving encounter clears the patrol unit permanently.
                Assert.That(bridge.TryResolveDangerZoneVictory("sw.sunken-drain", true, out _), Is.True);
                director.Step(nearDrain, playerSprinting: false, deltaSeconds: 0.1f, optionalHud: hud);
                Assert.That(director.ActiveUnitCount, Is.EqualTo(2));
                Assert.That(director.TryGetNearestActiveThreat(nearDrain, out RpgWorldPatrolSnapshot nextThreat), Is.True);
                Assert.That(nextThreat.MarkerId, Is.Not.EqualTo("sw.sunken-drain"));
            }
            finally
            {
                hud?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
