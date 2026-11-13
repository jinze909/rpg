using GamingIsLove.Makinom;
using GamingIsLove.ORKFramework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    public sealed class SoundwellEntrance : MonoBehaviour
    {
        public const string SceneName = "SoundwellTrial";
        public const string BootstrapSceneName = "Bootstrap";

        private AsyncOperation trialLoad;
        private Button entryButton;
        private RectTransform entryRect;
        private Text titleText;
        private Text actionText;
        private Text footerText;
        private Text outcomeText;
        private Button sfxButton;
        private Button prologueButton;
        private RectTransform prologueRect;
        private Canvas entryCanvas;
        private WhitewaterPrelude currentPrelude;
        // A UGUI Submit that closes S00 can be the same gamepad-A event
        // read by the entry menu on that frame. Do not treat it as a second
        // request to load the combat trial or reopen the prologue.
        private int lastPreludeExitFrame = -1;
        private Button languageButton;
        private RectTransform languageRect;
        private Text languageText;
        private RectTransform sfxRect;
        private Text sfxText;
        private Font englishFont;
        private float layoutAspect = -1f;
        private string shownEntryShortcut;
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

        private void Awake()
        {
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
            entryButton.onClick.AddListener(() => OpenTrial());

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
            SoundwellView.AddLabel(prologueRect, "S00 · 白水渡", 17,
                new Color(0.98f, 0.80f, 0.52f), Vector2.zero, new Vector2(242f, 33f));
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
        }

        private void OnDisable()
        {
            SoundwellUiLocalization.Changed -= RefreshLocalizedPresentation;
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
            // Localized entry text is authored for the default F2 binding.
            // Replace only its shortcut token, leaving the translated copy and
            // the independent gamepad Submit hint under StringTable authority.
            shownEntryShortcut = null;
            RefreshEntryShortcutHint();
            footerText.text = SoundwellUiLocalization.Get("entry.footer");
            RefreshReturnOutcome(true);
            RefreshSfxText();
            languageText.font = font;
            languageText.text = SoundwellUiLocalization.Get("action.language");
            Text preludeText = prologueButton.GetComponentInChildren<Text>();
            if (preludeText != null)
            {
                preludeText.font = font;
                preludeText.text = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese
                    ? "S00 · 白水渡" : "S00 · Whitewater Bridge";
            }
        }

        private void RefreshEntryShortcutHint()
        {
            if (actionText == null)
                return;
            string shortcut = SoundwellInputAdapter.EntryKeyboardShortcutLabel();
            if (shortcut == shownEntryShortcut)
                return;
            shownEntryShortcut = shortcut;
            string token = string.IsNullOrEmpty(shortcut) ? "[" : "[" + shortcut + " / ";
            actionText.text = SoundwellUiLocalization.Get("entry.action")
                .Replace("[F2 / ", token);
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
                selectOnDown = sfxButton
            };
            languageButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = sfxButton,
                selectOnDown = prologueButton
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
                selectOnDown = languageButton
            };
            sfxButton.transition = Selectable.Transition.ColorTint;
            languageButton.transition = Selectable.Transition.ColorTint;
            sfxButton.colors = entryButton.colors;
            languageButton.colors = entryButton.colors;
            prologueButton.colors = entryButton.colors;
        }

        private void ToggleSfx()
        {
            AudioMixer mixer = Resources.Load<AudioMixer>(SoundwellAudioBus.MixerResource);
            if (mixer == null || !SoundwellAudioBus.SetMuted(mixer, !SoundwellAudioBus.IsMuted))
                throw new System.InvalidOperationException(
                    "The saved Soundwell SFX volume control is unavailable.");
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
            sfxRect.sizeDelta = new Vector2(compact ? 300f : 320f, 40f);
            sfxText.rectTransform.sizeDelta = new Vector2(compact ? 294f : 314f, 36f);
            languageRect.anchoredPosition = new Vector2(0f, compact ? 181f : 160f);
            languageRect.sizeDelta = new Vector2(compact ? 186f : 204f, 40f);
            prologueRect.anchoredPosition = new Vector2(0f, compact ? 140f : 119f);
            prologueRect.sizeDelta = new Vector2(compact ? 220f : 254f, compact ? 32f : 36f);
            languageText.rectTransform.sizeDelta = new Vector2(compact ? 180f : 198f, 36f);
        }

        private void Update()
        {
            // ORK may initialize after this scene's Awake/Start. Recheck the
            // same Makinom source without creating a parallel summary flag.
            RefreshReturnOutcome();
            // Remaps may occur while the Entry screen stays alive. Refresh
            // only when the effective keyboard shortcut changes.
            RefreshEntryShortcutHint();
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
                selected == languageButton.gameObject || selected == prologueButton.gameObject;
            if (entryRequested && (!settingFocused || SoundwellInputAdapter.EntryKeyboardShortcutPressed()))
                OpenTrial();
        }

        public void OpenPrelude()
        {
            if (currentPrelude != null || trialLoad != null ||
                lastPreludeExitFrame == Time.frameCount)
                return;
            entryCanvas.enabled = false;
            var scene = new GameObject("S00 Whitewater Bridge · playable opening");
            scene.transform.SetParent(transform, false);
            currentPrelude = scene.AddComponent<WhitewaterPrelude>();
            currentPrelude.Begin(() =>
            {
                lastPreludeExitFrame = Time.frameCount;
                currentPrelude = null;
                entryCanvas.enabled = true;
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

        public AsyncOperation OpenTrial()
        {
            if (trialLoad != null || currentPrelude != null ||
                lastPreludeExitFrame == Time.frameCount)
                return trialLoad;
            // Enter commits the scene transition. Disable the other focused
            // UGUI settings on the same frame, before the async scene unload,
            // so extra Submit/clicks cannot silently change preferences while
            // the player is already headed into the trial.
            entryButton.interactable = false;
            sfxButton.interactable = false;
            languageButton.interactable = false;
            prologueButton.interactable = false;
            trialLoad = SceneManager.LoadSceneAsync(SceneName);
            return trialLoad;
        }
    }
}
