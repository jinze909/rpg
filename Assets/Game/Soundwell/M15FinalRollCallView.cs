using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    public enum M15RollCallViewStep
    {
        SelectReader = 0,
        SelectIdentityResponse = 1,
        SelectSteleInscription = 2,
        Completed = 3
    }

    /// <summary>
    /// Bottom-anchored safe-region UGUI panel for M15 final memorial roll call
    /// and stele inscription. Steps through reader selection (Protagonist /
    /// Han Qiao / Aunt Jiang), public vs. private identity response, and stele
    /// inscription (disabling public name carving when consented verified count
    /// is zero, and displaying verified ST-04 Zhongling quarry stone when
    /// delivered), with retry-safe Makinom persistence and ZH/EN localization.
    /// </summary>
    public sealed class M15FinalRollCallView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text body;
        private readonly Button[] choiceButtons = new Button[4];
        private readonly Text[] choiceLabels = new Text[4];
        private readonly M15FinalRollCallSession session;
        private readonly bool showZhonglingStone;
        private Action<M15RollCallReader, M15FinalIdentityResponse, M15SteleInscription> onRollCallSubmitted;
        private M15RollCallViewStep step;
        private M15RollCallReader selectedReader;
        private M15FinalIdentityResponse selectedIdentity;
        private bool submitting;
        private bool disposed;

        public M15FinalRollCallSession Session => session;
        public M15RollCallViewStep Step => step;
        public bool ShowZhonglingStone => showZhonglingStone;
        public RectTransform Panel => panel;
        public Text Heading => heading;
        public Text Body => body;
        public Button ProtagonistReaderButton => choiceButtons[0];
        public Button HanQiaoReaderButton => choiceButtons[1];
        public Button AuntJiangReaderButton => choiceButtons[2];
        public Button PublicTrueNameButton => choiceButtons[0];
        public Button PrivateCustodianButton => choiceButtons[1];
        public Button KeepOriginalSteleButton => choiceButtons[0];
        public Button AddReturnedAliveSteleButton => choiceButtons[1];
        public Button CarveNamesSteleButton => choiceButtons[2];
        public Button LeaveBlankSteleButton => choiceButtons[3];
        public bool IsDisposed => disposed || CanvasDestroyed();

        public M15FinalRollCallView(
            Transform parent,
            M15FinalRollCallSession session,
            Action<M15RollCallReader, M15FinalIdentityResponse, M15SteleInscription> onRollCallSubmitted,
            bool showZhonglingStone = false)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.onRollCallSubmitted = onRollCallSubmitted ??
                throw new ArgumentNullException(nameof(onRollCallSubmitted));
            this.showZhonglingStone = showZhonglingStone;
            step = session.IsCompleted
                ? M15RollCallViewStep.Completed
                : M15RollCallViewStep.SelectReader;

            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("M15 final roll call canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 142;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var holder = new GameObject("M15 roll call bottom panel", typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(904f, 256f);
            panel.anchoredPosition = new Vector2(0f, 138f);

            SoundwellUiTheme.CreatePanel(panel, "M15 roll call border",
                new Vector2(904f, 256f), new Color(0.64f, 0.56f, 0.42f, 1f));
            SoundwellUiTheme.CreatePanel(panel, "M15 roll call backing",
                new Vector2(900f, 252f), new Color(0.06f, 0.09f, 0.11f, 0.97f));
            Color ink = new Color(0.93f, 0.89f, 0.81f, 1f);

            heading = SoundwellUiTheme.CreateLabel(panel, 19,
                new Vector2(0f, 102f), new Vector2(842f, 28f), ink);
            heading.alignment = TextAnchor.MiddleLeft;

            body = SoundwellUiTheme.CreateLabel(panel, 15,
                new Vector2(0f, 40f), new Vector2(842f, 84f), ink);
            body.alignment = TextAnchor.UpperLeft;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;

            var positions = new[]
            {
                new Vector2(-216f, -44f),
                new Vector2(216f, -44f),
                new Vector2(-216f, -92f),
                new Vector2(216f, -92f)
            };
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                choiceButtons[i] = SoundwellUiTheme.CreateButton(
                    panel, "M15 roll call choice " + i, positions[i],
                    new Vector2(414f, 36f), 14, out choiceLabels[i]);
                choiceButtons[i].onClick.AddListener(() => HandleChoiceClick(index));
            }

            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            Refresh();
            SoundwellUiLocalization.Changed += Refresh;
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed || IsDestroyedExternally()) return;
            if (float.IsNaN(aspect) || float.IsInfinity(aspect) || aspect <= 0f) return;
            SoundwellUiTheme.ApplyViewportAspectScale(panel, aspect);
        }

        public void Refresh()
        {
            if (disposed || IsDestroyedExternally()) return;

            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = body.font = font;
            for (int i = 0; i < choiceLabels.Length; i++)
                choiceLabels[i].font = font;

            for (int i = 0; i < choiceButtons.Length; i++)
            {
                choiceButtons[i].gameObject.SetActive(false);
                choiceButtons[i].interactable = false;
            }

            switch (step)
            {
                case M15RollCallViewStep.SelectReader:
                    heading.text = zh ? "临潮纪念日 · 最后一次点名" : "LINCHAO MEMORIAL · FINAL ROLL CALL";
                    body.text = zh
                        ? $"名册至多念三遍，一应即停；无人答的名字不划去。小七按「{session.FormatXiaoqiRollCallEntry(true)}」应声。请选择由谁念名："
                        : $"Each name is called up to 3 times, stopping on the first answer; unanswered names stay on the roster. Xiaoqi answers as \"{session.FormatXiaoqiRollCallEntry(false)}\". Choose the reader:";
                    ShowChoice(0, zh ? "你自己念" : "Read the roster yourself");
                    ShowChoice(1, zh ? "请韩峤念" : "Ask Han Qiao to read");
                    ShowChoice(2, zh ? "请姜婶念" : "Ask Aunt Jiang to read");
                    SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, choiceButtons[0]);
                    break;

                case M15RollCallViewStep.SelectIdentityResponse:
                    heading.text = zh ? "最后一页 · 你的应声" : "FINAL PAGE · YOUR RESPONSE";
                    body.text = zh
                        ? "念到你的名字时：当众以真名应「在」将正式结算抚恤停发与旧账；也可选择散场后向可信保管人私下应声。"
                        : "When your name is called: answering publicly with your true name settles pension closure; or you may answer privately to a trusted custodian afterward.";
                    ShowChoice(0, zh ? "当众以真名应「在」（结算抚恤停发）" : "Answer publicly (settle pension closure)");
                    ShowChoice(1, zh ? "散场后向可信保管人私下应声" : "Answer privately to a trusted custodian");
                    SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, choiceButtons[0]);
                    break;

                case M15RollCallViewStep.SelectSteleInscription:
                    heading.text = zh ? "石碑刻字 · 留给临潮的字" : "MEMORIAL STELE · INSCRIPTION";
                    string stoneNote = showZhonglingStone
                        ? (zh
                            ? "钟岭采石场运抵临潮的新碑石已立妥。"
                            : " The delivered Zhongling quarry stone stands ready at the memorial.")
                        : string.Empty;
                    body.text = zh
                        ? $"已同意公开且核验的归来者人名：{session.ConsentedVerifiedNamesCount} 人。{stoneNote}请选择石碑刻字方案："
                        : $"Consented verified returnee names available to carve: {session.ConsentedVerifiedNamesCount}.{stoneNote} Choose the stele inscription:";
                    bool canCarve = session.CanChooseSteleInscription(M15SteleInscription.CarveConsentedVerifiedNames);
                    ShowChoice(0, zh ? "保留石碑原文" : "Keep original inscription", !submitting);
                    ShowChoice(1, zh ? "加刻一行「他们活着回来了」" : "Add \"They returned alive\"", !submitting);
                    ShowChoice(2, zh ? "刻上已同意且核验的归来者名字" : "Carve consented verified names", !submitting && canCarve);
                    ShowChoice(3, zh ? "石碑留空" : "Leave blank", !submitting);
                    if (!submitting)
                        SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, choiceButtons[0]);
                    break;

                case M15RollCallViewStep.Completed:
                    heading.text = zh ? "石碑前 · 点名礼成" : "MEMORIAL STELE · COMPLETE";
                    body.text = zh
                        ? "最终点名与石碑刻字已落盘记录。"
                        : "Final roll call and stele inscription have been recorded.";
                    break;
            }
        }

        private void ShowChoice(int index, string text, bool interactable = true)
        {
            choiceLabels[index].text = text;
            choiceButtons[index].gameObject.SetActive(true);
            choiceButtons[index].interactable = interactable;
        }

        private void HandleChoiceClick(int index)
        {
            if (disposed || IsDestroyedExternally()) return;

            if (step == M15RollCallViewStep.SelectReader && index >= 0 && index <= 2)
            {
                selectedReader = (M15RollCallReader)(index + 1);
                step = M15RollCallViewStep.SelectIdentityResponse;
                Refresh();
                return;
            }

            if (step == M15RollCallViewStep.SelectIdentityResponse && index >= 0 && index <= 1)
            {
                selectedIdentity = (M15FinalIdentityResponse)(index + 1);
                step = M15RollCallViewStep.SelectSteleInscription;
                Refresh();
                return;
            }

            if (step == M15RollCallViewStep.SelectSteleInscription && !submitting && index >= 0 && index <= 3)
            {
                M15SteleInscription inscription = (M15SteleInscription)(index + 1);
                if (!session.CanChooseSteleInscription(inscription))
                    return;

                submitting = true;
                Refresh();
                try
                {
                    onRollCallSubmitted?.Invoke(selectedReader, selectedIdentity, inscription);
                    if (session.IsCompleted)
                    {
                        step = M15RollCallViewStep.Completed;
                        onRollCallSubmitted = null;
                    }
                }
                finally
                {
                    submitting = false;
                    if (!disposed) Refresh();
                }
            }
        }

        private bool IsDestroyedExternally()
        {
            if (!CanvasDestroyed()) return false;
            Dispose();
            return true;
        }

        private bool CanvasDestroyed()
        {
            if (canvasObject == null || panel == null || heading == null || body == null)
                return true;
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (choiceButtons[i] == null || choiceLabels[i] == null)
                    return true;
            }
            return false;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            onRollCallSubmitted = null;
            SoundwellUiLocalization.Changed -= Refresh;
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (choiceButtons[i] != null)
                    choiceButtons[i].onClick.RemoveAllListeners();
            }
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }
}
