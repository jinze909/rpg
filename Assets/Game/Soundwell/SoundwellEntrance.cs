using GamingIsLove.Makinom;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
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
        private float layoutAspect = -1f;
        private int shownOutcome = -1;

        public int LastReturnOutcome => shownOutcome;

        private void Awake()
        {
            var canvas = new GameObject("Soundwell entry prompt", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
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
            titleText.resizeTextForBestFit = true;
            titleText.resizeTextMinSize = 18;
            titleText.resizeTextMaxSize = 32;
            actionText.resizeTextForBestFit = true;
            actionText.resizeTextMinSize = 15;
            actionText.resizeTextMaxSize = 22;
            footerText.resizeTextForBestFit = true;
            footerText.resizeTextMinSize = 11;
            footerText.resizeTextMaxSize = 15;
            LayoutForViewportAspect((float)Screen.width / Mathf.Max(Screen.height, 1));

            if (EventSystem.current == null)
            {
                var events = new GameObject("Soundwell entry EventSystem",
                    typeof(EventSystem), typeof(StandaloneInputModule));
                events.transform.SetParent(transform, false);
            }
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
            outcomeText.rectTransform.sizeDelta = new Vector2(compact ? 350f : 600f, 64f);
            outcomeText.rectTransform.anchoredPosition = new Vector2(0f, compact ? -142f : -126f);
            outcomeText.fontSize = compact ? 15 : 17;
        }

        private void Update()
        {
            // ORK may initialize after this scene's Awake/Start. Recheck the
            // same Makinom source without creating a parallel summary flag.
            RefreshReturnOutcome();
            float aspect = (float)Screen.width / Mathf.Max(Screen.height, 1);
            if (!Mathf.Approximately(aspect, layoutAspect))
                LayoutForViewportAspect(aspect);

            if (SoundwellControlFrame.EnterRequested(
                Input.GetKeyDown(KeyCode.F2), Input.GetKeyDown(KeyCode.JoystickButton0)))
                OpenTrial();
        }

        public void RefreshReturnOutcome()
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
                return;

            int result = variables.GetInt(SoundwellTrial.EntryOutcomeKey);
            if (result == shownOutcome)
                return;

            shownOutcome = result;
            outcomeText.text = result == 2
                ? "SURVEYOR SAFE / FALSE TRAIL MARKER REMOVED"
                : result == 1 ? "SURVEYOR SAFE / ROUTE MARKER STILL NEEDS CORRECTION"
                : "";
        }

        public AsyncOperation OpenTrial()
        {
            if (trialLoad != null)
                return trialLoad;
            entryButton.interactable = false;
            trialLoad = SceneManager.LoadSceneAsync(SceneName);
            return trialLoad;
        }
    }
}
