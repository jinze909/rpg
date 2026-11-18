using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    // Optional UGUI slice for M04: scene hosts decide when the dock
    // departure is ready, and persist only the finished answer.
    public sealed class M04XiaodouFarewellView : IDisposable
    {
        private readonly M04XiaodouFarewell encounter;
        private readonly GameObject canvasObject;
        private readonly RectTransform authoredView;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text dialogue;
        private readonly Button advance;
        private readonly Text advanceLabel;
        private readonly Button[] choices = new Button[2];
        private readonly Text[] choiceLabels = new Text[2];
        private Action<M04XiaodouFarewell> onFinished;
        private bool disposed;
        private bool delivered;

        public M04XiaodouFarewell Encounter => encounter;
        public Text Dialogue => dialogue;
        public Button AdvanceButton => advance;
        public Button GetChoiceButton(int index) => choices[index];

        public M04XiaodouFarewellView(Transform parent, bool promptedOnStage,
            Action<M04XiaodouFarewell> onFinished)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (onFinished == null) throw new ArgumentNullException(nameof(onFinished));
            this.onFinished = onFinished;
            encounter = new M04XiaodouFarewell(promptedOnStage);
            encounter.Begin();
            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("M04 Xiaodou farewell canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 127;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            Panel(canvas.transform, "M04 dock backdrop", new Vector2(950f, 530f),
                new Color(0.04f, 0.07f, 0.08f, 0.98f));
            var authored = new GameObject("M04 farewell panel", typeof(RectTransform));
            authored.transform.SetParent(canvas.transform, false);
            authoredView = (RectTransform)authored.transform;
            authoredView.anchorMin = authoredView.anchorMax = new Vector2(0.5f, 0.5f);
            authoredView.sizeDelta = new Vector2(960f, 540f);
            Panel(authoredView, "M04 farewell paper", new Vector2(920f, 515f),
                new Color(0.92f, 0.89f, 0.81f));

            heading = Label(authoredView, 24, new Vector2(0f, 208f),
                new Vector2(820f, 48f));
            dialogue = Label(authoredView, 20, new Vector2(0f, 53f),
                new Vector2(830f, 237f));
            dialogue.alignment = TextAnchor.MiddleLeft;
            for (int i = 0; i < choices.Length; i++)
            {
                int choice = i;
                choices[i] = NewButton(authoredView, "M04 Xiaodou choice " + i,
                    new Vector2(0f, -124f - i * 49f), new Vector2(760f, 43f),
                    out choiceLabels[i]);
                choices[i].onClick.AddListener(() => Choose(choice));
            }
            advance = NewButton(authoredView, "M04 Xiaodou advance",
                new Vector2(0f, -216f), new Vector2(460f, 43f),
                out advanceLabel);
            advance.onClick.AddListener(Advance);
            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            Refresh();
            SoundwellUiLocalization.Changed += Refresh;
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed || !(aspect > 0f) || float.IsNaN(aspect) ||
                float.IsInfinity(aspect)) return;
            float width = Mathf.Sqrt(960f * 540f * aspect);
            float height = Mathf.Sqrt(960f * 540f / aspect);
            float scale = Mathf.Min(1f, Mathf.Min(width / 960f, height / 540f));
            if (scale < 1f) scale *= 0.95f;
            authoredView.localScale = new Vector3(scale, scale, 1f);
        }

        public void Refresh()
        {
            if (disposed) return;
            if (canvasObject == null || heading == null)
            {
                Dispose();
                return;
            }
            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = dialogue.font = advanceLabel.font = font;
            heading.text = zh ? "出航前 · 小豆的糖人" : "BEFORE DEPARTURE · XIAODOU";
            dialogue.text = M04XiaodouFarewellCopy.Describe(encounter, zh);
            string[] options = M04XiaodouFarewellCopy.Choices(encounter, zh);
            for (int i = 0; i < choices.Length; i++)
            {
                choiceLabels[i].font = font;
                choiceLabels[i].text = i < options.Length ? options[i] : string.Empty;
                choices[i].gameObject.SetActive(!delivered && i < options.Length);
            }
            advanceLabel.text = encounter.Stage == M04FarewellStage.Greeting
                ? (zh ? "接过糖人，听小豆说" : "Take the sweet and listen")
                : (zh ? "向小豆道别" : "Say goodbye to Xiaodou");
            advance.gameObject.SetActive(!delivered &&
                (encounter.Stage == M04FarewellStage.Greeting ||
                 encounter.Stage == M04FarewellStage.HearingResponse));
            if (!delivered)
                SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform,
                    advance.gameObject.activeInHierarchy ? advance : choices[0]);
        }

        private void Choose(int index)
        {
            if (disposed || delivered || encounter.Stage != M04FarewellStage.ChoosingTrueLine ||
                index < 0 || index >= choices.Length) return;
            encounter.AnswerChild((M04XiaodouAnswer)index);
            Refresh();
        }

        private void Advance()
        {
            if (disposed || delivered) return;
            if (encounter.Stage == M04FarewellStage.Greeting)
                encounter.ReceiveCandy();
            else if (encounter.Stage == M04FarewellStage.HearingResponse)
            {
                encounter.SayGoodbye();
                delivered = true;
                Refresh();
                Action<M04XiaodouFarewell> callback = onFinished;
                onFinished = null;
                callback?.Invoke(encounter);
                return;
            }
            else return;
            Refresh();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            onFinished = null;
            SoundwellUiLocalization.Changed -= Refresh;
            foreach (Button option in choices)
                option.onClick.RemoveAllListeners();
            advance.onClick.RemoveAllListeners();
            if (canvasObject != null)
            {
                canvasObject.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(canvasObject);
                else UnityEngine.Object.DestroyImmediate(canvasObject);
            }
        }

        private static void Panel(Transform parent, string name, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            RectTransform rect = img.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
        }

        private static Text Label(Transform parent, int fontSize, Vector2 pos, Vector2 bounds)
        {
            Text label = SoundwellView.AddLabel(parent, "", fontSize,
                new Color(0.12f, 0.17f, 0.20f), pos, bounds);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 10;
            label.resizeTextMaxSize = fontSize;
            label.raycastTarget = false;
            return label;
        }

        private static Button NewButton(Transform parent, string name, Vector2 pos,
            Vector2 size, out Text label)
        {
            var go = new GameObject(name, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.color = new Color(0.82f, 0.83f, 0.77f);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            Button button = go.GetComponent<Button>();
            button.targetGraphic = img;
            label = Label(go.transform, 18, Vector2.zero, size);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(12f, 2f);
            label.rectTransform.offsetMax = new Vector2(-12f, -2f);
            return button;
        }
    }
}
