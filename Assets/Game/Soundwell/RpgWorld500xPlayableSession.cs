using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum RpgWorldInteractOutcome
    {
        None = 0,
        RestedAtCamp = 1,
        TriggeredDangerEncounter = 2,
        AdvancedLandmarkQuest = 3
    }

    /// <summary>
    /// End-to-end playable 500x RPG world session wiring RpgWorld500xExplorationHost,
    /// RpgWorld500xMinimapHud, and RpgWorld500xQuestJournalPanel together with
    /// one-click journal fast-travel, overview map toggle, unified interact
    /// dispatch, and zero-leak switching across all four 500x maps.
    /// </summary>
    public sealed class RpgWorld500xPlayableSession : IDisposable
    {
        private readonly Transform parent;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private readonly Action<string> onPartyRecover;
        private readonly Action<string, string> onRewardGranted;
        private readonly int streamingRadiusChunks;
        private readonly Sprite[] overrideTileSlices;

        private RpgWorld500xExplorationHost host;
        private RpgWorld500xMinimapHud minimapHud;
        private RpgWorld500xQuestJournalPanel journalPanel;
        private bool disposed;

        public RpgWorldMapId CurrentMapId => host != null ? host.Layout.MapId : RpgWorldMapId.Soundwell;
        public RpgWorld500xExplorationHost Host => host;
        public RpgWorld500xMinimapHud MinimapHud => minimapHud;
        public RpgWorld500xQuestJournalPanel JournalPanel => journalPanel;
        public bool IsDisposed => disposed || host == null || host.IsDisposed;

        public RpgWorld500xPlayableSession(
            Transform parent,
            RpgWorldMapId initialMapId,
            Func<string, int> getInt = null,
            Action<string, int> setInt = null,
            Action<string> onPartyRecover = null,
            Action<string, string> onRewardGranted = null,
            int streamingRadiusChunks = RpgExpandedWorldScale.DefaultStreamingRadiusChunks,
            Sprite[] overrideTileSlices = null)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.getInt = getInt;
            this.setInt = setInt;
            this.onPartyRecover = onPartyRecover;
            this.onRewardGranted = onRewardGranted;
            this.streamingRadiusChunks = streamingRadiusChunks;
            this.overrideTileSlices = overrideTileSlices;

            MountMap(initialMapId);
        }

        public bool SwitchMap(RpgWorldMapId targetMapId)
        {
            // Reject unsupported IDs before tearing down the active map.
            // Callers may pass serialized/stale IDs that are not valid enum members.
            if (IsDisposed || parent == null ||
                !Enum.IsDefined(typeof(RpgWorldMapId), targetMapId))
                return false;

            // Re-selecting the current map must preserve the player, streamed
            // chunks, journal state and active ORK bridge instead of resetting them.
            if (targetMapId == CurrentMapId)
                return true;

            TearDownActiveMap();
            MountMap(targetMapId);
            return true;
        }

        public bool StepMovement(Vector2 input, float deltaSeconds, bool sprinting = false)
        {
            if (IsDisposed || (journalPanel != null && journalPanel.IsVisible))
                return false;

            bool moved = host.StepMovement(input, deltaSeconds, sprinting);
            SyncHud();
            return moved;
        }

        public bool ToggleOverviewMap()
        {
            if (IsDisposed || minimapHud == null || minimapHud.IsDisposed)
                return false;
            return minimapHud.SetOverviewMode(!minimapHud.IsOverviewMode);
        }

        public bool ToggleJournal(RpgQuestJournalTab? tab = null)
        {
            if (IsDisposed || journalPanel == null || journalPanel.IsDisposed)
                return false;
            if (journalPanel.IsVisible && !tab.HasValue)
                return journalPanel.Hide();
            return journalPanel.Show(tab);
        }

        public RpgWorldInteractOutcome InteractNearby()
        {
            if (IsDisposed || (journalPanel != null && journalPanel.IsVisible))
                return RpgWorldInteractOutcome.None;

            if (host.TryRestAtNearbyCamp(out _))
            {
                SyncHud();
                return RpgWorldInteractOutcome.RestedAtCamp;
            }

            if (host.TryTriggerNearbyDangerEncounter(out _))
            {
                SyncHud();
                return RpgWorldInteractOutcome.TriggeredDangerEncounter;
            }

            if (host.TryInteractNearbyLandmarkQuest(1, out _))
            {
                SyncHud();
                return RpgWorldInteractOutcome.AdvancedLandmarkQuest;
            }

            return RpgWorldInteractOutcome.None;
        }

        public bool ResolveActiveDangerVictory(bool lockBreakAchieved,
            out RpgDangerZoneEncounterResult result)
        {
            result = default;
            if (IsDisposed)
                return false;
            bool ok = host.TryResolveActiveDangerVictory(lockBreakAchieved, out result);
            if (ok)
                SyncHud();
            return ok;
        }

        public void SyncHud()
        {
            if (IsDisposed)
                return;
            string district = host.CurrentDistrict.HasValue
                ? host.CurrentDistrict.Value.DisplayNameZh
                : host.Layout.Spec.DisplayNameZh;
            minimapHud?.UpdatePlayerAndCompass(host.PlayerPosition, district);
            if (journalPanel != null && journalPanel.IsVisible)
                journalPanel.Refresh();
        }

        private void MountMap(RpgWorldMapId mapId)
        {
            try
            {
                host = new RpgWorld500xExplorationHost(
                    parent, mapId, getInt, setInt,
                    onPartyRecover, onRewardGranted,
                    streamingRadiusChunks, overrideTileSlices);

                minimapHud = new RpgWorld500xMinimapHud(
                    parent,
                    host.Layout.Spec,
                    host.Layout.GetTileId,
                    host.Layout.UsesHarborPalette,
                    host.Tracker);

                journalPanel = new RpgWorld500xQuestJournalPanel(
                    parent,
                    host.Tracker,
                    onFastTravelRequested: HandleJournalFastTravel,
                    onTrackedQuestChanged: _ => SyncHud());

                SyncHud();
            }
            catch
            {
                TearDownActiveMap();
                throw;
            }
        }

        private void HandleJournalFastTravel(string markerId, Vector2Int _)
        {
            if (IsDisposed)
                return;
            if (host.TryFastTravelToBeacon(markerId, out RpgFastTravelResult travelResult))
            {
                journalPanel?.Hide();
                SyncHud();
            }
        }

        private void TearDownActiveMap()
        {
            journalPanel?.Dispose();
            journalPanel = null;
            minimapHud?.Dispose();
            minimapHud = null;
            host?.Dispose();
            host = null;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            TearDownActiveMap();
        }
    }
}
