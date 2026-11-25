using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Rpg.Soundwell
{
    public struct RpgWorld500xInputFrame
    {
        public Vector2 Move;
        public bool Sprint;
        public bool InteractPressed;
        public bool ToggleOverviewMapPressed;
        public bool QuickAutoSavePressed;
        public bool CloseModalPressed;
        public bool ReconPulsePressed;
        public RpgWorld500xModalKind RequestedModalToggle;
    }

    /// <summary>
    /// Single-component Play Mode controller uniting RpgWorld500xSceneBootstrapper,
    /// RpgWorld500xCampHavenService, RpgWorld500xSaveSlotManager, the 9-panel
    /// RpgWorld500xMenuRouter, RpgWorld500xReconPulseHudBar, and
    /// RpgWorld500xWeatherAdvisoryHudBadge. Drives movement, interaction, overview
    /// map toggle, field recon pulse, quick auto-save, DangerZone victory reward
    /// settlement, live weather advisory sync, and mutually-exclusive modal hotkeys
    /// through deterministic RpgWorld500xInputFrame frames.
    /// </summary>
    public sealed class RpgWorld500xPlayModeController : MonoBehaviour
    {
        [SerializeField] private RpgWorldMapId initialMapId = RpgWorldMapId.Soundwell;
        [SerializeField] private bool autoInitializeOnStart = true;

        private GameObject _uiRoot;
        private RpgWorld500xSceneBootstrapper _bootstrapper;
        private RpgWorld500xCampHavenService _campHaven;
        private RpgWorld500xSaveSlotManager _saveSlotManager;
        private RpgWorld500xMenuRouter _menuRouter;
        private RpgWorld500xReconPulseHudBar _reconPulseHud;
        private RpgWorld500xWeatherAdvisoryHudBadge _weatherAdvisoryHud;
        private float _elapsedPlaySeconds;
        private bool _initialized;
        private string _lastAutoSaveStatusZh = string.Empty;
        private string _lastDangerVictoryBannerZh = string.Empty;

        public bool IsInitialized => _initialized && _bootstrapper != null && !_bootstrapper.IsDisposed;
        public RpgWorld500xSceneBootstrapper Bootstrapper => _bootstrapper;
        public RpgWorld500xCampHavenService CampHaven => _campHaven;
        public RpgWorld500xSaveSlotManager SaveSlotManager => _saveSlotManager;
        public RpgWorld500xMenuRouter MenuRouter => _menuRouter;
        public RpgWorld500xReconPulseHudBar ReconPulseHud => _reconPulseHud;
        public RpgWorld500xWeatherAdvisoryHudBadge WeatherAdvisoryHud => _weatherAdvisoryHud;
        public string LastAutoSaveStatusZh => _lastAutoSaveStatusZh;
        public string LastDangerVictoryBannerZh => _lastDangerVictoryBannerZh;

        private void Start()
        {
            if (autoInitializeOnStart && !_initialized)
            {
                Initialize(initialMapId);
            }
        }

        private void Update()
        {
            if (!Application.isPlaying || !IsInitialized)
            {
                return;
            }

            RpgWorld500xInputFrame frame = PollHardwareInput();
            ProcessInputFrame(frame, Time.deltaTime);
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        public void Initialize(
            RpgWorldMapId? overrideMap = null,
            Camera followCamera = null,
            IRpgWorld500xSaveSlotStorage saveStorage = null,
            Func<DateTime> utcNowProvider = null,
            Sprite[] overrideTileSlices = null)
        {
            Shutdown();

            RpgWorldMapId targetMap = overrideMap ?? initialMapId;
            _uiRoot = new GameObject("World500xPlayModeUiRoot");
            _uiRoot.transform.SetParent(transform, false);

            _bootstrapper = new RpgWorld500xSceneBootstrapper(
                hostObject: null,
                initialMapId: targetMap,
                followCamera: followCamera,
                overrideTileSlices: overrideTileSlices);

            _campHaven = _bootstrapper.CreateCampHavenService();

            IRpgWorld500xSaveSlotStorage resolvedStorage = saveStorage ??
                (Application.isPlaying
                    ? (IRpgWorld500xSaveSlotStorage)new RpgWorld500xPlayerPrefsSaveSlotStorage()
                    : new RpgWorld500xInMemorySaveSlotStorage());

            _saveSlotManager = new RpgWorld500xSaveSlotManager(resolvedStorage, utcNowProvider);
            _menuRouter = new RpgWorld500xMenuRouter(_uiRoot.transform, _bootstrapper, _campHaven, _saveSlotManager);
            _reconPulseHud = new RpgWorld500xReconPulseHudBar(_uiRoot.transform, _bootstrapper);
            _weatherAdvisoryHud = new RpgWorld500xWeatherAdvisoryHudBadge(_uiRoot.transform, _bootstrapper);
            _elapsedPlaySeconds = 0f;

            // The standalone world owns only its own input setup; never add a
            // second EventSystem when a containing scene already provides one.
            foreach (Canvas canvas in _uiRoot.GetComponentsInChildren<Canvas>(true))
            {
                if (canvas.GetComponent<GraphicRaycaster>() == null)
                    canvas.gameObject.AddComponent<GraphicRaycaster>();
            }
            if (EventSystem.current == null &&
                UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            {
                _uiRoot.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
                _uiRoot.AddComponent<InputSystemUIInputModule>();
#elif ENABLE_LEGACY_INPUT_MANAGER
                _uiRoot.AddComponent<StandaloneInputModule>();
#endif
            }

            _lastAutoSaveStatusZh = string.Empty;
            _lastDangerVictoryBannerZh = string.Empty;
            _initialized = true;
        }

        public bool TryResolveActiveDangerVictory(
            bool lockBreakAchieved,
            out RpgDangerZoneEncounterResult encounterResult,
            out RpgWorld500xDangerVictoryReward reward)
        {
            encounterResult = default;
            reward = default;
            if (!IsInitialized)
            {
                return false;
            }

            RpgWorld500xGrandRuntimeFacade facade = _bootstrapper.Driver.Facade;
            if (!facade.Session.ResolveActiveDangerVictory(lockBreakAchieved, out encounterResult))
            {
                return false;
            }

            reward = RpgWorld500xDangerVictoryRewardService.GrantVictoryReward(
                _bootstrapper,
                _campHaven,
                facade.CurrentMapId,
                encounterResult.MarkerId,
                lockBreakAchieved);

            _lastDangerVictoryBannerZh = reward.SummaryBannerZh;
            return true;
        }

        public RpgWorld500xEffectiveExplorationModifiers ProcessInputFrame(
            RpgWorld500xInputFrame frame,
            float deltaSeconds)
        {
            if (!IsInitialized)
            {
                return default;
            }

            if (deltaSeconds > 0f && !float.IsNaN(deltaSeconds) && !float.IsInfinity(deltaSeconds))
            {
                _elapsedPlaySeconds += deltaSeconds;
            }

            // Do not let world movement or interaction leak through in the same
            // input frame that dismisses a modal (Escape or its toggle hotkey).
            bool wasModalOpen = _menuRouter.HasAnyModalOpen;
            if (frame.CloseModalPressed && wasModalOpen)
            {
                _menuRouter.CloseAll();
            }
            else if (frame.RequestedModalToggle != RpgWorld500xModalKind.None)
            {
                _menuRouter.ToggleModal(frame.RequestedModalToggle);
            }

            if (frame.QuickAutoSavePressed)
            {
                if (_saveSlotManager.TryAutoSave(_bootstrapper, out RpgWorld500xSaveSlotSummary sum, out string err, "快速自动存档"))
                {
                    _lastAutoSaveStatusZh = "自动存档成功：" + sum.MapDisplayNameZh + "（盐银 " + sum.SaltSilverBalance + "）";
                }
                else
                {
                    _lastAutoSaveStatusZh = "自动存档失败：" + err;
                }
            }

            if (frame.ToggleOverviewMapPressed && !wasModalOpen && !_menuRouter.HasAnyModalOpen)
            {
                _bootstrapper.Driver.Facade.Session.ToggleOverviewMap();
            }

            _reconPulseHud?.UpdateCooldownDisplay(_elapsedPlaySeconds);
            if (frame.ReconPulsePressed && !wasModalOpen && !_menuRouter.HasAnyModalOpen)
            {
                _reconPulseHud?.TryClickPulseScan(_elapsedPlaySeconds);
            }

            RpgWorld500xEffectiveExplorationModifiers mods = _menuRouter.StepFrameWithModalGuard(
                wasModalOpen ? Vector2.zero : frame.Move,
                wasModalOpen ? 0f : deltaSeconds,
                !wasModalOpen && frame.Sprint,
                !wasModalOpen && frame.InteractPressed);

            _weatherAdvisoryHud?.Refresh(_elapsedPlaySeconds);
            return mods;
        }

        public void Shutdown()
        {
            _initialized = false;
            _weatherAdvisoryHud?.Dispose();
            _weatherAdvisoryHud = null;
            _reconPulseHud?.Dispose();
            _reconPulseHud = null;
            _menuRouter?.Dispose();
            _menuRouter = null;
            _bootstrapper?.Dispose();
            _bootstrapper = null;
            _campHaven = null;
            _saveSlotManager = null;

            if (_uiRoot != null)
            {
                // Disable owned input immediately; Destroy is deferred in Play Mode.
                _uiRoot.SetActive(false);
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(_uiRoot);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(_uiRoot);
                }

                _uiRoot = null;
            }
        }

        private static RpgWorld500xInputFrame PollHardwareInput()
        {
            var frame = new RpgWorld500xInputFrame();
#if ENABLE_LEGACY_INPUT_MANAGER
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            frame.Move = new Vector2(h, v);
            frame.Sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            frame.InteractPressed = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space);
            frame.ToggleOverviewMapPressed = Input.GetKeyDown(KeyCode.Tab);
            frame.QuickAutoSavePressed = Input.GetKeyDown(KeyCode.F5);
            frame.CloseModalPressed = Input.GetKeyDown(KeyCode.Escape);
            frame.ReconPulsePressed = Input.GetKeyDown(KeyCode.R);

            if (Input.GetKeyDown(KeyCode.J)) frame.RequestedModalToggle = RpgWorld500xModalKind.QuestJournal;
            else if (Input.GetKeyDown(KeyCode.M)) frame.RequestedModalToggle = RpgWorld500xModalKind.WorldAtlas;
            else if (Input.GetKeyDown(KeyCode.B)) frame.RequestedModalToggle = RpgWorld500xModalKind.CampMerchant;
            else if (Input.GetKeyDown(KeyCode.K)) frame.RequestedModalToggle = RpgWorld500xModalKind.Bestiary;
            else if (Input.GetKeyDown(KeyCode.T)) frame.RequestedModalToggle = RpgWorld500xModalKind.FastTravelBeacons;
            else if (Input.GetKeyDown(KeyCode.N)) frame.RequestedModalToggle = RpgWorld500xModalKind.CartographyReputation;
            else if (Input.GetKeyDown(KeyCode.O)) frame.RequestedModalToggle = RpgWorld500xModalKind.SaveSlots;
            else if (Input.GetKeyDown(KeyCode.L)) frame.RequestedModalToggle = RpgWorld500xModalKind.FieldRumorAndLore;
            else if (Input.GetKeyDown(KeyCode.U)) frame.RequestedModalToggle = RpgWorld500xModalKind.ExpeditionMilestones;
#endif
            return frame;
        }
    }
}
