using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Bottom-anchored safe-region UGUI panel for RC-03 ("白矶岛的多数").
    /// Steps through the 3 island visits, live pump-diagram / successor-training
    /// / Lan Ruo consent checks, and the three-way charter decision with
    /// retry-safe Makinom persistence and ZH/EN localization.
    /// </summary>
    public sealed class RC03BaijiMineView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text body;
        private readonly Button[] topButtons = new Button[3];
        private readonly Text[] topLabels = new Text[3];
        private readonly Button[] bottomButtons = new Button[3];
        private readonly Text[] bottomLabels = new Text[3];
        private readonly RC03BaijiMineSession session;
        private readonly Func<bool> diagramMatchedAtPumpTwo;
        private readonly Func<bool> successorTrainingConfirmed;
        private readonly Func<bool> lanRuoWillingToStayOneYear;
        private Action<RC03Resolution> onResolutionSubmitted;
        private bool submitting;
        private bool disposed;

        public RC03BaijiMineSession Session => session;
        public RectTransform Panel => panel;
        public Text Heading => heading;
        public Text Body => body;
        public Button CanteenVisitButton => topButtons[0];
        public Button PumpRoomVisitButton => topButtons[1];
        public Button CharterTalksButton => topButtons[2];
        public Button VerifyDiagramButton => topButtons[0];
        public Button SecureTrainingButton => topButtons[1];
        public Button AskLanRuoStayButton => topButtons[2];
        public Button AmendCharterButton => bottomButtons[0];
        public Button SecretEvacuateButton => bottomButtons[1];
        public Button StayOneYearButton => bottomButtons[2];

        public RC03BaijiMineView(
            Transform parent,
            RC03BaijiMineSession session,
            Func<bool> diagramMatchedAtPumpTwo,
            Func<bool> successorTrainingConfirmed,
            Func<bool> lanRuoWillingToStayOneYear,
            Action<RC03Resolution> onResolutionSubmitted)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.diagramMatchedAtPumpTwo = diagramMatchedAtPumpTwo ??
                throw new ArgumentNullException(nameof(diagramMatchedAtPumpTwo));
            this.successorTrainingConfirmed = successorTrainingConfirmed ??
                throw new ArgumentNullException(nameof(successorTrainingConfirmed));
            this.lanRuoWillingToStayOneYear = lanRuoWillingToStayOneYear ??
                throw new ArgumentNullException(nameof(lanRuoWillingToStayOneYear));
            this.onResolutionSubmitted = onResolutionSubmitted ??
                throw new ArgumentNullException(nameof(onResolutionSubmitted));

            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("RC03 baiji mine canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 147;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var holder = new GameObject("RC03 baiji mine bottom panel", typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(904f, 256f);
            panel.anchoredPosition = new Vector2(0f, 138f);

            SoundwellUiTheme.CreatePanel(panel, "RC03 baiji border",
                new Vector2(904f, 256f), new Color(0.64f, 0.56f, 0.42f, 1f));
            SoundwellUiTheme.CreatePanel(panel, "RC03 baiji backing",
                new Vector2(900f, 252f), new Color(0.06f, 0.09f, 0.11f, 0.97f));
            Color ink = new Color(0.93f, 0.89f, 0.81f, 1f);

            heading = SoundwellUiTheme.CreateLabel(panel, 19,
                new Vector2(0f, 102f), new Vector2(842f, 28f), ink);
            heading.alignment = TextAnchor.MiddleLeft;

            body = SoundwellUiTheme.CreateLabel(panel, 15,
                new Vector2(0f, 44f), new Vector2(842f, 78f), ink);
            body.alignment = TextAnchor.UpperLeft;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;

            float[] xs = { -282f, 0f, 282f };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                topButtons[i] = SoundwellUiTheme.CreateButton(
                    panel, "RC03 top " + i, new Vector2(xs[i], -24f),
                    new Vector2(272f, 34f), 13, out topLabels[i]);
                topButtons[i].onClick.AddListener(() => HandleTopClick(index));

                bottomButtons[i] = SoundwellUiTheme.CreateButton(
                    panel, "RC03 bottom " + i, new Vector2(xs[i], -74f),
                    new Vector2(272f, 36f), 13, out bottomLabels[i]);
                bottomButtons[i].onClick.AddListener(() => HandleBottomClick(index));
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
            for (int i = 0; i < 3; i++)
            {
                topLabels[i].font = font;
                bottomLabels[i].font = font;
                topButtons[i].gameObject.SetActive(false);
                topButtons[i].interactable = false;
                bottomButtons[i].gameObject.SetActive(false);
                bottomButtons[i].interactable = false;
            }

            switch (session.Stage)
            {
                case RC03Stage.IslandInvestigation:
                    heading.text = zh ? "RC03 · 白矶岛互助会查访" : "RC03 · BAIJI ISLAND VISITS";
                    body.text = zh
                        ? "已核验的二十七人里有二十三人按手印愿留岛。先在食堂夜校、二号淡水泵房与互助会章程中摸清实情。"
                        : "Twenty-three of twenty-seven verified returnees chose to stay. Visit the canteen, Pump #2, and mutual-aid charter first.";
                    Show(topButtons[0], topLabels[0], zh ? "查食堂、夜校与配给表" : "Visit canteen & night school",
                        (session.VisitsCompleted & RC03IslandVisits.CanteenAndNightSchool) == 0);
                    Show(topButtons[1], topLabels[1], zh ? "查二号淡水泵房与蓝箬" : "Visit Pump #2 & Lan Ruo",
                        (session.VisitsCompleted & RC03IslandVisits.PumpRoomTwoAndLanRuo) == 0);
                    Show(topButtons[2], topLabels[2], zh ? "查互助会章程与深谈" : "Review charter & talks",
                        (session.VisitsCompleted & RC03IslandVisits.MutualAidCharterAndTalks) == 0);
                    FocusFirstInteractable();
                    break;

                case RC03Stage.ResolutionDecision:
                    heading.text = zh ? "RC03 · 白矶岛与蓝箬的出路" : "RC03 · BAIJI CHARTER DECISION";
                    string copyNote = session.CarriedM07PumpDiagramCopy
                        ? (zh ? "已携带 M07 停机泵图副本。" : "Carrying M07 pump diagram copy.")
                        : (zh ? "需在二号泵房与蓝箬核图。" : "Verify pump diagram at Pump #2.");
                    body.text = copyNote + (zh
                        ? " 改会章须先核图并落实接班培训；留一年须蓝箬本人明确同意。"
                        : " Amending charter needs verified diagram & successor training; 1-year stay needs Lan Ruo's consent.");
                    bool open = !submitting;
                    Show(topButtons[0], topLabels[0], zh ? "核对二号泵修泵图" : "Verify Pump #2 diagram",
                        open && !session.PumpDiagramVerified);
                    Show(topButtons[1], topLabels[1], zh ? "落实接班人培训" : "Secure successor training",
                        open && session.PumpDiagramVerified && !session.SuccessorTrainingSecured);
                    Show(topButtons[2], topLabels[2], zh ? "征询蓝箬留一年意愿" : "Ask Lan Ruo about 1-yr stay",
                        open && !session.LanRuoConsentedToStay);

                    Show(bottomButtons[0], bottomLabels[0], zh ? "劝会议改会章（蓝箬登船）" : "Amend charter (Lan Ruo boards)",
                        open && session.CanSettle(RC03Resolution.AmendCharterWithSuccessors));
                    Show(bottomButtons[1], bottomLabels[1], zh ? "帮蓝箬悄悄离岛（半岛停水）" : "Secretly evacuate (water cut)",
                        open && session.CanSettle(RC03Resolution.SecretlyEvacuateLanRuo));
                    Show(bottomButtons[2], bottomLabels[2], zh ? "按本人同意留守一年" : "Record agreed 1-year stay",
                        open && session.CanSettle(RC03Resolution.StayOneYearByConsent));
                    if (open) FocusFirstInteractable();
                    break;

                case RC03Stage.Completed:
                    heading.text = zh ? "RC03 · 白矶矿案已落笔" : "RC03 · BAIJI CASE RECORDED";
                    body.text = zh
                        ? "白矶矿处置结果与罗会长名册笔迹已落盘保存。"
                        : "Baiji Mine resolution and Chairman Luo's register note have been recorded.";
                    break;
            }
        }

        private static void Show(Button button, Text label, string text, bool interactable)
        {
            label.text = text;
            button.gameObject.SetActive(true);
            button.interactable = interactable;
        }

        private void FocusFirstInteractable()
        {
            for (int i = 0; i < 3; i++)
            {
                if (topButtons[i].gameObject.activeSelf && topButtons[i].interactable)
                {
                    SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, topButtons[i]);
                    return;
                }
            }
            for (int i = 0; i < 3; i++)
            {
                if (bottomButtons[i].gameObject.activeSelf && bottomButtons[i].interactable)
                {
                    SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, bottomButtons[i]);
                    return;
                }
            }
        }

        private void HandleTopClick(int index)
        {
            if (disposed || submitting || CanvasDestroyed() || index < 0 || index >= 3)
                return;
            if (!topButtons[index].gameObject.activeSelf || !topButtons[index].interactable)
                return;

            if (session.Stage == RC03Stage.IslandInvestigation)
            {
                RC03IslandVisits visit = index == 0
                    ? RC03IslandVisits.CanteenAndNightSchool
                    : index == 1
                        ? RC03IslandVisits.PumpRoomTwoAndLanRuo
                        : RC03IslandVisits.MutualAidCharterAndTalks;
                if (session.TryCompleteVisit(visit)) Refresh();
                return;
            }

            if (session.Stage == RC03Stage.ResolutionDecision)
            {
                if (index == 0) session.TryVerifyPumpDiagram(diagramMatchedAtPumpTwo());
                else if (index == 1) session.TrySecureSuccessorTraining(successorTrainingConfirmed());
                else if (index == 2) session.TryRecordLanRuoStayDecision(lanRuoWillingToStayOneYear());
                Refresh();
            }
        }

        private void HandleBottomClick(int index)
        {
            if (disposed || submitting || CanvasDestroyed() || index < 0 || index >= 3)
                return;
            if (session.Stage != RC03Stage.ResolutionDecision ||
                !bottomButtons[index].gameObject.activeSelf ||
                !bottomButtons[index].interactable)
                return;

            var resolution = (RC03Resolution)(index + 1);
            if (!session.CanSettle(resolution)) return;

            submitting = true;
            Refresh();
            try
            {
                onResolutionSubmitted?.Invoke(resolution);
                if (session.IsResolved)
                    onResolutionSubmitted = null;
            }
            finally
            {
                submitting = false;
                if (!disposed) Refresh();
            }
        }

        private bool CanvasDestroyed()
        {
            if (canvasObject == null || panel == null || heading == null || body == null)
                return true;
            for (int i = 0; i < 3; i++)
            {
                if (topButtons[i] == null || topLabels[i] == null ||
                    bottomButtons[i] == null || bottomLabels[i] == null)
                    return true;
            }
            return false;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            onResolutionSubmitted = null;
            SoundwellUiLocalization.Changed -= Refresh;
            for (int i = 0; i < 3; i++)
            {
                if (topButtons[i] != null) topButtons[i].onClick.RemoveAllListeners();
                if (bottomButtons[i] != null) bottomButtons[i].onClick.RemoveAllListeners();
            }
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }
}
