using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionWorldActivityDirectorE2ETests
    {
        [Test]
        public void AllFourRegions_WorldActivityDirectorTracksFirstHearingStorySpotCacheAndShortcutConsistently()
        {
            int verifiedRegions = 0;

            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                var npcs = RpgWorld500xLivingWorldCatalog.GetFieldNpcs(spec.MapId);
                var spots = RpgWorld500xLivingWorldCatalog.GetStorySpots(spec.MapId);
                var caches = RpgWorld500xContentCatalog.GetCaches(spec.MapId);
                var shortcuts = RpgWorld500xContentCatalog.GetShortcuts(spec.MapId);

                Assert.IsNotEmpty(npcs, "Expected field NPCs in " + spec.MapId);
                Assert.IsNotEmpty(spots, "Expected story spots in " + spec.MapId);
                Assert.IsNotEmpty(caches, "Expected supply caches in " + spec.MapId);
                Assert.IsNotEmpty(shortcuts, "Expected shortcuts in " + spec.MapId);

                foreach (bool discoverLandmarkFirst in new[] { false, true })
                {
                    var store = new Dictionary<string, int>(StringComparer.Ordinal);
                    var layout = RpgWorld500xMapLayout.Build(spec.MapId);
                    var tracker = new RpgMapExplorationTracker(spec.MapId);
                    var director = new RpgWorld500xWorldActivityDirector(
                        layout,
                        tracker,
                        k => store.TryGetValue(k, out int v) ? v : 0,
                        (k, v) => store[k] = v);

                    // Non-finite probe fails closed.
                    Assert.IsFalse(director.TryInteractPriority(new Vector2(float.NaN, 0f), 6f, out _));
                    Assert.AreEqual(0, director.InteractionCount);

                    RpgFieldNpcDef npc = npcs[0];
                    if (discoverLandmarkFirst && !string.IsNullOrEmpty(npc.RumorTargetMarkerId))
                    {
                        tracker.TryDiscoverMarker(npc.RumorTargetMarkerId);
                    }

                    bool targetKnownBefore = !string.IsNullOrEmpty(npc.RumorTargetMarkerId) &&
                                             tracker.IsMarkerDiscovered(npc.RumorTargetMarkerId);
                    Vector2 npcPos = new Vector2(npc.Tile.x + 0.5f, npc.Tile.y + 0.5f);

                    Assert.IsTrue(director.TryInteractPriority(npcPos, 0.5f, out var firstTalk));
                    Assert.AreEqual(RpgWorld500xInteractionKind.NpcDialogue, firstTalk.Kind);
                    Assert.IsTrue(firstTalk.FirstTimeDiscovery);
                    Assert.AreEqual(1, store[npc.RumorMakinomKey]);
                    Assert.AreEqual(
                        targetKnownBefore ? string.Empty : (npc.RumorTargetMarkerId ?? string.Empty),
                        firstTalk.RevealedMarkerId);

                    // Repeat talk never double-counts as a newly heard rumor.
                    Assert.IsTrue(director.TryInteractPriority(npcPos, 0.5f, out var repeatTalk));
                    Assert.AreEqual(RpgWorld500xInteractionKind.NpcDialogue, repeatTalk.Kind);
                    Assert.IsFalse(repeatTalk.FirstTimeDiscovery);
                    Assert.AreEqual(firstTalk.RevealedMarkerId, repeatTalk.RevealedMarkerId);
                    Assert.AreEqual(1, director.LivingWorld.BuildLivingWorldProgressSummary().HeardRumorCount);

                    // Story spot, supply cache, and shortcut interactions persist cleanly in the same director.
                    RpgStorySpotDef spot = spots[0];
                    Vector2 spotPos = new Vector2(spot.Tile.x + 0.5f, spot.Tile.y + 0.5f);
                    Assert.IsTrue(director.TryInteractPriority(spotPos, 0.5f, out _));
                    Assert.AreEqual(1, store[spot.MakinomKey]);
                    Assert.AreEqual(1, director.LivingWorld.BuildLivingWorldProgressSummary().InspectedStorySpotCount);

                    RpgSupplyCacheDef cache = caches[0];
                    Vector2 cachePos = new Vector2(cache.Tile.x + 0.5f, cache.Tile.y + 0.5f);
                    Assert.IsTrue(director.TryInteractPriority(cachePos, 0.5f, out _));
                    Assert.AreEqual(1, store[cache.MakinomKey]);

                    RpgWorldShortcutDef shortcut = shortcuts[0];
                    Vector2 shortcutPos = new Vector2(shortcut.TriggerTile.x + 0.5f, shortcut.TriggerTile.y + 0.5f);
                    Assert.IsTrue(director.TryInteractPriority(shortcutPos, 0.5f, out _));
                    Assert.AreEqual(1, store[shortcut.MakinomKey]);

                    Assert.AreEqual(5, director.InteractionCount);
                }

                verifiedRegions++;
            }

            Assert.AreEqual(4, verifiedRegions);
        }
    }
}
