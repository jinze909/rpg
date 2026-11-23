using System;
using System.Collections.Generic;
using GamingIsLove.Makinom;
using UnityEngine;

namespace Rpg.Soundwell
{
    public readonly struct RpgWorldCampRestResult
    {
        public RpgWorldCampRestResult(string markerId, string campNameZh,
            bool partyRecovered, int totalCampRests)
        {
            MarkerId = markerId;
            CampNameZh = campNameZh;
            PartyRecovered = partyRecovered;
            TotalCampRests = totalCampRests;
        }

        public string MarkerId { get; }
        public string CampNameZh { get; }
        public bool PartyRecovered { get; }
        public int TotalCampRests { get; }
    }

    public readonly struct RpgDangerZoneEncounterResult
    {
        public RpgDangerZoneEncounterResult(string markerId, string zoneNameZh,
            bool lockBreakBonus, string completedQuestId)
        {
            MarkerId = markerId;
            ZoneNameZh = zoneNameZh;
            LockBreakBonus = lockBreakBonus;
            CompletedQuestId = completedQuestId ?? string.Empty;
        }

        public string MarkerId { get; }
        public string ZoneNameZh { get; }
        public bool LockBreakBonus { get; }
        public string CompletedQuestId { get; }
    }

    public readonly struct RpgFastTravelResult
    {
        public RpgFastTravelResult(string markerId, string destinationNameZh,
            Vector2Int destinationTile, int totalFastTravels)
        {
            MarkerId = markerId;
            DestinationNameZh = destinationNameZh;
            DestinationTile = destinationTile;
            TotalFastTravels = totalFastTravels;
        }

        public string MarkerId { get; }
        public string DestinationNameZh { get; }
        public Vector2Int DestinationTile { get; }
        public Vector2 DestinationWorldCenter =>
            new Vector2(DestinationTile.x + 0.5f, DestinationTile.y + 0.5f);
        public int TotalFastTravels { get; }
    }

    public readonly struct RpgLandmarkQuestRewardResult
    {
        public RpgLandmarkQuestRewardResult(string questId, string titleZh,
            RpgLandmarkQuestState state, bool rewardGrantedNow, string rewardSummaryZh)
        {
            QuestId = questId;
            TitleZh = titleZh;
            State = state;
            RewardGrantedNow = rewardGrantedNow;
            RewardSummaryZh = rewardSummaryZh ?? string.Empty;
        }

        public string QuestId { get; }
        public string TitleZh { get; }
        public RpgLandmarkQuestState State { get; }
        public bool RewardGrantedNow { get; }
        public string RewardSummaryZh { get; }
    }

    /// <summary>
    /// Integrates 500x-scale world exploration (RpgMapExplorationTracker) with
    /// ORK Framework party recovery, DangerZone combat encounters, idempotent
    /// landmark quest rewards, discovered fast-travel beacons, and Makinom
    /// variable persistence.
    /// </summary>
    public sealed class RpgWorldOrkExplorationBridge
    {
        public const string CampRestsKey = "rpg.return.world.camp-rests";
        public const string FastTravelsKey = "rpg.return.world.fast-travels";
        public const float SprintSpeedMultiplier = 1.65f;
        public const float DangerSprintSpeedMultiplier = 1.25f;

        private readonly RpgMapExplorationTracker tracker;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private readonly Action<string> onPartyRecover;
        private readonly Action<string, string> onRewardGranted;
        private readonly HashSet<string> clearedDangerZones = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> rewardedQuests = new HashSet<string>(StringComparer.Ordinal);
        private string activeEncounterMarkerId = string.Empty;

        public RpgMapExplorationTracker Tracker => tracker;
        public string ActiveEncounterMarkerId => activeEncounterMarkerId;
        public bool IsInDangerEncounter => !string.IsNullOrEmpty(activeEncounterMarkerId);
        public int ClearedDangerZoneCount => clearedDangerZones.Count;
        public int RewardedQuestCount => rewardedQuests.Count;

