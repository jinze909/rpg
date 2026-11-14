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
        public const string FarBankWarningKey = "rpg.return.s00.far-bank-warning";
        public const string CableHoldKey = "rpg.return.s00.cable-hold";
        // The post must be physically beside Shen Li (at ClampX + 58), not
        // a disconnected marker halfway across the bridge.
        private const float BridgePostX = ClampX + 30f;
        // A sightline forward of Shen Li's eastern clamp. Players must
        // approach it physically after the second warning bell.
        private const float FarBankLookoutX = 75f;
        // Warned soldiers advance visibly while the player returns to Shen Li,
        // but only a later ORK battle can resolve the encounter.
        private const float FarBankThreatApproach = 58f;
        // After the player actually braces Shen Li's cable, the three
        // figures close another short gap. They remain scenery, not a battle.
        private const float FarBankThreatAfterHold = 20f;
        private const float FarBankThreatSpeed = 24f;
        private const float WalkSpeed = 175f;
        private const float ClampX = 178f;
        private const float RepairReach = 44f;
        private const float EvacueeStartX = -72f;
        private const float EvacueeSafeX = -265f;
        private const float SpareRopeTravelSeconds = 0.55f;
        private static readonly Vector2 SpareRopeDispatch = new Vector2(-290f, 51f);
        private static readonly Vector2 SpareRopeDock = new Vector2(ClampX + 30f, 51f);
        private static readonly Color Ink = new Color(0.87f, 0.90f, 0.88f);
        private static readonly Color Brass = new Color(0.91f, 0.72f, 0.40f);
        // A wounded follower needs visible readiness and separation feedback.
        private static readonly Color EvacueeFollowingTint = new Color(0.72f, 0.94f, 0.76f);
        private static readonly Color EvacueeWaitingTint = new Color(1f, 0.60f, 0.35f);
        private readonly Button[] choices = new Button[3];
        private readonly Text[] choiceLabels = new Text[3];
        private RectTransform marker;
        private RectTransform stretcher;
        private RectTransform hanQiao;
        private RectTransform rescuePoint;
        private RectTransform shenLiRescueLine;
        private Texture2D shenLiRescueRopeTexture;
        private RectTransform lastBoat;
        private Texture2D lastBoatPixelTexture;
        private RectTransform evacuee;
        private RectTransform evacueeSafeLine;
        private RectTransform spareRope;
        private RectTransform bridgePost;
        private RectTransform farBankLanternLeft;
        private RectTransform farBankLanternRight;
        private RectTransform farBankLookout;
        private readonly Image[] farBankThreats = new Image[3];
        private AudioSource bellAudio;
        private AudioClip firstBellClip;
        private AudioClip secondBellClip;
        private Sprite playerPixelSprite;
        private Sprite shenLiPixelSprite;
        private Sprite shenLiBracingSprite;
        private Texture2D playerPixelTexture;
        private Texture2D shenLiPixelTexture;
        private Texture2D shenLiBracingTexture;
        private Image shenLiFigure;
        private Sprite hanQiaoPixelSprite;
        private Sprite evacueePixelSprite;
        private Sprite farBankGuardSprite;
        private Sprite spareRopePixelSprite;
        private Texture2D hanQiaoPixelTexture;
        private Texture2D evacueePixelTexture;
        private Texture2D farBankGuardTexture;
        private Texture2D spareRopePixelTexture;
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
        // 13: reached bridge post; 14: second bell and the forward sightline;
        // 15: personally spotted troops and return to the cable;
        // 16: the player physically holds the cable beside Shen Li.
        // ORK defence and the fixed third-bell break remain pending.
        public static string SceneTitle => Copy("S00 · Whitewater Crossing", "S00 · 白水渡");
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
        public bool CanWarnFarBank => phase == 14 && farBankLookout != null &&
            Mathf.Abs(walker.x - FarBankLookoutX) <= RepairReach &&
            Mathf.Abs(walker.y) <= RepairReach;
        public bool CanHoldCable => phase == 15 && bridgePost != null &&
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
            // UGUI Cancel goes to the focused control instead of bubbling to S00.
            // Forward it to the existing exit path without a second input owner.

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

            title = Label(canvas.transform, SceneTitle, 28,
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
            shenLiFigure = Panel(canvas.transform, "PLACEHOLDER Shen Li",
                new Vector2(ClampX + 58f, 18f), new Vector2(32f, 32f),
                Color.white);
            shenLiPixelSprite = MakeActorSprite("PLACEHOLDER 16px Shen Li",
                ShenLiPixels, new Color32(190, 133, 62, 255),
                out shenLiPixelTexture);
            shenLiBracingSprite = MakeActorSprite("S00 16px Shen Li bracing the cable",
                ShenLiBracingPixels, new Color32(190, 133, 62, 255),
                out shenLiBracingTexture);
            shenLiFigure.sprite = shenLiPixelSprite;
            stretcher = Panel(canvas.transform, "PLACEHOLDER stretcher wagon",
                new Vector2(-320f, 23f), new Vector2(52f, 27f),
                new Color(0.82f, 0.78f, 0.63f)).rectTransform;
            // S00-03: the officer is visibly in the river, not simply a
            // dialogue flag. He only appears after the workshop conversation.
            // The last ferry is a real point-filtered pixel silhouette.
            // Its 48x12 source renders at an integer 2x scale, matching
            // the existing 16px actors without a soft stretched UGUI bar.
            const int boatWidth = 48;
            const int boatHeight = 12;
            lastBoatPixelTexture = new Texture2D(boatWidth, boatHeight,
                TextureFormat.RGBA32, false)
            {
                name = "S00 48x12 last ferry",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0
            };
            var boatPixels = new Color32[boatWidth * boatHeight];
            for (int y = 0; y < boatHeight; y++)
            {
                for (int x = 0; x < boatWidth; x++)
                {
                    int inset = y <= 4 ? 8 - y : 3;
                    bool hull = y >= 2 && y <= 8 &&
                        x >= inset && x < boatWidth - inset;
                    bool gunwale = y == 9 && x >= 5 && x < boatWidth - 5;
                    bool seat = y == 10 && x >= 14 && x < boatWidth - 14;
                    boatPixels[y * boatWidth + x] = gunwale
                        ? new Color32(220, 177, 112, 255)
                        : seat ? new Color32(154, 111, 69, 255)
                        : hull ? y <= 3 ? new Color32(79, 53, 42, 255)
                            : x % 9 == 0 ? new Color32(112, 76, 52, 255)
                            : new Color32(146, 102, 67, 255)
                        : new Color32(0, 0, 0, 0);
                }
            }
            lastBoatPixelTexture.SetPixels32(boatPixels);
            lastBoatPixelTexture.Apply(false, false);
            var boatObject = new GameObject("S00 last boat",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            boatObject.transform.SetParent(canvas.transform, false);
            lastBoat = (RectTransform)boatObject.transform;
            lastBoat.anchorMin = lastBoat.anchorMax = new Vector2(0.5f, 0.5f);
            lastBoat.anchoredPosition = new Vector2(295f, 91f);
            lastBoat.sizeDelta = new Vector2(boatWidth * 2f, boatHeight * 2f);
            var boatImage = boatObject.GetComponent<RawImage>();
            boatImage.texture = lastBoatPixelTexture;
            boatImage.raycastTarget = false;
            var hanQiaoFigure = Panel(canvas.transform, "S00 Han Qiao in water",
                new Vector2(-70f, 92f), new Vector2(32f, 32f), Color.white);
            hanQiaoPixelSprite = MakeActorSprite("PLACEHOLDER 16px wounded Han Qiao",
                HanQiaoPixels, new Color32(100, 127, 155, 255),
                out hanQiaoPixelTexture);
            hanQiaoFigure.sprite = hanQiaoPixelSprite;
            hanQiao = hanQiaoFigure.rectTransform;
            rescuePoint = Panel(canvas.transform, "S00 rescue station",
                new Vector2(ClampX, 12f), new Vector2(30f, 12f), Brass).rectTransform;
            // Shen Li's authored rope alternative has a real, non-clickable
            // line across the water, not merely text claiming she threw it.
            // It appears only while that rescue plan is active and retracts
            // on cancellation or the player's earned rescue interaction.
            // The player's Shen Li rescue choice reveals a real repeating
            // nearest-neighbour 16x4 rope pattern, not a stretched UGUI bar.
            // Tiling it at one canvas unit per authored pixel preserves the
            // strict pixel scale along the full line toward Han Qiao.
            const int thrownRopePixelsWide = 16;
            const int thrownRopePixelsHigh = 4;
            shenLiRescueRopeTexture = new Texture2D(thrownRopePixelsWide,
                thrownRopePixelsHigh, TextureFormat.RGBA32, false)
            {
                name = "S00 pixel rescue rope",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                anisoLevel = 0
            };
            var ropePixels = new Color32[thrownRopePixelsWide * thrownRopePixelsHigh];
            for (int y = 0; y < thrownRopePixelsHigh; y++)
            {
                for (int x = 0; x < thrownRopePixelsWide; x++)
                {
                    // Two opposing diagonal strand rows give the player a
                    // readable braided rope rather than a solid line.
                    bool lightStrand = (x + (y == 2 ? 2 : 0)) % 4 < 2;
                    ropePixels[y * thrownRopePixelsWide + x] =
                        y == 0 || y == 3 ? new Color32(54, 42, 33, 255) :
                        lightStrand ? new Color32(224, 183, 112, 255) :
                            new Color32(151, 105, 56, 255);
                }
            }
            shenLiRescueRopeTexture.SetPixels32(ropePixels);
            shenLiRescueRopeTexture.Apply(false, false);
            var thrownLine = new GameObject("S00 Shen Li thrown rescue line",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            thrownLine.transform.SetParent(canvas.transform, false);
            shenLiRescueLine = (RectTransform)thrownLine.transform;
            shenLiRescueLine.anchorMin = shenLiRescueLine.anchorMax =
                new Vector2(0.5f, 0.5f);
            shenLiRescueLine.sizeDelta = new Vector2(315f, 4f);
            shenLiRescueLine.anchoredPosition = new Vector2(83f, 55f);
            var ropeGraphic = thrownLine.GetComponent<RawImage>();
            ropeGraphic.texture = shenLiRescueRopeTexture;
            ropeGraphic.uvRect = new Rect(0f, 0f,
                shenLiRescueLine.sizeDelta.x / thrownRopePixelsWide, 1f);
            ropeGraphic.raycastTarget = false;
            shenLiRescueLine.localRotation = Quaternion.Euler(0f, 0f, -13.6f);
            shenLiRescueLine.gameObject.SetActive(false);
            lastBoat.gameObject.SetActive(false);
            hanQiao.gameObject.SetActive(false);
            rescuePoint.gameObject.SetActive(false);
            // S00-04 opens only after the officer has actually boarded.
            // Both figures are scene-space UGUI markers, with a visible
            // destination; no automatic quest or battle is awarded.
            evacueeSafeLine = Panel(canvas.transform, "S00 evacuation safe line",
                new Vector2(EvacueeSafeX, 12f), new Vector2(14f, 62f),
                new Color(0.66f, 0.84f, 0.59f)).rectTransform;
            var evacueeFigure = Panel(canvas.transform, "S00 last evacuee",
                new Vector2(EvacueeStartX, 12f), new Vector2(32f, 32f), Color.white);
            evacueePixelSprite = MakeActorSprite("PLACEHOLDER 16px wounded evacuee",
                EvacueePixels, new Color32(161, 126, 88, 255),
                out evacueePixelTexture);
            evacueeFigure.sprite = evacueePixelSprite;
            evacuee = evacueeFigure.rectTransform;
            evacueeSafeLine.gameObject.SetActive(false);
            evacuee.gameObject.SetActive(false);
            // Visible only after the player finishes the escort and calls out.
            // Use a genuinely coiled 16px nearest-neighbour item, not a solid
            // rectangle. The original rope travel and Makinom signal remain.
            var spareRopeFigure = Panel(canvas.transform, "PLACEHOLDER S00 spare rope at clamp",
                new Vector2(ClampX + 30f, 51f), new Vector2(32f, 32f), Color.white);
            spareRopePixelSprite = MakeActorSprite("S00 16px spare rope coil",
                SpareRopePixels, new Color32(164, 119, 71, 255),
                out spareRopePixelTexture);
            spareRopeFigure.sprite = spareRopePixelSprite;
            spareRopeFigure.raycastTarget = false;
            spareRope = spareRopeFigure.rectTransform;
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
            farBankLookout = Panel(canvas.transform, "S00 forward lookout",
                new Vector2(FarBankLookoutX, 12f), new Vector2(26f, 40f), Brass)
                .rectTransform;
            farBankLookout.gameObject.SetActive(false);
            var playerFigure = Panel(canvas.transform, "PLACEHOLDER player",
                new Vector2(walker.x, 12f), new Vector2(32f, 32f),
                Color.white);
            playerPixelSprite = MakeActorSprite("PLACEHOLDER 16px player",
                PlayerPixels, new Color32(72, 151, 183, 255),
                out playerPixelTexture);
            playerFigure.sprite = playerPixelSprite;
            marker = playerFigure.rectTransform;
            // Distant spear-bearing enemies get a distinct 16px silhouette
            // rather than three recoloured player sprites. They remain
            // non-interactive scenery until a real ORK encounter is authored.
            farBankGuardSprite = MakeActorSprite("S00 16px far-bank spear guard",
                FarBankGuardPixels, new Color32(166, 73, 65, 255),
                out farBankGuardTexture);
            for (int i = 0; i < farBankThreats.Length; i++)
            {
                farBankThreats[i] = Panel(canvas.transform,
                    "S00 far-bank approaching figure " + i,
                    new Vector2(-283f + i * 34f, 22f), new Vector2(32f, 32f),
                    Color.white);
                farBankThreats[i].sprite = farBankGuardSprite;
                farBankThreats[i].gameObject.SetActive(false);
            }

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
                choices[i].gameObject.AddComponent<SoundwellActionMenuCancelHandler>()
                    .Initialize(Close);
                choiceLabels[i] = Label(choices[i].transform, "", 17,
                    Vector2.zero, new Vector2(224f, 48f), Ink);
            }
            next = Button(canvas.transform, "S00 continue to workshop",
                new Vector2(145f, -245f), new Vector2(228f, 29f), ContinueScene);
            next.gameObject.AddComponent<SoundwellActionMenuCancelHandler>()
                .Initialize(Close);
            Label(next.transform, Copy("Talk about the workshop", "聊聊战后的工坊"), 14,
                Vector2.zero, new Vector2(222f, 27f), Ink);
            next.gameObject.SetActive(false);
            exit = Button(canvas.transform, "S00 return to Bootstrap",
                new Vector2(-145f, -245f), new Vector2(228f, 29f), Close);
            exit.gameObject.AddComponent<SoundwellActionMenuCancelHandler>()
                .Initialize(Close);
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
                shenLiRescueLine.gameObject.SetActive(choice == 1);
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
            ShowRescueChoices();
        }

        // Picking a rescuer is reversible until the player has actually
        // reached their station and pulled Han Qiao free. The boat and
        // Makinom rescue result must never advance on a cancelled plan.
        private void ShowRescueChoices()
        {
            SoundwellInputAdapter.SetUiContext();
            narration.text = Copy(
                "A splash. Han Qiao clings to broken timber; his soaked uniform pulls him down. He shouts only: 'Rope!'",
                "一声落水。韩峤扒着断木，浸水的军服把他往下拖。他没有喊官衔，只喊：“绳子！”");
            instructions.text = Copy("Choose who takes the line. You still have to pull him out.",
                "决定谁来搭手。选完还得亲自完成救援。");
            ShowChoices(new[] { "Pull him myself", "Ask Shen Li to throw rope", "Call nearby soldiers" },
                new[] { "我自己拉", "请沈砾抛绳", "喊旁边士兵搭手" });
        }

        private void CancelUncommittedRescue()
        {
            if (phase != 7)
                return;
            // Restore the choice without resetting the player's position,
            // progressing Han Qiao's boat, or writing the Makinom outcome.
            rescueMethod = -1;
            rescuePoint.gameObject.SetActive(false);
            shenLiRescueLine.gameObject.SetActive(false);
            phase = 6;
            ShowRescueChoices();
        }

        private void FinishRescue()
        {
            if (!CanRescue)
                return;
            if (Maki.Initialized && Maki.Game?.Variables != null)
                Maki.Game.Variables.Set(HanQiaoRescueKey, rescueMethod + 1);
            phase = 8;
            rescuePoint.gameObject.SetActive(false);
            shenLiRescueLine.gameObject.SetActive(false);
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
            // The script says the last boat departs. Move the actual boat
            // and its rescued passenger out of the scene together instead
            // of leaving Han Qiao visible on a stationary boat.
            StartCoroutine(LastBoatDeparts());
            evacuee.anchoredPosition = new Vector2(EvacueeStartX, 12f);
            evacuee.GetComponent<Image>().color = Color.white;
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

        private IEnumerator LastBoatDeparts()
        {
            if (lastBoat == null || hanQiao == null)
                yield break;

            Vector2 boatDock = lastBoat.anchoredPosition;
            Vector2 passengerDock = hanQiao.anchoredPosition;
            const float travel = 320f;
            const float duration = 1.25f;
            float elapsed = 0f;
            // Unscaled time matches the preceding boarding animation.
            // This is presentation only: the Makinom rescue result was
            // already earned at the actual physical rescue interaction.
            while (elapsed < duration && lastBoat != null && hanQiao != null)
            {
                elapsed += Time.unscaledDeltaTime;
                Vector2 displacement = new Vector2(
                    travel * Mathf.Clamp01(elapsed / duration), 0f);
                lastBoat.anchoredPosition = boatDock + displacement;
                hanQiao.anchoredPosition = passengerDock + displacement;
                yield return null;
            }
            if (lastBoat != null)
                lastBoat.gameObject.SetActive(false);
            if (hanQiao != null)
                hanQiao.gameObject.SetActive(false);
        }

        private void HelpEvacuee()
        {
            if (!CanHelpEvacuee)
                return;
            phase = 10;
            evacueeWaiting = false;
            SetEvacueeEscortCue(false);
            narration.text = Copy(
                "You take the evacuee's arm. 'Keep behind me. Don't stop at the open span.'",
                "你扶住伤者的胳膊：“跟在我后面，别停在空桥上。”");
            instructions.text = Copy(
                "Lead west. Green: following; amber: stopped. Press E / Pad A at the safe line.",
                "向西带路：绿色表示跟随，橙色表示掉队；到安全线后按 E／手柄 A 确认撤离。");
        }

        private void UpdateEvacueeFollow()
        {
            if (phase != 10 || evacuee == null)
                return;
            float gap = Mathf.Abs(walker.x - evacuee.anchoredPosition.x);
            // The ordinary east/west escort retains its established pace.
            // Straying far off the bridge lane leaves a wounded follower
            // behind; return to the lane (with hysteresis) to reconnect.
            // This uses the same player position and follower state, not a
            // second quest timer or teleport.
            bool offLane = Mathf.Abs(walker.y) >
                (evacueeWaiting ? 20f : 33f);
            bool waiting = offLane || (evacueeWaiting
                ? gap > 65f : gap > 115f);
            if (waiting != evacueeWaiting)
            {
                evacueeWaiting = waiting;
                SetEvacueeEscortCue(waiting);
                instructions.text = waiting
                    ? Copy("Amber: the evacuee stopped. Return to their bridge lane.",
                        "橙色：伤者停下了。回到他所在的桥面通道重新接应。")
                    : Copy("Green: following. Stay close; press E / Pad A at the safe line.",
                        "绿色：伤者正在跟随。保持靠近，到安全线按 E／手柄 A。");
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
            // Keep the previously verified on-lane escort pace. Moving off
            // the bridge lane, not ordinary westward travel, breaks contact.
            float newX = Mathf.MoveTowards(at.x, followTarget, 220f * Time.unscaledDeltaTime);
            evacuee.anchoredPosition = new Vector2(newX, 12f);
        }

        private void SetEvacueeEscortCue(bool waiting)
        {
            if (evacuee != null && evacuee.TryGetComponent<Image>(out var graphic))
                graphic.color = waiting ? EvacueeWaitingTint : EvacueeFollowingTint;
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
            spareRope.anchoredPosition = SpareRopeDispatch;
            spareRope.gameObject.SetActive(true);
            // The coil travels across the visible bridge to Shen Li's clamp.
            // Its presentation never advances the ORK battle or awards a quest.
            StartCoroutine(DeliverSpareRope());
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

        private IEnumerator DeliverSpareRope()
        {
            float elapsed = 0f;
            while (elapsed < SpareRopeTravelSeconds && spareRope != null)
            {
                elapsed += Time.unscaledDeltaTime;
                spareRope.anchoredPosition = Vector2.Lerp(SpareRopeDispatch,
                    SpareRopeDock, Mathf.Clamp01(elapsed / SpareRopeTravelSeconds));
                yield return null;
            }
            if (spareRope != null)
                spareRope.anchoredPosition = SpareRopeDock;
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
            farBankLookout.gameObject.SetActive(true);
            // Return control to the real gameplay InputAction, so spotting
            // the opposing troops cannot be granted by the bell or a click.
            SoundwellInputAdapter.SetGameplayContext();
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
                "The lights are going dark. Walk to the amber lookout on the bridge and press E / Pad A to identify the movement.",
                "灯火正一盏盏熄灭。走到桥上金色瞭望点，按 E／手柄 A 辨认对岸的动静。");
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        private void WarnFarBank()
        {
            if (!CanWarnFarBank)
                return;
            phase = 15;
            if (Maki.Initialized && Maki.Game?.Variables != null)
                Maki.Game.Variables.Set(FarBankWarningKey, 1);
            farBankLookout.GetComponent<Image>().color =
                new Color(0.42f, 0.82f, 0.66f);
            foreach (Image threat in farBankThreats)
                threat.gameObject.SetActive(true);
            StartCoroutine(AdvanceFarBankThreats());
            // Shen Li is still at the real cable post. Returning to her is a
            // second player action, never an automatic follow-up to spotting.
            bridgePost.GetComponent<Image>().color =
                new Color(0.87f, 0.67f, 0.29f);
            SoundwellInputAdapter.SetGameplayContext();
            narration.text = Copy(
                "Three armed silhouettes appear where the far-bank lamps vanished. You warn Shen Li and the bridge crew. The cable is still under strain.",
                "对岸灯火熄灭处出现三道持械人影。你向沈砾和桥头同伴发出警告，缆索仍在吃力地绷着。");
            instructions.text = Copy(
                "Get back to Shen Li's amber cable post; press E / Pad A to hold the cable. The attackers are not defeated.",
                "返回沈砾身边金色缆桩，按 E／手柄 A 一起扶住缆索。敌军仍未被击退。");
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        // Distant visual cue only: no combatants, collisions, turns, rewards
        // or third-bell trigger. The figures stop short of the bridge post.
        private IEnumerator AdvanceFarBankThreats()
        {
            var destinations = new float[farBankThreats.Length];
            for (int i = 0; i < farBankThreats.Length; i++)
                destinations[i] = farBankThreats[i].rectTransform.anchoredPosition.x +
                    FarBankThreatApproach;
            bool advancedAfterHold = false;
            // Hold at the first sighted position while the player is away.
            // A real interaction at Shen Li's post advances the same visual
            // motor exactly once, with no second enemy actor or combat state.
            while (phase == 15 || phase == 16)
            {
                if (phase == 16 && !advancedAfterHold)
                {
                    for (int i = 0; i < destinations.Length; i++)
                        destinations[i] += FarBankThreatAfterHold;
                    advancedAfterHold = true;
                }
                bool approaching = false;
                for (int i = 0; i < farBankThreats.Length; i++)
                {
                    RectTransform figure = farBankThreats[i].rectTransform;
                    float x = Mathf.MoveTowards(figure.anchoredPosition.x,
                        destinations[i], FarBankThreatSpeed * Time.unscaledDeltaTime);
                    figure.anchoredPosition = new Vector2(x, figure.anchoredPosition.y);
                    approaching |= x < destinations[i] - 0.01f;
                }
                if (!approaching && advancedAfterHold)
                    yield break;
                yield return null;
            }
        }

        private void HoldCableAtPost()
        {
            if (!CanHoldCable)
                return;
            phase = 16;
            if (Maki.Initialized && Maki.Game?.Variables != null)
                Maki.Game.Variables.Set(CableHoldKey, 1);
            bridgePost.GetComponent<Image>().color =
                new Color(0.42f, 0.82f, 0.66f);
            // A visible attempt, not a way to rewrite the established third
            // bell or silently award a battle victory.
            cable.color = new Color(0.96f, 0.73f, 0.32f);
            // A real held-cable interaction changes Shen Li's arm and
            // wrench silhouette at the existing 2x pixel scale. Do not
            // borrow the ORK combat animator or auto-advance the history.
            if (shenLiFigure != null)
                shenLiFigure.sprite = shenLiBracingSprite;
            SoundwellInputAdapter.SetUiContext();
            narration.text = Copy(
                "You brace the strained cable beside Shen Li. For a moment it holds; the armed figures are still advancing over the far span.",
                "你在沈砾身边扶稳吃紧的缆索。它暂时绷住了，可对岸持械的人影仍在逼近。");
            instructions.text = Copy(
                "The cable is held for now, not saved. ORK bridge defence and the fixed third-bell break remain ahead. Return to entry.",
                "缆索暂时撑住，并没有改变结局。ORK 桥头防守战与既定的第三声断桥仍在后续。可返回入口。");
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

        // Scene-local 16x16 nearest-neighbour sprites. S00 is an existing UGUI
        // overlay, so its character Images use pixel-perfect texture input
        // without adding a second world renderer, Animator or combat owner.
        private static readonly string[] PlayerPixels =
        {
            "................",
            ".....kkkkkk.....",
            "....khhhhkkk....",
            "....khffffhk....",
            "....khffffhk....",
            ".....kffffk.....",
            "......kkkk......",
            ".....kcccck.....",
            "...ggkcccckgg...",
            "...ggkcccckgg...",
            ".....kbbbbk.....",
            ".....kcccck.....",
            ".....kk..kk.....",
            ".....kk..kk.....",
            "....kkk..kkk....",
            "................",
        };

        // The ponytail and wrench distinguish Shen Li from the player at 2x.
        private static readonly string[] ShenLiPixels =
        {
            "................",
            "....kkkkkkkh....",
            "....khhhhkkh....",
            "....khffffkh....",
            "....khffffkh....",
            ".....kffffkh....",
            "......kkkk.h....",
            ".....kcccck.....",
            "...ggkcccckgw...",
            "...ggkcccckgw...",
            ".....kbbbbk.w...",
            ".....kcccck.w...",
            ".....kk..kk.w...",
            ".....kk..kk.w...",
            "....kkk..kkk....",
            "................",
        };

        // An extended left arm and wrench face the stressed post after the
        // player physically confirms the cable hold. No added Animator owner.
        private static readonly string[] ShenLiBracingPixels =
        {
            "................",
            "....kkkkkkkh....",
            "....khhhhkkh....",
            "....khffffkh....",
            "....khffffkh....",
            ".....kffffkh....",
            "......kkkk.h....",
            ".....kcccck.....",
            "wggggkcccckg....",
            "wwwggkcccckg....",
            ".w...kbbbbk.....",
            ".w...kcccck.....",
            ".....kk..kk.....",
            ".....kk..kk.....",
            "....kkk..kkk....",
            "................",
        };

        // Water foam around Han Qiao, and a head bandage on the wounded
        // evacuee, distinguish the two actual story figures at 2x scale.
        private static readonly string[] HanQiaoPixels =
        {
            "................",
            "......www.......",
            ".....kkkk.......",
            "....kkfffkk.....",
            "....kwfffwk.....",
            ".....kfffk......",
            "....wwkkww......",
            "...wwkcckkw.....",
            "..wwccc...ww....",
            "..wccccccw......",
            "...kkcckk.......",
            "....kcck........",
            "....kbbk........",
            "....wbbw........",
            "...ww..ww.......",
            "................",
        };

        private static readonly string[] EvacueePixels =
        {
            "................",
            ".....wwwkk......",
            "....wwffkk......",
            "....kkffkk......",
            ".....kffk.......",
            "......kk........",
            ".....kcck.......",
            "....kcccck......",
            "...gkcccckg.....",
            "...gkcccckg.....",
            "....kbbbbk......",
            "....kcccck......",
            "....kk..kk......",
            "...kbb..bbk.....",
            "...kk....kk.....",
            "................",
        };

        // Helmet, shoulder armour and an upright spear remain recognizable
        // at native 16px and exact 2x UI scale, unlike the blue player sprite.
        // The silhouette is local presentation, not a new combat participant.
        private static readonly string[] FarBankGuardPixels =
        {
            ".............g..",
            "............gg..",
            ".............g..",
            ".....kkkkk...g..",
            "....kkkkkkk..g..",
            "....kkfffkk..g..",
            "....kkfffkk..g..",
            ".....kfffk...g..",
            "......kkkk...g..",
            "....kkcccckk.g..",
            "...kkkcccckkkg..",
            "...kkkcccckkkg..",
            "......kbbk...g..",
            "......kbbk...g..",
            ".....kk..kk..g..",
            ".....kk..kk.....",
        };

        // Two nested loops and an open centre read as a separate wound coil
        // at native 16px. All source pixels use the same scene-local palette.
        private static readonly string[] SpareRopePixels =
        {
            "................",
            "....kkkkkkkk....",
            "...kkccggcckk...",
            "..kkcckkkkcckk..",
            "..kcck....kcck..",
            ".kcck......kcck.",
            ".kcck.kkkk.kcck.",
            ".kcck.kcck.kcck.",
            ".kcck.kcck.kcck.",
            ".kcck.kkkk.kcck.",
            ".kcck......kcck.",
            "..kcck....kcck..",
            "..kkcckkkkcckk..",
            "...kkccggcckk...",
            "....kkkkkkkk....",
            "................",
        };

        private static Sprite MakeActorSprite(string name, string[] art,
            Color32 coat, out Texture2D texture)
        {
            const int size = 16;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name + " texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Pattern rows are top to bottom; Unity pixels are bottom to top.
                    switch (art[size - 1 - y][x])
                    {
                        case 'k': pixels[y * size + x] = new Color32(24, 32, 41, 255); break;
                        case 'h': pixels[y * size + x] = new Color32(82, 51, 43, 255); break;
                        case 'f': pixels[y * size + x] = new Color32(226, 180, 136, 255); break;
                        case 'c': pixels[y * size + x] = coat; break;
                        case 'g': pixels[y * size + x] = new Color32(195, 181, 151, 255); break;
                        case 'b': pixels[y * size + x] = new Color32(67, 70, 75, 255); break;
                        case 'w': pixels[y * size + x] = new Color32(181, 202, 207, 255); break;
                        default: pixels[y * size + x] = new Color32(0, 0, 0, 0); break;
                    }
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), size);
            sprite.name = name;
            return sprite;
        }

        private void OnDestroy()
        {
            // AudioSource belongs to the scene canvas and stops with it.
            // Release the generated sample clip when the temporary scene ends.
            if (firstBellClip != null)
                Destroy(firstBellClip);
            if (secondBellClip != null)
                Destroy(secondBellClip);
            if (playerPixelSprite != null)
                Destroy(playerPixelSprite);
            if (shenLiPixelSprite != null)
                Destroy(shenLiPixelSprite);
            if (shenLiBracingSprite != null)
                Destroy(shenLiBracingSprite);
            if (playerPixelTexture != null)
                Destroy(playerPixelTexture);
            if (shenLiPixelTexture != null)
                Destroy(shenLiPixelTexture);
            if (shenLiBracingTexture != null)
                Destroy(shenLiBracingTexture);
            if (hanQiaoPixelSprite != null)
                Destroy(hanQiaoPixelSprite);
            if (evacueePixelSprite != null)
                Destroy(evacueePixelSprite);
            if (farBankGuardSprite != null)
                Destroy(farBankGuardSprite);
            if (spareRopePixelSprite != null)
                Destroy(spareRopePixelSprite);
            if (hanQiaoPixelTexture != null)
                Destroy(hanQiaoPixelTexture);
            if (evacueePixelTexture != null)
                Destroy(evacueePixelTexture);
            if (farBankGuardTexture != null)
                Destroy(farBankGuardTexture);
            if (spareRopePixelTexture != null)
                Destroy(spareRopePixelTexture);
            if (shenLiRescueRopeTexture != null)
                Destroy(shenLiRescueRopeTexture);
            if (lastBoatPixelTexture != null)
                Destroy(lastBoatPixelTexture);
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
            if (phase != 1 && phase != 7 && phase != 9 && phase != 10 &&
                phase != 12 && phase != 14 && phase != 15)
                return;
            SoundwellControlFrame frame = SoundwellInputAdapter.ReadGameplay();
            // An uncommitted Han Qiao rescue plan is cancellable. In other
            // movement phases Esc / Pad B still leaves the scene as before.
            if (frame.Back)
            {
                if (phase == 7)
                    CancelUncommittedRescue();
                else
                    Close();
                return;
            }
            Vector2 delta = Vector2.ClampMagnitude(frame.Movement, 1f) *
                WalkSpeed * Time.unscaledDeltaTime;
            walker = new Vector2(Mathf.Clamp(walker.x + delta.x, -325f, 275f),
                Mathf.Clamp(walker.y + delta.y, -43f, 43f));
            marker.anchoredPosition = new Vector2(walker.x, 12f + walker.y);
            if (phase == 15)
            {
                if (frame.Ring)
                    HoldCableAtPost();
                return;
            }
            if (phase == 14)
            {
                if (frame.Ring)
                    WarnFarBank();
                return;
            }
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
