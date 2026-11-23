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
