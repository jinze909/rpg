using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Player-facing M06 pump-room encounter, driven exclusively by the
    /// verified M06ReunionSession. The host owns Makinom writes and must
    /// supply the save callback; this view never claims M07 work is complete.
    /// </summary>
    public sealed class M06PumpRoomReunionView : IDisposable
    {
        private readonly M06PumpRoomReunionSession session;
        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly Text heading;
        private readonly Text narrative;
        private readonly Button[] choices = new Button[4];
        private readonly Text[] choiceLabels = new Text[4];
        private readonly Font latinFont;
        private Func<M06RescuePlan, bool> onSavePlan;
        private bool saving;
        private bool disposed;

        public M06ReunionStage Stage => session.Stage;
        public Text Narrative => narrative;
        public RectTransform Panel => panel;
        public Button[] ChoiceButtons => choices;

        public M06PumpRoomReunionView(
            Transform parent,
            M06PumpRoomReunionSession session,
            Func<M06RescuePlan, bool> onSavePlan)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.onSavePlan = onSavePlan ?? throw new ArgumentNullException(nameof(onSavePlan));

            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = SoundwellUiTheme.CreateOverlayCanvas(
                parent, "M06 pump-room reunion canvas", 134, out panel);
            heading = SoundwellUiTheme.CreateLabel(panel, 25,
                new Vector2(0f, 212f), new Vector2(842f, 43f));
            narrative = SoundwellUiTheme.CreateLabel(panel, 18,
                new Vector2(0f, 87f), new Vector2(834f, 194f));
            narrative.alignment = TextAnchor.MiddleLeft;
            narrative.horizontalOverflow = HorizontalWrapMode.Wrap;
            narrative.verticalOverflow = VerticalWrapMode.Overflow;

            for (int i = 0; i < choices.Length; i++)
            {
                int index = i;
                choices[i] = SoundwellUiTheme.CreateButton(panel,
                    "M06 reunion choice " + (i + 1),
                    new Vector2(0f, -81f - 49f * i),
                    new Vector2(814f, 43f), 16, out choiceLabels[i]);
                choices[i].onClick.AddListener(() => Choose(index));
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
            heading.font = narrative.font = font;
            for (int i = 0; i < choices.Length; i++)
            {
                choices[i].gameObject.SetActive(false);
                choices[i].interactable = false;
                choiceLabels[i].font = font;
            }

            heading.text = zh
                ? "赤盐矿营 · 泵房重逢"
                : "RED SALT MINE CAMP · PUMP-ROOM REUNION";
            switch (session.Stage)
            {
                case M06ReunionStage.VesselBriefing:
                    narrative.text = (zh
                        ? "沈砾没先看你，先看你身后的水面。\n沈砾：几个人？吃水线多深？\n"
                        : "Shen Li looks past you to the water. How many arrived, and what is their draft?\n")
                        + session.FormatVesselBriefing(zh);
                    SetChoice(0, zh ? "沈砾，我来接你了。" : "I came to bring you back.");
                    SetChoice(1, zh ? "三个月没见，你的扳手名字还是难听。" : "Three months and your wrench names are still awful.");
                    SetChoice(2, session.FormatWithdrawalOrChannelChoice(zh));
                    break;
                case M06ReunionStage.PumpRoomWitnesses:
                    narrative.text = zh
                        ? "小七递上名叫“税务官”的扳手。欧林在修锁，梁伯偷煎盐鱼。易达留下茶包，沈砾没碰，也没扔。\n主泵仍在排水；眼前的检修不等于停机撤离。"
                        : "Xiao Qi passes the wrench called Tax Collector. Oulin tends the locks while Liang cooks salt fish. Yi Da leaves tea untouched.\nThe main pump is still running: inspection is not evacuation.";
                    SetChoice(0, zh ? "查看泵房与班组" : "Observe the pump room and crew");
                    break;
                case M06ReunionStage.LetterHandoff:
                    narrative.text = zh
                        ? "你实际带来了瑟兰托付的八封信。陶霁读了那封修好门槛的信；一位矿工收下账页；另有一封被退回，仍需待投。\n不能把八封都记成已送达。"
                        : "You carried Seran's eight letters. Tao Ji reads about the repaired doorstep; a miner takes a household ledger. One unopened letter is returned and remains pending.\nDo not mark all eight as delivered.";
                    SetChoice(0, zh ? "按收件人交信，保留待投那封" : "Deliver by recipient; keep the returned letter pending");
                    break;
                case M06ReunionStage.OulinRetaliationTruth:
                    narrative.text = zh
                        ? "欧林告诉你：沈砾修泵让许多人吃饱，也让矿主的账更漂亮。你逃走后矿主扣掉半数口粮，两位老矿工没有熬过去。\n沈砾不愿一走了之，后面的人仍靠主泵排水。"
                        : "Oulin tells you Shen Li's repairs fed miners but enriched the owner. After your escape, half the rations were withheld; two elder miners died.\nShen Li refuses to abandon those still dependent on the main pump.";
                    SetChoice(0, zh ? "听完实情，和沈砾商量营救" : "Hear the truth and discuss rescue with Shen Li");
                    break;
                case M06ReunionStage.RescuePlanDecision:
                    narrative.text = zh
                        ? "主泵无准备停转会危及下层五百人。任何停机方案必须先撤人、培训接手人并接好手摇备用泵。\n四条路线互斥；此处只敲定计划，M07 才执行。"
                        : "Stopping the main pump without preparation endangers 500 miners below. A shutdown requires prior evacuation, trained operators and a hand-pump backup.\nChoose one plan; only M07 carries it out.";
                    SetChoice(0, M06PumpRoomReunionSession.FormatPlanLabel(M06RescuePlan.OfficialHandover, zh));
                    SetChoice(1, M06PumpRoomReunionSession.FormatPlanLabel(M06RescuePlan.CargoCover, zh));
                    SetChoice(2, M06PumpRoomReunionSession.FormatPlanLabel(M06RescuePlan.MinerShutdown, zh));
                    SetChoice(3, M06PumpRoomReunionSession.FormatPlanLabel(M06RescuePlan.ForceBreakout, zh));
                    break;
                case M06ReunionStage.Completed:
                    narrative.text = zh
                        ? "营救方案已记录。实际接应、停机与人员撤离尚待 M07 执行，不能预先算作成功。"
                        : "The rescue plan is recorded. Pickup, shutdown and evacuation are not completed until their M07 actions.";
                    break;
                default:
                    throw new InvalidOperationException("Unexpected M06 reunion stage.");
            }

            if (!saving && session.Stage != M06ReunionStage.Completed)
            {
                for (int i = 0; i < choices.Length; i++)
                    if (choices[i].gameObject.activeSelf)
                    {
                        SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, choices[i]);
                        break;
                    }
            }
        }

        private void SetChoice(int index, string label)
        {
            choices[index].gameObject.SetActive(true);
            choices[index].interactable = !saving;
            choiceLabels[index].text = label;
        }

        private void Choose(int index)
        {
            if (disposed || saving || CanvasDestroyed() ||
                index < 0 || index >= choices.Length ||
                !choices[index].gameObject.activeSelf)
                return;

            switch (session.Stage)
            {
                case M06ReunionStage.VesselBriefing:
                    session.AnswerVesselBriefing((M06ReunionGreeting)index);
                    break;
                case M06ReunionStage.PumpRoomWitnesses:
                    session.WitnessPumpRoomAndInspection();
                    break;
                case M06ReunionStage.LetterHandoff:
                    session.DeliverCarriedLetters();
                    break;
                case M06ReunionStage.OulinRetaliationTruth:
                    session.HearOulinRetaliationTruth();
                    break;
                case M06ReunionStage.RescuePlanDecision:
                    CommitPlan((M06RescuePlan)(index + 1));
                    break;
                default:
                    return;
            }

            Refresh();
        }

        private void CommitPlan(M06RescuePlan plan)
        {
            if (onSavePlan == null)
                return;
            saving = true;
            Refresh();
            try
            {
                bool accepted = onSavePlan(plan);
                if (accepted && session.Stage == M06ReunionStage.Completed)
                    onSavePlan = null;
            }
            finally
            {
                saving = false;
                Refresh();
            }
        }

        private bool CanvasDestroyed()
        {
            if (canvasObject == null || panel == null || heading == null ||
                narrative == null)
                return true;
            for (int i = 0; i < choices.Length; i++)
                if (choices[i] == null || choiceLabels[i] == null)
                    return true;
            return false;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            onSavePlan = null;
            SoundwellUiLocalization.Changed -= Refresh;
            foreach (Button button in choices)
                if (button != null) button.onClick.RemoveAllListeners();
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }
}