        public RpgWorldOrkExplorationBridge(
            RpgWorldMapId mapId,
            Func<string, int> getInt = null,
            Action<string, int> setInt = null,
            Action<string> onPartyRecover = null,
            Action<string, string> onRewardGranted = null)
        {
            tracker = new RpgMapExplorationTracker(mapId);
            this.getInt = getInt ?? DefaultGetInt;
            this.setInt = setInt ?? DefaultSetInt;
            this.onPartyRecover = onPartyRecover;
            this.onRewardGranted = onRewardGranted;
            LoadState();
        }

        public static string DangerClearedKey(string markerId)
        {
            return "rpg.return.world.danger-cleared." + markerId;
        }

        public static string QuestRewardedKey(string questId)
        {
            return "rpg.return.world.quest-rewarded." + questId;
        }

        public static float ComputeEffectiveMoveSpeed(float baseSpeed, bool sprinting, bool inDangerZone)
        {
            if (float.IsNaN(baseSpeed) || float.IsInfinity(baseSpeed) || baseSpeed <= 0f)
                return 0f;
            if (!sprinting)
                return baseSpeed;
            return baseSpeed * (inDangerZone ? DangerSprintSpeedMultiplier : SprintSpeedMultiplier);
        }

        public bool IsDangerZoneCleared(string markerId)
        {
            return !string.IsNullOrEmpty(markerId) && clearedDangerZones.Contains(markerId);
        }

        public bool IsQuestRewarded(string questId)
        {
            return !string.IsNullOrEmpty(questId) && rewardedQuests.Contains(questId);
        }

        public int UpdatePlayerPosition(Vector2 playerTile)
        {
            int discovered = tracker.DiscoverMarkersInRange(playerTile);
            if (discovered > 0)
                tracker.TrySave(setInt);
            return discovered;
        }

        public bool TryRestAtCamp(string markerId, Vector2 playerTile, out RpgWorldCampRestResult result)
        {
            result = default;
            if (IsInDangerEncounter || !IsFinite(playerTile) ||
                !RpgWorldLandmarkCatalog.TryGetMarker(tracker.MapId, markerId, out RpgMapLandmarkMarker marker) ||
                marker.Category != RpgMapMarkerCategory.CampAndSupply)
                return false;

            float dx = playerTile.x - marker.Tile.x;
            float dy = playerTile.y - marker.Tile.y;
            float interactRadius = Mathf.Max(4f, marker.DiscoveryRadiusTiles);
            if (dx * dx + dy * dy > interactRadius * interactRadius)
                return false;

            tracker.TryDiscoverMarker(marker.Id);
            tracker.TrySave(setInt);

            onPartyRecover?.Invoke(marker.Id);
            int totalRests = Mathf.Max(0, getInt(CampRestsKey)) + 1;
            setInt(CampRestsKey, totalRests);

            result = new RpgWorldCampRestResult(marker.Id, marker.TitleZh, partyRecovered: true, totalRests);
            return true;
        }

        public bool TryTriggerDangerZoneEncounter(string markerId, Vector2 playerTile)
        {
            if (IsInDangerEncounter || !IsFinite(playerTile) ||
                !RpgWorldLandmarkCatalog.TryGetMarker(tracker.MapId, markerId, out RpgMapLandmarkMarker marker) ||
                marker.Category != RpgMapMarkerCategory.DangerZone ||
                clearedDangerZones.Contains(marker.Id))
                return false;

            float dx = playerTile.x - marker.Tile.x;
            float dy = playerTile.y - marker.Tile.y;
            float radius = Mathf.Max(4f, marker.DiscoveryRadiusTiles);
            if (dx * dx + dy * dy > radius * radius)
                return false;

            tracker.TryDiscoverMarker(marker.Id);
            tracker.TrySave(setInt);
            activeEncounterMarkerId = marker.Id;
            return true;
        }

