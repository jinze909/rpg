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

        private void Awake()
        {
            var canvas = new GameObject("Soundwell entry prompt", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 540);
            canvas.gameObject.AddComponent<GraphicRaycaster>();

            SoundwellView.AddLabel(canvas.transform, "SOUNDWELL SIGNAL TRIAL", 32,
                new Color(0.86f, 0.93f, 0.97f), new Vector2(0f, 70f), new Vector2(520f, 48f));

            var buttonObject = new GameObject("Enter Soundwell button",
                typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -8f);
            rect.sizeDelta = new Vector2(460f, 62f);

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
            SoundwellView.AddLabel(rect, "[F2 / Pad A] Enter the trial", 22,
                new Color(1f, 0.72f, 0.33f), Vector2.zero, new Vector2(440f, 50f));
            entryButton.onClick.AddListener(() => OpenTrial());

            SoundwellView.AddLabel(canvas.transform, "CLICK / ENTER / PAD A TO BEGIN", 15,
                new Color(0.65f, 0.78f, 0.83f), new Vector2(0f, -65f), new Vector2(440f, 28f));
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
        }

        private void Update()
        {
            if (SoundwellControlFrame.EnterRequested(
                Input.GetKeyDown(KeyCode.F2), Input.GetKeyDown(KeyCode.JoystickButton0)))
                OpenTrial();
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
