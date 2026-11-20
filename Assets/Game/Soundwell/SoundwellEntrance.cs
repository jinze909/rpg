using GamingIsLove.Makinom;
using GamingIsLove.ORKFramework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    public sealed class SoundwellEntrance : MonoBehaviour
    {
        public const string SceneName = "SoundwellTrial";
        public const string BootstrapSceneName = "Bootstrap";
        public static bool RoleEncounterRequested { get; private set; }

        // The selected player-facing encounter applies only to the next loaded
        // Soundwell scene. Otherwise a later prototype comparison or reload
        // can silently inherit the previous battle mode.
        internal static bool ConsumeRoleEncounterRequest()
        {
            if (!RoleEncounterRequested) return false;
            RoleEncounterRequested = false;
            return true;
        }

        private AsyncOperation trialLoad;
        private Button entryButton;
        private RectTransform entryRect;
        private Text titleText;
        private Text actionText;
        private Text footerText;
        private Text outcomeText;
        private Button sfxButton;
        private Button formationButton;
        private RectTransform formationRect;
        private Text formationText;
        private Button prologueButton;
        private RectTransform prologueRect;
        private Button saveCheckpointButton, loadCheckpointButton;
        private RectTransform saveCheckpointRect, loadCheckpointRect;
        private Text saveCheckpointText, loadCheckpointText;
        private bool confirmCheckpointOverwrite, confirmCheckpointLoad;
        private bool checkpointReadFailed;
        private bool checkpointLoading, checkpointSaved;
        private Canvas entryCanvas;
        private WhitewaterPrelude currentPrelude;
        private M00MemorialSquare currentMemorial;
        // A UGUI Submit that closes S00 can be the same gamepad-A event
        // read by the entry menu on that frame. Do not treat it as a second
        // request to load the combat trial or reopen the prologue.
        private int lastOpeningExitFrame = -1;
        private Button languageButton;
        private RectTransform languageRect;
        private Text languageText;
        private RectTransform sfxRect;
        private Text sfxText;
        private Font englishFont;
        private float layoutAspect = -1f;
        private string shownEntryPrompt;
        private bool entryBindingHintDirty;
        private float nextOutcomeRefreshAt;
        private const float OutcomeRefreshInterval = 0.20f;
        private string shownPreludeProgressKey;
        private int shownOutcome = -1;
        private bool shownOutcomeRecorded;
        private bool shownCartKitRecovered;
        private bool shownSafeRouteMarked;
        private int shownBeastContacts = -1;
        private bool shownRescueSettled;
        private string shownRescuerExperience = string.Empty;

        public int LastReturnOutcome => shownOutcome;
        public string ReturnSummaryText => outcomeText != null ? outcomeText.text : string.Empty;
        public bool LastReturnCartKitRecovered => shownCartKitRecovered;
        public bool LastReturnSafeRouteMarked => shownSafeRouteMarked;
        public int LastReturnBeastContacts => shownBeastContacts;
        public bool MemorialSquareOpen => currentMemorial != null;
        public M00MemorialSquare CurrentMemorialSquare => currentMemorial;

        private void Awake()
        {
            RoleEncounterRequested = false;
            SoundwellInputAdapter.SetEntryContext();
            SoundwellInputAdapter.EnsureEventSystem(transform);
            var canvas = new GameObject("Soundwell entry prompt", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            entryCanvas = canvas;
            var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 540);
            scaler.matchWidthOrHeight = 0.5f;
            canvas.gameObject.AddComponent<GraphicRaycaster>();

            titleText = SoundwellView.AddLabel(canvas.transform, "SOUNDWELL SIGNAL TRIAL", 32,
                new Color(0.86f, 0.93f, 0.97f), new Vector2(0f, 70f), new Vector2(520f, 48f));

            var buttonObject = new GameObject("Enter Soundwell button",
                typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -8f);
            rect.sizeDelta = new Vector2(460f, 62f);
            entryRect = rect;

            var background = buttonObject.GetComponent<Image>();
            background.color = new Color(0.15f, 0.24f, 0.30f);
            entryButton = buttonObject.GetComponent<Button>();
            var colors = entryButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.92f, 0.70f);
            colors.selectedColor = new Color(1f, 0.86f, 0.60f);
            colors.pressedColor = new Color(0.95f, 0.72f, 0.36f);
            colors.disabledColor = new Color(0.40f, 0.44f, 0.47f);
            entryButton.colors = colors;
            entryButton.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            actionText = SoundwellView.AddLabel(rect, "[F2 / Pad A] Enter the trial", 22,
                new Color(1f, 0.72f, 0.33f), Vector2.zero, new Vector2(440f, 50f));
            entryButton.onClick.AddListener(() => OpenPlayableTrial());

            footerText = SoundwellView.AddLabel(canvas.transform, "CLICK / ENTER / PAD A TO BEGIN", 15,
                new Color(0.65f, 0.78f, 0.83f), new Vector2(0f, -65f), new Vector2(440f, 28f));
            outcomeText = SoundwellView.AddLabel(canvas.transform, "", 17,
                new Color(0.58f, 0.88f, 0.76f), new Vector2(0f, -126f), new Vector2(600f, 64f));

            // Player-facing control for the existing persistent SFX mixer bus.
            // Keep it on the entry screen so it never competes with ORK turns.
            var sfxObject = new GameObject("Soundwell SFX toggle",
                typeof(RectTransform), typeof(Image), typeof(Button));
            sfxObject.transform.SetParent(canvas.transform, false);
            sfxRect = (RectTransform)sfxObject.transform;
            sfxRect.anchorMin = sfxRect.anchorMax = new Vector2(0.5f, 0.5f);
            sfxRect.anchoredPosition = new Vector2(0f, -230f);
            sfxRect.sizeDelta = new Vector2(320f, 40f);
            sfxObject.GetComponent<Image>().color = new Color(0.15f, 0.24f, 0.30f);
            sfxButton = sfxObject.GetComponent<Button>();
            sfxButton.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            sfxButton.onClick.AddListener(ToggleSfx);
            sfxText = SoundwellView.AddLabel(sfxRect, "", 16,
                new Color(0.80f, 0.91f, 0.93f), Vector2.zero, new Vector2(314f, 36f));

            // Choose the SAME rescue encounter with or without optional
            // positional steps. Changing this preference never spends an ORK turn.
            var formationObject = new GameObject("Soundwell formation toggle",
                typeof(RectTransform), typeof(Image), typeof(Button));
            formationObject.transform.SetParent(canvas.transform, false);
            formationRect = (RectTransform)formationObject.transform;
            formationRect.anchorMin = formationRect.anchorMax = new Vector2(0.5f, 0.5f);
            formationRect.anchoredPosition = new Vector2(150f, -230f);
            formationRect.sizeDelta = new Vector2(280f, 40f);
            formationObject.GetComponent<Image>().color = new Color(0.15f, 0.24f, 0.30f);
            formationButton = formationObject.GetComponent<Button>();
            formationButton.onClick.AddListener(ToggleFormationPreference);
            formationText = SoundwellView.AddLabel(formationRect, "", 16,
                new Color(0.80f, 0.91f, 0.93f), Vector2.zero, new Vector2(270f, 36f));
            formationText.resizeTextForBestFit = true;
            formationText.resizeTextMinSize = 11;
            formationText.resizeTextMaxSize = 16;

            // Let keyboard and gamepad users select their language BEFORE
            // entering the trial. This uses the same saved StringTables and
            // PlayerPrefs-backed toggle as the trial's existing UGUI button.
            var languageObject = new GameObject("Soundwell entry Language button",
                typeof(RectTransform), typeof(Image), typeof(Button));
            languageObject.transform.SetParent(canvas.transform, false);
            languageRect = (RectTransform)languageObject.transform;
            languageRect.anchorMin = languageRect.anchorMax = new Vector2(0.5f, 0.5f);
            languageRect.anchoredPosition = new Vector2(0f, 160f);
            languageRect.sizeDelta = new Vector2(204f, 40f);
            languageObject.GetComponent<Image>().color = new Color(0.15f, 0.24f, 0.30f);
            languageButton = languageObject.GetComponent<Button>();
            languageButton.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            languageButton.onClick.AddListener(SoundwellUiLocalization.Toggle);
            languageText = SoundwellView.AddLabel(languageRect, "", 16,
                new Color(0.80f, 0.91f, 0.93f), Vector2.zero, new Vector2(198f, 36f));
            // The next story slice is an optional real playable scene, not
            // a replacement for the existing Soundwell regression entry.
            var prologueObject = new GameObject("S00 Whitewater prologue button",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            prologueObject.transform.SetParent(canvas.transform, false);
            prologueRect = (RectTransform)prologueObject.transform;
            prologueRect.anchorMin = prologueRect.anchorMax = new Vector2(0.5f, 0.5f);
            prologueRect.sizeDelta = new Vector2(254f, 36f);
            prologueObject.GetComponent<Image>().color = new Color(0.15f, 0.24f, 0.30f);
            prologueButton = prologueObject.GetComponent<Button>();
            prologueButton.colors = entryButton.colors;
            prologueButton.onClick.AddListener(OpenPrelude);
            var prologueLabel = SoundwellView.AddLabel(prologueRect,
                SoundwellUiLocalization.Get("entry.whitewater_prologue"), 17,
                new Color(0.98f, 0.80f, 0.52f), Vector2.zero, new Vector2(242f, 33f));
            // The compact 220px button must fit both authored languages;
            // UGUI shrinks the glyphs only when needed, never the hit target.
            prologueLabel.resizeTextForBestFit = true;
            prologueLabel.resizeTextMinSize = 12;
            prologueLabel.resizeTextMaxSize = 17;
            saveCheckpointButton = NewCheckpointButton(canvas.transform,
                "Save ORK checkpoint button", out saveCheckpointRect, out saveCheckpointText);
            loadCheckpointButton = NewCheckpointButton(canvas.transform,
                "Load ORK checkpoint button", out loadCheckpointRect, out loadCheckpointText);
            saveCheckpointButton.onClick.AddListener(SaveCheckpoint);
            loadCheckpointButton.onClick.AddListener(LoadCheckpoint);
            ConfigureEntrySettingsNavigation();
            titleText.resizeTextForBestFit = true;
            titleText.resizeTextMinSize = 18;
            titleText.resizeTextMaxSize = 32;
            actionText.resizeTextForBestFit = true;
            actionText.resizeTextMinSize = 15;
            actionText.resizeTextMaxSize = 22;
            footerText.resizeTextForBestFit = true;
            footerText.resizeTextMinSize = 11;
            footerText.resizeTextMaxSize = 15;
            outcomeText.resizeTextForBestFit = true;
            outcomeText.resizeTextMinSize = 13;
            outcomeText.resizeTextMaxSize = 17;
            englishFont = titleText.font;
            RefreshLocalizedPresentation();
            LayoutForViewportAspect((float)Screen.width / Mathf.Max(Screen.height, 1));
        }

        private void OnEnable()
        {
            SoundwellUiLocalization.Changed += RefreshLocalizedPresentation;
            InputSystem.onActionChange += OnInputActionChanged;
            // Refresh immediately when this screen is shown again.
            entryBindingHintDirty = true;
            nextOutcomeRefreshAt = 0f;
        }

        private void OnDisable()
        {
            SoundwellUiLocalization.Changed -= RefreshLocalizedPresentation;
            InputSystem.onActionChange -= OnInputActionChanged;
        }

        private static bool ShouldRefreshBindingPrompt(InputActionChange change) =>
            change == InputActionChange.BoundControlsChanged;

        private void OnInputActionChanged(object source, InputActionChange change)
        {
            // Input System owns effective control changes; individual action
            // presses must never trigger a new binding-label lookup.
            if (ShouldRefreshBindingPrompt(change))
                entryBindingHintDirty = true;
        }

        private void RefreshLocalizedPresentation()
        {
            if (titleText == null)
                return;
            // Keep the return screen in the same authored StringTables as
            // Soundwell's UGUI controls. ORK and Makinom remain authoritative.
            Font font = SoundwellUiLocalization.PresentationFont(englishFont);
            titleText.font = actionText.font = footerText.font = outcomeText.font = font;
            titleText.text = SoundwellUiLocalization.Get("entry.title");
            // Re-render both effective shortcut labels on a locale switch.
            shownEntryPrompt = null;
            RefreshEntryShortcutHint();
            footerText.text = SoundwellUiLocalization.Get("entry.footer");
            RefreshReturnOutcome(true);
            RefreshSfxText();
            languageText.font = font;            RefreshFormationText();

            languageText.text = SoundwellUiLocalization.Get("action.language");
            RefreshPreludeProgress(true);
            RefreshCheckpointControls();
        }

        // S00 only records what the player ACTUALLY reached in Makinom.
        // The prologue button always starts from the opening; when a prior
        // visit exists it must say Replay, never falsely promise Resume.
        public static string PreludeProgressKey(bool evacueeSafe,
            bool secondBellHeard, bool cableHeld, bool thirdBellHeard = false)
        {
            // This flag is earned only from the real S00 ORK victory callback.
            // Earlier scene exits may only advertise what the player reached.
            if (thirdBellHeard)
                return "entry.whitewater_replay_third_bell";
            if (cableHeld)
                return "entry.whitewater_replay_cable";
            if (secondBellHeard)
                return "entry.whitewater_replay_bell";
            if (evacueeSafe)
                return "entry.whitewater_replay_evacuee";
            return "entry.whitewater_prologue";
        }

        private void RefreshPreludeProgress(bool force = false)
        {
            if (prologueButton == null)
                return;
            var state = Maki.Initialized ? Maki.Game?.Variables : null;
            string key = PreludeProgressKey(
                state != null && state.GetInt(WhitewaterPrelude.EvacueeEscortedKey) != 0,
                state != null && state.GetInt(WhitewaterPrelude.SecondBellKey) != 0,
                state != null && state.GetInt(WhitewaterPrelude.CableHoldKey) != 0,
                state != null && state.GetInt(WhitewaterPrelude.ThirdBellKey) != 0);
            if (!force && key == shownPreludeProgressKey)
                return;
            Text label = prologueButton.GetComponentInChildren<Text>();
            if (label == null)
                return;
            shownPreludeProgressKey = key;
            label.font = SoundwellUiLocalization.PresentationFont(englishFont);
            label.text = SoundwellUiLocalization.Get(key);
        }

        private void RefreshEntryShortcutHint()
        {
            if (actionText == null)
                return;
            string prompt = EntryActionPromptForCurrentBindings();
            if (prompt == shownEntryPrompt)
                return;
            shownEntryPrompt = prompt;
            actionText.text = prompt;
        }

        // Display only: the input adapter still owns Entry actions. Focused
        // UGUI Submit remains separate from this rebindable entry shortcut.
        public static string EntryActionPromptForCurrentBindings()
        {
            string authored = SoundwellUiLocalization.Get("entry.action");
            int end = authored.IndexOf(']');
            if (authored.Length == 0 || authored[0] != '[' || end < 0)
                return authored;

            string keyboard = SoundwellInputAdapter.EntryKeyboardShortcutLabel();
            var entry = Resources.Load<InputActionAsset>("Input/SoundwellControls")
                ?.FindActionMap("Entry", false)?.FindAction("Interact", false);
            string gamepad = string.Empty;
            if (entry != null)
            {
                for (int i = 0; i < entry.bindings.Count; i++)
                {
                    var binding = entry.bindings[i];
                    if (binding.isComposite || binding.isPartOfComposite ||
                        string.IsNullOrEmpty(binding.effectivePath) ||
                        !binding.effectivePath.StartsWith("<Gamepad>/",
                            System.StringComparison.OrdinalIgnoreCase))
                        continue;
                    // Retain the authored default A; display remapped buttons
                    // using the real Input System binding rather than Pad A.
                    gamepad = string.Equals(binding.effectivePath,
                        "<Gamepad>/buttonSouth",
                        System.StringComparison.OrdinalIgnoreCase)
                        ? "A" : entry.GetBindingDisplayString(i);
                    break;
                }
            }

            bool chinese = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            string padLabel = string.IsNullOrEmpty(gamepad) ? string.Empty :
                (chinese ? "手柄 " : "Pad ") + gamepad;
            string combined = !string.IsNullOrEmpty(keyboard) && !string.IsNullOrEmpty(padLabel)
                ? keyboard + (chinese ? "／" : " / ") + padLabel
                : !string.IsNullOrEmpty(keyboard) ? keyboard : padLabel;
            return string.IsNullOrEmpty(combined)
                ? authored.Substring(end + 1).TrimStart()
                : "[" + combined + "]" + authored.Substring(end + 1);
        }

        // Use the actual UGUI navigation graph for D-pad and keyboard arrows:
        // Language (top) -> Enter (middle) -> SFX (bottom), wrapping at the ends.
        // Automatic spatial navigation can skip the small settings controls on
        // narrow layouts. Reuse the Entry button's selected/highlight palette so
        // all three focus targets have the same visible feedback.
        private void ConfigureEntrySettingsNavigation()
        {
            entryButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = prologueButton,
                selectOnDown = sfxButton,
                selectOnRight = formationButton
            };
            languageButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = sfxButton,
                selectOnDown = prologueButton,
                selectOnLeft = saveCheckpointButton,
                selectOnRight = loadCheckpointButton
            };
            saveCheckpointButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = loadCheckpointButton,
                selectOnRight = languageButton,
                selectOnUp = sfxButton,
                selectOnDown = languageButton
            };
            loadCheckpointButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = languageButton,
                selectOnRight = saveCheckpointButton,
                selectOnUp = formationButton,
                selectOnDown = languageButton
            };
            prologueButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = languageButton,
                selectOnDown = entryButton
            };
            sfxButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = entryButton,
                selectOnDown = languageButton,
                selectOnRight = formationButton
            };
            formationButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = sfxButton,
                selectOnUp = entryButton,
                selectOnDown = languageButton
            };
            sfxButton.transition = Selectable.Transition.ColorTint;
            formationButton.transition = Selectable.Transition.ColorTint;
            languageButton.transition = Selectable.Transition.ColorTint;
            sfxButton.colors = entryButton.colors;
            formationButton.colors = entryButton.colors;
            languageButton.colors = entryButton.colors;
            prologueButton.colors = entryButton.colors;
            saveCheckpointButton.colors = entryButton.colors;
            loadCheckpointButton.colors = entryButton.colors;
        }


        private bool CheckpointReady =>
            entryCanvas != null && entryCanvas.isActiveAndEnabled && trialLoad == null &&
            currentPrelude == null && currentMemorial == null &&
            lastOpeningExitFrame != Time.frameCount &&
            SceneManager.GetActiveScene().name == BootstrapSceneName &&
            Maki.Initialized && ORK.Initialized && Maki.Game?.Variables != null;

        private static Button NewCheckpointButton(Transform parent, string name,
            out RectTransform rect, out Text label)
        {
            var go = new GameObject(name, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(168f, 36f);
            go.GetComponent<Image>().color = new Color(0.15f, 0.24f, 0.30f);
            label = SoundwellView.AddLabel(rect, "", 15,
                new Color(0.85f, 0.92f, 0.94f), Vector2.zero, new Vector2(132f, 30f));
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 11;
            label.resizeTextMaxSize = 15;
            return go.GetComponent<Button>();
        }

        private void RefreshCheckpointControls()
        {
            if (saveCheckpointButton == null)
                return;
            bool ready = CheckpointReady && !checkpointLoading;
            bool exists = false;
            if (ready)
            {
                try { exists = Maki.SaveGame.FileExists(0); }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("ORK checkpoint unavailable: " + ex.Message);
                    ready = false;
                }
            }
            saveCheckpointButton.interactable = ready;
            loadCheckpointButton.interactable = ready && exists;
            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            var font = SoundwellUiLocalization.PresentationFont(englishFont);
            saveCheckpointText.font = loadCheckpointText.font = font;
            saveCheckpointText.text = !ready ? (zh ? "存档暂不可用" : "Save unavailable")
                : confirmCheckpointOverwrite && exists ? (zh ? "确认覆盖存档" : "Confirm overwrite")
                : checkpointSaved && exists ? (zh ? "已存档" : "Checkpoint saved")
                : (zh ? "保存检查点" : "Save checkpoint");
            loadCheckpointText.text = checkpointLoading ? (zh ? "正在读档" : "Loading checkpoint")
                : checkpointReadFailed ? (zh ? "无法读取存档" : "Could not read checkpoint")
                : !exists || !ready ? (zh ? "暂无存档" : "No saved checkpoint")
                : confirmCheckpointLoad ? (zh ? "确认读取存档" : "Confirm load")
                : (zh ? "读取检查点" : "Load checkpoint");
        }

        private void SaveCheckpoint()
        {
            if (!CheckpointReady || checkpointLoading)
                return;
            var saves = Maki.SaveGame;
            try
            {
                bool exists = saves.FileExists(0);
                if (exists && !confirmCheckpointOverwrite)
                {
                    confirmCheckpointOverwrite = true;
                    confirmCheckpointLoad = false;
                    checkpointSaved = false;
                    RefreshCheckpointControls();
                    return;
                }
                saves.Save(0);
                confirmCheckpointOverwrite = confirmCheckpointLoad = false;
                checkpointReadFailed = false;
                checkpointSaved = saves.FileExists(0);
            }
            catch (System.Exception ex)
            {
                confirmCheckpointOverwrite = checkpointSaved = false;
                Debug.LogError("ORK checkpoint save failed: " + ex);
            }
            RefreshCheckpointControls();
        }

        private void LoadCheckpoint()
        {
            if (!CheckpointReady || checkpointLoading)
                return;
            var saves = Maki.SaveGame;
            try
            {
                if (!saves.FileExists(0))
                    return;
                if (!confirmCheckpointLoad)
                {
                    confirmCheckpointLoad = true;
                    confirmCheckpointOverwrite = checkpointSaved = false;
                    checkpointReadFailed = false;
                    RefreshCheckpointControls();
                    return;
                }
                confirmCheckpointLoad = false;
                // ORK clears the live session before parsing in Load.
                // Check the file's required roots and scene before allowing that reset.
                if (!CanReadCheckpoint(saves.LoadFile(0)))
                    throw new System.IO.InvalidDataException("The checkpoint is incomplete or its scene is unavailable.");
                checkpointLoading = true;
                saves.GameLoaded += OnCheckpointLoaded;
                saves.Load(0);
                if (checkpointLoading && !saves.IsLoading)
                    throw new System.IO.IOException("The checkpoint disappeared before loading began.");
            }
            catch (System.Exception ex)
            {
                saves.GameLoaded -= OnCheckpointLoaded;
                checkpointLoading = false;
                checkpointReadFailed = true;
                Debug.LogWarning("ORK checkpoint could not be read. " + ex.Message);
            }
            RefreshCheckpointControls();
        }

        private static bool CanReadCheckpoint(DataObject data)
        {
            var ork = data?.GetFile("ORK");
            if (data?.GetFile("game") == null || ork?.GetFile("game") == null)
                return false;
            var settings = ORK.SaveGameSettings;
            if (settings.saveGroups && ork.GetFile("groups") == null)
                return false;
            string scene = settings.saveSceneName;
            if (settings.saveGroups && settings.saveGroupPosition)
                ork.Get("scenename", ref scene);
            return !string.IsNullOrEmpty(scene) && Application.CanStreamedLevelBeLoaded(scene);
        }

        private void OnCheckpointLoaded()
        {
            Maki.SaveGame.GameLoaded -= OnCheckpointLoaded;
            checkpointLoading = false;
            checkpointReadFailed = false;
            confirmCheckpointOverwrite = confirmCheckpointLoad = checkpointSaved = false;
            RefreshReturnOutcome(true);
            RefreshPreludeProgress(true);
            RefreshCheckpointControls();
        }

        private void OnDestroy()
        {
            if (checkpointLoading && Maki.Initialized)
                Maki.SaveGame.GameLoaded -= OnCheckpointLoaded;
        }

        private void ToggleFormationPreference()
        {
            OrkPartyBridge.SetPreferredFixedFormation(!OrkPartyBridge.PreferredFixedFormation);
            RefreshFormationText();
        }

        private void RefreshFormationText()
        {
            if (formationText == null)
                return;
            formationText.font = SoundwellUiLocalization.PresentationFont(englishFont);
            formationText.text = SoundwellUiLocalization.Get(
                OrkPartyBridge.PreferredFixedFormation
                    ? "entry.formation_fixed" : "entry.formation_mobile");
        }

        private void ToggleSfx()
        {
            // Missing or malformed audio configuration is visible in the
            // console, but must not crash the entry menu or change the saved
            // mute choice without a successful mixer operation.
            if (SoundwellAudioBus.ToggleMuted())
                RefreshSfxText();
        }

        private void RefreshSfxText()
        {
            if (sfxText == null)
                return;
            sfxText.font = SoundwellUiLocalization.PresentationFont(englishFont);
            sfxText.text = SoundwellUiLocalization.Get(
                SoundwellAudioBus.IsMuted ? "entry.sfx_unmute" : "entry.sfx_mute");
        }

        private void Start()
        {
            // A real selection allows the standard UI Submit mapping (Enter / pad A)
            // without inventing a parallel focus/navigation implementation.
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(entryButton.gameObject);
            RefreshReturnOutcome();
        }

        // Responsive scene-local UGUI layout; no new scene or parallel input stack.
        // Widths below are canvas units, with CanvasScaler handling screen pixels.
        public void LayoutForViewportAspect(float aspect)
        {
            layoutAspect = Mathf.Max(0.1f, aspect);
            bool compact = layoutAspect <= 1.25f;
            entryRect.sizeDelta = new Vector2(compact ? 310f : 460f, compact ? 68f : 62f);
            entryRect.anchoredPosition = new Vector2(0f, compact ? -5f : -8f);
            titleText.rectTransform.sizeDelta = new Vector2(compact ? 340f : 520f, 50f);
            titleText.rectTransform.anchoredPosition = new Vector2(0f, compact ? 96f : 70f);
            titleText.fontSize = compact ? 26 : 32;
            actionText.rectTransform.sizeDelta = new Vector2(compact ? 295f : 440f, 50f);
            actionText.fontSize = compact ? 19 : 22;
            footerText.rectTransform.sizeDelta = new Vector2(compact ? 320f : 440f, 30f);
            footerText.rectTransform.anchoredPosition = new Vector2(0f, compact ? -80f : -65f);
            footerText.fontSize = compact ? 13 : 15;
            // The return summary sits BETWEEN the footer and SFX button.
            // Its previous top overlapped the footer by 12-16 canvas units,
            // obscuring the rescue result on both wide and narrow screens.
            outcomeText.rectTransform.sizeDelta = new Vector2(compact ? 350f : 600f, compact ? 102f : 116f);
            outcomeText.rectTransform.anchoredPosition = new Vector2(0f, compact ? -153f : -145f);
            outcomeText.fontSize = compact ? 15 : 17;
            // Same horizontal row keeps both settings above the bottom edge,
            // and prevents the return summary from overlapping either control.
            sfxRect.anchoredPosition = new Vector2(compact ? -94f : -150f, -230f);
            sfxRect.sizeDelta = new Vector2(compact ? 176f : 280f, 40f);
            sfxText.rectTransform.sizeDelta = new Vector2(compact ? 166f : 270f, 36f);
            sfxText.resizeTextForBestFit = true;
            sfxText.resizeTextMinSize = 11;
            sfxText.resizeTextMaxSize = 16;
            formationRect.anchoredPosition = new Vector2(compact ? 94f : 150f, -230f);
            formationRect.sizeDelta = new Vector2(compact ? 176f : 280f, 40f);
            formationText.rectTransform.sizeDelta =
                new Vector2(compact ? 166f : 270f, 36f);
            languageRect.anchoredPosition = new Vector2(0f, compact ? 181f : 160f);
            languageRect.sizeDelta = new Vector2(compact ? 186f : 204f, 40f);
            prologueRect.anchoredPosition = new Vector2(0f, compact ? 140f : 119f);
            prologueRect.sizeDelta = new Vector2(compact ? 220f : 254f, compact ? 32f : 36f);
            var prologueLabel = prologueButton.GetComponentInChildren<Text>();
            if (prologueLabel != null)
                prologueLabel.rectTransform.sizeDelta =
                    new Vector2(compact ? 210f : 242f, compact ? 28f : 33f);
            languageText.rectTransform.sizeDelta = new Vector2(compact ? 180f : 198f, 36f);
            saveCheckpointRect.anchoredPosition = new Vector2(compact ? -83f : -300f, compact ? 228f : 160f);
            loadCheckpointRect.anchoredPosition = new Vector2(compact ? 83f : 300f, compact ? 228f : 160f);
            saveCheckpointRect.sizeDelta = loadCheckpointRect.sizeDelta =
                new Vector2(compact ? 144f : 168f, 36f);

            // CanvasScaler's geometric-mean scale makes the logical height
            // just ~322 at 5:1. The normal y=-230 settings row is then fully
            // offscreen, even though the window has ample horizontal space.
            // Keep all five real buttons reachable in two clear columns.
            if (layoutAspect >= 2.55f)
            {
                titleText.rectTransform.anchoredPosition = new Vector2(-270f, 101f);
                entryRect.anchoredPosition = new Vector2(-270f, 15f);
                footerText.rectTransform.anchoredPosition = new Vector2(-270f, -60f);
                outcomeText.rectTransform.anchoredPosition = new Vector2(-270f, -112f);
                outcomeText.rectTransform.sizeDelta = new Vector2(500f, 64f);

                languageRect.anchoredPosition = new Vector2(300f, 105f);
                prologueRect.anchoredPosition = new Vector2(300f, 52f);
                formationRect.anchoredPosition = new Vector2(300f, -13f);
                sfxRect.anchoredPosition = new Vector2(300f, -77f);
                saveCheckpointRect.anchoredPosition = new Vector2(205f, -129f);
                loadCheckpointRect.anchoredPosition = new Vector2(405f, -129f);
            }
        }

        private void Update()
        {
            // Keep ownership of the story objects without polling the hidden Entry UI.
            if (currentPrelude != null || currentMemorial != null)
                return;

            // ORK can create or reactivate its own EventSystem after this
            // component's Awake. While Entry is visible, restore the single
            // New Input System UI owner and keep navigation/Submit enabled.
            if (entryCanvas != null && entryCanvas.isActiveAndEnabled)
            {
                EventSystem current = EventSystem.current;
                if (current == null ||
                    current.GetComponent<InputSystemUIInputModule>() == null ||
                    !current.sendNavigationEvents)
                    SoundwellInputAdapter.EnsureEventSystem(transform);
            }

            // Makinom lacks a public outcome/marker change notification.
            // Poll its authoritative state at a bounded UI cadence.
            if (Time.unscaledTime >= nextOutcomeRefreshAt)
            {
                RefreshReturnOutcome();
                RefreshPreludeProgress();
                RefreshCheckpointControls();
                nextOutcomeRefreshAt = Time.unscaledTime + OutcomeRefreshInterval;
            }
            // Effective remaps notify us through the Input System, so avoid
            // per-frame binding-string lookups while the screen is idle.
            if (entryBindingHintDirty)
            {
                entryBindingHintDirty = false;
                RefreshEntryShortcutHint();
            }
            float aspect = (float)Screen.width / Mathf.Max(Screen.height, 1);
            if (!Mathf.Approximately(aspect, layoutAspect))
                LayoutForViewportAspect(aspect);

            bool entryRequested = SoundwellControlFrame.EnterRequested(
                SoundwellInputAdapter.InteractPressed(), false);
            // Pad A is both Entry/Interact and the UGUI Submit button.
            // When an entry setting has UGUI focus, Pad A is Submit and
            // must NEVER also load the trial. The dedicated F2 shortcut may
            // still enter directly regardless of the current selection.
            GameObject selected = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject : null;
            bool settingFocused = selected == sfxButton.gameObject ||
                selected == formationButton.gameObject ||
                selected == languageButton.gameObject || selected == prologueButton.gameObject ||
                selected == saveCheckpointButton.gameObject || selected == loadCheckpointButton.gameObject;
            if (entryRequested && (!settingFocused || SoundwellInputAdapter.EntryKeyboardShortcutPressed()))
                OpenPlayableTrial();
        }

        public void OpenPrelude()
        {
            if (checkpointLoading || currentPrelude != null || currentMemorial != null || trialLoad != null ||
                lastOpeningExitFrame == Time.frameCount)
                return;
            // Confirmation applies to this visit to the entry screen.
            confirmCheckpointOverwrite = confirmCheckpointLoad = checkpointSaved = checkpointReadFailed = false;
            // Disable the whole menu to prevent hidden Selectables from
            // retaining focus or intercepting pointer/gamepad Submit.
            entryCanvas.gameObject.SetActive(false);
            var scene = new GameObject("S00 Whitewater Bridge · playable opening");
            scene.transform.SetParent(transform, false);
            currentPrelude = scene.AddComponent<WhitewaterPrelude>();
            currentPrelude.Begin(() =>
            {
                // The M00 jump is earned by the actual S00 third bell. Leaving
                // early still returns to the normal entry screen.
                bool openMemorial = currentPrelude != null && currentPrelude.ThirdBellStarted;
                lastOpeningExitFrame = Time.frameCount;
                currentPrelude = null;
                if (openMemorial)
                {
                    var memorialObject = new GameObject("M00 Memorial Square · playable opening");
                    memorialObject.transform.SetParent(transform, false);
                    currentMemorial = memorialObject.AddComponent<M00MemorialSquare>();
                    currentMemorial.Begin(() =>
                    {
                        lastOpeningExitFrame = Time.frameCount;
                        currentMemorial = null;
                        entryCanvas.gameObject.SetActive(true);
                        if (EventSystem.current != null)
                            EventSystem.current.SetSelectedGameObject(prologueButton.gameObject);
                    });
                    return;
                }
                entryCanvas.gameObject.SetActive(true);
                SoundwellInputAdapter.SetEntryContext();
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(prologueButton.gameObject);
            });
        }

        public void RefreshReturnOutcome(bool force = false)
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
                return;

            int result = variables.GetInt(SoundwellTrial.EntryOutcomeKey);
            bool outcomeRecorded = variables.GetInt(SoundwellTrial.EntryOutcomeRecordedKey) != 0;
            bool cartKitRecovered = result == 2 &&
                variables.GetInt(SoundwellTrial.EntryCartKitKey) != 0;
            bool safeRouteMarked = cartKitRecovered &&
                variables.GetInt(SoundwellTrial.EntrySafeRouteKey) != 0;
            int contacts = Mathf.Clamp(
                variables.GetInt(SoundwellTrial.EntryBeastContactsKey), 0, 999);
            bool rescueSettled = SoundwellOrkParty.RescueSettled;
            // Read the banked progress from the actual ORK player group.
            // The result screen may outlive a replay or scene transition:
            // never invent a duplicate UI experience balance.
            string rescuerExperience = rescueSettled ? ReadRescuerExperience() : string.Empty;
            if (!force && rescueSettled == shownRescueSettled &&
                rescuerExperience == shownRescuerExperience &&
                result == shownOutcome && outcomeRecorded == shownOutcomeRecorded &&
                cartKitRecovered == shownCartKitRecovered &&
                safeRouteMarked == shownSafeRouteMarked && contacts == shownBeastContacts)
                return;

            shownOutcome = result;
            shownRescueSettled = rescueSettled;
            shownRescuerExperience = rescuerExperience;
            shownOutcomeRecorded = outcomeRecorded;
            shownCartKitRecovered = cartKitRecovered;
            shownSafeRouteMarked = safeRouteMarked;
            shownBeastContacts = contacts;
            string encounterReport = contacts > 0
                ? string.Format(SoundwellUiLocalization.Get("entry.contacts"), contacts)
                : result > 0 ? SoundwellUiLocalization.Get("entry.clean_evasion") : "";
            outcomeText.text = result == 2
                ? SoundwellUiLocalization.Get("entry.safe_corrected") +
                    (cartKitRecovered ? SoundwellUiLocalization.Get("entry.kit_recovered") : "") +
                    (safeRouteMarked ? SoundwellUiLocalization.Get("entry.route_posted") : "") +
                    encounterReport
                : result == 1 ? SoundwellUiLocalization.Get("entry.safe_pending") +
                    encounterReport
                : contacts > 0 || outcomeRecorded
                    ? string.Format(SoundwellUiLocalization.Get("entry.withdrawn"), contacts)
                    : "";
            if (rescueSettled)
            {
                outcomeText.text += (outcomeText.text.Length > 0 ? "\n" : "") +
                    string.Format(SoundwellUiLocalization.Get("entry.rescue_experience"),
                        SoundwellOrkParty.RescueExperience);
                if (rescuerExperience.Length > 0)
                    outcomeText.text += "\n" + rescuerExperience;
            }
        }

        private static string ReadRescuerExperience()
        {
            if (!ORK.Initialized || ORK.Game?.PlayerHandler == null)
                return string.Empty;
            Group group = ORK.Game.PlayerHandler.GetGroup(0);
            var members = group?.GetGroup();
            if (members == null || members.Count != 3)
                return string.Empty;

            var amount = new string[members.Count];
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i] == null)
                    return string.Empty;
                amount[i] = members[i].Status[SoundwellOrkParty.ExperienceStatus].GetValue()
                    .ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            }
            // "Rescuer" is already authored in the single saved EN/zh UI
            // StringTables. Numbers come directly from ORK, not an extra
            // wallet, Makinom flag, or PlayerPrefs shadow-save.
            return SoundwellUiLocalization.Get("battle.rescuer") +
                " 1-3: " + string.Join(" / ", amount);
        }

        public AsyncOperation OpenPlayableTrial()
        {
            if (checkpointLoading || trialLoad != null || currentPrelude != null || currentMemorial != null ||
                lastOpeningExitFrame == Time.frameCount)
                return trialLoad;
            RoleEncounterRequested = true;
            return OpenTrial();
        }

        // Explicit prototype entry retained for callers comparing the original
        // movement/formation experiment. The visible player entry uses roles.
        public AsyncOperation OpenTrial()
        {
            if (checkpointLoading || trialLoad != null || currentPrelude != null || currentMemorial != null ||
                lastOpeningExitFrame == Time.frameCount)
                return trialLoad;
            // Enter commits the scene transition. Disable the other focused
            // UGUI settings on the same frame, before the async scene unload,
            // so extra Submit/clicks cannot silently change preferences while
            // the player is already headed into the trial.
            entryButton.interactable = false;
            sfxButton.interactable = false;
            formationButton.interactable = false;
            languageButton.interactable = false;
            prologueButton.interactable = false;
            trialLoad = SceneManager.LoadSceneAsync(SceneName);
            return trialLoad;
        }
    }
}
