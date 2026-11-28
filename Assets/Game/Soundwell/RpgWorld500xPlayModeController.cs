using System;
using System.Collections.Generic;
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
        public int RequestedSupplySlotIndex;
        public RpgWorld500xModalKind RequestedModalToggle;
    }

    /// <summary>
    /// Single-component Play Mode controller uniting RpgWorld500xSceneBootstrapper,
    /// RpgWorld500xCampHavenService, RpgWorld500xSaveSlotManager, the 16-panel
    /// RpgWorld500xMenuRouter, RpgWorld500xQuickMenuDockHud, RpgWorld500xReconPulseHudBar,
    /// RpgWorld500xWeatherAdvisoryHudBadge, and RpgWorld500xFieldSupplyHotbarHud.
    /// Drives movement, interaction, overview map toggle, field recon pulse,
    /// field supply hotbar consumption, quick auto-save, DangerZone victory reward
    /// settlement, expedition digest recap, live weather advisory sync, and
    /// mutually-exclusive modal hotkeys through deterministic RpgWorld500xInputFrame frames.
    /// </summary>
    public sealed class RpgWorld500xPlayModeController : MonoBehaviour
    {
        private static readonly KeyCode[] ModalHotkeyOrder =
        {
            KeyCode.J,
            KeyCode.M,
            KeyCode.B,
            KeyCode.K,
            KeyCode.T,
            KeyCode.N,
            KeyCode.O,
            KeyCode.L,
            KeyCode.U,
            KeyCode.C,
            KeyCode.H,
            KeyCode.V,
            KeyCode.P,
            KeyCode.X,
            KeyCode.G,
            KeyCode.Y
        };

        [SerializeField] private RpgWorldMapId initialMapId = RpgWorldMapId.Soundwell;
        [SerializeField] private bool autoInitializeOnStart = true;

        private GameObject _uiRoot;
        private RpgWorld500xSceneBootstrapper _bootstrapper;
        private RpgWorld500xCampHavenService _campHaven;
        private RpgWorld500xSaveSlotManager _saveSlotManager;
        private RpgWorld500xMenuRouter _menuRouter;
        private RpgWorld500xQuickMenuDockHud _quickMenuDockHud;
        private RpgWorld500xReconPulseHudBar _reconPulseHud;
        private RpgWorld500xWeatherAdvisoryHudBadge _weatherAdvisoryHud;
        private RpgWorld500xFieldSupplyHotbarHud _supplyHotbarHud;
        private CanvasGroup _supplyHotbarInputGroup;
        private float _elapsedPlaySeconds;
        private bool _initialized;
        private bool _allowReconWorldInput;
        private string _lastAutoSaveStatusZh = string.Empty;
        private string _lastDangerVictoryBannerZh = string.Empty;

        public bool IsInitialized => _initialized && _bootstrapper != null && !_bootstrapper.IsDisposed;
        public RpgWorld500xSceneBootstrapper Bootstrapper => _bootstrapper;
        public RpgWorld500xCampHavenService CampHaven => _campHaven;
        public RpgWorld500xSaveSlotManager SaveSlotManager => _saveSlotManager;
        public RpgWorld500xMenuRouter MenuRouter => _menuRouter;
        public RpgWorld500xQuickMenuDockHud QuickMenuDockHud => _quickMenuDockHud;
        public RpgWorld500xReconPulseHudBar ReconPulseHud => _reconPulseHud;
        public RpgWorld500xWeatherAdvisoryHudBadge WeatherAdvisoryHud => _weatherAdvisoryHud;
        public RpgWorld500xFieldSupplyHotbarHud SupplyHotbarHud => _supplyHotbarHud;
        public string LastAutoSaveStatusZh => _lastAutoSaveStatusZh;
        public string LastDangerVictoryBannerZh => _lastDangerVictoryBannerZh;

        public static bool TryResolveModalHotkey(KeyCode key, out RpgWorld500xModalKind modalKind)
        {
            switch (key)
            {
                case KeyCode.J: modalKind = RpgWorld500xModalKind.QuestJournal; return true;
                case KeyCode.M: modalKind = RpgWorld500xModalKind.WorldAtlas; return true;
                case KeyCode.B: modalKind = RpgWorld500xModalKind.CampMerchant; return true;
                case KeyCode.K: modalKind = RpgWorld500xModalKind.Bestiary; return true;
                case KeyCode.T: modalKind = RpgWorld500xModalKind.FastTravelBeacons; return true;
                case KeyCode.N: modalKind = RpgWorld500xModalKind.CartographyReputation; return true;
                case KeyCode.O: modalKind = RpgWorld500xModalKind.SaveSlots; return true;
                case KeyCode.L: modalKind = RpgWorld500xModalKind.FieldRumorAndLore; return true;
                case KeyCode.U: modalKind = RpgWorld500xModalKind.ExpeditionMilestones; return true;
                case KeyCode.C: modalKind = RpgWorld500xModalKind.CacheAndShortcut; return true;
                case KeyCode.H: modalKind = RpgWorld500xModalKind.ExpeditionDigest; return true;
                case KeyCode.V: modalKind = RpgWorld500xModalKind.BountyBoard; return true;
                case KeyCode.P: modalKind = RpgWorld500xModalKind.ExpeditionReadiness; return true;
                case KeyCode.X: modalKind = RpgWorld500xModalKind.DangerTacticalBrief; return true;
                case KeyCode.G: modalKind = RpgWorld500xModalKind.WorldGazetteer; return true;
                case KeyCode.Y: modalKind = RpgWorld500xModalKind.TravelItinerary; return true;
                default: modalKind = RpgWorld500xModalKind.None; return false;
            }
        }

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
            RpgWorldMapId targetMap = overrideMap ?? initialMapId;
            // Validate the public play-mode entry before shutting down the live
            // controller; the driver's lower-level guard cannot restore a disposed UI.
            if (!Enum.IsDefined(typeof(RpgWorldMapId), targetMap))
            {
                throw new ArgumentOutOfRangeException(nameof(overrideMap), targetMap, "Unknown map id.");
            }

            Shutdown();
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
            _quickMenuDockHud = new RpgWorld500xQuickMenuDockHud(_uiRoot.transform, _menuRouter);
            _reconPulseHud = new RpgWorld500xReconPulseHudBar(_uiRoot.transform, _bootstrapper,
                canReceiveWorldInput: () => IsInitialized && _allowReconWorldInput && !_menuRouter.HasAnyModalOpen);
            _weatherAdvisoryHud = new RpgWorld500xWeatherAdvisoryHudBadge(_uiRoot.transform, _bootstrapper);
            _supplyHotbarHud = new RpgWorld500xFieldSupplyHotbarHud(
                _uiRoot.transform,
                _bootstrapper,
                _bootstrapper.CreateFieldSupplyUseService(),
                () => _elapsedPlaySeconds);
            // Reserve the bottom row for recon, weather and transient world toasts.
            // Position the hotbar above that row when composing the live world HUD.
            var supplyPanel = _uiRoot.transform.Find("World500xFieldSupplyHotbarHud/SupplyHotbarPanel") as RectTransform;
            if (supplyPanel != null)
            {
                _supplyHotbarInputGroup = supplyPanel.gameObject.AddComponent<CanvasGroup>();
                supplyPanel.anchoredPosition = new Vector2(0f, 142f);
                // This HUD currently uses Chinese copy regardless of story locale.
                // Use the same pinned OFL font as the localized story presentation,
                // rather than relying on the runner's Latin-only system font.
                Font chineseFont = Resources.Load<Font>("Soundwell/Fonts/SoundwellChinese");
                foreach (Text label in supplyPanel.GetComponentsInChildren<Text>(true))
                {
                    if (chineseFont != null)
                        label.font = chineseFont;
                    label.fontSize = 14;
                }
            }
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
            _allowReconWorldInput = true;
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

        public RpgWorld500xExpeditionDigest BuildExpeditionDigest()
        {
            if (!IsInitialized)
            {
                return default;
            }

            return RpgWorld500xCampfireJournalDigestService.BuildDigest(
                _bootstrapper,
                _elapsedPlaySeconds,
                _supplyHotbarHud != null ? _supplyHotbarHud.SupplyUseService : null);
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

            bool allowOpenWorldActions = !wasModalOpen && !_menuRouter.HasAnyModalOpen;
            // Raw UGUI callbacks must obey the same live modal and closing-frame guard as R.
            _allowReconWorldInput = allowOpenWorldActions;
            // Mouse and keyboard must share the modal guard, including the closing frame.
            if (_supplyHotbarInputGroup != null)
            {
                _supplyHotbarInputGroup.interactable = allowOpenWorldActions;
                _supplyHotbarInputGroup.blocksRaycasts = allowOpenWorldActions;
            }

            if (frame.ToggleOverviewMapPressed && allowOpenWorldActions)
            {
                _bootstrapper.Driver.Facade.Session.ToggleOverviewMap();
            }

            _reconPulseHud?.UpdateCooldownDisplay(_elapsedPlaySeconds);
            if (frame.ReconPulsePressed && allowOpenWorldActions)
            {
                _reconPulseHud?.TryClickPulseScan(_elapsedPlaySeconds);
            }

            if (frame.RequestedSupplySlotIndex > 0 && allowOpenWorldActions && _supplyHotbarHud != null)
            {
                IReadOnlyList<RpgWorld500xCampSupplyItemDef> supplies =
                    RpgWorld500xBountyAndSupplyCatalog.GetCampSupplies();
                int idx = frame.RequestedSupplySlotIndex - 1;
                if (idx >= 0 && idx < supplies.Count)
                {
                    _supplyHotbarHud.TryClickUseSupply(supplies[idx].Id, _elapsedPlaySeconds);
                }
            }

            RpgWorld500xEffectiveExplorationModifiers mods = _menuRouter.StepFrameWithModalGuard(
                wasModalOpen ? Vector2.zero : frame.Move,
                wasModalOpen ? 0f : deltaSeconds,
                !wasModalOpen && frame.Sprint,
                !wasModalOpen && frame.InteractPressed);

            _quickMenuDockHud?.RefreshActiveHighlight();
            _weatherAdvisoryHud?.Refresh(_elapsedPlaySeconds);
            _supplyHotbarHud?.Refresh(_elapsedPlaySeconds);
            return mods;
        }

        public void Shutdown()
        {
            _initialized = false;
            _allowReconWorldInput = false;
            _supplyHotbarHud?.Dispose();
            _supplyHotbarHud = null;
            _supplyHotbarInputGroup = null;
            _weatherAdvisoryHud?.Dispose();
            _weatherAdvisoryHud = null;
            _reconPulseHud?.Dispose();
            _reconPulseHud = null;
            _quickMenuDockHud?.Dispose();
            _quickMenuDockHud = null;
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

            if (Input.GetKeyDown(KeyCode.Alpha1)) frame.RequestedSupplySlotIndex = 1;
            else if (Input.GetKeyDown(KeyCode.Alpha2)) frame.RequestedSupplySlotIndex = 2;
            else if (Input.GetKeyDown(KeyCode.Alpha3)) frame.RequestedSupplySlotIndex = 3;
            else if (Input.GetKeyDown(KeyCode.Alpha4)) frame.RequestedSupplySlotIndex = 4;
            else if (Input.GetKeyDown(KeyCode.Alpha5)) frame.RequestedSupplySlotIndex = 5;
            else if (Input.GetKeyDown(KeyCode.Alpha6)) frame.RequestedSupplySlotIndex = 6;

            for (int i = 0; i < ModalHotkeyOrder.Length; i++)
            {
                if (Input.GetKeyDown(ModalHotkeyOrder[i]) &&
                    TryResolveModalHotkey(ModalHotkeyOrder[i], out RpgWorld500xModalKind modalKind))
                {
                    frame.RequestedModalToggle = modalKind;
                    break;
                }
            }
#endif
            return frame;
        }
    }
}
