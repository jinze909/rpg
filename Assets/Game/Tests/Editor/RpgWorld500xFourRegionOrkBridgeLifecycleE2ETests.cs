using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    /// <summary>
    /// Four-region E2E regression tests for RpgWorldOrkExplorationBridge covering
    /// camp rest recovery, discovered fast-travel gating, danger-linked quest
    /// pre-victory guard, active-encounter lockouts, victory resolution, and
    /// idempotent reload persistence across Soundwell, RedSaltHarbor,
    /// WhitewaterCrossing, and MineCamp.
    /// </summary>
    public sealed class RpgWorld500xFourRegionOrkBridgeLifecycleE2ETests
    {
        [Test]
        public void AllFourRegions_CampRestFastTravelDangerQuestGateAndVictoryPersistIdempotently()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            for (int r = 0; r < specs.Count; r++)
            {
                RpgExpandedMapSpec spec = specs[r];
                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
                Action<string, int> write = (k, v) => store[k] = v;

                int partyRecoverCalls = 0;
                int rewardCalls = 0;
                var bridge = new RpgWorldOrkExplorationBridge(
                    spec.MapId,
                    read,
                    write,
                    onPartyRecover: _ => partyRecoverCalls++,
                    onRewardGranted: (_, __) => rewardCalls++);

                IReadOnlyList<RpgMapLandmarkMarker> markers = RpgWorldLandmarkCatalog.GetMarkers(spec.MapId);
                var probe = new RpgMapExplorationTracker(spec.MapId);
                RpgMapLandmarkMarker camp = default;
                bool hasCamp = false;
                RpgMapLandmarkMarker dangerWithQuest = default;
                bool hasDangerWithQuest = false;
                RpgMapLandmarkMarker fastTravelMarker = default;
                bool hasFastTravelMarker = false;

                for (int i = 0; i < markers.Count; i++)
                {
                    RpgMapLandmarkMarker m = markers[i];
                    if (!hasCamp && m.Category == RpgMapMarkerCategory.CampAndSupply)
                    {
                        camp = m;
                        hasCamp = true;
                    }

                    if (!hasDangerWithQuest &&
                        m.Category == RpgMapMarkerCategory.DangerZone &&
                        !string.IsNullOrEmpty(m.LinkedQuestId) &&
                        RpgWorldLandmarkCatalog.TryGetQuest(spec.MapId, m.LinkedQuestId, out _))
                    {
                        dangerWithQuest = m;
                        hasDangerWithQuest = true;
                    }

                    if (!hasFastTravelMarker)
                    {
                        probe.TryDiscoverMarker(m.Id);
                        if (probe.TryGetFastTravelDestination(m.Id, out _))
                        {
                            fastTravelMarker = m;
                            hasFastTravelMarker = true;
                        }
                    }
                }

                Assert.IsTrue(hasCamp, $"Region {spec.MapId} must have a CampAndSupply marker.");
                Assert.IsTrue(hasDangerWithQuest, $"Region {spec.MapId} must have a DangerZone with LinkedQuestId.");
                Assert.IsTrue(hasFastTravelMarker, $"Region {spec.MapId} must have a fast-travel destination marker.");

                // Fast travel requires prior discovery and walkable destination.
                Assert.IsFalse(bridge.TryFastTravel(fastTravelMarker.Id, (x, y) => true, out _),
                    $"Undiscovered fast travel should fail on {spec.MapId}.");
                var ftPos = new Vector2(fastTravelMarker.Tile.x, fastTravelMarker.Tile.y);
                Assert.GreaterOrEqual(bridge.UpdatePlayerPosition(ftPos), 1);
                Assert.IsFalse(bridge.TryFastTravel(fastTravelMarker.Id, (x, y) => false, out _),
                    $"Non-walkable fast travel destination should fail on {spec.MapId}.");
                Assert.IsTrue(bridge.TryFastTravel(fastTravelMarker.Id, (x, y) => true, out RpgFastTravelResult ft),
                    $"Discovered fast travel should succeed on {spec.MapId}.");
                Assert.AreEqual(fastTravelMarker.Tile, ft.DestinationTile);
                Assert.AreEqual(1, ft.TotalFastTravels);

                // Camp rest requires proximity and CampAndSupply category.
                var farAway = new Vector2(camp.Tile.x + 500f, camp.Tile.y + 500f);
                Assert.IsFalse(bridge.TryRestAtCamp(camp.Id, farAway, out _));
                var campPos = new Vector2(camp.Tile.x, camp.Tile.y);
                Assert.IsTrue(bridge.TryRestAtCamp(camp.Id, campPos, out RpgWorldCampRestResult rest),
                    $"Camp rest failed on {spec.MapId}/{camp.Id}.");
                Assert.IsTrue(rest.PartyRecovered);
                Assert.AreEqual(1, rest.TotalCampRests);
                Assert.AreEqual(1, partyRecoverCalls);

                // Danger-linked quest cannot be advanced before DangerZone victory.
                string questKey = RpgMapExplorationTracker.QuestProgressKey(dangerWithQuest.LinkedQuestId);
                Assert.IsFalse(bridge.TryAdvanceLandmarkQuest(dangerWithQuest.LinkedQuestId, int.MaxValue, out _),
                    $"Direct advance of uncleared danger quest should fail on {spec.MapId}.");
                Assert.AreEqual(0, read(questKey));
                Assert.IsFalse(bridge.IsQuestRewarded(dangerWithQuest.LinkedQuestId));
                Assert.AreEqual(0, rewardCalls);

                // Triggering DangerZone encounter locks fast travel, camp rest, and direct quest advance.
                var dangerPos = new Vector2(dangerWithQuest.Tile.x, dangerWithQuest.Tile.y);
                Assert.IsTrue(bridge.TryTriggerDangerZoneEncounter(dangerWithQuest.Id, dangerPos),
                    $"DangerZone encounter trigger failed on {spec.MapId}/{dangerWithQuest.Id}.");
                Assert.IsTrue(bridge.IsInDangerEncounter);
                Assert.AreEqual(dangerWithQuest.Id, bridge.ActiveEncounterMarkerId);
                Assert.IsFalse(bridge.TryFastTravel(fastTravelMarker.Id, (x, y) => true, out _));
                Assert.IsFalse(bridge.TryRestAtCamp(camp.Id, campPos, out _));
                Assert.IsFalse(bridge.TryAdvanceLandmarkQuest(dangerWithQuest.LinkedQuestId, 1, out _));

                // Resolving victory clears DangerZone, completes linked quest, and grants reward once.
                Assert.IsTrue(bridge.TryResolveDangerZoneVictory(dangerWithQuest.Id, lockBreakAchieved: true,
                    out RpgDangerZoneEncounterResult victory),
                    $"DangerZone victory resolution failed on {spec.MapId}/{dangerWithQuest.Id}.");
                Assert.IsTrue(victory.LockBreakBonus);
                Assert.AreEqual(dangerWithQuest.LinkedQuestId, victory.CompletedQuestId);
                Assert.IsFalse(bridge.IsInDangerEncounter);
                Assert.IsTrue(bridge.IsDangerZoneCleared(dangerWithQuest.Id));
                Assert.IsTrue(bridge.IsQuestRewarded(dangerWithQuest.LinkedQuestId));
                Assert.AreEqual(1, rewardCalls);

                // Re-triggering cleared DangerZone or reloading must not duplicate rewards.
                Assert.IsFalse(bridge.TryTriggerDangerZoneEncounter(dangerWithQuest.Id, dangerPos));
                var reloaded = new RpgWorldOrkExplorationBridge(
                    spec.MapId, read, write,
                    onRewardGranted: (_, __) => rewardCalls++);
                Assert.IsTrue(reloaded.IsDangerZoneCleared(dangerWithQuest.Id));
                Assert.IsTrue(reloaded.IsQuestRewarded(dangerWithQuest.LinkedQuestId));
                Assert.AreEqual(RpgLandmarkQuestState.Completed,
                    reloaded.Tracker.GetQuestState(dangerWithQuest.LinkedQuestId));
                Assert.IsFalse(reloaded.TryAdvanceLandmarkQuest(dangerWithQuest.LinkedQuestId, 1, out _));
                Assert.AreEqual(1, rewardCalls);
            }
        }
    }
}
