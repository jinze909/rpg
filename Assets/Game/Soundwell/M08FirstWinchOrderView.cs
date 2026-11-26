using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// M08's bottom-safe, localized first commission panel. Interacts with
    /// real winch-order state; the host confirms completion and handles Makinom
    /// chapter/save facts. A failed host callback keeps delivery retryable.
    /// </summary>
    public sealed class M08FirstWinchOrderView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly Text heading;
        private readonly Text body;
        private readonly Button[] choices = new Button[3];
        private readonly Text[] labels = new Text[3];
        private readonly Button finishButton;
        private readonly Text finishLabel;
        private readonly Font latinFont;
        private readonly M08FirstWinchOrderSession session;
        private Action<M08FirstWinchOrderSession> onCompleted;
        private bool delivered;
        private bool disposed;

        public M08FirstWinchOrderSession Session => session;
        public RectTransform Panel => panel;
        public Text Body => body;
        public Button PrimaryButton => choices[0];
        public Button SecondaryButton => choices[1];
        public Button ThirdButton => choices[2];
        public Button FinishButton => finishButton;
        public bool IsDisposed => disposed || CanvasDestroyed();

        public M08FirstWinchOrderView(
            Transform parent,
            M08FirstWinchOrderSession session,
            Action<M08FirstWinchOrderSession> onCompleted)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.onCompleted = onCompleted ?? throw new ArgumentNullException(nameof(onCompleted));

            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("M08 first winch order canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 137;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var holder = new GameObject("M08 winch order bottom panel", typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(904f, 248f);
            panel.anchoredPosition = new Vector2(0f, 134f);
            SoundwellUiTheme.CreatePanel(panel, "M08 winch border",
                new Vector2(904f, 248f), new Color(0.64f, 0.56f, 0.42f, 1f));
            SoundwellUiTheme.CreatePanel(panel, "M08 winch backing",
                new Vector2(900f, 244f), new Color(0.06f, 0.09f, 0.11f, 0.97f));
            var ink = new Color(0.93f, 0.89f, 0.81f, 1f);
            heading = SoundwellUiTheme.CreateLabel(panel, 19,
                new Vector2(0f, 98f), new Vector2(842f, 28f), ink);
            heading.alignment = TextAnchor.MiddleLeft;
            body = SoundwellUiTheme.CreateLabel(panel, 15,
                new Vector2(0f, 35f), new Vector2(842f, 82f), ink);
            body.alignment = TextAnchor.UpperLeft;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;

            var positions = new[] {
                new Vector2(-280f, -72f),
                new Vector2(0f, -72f),
                new Vector2(280f, -72f)
            };
            for (int i = 0; i < choices.Length; i++)
            {
                int index = i;
                choices[i] = SoundwellUiTheme.CreateButton(
                    panel, "M08 winch choice " + i, positions[i],
                    new Vector2(270f, 36f), 14, out labels[i]);
                choices[i].onClick.AddListener(() => Choose(index));
            }
            finishButton = SoundwellUiTheme.CreateButton(
                panel, "M08 winch finish",
                new Vector2(0f, -110f), new Vector2(360f, 34f),
                15, out finishLabel);
            finishButton.onClick.AddListener(Finish);

            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            SoundwellUiLocalization.Changed += Refresh;
            Refresh();
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed || IsDestroyedExternally()) return;
            if (float.IsNaN(aspect) || float.IsInfinity(aspect) || aspect <= 0f)
                return;
            SoundwellUiTheme.ApplyViewportAspectScale(panel, aspect);
        }

        public void Refresh()
        {
            if (disposed || IsDestroyedExternally()) return;
            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = body.font = finishLabel.font = font;
            for (int i = 0; i < choices.Length; i++)
            {
                labels[i].font = font;
                choices[i].interactable = false;
                choices[i].gameObject.SetActive(false);
            }
            finishButton.interactable = false;
            finishButton.gameObject.SetActive(false);
            heading.text = zh ? "M08 · 第一张订单「债主」" : "M08 · FIRST COMMISSION: THE COLLECTOR";

            switch (session.Stage)
            {
                case M08WinchOrderStage.SourceParts:
                    body.text = zh
                        ? "旧维修铺要靠一台小绞盘赚第一笔工坊押金。先在旧货摊找齿轮，再去船坞找钢索；两样都要到手。"
                        : "The old workshop needs a small winch to earn its first deposit. Source a gear at the scrap stall and cable at the dock.";
                    if (!session.HasScrapGear)
                        Show(0, zh ? "旧货摊找齿轮" : "Find scrap-stall gear");
                    if (!session.HasDockCable)
                        Show(1, zh ? "船坞找钢索" : "Find dock cable");
                    break;
                case M08WinchOrderStage.Assemble:
                    body.text = zh
                        ? "齿轮和钢索都已找到，能装起来了。旧件的张力仍需试机检验。"
                        : "The gear and cable are ready. Assemble the winch, then inspect the cable tension.";
                    Show(0, zh ? "组装小绞盘" : "Assemble small winch");
                    break;
                case M08WinchOrderStage.TestLift:
                    body.text = zh
                        ? "绞盘可试着吊重物。若钢索松动，试机会失败，但可以修好重试。"
                        : "Ready to lift a test load. Loose cable can fail the test, but the fault is repairable.";
                    Show(0, zh ? "吊重物试机" : "Test lifting the load");
                    if (session.CableTension < M08FirstWinchOrderSession.MinimumSafeTension)
                        Show(1, zh ? "先检查并收紧钢索" : "Inspect and tighten cable");
                    break;
                case M08WinchOrderStage.RepairLooseCable:
                    body.text = zh
                        ? "试机发现钢索松动，重物没有稳稳抬起。是能修的小毛病，不扣订单，不会卡关。"
                        : "The test exposed a loose cable. The load did not lift safely, but this minor fault can be fixed without losing the job.";
                    Show(0, zh ? "重新固定钢索" : "Repair loose cable");
                    break;
                case M08WinchOrderStage.Completed:
                    body.text = zh
                        ? "重物稳稳吊起，第一张民间订单做成了。工坊押金由真实完工确认；别把试机失败当成卡关。"
                        : "The load rises securely. The first civilian commission is finished; the host now confirms the workshop deposit.";
                    finishLabel.text = zh ? "确认完工并提交给剧情宿主" : "Confirm completion with story host";
                    finishButton.gameObject.SetActive(!delivered);
                    finishButton.interactable = !delivered;
                    break;
            }

            if (!delivered)
            {
                Button chosen = finishButton.interactable ? finishButton : null;
                if (chosen == null)
                    for (int i = 0; i < choices.Length; i++)
                        if (choices[i].interactable) { chosen = choices[i]; break; }
                if (chosen != null)
                    SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, chosen);
            }
        }

        private void Show(int index, string label)
        {
            labels[index].text = label;
            choices[index].gameObject.SetActive(true);
            choices[index].interactable = true;
        }

        private void Choose(int index)
        {
            if (disposed || delivered || IsDestroyedExternally() ||
                index < 0 || index >= choices.Length ||
                !choices[index].IsActive() || !choices[index].IsInteractable())
                return;
            switch (session.Stage)
            {
                case M08WinchOrderStage.SourceParts:
                    if (index == 0) session.TryAcquirePart(M08WinchPart.ScrapGear);
                    if (index == 1) session.TryAcquirePart(M08WinchPart.DockCable);
                    break;
                case M08WinchOrderStage.Assemble:
                    if (index == 0) session.TryAssemble();
                    break;
                case M08WinchOrderStage.TestLift:
                    if (index == 0) session.TryTestLift();
                    if (index == 1) session.TryTightenCable();
                    break;
                case M08WinchOrderStage.RepairLooseCable:
                    if (index == 0) session.TryRepairLooseCable();
                    break;
            }
            Refresh();
        }

        private void Finish()
        {
            if (disposed || delivered || IsDestroyedExternally() ||
                !session.IsCompleted || !finishButton.IsActive() || !finishButton.IsInteractable())
                return;
            // Guard against reentrant submits while keeping failed host writes retryable.
            delivered = true;
            Refresh();
            var callback = onCompleted;
            try
            {
                callback?.Invoke(session);
                onCompleted = null;
            }
            catch
            {
                if (!disposed)
                {
                    delivered = false;
                    Refresh();
                }
                throw;
            }
        }

        private bool IsDestroyedExternally()
        {
            if (!CanvasDestroyed())
                return false;
            Dispose();
            return true;
        }

        private bool CanvasDestroyed()
        {
            if (canvasObject == null || panel == null || heading == null ||
                body == null || finishButton == null || finishLabel == null)
                return true;
            for (int i = 0; i < choices.Length; i++)
                if (choices[i] == null || labels[i] == null) return true;
            return false;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            onCompleted = null;
            SoundwellUiLocalization.Changed -= Refresh;
            for (int i = 0; i < choices.Length; i++)
                if (choices[i] != null) choices[i].onClick.RemoveAllListeners();
            if (finishButton != null) finishButton.onClick.RemoveAllListeners();
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }
}
