using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionPatrolAndMinimapE2ETests
    {
        [Test]
        public void AllFourRegions_PatrolSprintAlertEncounterClearAndMinimapThreatSyncWithZeroLeak()
        {
            // Warm up one-time UGUI/font caches before recording steady-state leak baseline.
            var warmupRoot = new GameObject("World500xFourRegionPatrolWarmupRoot");
            RpgWorld500xMinimapHud warmupHud = null;
            try
            {
                var warmupStore = new Dictionary<string, int>(StringComparer.Ordinal);
                RpgWorld500xMapLayout warmupLayout = RpgWorld500xMapLayout.Build(RpgWorldMapId.Soundwell);
                var warmupBridge = new RpgWorldOrkExplorationBridge(
                    RpgWorldMapId.Soundwell,
                    k => warmupStore.TryGetValue(k, out int v) ? v : 0,
                    (k, v) => warmupStore[k] = v);
                warmupHud = new RpgWorld500xMinimapHud(
                    warmupRoot.transform,
                    warmupLayout.Spec,
                    warmupLayout.GetTileId,
                    warmupLayout.UsesHarborPalette,
                    warmupBridge.Tracker);
                warmupHud.UpdateThreatMarker(Vector2.zero, visible: false);
            }
            finally
            {
                warmupHud?.Dispose();
                UnityEngine.Object.DestroyImmediate(warmupRoot);
            }

            int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

            int verifiedRegions = 0;
            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                var root = new GameObject("World500xFourRegionPatrolE2E_" + spec.MapId);
                RpgWorld500xMinimapHud hud = null;
                try
                {
                    var store = new Dictionary<string, int>(StringComparer.Ordinal);
                    RpgWorld500xMapLayout layout = RpgWorld500xMapLayout.Build(spec.MapId);
                    var bridge = new RpgWorldOrkExplorationBridge(
                        spec.MapId,
                        k => store.TryGetValue(k, out int v) ? v : 0,
                        (k, v) => store[k] = v);
                    hud = new RpgWorld500xMinimapHud(
                        root.transform,
                        layout.Spec,
                        layout.GetTileId,
                        layout.UsesHarborPalette,
                        bridge.Tracker);

                    var director = new RpgWorld500xPatrolAndEncounterDirector(layout, bridge);
                    Assert.Greater(director.TotalUnitCount, 0, "Expected danger patrol units in " + spec.MapId);
                    Assert.AreEqual(director.TotalUnitCount, director.ActiveUnitCount);

                    // Non-finite inputs fail closed without triggering encounters.
                    Assert.IsEmpty(director.Step(new Vector2(float.NaN, 0f), false, 0.2f, hud));
                    Assert.IsEmpty(director.Step(new Vector2(0f, 0f), false, float.PositiveInfinity, hud));
                    Assert.IsFalse(bridge.IsInDangerEncounter);

                    RpgMapLandmarkMarker firstDanger = default;
                    bool foundDanger = false;
                    for (int i = 0; i < layout.Markers.Count; i++)
                    {
                        if (layout.Markers[i].Category == RpgMapMarkerCategory.DangerZone)
                        {
                            firstDanger = layout.Markers[i];
                            foundDanger = true;
                            break;
                        }
                    }

                    Assert.IsTrue(foundDanger, "Expected DangerZone marker in " + spec.MapId);
                    Vector2 anchor = new Vector2(firstDanger.Tile.x + 0.5f, firstDanger.Tile.y + 0.5f);

                    // Sprinting at 18 tiles away triggers AlertChasing within sprint detection radius (22 tiles).
                    Vector2 sprintProbe = anchor + new Vector2(18f, 0f);
                    Assert.IsEmpty(director.Step(sprintProbe, playerSprinting: true, deltaSeconds: 0.1f, optionalHud: hud));
                    Assert.IsTrue(director.TryGetNearestActiveThreat(anchor, out RpgWorldPatrolSnapshot chased));
                    Assert.AreEqual(firstDanger.Id, chased.MarkerId);
                    Assert.AreEqual(RpgWorldPatrolState.AlertChasing, chased.State);

                    // Contact triggers the ORK danger encounter, and victory clears the patrol unit.
                    string triggered = director.Step(chased.Position, playerSprinting: false, deltaSeconds: 0.1f, optionalHud: hud);
                    Assert.AreEqual(firstDanger.Id, triggered);
                    Assert.IsTrue(bridge.IsInDangerEncounter);
                    Assert.IsTrue(bridge.TryResolveDangerZoneVictory(firstDanger.Id, true, out _));
                    Assert.IsFalse(bridge.IsInDangerEncounter);

                    Assert.IsEmpty(director.Step(anchor, playerSprinting: false, deltaSeconds: 0.1f, optionalHud: hud));
                    Assert.AreEqual(director.TotalUnitCount - 1, director.ActiveUnitCount);

                    // Clear all remaining patrol units in the region and verify threat tracking empties cleanly.
                    int safetyGuard = director.TotalUnitCount + 2;
                    while (director.TryGetNearestActiveThreat(anchor, out RpgWorldPatrolSnapshot remaining))
                    {
                        Assert.Greater(safetyGuard--, 0, "Exceeded expected patrol count in " + spec.MapId);
                        Assert.AreNotEqual(firstDanger.Id, remaining.MarkerId);

                        string nextTriggered = director.Step(remaining.Position, playerSprinting: false, deltaSeconds: 0.1f, optionalHud: hud);
                        Assert.AreEqual(remaining.MarkerId, nextTriggered);
                        Assert.IsTrue(bridge.IsInDangerEncounter);
                        Assert.IsTrue(bridge.TryResolveDangerZoneVictory(remaining.MarkerId, true, out _));
                        Assert.IsEmpty(director.Step(anchor, playerSprinting: false, deltaSeconds: 0.1f, optionalHud: hud));
                    }

                    Assert.AreEqual(0, director.ActiveUnitCount);
                    Assert.IsFalse(director.TryGetNearestActiveThreat(anchor, out _));
                    verifiedRegions++;
                }
                finally
                {
                    hud?.Dispose();
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }

            Assert.AreEqual(4, verifiedRegions);
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
        }
    }
}
