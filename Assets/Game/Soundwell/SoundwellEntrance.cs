using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    public sealed class SoundwellEntrance : MonoBehaviour
    {
        public const string SceneName = "SoundwellTrial";

        private void Awake()
        {
            var canvas = new GameObject("Soundwell entry prompt", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var label = SoundwellView.AddLabel(canvas.transform, "F2  |  Enter the Soundwell trial", 28,
                new Color(0.86f, 0.93f, 0.97f), Vector2.zero, new Vector2(640, 72));
            label.alignment = TextAnchor.MiddleCenter;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F2))
                OpenTrial();
        }

        public AsyncOperation OpenTrial(LoadSceneMode mode = LoadSceneMode.Single) =>
            SceneManager.LoadSceneAsync(SceneName, mode);
    }
}
