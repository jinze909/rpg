using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Optional M02 approval interaction. Emits a final observed outcome; the
    /// host must persist any successful stamp before considering it delivered.
    /// </summary>
    public sealed class M02MissingReturnView : IDisposable
    {
        private readonly M02MissingReturnReview review;
        private readonly GameObject canvasObject;
        private readonly RectTransform authoredView;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text narrative;
        private readonly Button requestButton;
        private readonly Text requestText;
        private readonly Button stampButton;
        private readonly Text stampText;
        private readonly Button leaveButton;
        private readonly Text leaveText;
        private Action<M02MissingReturnOutcome> finished;
        private bool disposed;
        private bool delivered;

        public M02MissingReturnReview Review => review;
        public Text Narrative => narrative;
        public Button RequestButton => requestButton;
        public Button StampButton => stampButton;
        public Button LeaveButton => leaveButton;

        public M02MissingReturnView(Transform parent, M01PensionChoice priorChoice,
            M02BridgeEvidence recordedEvidence, bool assistanceAccepted,
            Action<M02MissingReturnOutcome> finished)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            if (finished == null)
                throw new ArgumentNullException(nameof(finished));

            review = new M02MissingReturnReview(priorChoice, recordedEvidence,
                assistanceAccepted);
            review.Begin();
            this.finished = finished;
            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("M02 missing-return canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 124;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            Panel(canvas.transform, "M02 dim backdrop", new Vector2(950f, 530f),
                new Color(0.04f, 0.06f, 0.08f, 0.98f));
            var body = new GameObject("M02 review authored view", typeof(RectTransform));
            body.transform.SetParent(canvas.transform, false);
            authoredView = (RectTransform)body.transform;
            authoredView.anchorMin = authoredView.anchorMax = new Vector2(0.5f, 0.5f);
            authoredView.sizeDelta = new Vector2(960f, 540f);
            Panel(authoredView, "M02 review paper", new Vector2(920f, 515f),
                new Color(0.92f, 0.89f, 0.81f));
            heading = Label(authoredView, 25, new Vector2(0f, 210f),
                new Vector2(835f, 50f));
            narrative = Label(authoredView, 20, new Vector2(0f, 52f),
                new Vector2(825f, 225f));
            narrative.alignment = TextAnchor.MiddleLeft;
            requestButton = Button(authoredView, "M02 ask for review",
                new Vector2(0f, -90f), new Vector2(790f, 49f), out requestText);
            requestButton.onClick.AddListener(Ask);
            stampButton = Button(authoredView, "M02 stamp approved review",
                new Vector2(0f, -150f), new Vector2(790f, 49f), out stampText);
            stampButton.onClick.AddListener(Stamp);
            leaveButton = Button(authoredView, "M02 return to harbor",
                new Vector2(0f, -211f), new Vector2(420f, 49f), out leaveText);
            leaveButton.onClick.AddListener(Leave);
            LayoutForViewportAspect((float)Mathf.Max(Screen.width, 1) /
                Mathf.Max(Screen.height, 1));
            Refresh();
            SoundwellUiLocalization.Changed += Refresh;
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
            if (canvasObject == null || heading == null)
            {
                Dispose();
                return;
            }
            bool chinese = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = narrative.font = requestText.font =
                stampText.font = leaveText.font = font;
            heading.text = chinese ? "港务署 · 失踪归还审定" :
                "HARBOR OFFICE · MISSING-RETURN REVIEW";
            narrative.text = M02MissingReturnCopy.Description(review, chinese);
            requestText.text = chinese ? "正式提出申请" : "Request official review";
            stampText.text = chinese ? "请韩峤批注并盖印" : "Have Han Qiao sign and stamp";
            leaveText.text = chinese ? "离开审核窗口" : "Leave the review desk";
            requestButton.gameObject.SetActive(!delivered &&
                review.CanRequest && !review.Requested);
            stampButton.gameObject.SetActive(!delivered && review.CanStamp);
            leaveButton.gameObject.SetActive(!delivered);
            SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform,
                requestButton.gameObject.activeInHierarchy ? requestButton :
                stampButton.gameObject.activeInHierarchy ? stampButton : leaveButton);
        }

        private void Ask()
        {
            if (disposed || delivered || !review.CanRequest || review.Requested)
                return;
            review.Request();
            Refresh();
        }

        private void Stamp()
        {
            if (disposed || delivered || !review.CanStamp)
                return;
            review.Stamp();
            Refresh();
        }

        private void Leave()
        {
            if (disposed || delivered)
                return;
            delivered = true;
            M02MissingReturnOutcome outcome = review.Leave();
            Action<M02MissingReturnOutcome> callback = finished;
            finished = null;
            callback?.Invoke(outcome);
            Refresh();
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            finished = null;
            SoundwellUiLocalization.Changed -= Refresh;
            requestButton.onClick.RemoveAllListeners();
            stampButton.onClick.RemoveAllListeners();
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

        private static Text Label(Transform parent, int size,
            Vector2 pos, Vector2 bounds)
        {
            Text text = SoundwellView.AddLabel(parent, string.Empty, size,
                new Color(0.12f, 0.17f, 0.20f), pos, bounds);
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 10;
            text.resizeTextMaxSize = size;
            return text;
        }

        private static Button Button(Transform parent, string name,
            Vector2 pos, Vector2 size, out Text label)
        {
            var go = new GameObject(name, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.color = new Color(0.82f, 0.83f, 0.77f);
            RectTransform rect = img.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            Button button = go.GetComponent<Button>();
            button.targetGraphic = img;
            label = Label(go.transform, 17, Vector2.zero, size);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(10f, 2f);
            label.rectTransform.offsetMax = new Vector2(-10f, -2f);
            return button;
        }
    }
}
