using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Grand end-to-end runtime facade unifying the entire 500x-area RPG world stack:
    /// RpgWorld500xPlayableSession (streaming host, minimap, journal, fast-travel),
    /// RpgWorld500xWorldActivityDirector (caches, shortcuts, field NPCs, story spots,
    /// ambient micro-events, 24h weather), RpgWorld500xPropVisualSpawner (chunk-culled
    /// 16-PPU sprites), RpgWorld500xDistrictProgressTracker (15 districts & fog-of-war),
    /// RpgWorld500xFieldBountyAndSupplyService, RpgWorld500xThreatCodexService, and
    /// RpgWorld500xWaypointNavigator.
    /// </summary>
    public sealed class RpgWorld500xGrandRuntimeFacade : IDisposable
    {
        private readonly Transform _parent;
        private readonly Func<string, int> _getInt;
        private readonly Action<string, int> _setInt;
        private readonly Action<string, string> _onToast;
        private readonly int _streamingRadiusChunks;

        private readonly RpgWorld500xPlayableSession _session;
        private readonly RpgWorld500xWeatherAndTimeOfDayService _atmosphere;
        private readonly RpgWorld500xFieldBountyAndSupplyService _bountyAndSupply;
        private readonly RpgWorld500xThreatCodexService _threatCodex;

        private RpgWorld500xWorldActivityDirector _activityDirector;
        private RpgWorld500xPropVisualSpawner _propSpawner;
        private RpgWorld500xDistrictProgressTracker _districtTracker;
        private float _totalElapsedSeconds;
        private bool _disposed;

        public RpgWorldMapId CurrentMapId => _session.CurrentMapId;
        public RpgWorld500xPlayableSession Session => _session;
        public RpgWorld500xWorldActivityDirector ActivityDirector => _activityDirector;
        public RpgWorld500xPropVisualSpawner PropSpawner => _propSpawner;
        public RpgWorld500xDistrictProgressTracker DistrictTracker => _districtTracker;
        public RpgWorld500xWeatherAndTimeOfDayService Atmosphere => _atmosphere;
        public RpgWorld500xFieldBountyAndSupplyService BountyAndSupply => _bountyAndSupply;
        public RpgWorld500xThreatCodexService ThreatCodex => _threatCodex;
        public bool IsDisposed => _disposed || _session.IsDisposed;

        public RpgWorld500xGrandRuntimeFacade(
            Transform parent,
            RpgWorldMapId initialMapId,
            Func<string, int> getInt = null,
            Action<string, int> setInt = null,
            Action<string> onPartyRecover = null,
            Action<string, string> onRewardGranted = null,
            Action<string, string> onToast = null,
            int streamingRadiusChunks = RpgExpandedWorldScale.DefaultStreamingRadiusChunks,
            Sprite[] overrideTileSlices = null)
        {
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            _getInt = getInt ?? (_ => 0);
            _setInt = setInt ?? ((_, __) => { });
            _onToast = onToast;
            _streamingRadiusChunks = Math.Max(1, streamingRadiusChunks);

            _atmosphere = new RpgWorld500xWeatherAndTimeOfDayService();
            _bountyAndSupply = new RpgWorld500xFieldBountyAndSupplyService(_getInt, _setInt);
            _threatCodex = new RpgWorld500xThreatCodexService(
                _getInt,
                _setInt,
                dossier =>
                {
                    _bountyAndSupply.AddSaltSilver(dossier.Profile.CodexStudySilverBonus);
                    _onToast?.Invoke("威胁图鉴收录：" + dossier.Profile.EnemyNameZh, dossier.Profile.WeaknessHintZh);
                });

            _session = new RpgWorld500xPlayableSession(
                parent,
                initialMapId,
                _getInt,
                _setInt,
                onPartyRecover,
                onRewardGranted,
                _streamingRadiusChunks,
                overrideTileSlices);

            MountPerMapSubsystems();
        }

        public RpgWorld500xEffectiveExplorationModifiers StepFrame(
            Vector2 moveInput,
            float deltaSeconds,
            bool sprinting = false)
        {
            if (IsDisposed)
            {
                return default;
            }

            if (deltaSeconds > 0f && !float.IsNaN(deltaSeconds) && !float.IsInfinity(deltaSeconds))
            {
                _totalElapsedSeconds += deltaSeconds;
            }

            _session.StepMovement(moveInput, deltaSeconds, sprinting);
            Vector2 playerPos = _session.Host.PlayerPosition;

            _propSpawner.UpdateFocus(playerPos, _streamingRadiusChunks);
            if (_districtTracker.RecordPlayerTile(playerPos, _streamingRadiusChunks, out RpgWorld500xDistrictDef entered))
            {
                _onToast?.Invoke("进入片区", entered.DisplayNameZh);
            }

            RpgWorld500xEffectiveExplorationModifiers mods =
                _activityDirector.StepEnvironment(playerPos, deltaSeconds, _totalElapsedSeconds);

            ClaimProgressRewards();
            return mods;
        }

        public bool TryInteractUnified(
            float interactRadius,
            out RpgWorld500xInteractionResult activityResult,
            out RpgWorldInteractOutcome sessionOutcome)
        {
            activityResult = RpgWorld500xInteractionResult.None;
            sessionOutcome = RpgWorldInteractOutcome.None;

            if (IsDisposed)
            {
                return false;
            }

            Vector2 playerPos = _session.Host.PlayerPosition;
            if (_activityDirector.TryInteractPriority(playerPos, interactRadius, out activityResult))
            {
                if (activityResult.SaltSilverGranted > 0)
                {
                    _bountyAndSupply.AddSaltSilver(activityResult.SaltSilverGranted);
                }

                _propSpawner.RefreshActiveSprites();
                _session.SyncHud();
                ClaimProgressRewards();
                return true;
            }

            sessionOutcome = _session.InteractNearby();
            if (sessionOutcome == RpgWorldInteractOutcome.RestedAtCamp)
            {
                _activityDirector.RestAtCampfire();
            }

            if (sessionOutcome != RpgWorldInteractOutcome.None)
            {
                ClaimProgressRewards();
                return true;
            }

            return false;
        }

        public bool SwitchMap(RpgWorldMapId targetMapId)
        {
            if (IsDisposed)
            {
                return false;
            }

            TearDownPerMapSubsystems();
            bool switched = _session.SwitchMap(targetMapId);
            if (switched)
            {
                MountPerMapSubsystems();
            }

            return switched;
        }

        public RpgWorld500xMapCompletionSummary GetCompletionSummary()
        {
            return _districtTracker.ComputeCompletionSummary(
                _session.Host.Layout,
                _session.Host.Tracker,
                _activityDirector.CachesAndShortcuts);
        }

        public List<RpgWorld500xWaypointGuidance> GetTopNearbyWaypoints(int maxResults = 5)
        {
            if (IsDisposed)
            {
                return new List<RpgWorld500xWaypointGuidance>();
            }

            return RpgWorld500xWaypointNavigator.FindTopNearbyPointsOfInterest(
                _session.Host.Layout,
                _session.Host.PlayerPosition,
                maxResults,
                _session.Host.Tracker,
                _activityDirector.CachesAndShortcuts,
                _getInt);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            TearDownPerMapSubsystems();
            _session.Dispose();
        }

        private void MountPerMapSubsystems()
        {
            RpgWorld500xMapLayout layout = _session.Host.Layout;
            RpgMapExplorationTracker tracker = _session.Host.Tracker;

            _activityDirector = new RpgWorld500xWorldActivityDirector(
                layout,
                tracker,
                _getInt,
                _setInt,
                _atmosphere,
                _onToast);

            _propSpawner = new RpgWorld500xPropVisualSpawner(
                layout,
                _parent,
                _activityDirector.CachesAndShortcuts);

            _districtTracker = new RpgWorld500xDistrictProgressTracker(
                layout.Spec,
                _getInt,
                _setInt,
                milestone =>
                {
                    _bountyAndSupply.AddSaltSilver(milestone.SaltSilverReward);
                    _onToast?.Invoke(milestone.TitleZh, "获得盐银 +" + milestone.SaltSilverReward);
                });

            Vector2 playerPos = _session.Host.PlayerPosition;
            _propSpawner.UpdateFocus(playerPos, _streamingRadiusChunks);
            _districtTracker.RecordPlayerTile(playerPos, _streamingRadiusChunks, out _);
        }

        private void ClaimProgressRewards()
        {
            RpgWorld500xMapCompletionSummary summary = GetCompletionSummary();
            _districtTracker.ClaimEligibleMilestones(summary.OverallCompletionPercent);
            _bountyAndSupply.ClaimCompletedBounties(summary);
        }

        private void TearDownPerMapSubsystems()
        {
            _propSpawner?.Dispose();
            _propSpawner = null;
            _activityDirector = null;
            _districtTracker = null;
        }
    }
}
