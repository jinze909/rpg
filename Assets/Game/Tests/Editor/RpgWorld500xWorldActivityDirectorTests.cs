using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xWorldActivityDirectorTests
    {
        [Test]
        public void PriorityInteraction_HandlesShortcutCacheNpcAndStorySpotWithToasts()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            var toasts = new List<string>();

            RpgWorld500xMapLayout soundwell = RpgWorld500xMapLayout.Build(RpgWorldMapId.Soundwell);
            var tracker = new RpgMapExplorationTracker(RpgWorldMapId.Soundwell);
            var director = new RpgWorld500xWorldActivityDirector(
                soundwell,
                tracker,
                k => store.TryGetValue(k, out int v) ? v : 0,
                (k, v) => store[k] = v,
                onToast: (title, body) => toasts.Add(title + ":" + body));

            // 1. Unlock shortcut sw_sc_entry_north at (380, 920)
            Assert.IsTrue(director.TryInteractPriority(new Vector2(380.5f, 920.5f), 6f, out RpgWorld500xInteractionResult scRes));
            Assert.AreEqual(RpgWorld500xInteractionKind.ShortcutUnlocked, scRes.Kind);
            Assert.AreEqual("sw_sc_entry_north", scRes.EntityId);
            Assert.IsTrue(scRes.FirstTimeDiscovery);

            // 2. Loot cache sw_c03 at (290, 780)
            Assert.IsTrue(director.TryInteractPriority(new Vector2(290.5f, 780.5f), 6f, out RpgWorld500xInteractionResult cacheRes));
            Assert.AreEqual(RpgWorld500xInteractionKind.CacheLooted, cacheRes.Kind);
            Assert.AreEqual("sw_c03", cacheRes.EntityId);
            Assert.AreEqual(30, cacheRes.SaltSilverGranted);
            Assert.AreEqual(30, director.TotalSaltSilverEarned);

            // 3. Talk to rumor broker sw_npc_01 at (250, 690)
            Assert.IsTrue(director.TryInteractPriority(new Vector2(250.5f, 690.5f), 6f, out RpgWorld500xInteractionResult npcRes));
            Assert.AreEqual(RpgWorld500xInteractionKind.NpcDialogue, npcRes.Kind);
            Assert.AreEqual("sw_npc_01", npcRes.EntityId);
            Assert.AreEqual("sw.old-wellhead", npcRes.RevealedMarkerId);
            Assert.AreEqual("sw.old-wellhead", tracker.TrackedMarkerId);

            // 4. Inspect story spot sw_lore_01 at (160, 720)
            Assert.IsTrue(director.TryInteractPriority(new Vector2(160.5f, 720.5f), 6f, out RpgWorld500xInteractionResult spotRes));
            Assert.AreEqual(RpgWorld500xInteractionKind.StorySpotInspected, spotRes.Kind);
            Assert.AreEqual("sw_lore_01", spotRes.EntityId);
            Assert.IsTrue(spotRes.FirstTimeDiscovery);

            Assert.AreEqual(4, director.InteractionCount);
            Assert.AreEqual(4, toasts.Count);
        }

        [Test]
        public void RumorInteraction_FirstDiscoveryTracksHearingRatherThanWaypointReveal()
        {
            foreach (bool discoverLandmarkFirst in new[] { false, true })
            {
                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                var layout = RpgWorld500xMapLayout.Build(RpgWorldMapId.Soundwell);
                var tracker = new RpgMapExplorationTracker(RpgWorldMapId.Soundwell);
                var director = new RpgWorld500xWorldActivityDirector(
                    layout, tracker,
                    k => store.TryGetValue(k, out int v) ? v : 0,
                    (k, v) => store[k] = v);
                var npc = RpgWorld500xLivingWorldCatalog.GetFieldNpcs(RpgWorldMapId.Soundwell)[0];
                Assert.AreEqual("sw_npc_01", npc.Id);
                if (discoverLandmarkFirst)
                    Assert.IsTrue(tracker.TryDiscoverMarker(npc.RumorTargetMarkerId));

                var position = new Vector2(npc.Tile.x + 0.5f, npc.Tile.y + 0.5f);
                Assert.IsTrue(director.TryInteractPriority(position, 6f, out var first));
                Assert.AreEqual(RpgWorld500xInteractionKind.NpcDialogue, first.Kind);
                Assert.IsTrue(first.FirstTimeDiscovery);
                Assert.AreEqual(1, store[npc.RumorMakinomKey]);
                Assert.AreEqual(discoverLandmarkFirst ? string.Empty : npc.RumorTargetMarkerId,
                    first.RevealedMarkerId);

                // Until visiting the landmark, a repeat hint may still reveal a waypoint,
                // but must never be reported as a newly heard rumor.
                Assert.IsTrue(director.TryInteractPriority(position, 6f, out var repeat));
                Assert.AreEqual(RpgWorld500xInteractionKind.NpcDialogue, repeat.Kind);
                Assert.IsFalse(repeat.FirstTimeDiscovery);
                Assert.AreEqual(first.RevealedMarkerId, repeat.RevealedMarkerId);
                Assert.AreEqual(1, director.LivingWorld.BuildLivingWorldProgressSummary().HeardRumorCount);
            }
        }

        [Test]
        public void StepEnvironment_CombinesWeatherAndAmbientMicroEventsAndSupportsCampfireRest()
        {
            RpgWorld500xMapLayout ww = RpgWorld500xMapLayout.Build(RpgWorldMapId.WhitewaterCrossing);
            var tracker = new RpgMapExplorationTracker(RpgWorldMapId.WhitewaterCrossing);
            var atmosphere = new RpgWorld500xWeatherAndTimeOfDayService(initialMinutes: 1320f, initialDay: 2);
            atmosphere.SetWeather(RpgWorldMapId.WhitewaterCrossing, RpgWorld500xWeatherKind.GaleStorm);

            var director = new RpgWorld500xWorldActivityDirector(ww, tracker, atmosphere: atmosphere);

            // Stand on pontoon during active FogGust (t = 5s) + Night + GaleStorm
            RpgWorld500xEffectiveExplorationModifiers mods =
                director.StepEnvironment(new Vector2(896f, 496f), 1f, 5f);

            Assert.IsTrue(mods.AmbientEvent.IsActive);
            Assert.AreEqual(RpgAmbientMicroEventKind.FogGust, mods.AmbientEvent.Kind);
            Assert.Less(mods.EffectivePatrolSightMultiplier, 0.70f);
            Assert.Less(mods.EffectiveMoveSpeedMultiplier, 1.0f);
            Assert.IsTrue(mods.CombinedStatusCaptionZh.Contains("白水江雾"));

            // Campfire rest jumps to Day 3 06:00 Dawn and clears GaleStorm
            RpgWorld500xAtmosphereSnapshot dawn = director.RestAtCampfire();
            Assert.AreEqual(3, dawn.DayCount);
            Assert.AreEqual("06:00", dawn.FormattedClock);
            Assert.AreEqual(RpgWorld500xTimePhase.Dawn, dawn.Phase);
            Assert.AreEqual(RpgWorld500xWeatherKind.Clear, dawn.Weather);
        }
    }
}
