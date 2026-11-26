using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorldOrkExplorationBridgeTests
    {
        [Test]
        public void CampRestDangerEncounterFastTravelAndQuestRewardsRunEndToEndAndPersistIdempotently()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
            Action<string, int> write = (k, v) => store[k] = v;

            int partyRecoverCalls = 0;
            int rewardCalls = 0;
            var bridge = new RpgWorldOrkExplorationBridge(
                RpgWorldMapId.Soundwell,
                read,
                write,
                onPartyRecover: _ => partyRecoverCalls++,
                onRewardGranted: (_, __) => rewardCalls++);

            // Sprint scaling rejects non-finite speeds cleanly.
            Assert.That(RpgWorldOrkExplorationBridge.ComputeEffectiveMoveSpeed(float.NaN, true, false), Is.Zero);
            Assert.That(RpgWorldOrkExplorationBridge.ComputeEffectiveMoveSpeed(6f, false, false), Is.EqualTo(6f));
            Assert.That(RpgWorldOrkExplorationBridge.ComputeEffectiveMoveSpeed(6f, true, false),
                Is.EqualTo(6f * RpgWorldOrkExplorationBridge.SprintSpeedMultiplier));
            Assert.That(RpgWorldOrkExplorationBridge.ComputeEffectiveMoveSpeed(6f, true, true),
                Is.EqualTo(6f * RpgWorldOrkExplorationBridge.DangerSprintSpeedMultiplier));

            // Discover entry beacon and verify fast travel works once discovered.
            Assert.That(bridge.TryFastTravel("sw.entry-shaft", (x, y) => true, out _), Is.False);
            Assert.That(bridge.UpdatePlayerPosition(new Vector2(224f, 672f)), Is.EqualTo(1));
            Assert.That(bridge.TryFastTravel("sw.entry-shaft", (x, y) => true, out RpgFastTravelResult ft), Is.True);
            Assert.That(ft.DestinationTile, Is.EqualTo(new Vector2Int(224, 672)));
            Assert.That(ft.TotalFastTravels, Is.EqualTo(1));

            // Non-walkable fast travel target fails closed.
            Assert.That(bridge.TryFastTravel("sw.entry-shaft", (x, y) => false, out _), Is.False);

            // Camp rest requires proximity and CampAndSupply category.
            Assert.That(bridge.TryRestAtCamp("sw.moss-cistern", new Vector2(224f, 672f), out _), Is.False);
            Assert.That(bridge.TryRestAtCamp("sw.entry-shaft", new Vector2(224f, 672f), out _), Is.False);
            Assert.That(bridge.TryRestAtCamp("sw.moss-cistern", new Vector2(1180f, 1120f), out RpgWorldCampRestResult rest), Is.True);
            Assert.That(rest.PartyRecovered, Is.True);
            Assert.That(rest.TotalCampRests, Is.EqualTo(1));
            Assert.That(partyRecoverCalls, Is.EqualTo(1));

            // Even a direct bridge caller cannot claim a danger quest without victory.
            string questKey = RpgMapExplorationTracker.QuestProgressKey("q.sw.clear-drain-grate");
            int progressBeforeVictory = read(questKey);
            Assert.That(bridge.TryAdvanceLandmarkQuest("q.sw.clear-drain-grate", int.MaxValue, out _), Is.False);
            Assert.That(read(questKey), Is.EqualTo(progressBeforeVictory));
            Assert.That(bridge.IsQuestRewarded("q.sw.clear-drain-grate"), Is.False);
            Assert.That(rewardCalls, Is.Zero);

            // DangerZone encounter blocks fast-travel and camp rest until resolved, then completes linked quest once.
            Assert.That(bridge.TryTriggerDangerZoneEncounter("sw.sunken-drain", new Vector2(680f, 640f)), Is.True);
            Assert.That(bridge.IsInDangerEncounter, Is.True);
            Assert.That(bridge.TryAdvanceLandmarkQuest("q.sw.clear-drain-grate", 1, out _), Is.False);
            Assert.That(read(questKey), Is.EqualTo(progressBeforeVictory));
            Assert.That(rewardCalls, Is.Zero);
            Assert.That(bridge.TryFastTravel("sw.entry-shaft", (x, y) => true, out _), Is.False);
            Assert.That(bridge.TryRestAtCamp("sw.moss-cistern", new Vector2(1180f, 1120f), out _), Is.False);

            Assert.That(bridge.TryResolveDangerZoneVictory("sw.sunken-drain", lockBreakAchieved: true,
                out RpgDangerZoneEncounterResult victory), Is.True);
            Assert.That(victory.LockBreakBonus, Is.True);
            Assert.That(victory.CompletedQuestId, Is.EqualTo("q.sw.clear-drain-grate"));
            Assert.That(bridge.IsInDangerEncounter, Is.False);
            Assert.That(bridge.IsDangerZoneCleared("sw.sunken-drain"), Is.True);
            Assert.That(bridge.IsQuestRewarded("q.sw.clear-drain-grate"), Is.True);
            Assert.That(rewardCalls, Is.EqualTo(1));

            // Re-triggering cleared danger zone or reloading must not duplicate rewards.
            Assert.That(bridge.TryTriggerDangerZoneEncounter("sw.sunken-drain", new Vector2(680f, 640f)), Is.False);
            var reloaded = new RpgWorldOrkExplorationBridge(
                RpgWorldMapId.Soundwell, read, write,
                onRewardGranted: (_, __) => rewardCalls++);
            Assert.That(reloaded.IsDangerZoneCleared("sw.sunken-drain"), Is.True);
            Assert.That(reloaded.IsQuestRewarded("q.sw.clear-drain-grate"), Is.True);
            Assert.That(reloaded.Tracker.GetQuestState("q.sw.clear-drain-grate"),
                Is.EqualTo(RpgLandmarkQuestState.Completed));
            Assert.That(reloaded.TryAdvanceLandmarkQuest("q.sw.clear-drain-grate", 1, out _), Is.False);
            Assert.That(rewardCalls, Is.EqualTo(1));
        }
    }
}