        public bool TryResolveDangerZoneVictory(string markerId, bool lockBreakAchieved,
            out RpgDangerZoneEncounterResult result)
        {
            result = default;
            if (string.IsNullOrEmpty(markerId) ||
                !string.Equals(activeEncounterMarkerId, markerId, StringComparison.Ordinal) ||
                !RpgWorldLandmarkCatalog.TryGetMarker(tracker.MapId, markerId, out RpgMapLandmarkMarker marker))
                return false;

            activeEncounterMarkerId = string.Empty;
            clearedDangerZones.Add(marker.Id);
            setInt(DangerClearedKey(marker.Id), 1);

            string completedQuestId = string.Empty;
            if (!string.IsNullOrEmpty(marker.LinkedQuestId) &&
                RpgWorldLandmarkCatalog.TryGetQuest(tracker.MapId, marker.LinkedQuestId, out RpgLandmarkQuestDefinition quest))
            {
                if (TryAdvanceLandmarkQuest(quest.QuestId, quest.RequiredSteps, out RpgLandmarkQuestRewardResult qr) &&
                    qr.State == RpgLandmarkQuestState.Completed)
                {
                    completedQuestId = quest.QuestId;
                }
            }

            result = new RpgDangerZoneEncounterResult(marker.Id, marker.TitleZh,
                lockBreakAchieved, completedQuestId);
            return true;
        }

        public bool TryFastTravel(string markerId, Func<int, int, bool> isWalkable,
            out RpgFastTravelResult result)
        {
            result = default;
            if (IsInDangerEncounter || isWalkable == null ||
                !tracker.TryGetFastTravelDestination(markerId, out Vector2Int dest) ||
                !isWalkable(dest.x, dest.y) ||
                !RpgWorldLandmarkCatalog.TryGetMarker(tracker.MapId, markerId, out RpgMapLandmarkMarker marker))
                return false;

            int count = Mathf.Max(0, getInt(FastTravelsKey)) + 1;
            setInt(FastTravelsKey, count);
            result = new RpgFastTravelResult(marker.Id, marker.TitleZh, dest, count);
            return true;
        }

        public bool TryAdvanceLandmarkQuest(string questId, int deltaSteps,
            out RpgLandmarkQuestRewardResult result)
        {
            result = default;
            if (deltaSteps <= 0 ||
                !RpgWorldLandmarkCatalog.TryGetQuest(tracker.MapId, questId, out RpgLandmarkQuestDefinition quest))
                return false;

            if (!tracker.TryAdvanceQuest(questId, deltaSteps))
                return false;

            tracker.TrySave(setInt);
            RpgLandmarkQuestState state = tracker.GetQuestState(questId);
            bool grantedNow = false;
            if (state == RpgLandmarkQuestState.Completed && rewardedQuests.Add(questId))
            {
                setInt(QuestRewardedKey(questId), 1);
                onRewardGranted?.Invoke(questId, quest.RewardSummaryZh);
                grantedNow = true;
            }

            result = new RpgLandmarkQuestRewardResult(quest.QuestId, quest.TitleZh,
                state, grantedNow, quest.RewardSummaryZh);
            return true;
        }

        public void LoadState()
        {
            tracker.TryLoad(getInt);
            clearedDangerZones.Clear();
            rewardedQuests.Clear();

            IReadOnlyList<RpgMapLandmarkMarker> markers = RpgWorldLandmarkCatalog.GetMarkers(tracker.MapId);
            for (int i = 0; i < markers.Count; i++)
            {
                if (markers[i].Category == RpgMapMarkerCategory.DangerZone &&
                    getInt(DangerClearedKey(markers[i].Id)) == 1)
                    clearedDangerZones.Add(markers[i].Id);
            }

            IReadOnlyList<RpgLandmarkQuestDefinition> quests = RpgWorldLandmarkCatalog.GetQuests(tracker.MapId);
            for (int i = 0; i < quests.Count; i++)
            {
                if (getInt(QuestRewardedKey(quests[i].QuestId)) == 1)
                    rewardedQuests.Add(quests[i].QuestId);
            }
        }

        private static bool IsFinite(Vector2 v)
        {
            return !float.IsNaN(v.x) && !float.IsInfinity(v.x) &&
                !float.IsNaN(v.y) && !float.IsInfinity(v.y);
        }

        private static int DefaultGetInt(string key)
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null ? variables.GetInt(key) : 0;
        }

        private static void DefaultSetInt(string key, int value)
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables != null)
                variables.Set(key, value);
        }
    }
}
