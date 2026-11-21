using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Bottom-anchored UGUI for the ST-01 Jingwan theater reckoning.
    /// This screen only presents choices when their corresponding physical
    /// scene prerequisites are verified; it never creates backstage visits,
    /// pre-performance rehearsals, displaced workers or invitations.
    /// </summary>
    public sealed class ST01TheaterChoiceView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text body;
        private readonly Button[] buttons = new Button[3];
        private readonly Text[] labels = new Text[3];
        private readonly ST01DepartureChoice? departure;
        private readonly ST01TheaterChoice? pending;
        private readonly Func<ST01TheaterChoice, bool> canCommit;
        private Action<ST01TheaterChoice> onSubmit;
        private bool disposed;
        private bool submitting;

        public RectTransform Panel => panel;
        public Text Heading => heading;
        public Text Body => body;
        public Button RewriteButton => buttons[0];
        public Button ContractButton => buttons[1];
        public Button BoycottButton => buttons[2];
        public bool IsDisposed => disposed;

        public ST01TheaterChoiceView(
            Transform parent,
            ST01DepartureChoice? departure,
            ST01TheaterChoice? pending,
            Func<ST01TheaterChoice, bool> canCommit,
            Action<ST01TheaterChoice> onSubmit)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.canCommit = canCommit ?? throw new ArgumentNullException(nameof(canCommit));
            this.onSubmit = onSubmit ?? throw new ArgumentNullException(nameof(onSubmit));
            this.departure = departure;
            this.pending = pending;

            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("ST01 Jingwan theater canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 145;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var holder = new GameObject("ST01 Jingwan theater bottom panel",
                typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(904f, 256f);
            panel.anchoredPosition = new Vector2(0f, 138f);

            SoundwellUiTheme.CreatePanel(panel, "ST01 theater border",
                new Vector2(904f, 256f), new Color(0.64f, 0.56f, 0.42f, 1f));
            SoundwellUiTheme.CreatePanel(panel, "ST01 theater backing",
                new Vector2(900f, 252f), new Color(0.06f, 0.09f, 0.11f, 0.97f));
            Color ink = new Color(0.93f, 0.89f, 0.81f, 1f);
            heading = SoundwellUiTheme.CreateLabel(panel, 19,
                new Vector2(0f, 102f), new Vector2(842f, 28f), ink);
            heading.alignment = TextAnchor.MiddleLeft;
            body = SoundwellUiTheme.CreateLabel(panel, 14,
                new Vector2(0f, 37f), new Vector2(842f, 94f), ink);
            body.alignment = TextAnchor.UpperLeft;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;

            var positions = new[]
            {
                new Vector2(-286f, -82f),
                new Vector2(0f, -82f),
                new Vector2(286f, -82f)
            };
            for (int i = 0; i < buttons.Length; i++)
            {
                int slot = i;
                buttons[i] = SoundwellUiTheme.CreateButton(
                    panel, "ST01 theater decision " + i, positions[i],
                    new Vector2(276f, 38f), 13, out labels[i]);
                buttons[i].onClick.AddListener(() => OnClick(slot));
            }

            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            Refresh();
            SoundwellUiLocalization.Changed += Refresh;
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed) return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }
            SoundwellUiTheme.ApplyViewportAspectScale(panel, aspect);
        }

        public void Refresh()
        {
            if (disposed) return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }
            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = body.font = font;
            for (int i = 0; i < labels.Length; i++)
                labels[i].font = font;

            heading.text = zh ? "ST-01 · 镜湾《潮路》首演抉择"
                : "ST-01 · JINGWAN THEATER — THE TIDE ROAD";
            string past = !departure.HasValue
                ? (zh ? "这是你与柳翎第一次相遇。"
                    : "You first meet Liu Ling here.")
                : departure.Value == ST01DepartureChoice.TalkTogether
                    ? (zh ? "白苇离团时，她曾与母亲和阿芍摊开谈。"
                        : "At White Reed she spoke openly with her mother and A Shao.")
                    : (zh ? "白苇假绑架留下的订金与误工仍未抹平。"
                        : "The staged abduction left real deposit and crew costs.");

            body.text = (zh
                ? "旧船工抱着拆下的门牌站在台阶下。你读过原稿和删改稿，也看过彩排；改戏有撤资风险，保合同无法抹去美化，抵制不自动换来和解。"
                : "Displaced dockworkers carry their old doorplates. You have read both scripts and witnessed rehearsal. Rewriting risks the sponsor; keeping the contract does not undo its false account; boycott does not guarantee reconciliation.")
                + "  " + past;

            labels[0].text = zh ? "改戏并承担撤资风险" : "Rewrite and face funding risk";
            labels[1].text = zh ? "保合同、安排住户发声" : "Keep contract; hear residents";
            labels[2].text = zh ? "公开抵制此版演出" : "Publicly boycott this version";

            for (int i = 0; i < buttons.Length; i++)
            {
                var choice = (ST01TheaterChoice)(i + 1);
                bool allowed = (!pending.HasValue || pending.Value == choice) &&
                    !submitting && canCommit(choice);
                buttons[i].gameObject.SetActive(true);
                buttons[i].interactable = allowed;
            }
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].interactable)
                {
                    SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, buttons[i]);
                    break;
                }
            }
        }

        private void OnClick(int index)
        {
            if (disposed || submitting || CanvasDestroyed() ||
                index < 0 || index >= buttons.Length ||
                !buttons[index].gameObject.activeSelf || !buttons[index].interactable)
                return;
            var choice = (ST01TheaterChoice)(index + 1);
            submitting = true;
            try
            {
                onSubmit?.Invoke(choice);
            }
            finally
            {
                submitting = false;
                if (!disposed) Refresh();
            }
        }

        private bool CanvasDestroyed()
        {
            if (canvasObject == null || panel == null ||
                heading == null || body == null)
                return true;
            for (int i = 0; i < buttons.Length; i++)
                if (buttons[i] == null || labels[i] == null)
                    return true;
            return false;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            onSubmit = null;
            SoundwellUiLocalization.Changed -= Refresh;
            for (int i = 0; i < buttons.Length; i++)
                if (buttons[i] != null)
                    buttons[i].onClick.RemoveAllListeners();
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }
}
