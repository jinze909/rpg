using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// UGUI conversation over M02's harbor-desk choices. It emits one
    /// observed result to its host, which is responsible for Makinom saves.
    /// </summary>
    public sealed class M02HarborDeskView : IDisposable
    {
        private readonly M02HarborDesk desk;
        private readonly GameObject canvasObject;
        private readonly RectTransform authoredView;
        private readonly Text title;
        private readonly Text dialogue;
        private readonly Text[] actionLabels = new Text[4];
        private readonly Button[] actionButtons = new Button[4];
        private readonly Button leaveButton;
        private readonly Text leaveLabel;
        private readonly Font latinFont;
        private Action<M02HarborDeskOutcome> onFinished;
        private bool disposed;
        private bool delivered;
        private string lastResult;

        public M02HarborDesk Desk => desk;
        public Text Dialogue => dialogue;
        public Button LeaveButton => leaveButton;

        public M02HarborDeskView(Transform parent, M01PensionChoice pensionChoice,
            Action<M02HarborDeskOutcome> onFinished)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            if (onFinished == null)
                throw new ArgumentNullException(nameof(onFinished));
            desk = new M02HarborDesk(pensionChoice);
            desk.Begin();
            this.onFinished = onFinished;
            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            canvasObject = new GameObject("M02 harbor desk canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 123;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            Panel(canvas.transform, "M02 harbor backdrop", new Vector2(950f, 530f),
                new Color(0.05f, 0.07f, 0.09f, 0.98f));
            var panel = new GameObject("M02 harbor authored view", typeof(RectTransform));
            panel.transform.SetParent(canvas.transform, false);
            authoredView = (RectTransform)panel.transform;
            authoredView.anchorMin = authoredView.anchorMax = new Vector2(0.5f, 0.5f);
            authoredView.sizeDelta = new Vector2(960f, 540f);
            Panel(authoredView, "M02 paper", new Vector2(920f, 515f),
                new Color(0.92f, 0.89f, 0.81f));

            title = Label(authoredView, 24, new Vector2(0f, 216f),
                new Vector2(840f, 45f));
            dialogue = Label(authoredView, 18, new Vector2(0f, 74f),
                new Vector2(840f, 206f));
            dialogue.alignment = TextAnchor.MiddleLeft;
            for (int i = 0; i < actionButtons.Length; i++)
            {
                int selected = i;
                actionButtons[i] = NewButton(authoredView, "M02 office action " + i,
                    new Vector2(0f, -58f - i * 43f), new Vector2(825f, 39f),
                    out actionLabels[i]);
                actionButtons[i].onClick.AddListener(() => Choose((M02HarborDeskAction)selected));
            }
            leaveButton = NewButton(authoredView, "M02 harbor leave",
                new Vector2(0f, -230f), new Vector2(400f, 40f), out leaveLabel);
            leaveButton.onClick.AddListener(Leave);

            LayoutForViewportAspect((float)Mathf.Max(Screen.width, 1) /
                Mathf.Max(Screen.height, 1));
            Refresh();
            SoundwellUiLocalization.Changed += Refresh;
        }

        public Button GetActionButton(int index)
        {
            if (index < 0 || index >= actionButtons.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return actionButtons[index];
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed || !(aspect > 0f) || float.IsNaN(aspect) || float.IsInfinity(aspect))
                return;
            float width = Mathf.Sqrt(960f * 540f * aspect);
            float height = Mathf.Sqrt(960f * 540f / aspect);
            float scale = Mathf.Min(1f, Mathf.Min(width / 960f, height / 540f));
            if (scale < 1f)
                scale *= 0.95f;
            authoredView.localScale = new Vector3(scale, scale, 1f);
        }

        public void Refresh()
        {
            if (disposed)
                return;
            if (canvasObject == null || title == null)
            {
                Dispose();
                return;
            }
            bool chinese = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            title.font = dialogue.font = leaveLabel.font = font;
            title.text = chinese ? "临潮港务署 · 船坞与旧账" : "HARBOR OFFICE · SHIPS AND DEBTS";
            string intro = M02HarborDeskDialogue.Introduction(desk, chinese);
            dialogue.text = intro + (desk.LastAction.HasValue
                ? "\n\n" + M02HarborDeskDialogue.Result(desk.LastAction.Value, chinese)
                : string.Empty);
            string[] en = { "Present living-registration papers", "Ask about Qi Que's ship", "Ask about Lu Jun's escort", "Help check the transfer list" };
            string[] zh = { "递交活人户籍呈文", "询问祁雀的船贷", "询问陆钧的护卫", "答应协理移交名册" };
            for (int i = 0; i < actionButtons.Length; i++)
            {
                actionLabels[i].font = font;
                actionLabels[i].text = chinese ? zh[i] : en[i];
                actionButtons[i].gameObject.SetActive(!delivered &&
                    (i != 0 || desk.CanPresentLivingPapers));
            }
            leaveLabel.text = chinese ? "离开港务署" : "Leave the harbor office";
            leaveButton.gameObject.SetActive(!delivered);
            SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform,
                actionButtons[0].gameObject.activeInHierarchy ? actionButtons[0] : leaveButton);
        }

        private void Choose(M02HarborDeskAction action)
        {
            if (disposed || delivered)
                return;
            desk.Act(action);
            Refresh();
        }

        private void Leave()
        {
            if (disposed || delivered)
                return;
            delivered = true;
            M02HarborDeskOutcome result = desk.Finish();
            Action<M02HarborDeskOutcome> callback = onFinished;
            onFinished = null;
            callback?.Invoke(result);
            Refresh();
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            onFinished = null;
            SoundwellUiLocalization.Changed -= Refresh;
            foreach (Button b in actionButtons)
                if (b != null)
                    b.onClick.RemoveAllListeners();
            if (leaveButton != null)
                leaveButton.onClick.RemoveAllListeners();
            if (canvasObject != null)
            {
                canvasObject.SetActive(false);
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(canvasObject);
                else
                    UnityEngine.Object.DestroyImmediate(canvasObject);
            }
        }

        private static void Panel(Transform parent, string name,
            Vector2 size, Color color)
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

        private static Text Label(Transform parent, int size,
            Vector2 position, Vector2 bounds)
        {
            Text text = SoundwellView.AddLabel(parent, string.Empty, size,
                new Color(0.12f, 0.17f, 0.2f), position, bounds);
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 10;
            text.resizeTextMaxSize = size;
            return text;
        }

        private static Button NewButton(Transform parent, string name,
            Vector2 position, Vector2 size, out Text label)
        {
            var go = new GameObject(name, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = new Color(0.82f, 0.83f, 0.77f);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            label = Label(go.transform, 16, Vector2.zero, size);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(10f, 2f);
            label.rectTransform.offsetMax = new Vector2(-10f, -2f);
            return button;
        }
    }
}
