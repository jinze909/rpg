using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// M08's optional night-dock truth conversation. No conversation starts
    /// if Shen Li has chosen another route, or before the first winch
    /// commission is confirmed. Keeps the three authored responses separate
    /// and lets the owning story host retry an uncommitted Makinom write.
    /// </summary>
    public sealed class M08DockTruthView : IDisposable
    {
        private readonly M08DockTruthSession session;
        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly Text heading;
        private readonly Text body;
        private readonly Button hearButton;
        private readonly Text hearLabel;
        private readonly Button[] responseButtons = new Button[3];
        private readonly Text[] responseLabels = new Text[3];
        private readonly Button finishButton;
        private readonly Text finishLabel;
        private readonly Font latinFont;
        private Func<M08DockTruthResponse, bool> recordResponse;
        private bool disposed;

        public M08DockTruthSession Session => session;
        public Text Body => body;
        public RectTransform Panel => panel;
        public Button HearButton => hearButton;
        public Button FirstResponseButton => responseButtons[0];
        public Button SecondResponseButton => responseButtons[1];
        public Button ThirdResponseButton => responseButtons[2];
        public Button FinishButton => finishButton;
        public bool IsDisposed => disposed || CanvasDestroyed();

        public M08DockTruthView(
            Transform parent,
            M08DockTruthSession session,
            Func<M08DockTruthResponse, bool> recordResponse)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.recordResponse = recordResponse ?? throw new ArgumentNullException(nameof(recordResponse));
            if (!session.CanConverse)
                throw new InvalidOperationException(
                    "M08 dock truth requires Shen Li present and the winch commission confirmed.");

            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("M08 dock truth canvas",
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

            var holder = new GameObject("M08 dock truth bottom panel", typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(904f, 248f);
            panel.anchoredPosition = new Vector2(0f, 134f);
            SoundwellUiTheme.CreatePanel(panel, "M08 truth border",
                new Vector2(904f, 248f), new Color(0.64f, 0.56f, 0.42f, 1f));
            SoundwellUiTheme.CreatePanel(panel, "M08 truth backing",
                new Vector2(900f, 244f), new Color(0.06f, 0.09f, 0.11f, 0.97f));
            var ink = new Color(0.93f, 0.89f, 0.81f, 1f);
            heading = SoundwellUiTheme.CreateLabel(panel, 19,
                new Vector2(0f, 98f), new Vector2(842f, 28f), ink);
            heading.alignment = TextAnchor.MiddleLeft;
            body = SoundwellUiTheme.CreateLabel(panel, 15,
                new Vector2(0f, 36f), new Vector2(842f, 88f), ink);
            body.alignment = TextAnchor.UpperLeft;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;

            hearButton = SoundwellUiTheme.CreateButton(
                panel, "M08 truth hear", new Vector2(0f, -83f),
                new Vector2(418f, 38f), 15, out hearLabel);
            hearButton.onClick.AddListener(HearTruth);
            var positions = new[] {
                new Vector2(-286f, -83f),
                new Vector2(0f, -83f),
                new Vector2(286f, -83f)
            };
            for (int i = 0; i < responseButtons.Length; i++)
            {
                int choice = i;
                responseButtons[i] = SoundwellUiTheme.CreateButton(
                    panel, "M08 truth reply " + i, positions[i],
                    new Vector2(280f, 38f), 14, out responseLabels[i]);
                responseButtons[i].onClick.AddListener(() => Respond(choice));
            }
            finishButton = SoundwellUiTheme.CreateButton(
                panel, "M08 truth finish", new Vector2(0f, -111f),
                new Vector2(388f, 34f), 15, out finishLabel);
            finishButton.onClick.AddListener(Finish);

            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            SoundwellUiLocalization.Changed += Refresh;
            Refresh();
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed) return;
            if (CanvasDestroyed()) { Dispose(); return; }
            SoundwellUiTheme.ApplyViewportAspectScale(panel, aspect);
        }

        public void Refresh()
        {
            if (disposed) return;
            if (CanvasDestroyed()) { Dispose(); return; }
            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = body.font = hearLabel.font = finishLabel.font = font;
            foreach (var label in responseLabels) label.font = font;
            hearButton.gameObject.SetActive(false);
            hearButton.interactable = false;
            foreach (var button in responseButtons)
            {
                button.gameObject.SetActive(false);
                button.interactable = false;
            }
            finishButton.gameObject.SetActive(false);
            finishButton.interactable = false;
            heading.text = zh ? "M08 · 夜里码头「我一直知道，没说」"
                              : "M08 · THE NIGHT DOCK: WHAT I NEVER SAID";

            Button focus = null;
            switch (session.Stage)
            {
                case M08DockTruthStage.AwaitingTruth:
                    body.text = zh
                        ? "没有任务标记的夜晚。你和沈砾坐在码头石阶上，谈起她的矿工朋友与临潮故人。海风吹过，她终于愿意讲那一截断缆。"
                        : "On a night without a quest marker, you sit beside Shen Li on the dock steps. You trade stories of people lost and found. She is finally ready to speak of the severed cable.";
                    hearLabel.text = zh ? "听沈砾讲完" : "Listen to Shen Li";
                    hearButton.gameObject.SetActive(true);
                    hearButton.interactable = true;
                    focus = hearButton;
                    break;

                case M08DockTruthStage.AwaitingResponse:
                    body.text = zh
                        ? "沈砾：天亮时我看见桥桩上的断缆，那刀口像工兵刀。我怀疑是自己人，没敢说。你那时只剩一口气。你想怎么回应？"
                        : "Shen Li: At dawn the severed cable hung from our bridge post. The cut looked like a sapper's blade. I suspected our own side, but you were barely alive. What do you say?";
                    SetResponse(0, zh ? "你替我做了决定" : "You decided for me");
                    SetResponse(1, zh ? "暂时沉默" : "Stay silent");
                    SetResponse(2, zh ? "我也曾害怕开口" : "I hid things too");
                    focus = responseButtons[0];
                    break;

                case M08DockTruthStage.AwaitingHost:
                    switch (session.SelectedResponse.Value)
                    {
                        case M08DockTruthResponse.AngerAtBeingShielded:
                            body.text = zh
                                ? "沈砾没有躲开：那时你背上全是血。我怕说出来，你连最后一口气都留不住。往后我不替你瞒了。"
                                : "Shen Li does not look away: Your back was covered in blood. I feared the truth would take your last breath. I will not hide it from you again.";
                            break;
                        case M08DockTruthResponse.StaySilent:
                            body.text = zh
                                ? "沈砾没有再解释。你们并排在海风里坐了很久。"
                                : "Shen Li says nothing more. You sit together in the sea wind for a long time.";
                            break;
                        case M08DockTruthResponse.AdmitSharedFear:
                            body.text = zh
                                ? "沈砾看你一眼，轻轻叹气：那咱们俩算扯平了。"
                                : "Shen Li glances at you and sighs: Then I suppose we are even.";
                            break;
                    }
                    finishLabel.text = zh ? "记下这次回应" : "Record this response";
                    finishButton.gameObject.SetActive(true);
                    finishButton.interactable = true;
                    focus = finishButton;
                    break;

                case M08DockTruthStage.Completed:
                    body.text = zh ? "这晚没有人赢了谁。真实的关系选择已交给剧情宿主记录。"
                                   : "Neither of you won tonight. The choice is now recorded by the story host.";
                    break;
            }

            if (focus != null)
                SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, focus);
        }

        private void SetResponse(int index, string label)
        {
            responseLabels[index].text = label;
            responseButtons[index].gameObject.SetActive(true);
            responseButtons[index].interactable = true;
        }

        private void HearTruth()
        {
            if (disposed || CanvasDestroyed() || !hearButton.interactable) return;
            session.TryHearTruth();
            Refresh();
        }

        private void Respond(int index)
        {
            if (disposed || CanvasDestroyed() ||
                session.Stage != M08DockTruthStage.AwaitingResponse ||
                index < 0 || index >= responseButtons.Length ||
                !responseButtons[index].interactable)
                return;
            session.TryRespond((M08DockTruthResponse)(index + 1));
            Refresh();
        }

        private void Finish()
        {
            if (disposed || CanvasDestroyed() || !finishButton.interactable)
                return;
            // Host owns the authoritative shen_relation write.
            // A failed or interrupted save keeps this confirmation retryable.
            if (session.TryAcknowledge(recordResponse))
            {
                recordResponse = null;
                Refresh();
            }
        }

        private bool CanvasDestroyed()
        {
            if (canvasObject == null || panel == null || heading == null ||
                body == null || hearButton == null || hearLabel == null ||
                finishButton == null || finishLabel == null) return true;
            for (int i = 0; i < responseButtons.Length; i++)
                if (responseButtons[i] == null || responseLabels[i] == null) return true;
            return false;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            recordResponse = null;
            SoundwellUiLocalization.Changed -= Refresh;
            if (hearButton != null) hearButton.onClick.RemoveAllListeners();
            foreach (var button in responseButtons)
                if (button != null) button.onClick.RemoveAllListeners();
            if (finishButton != null) finishButton.onClick.RemoveAllListeners();
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }
}
