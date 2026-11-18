using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Optional playable M03 camp panel. Rendering reflects the exact
    /// state-machine gates. Host persists the completed choices through Makinom.
    /// </summary>
    public sealed class M03LuJunView : IDisposable
    {
        private readonly M03LuJunEncounter encounter;
        private readonly GameObject canvasObject;
        private readonly RectTransform authoredView;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text dialogue;
        private readonly Button[] choices = new Button[5];
        private readonly Text[] choiceLabels = new Text[5];
        private readonly Button advance;
        private readonly Text advanceLabel;
        private Action<M03LuJunEncounter> onFinished;
        private bool disposed;
        private bool delivered;

        public M03LuJunEncounter Encounter => encounter;
        public Text Dialogue => dialogue;
        public Button AdvanceButton => advance;

        private Button PreferredFocusTarget
        {
            get
            {
                if (disposed || delivered)
                    return null;
                if (advance.gameObject.activeInHierarchy)
                    return advance;
                foreach (Button option in choices)
                    if (option != null && option.gameObject.activeInHierarchy)
                        return option;
                return null;
            }
        }

        public M03LuJunView(Transform parent, M02BridgeEvidence evidence,
            bool officialRegistrationEffective, Action<M03LuJunEncounter> onFinished)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (onFinished == null) throw new ArgumentNullException(nameof(onFinished));
            encounter = new M03LuJunEncounter(evidence, officialRegistrationEffective);
            encounter.Begin();
            this.onFinished = onFinished;
            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("M03 Lu Jun camp canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 126;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            Panel(canvas.transform, "M03 shore backdrop", new Vector2(950f, 530f),
                new Color(0.04f, 0.07f, 0.08f, 0.98f));
            var authored = new GameObject("M03 authored panel", typeof(RectTransform));
            authored.transform.SetParent(canvas.transform, false);
            authoredView = (RectTransform)authored.transform;
            authoredView.anchorMin = authoredView.anchorMax = new Vector2(0.5f, 0.5f);
            authoredView.sizeDelta = new Vector2(960f, 540f);
            Panel(authoredView, "M03 shore paper", new Vector2(920f, 515f),
                new Color(0.92f, 0.89f, 0.81f));
            heading = Label(authoredView, 23, new Vector2(0f, 218f),
                new Vector2(835f, 48f));
            dialogue = Label(authoredView, 19, new Vector2(0f, 106f),
                new Vector2(835f, 151f));
            dialogue.alignment = TextAnchor.MiddleLeft;
            for (int i = 0; i < choices.Length; i++)
            {
                int index = i;
                choices[i] = NewButton(authoredView, "M03 Lu Jun option " + i,
                    new Vector2(0f, -29f - i * 42f), new Vector2(825f, 39f),
                    out choiceLabels[i]);
                choices[i].onClick.AddListener(() => Choose(index));
            }
            advance = NewButton(authoredView, "M03 Lu Jun advance",
                new Vector2(0f, -238f), new Vector2(405f, 38f), out advanceLabel);
            advance.onClick.AddListener(Advance);
            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            Refresh();
            SoundwellUiLocalization.Changed += Refresh;
        }

        public Button GetChoiceButton(int index)
        {
            if (index < 0 || index >= choices.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return choices[index];
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed || !(aspect > 0f) || float.IsNaN(aspect) || float.IsInfinity(aspect))
                return;
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
            bool chinese = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = dialogue.font = advanceLabel.font = font;
            heading.text = chinese ? "海边旧营 · 陆钧的名册" : "OLD GUARD CAMP · LU JUN'S ROLL";
            dialogue.text = M03LuJunCopy.Narration(encounter, chinese);
            string[] options = M03LuJunCopy.Choices(encounter, chinese);
            for (int i = 0; i < choices.Length; i++)
            {
                choiceLabels[i].font = font;
                choiceLabels[i].text = i < options.Length ? options[i] : string.Empty;
                bool eligible = i < options.Length && !delivered;
                if (encounter.Stage == M03LuJunStage.ChoosingVoyage)
                    eligible &= encounter.CanChoose((M03VoyageChoice)i);
                choices[i].gameObject.SetActive(eligible);
            }
            advanceLabel.text = chinese ? "听完点名，向陆钧走去" : "After the roll, approach Lu Jun";
            advance.gameObject.SetActive(!delivered &&
                encounter.Stage == M03LuJunStage.VisitingVeterans);
            if (!delivered)
                SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, PreferredFocusTarget);
        }

        private void Choose(int index)
        {
            if (disposed || delivered || index < 0 || index >= choices.Length)
                return;
            switch (encounter.Stage)
            {
                case M03LuJunStage.ChoosingOpening:
                    if (index >= 3) return;
                    encounter.Ask((M03LuJunOpening)index);
                    break;
                case M03LuJunStage.RespondingToConfession:
                    if (index >= 3) return;
                    encounter.Respond((M03ConfessionResponse)index);
                    break;
                case M03LuJunStage.ReplyingToSecrecyRequest:
                    if (index >= 3) return;
                    encounter.AnswerSecrecy((M03SecrecyAnswer)(index + 1));
                    break;
                case M03LuJunStage.ChoosingVoyage:
                    if (!encounter.CanChoose((M03VoyageChoice)index)) return;
                    encounter.ChooseVoyage((M03VoyageChoice)index);
                    delivered = true;
                    Refresh();
                    Action<M03LuJunEncounter> callback = onFinished;
                    onFinished = null;
                    callback?.Invoke(encounter);
                    return;
                default:
                    return;
            }
            Refresh();
            FocusFirstActionAfterStageChange();
        }

        private void Advance()
        {
            if (disposed || delivered || encounter.Stage != M03LuJunStage.VisitingVeterans)
                return;
            encounter.ObserveRollCall();
            Refresh();
            FocusFirstActionAfterStageChange();
        }

        // The same UGUI button index carries a different answer after each
        // dialogue stage. Retaining its focus can turn a second pad Submit
        // into an unintended ship booking rather than a deliberate choice.
        private void FocusFirstActionAfterStageChange()
        {
            EventSystem system = EventSystem.current;
            if (system == null || system.alreadySelecting)
                return;
            GameObject selected = system.currentSelectedGameObject;
            if (selected == null || !selected.transform.IsChildOf(canvasObject.transform))
                return;
            Button first = PreferredFocusTarget;
            if (first != null && first.gameObject.activeInHierarchy &&
                selected != first.gameObject)
                system.SetSelectedGameObject(first.gameObject);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            onFinished = null;
            SoundwellUiLocalization.Changed -= Refresh;
            foreach (Button button in choices)
                if (button != null)
                    button.onClick.RemoveAllListeners();
            if (advance != null)
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
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
        }

        private static Text Label(Transform parent, int fontSize,
            Vector2 position, Vector2 bounds)
        {
            Text t = SoundwellView.AddLabel(parent, string.Empty, fontSize,
                new Color(0.12f, 0.17f, 0.20f), position, bounds);
            t.raycastTarget = false;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 10;
            t.resizeTextMaxSize = fontSize;
            return t;
        }

        private static Button NewButton(Transform parent, string name,
            Vector2 pos, Vector2 size, out Text label)
        {
            var go = new GameObject(name, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = new Color(0.82f, 0.83f, 0.77f);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            label = Label(go.transform, 17, Vector2.zero, size);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(10f, 1f);
            label.rectTransform.offsetMax = new Vector2(-10f, -1f);
            return button;
        }
    }
}
