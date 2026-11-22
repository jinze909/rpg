using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Bottom-anchored safe-region UGUI panel for M11 ("这次轮到她来找你").
    /// Drives Huilan Pass three-bell entry, conscription observation (including
    /// RC-05 Hao Lie stance when enlisted), Shen Li's 3-call fog shallow-boat
    /// extraction (tracking Oulin vs shore boatman helm per N53) or fallback
    /// paid-artisan extraction when Shen Li is absent, and post-rescue pact
    /// confirmation with ZH/EN localization.
    /// </summary>
    public sealed class M11ReverseRescueView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text body;
        private readonly Button[] choiceButtons = new Button[3];
        private readonly Text[] choiceLabels = new Text[3];
        private readonly M11ReverseRescueSession session;
        private Action onCommitRequested;
        private bool submitting;
        private bool disposed;

        public M11ReverseRescueSession Session => session;
        public RectTransform Panel => panel;
        public Text Heading => heading;
        public Text Body => body;
        public Button LandGateButton => choiceButtons[0];
        public Button MerchantCaravanButton => choiceButtons[1];
        public Button ReliefPassButton => choiceButtons[2];
        public Button CoverReturneesButton => choiceButtons[0];
        public Button HaoLieRefuseButton => choiceButtons[0];
        public Button HaoLieEnforceButton => choiceButtons[1];
        public Button HearFogCallButton => choiceButtons[0];
        public Button AnswerPresentButton => choiceButtons[0];
        public Button FallbackArtisansButton => choiceButtons[0];
        public Button MutualDivisionButton => choiceButtons[0];
        public Button CallItEvenButton => choiceButtons[1];
        public Button ConfirmRescueButton => choiceButtons[0];
        public bool IsDisposed => disposed || CanvasDestroyed();

        public M11ReverseRescueView(
            Transform parent,
            M11ReverseRescueSession session,
            Action onCommitRequested)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.onCommitRequested = onCommitRequested ??
                throw new ArgumentNullException(nameof(onCommitRequested));

            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("M11 reverse rescue canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 152;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var holder = new GameObject("M11 reverse rescue bottom panel", typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(904f, 256f);
            panel.anchoredPosition = new Vector2(0f, 138f);

            SoundwellUiTheme.CreatePanel(panel, "M11 reverse rescue border",
                new Vector2(904f, 256f), new Color(0.60f, 0.54f, 0.44f, 1f));
            SoundwellUiTheme.CreatePanel(panel, "M11 reverse rescue backing",
                new Vector2(900f, 252f), new Color(0.06f, 0.09f, 0.11f, 0.97f));
            Color ink = new Color(0.93f, 0.89f, 0.81f, 1f);

            heading = SoundwellUiTheme.CreateLabel(panel, 19,
                new Vector2(0f, 102f), new Vector2(842f, 28f), ink);
            heading.alignment = TextAnchor.MiddleLeft;

            body = SoundwellUiTheme.CreateLabel(panel, 15,
                new Vector2(0f, 38f), new Vector2(842f, 88f), ink);
            body.alignment = TextAnchor.UpperLeft;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;

            var positions = new[]
            {
                new Vector2(-282f, -72f),
                new Vector2(0f, -72f),
                new Vector2(282f, -72f)
            };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                choiceButtons[i] = SoundwellUiTheme.CreateButton(
                    panel, "M11 reverse rescue choice " + i, positions[i],
                    new Vector2(272f, 38f), 13, out choiceLabels[i]);
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
            if (float.IsNaN(aspect) || float.IsInfinity(aspect) || aspect <= 0f)
                return;
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

            switch (session.Phase)
            {
                case M11Phase.ThreeBellsAndEntry:
                    heading.text = zh ? "M11 · 回澜关的三声钟" : "M11 · THREE BELLS AT HUILAN PASS";
                    body.text = zh
                        ? "高处钟楼连敲三声信号钟，主航道落栅，陆侧仍在逐人查验。与关外接应人约好退路后，选择进关方式："
                        : "Three signal bells ring from the tower as the sea gate drops. After setting a fallback point outside, choose how to enter:";
                    ShowChoice(0, zh ? "陆侧岗口交涉进关" : "Negotiate at land gate");
                    ShowChoice(1, zh ? "随获准商队进关调查" : "Enter with merchant caravan");
                    ShowChoice(2, zh ? "领急用物资通行许可进关" : "Use relief supply pass");
                    FocusFirstInteractable();
                    break;

                case M11Phase.ConscriptionAndTrap:
                    heading.text = zh ? "M11 · 关内船票与受困" : "M11 · TICKETS & WAREHOUSE TRAP";
                    if (session.RC05EnlistedInPass)
                    {
                        body.text = zh
                            ? "陆钧副手正没收不愿入伍者的船票，亦有旧兵自愿缝回蓝布。你撞见已入陆钧麾下的郝烈第三班，交涉将决定他们是否参与强征。"
                            : "Lu Jun's deputy confiscates tickets while some veterans volunteer. You meet Hao Lie's Third Squad inside the pass.";
                        ShowChoice(0, zh ? "劝阻郝烈，拒绝强征并掩护居民" : "Persuade Hao Lie to refuse");
                        ShowChoice(1, zh ? "交涉未果，第三班参与强征" : "Talks fail; squad enforces");
                    }
                    else
                    {
                        body.text = zh
                            ? "陆钧副手正没收返乡者船票，同时也有旧兵自愿留下。你掩护被扣居民撤离，被守军困在旧仓区临水码头。"
                            : "Lu Jun's deputy confiscates returnee tickets while some veterans stay voluntarily. Covering civilians leaves you trapped at the old dock.";
                        ShowChoice(0, zh ? "记下强征实据并掩护居民退入旧仓" : "Record evidence & cover returnees");
                    }
                    FocusFirstInteractable();
                    break;

                case M11Phase.FogExtraction:
                    heading.text = zh ? "M11 · 浓雾旧运渠接应" : "M11 · FOG CANAL EXTRACTION";
                    if (session.ShenLiAccompanying)
                    {
                        if (session.EchoCallsHeard < 3)
                        {
                            int nextCall = session.EchoCallsHeard + 1;
                            body.text = zh
                                ? "后半夜浓雾涌进旧运渠。沈砾站在浅底船头把你的名字叫进雾里（已闻 " +
                                  session.EchoCallsHeard + "/3 遍）。"
                                : "Dense fog fills the old canal. Shen Li calls your name from the shallow boat (" +
                                  session.EchoCallsHeard + "/3 calls heard).";
                            ShowChoice(0, zh ? "听雾中第 " + nextCall + " 遍呼名" : "Hear fog call " + nextCall + "/3");
                        }
                        else
                        {
                            string helmNote = session.OulinParticipating
                                ? (zh ? "欧林随船认出岸边缺口。" : "Oulin spots the bank gap alongside her.")
                                : (zh ? "欧林未同行，由关外接应船工掌舵。" : "With Oulin absent, the shore boatman takes the helm.");
                            body.text = (zh
                                ? "三遍呼名已至，浅底船摸到码头外一里以内。"
                                : "Three calls heard; the shallow boat is within a li of the dock.") + helmNote;
                            ShowChoice(0, zh ? "高声应答：「在！」登船脱困" : "Answer 'Present!' & board");
                        }
                    }
                    else
                    {
                        body.text = zh
                            ? "沈砾未随行。按预先留下的关外接应方案，付费工匠以更高代价从旧运渠暗口接应脱困。"
                            : "Shen Li is not accompanying you. Pre-arranged paid artisans extract you through the canal at higher cost.";
                        ShowChoice(0, zh ? "启用付费工匠替代接应脱困" : "Extract via fallback artisans");
                    }
                    FocusFirstInteractable();
                    break;

                case M11Phase.PostRescuePact:
                    heading.text = zh ? "M11 · 关外分工与落盘" : "M11 · POST-RESCUE PACT";
                    if (session.RescueProvider == M11RescueProvider.ShenLiShallowBoat &&
                        session.DivisionPact == M11ShenDivisionPact.NoneShenAbsent)
                    {
                        body.text = zh
                            ? "沈砾：「我叫了三遍。下次直接答『在』，就一个字。」你们坐下商量下一次进回澜关的分工，以及谁有权替别人答应危险："
                            : "Shen Li: 'I called three times. Next time just answer Present.' Settle how to divide the next return to Huilan Pass:";
                        ShowChoice(0, zh ? "我找陆钧，外侧水路交你并问欧林" : "Consult & divide water/tower roles");
                        ShowChoice(1, zh ? "这次你先来救我，下回扯平" : "Call it even next time");
                        FocusFirstInteractable();
                    }
                    else
                    {
                        body.text = session.RescueProvider == M11RescueProvider.ShenLiShallowBoat
                            ? (zh ? "沈砾点了点头：「记住你今天说的话。」确认落盘保存 M11 记录。"
                                  : "Shen Li nods: 'Remember what you said today.' Confirm to save M11.")
                            : (zh ? "替代接应已完成脱困，未触发沈砾呼名与和解。确认落盘保存 M11 记录。"
                                  : "Fallback extraction complete without Shen Li's echo or reconciliation. Confirm to save M11.");
                        ShowChoice(0, zh ? "确认落盘结束 M11" : "Confirm & save M11", !submitting);
                        if (!submitting) FocusFirstInteractable();
                    }
                    break;

                case M11Phase.Completed:
                    heading.text = zh ? "M11 · 回澜关脱困已落盘" : "M11 · HUILAN EXTRACTION SAVED";
                    body.text = zh
                        ? "M11 反向救援结果已写入记录。"
                        : "The M11 reverse-rescue outcome has been saved.";
                    break;
            }
        }

        private void ShowChoice(int index, string text, bool interactable = true)
        {
            choiceLabels[index].text = text;
            choiceButtons[index].gameObject.SetActive(true);
            choiceButtons[index].interactable = interactable;
        }

        private void FocusFirstInteractable()
        {
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (choiceButtons[i].gameObject.activeSelf && choiceButtons[i].interactable)
                {
                    SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, choiceButtons[i]);
                    return;
                }
            }
        }

        private void HandleChoiceClick(int index)
        {
            if (disposed || IsDestroyedExternally() || index < 0 || index >= choiceButtons.Length)
                return;
            if (!choiceButtons[index].gameObject.activeSelf || !choiceButtons[index].interactable)
                return;

            switch (session.Phase)
            {
                case M11Phase.ThreeBellsAndEntry:
                    var method = (M11PassEntryMethod)(index + 1);
                    if (session.TryObserveThreeBellsAndEnter(method)) Refresh();
                    break;

                case M11Phase.ConscriptionAndTrap:
                    if (session.RC05EnlistedInPass)
                    {
                        var stance = index == 0
                            ? M11ThirdSquadConscriptionStance.RefusedForcedConscription
                            : M11ThirdSquadConscriptionStance.ParticipatedInEnforcement;
                        if (session.TryObserveConscriptionAndCoverReturnees(stance)) Refresh();
                    }
                    else if (index == 0 &&
                        session.TryObserveConscriptionAndCoverReturnees(
                            M11ThirdSquadConscriptionStance.NotInPass))
                    {
                        Refresh();
                    }
                    break;

                case M11Phase.FogExtraction:
                    if (session.ShenLiAccompanying)
                    {
                        if (session.EchoCallsHeard < 3)
                        {
                            if (index == 0 && session.TryHearShenLiFogCall()) Refresh();
                        }
                        else if (index == 0 && session.TryAnswerPresentForShenLiExtraction())
                        {
                            Refresh();
                        }
                    }
                    else if (index == 0 && session.TryExtractViaFallbackArtisans())
                    {
                        Refresh();
                    }
                    break;

                case M11Phase.PostRescuePact:
                    if (session.RescueProvider == M11RescueProvider.ShenLiShallowBoat &&
                        session.DivisionPact == M11ShenDivisionPact.NoneShenAbsent)
                    {
                        var pact = index == 0
                            ? M11ShenDivisionPact.MutualConsultAndDivision
                            : M11ShenDivisionPact.CallItEvenNextTime;
                        if (session.TryChooseDivisionPact(pact)) Refresh();
                    }
                    else if (index == 0 && !submitting)
                    {
                        submitting = true;
                        Refresh();
                        try
                        {
                            onCommitRequested?.Invoke();
                            if (session.IsCompleted)
                                onCommitRequested = null;
                        }
                        finally
                        {
                            submitting = false;
                            if (!disposed) Refresh();
                        }
                    }
                    break;
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
            onCommitRequested = null;
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
