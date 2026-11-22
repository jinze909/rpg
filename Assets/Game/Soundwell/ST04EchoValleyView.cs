using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Bottom-safe UGUI expedition panel for ST-04 Echo Valley. Buttons
    /// advance only when the scene has actually established each clue,
    /// rescue, sound experiment, entrance, drain action and beast outcome.
    /// No button can spawn a rescued surveyor or bypass ORK combat.
    /// </summary>
    public sealed class ST04EchoValleyView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly Text heading;
        private readonly Text body;
        private readonly Button[] buttons = new Button[3];
        private readonly Text[] labels = new Text[3];
        private readonly Font latinFont;
        private readonly ST04EchoValleySession session;
        private readonly IST04EchoValleyWorldEvidence world;
        private readonly Func<ST04RoadResolution, bool> canChooseRoad;
        private Action<ST04RoadResolution> onRoadChoice;
        private bool submitting;
        private bool disposed;

        public ST04EchoValleyView(Transform parent,
            ST04EchoValleySession session,
            IST04EchoValleyWorldEvidence world,
            Func<ST04RoadResolution, bool> canChooseRoad,
            Action<ST04RoadResolution> onRoadChoice)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.canChooseRoad = canChooseRoad ??
                throw new ArgumentNullException(nameof(canChooseRoad));
            this.onRoadChoice = onRoadChoice ??
                throw new ArgumentNullException(nameof(onRoadChoice));

            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("ST04 Echo Valley canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 149;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth,
                SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var holder = new GameObject("ST04 Echo Valley bottom panel",
                typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(904f, 256f);
            panel.anchoredPosition = new Vector2(0f, 138f);
            SoundwellUiTheme.CreatePanel(panel, "ST04 valley border",
                new Vector2(904f, 256f), new Color(0.64f, 0.56f, 0.42f, 1f));
            SoundwellUiTheme.CreatePanel(panel, "ST04 valley backing",
                new Vector2(900f, 252f), new Color(0.06f, 0.09f, 0.11f, 0.97f));
            Color ink = new Color(0.93f, 0.89f, 0.81f, 1f);
            heading = SoundwellUiTheme.CreateLabel(panel, 19,
                new Vector2(0f, 100f), new Vector2(840f, 30f), ink);
            heading.alignment = TextAnchor.MiddleLeft;
            body = SoundwellUiTheme.CreateLabel(panel, 14,
                new Vector2(0f, 30f), new Vector2(840f, 106f), ink);
            body.alignment = TextAnchor.UpperLeft;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;

            var positions = new[]
            {
                new Vector2(-282f, -86f),
                new Vector2(0f, -86f),
                new Vector2(282f, -86f)
            };
            for (int i = 0; i < buttons.Length; i++)
            {
                int slot = i;
                buttons[i] = SoundwellUiTheme.CreateButton(
                    panel, "ST04 valley option " + i, positions[i],
                    new Vector2(272f, 42f), 13, out labels[i]);
                buttons[i].onClick.AddListener(() => OnClick(slot));
            }
            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            Refresh();
            SoundwellUiLocalization.Changed += Refresh;
        }

        public ST04EchoValleySession Session => session;
        public RectTransform Panel => panel;
        public Text Heading => heading;
        public Text Body => body;
        public Button FirstOption => buttons[0];
        public Button SecondOption => buttons[1];
        public Button ThirdOption => buttons[2];
        public bool IsDisposed => disposed || CanvasDestroyed();

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed) return;
            if (CanvasDestroyed()) { Dispose(); return; }
            if (float.IsNaN(aspect) || float.IsInfinity(aspect) || aspect <= 0f)
                return;
            SoundwellUiTheme.ApplyViewportAspectScale(panel, aspect);
        }

        public void Refresh()
        {
            if (disposed) return;
            if (CanvasDestroyed()) { Dispose(); return; }
            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = body.font = font;
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i].font = font;
                buttons[i].gameObject.SetActive(false);
                buttons[i].interactable = false;
            }

            heading.text = zh ? "ST-04 · 钟岭的回声会咬人" : "ST-04 · ECHO VALLEY";
            bool present = world.AtValley && world.SangJingAndMengHuiPresent;
            switch (session.Phase)
            {
                case ST04EchoValleyPhase.InspectEmptyMuleCart:
                    body.text = zh
                        ? "被拖空的骡车旁有三处真实痕迹。调查钟绳兽爪、裂隙骡蹄印和震落苔藓。"
                        : "Inspect three actual clues by the empty mule cart: bell-rope claws, hoofprints to the crack and fallen moss.";
                    Choice(0, zh ? "调查钟绳边的兽爪" : "Inspect bell-rope claws",
                        present && world.HasInspected(ST04RoadClue.SlippedClawsAtOldBellRope));
                    Choice(1, zh ? "调查通向裂隙的蹄印" : "Inspect hoofprints to crack",
                        present && world.HasInspected(ST04RoadClue.HoofprintsTurningToRockCrack));
                    Choice(2, zh ? "调查只在一侧震落的苔藓" : "Inspect one-sided fallen moss",
                        present && world.HasInspected(ST04RoadClue.MossFellOnBellFacingSide));
                    break;
                case ST04EchoValleyPhase.ChooseValleyEntry:
                    body.text = zh
                        ? "三处线索齐备。决定如何真正进入山谷；进谷方法会改变遭遇位置，不会消除借声兽。"
                        : "All three clues found. Enter the valley; the approach changes encounter positioning, not the beasts' existence.";
                    Choice(0, zh ? "安静摸进山谷" : "Enter quietly",
                        present && world.HasEnteredVia(ST04ValleyEntry.EnterQuietly));
                    Choice(1, zh ? "从高地观察回声再进谷" : "Survey echoes from ridge",
                        present && world.HasEnteredVia(ST04ValleyEntry.SurveyEchoesFromRidge));
                    Choice(2, zh ? "跟随桑荆铜铃进谷" : "Follow Sang Jing's bell",
                        present && world.HasEnteredVia(ST04ValleyEntry.FollowSangJingAndBell));
                    break;
                case ST04EchoValleyPhase.RescueSurveyors:
                    body.text = zh
                        ? "侧洞有两名活着的测路人。先在场景或 ORK 遭遇中救出两人，再确认救援事实。"
                        : "Two surveyors are alive in the side cave. Complete their actual rescue in the world before confirming.";
                    Choice(0, zh ? "核实两名测路人已获救" : "Verify two rescued surveyors",
                        present && world.SurveyorsActuallyRescued == 2);
                    break;
                case ST04EchoValleyPhase.VerifyEchoBehavior:
                    body.text = zh
                        ? "实际试铃、投石与湿布：连续清亮回声引兽，低语不引兽，湿铃让兽失去目标；核对古雨声放大器与采石改水道。"
                        : "Test bell, stone and wet cloth: clear echoes attract; low voices do not; a damp bell breaks pursuit. Verify ancient rain device and quarry diversion.";
                    Choice(0, zh ? "核实全部声音与水道证据" : "Verify all sound and water clues",
                        present && SoundEvidenceComplete());
                    break;
                case ST04EchoValleyPhase.EnterInnerChamber:
                    body.text = zh
                        ? "选择已实际完成的进内室办法。守路兽交战由 ORK 判定，界面不会替玩家解决战斗。"
                        : "Confirm the real chamber route. ORK controls combat; this panel cannot defeat guards.";
                    Choice(0, zh ? "清除守路兽进入" : "Enter after defeating beast",
                        present && world.HasEnteredChamberVia(ST04ChamberEntry.ClearGuardBeast));
                    Choice(1, zh ? "用铃引兽经过侧廊" : "Lure beast with bell",
                        present && world.HasEnteredChamberVia(ST04ChamberEntry.LureWithBell));
                    Choice(2, zh ? "拆开通风栅绕行" : "Open vent grate",
                        present && world.HasEnteredChamberVia(ST04ChamberEntry.OpenVentGrate));
                    break;
                case ST04EchoValleyPhase.MarkAlliesAndOpenDrain:
                    body.text = zh
                        ? "先标记同伴的位置，再实际开启泄水口。未完成的动作不可只凭按钮判定成功。"
                        : "Mark allies before actually opening the drain. UI clicks cannot simulate either action.";
                    Choice(0, zh ? "确认已标记同伴并泄水" : "Confirm marked allies and drain",
                        present && world.CompanionsMarkedBeforeDrain &&
                        world.DrainActuallyOpened);
                    break;
                case ST04EchoValleyPhase.HandleBeasts:
                    body.text = zh
                        ? "借声兽的实际处置决定哪些取舍可信；仅确认已经完成的战斗、引导或隔离。"
                        : "Only confirm a beast outcome actually reached in gameplay: fighting, guiding home or containment.";
                    Choice(0, zh ? "成兽已被击杀" : "Adult beasts slain",
                        present && world.HasResolvedBeastsAs(ST04BeastHandling.Slain));
                    Choice(1, zh ? "成兽已引回旧栖地" : "Beasts returned to ridge",
                        present && world.HasResolvedBeastsAs(ST04BeastHandling.LedBackToOldHabitat));
                    Choice(2, zh ? "成兽被安全隔离" : "Beasts safely contained",
                        present && world.HasResolvedBeastsAs(ST04BeastHandling.ContainedAwayFromClosedRoad));
                    break;
                case ST04EchoValleyPhase.DecideRoadAndRainWall:
                    body.text = zh
                        ? "代价不免费：关声井会损毁部分听雨壁；引兽上岭须付长路工料；保住听雨壁须永久封路并先安置石匠。"
                        : "No free ending: sealing the well damages the rain wall; the ridge route costs funds; preserving the wall closes the road and needs other work.";
                    Choice(0, zh ? "猎杀成兽、关井通近路" : "Close well; reopen short road",
                        present && canChooseRoad(ST04RoadResolution.KillAdultsAndCloseWell));
                    Choice(1, zh ? "引兽归岭、筹钱走长路" : "Fund long ridge road",
                        present && canChooseRoad(ST04RoadResolution.ReturnBeastsAndUseCostlyLongRoad));
                    Choice(2, zh ? "保留听雨壁、改去别处谋生" : "Preserve wall; close road",
                        present && canChooseRoad(ST04RoadResolution.PreserveRainWallAndCloseRoad));
                    break;
                default:
                    body.text = zh ? "钟岭决定已记录。" : "The valley outcome is recorded.";
                    break;
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].gameObject.activeSelf && buttons[i].interactable)
                {
                    SoundwellInputAdapter.KeepDialogFocus(
                        canvasObject.transform, buttons[i]);
                    break;
                }
            }
        }

        private bool SoundEvidenceComplete()
        {
            return world.ClearContinuousEchoAttractsBeasts &&
                world.LowVoicesDoNotAttractBeasts &&
                world.WetBellLosesBeastTarget &&
                world.AncientRainAmplifierIdentified &&
                world.QuarryWaterDiversionConfirmed;
        }

        private void Choice(int index, string label, bool enabled)
        {
            labels[index].text = label;
            buttons[index].gameObject.SetActive(true);
            buttons[index].interactable = enabled && !submitting;
        }

        private void OnClick(int index)
        {
            if (disposed || submitting || CanvasDestroyed() ||
                index < 0 || index >= buttons.Length ||
                !buttons[index].gameObject.activeSelf ||
                !buttons[index].interactable ||
                !world.AtValley || !world.SangJingAndMengHuiPresent)
                return;
            submitting = true;
            try
            {
                switch (session.Phase)
                {
                    case ST04EchoValleyPhase.InspectEmptyMuleCart:
                        var clue = (ST04RoadClue)(index + 1);
                        if (world.HasInspected(clue))
                            session.TryInspectClue(clue);
                        break;
                    case ST04EchoValleyPhase.ChooseValleyEntry:
                        var entry = (ST04ValleyEntry)(index + 1);
                        if (world.HasEnteredVia(entry))
                            session.TryChooseEntry(entry);
                        break;
                    case ST04EchoValleyPhase.RescueSurveyors:
                        if (index == 0)
                            session.TryRescueSurveyors(world.SurveyorsActuallyRescued);
                        break;
                    case ST04EchoValleyPhase.VerifyEchoBehavior:
                        if (index == 0 && SoundEvidenceComplete())
                            session.TryVerifySoundAndRainMechanism(
                                world.ClearContinuousEchoAttractsBeasts,
                                world.LowVoicesDoNotAttractBeasts,
                                world.WetBellLosesBeastTarget,
                                world.AncientRainAmplifierIdentified,
                                world.QuarryWaterDiversionConfirmed);
                        break;
                    case ST04EchoValleyPhase.EnterInnerChamber:
                        var chamber = (ST04ChamberEntry)(index + 1);
                        if (world.HasEnteredChamberVia(chamber))
                            session.TryEnterInnerChamber(chamber);
                        break;
                    case ST04EchoValleyPhase.MarkAlliesAndOpenDrain:
                        if (index == 0)
                            session.TryOpenDrain(world.CompanionsMarkedBeforeDrain,
                                world.DrainActuallyOpened);
                        break;
                    case ST04EchoValleyPhase.HandleBeasts:
                        var beast = (ST04BeastHandling)(index + 1);
                        if (world.HasResolvedBeastsAs(beast))
                            session.TryResolveBeasts(beast);
                        break;
                    case ST04EchoValleyPhase.DecideRoadAndRainWall:
                        var resolution = (ST04RoadResolution)(index + 1);
                        if (canChooseRoad(resolution))
                            onRoadChoice?.Invoke(resolution);
                        break;
                }
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
            onRoadChoice = null;
            SoundwellUiLocalization.Changed -= Refresh;
            for (int i = 0; i < buttons.Length; i++)
                if (buttons[i] != null)
                    buttons[i].onClick.RemoveAllListeners();
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }
}
