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
        private readonly RpgPixelArtManifest artManifest;
        private readonly Image documentIcon;
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
        public Image DocumentIcon => documentIcon;
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
            artManifest = RpgPixelArtManifest.LoadDefault();
            canvasObject = SoundwellUiTheme.CreateOverlayCanvas(
                parent, "M02 missing-return canvas", 124, out authoredView,
                new Color(0.04f, 0.06f, 0.08f, 0.98f));
            documentIcon = RpgStoryArtResolver.CreateOrUpdatePixelIcon(
                authoredView, "M02 document icon",
                new Vector2(-346f, 64f), new Vector2(112f, 112f),
                ResolveDocumentSprite());
            heading = SoundwellUiTheme.CreateLabel(authoredView, 25,
                new Vector2(0f, 210f), new Vector2(835f, 50f));
            narrative = SoundwellUiTheme.CreateLabel(authoredView, 20,
                new Vector2(72f, 52f), new Vector2(678f, 225f));
            narrative.alignment = TextAnchor.MiddleLeft;
            requestButton = SoundwellUiTheme.CreateButton(authoredView,
                "M02 ask for review", new Vector2(0f, -90f),
                new Vector2(790f, 49f), 17, out requestText);
            requestButton.onClick.AddListener(Ask);
            stampButton = SoundwellUiTheme.CreateButton(authoredView,
                "M02 stamp approved review", new Vector2(0f, -150f),
                new Vector2(790f, 49f), 17, out stampText);
            stampButton.onClick.AddListener(Stamp);
            leaveButton = SoundwellUiTheme.CreateButton(authoredView,
                "M02 return to harbor", new Vector2(0f, -211f),
                new Vector2(420f, 49f), 17, out leaveText);
            leaveButton.onClick.AddListener(Leave);
            LayoutForViewportAspect((float)Mathf.Max(Screen.width, 1) /
                Mathf.Max(Screen.height, 1));
            Refresh();
            SoundwellUiLocalization.Changed += Refresh;
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed)
                return;
            if (canvasObject == null || authoredView == null)
            {
                Dispose();
                return;
            }
            SoundwellUiTheme.ApplyViewportAspectScale(authoredView, aspect);
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
            if (documentIcon != null)
                RpgStoryArtResolver.ApplyPixelSprite(documentIcon, ResolveDocumentSprite());
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
            if (!delivered)
                SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform,
                    requestButton.gameObject.activeInHierarchy ? requestButton :
                    stampButton.gameObject.activeInHierarchy ? stampButton : leaveButton);
        }

        private Sprite ResolveDocumentSprite()
        {
            return RpgStoryArtResolver.ResolveM02DocumentSprite(
                review.Approved, artManifest);
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
            // Lock against reentrant UGUI submits before persisting through the
            // host. A failed write must not consume the stamped outcome or
            // irrevocably close the review desk.
            delivered = true;
            Refresh();
            try
            {
                review.Leave(finished);
                finished = null;
            }
            catch
            {
                delivered = false;
                Refresh();
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            finished = null;
            SoundwellUiLocalization.Changed -= Refresh;
            if (requestButton != null)
                requestButton.onClick.RemoveAllListeners();
            if (stampButton != null)
                stampButton.onClick.RemoveAllListeners();
            if (leaveButton != null)
                leaveButton.onClick.RemoveAllListeners();
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }
}
