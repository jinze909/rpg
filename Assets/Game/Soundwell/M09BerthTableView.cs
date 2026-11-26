using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Bottom-anchored safe-region UGUI panel for M09 second-batch berth table
    /// negotiation. Displays live berth shortfall (separate from M07's first
    /// batch), lets the player combine any subset of the three capacity
    /// exchanges (Han Qiao, Shipwright, Qi Que), respects Shen Li's presence
    /// before enabling shipwright/reconciliation actions, and supports clean
    /// retry on interrupted Makinom writes.
    /// </summary>
    public sealed class M09BerthTableView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text summary;
        private readonly Button hanQiaoButton;
        private readonly Text hanQiaoLabel;
        private readonly Button shipwrightButton;
        private readonly Text shipwrightLabel;
        private readonly Button qiQueButton;
        private readonly Text qiQueLabel;
        private readonly Button reconcileButton;
        private readonly Text reconcileLabel;
        private readonly Button settleButton;
        private readonly Text settleLabel;
        private readonly M09BerthTableSession session;
        private readonly int hanQiaoGain;
        private readonly int shipwrightGain;
        private readonly int qiQueGain;
        // Scene-owned evidence of an already-paid shipyard deal. No money
        // is created, charged, or inferred by the UI.
        private readonly Func<bool> shipwrightFeePaidInWorld;
        private Action<M09BerthTableSession> onSettleRequested;
        private bool settling;
        private bool checkingFeeEvidence;
        private bool disposed;

        public M09BerthTableSession Session => session;
        public RectTransform Panel => panel;
        public Text Heading => heading;
        public Text Summary => summary;
        public Button HanQiaoButton => hanQiaoButton;
        public Button ShipwrightButton => shipwrightButton;
        public Button QiQueButton => qiQueButton;
        public Button ReconcileButton => reconcileButton;
        public Button SettleButton => settleButton;
        public bool IsDisposed => disposed || CanvasDestroyed();

        public M09BerthTableView(
            Transform parent,
            M09BerthTableSession session,
            Action<M09BerthTableSession> onSettleRequested,
            int hanQiaoGain = 3,
            int shipwrightGain = 2,
            int qiQueGain = 3,
            Func<bool> shipwrightFeePaidInWorld = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.onSettleRequested = onSettleRequested ??
                throw new ArgumentNullException(nameof(onSettleRequested));
            if (hanQiaoGain <= 0 || shipwrightGain <= 0 || qiQueGain <= 0)
                throw new ArgumentOutOfRangeException(nameof(hanQiaoGain),
                    "Berth gains must be positive.");

            this.hanQiaoGain = hanQiaoGain;
            this.shipwrightGain = shipwrightGain;
            this.qiQueGain = qiQueGain;
            this.shipwrightFeePaidInWorld = shipwrightFeePaidInWorld ?? (() => false);

            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("M09 berth table canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 138;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var holder = new GameObject("M09 berth bottom panel", typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(904f, 256f);
            panel.anchoredPosition = new Vector2(0f, 138f);

            SoundwellUiTheme.CreatePanel(panel, "M09 berth border",
                new Vector2(904f, 256f), new Color(0.64f, 0.56f, 0.42f, 1f));
            SoundwellUiTheme.CreatePanel(panel, "M09 berth backing",
                new Vector2(900f, 252f), new Color(0.06f, 0.09f, 0.11f, 0.97f));
            Color ink = new Color(0.93f, 0.89f, 0.81f, 1f);

            heading = SoundwellUiTheme.CreateLabel(panel, 19,
                new Vector2(0f, 102f), new Vector2(842f, 28f), ink);
            heading.alignment = TextAnchor.MiddleLeft;

            summary = SoundwellUiTheme.CreateLabel(panel, 15,
                new Vector2(0f, 44f), new Vector2(842f, 78f), ink);
            summary.alignment = TextAnchor.UpperLeft;
            summary.horizontalOverflow = HorizontalWrapMode.Wrap;

            hanQiaoButton = SoundwellUiTheme.CreateButton(
                panel, "M09 han qiao exchange", new Vector2(-216f, -24f),
                new Vector2(414f, 34f), 14, out hanQiaoLabel);
            hanQiaoButton.onClick.AddListener(() =>
                HandleExchange(hanQiaoButton, M09BerthExchangeFlags.HanQiaoCivilianVoyage, this.hanQiaoGain));

            shipwrightButton = SoundwellUiTheme.CreateButton(
                panel, "M09 shipwright exchange", new Vector2(216f, -24f),
                new Vector2(414f, 34f), 14, out shipwrightLabel);
            shipwrightButton.onClick.AddListener(() =>
                HandleExchange(shipwrightButton, M09BerthExchangeFlags.ShipwrightExtraBerths, this.shipwrightGain));

            qiQueButton = SoundwellUiTheme.CreateButton(
                panel, "M09 qi que exchange", new Vector2(-216f, -66f),
                new Vector2(414f, 34f), 14, out qiQueLabel);
            qiQueButton.onClick.AddListener(() =>
                HandleExchange(qiQueButton, M09BerthExchangeFlags.QiQueCargoVoyage, this.qiQueGain));

            reconcileButton = SoundwellUiTheme.CreateButton(
                panel, "M09 shen reconcile", new Vector2(216f, -66f),
                new Vector2(414f, 34f), 14, out reconcileLabel);
            reconcileButton.onClick.AddListener(HandleReconcile);

            settleButton = SoundwellUiTheme.CreateButton(
                panel, "M09 settle berth table", new Vector2(280f, -106f),
                new Vector2(286f, 34f), 15, out settleLabel);
            settleButton.onClick.AddListener(HandleSettle);

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
            if (float.IsNaN(aspect) || float.IsInfinity(aspect) || aspect <= 0f)
                return;
            SoundwellUiTheme.ApplyViewportAspectScale(panel, aspect);
        }

        public void Refresh()
        {
            if (disposed || checkingFeeEvidence) return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }

            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = summary.font = hanQiaoLabel.font = shipwrightLabel.font =
                qiQueLabel.font = reconcileLabel.font = settleLabel.font = font;

            heading.text = zh
                ? "第二批接驳 · 船舱床位表"
                : "SECOND BATCH · CABIN BERTH TABLE";

            string debtNote = session.QiDebtDisclosedHere
                ? (zh ? "  ｜  祁雀已摊开船贷" : "  |  Qi Que disclosed ship debt")
                : string.Empty;
            string shenNote = session.ShenReconciled
                ? (zh ? "  ｜  已与沈砾谈妥各自打算" : "  |  Reconciled with Shen Li")
                : (!session.ShenLiPresent
                    ? (zh ? "  ｜  沈砾未同行" : "  |  Shen Li absent")
                    : string.Empty);

            summary.text = zh
                ? $"当前铺位：{session.TotalBerths}  ｜  第二批待安排：{session.PendingNames}  ｜  尚缺铺位：{session.CurrentShortfall}{debtNote}{shenNote}\n未安排的名字仍待后续接应，不计为已登船。"
                : $"Total berths: {session.TotalBerths}  |  Second-batch pending: {session.PendingNames}  |  Shortfall: {session.CurrentShortfall}{debtNote}{shenNote}\nUnassigned names remain pending for later pickup and are never counted as boarded.";

            hanQiaoLabel.text = zh
                ? $"韩峤民用航次（+{hanQiaoGain}铺，需正式登记在世）"
                : $"Han Qiao civilian voyage (+{hanQiaoGain}, living registry)";
            shipwrightLabel.text = session.ShenLiPresent
                ? (zh ? $"修船匠加铺（+{shipwrightGain}铺，沈砾留教两天）"
                      : $"Shipwright extra berths (+{shipwrightGain}, Shen Li teaches 2d)")
                : (zh ? $"修船匠加铺（+{shipwrightGain}铺，已付修船费）"
                      : $"Shipwright extra berths (+{shipwrightGain}, verified fee paid)");
            qiQueLabel.text = zh
                ? $"祁雀让出载货航次（+{qiQueGain}铺，谈船工分成）"
                : $"Qi Que cargo voyage (+{qiQueGain}, crew profit-share)";
            reconcileLabel.text = zh
                ? "与沈砾摊开谈妥各自打算"
                : "Talk openly with Shen Li";
            settleLabel.text = zh
                ? "敲定第二批床位表"
                : "Settle berth table";

            bool active = !session.IsSettled && !settling;
            bool hanUsed = (session.NegotiatedExchanges & M09BerthExchangeFlags.HanQiaoCivilianVoyage) != 0;
            bool shipUsed = (session.NegotiatedExchanges & M09BerthExchangeFlags.ShipwrightExtraBerths) != 0;
            bool qiUsed = (session.NegotiatedExchanges & M09BerthExchangeFlags.QiQueCargoVoyage) != 0;

            hanQiaoButton.gameObject.SetActive(!session.IsSettled);
            hanQiaoButton.interactable = active && !hanUsed;

            bool paidDealObserved = !session.ShenLiPresent && ReadShipwrightFeeEvidence();
            if (disposed || CanvasDestroyed())
            {
                Dispose();
                return;
            }
            shipwrightButton.gameObject.SetActive(!session.IsSettled &&
                (session.ShenLiPresent || paidDealObserved));
            shipwrightButton.interactable = active && !shipUsed &&
                (session.ShenLiPresent || paidDealObserved);

            qiQueButton.gameObject.SetActive(!session.IsSettled);
            qiQueButton.interactable = active && !qiUsed;

            reconcileButton.gameObject.SetActive(!session.IsSettled && session.ShenLiPresent);
            reconcileButton.interactable = active && session.ShenLiPresent && !session.ShenReconciled;

            settleButton.gameObject.SetActive(!session.IsSettled);
            settleButton.interactable = active;
            if (active)
                SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, settleButton);
        }

        private static bool CanInvoke(Button button)
        {
            return button != null && button.IsActive() && button.IsInteractable();
        }

        // The scene callback observes an existing payment, not a new charge.
        // Block nested UI actions and refreshes while it is being evaluated.
        private bool ReadShipwrightFeeEvidence()
        {
            checkingFeeEvidence = true;
            try { return shipwrightFeePaidInWorld(); }
            finally { checkingFeeEvidence = false; }
        }

        private void HandleExchange(Button button, M09BerthExchangeFlags exchange, int gain)
        {
            if (disposed || settling || checkingFeeEvidence || session.IsSettled)
                return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }

            if (!CanInvoke(button)) return;

            bool needsPaidDeal = exchange == M09BerthExchangeFlags.ShipwrightExtraBerths &&
                !session.ShenLiPresent;
            if (needsPaidDeal)
            {
                bool paid = ReadShipwrightFeeEvidence();
                if (disposed || CanvasDestroyed())
                {
                    Dispose();
                    return;
                }
                if (!paid || !CanInvoke(button)) return;
            }
            if (session.TryNegotiateExchange(exchange, gain,
                    shipwrightFeePaid: needsPaidDeal))
                Refresh();
        }

        private void HandleReconcile()
        {
            if (disposed || settling || checkingFeeEvidence || session.IsSettled)
                return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }

            if (!CanInvoke(reconcileButton)) return;

            if (session.TryPlayShenReconciliation())
                Refresh();
        }

        private void HandleSettle()
        {
            if (disposed || settling || checkingFeeEvidence || session.IsSettled)
                return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }

            if (!CanInvoke(settleButton)) return;

            settling = true;
            Refresh();
            try
            {
                onSettleRequested?.Invoke(session);
                if (session.IsSettled)
                    onSettleRequested = null;
            }
            finally
            {
                settling = false;
                if (!disposed) Refresh();
            }
        }

        private bool CanvasDestroyed()
        {
            return canvasObject == null || panel == null || heading == null ||
                summary == null || hanQiaoButton == null || hanQiaoLabel == null ||
                shipwrightButton == null || shipwrightLabel == null ||
                qiQueButton == null || qiQueLabel == null ||
                reconcileButton == null || reconcileLabel == null ||
                settleButton == null || settleLabel == null;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            onSettleRequested = null;
            SoundwellUiLocalization.Changed -= Refresh;
            if (hanQiaoButton != null) hanQiaoButton.onClick.RemoveAllListeners();
            if (shipwrightButton != null) shipwrightButton.onClick.RemoveAllListeners();
            if (qiQueButton != null) qiQueButton.onClick.RemoveAllListeners();
            if (reconcileButton != null) reconcileButton.onClick.RemoveAllListeners();
            if (settleButton != null) settleButton.onClick.RemoveAllListeners();
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }
}
