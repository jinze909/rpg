using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    public sealed class SoundwellEntrance : MonoBehaviour
    {
        public const string SceneName = "SoundwellTrial";
        public const string BootstrapSceneName = "Bootstrap";

        private AsyncOperation trialLoad;

        private void Awake()
        {
            var canvas = new GameObject("Soundwell entry prompt", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 540);

            SoundwellView.AddLabel(canvas.transform, "SOUNDWELL SIGNAL TRIAL", 32,
                new Color(0.86f, 0.93f, 0.97f), new Vector2(0f, 23f), new Vector2(520f, 48f));
            SoundwellView.AddLabel(canvas.transform, "[F2] Enter the trial", 22,
                new Color(1f, 0.72f, 0.33f), new Vector2(0f, -22f), new Vector2(420f, 34f));
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F2))
                OpenTrial();
        }

        public AsyncOperation OpenTrial() => trialLoad ??= SceneManager.LoadSceneAsync(SceneName);
    }
}
