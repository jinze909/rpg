using System;
using System.Collections.Generic;
using UnityEngine;

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
        public RpgWorld500xModalKind RequestedModalToggle;
    }

    /// <summary>
    /// Single-component Play Mode controller uniting RpgWorld500xSceneBootstrapper,
    /// RpgWorld500xCampHavenService, RpgWorld500xSaveSlotManager, and the 9-panel
    /// RpgWorld500xMenuRouter. Drives movement, interaction, overview map toggle,
    /// quick auto-save, and mutually-exclusive modal hotkeys through deterministic
    /// RpgWorld500xInputFrame frames.
    /// </summary>
    public sealed class RpgWorld500xPlayModeController : MonoBehaviour
    {
        [SerializeField] private RpgWorldMapId initialMapId = RpgWorldMapId.Soundwell;
        [SerializeField] private bool autoInitializeOnStart = true;

        private readonly Dictionary<string, int> _metaStore =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private GameObject _uiRoot;
        private RpgWorld500xSceneBootstrapper _bootstrapper;
        private RpgWorld500xCampHavenService _campHaven;
        private RpgWorld500xSaveSlotManager _saveSlotManager;
        private RpgWorld500xMenuRouter _menuRouter;
        private bool _initialized;
        private string _lastAutoSaveStatusZh = string.Empty;

        public bool IsInitialized => _initialized && _bootstrapper != null && !_bootstrapper.IsDisposed;
        public RpgWorld500xSceneBootstrapper Bootstrapper => _bootstrapper;
        public RpgWorld500xCampHavenService CampHaven => _campHaven;
        public RpgWorld500xSaveSlotManager SaveSlotManager => _saveSlotManager;
        public RpgWorld500xMenuRouter MenuRouter => _menuRouter;
        public string LastAutoSaveStatusZh => _lastAutoSaveStatusZh;

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

            _campHaven = new RpgWorld500xCampHavenService(
                getInt: k => _metaStore.TryGetValue(k, out int v) ? v : 0,
                setInt: (k, v) => _metaStore[k] = v);

            IRpgWorld500xSaveSlotStorage resolvedStorage = saveStorage ??
                (Application.isPlaying
                    ? (IRpgWorld500xSaveSlotStorage)new RpgWorld500xPlayerPrefsSaveSlotStorage()
                    : new RpgWorld500xInMemorySaveSlotStorage());

            _saveSlotManager = new RpgWorld500xSaveSlotManager(resolvedStorage, utcNowProvider);
            _menuRouter = new RpgWorld500xMenuRouter(_uiRoot.transform, _bootstrapper, _campHaven, _saveSlotManager);
            _lastAutoSaveStatusZh = string.Empty;
            _initialized = true;
        }

        public RpgWorld500xEffectiveExplorationModifiers ProcessInputFrame(
            RpgWorld500xInputFrame frame,
            float deltaSeconds)
        {
            if (!IsInitialized)
            {
                return default;
            }

            if (frame.CloseModalPressed && _menuRouter.HasAnyModalOpen)
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

            if (frame.ToggleOverviewMapPressed && !_menuRouter.HasAnyModalOpen)
            {
                _bootstrapper.Driver.Facade.Session.ToggleOverviewMap();
            }

            return _menuRouter.StepFrameWithModalGuard(
                frame.Move,
                deltaSeconds,
                frame.Sprint,
                frame.InteractPressed);
        }

        public void Shutdown()
        {
            _initialized = false;
            _menuRouter?.Dispose();
            _menuRouter = null;
            _bootstrapper?.Dispose();
            _bootstrapper = null;
            _campHaven = null;
            _saveSlotManager = null;

            if (_uiRoot != null)
            {
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
