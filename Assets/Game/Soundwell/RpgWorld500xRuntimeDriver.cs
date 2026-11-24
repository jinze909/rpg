using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Drop-in Unity MonoBehaviour scene driver for RpgWorld500xGrandRuntimeFacade.
    /// Bridges Play Mode input, bounded orthographic camera follow, HUD toast/status
    /// readouts, 4-map switching, and deterministic OnDestroy() native-asset cleanup.
    /// </summary>
    public sealed class RpgWorld500xRuntimeDriver : MonoBehaviour
    {
        public const int MaxRecentToasts = 8;

        [SerializeField] private RpgWorldMapId initialMapId = RpgWorldMapId.Soundwell;
        [SerializeField] private int streamingRadiusChunks = RpgExpandedWorldScale.DefaultStreamingRadiusChunks;
        [SerializeField] private float interactRadiusTiles = 6f;
        [SerializeField] private bool autoInitializeOnStart;
        [SerializeField] private Camera followCamera;

        private readonly Dictionary<string, int> _fallbackStore = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<string> _recentToasts = new List<string>();
        private RpgWorld500xGrandRuntimeFacade _facade;

        public RpgWorld500xGrandRuntimeFacade Facade => _facade;
        public bool IsInitialized => _facade != null && !_facade.IsDisposed;
        // A menu router owns the frame loop when present; standalone drivers still poll input.
        public bool AutomaticFrameUpdates { get; set; } = true;
        public IReadOnlyList<string> RecentToasts => _recentToasts;
        public string LastEnvironmentCaptionZh { get; private set; } = string.Empty;
        public string LastNearestWaypointReadoutZh { get; private set; } = string.Empty;

        public Camera FollowCamera
        {
            get => followCamera;
            set => followCamera = value;
        }

        private void Start()
        {
            if (autoInitializeOnStart && !IsInitialized)
            {
                Initialize();
            }
        }

        public void Initialize(
            RpgWorldMapId? overrideMap = null,
            Func<string, int> getInt = null,
            Action<string, int> setInt = null,
            Action<string> onPartyRecover = null,
            Action<string, string> onRewardGranted = null,
            Sprite[] overrideTileSlices = null)
        {
            Shutdown();

            RpgWorldMapId targetMap = overrideMap ?? initialMapId;
            Func<string, int> read = getInt ?? (k => _fallbackStore.TryGetValue(k, out int v) ? v : 0);
            Action<string, int> write = setInt ?? ((k, v) => _fallbackStore[k] = v);

            _facade = new RpgWorld500xGrandRuntimeFacade(
                transform,
                targetMap,
                read,
                write,
                onPartyRecover,
                onRewardGranted,
                RecordToast,
                Math.Max(1, streamingRadiusChunks),
                overrideTileSlices);

            SyncCameraToPlayer();
            RefreshWaypointReadout();
        }

        private void Update()
        {
            if (!AutomaticFrameUpdates || !IsInitialized)
            {
                return;
            }

            Vector2 move = Vector2.zero;
            bool sprint = false;
            bool interact = false;
            bool toggleJournal = false;
            bool toggleOverview = false;

#if ENABLE_LEGACY_INPUT_MANAGER
            move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            interact = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space);
            toggleJournal = Input.GetKeyDown(KeyCode.J);
            toggleOverview = Input.GetKeyDown(KeyCode.M);
#endif

            TickFrame(move, Time.deltaTime, sprint, interact, toggleJournal, toggleOverview);
        }

        public RpgWorld500xEffectiveExplorationModifiers TickFrame(
            Vector2 moveInput,
            float deltaSeconds,
            bool sprinting = false,
            bool interactPressed = false,
            bool toggleJournalPressed = false,
            bool toggleOverviewPressed = false)
        {
            if (!IsInitialized)
            {
                return default;
            }

            if (toggleJournalPressed)
            {
                _facade.Session.ToggleJournal();
            }

            if (toggleOverviewPressed)
            {
                _facade.Session.ToggleOverviewMap();
            }

            if (interactPressed)
            {
                _facade.TryInteractUnified(Mathf.Max(1f, interactRadiusTiles), out _, out _);
            }

            RpgWorld500xEffectiveExplorationModifiers mods = _facade.StepFrame(moveInput, deltaSeconds, sprinting);
            LastEnvironmentCaptionZh = mods.CombinedStatusCaptionZh ?? string.Empty;

            SyncCameraToPlayer();
            RefreshWaypointReadout();
            return mods;
        }

        public bool SwitchMap(RpgWorldMapId targetMapId)
        {
            if (!IsInitialized)
            {
                return false;
            }

            bool ok = _facade.SwitchMap(targetMapId);
            if (ok)
            {
                SyncCameraToPlayer();
                RefreshWaypointReadout();
            }

            return ok;
        }

        internal bool TryRestorePlayerPosition(Vector2 position)
        {
            if (!IsInitialized || !_facade.Session.Host.TryPlaceAtWalkablePosition(position))
                return false;

            SyncAfterExternalPositionChange();
            return true;
        }

        // Position changes from menu actions must refresh immediately even
        // while the modal router suppresses normal world-frame progression.
        internal void SyncAfterExternalPositionChange()
        {
            if (!IsInitialized)
                return;

            _facade.Session.SyncHud();
            SyncCameraToPlayer();
            RefreshWaypointReadout();
        }

        public void Shutdown()
        {
            if (_facade != null)
            {
                _facade.Dispose();
                _facade = null;
            }

            _recentToasts.Clear();
            LastEnvironmentCaptionZh = string.Empty;
            LastNearestWaypointReadoutZh = string.Empty;
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        private void RecordToast(string title, string body)
        {
            string entry = string.IsNullOrEmpty(body) ? (title ?? string.Empty) : (title + "：" + body);
            _recentToasts.Add(entry);
            while (_recentToasts.Count > MaxRecentToasts)
            {
                _recentToasts.RemoveAt(0);
            }
        }

        private void SyncCameraToPlayer()
        {
            if (followCamera == null || !IsInitialized)
            {
                return;
            }

            RpgWorld500xMapLayout layout = _facade.Session.Host.Layout;
            Vector2 pos = _facade.Session.Host.PlayerPosition;
            if (float.IsNaN(pos.x) || float.IsInfinity(pos.x) ||
                float.IsNaN(pos.y) || float.IsInfinity(pos.y))
            {
                return;
            }

            float clampedX = Mathf.Clamp(pos.x, 0f, layout.Width);
            float clampedY = Mathf.Clamp(pos.y, 0f, layout.Height);
            Vector3 camPos = followCamera.transform.position;
            followCamera.transform.position = new Vector3(clampedX, clampedY, camPos.z);
        }

        private void RefreshWaypointReadout()
        {
            if (!IsInitialized)
            {
                LastNearestWaypointReadoutZh = string.Empty;
                return;
            }

            List<RpgWorld500xWaypointGuidance> top = _facade.GetTopNearbyWaypoints(1);
            LastNearestWaypointReadoutZh = top.Count > 0 ? top[0].HudReadoutZh : string.Empty;
        }
    }
}
