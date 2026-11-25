using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Bottom-anchored safe-region UGUI panel for M13 approach route selection
    /// and A Yuan high-risk consent gating. Enforces SCENE_SCRIPT_M13.md rules:
    /// Route B/C stay disabled until safety preparation is complete or both
    /// Qi Que's warning and explicit risk consent are recorded, and Route B
    /// also requires the 3-bell reverse-signal agreement.
    /// </summary>
    public sealed class M13DepartureRiskView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text summary;
        private readonly Button completePrepButton;
        private readonly Text completePrepLabel;
        private readonly Button agreeThreeBellsButton;
        private readonly Text agreeThreeBellsLabel;
        private readonly Button acceptRiskConsentButton;
        private readonly Text acceptRiskConsentLabel;
        private readonly Button routeAButton;
        private readonly Text routeALabel;
        private readonly Button routeBButton;
        private readonly Text routeBLabel;
        private readonly Button routeCButton;
        private readonly Text routeCLabel;
        private readonly M13DepartureRiskSession session;
        private Action<M13ApproachRoute> onDepartureRequested;
        private bool submitting;
        private bool disposed;

        public M13DepartureRiskSession Session => session;
        public RectTransform Panel => panel;
        public Text Heading => heading;
        public Text Summary => summary;
        public Button CompletePrepButton => completePrepButton;
        public Button AgreeThreeBellsButton => agreeThreeBellsButton;
        public Button AcceptRiskConsentButton => acceptRiskConsentButton;
        public Button RouteAButton => routeAButton;
        public Button RouteBButton => routeBButton;
        public Button RouteCButton => routeCButton;
        public bool IsDisposed => disposed || CanvasDestroyed();

        public M13DepartureRiskView(
            Transform parent,
            M13DepartureRiskSession session,
            Action<M13ApproachRoute> onDepartureRequested)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.onDepartureRequested = onDepartureRequested ??
                throw new ArgumentNullException(nameof(onDepartureRequested));

            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("M13 departure risk canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 140;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var holder = new GameObject("M13 departure bottom panel", typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(904f, 256f);
            panel.anchoredPosition = new Vector2(0f, 138f);

            SoundwellUiTheme.CreatePanel(panel, "M13 departure border",
                new Vector2(904f, 256f), new Color(0.64f, 0.56f, 0.42f, 1f));
            SoundwellUiTheme.CreatePanel(panel, "M13 departure backing",
                new Vector2(900f, 252f), new Color(0.06f, 0.09f, 0.11f, 0.97f));
            Color ink = new Color(0.93f, 0.89f, 0.81f, 1f);

            heading = SoundwellUiTheme.CreateLabel(panel, 19,
                new Vector2(0f, 102f), new Vector2(842f, 28f), ink);
            heading.alignment = TextAnchor.MiddleLeft;

            summary = SoundwellUiTheme.CreateLabel(panel, 15,
                new Vector2(0f, 44f), new Vector2(842f, 78f), ink);
            summary.alignment = TextAnchor.UpperLeft;
            summary.horizontalOverflow = HorizontalWrapMode.Wrap;

            completePrepButton = SoundwellUiTheme.CreateButton(
                panel, "M13 complete prep", new Vector2(-282f, -24f),
                new Vector2(272f, 34f), 14, out completePrepLabel);
            completePrepButton.onClick.AddListener(HandleCompletePrep);

            agreeThreeBellsButton = SoundwellUiTheme.CreateButton(
                panel, "M13 agree three bells", new Vector2(0f, -24f),
                new Vector2(272f, 34f), 14, out agreeThreeBellsLabel);
            agreeThreeBellsButton.onClick.AddListener(HandleAgreeThreeBells);

            acceptRiskConsentButton = SoundwellUiTheme.CreateButton(
                panel, "M13 accept risk consent", new Vector2(282f, -24f),
                new Vector2(272f, 34f), 14, out acceptRiskConsentLabel);
            acceptRiskConsentButton.onClick.AddListener(HandleAcceptRiskConsent);

            routeAButton = SoundwellUiTheme.CreateButton(
                panel, "M13 route A", new Vector2(-282f, -74f),
                new Vector2(272f, 36f), 14, out routeALabel);
            routeAButton.onClick.AddListener(() =>
                HandleConfirmRoute(M13ApproachRoute.RouteASupervisedCivilian));

            routeBButton = SoundwellUiTheme.CreateButton(
                panel, "M13 route B", new Vector2(0f, -74f),
                new Vector2(272f, 36f), 14, out routeBLabel);
            routeBButton.onClick.AddListener(() =>
                HandleConfirmRoute(M13ApproachRoute.RouteBBellTowerAssault));

            routeCButton = SoundwellUiTheme.CreateButton(
                panel, "M13 route C", new Vector2(282f, -74f),
                new Vector2(272f, 36f), 14, out routeCLabel);
            routeCButton.onClick.AddListener(() =>
                HandleConfirmRoute(M13ApproachRoute.RouteCOldCanalNightRun));

            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            Refresh();
            SoundwellUiLocalization.Changed += Refresh;
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
            heading.font = summary.font = completePrepLabel.font = agreeThreeBellsLabel.font =
                acceptRiskConsentLabel.font = routeALabel.font = routeBLabel.font =
                routeCLabel.font = font;

            heading.text = zh
                ? "回澜关前 · 进场路线与风险确认"
                : "BEFORE HUILAN PASS · APPROACH ROUTE & RISK GATE";

            string prepState = session.SafetyPreparationComplete
                ? (zh ? "接应已齐备（阿鸢安全）" : "Prep complete (A Yuan safe)")
                : (zh ? "接应不足" : "Prep incomplete");
            string bellState = session.ThreeBellReverseSignalAgreed
                ? (zh ? "三声钟反号已约定" : "3-bell reverse signal agreed")
                : (zh ? "三声钟未约定" : "3-bell signal not set");
            string consentState = session.ExplicitHighRiskConsent
                ? (zh ? "已独立确认高风险" : "High-risk consent recorded")
                : (session.QiQueWarningGiven
                    ? (zh ? "祁雀已发出风险警告" : "Qi Que warned of risk")
                    : (zh ? "未确认高风险" : "No risk consent"));

            summary.text = zh
                ? $"状态：{prepState}  ｜  {bellState}  ｜  {consentState}\n路线 B/C 须先补齐接应，或在祁雀警告后独立确认阿鸢高风险哨位方可启程。"
                : $"Status: {prepState}  |  {bellState}  |  {consentState}\nRoute B/C require complete prep or explicit high-risk consent after Qi Que's warning.";

            completePrepLabel.text = zh ? "补齐人手与接应" : "Complete safety prep";
            agreeThreeBellsLabel.text = zh ? "约定三声钟反号" : "Agree 3-bell signal";
            acceptRiskConsentLabel.text = zh ? "独立确认阿鸢高风险" : "Accept A Yuan risk";
            routeALabel.text = zh ? "启程：路线 A（监督移交）" : "Depart: Route A (Civilian)";
            routeBLabel.text = zh ? "启程：路线 B（夜夺钟楼）" : "Depart: Route B (Bell Tower)";
            routeCLabel.text = zh ? "启程：路线 C（旧运渠夜航）" : "Depart: Route C (Old Canal)";

            bool open = !session.IsDeparted && !submitting;
            completePrepButton.gameObject.SetActive(!session.IsDeparted);
            completePrepButton.interactable = open && !session.SafetyPreparationComplete;

            agreeThreeBellsButton.gameObject.SetActive(!session.IsDeparted);
            agreeThreeBellsButton.interactable = open && !session.ThreeBellReverseSignalAgreed;

            bool showConsent = !session.IsDeparted &&
                !session.SafetyPreparationComplete &&
                session.QiQueWarningGiven;
            acceptRiskConsentButton.gameObject.SetActive(showConsent);
            acceptRiskConsentButton.interactable = open && showConsent && !session.ExplicitHighRiskConsent;

            routeAButton.gameObject.SetActive(!session.IsDeparted);
            routeAButton.interactable = open &&
                session.CanConfirmDeparture(M13ApproachRoute.RouteASupervisedCivilian);

            routeBButton.gameObject.SetActive(!session.IsDeparted);
            routeBButton.interactable = open &&
                session.CanConfirmDeparture(M13ApproachRoute.RouteBBellTowerAssault);

            routeCButton.gameObject.SetActive(!session.IsDeparted);
            routeCButton.interactable = open &&
                session.CanConfirmDeparture(M13ApproachRoute.RouteCOldCanalNightRun);

            if (open)
                SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, routeAButton);
        }

        private void HandleCompletePrep()
        {
            if (disposed || submitting || session.IsDeparted || IsDestroyedExternally()) return;
            if (!completePrepButton.IsActive() || !completePrepButton.IsInteractable()) return;
            session.CompleteSafetyPreparation();
            Refresh();
        }

        private void HandleAgreeThreeBells()
        {
            if (disposed || submitting || session.IsDeparted || IsDestroyedExternally()) return;
            if (!agreeThreeBellsButton.IsActive() || !agreeThreeBellsButton.IsInteractable()) return;
            session.AgreeThreeBellReverseSignal();
            Refresh();
        }

        private void HandleAcceptRiskConsent()
        {
            if (disposed || submitting || session.IsDeparted || IsDestroyedExternally()) return;
            if (!acceptRiskConsentButton.IsActive() || !acceptRiskConsentButton.IsInteractable()) return;
            if (session.TryAcceptHighRiskConsent())
                Refresh();
        }

        private void HandleConfirmRoute(M13ApproachRoute route)
        {
            if (disposed || submitting || session.IsDeparted || IsDestroyedExternally()) return;
            Button routeButton;
            switch (route)
            {
                case M13ApproachRoute.RouteASupervisedCivilian: routeButton = routeAButton; break;
                case M13ApproachRoute.RouteBBellTowerAssault: routeButton = routeBButton; break;
                case M13ApproachRoute.RouteCOldCanalNightRun: routeButton = routeCButton; break;
                default: return;
            }
            if (!routeButton.IsActive() || !routeButton.IsInteractable() ||
                !session.CanConfirmDeparture(route)) return;

            submitting = true;
            Refresh();
            try
            {
                onDepartureRequested?.Invoke(route);
                if (session.IsDeparted)
                    onDepartureRequested = null;
            }
            finally
            {
                submitting = false;
                if (!disposed) Refresh();
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
            return canvasObject == null || panel == null || heading == null ||
                summary == null || completePrepButton == null || completePrepLabel == null ||
                agreeThreeBellsButton == null || agreeThreeBellsLabel == null ||
                acceptRiskConsentButton == null || acceptRiskConsentLabel == null ||
                routeAButton == null || routeALabel == null ||
                routeBButton == null || routeBLabel == null ||
                routeCButton == null || routeCLabel == null;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            onDepartureRequested = null;
            SoundwellUiLocalization.Changed -= Refresh;
            if (completePrepButton != null) completePrepButton.onClick.RemoveAllListeners();
            if (agreeThreeBellsButton != null) agreeThreeBellsButton.onClick.RemoveAllListeners();
            if (acceptRiskConsentButton != null) acceptRiskConsentButton.onClick.RemoveAllListeners();
            if (routeAButton != null) routeAButton.onClick.RemoveAllListeners();
            if (routeBButton != null) routeBButton.onClick.RemoveAllListeners();
            if (routeCButton != null) routeCButton.onClick.RemoveAllListeners();
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }
}
