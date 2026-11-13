using System;
using System.Collections;
using GamingIsLove.Makinom;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    // S00-01 is a deliberately small playable narrative scene inside the
    // existing Bootstrap. This is not a replacement RPG action/quest system:
    // transient presentation is local; dialogue outcomes use Makinom variables.
    public sealed class WhitewaterPrelude : MonoBehaviour
    {
        public const string RollCallKey = "rpg.return.s00.rollcall";
        public const string RepairKey = "rpg.return.s00.cable-repair";
        public const string WorkshopPromiseKey = "rpg.return.s00.workshop-promise";
        public const string HanQiaoRescueKey = "rpg.return.s00.hanqiao-rescue";
        public const string EvacueeEscortedKey = "rpg.return.s00.last-evacuee-escorted";
        public const string SpareRopeCallKey = "rpg.return.s00.spare-rope-call";
        public const string BridgePostKey = "rpg.return.s00.bridge-post";
        public const string SecondBellKey = "rpg.return.s00.second-bell";
        // The post must be physically beside Shen Li (at ClampX + 58), not
        // a disconnected marker halfway across the bridge.
        private const float BridgePostX = ClampX + 30f;
        private const float WalkSpeed = 175f;
        private const float ClampX = 178f;
        private const float RepairReach = 44f;
        private const float EvacueeStartX = -72f;
        private const float EvacueeSafeX = -265f;
        private static readonly Color Ink = new Color(0.87f, 0.90f, 0.88f);
        private static readonly Color Brass = new Color(0.91f, 0.72f, 0.40f);
        private readonly Button[] choices = new Button[3];
        private readonly Text[] choiceLabels = new Text[3];
        private RectTransform marker;
        private RectTransform stretcher;
        private RectTransform hanQiao;
        private RectTransform rescuePoint;
        private RectTransform lastBoat;
        private RectTransform evacuee;
        private RectTransform evacueeSafeLine;
        private RectTransform spareRope;
        private RectTransform bridgePost;
        private RectTransform farBankLanternLeft;
        private RectTransform farBankLanternRight;
        private AudioSource bellAudio;
        private AudioClip firstBellClip;
        private AudioClip secondBellClip;
        private Image cable;
        private Text narration;
        private Text instructions;
        private Text title;
        private Button exit;
        private Button next;
        private Action onExit;
        private int phase;
        private int rescueMethod;
        private bool evacueeWaiting;
        private Vector2 walker = new Vector2(-245f, 0f);

        // 0: roll call; 1: walk; 2: cable decision; 3: stretcher crossing;
        // 4: workshop promise choice; 5: its response; 6: man overboard;
        // 7: rescue station; 8: Han Qiao boards the last boat;
        // 9: reach the stranded evacuee; 10: escort west; 11: confirm safe crossing;
        // 12: voluntary rope call, first bell and physical guard-post approach;
        // 13: reached bridge post; 14: witnessed the second bell and lights going out.
        // ORK defence and the fixed third-bell break remain pending.
        public int Phase => phase;
        public Vector2 WalkerPosition => walker;
        public string CurrentNarration => narration != null ? narration.text : string.Empty;
        public bool CanRepair => phase == 1 && Mathf.Abs(walker.x - ClampX) <= RepairReach &&
            Mathf.Abs(walker.y) <= RepairReach;
        public bool CanRescue => phase == 7 &&
            Mathf.Abs(walker.x - RescueStationX(rescueMethod)) <= RepairReach &&
            Mathf.Abs(walker.y) <= RepairReach;
        public Vector2 EvacueePosition => evacuee != null
            ? evacuee.anchoredPosition : Vector2.zero;
        public bool CanHelpEvacuee => phase == 9 && evacuee != null &&
            Mathf.Abs(walker.x - evacuee.anchoredPosition.x) <= RepairReach &&
            Mathf.Abs(walker.y) <= RepairReach;
        public bool CanDeliverEvacuee => phase == 10 && evacuee != null &&
            // Stopping near the line is not a rescue if the wounded follower
            // has lost the escort and is still waiting for the player to return.
            !evacueeWaiting && walker.x <= EvacueeSafeX &&
            evacuee.anchoredPosition.x <= EvacueeSafeX + 30f &&
            Mathf.Abs(walker.y) <= RepairReach;
        public bool CanTakeBridgePost => phase == 12 && bridgePost != null &&
            Mathf.Abs(walker.x - BridgePostX) <= RepairReach &&
            Mathf.Abs(walker.y) <= RepairReach;

        private static float RescueStationX(int choice) =>
            choice == 0 ? 60f : choice == 1 ? ClampX : -70f;

        private static bool Chinese => SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
        private static string Copy(string en, string zh) => Chinese ? zh : en;

        public void Begin(Action exitAction)
        {
            onExit = exitAction;
            phase = 0;
            // The existing InputSystemUIInputModule owns dialogue navigation.
            SoundwellInputAdapter.SetUiContext();
            SoundwellInputAdapter.EnsureEventSystem(transform);
            // World movement already handles Back through Gameplay input.
            // Dialogue switches to UGUI, where B / Esc emits an EventSystem
            // Cancel instead. Let it bubble from the focused choice/Continue
            // button to this same scene owner; no second input map or handler.
            gameObject.AddComponent<SoundwellActionMenuCancelHandler>().Initialize(Close);

            var canvas = new GameObject("S00 Whitewater playable canvas",
                typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            // S00 shares the existing saved M6 SFX mixer/mute preference with
            // the playable Soundwell trial; no parallel audio bus is created.
            bellAudio = canvas.gameObject.AddComponent<AudioSource>();
            bellAudio.playOnAwake = false;
            bellAudio.spatialBlend = 0f;
            SoundwellAudioBus.Route(bellAudio);
            var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            scaler.matchWidthOrHeight = 0.5f;
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            Panel(canvas.transform, "S00 night backdrop", new Vector2(0f, 0f),
                new Vector2(960f, 540f), new Color(0.045f, 0.070f, 0.095f));

            title = Label(canvas.transform, "S00 · 白水渡", 28,
                new Vector2(0f, 209f), new Vector2(740f, 48f), Brass);
            Label(canvas.transform, Copy("Night roll call · the last bridge",
                "夜点名 · 最后的浮桥"), 17, new Vector2(0f, 168f), new Vector2(750f, 34f), Ink);
            Panel(canvas.transform, "PLACEHOLDER 16px ferry water",
                new Vector2(0f, 34f), new Vector2(790f, 180f),
                new Color(0.11f, 0.21f, 0.26f));
            Panel(canvas.transform, "PLACEHOLDER bridge walkway",
                new Vector2(0f, 12f), new Vector2(720f, 58f),
                new Color(0.34f, 0.29f, 0.23f));
            Panel(canvas.transform, "PLACEHOLDER cable line",
                new Vector2(0f, 67f), new Vector2(690f, 7f),
                new Color(0.56f, 0.55f, 0.50f));
            cable = Panel(canvas.transform, "Loose cable clamp",
                new Vector2(ClampX, 12f), new Vector2(28f, 36f),
                new Color(0.69f, 0.30f, 0.27f));
            Panel(canvas.transform, "PLACEHOLDER Shen Li",
                new Vector2(ClampX + 58f, 18f), new Vector2(24f, 34f),
                Brass);
            stretcher = Panel(canvas.transform, "PLACEHOLDER stretcher wagon",
                new Vector2(-320f, 23f), new Vector2(52f, 27f),
                new Color(0.82f, 0.78f, 0.63f)).rectTransform;
            // S00-03: the officer is visibly in the river, not simply a
            // dialogue flag. He only appears after the workshop conversation.
            lastBoat = Panel(canvas.transform, "S00 last boat",
                new Vector2(295f, 91f), new Vector2(100f, 24f),
                new Color(0.55f, 0.42f, 0.29f)).rectTransform;
            hanQiao = Panel(canvas.transform, "S00 Han Qiao in water",
                new Vector2(-70f, 92f), new Vector2(19f, 29f),
                new Color(0.72f, 0.77f, 0.82f)).rectTransform;
            rescuePoint = Panel(canvas.transform, "S00 rescue station",
                new Vector2(ClampX, 12f), new Vector2(30f, 12f), Brass).rectTransform;
            lastBoat.gameObject.SetActive(false);
            hanQiao.gameObject.SetActive(false);
            rescuePoint.gameObject.SetActive(false);
            // S00-04 opens only after the officer has actually boarded.
            // Both figures are scene-space UGUI markers, with a visible
            // destination; no automatic quest or battle is awarded.
            evacueeSafeLine = Panel(canvas.transform, "S00 evacuation safe line",
                new Vector2(EvacueeSafeX, 12f), new Vector2(14f, 62f),
                new Color(0.66f, 0.84f, 0.59f)).rectTransform;
            evacuee = Panel(canvas.transform, "S00 last evacuee",
                new Vector2(EvacueeStartX, 12f), new Vector2(22f, 32f),
                new Color(0.91f, 0.80f, 0.61f)).rectTransform;
            evacueeSafeLine.gameObject.SetActive(false);
            evacuee.gameObject.SetActive(false);
            // Visible only after the player personally finishes the escort and calls out.
            spareRope = Panel(canvas.transform, "PLACEHOLDER S00 spare rope at clamp",
                new Vector2(ClampX + 30f, 51f), new Vector2(32f, 11f), Brass).rectTransform;
            spareRope.gameObject.SetActive(false);
            bridgePost = Panel(canvas.transform, "S00 bridge defence post",
                new Vector2(BridgePostX, 12f), new Vector2(35f, 40f),
                new Color(0.87f, 0.67f, 0.29f)).rectTransform;
            bridgePost.gameObject.SetActive(false);
            // Opposite-bank lamps visibly withdraw at the scripted second bell.
            // They are scenery, not another combat or mission authority.
            farBankLanternLeft = Panel(canvas.transform, "S00 far bank lantern left",
                new Vector2(-293f, 90f), new Vector2(14f, 15f), Brass).rectTransform;
            farBankLanternRight = Panel(canvas.transform, "S00 far bank lantern right",
                new Vector2(-256f, 90f), new Vector2(14f, 15f), Brass).rectTransform;
            farBankLanternLeft.gameObject.SetActive(false);
            farBankLanternRight.gameObject.SetActive(false);
            marker = Panel(canvas.transform, "PLACEHOLDER player",
                new Vector2(walker.x, 12f), new Vector2(22f, 31f),
                new Color(0.64f, 0.85f, 0.93f)).rectTransform;

            narration = Label(canvas.transform, "", 21,
                new Vector2(0f, -100f), new Vector2(840f, 83f), Ink);
            instructions = Label(canvas.transform, "", 15,
                new Vector2(0f, -143f), new Vector2(790f, 34f), Brass);
            for (int i = 0; i < choices.Length; i++)
            {
                int choice = i;
                choices[i] = Button(canvas.transform, "S00 choice " + i,
                    new Vector2((i - 1) * 250f, -192f), new Vector2(232f, 50f),
                    () => Choose(choice));
                choiceLabels[i] = Label(choices[i].transform, "", 17,
                    Vector2.zero, new Vector2(224f, 48f), Ink);
            }
            next = Button(canvas.transform, "S00 continue to workshop",
                new Vector2(145f, -245f), new Vector2(228f, 29f), ContinueScene);
            Label(next.transform, Copy("Talk about the workshop", "聊聊战后的工坊"), 14,
                Vector2.zero, new Vector2(222f, 27f), Ink);
            next.gameObject.SetActive(false);
            exit = Button(canvas.transform, "S00 return to Bootstrap",
                new Vector2(-145f, -245f), new Vector2(228f, 29f), Close);
            Label(exit.transform, Copy("Back to entry", "返回入口"), 14,
                Vector2.zero, new Vector2(222f, 27f), Ink);
            ShowRollCall();
        }

        private void ShowRollCall()
        {
            narration.text = Copy("Sergeant: Shen Li.  Shen Li: 'Here. Still fixing it.'\nThen the sergeant calls your name.",
                "军士：“沈砾。”沈砾头也不抬：“在，修着呢。”\n军士接着念到你的名字。");
            instructions.text = Copy("Choose what you say; none of these is a skill check.",
                "选择怎样应声。没有口才检定，也不会因此救不了谁。");
            ShowChoices(new[] { "Here.", "Raise your hand.", "Make room first." },
                new[] { "在。", "举手示意", "先给我腾个地方" });
        }

        private void Choose(int choice)
        {
            if (choice < 0 || choice > 2)
                return;
            if (phase == 0)
            {
                if (Maki.Initialized && Maki.Game?.Variables != null)
                    Maki.Game.Variables.Set(RollCallKey, choice + 1);
                phase = 1;
                HideChoices();
                SoundwellInputAdapter.SetGameplayContext();
                narration.text = Copy(
                    choice == 2 ? "Shen Li pushes a wrench toward you. 'This one's called Tax Collector. Two knocks before it works.'"
                    : "Shen Li: 'Here. The other wrench is Tax Collector. It only listens after two knocks.'",
                    "沈砾把一把扳手推到你脚边：“这把叫税务官。敲两下才肯听话。”");
                instructions.text = Copy("WASD / left stick: reach the red clamp. E / Pad A: work. Esc / Pad B: back.",
                    "WASD／左摇杆到红色缆桩。E／手柄 A 加固，Esc／手柄 B 返回。");
            }
            else if (phase == 2)
            {
                if (Maki.Initialized && Maki.Game?.Variables != null)
                    Maki.Game.Variables.Set(RepairKey, choice + 1);
                phase = 3;
                HideChoices();
                cable.color = new Color(0.58f, 0.80f, 0.59f);
                SoundwellInputAdapter.SetUiContext();
                narration.text = Copy(
                    choice == 2 ? "Shen Li demonstrates one turn. You brace the plate together. The stretcher rolls across."
                    : choice == 1 ? "You set the wooden wedge. The cable draws tight and the stretcher rolls across."
                    : "You hold the plate while Shen Li tightens the cable. The stretcher rolls across.",
                    choice == 2 ? "沈砾先示范一圈。你扶住夹板，一起收紧缆索。第一辆担架车开始过桥。"
                    : choice == 1 ? "你塞进木楔，缆索重新绷紧。第一辆担架车开始过桥。"
                    : "你扶稳夹板，沈砾收紧缆索。第一辆担架车开始过桥。");
                instructions.text = Copy("The stretcher is crossing. Then ask Shen Li about life after the war.",
                    "担架车正过桥。等它过去，再问沈砾战后的打算。");
                StartCoroutine(CrossStretcher());
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(exit.gameObject);
            }
            else if (phase == 4)
            {
                if (Maki.Initialized && Maki.Game?.Variables != null)
                    Maki.Game.Variables.Set(WorkshopPromiseKey, choice + 1);
                phase = 5;
                HideChoices();
                narration.text = Copy(
                    choice == 0 ? "Shen Li: 'Then I'll remember. Don't complain if the jobs are small.'"
                    : choice == 1 ? "Shen Li: 'Fair. Don't make a promise just for me.'"
                    : "Shen Li: 'You can explain to customers why their pots are still at war.'",
                    choice == 0 ? "沈砾：“那我先记着。到时别嫌活小。”"
                    : choice == 1 ? "沈砾：“行，别为了我答应。”"
                    : "沈砾：“你来负责跟客人解释，为什么他们的锅还在打仗。”");
                instructions.text = Copy("S00-02 complete. Your reply matters to this conversation, not the bridge's fate.",
                    "S00-02 完成：你的回答改变这一刻的对话，不改变断桥的历史。");
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(exit.gameObject);
                next.gameObject.name = "S00 continue to last boat";
                Text nextLabel = next.GetComponentInChildren<Text>(true);
                if (nextLabel != null)
                    nextLabel.text = Copy("Help at the last boat", "去最后一条船救人");
                next.gameObject.SetActive(true);
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(next.gameObject);
            }
            else if (phase == 6)
            {
                // A method is not a rescue: the player must reach a real
                // station and confirm the physical interaction first.
                rescueMethod = choice;
                phase = 7;
                HideChoices();
                SoundwellInputAdapter.SetGameplayContext();
                rescuePoint.anchoredPosition = new Vector2(RescueStationX(choice), 12f);
                rescuePoint.gameObject.SetActive(true);
                narration.text = Copy(
                    choice == 0 ? "Grab the wet line and haul Han Qiao yourself."
                    : choice == 1 ? "Shen Li throws the rope. Help her draw Han Qiao out."
                    : "The soldiers take your call. Join their lift at the rail.",
                    choice == 0 ? "你抓住湿绳，准备亲手拉韩峤上岸。"
                    : choice == 1 ? "沈砾抛出绳索。你得帮她一起把韩峤拉出来。"
                    : "旁边的士兵听见你叫人。到栏杆边一起拉。");
                instructions.text = Copy("WASD / left stick to the gold station; E / Pad A to rescue. Esc / Pad B to leave.",
                    "WASD／左摇杆到金色救援点，E／手柄 A 拉人，Esc／手柄 B 返回。");
            }
        }

        private void ContinueScene()
        {
            if (phase == 3)
                ShowWorkshopPromise();
            else if (phase == 5)
                ShowLastBoat();
            else if (phase == 8)
                ShowFinalRetreat();
            else if (phase == 11)
                CallForSpareRope();
            else if (phase == 13)
                WitnessSecondBell();
        }

        private void ShowLastBoat()
        {
            if (phase != 5 || next == null || !next.gameObject.activeInHierarchy)
                return;
            phase = 6;
            next.gameObject.SetActive(false);
            hanQiao.gameObject.SetActive(true);
            lastBoat.gameObject.SetActive(true);
            SoundwellInputAdapter.SetUiContext();
            narration.text = Copy(
                "A splash. Han Qiao clings to broken timber; his soaked uniform pulls him down. He shouts only: 'Rope!'",
                "一声落水。韩峤扒着断木，浸水的军服把他往下拖。他没有喊官衔，只喊：“绳子！”");
            instructions.text = Copy("Choose who takes the line. You still have to pull him out.",
                "决定谁来搭手。选完还得亲自完成救援。");
            ShowChoices(new[] { "Pull him myself", "Ask Shen Li to throw rope", "Call nearby soldiers" },
                new[] { "我自己拉", "请沈砾抛绳", "喊旁边士兵搭手" });
        }

        private void FinishRescue()
        {
            if (!CanRescue)
                return;
            if (Maki.Initialized && Maki.Game?.Variables != null)
                Maki.Game.Variables.Set(HanQiaoRescueKey, rescueMethod + 1);
            phase = 8;
            rescuePoint.gameObject.SetActive(false);
            SoundwellInputAdapter.SetUiContext();
            narration.text = Copy(
                rescueMethod == 0 ? "You heave Han Qiao clear. He is badly hurt, but alive."
                : rescueMethod == 1 ? "Shen Li's rope holds. Together you pull Han Qiao clear, badly hurt but alive."
                : "The soldiers lift on your count. Han Qiao comes free, badly hurt but alive.",
                rescueMethod == 0 ? "你把韩峤拉上来了。他伤得很重，但还活着。"
                : rescueMethod == 1 ? "沈砾的绳索绷住了。你们一起把韩峤拉出来。他伤得很重，但活着。"
                : "士兵听你号令，一起把韩峤拉了上来。他伤得很重，但活着。");
            instructions.text = Copy("The last boat is boarding Han Qiao. He will not witness the cable breaking.",
                "韩峤上了最后一条船。他不会留在桥头看见断缆。");
            StartCoroutine(BoatTakesHanQiao());
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(exit.gameObject);
        }

        private IEnumerator BoatTakesHanQiao()
        {
            Vector2 start = hanQiao.anchoredPosition;
            float elapsed = 0f;
            while (elapsed < 2f && hanQiao != null)
            {
                elapsed += Time.unscaledDeltaTime;
                hanQiao.anchoredPosition = Vector2.Lerp(start,
                    new Vector2(290f, 100f), Mathf.Clamp01(elapsed / 2f));
                yield return null;
            }
            if (phase == 8 && next != null)
            {
                instructions.text = Copy(
                    "Han Qiao is aboard. One more wounded evacuee is still on the bridge.",
                    "韩峤已经上船。桥上还有一名伤者没撤离。");
                next.gameObject.name = "S00 continue to final retreat";
                Text label = next.GetComponentInChildren<Text>(true);
                if (label != null)
                    label.text = Copy("Cover the last evacuee", "掩护最后一名撤离者");
                next.gameObject.SetActive(true);
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(next.gameObject);
            }
        }

        private void ShowFinalRetreat()
        {
            if (phase != 8 || next == null || !next.gameObject.activeInHierarchy)
                return;
            phase = 9;
            next.gameObject.SetActive(false);
            evacuee.anchoredPosition = new Vector2(EvacueeStartX, 12f);
            evacuee.gameObject.SetActive(true);
            evacueeSafeLine.gameObject.SetActive(true);
            SoundwellInputAdapter.SetGameplayContext();
            narration.text = Copy(
                "The last boat pulls away. Enemy troops close in; a wounded evacuee is stuck beside the railing.",
                "最后一条船离岸。敌军正在逼近，一名伤者还困在桥边的栏杆旁。");
            instructions.text = Copy(
                "Reach the amber evacuee, press E / Pad A, then lead them west to the green safe line.",
                "走到金色伤者旁按 E／手柄 A，再带他向西走到绿色安全线。");
        }

        private void HelpEvacuee()
        {
            if (!CanHelpEvacuee)
                return;
            phase = 10;
            evacueeWaiting = false;
            narration.text = Copy(
                "You take the evacuee's arm. 'Keep behind me. Don't stop at the open span.'",
                "你扶住伤者的胳膊：“跟在我后面，别停在空桥上。”");
            instructions.text = Copy(
                "Lead west. Keep close or they will stop; press E / Pad A at the safe line.",
                "向西带路，离太远他会停下；到安全线后按 E／手柄 A 确认撤离。");
        }

        private void UpdateEvacueeFollow()
        {
            if (phase != 10 || evacuee == null)
                return;
            float gap = Mathf.Abs(walker.x - evacuee.anchoredPosition.x);
            bool waiting = evacueeWaiting
                ? gap > 65f : gap > 115f;
            if (waiting != evacueeWaiting)
            {
                evacueeWaiting = waiting;
                instructions.text = waiting
                    ? Copy("Too far ahead. Go back for the evacuee.",
                        "你走得太远了。回去接上伤者。")
                    : Copy("Lead west, stay close, then press E / Pad A at the green line.",
                        "向西带路，保持靠近，到绿色线按 E／手柄 A。");
            }
            if (evacueeWaiting)
                return;
            Vector2 at = evacuee.anchoredPosition;
            // Keep the wounded follower within a body-width in either
            // direction. Doubling back east to regroup must not abandon them
            // at their old position; an escort should recover without replay.
            // The waiting gap above still prevents teleporting a lost ally.
            const float followOffset = 24f;
            float followTarget = at.x;
            if (walker.x < at.x - followOffset)
                followTarget = walker.x + followOffset;
            else if (walker.x > at.x + followOffset)
                followTarget = walker.x - followOffset;
            float newX = Mathf.MoveTowards(at.x, followTarget, 220f * Time.unscaledDeltaTime);
            evacuee.anchoredPosition = new Vector2(newX, 12f);
        }

        private void DeliverEvacuee()
        {
            if (!CanDeliverEvacuee)
                return;
            if (Maki.Initialized && Maki.Game?.Variables != null)
                Maki.Game.Variables.Set(EvacueeEscortedKey, 1);
            phase = 11;
            SoundwellInputAdapter.SetUiContext();
            narration.text = Copy(
                "One more person crosses the safe line. Enemy troops close in; Shen Li still watches the bridge cable.",
                "又一人越过安全线。敌军越逼越近，沈砾仍在留意桥上的缆索。");
            instructions.text = Copy(
                "Call for spare rope to help the bridge crew. The ORK defence is a later slice.",
                "呼喊多余的绳索，提醒桥头同伴。ORK 防守战仍属后续切片。");
            next.gameObject.name = "S00 call for spare rope";
            Text label = next.GetComponentInChildren<Text>(true);
            if (label != null)
                label.text = Copy("Who has spare rope?", "谁带了多余的绳子？");
            next.gameObject.SetActive(true);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(next.gameObject);
        }

        private void CallForSpareRope()
        {
            // The cue is player initiated and cannot be earned by choosing a
            // dialogue option, skipping the escort, or waiting out a timer.
            if (phase != 11 || next == null || !next.gameObject.activeInHierarchy)
                return;
            phase = 12;
            next.gameObject.SetActive(false);
            if (Maki.Initialized && Maki.Game?.Variables != null)
                Maki.Game.Variables.Set(SpareRopeCallKey, 1);
            spareRope.gameObject.SetActive(true);
            bridgePost.gameObject.SetActive(true);
            cable.color = new Color(0.78f, 0.43f, 0.28f);
            firstBellClip = CreateFirstBellCue();
            bellAudio.clip = firstBellClip;
            bellAudio.Play();
            narration.text = Copy(
                "You shout, 'Who has spare rope?' A coil reaches Shen Li at the clamp. The first bell sounds; she turns toward the strained cable.",
                "你喊：“谁带了多余的绳子？”一卷绳索送到缆桩边的沈砾手里。第一声号钟响起，她转头望向吃紧的缆索。");
            instructions.text = Copy(
                "Reach the amber bridge post near Shen Li; press E / Pad A to take position. ORK defence remains pending.",
                "走到沈砾附近金色桥头守位，按 E／手柄 A 就位。ORK 防守战仍待实现。");
            SoundwellInputAdapter.SetGameplayContext();
        }

        private void TakeBridgePost()
        {
            // Reachability uses the same movement and physical proximity as
            // earlier rescues. A dialogue click or the first bell alone cannot
            // mark the player as posted or pretend an ORK battle was won.
            if (!CanTakeBridgePost)
                return;
            phase = 13;
            bridgePost.GetComponent<Image>().color = new Color(0.42f, 0.82f, 0.66f);
            if (Maki.Initialized && Maki.Game?.Variables != null)
                Maki.Game.Variables.Set(BridgePostKey, 1);
            SoundwellInputAdapter.SetUiContext();
            narration.text = Copy(
                "You take the bridge post beside Shen Li. The evacuee is safe; you face the approaching troops together.",
                "你在沈砾身旁站定桥头守位。伤者已撤离，你们一起望向逼近的敌军。");
            instructions.text = Copy(
                "Hold the post. Choose 'Watch the opposite bank' when ready; the third bell is still ahead.",
                "守住桥头。准备好后选择“观察对岸”，第三声号钟尚未响起。");
            farBankLanternLeft.gameObject.SetActive(true);
            farBankLanternRight.gameObject.SetActive(true);
            next.gameObject.name = "S00 watch opposite bank";
            Text buttonLabel = next.GetComponentInChildren<Text>(true);
            if (buttonLabel != null)
                buttonLabel.text = Copy("Watch the opposite bank", "观察对岸");
            next.gameObject.SetActive(true);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(next.gameObject);
        }

        private void WitnessSecondBell()
        {
            // The second bell belongs to the established S00 history, not a
            // cooldown or a reward for standing still. Only the player who
            // physically reached the post can advance this scene beat.
            if (phase != 13 || next == null || !next.gameObject.activeInHierarchy)
                return;
            phase = 14;
            next.gameObject.SetActive(false);
            // The narration says the far-bank lamps disappear one by one.
            // Make the retreat legible on two visual beats; this is only
            // presentation, not a new battle timer or quest authority.
            StartCoroutine(ExtinguishFarBankLamps());
            secondBellClip = CreateSecondBellCue();
            bellAudio.clip = secondBellClip;
            bellAudio.Play();
            if (Maki.Initialized && Maki.Game?.Variables != null)
                Maki.Game.Variables.Set(SecondBellKey, 1);
            narration.text = Copy(
                "The second bell rolls across the bridge. On the far bank, the lamps withdraw one by one. Shen Li grips the cable and looks across the span.",
                "第二声号钟传过浮桥。对岸的灯火一盏接一盏收起。沈砾攥住缆索，望向桥的另一端。");
            instructions.text = Copy(
                "The second bell has sounded. The ORK defence and fixed third-bell break are later playable beats. Return to entry.",
                "第二声号钟已响。ORK 防守战与既定的第三声断桥仍属后续可玩段落；可返回入口。");
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(exit.gameObject);
        }

        private IEnumerator ExtinguishFarBankLamps()
        {
            farBankLanternLeft.gameObject.SetActive(false);
            // ORK can pause scaled time; the historical scene cue must still
            // finish without waiting for the battle clock.
            yield return new WaitForSecondsRealtime(0.35f);
            if (farBankLanternRight != null)
                farBankLanternRight.gameObject.SetActive(false);
        }

        private static AudioClip CreateFirstBellCue() =>
            CreateWarningBellCue("PLACEHOLDER S00 first warning bell", 510f);

        private static AudioClip CreateSecondBellCue() =>
            CreateWarningBellCue("PLACEHOLDER S00 second warning bell", 440f);

        private static AudioClip CreateWarningBellCue(string clipName, float frequency)
        {
            // An original short nonverbal scene cue, not a new encounter clock
            // or RPG authority. First/second bells use the same saved M6 SFX bus.
            const int sampleRate = 22050;
            const float duration = 0.9f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float seconds = (float)i / sampleRate;
                float phase = seconds * frequency * Mathf.PI * 2f;
                float envelope = Mathf.Min(seconds * 1600f, 1f) *
                    Mathf.Exp(-5f * seconds / duration);
                samples[i] = 0.19f * envelope *
                    (Mathf.Sin(phase) + 0.33f * Mathf.Sin(phase * 2.73f));
            }
            AudioClip clip = AudioClip.Create(clipName,
                sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnDestroy()
        {
            // AudioSource belongs to the scene canvas and stops with it.
            // Release the generated sample clip when the temporary scene ends.
            if (firstBellClip != null)
                Destroy(firstBellClip);
            if (secondBellClip != null)
                Destroy(secondBellClip);
        }

        private void ShowWorkshopPromise()
        {
            if (phase != 3 || next == null || !next.gameObject.activeInHierarchy)
                return;
            phase = 4;
            next.gameObject.SetActive(false);
            SoundwellInputAdapter.SetUiContext();
            narration.text = Copy(
                "Shen Li checks the tight cable. 'After the war I want a shop: pots, doors, ordinary jobs. No more half-bridges. Will you come?'",
                "沈砾试了试缆口：“打完这仗我开间铺子。接普通单，补锅，修门。谁再抬半座桥来，我就关门。你来不来？”");
            instructions.text = Copy("Answer honestly. The rescue and the later reunion are not decided here.",
                "照你的心意回答。这句话不会替你决定后来谁能获救、怎样重逢。");
            ShowChoices(new[] { "I'll come", "I can't promise", "What about my wages?" },
                new[] { "我会来", "我现在答应不了", "那我的工钱怎么算？" });
        }

        private void ShowChoices(string[] english, string[] chinese)
        {
            for (int i = 0; i < choices.Length; i++)
            {
                choices[i].gameObject.SetActive(true);
                choiceLabels[i].text = Chinese ? chinese[i] : english[i];
            }
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(choices[0].gameObject);
        }

        private void HideChoices()
        {
            foreach (Button choice in choices)
                choice.gameObject.SetActive(false);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        private void Update()
        {
            if (phase != 1 && phase != 7 && phase != 9 && phase != 10 && phase != 12)
                return;
            SoundwellControlFrame frame = SoundwellInputAdapter.ReadGameplay();
            // In world-movement context UGUI Submit/Cancel is not active.
            // Let keyboard Esc and controller B exit without a mouse.
            if (frame.Back)
            {
                Close();
                return;
            }
            Vector2 delta = Vector2.ClampMagnitude(frame.Movement, 1f) *
                WalkSpeed * Time.unscaledDeltaTime;
            walker = new Vector2(Mathf.Clamp(walker.x + delta.x, -325f, 275f),
                Mathf.Clamp(walker.y + delta.y, -43f, 43f));
            marker.anchoredPosition = new Vector2(walker.x, 12f + walker.y);
            if (phase == 12)
            {
                if (frame.Ring)
                    TakeBridgePost();
                return;
            }
            if (phase == 7)
            {
                if (frame.Ring && CanRescue)
                    FinishRescue();
                return;
            }
            if (phase == 9)
            {
                if (frame.Ring)
                    HelpEvacuee();
                return;
            }
            if (phase == 10)
            {
                UpdateEvacueeFollow();
                if (frame.Ring)
                    DeliverEvacuee();
                return;
            }
            if (frame.Ring && CanRepair)
            {
                phase = 2;
                SoundwellInputAdapter.SetUiContext();
                narration.text = Copy("Shen Li: 'The clamp is loose. How shall we brace it?'",
                    "沈砾：“夹口松了。你想怎么固定？”");
                instructions.text = Copy("The different methods all make the bridge safe.",
                    "做法可以不同，目标都是让伤员安全通过。");
                ShowChoices(new[] { "Hold the plate", "Set the wedge", "Ask her to show you" },
                    new[] { "扶住夹板", "塞进木楔", "请她先示范" });
            }
        }

        private IEnumerator CrossStretcher()
        {
            float elapsed = 0f;
            while (elapsed < 2f && stretcher != null)
            {
                elapsed += Time.unscaledDeltaTime;
                stretcher.anchoredPosition = new Vector2(Mathf.Lerp(-320f, 308f, Mathf.Clamp01(elapsed / 2f)), 23f);
                yield return null;
            }
            if (phase == 3 && next != null)
            {
                next.gameObject.SetActive(true);
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(next.gameObject);
            }
        }

        public void Close()
        {
            // Leaving the unfinished prologue never starts an ORK encounter or
            // awards quest experience. Makinom retains only dialogue/repair
            // outcomes already earned through real player interactions.
            Action callback = onExit;
            onExit = null;
            callback?.Invoke();
            Destroy(gameObject);
        }

        private static Image Panel(Transform parent, string name, Vector2 pos,
            Vector2 size, Color color)
        {
            var objectWithImage = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            objectWithImage.transform.SetParent(parent, false);
            var image = objectWithImage.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            image.rectTransform.sizeDelta = size;
            image.rectTransform.anchoredPosition = pos;
            return image;
        }

        private static Text Label(Transform parent, string message, int fontSize,
            Vector2 pos, Vector2 size, Color color)
        {
            Text label = SoundwellView.AddLabel(parent, message, fontSize, color, pos, size);
            label.font = SoundwellUiLocalization.PresentationFont(label.font);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Mathf.Max(12, fontSize - 5);
            label.resizeTextMaxSize = fontSize;
            return label;
        }

        private static Button Button(Transform parent, string name, Vector2 pos,
            Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var obj = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            obj.GetComponent<Image>().color = new Color(0.15f, 0.24f, 0.30f);
            var button = obj.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.96f, 0.88f, 0.66f);
            colors.selectedColor = new Color(0.96f, 0.82f, 0.49f);
            button.colors = colors;
            button.onClick.AddListener(action);
            return button;
        }
    }
}
