using System;
using System.Collections;
using GamingIsLove.Makinom;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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
        public const string ThirdBellKey = "rpg.return.s00.third-bell";
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
        // The wounded civilian's 32px sprite must overlap the player's body
        // before an escort can begin; the wider clamp repair radius does not apply.
        private const float RescueStationReachY = 28f;
        // Two 32px-wide figures need at least one visible pixel of horizontal
        // overlap before the player can take the wounded civilian's arm.
        // The clamp's 44px tool radius would permit a visible gap.
        private const float EvacueeContactReachX = 31f;
        private const float EvacueeContactReachY = 28f;
        // These stage markers are only 26-35 pixels wide, and the player
        // sprite is 32 pixels wide. Do not award a bridge interaction when
        // the two visible bodies are no longer touching.
        private const float BridgeStationReachX = 27f;
        private const float BridgeStationReachY = 30f;
        private const float EvacueeStartX = -72f;
        private const float EvacueeSafeX = -265f;
        private const float SpareRopeTravelSeconds = 0.55f;
        private static readonly Vector2 SpareRopeDispatch = new Vector2(-290f, 51f);
        private static readonly Vector2 SpareRopeDock = new Vector2(ClampX + 30f, 51f);
        private static readonly Vector2 SpareRopeBrace = new Vector2(ClampX - 3f, 30f);
        private static readonly Color Ink = new Color(0.87f, 0.90f, 0.88f);
        private static readonly Color Brass = new Color(0.91f, 0.72f, 0.40f);
        // A wounded follower needs visible readiness and separation feedback.
        private static readonly Color EvacueeFollowingTint = new Color(0.72f, 0.94f, 0.76f);
        private static readonly Color EvacueeWaitingTint = new Color(1f, 0.60f, 0.35f);
        private readonly Button[] choices = new Button[3];
        private readonly Text[] choiceLabels = new Text[3];
        // Keep the authored options in both languages while a decision is open.
        // Switching locale is presentation only: it cannot submit a choice.
        private string[] openChoicesEnglish;
        private string[] openChoicesChinese;
        private string shownChoiceLanguage;
        private RectTransform marker;
        private RectTransform stretcher;
        private RectTransform hanQiao;
        private RectTransform rescuePoint;
        private RectTransform shenLiRescueLine;
        private RectTransform soldierRescueHelper;
        private Sprite soldierReadySprite;
        private Sprite soldierHaulingSprite;
        private Sprite youngSoldierShieldSprite;
        private Texture2D soldierReadyTexture;
        private Texture2D soldierHaulingTexture;
        private Texture2D youngSoldierShieldTexture;
        private Texture2D shenLiRescueRopeTexture;
        private RectTransform lastBoat;
        private Texture2D lastBoatPixelTexture;
        private RectTransform evacuee;
        private Text evacueeEscortStatus;
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
        private AudioClip thirdBellClip;
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
        private Sprite encirclingTorchSprite;
        private Sprite farBankGuardWalkSprite;
        private Sprite spareRopePixelSprite;
        private Texture2D hanQiaoPixelTexture;
        private Texture2D evacueePixelTexture;
        private Texture2D farBankGuardTexture;
        private Texture2D encirclingTorchTexture;
        private Texture2D farBankGuardWalkTexture;
        private Texture2D spareRopePixelTexture;
        private Image cable;
        private RectTransform bridgeWalkway;
        private RectTransform bridgeCable;
        private Text narration;
        private Text instructions;
        private Text title;
        private Text stageSubtitle;
        private float lastStageLayoutAspect = -1f;
        private Button exit;
        private Button next;
        private Button skipCompletedReplay;
        private Text skipCompletedReplayLabel;
        private Action onExit;
        private WhitewaterDefenseEncounter defenseEncounter;
        private bool defenseStarted;
        private bool defenseWon;
        private bool thirdBellStarted;
        private int phase;
        // S00 station prompts are keyed only to physical reach, phase and
        // selected locale. They never grant actions or copy Makinom state.
        private int shownBridgeCuePhase = -1;
        private bool shownBridgeCueReachable;
        private string shownBridgeCueLanguage;
        private string shownBridgeCueBindings;
        // The S00 movement motor still polls reachability every frame, but
        // formatting five effective Input System binding labels is only
        // necessary after the underlying controls actually change.
        private bool stationBindingsDirty = true;
        private string cachedStationBindings;
        private int rescueMethod;
        // The v1.2 Tax Collector wrench beat consumes two separate earned
        // gameplay interactions; Makinom remains owner of the repair outcome.
        private int clampKnocks;
        // The v1.2 wrench questions are a UI substate of the clamp approach;
        // they do not advance the player's movement or repair phase.
        private bool wrenchQuestionsOpen;
        // Only the third earned safe-line handoff completes S00-04 in v1.2.
        // These are the cook, the straggling wounded soldier and the
        // shield-bearing young soldier, in that fixed authored order.
        private int evacueesRescued;
        private bool shownRescueCueReachable;
        private string shownRescueCueLanguage;
        private string shownRescueCueBindings;
        private bool evacueeWaiting;
        private bool evacueeWaitedOffLane;
        private bool shownEvacueePickupReachable;
        private bool shownEvacueePickupOffLane;
        private string shownEvacueePickupLanguage;
        private string shownEvacueePickupBindings;
        private bool shownEvacueeSafeLineReachable;
        private bool shownEvacueeSafeLineLaneAligned;
        private string shownEvacueeSafeLineLanguage;
        private string shownEvacueeSafeLineBindings;
        private Vector2 walker = new Vector2(-245f, 0f);

        // 0: roll call; 1: walk; 2: cable decision; 3: stretcher crossing;
        // 4: workshop promise choice; 5: its response; 6: man overboard;
        // 7: rescue station; 8: Han Qiao boards the last boat;
        // 9: reach the stranded evacuee; 10: escort west; 11: confirm safe crossing;
        // 12: voluntary rope call, first bell and physical guard-post approach;
        // 13: reached bridge post; 14: second bell and the forward sightline;
        // 15: personally spotted troops and return to the cable;
        // 16: the player physically holds the cable beside Shen Li.
        // Phase 17 is the earned, fixed third-bell break after ORK victory;
        // phase 18 is the Twelve Years Later card, not a playable M00.
        public static string SceneTitle => Copy("S00 · Whitewater Crossing", "S00 · 白水渡");
        public int Phase => phase;
        public int EvacueesRescued => evacueesRescued;
        public bool ThirdBellStarted => thirdBellStarted;
        public Vector2 WalkerPosition => walker;
        public string CurrentNarration => narration != null ? narration.text : string.Empty;
        public string CurrentInstructions => instructions != null ? instructions.text : string.Empty;
        public bool CanRepair => phase == 1 && Mathf.Abs(walker.x - ClampX) <= RepairReach &&
            Mathf.Abs(walker.y) <= RepairReach;
        public bool CanRescue => phase == 7 &&
            Mathf.Abs(walker.x - RescueStationX(rescueMethod)) <= RepairReach &&
            // The visible rescue station is on the bridge deck. A nearby
            // horizontal position from below the walkway is not a valid haul.
            Mathf.Abs(walker.y) <= RescueStationReachY;
        public Vector2 EvacueePosition => evacuee != null
            ? evacuee.anchoredPosition : Vector2.zero;
        public bool CanHelpEvacuee => phase == 9 && evacuee != null &&
            Mathf.Abs(walker.x - evacuee.anchoredPosition.x) <= EvacueeContactReachX &&
            // Stay on the bridge lane, and require actual sprite overlap in Y.
            // A player below the lane can otherwise touch X while remaining
            // visibly separated from the wounded civilian centered at Y=12.
            Mathf.Abs(walker.y) <= EvacueeContactReachY &&
            Mathf.Abs(walker.y - evacuee.anchoredPosition.y) <= EvacueeContactReachY;
        public bool CanDeliverEvacuee => phase == 10 && evacuee != null &&
            // Stopping near the line is not a rescue if the wounded follower
            // has lost the escort and is still waiting for the player to return.
            !evacueeWaiting && walker.x <= EvacueeSafeX &&
            evacuee.anchoredPosition.x <= EvacueeSafeX + 30f &&
            // A handoff is only valid when the player and the survivor are
            // physically on the same bridge lane, not merely within the
            // broad clamp-tool radius. Otherwise the next pickup can be
            // visibly unreachable from the previous off-lane handoff.
            Mathf.Abs(walker.y - evacuee.anchoredPosition.y) <= EvacueeContactReachY;
        public bool CanTakeBridgePost => phase == 12 && bridgePost != null &&
            Mathf.Abs(walker.x - BridgePostX) <= BridgeStationReachX &&
            Mathf.Abs(walker.y) <= BridgeStationReachY;
        public bool CanWarnFarBank => phase == 14 && farBankLookout != null &&
            Mathf.Abs(walker.x - FarBankLookoutX) <= BridgeStationReachX &&
            Mathf.Abs(walker.y) <= BridgeStationReachY;
        public bool CanHoldCable => phase == 15 && bridgePost != null &&
            Mathf.Abs(walker.x - BridgePostX) <= BridgeStationReachX &&
            Mathf.Abs(walker.y) <= BridgeStationReachY;

        private static float RescueStationX(int choice) =>
            choice == 0 ? 60f : choice == 1 ? ClampX : -70f;

        // A live binding can change without moving the actor or toggling
        // language. Keep station cues in sync with all controls they print:
        // walking, hauling and cancelling. This is UI-only and never writes
        // an input action, Makinom state or gameplay phase.
        private void OnEnable()
        {
            InputSystem.onActionChange += OnStationActionChanged;
            SoundwellUiLocalization.Changed += RefreshCompletedReplayLabel;
            stationBindingsDirty = true;
        }

        private void OnDisable()
        {
            InputSystem.onActionChange -= OnStationActionChanged;
            SoundwellUiLocalization.Changed -= RefreshCompletedReplayLabel;
        }

        private void RefreshCompletedReplayLabel()
        {
            if (skipCompletedReplayLabel != null)
                skipCompletedReplayLabel.text = Copy(
                    "Skip to Memorial Day", "跳到临潮纪念日");
        }

        private static bool HasEarnedThirdBell()
        {
            return Maki.Initialized && Maki.Game?.Variables != null &&
                Maki.Game.Variables.GetInt(ThirdBellKey) != 0;
        }

        private void OnStationActionChanged(object source, InputActionChange change)
        {
            if (change == InputActionChange.BoundControlsChanged)
                stationBindingsDirty = true;
        }

        private string StationPromptBindingSignature()
        {
            if (!stationBindingsDirty && cachedStationBindings != null)
                return cachedStationBindings;
            cachedStationBindings = string.Concat(
                SoundwellInputAdapter.GameplayKeyboardMovementLabel(), "|",
                SoundwellInputAdapter.GameplayKeyboardInteractionLabel(), "|",
                SoundwellInputAdapter.GameplayGamepadInteractionLabel(), "|",
                SoundwellInputAdapter.GameplayKeyboardBackLabel(), "|",
                SoundwellInputAdapter.GameplayGamepadBackLabel());
            stationBindingsDirty = false;
            return cachedStationBindings;
        }

        private static bool Chinese => SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
        private static string Copy(string en, string zh)
        {
            string message = Chinese ? zh : en;
            // The stage's two movement prompts must follow the effective
            // keyboard composite too. Saved alternate bindings (e.g. IJKL)
            // must not be advertised as WASD; the live stick is unchanged.
            if (message.Contains("WASD / left stick") ||
                message.Contains("WASD／左摇杆"))
            {
                string movement = SoundwellInputAdapter.GameplayKeyboardMovementLabel();
                message = Chinese
                    ? message.Replace("WASD／左摇杆",
                        string.IsNullOrEmpty(movement) ? "左摇杆" : movement + "／左摇杆")
                    : message.Replace("WASD / left stick",
                        string.IsNullOrEmpty(movement) ? "left stick" : movement + " / left stick");
            }
            // The two walkable S00 phases handle Gameplay/Back (not a
            // fixed Escape/Pad B shortcut). Saved remaps must update both
            // locale prompts without changing the original input owner.
            if (message.Contains("Esc / Pad B") ||
                message.Contains("Esc／手柄 B"))
            {
                string keyboardBack = SoundwellInputAdapter.GameplayKeyboardBackLabel();
                string gamepadBack = SoundwellInputAdapter.GameplayGamepadBackLabel();
                string padBack = string.IsNullOrEmpty(gamepadBack) ? string.Empty
                    : (Chinese ? "手柄 " : "Pad ") + gamepadBack;
                string backLabel = !string.IsNullOrEmpty(keyboardBack) && !string.IsNullOrEmpty(padBack)
                    ? keyboardBack + (Chinese ? "／" : " / ") + padBack
                    : !string.IsNullOrEmpty(keyboardBack) ? keyboardBack
                    : !string.IsNullOrEmpty(padBack) ? padBack
                    : Chinese ? "返回" : "Back";
                message = Chinese
                    ? message.Replace("Esc／手柄 B", backLabel)
                    : message.Replace("Esc / Pad B", backLabel);
            }
            // Only scripted instructions with the original action pair
            // are rewritten. Resolve both effective bindings from Gameplay/Ring
            // so neither keyboard nor gamepad hints lie after a saved remap.
            if (!message.Contains("E / Pad A") && !message.Contains("E／手柄 A"))
                return message;
            string key = SoundwellInputAdapter.GameplayKeyboardInteractionLabel();
            string pad = SoundwellInputAdapter.GameplayGamepadInteractionLabel();
            string padLabel = string.IsNullOrEmpty(pad) ? string.Empty
                : (Chinese ? "手柄 " : "Pad ") + pad;
            string separator = Chinese ? "／" : " / ";
            string prompt = !string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(padLabel)
                ? key + separator + padLabel
                : !string.IsNullOrEmpty(key) ? key
                : !string.IsNullOrEmpty(padLabel) ? padLabel
                : Chinese ? "互动" : "Interact";
            return Chinese
                ? message.Replace("E／手柄 A", prompt)
                : message.Replace("E / Pad A", prompt);
        }

        public void Begin(Action exitAction)
        {
            onExit = exitAction;
            defenseEncounter = GetComponentInParent<WhitewaterDefenseEncounter>();
            defenseStarted = false;
            defenseWon = false;
            thirdBellStarted = false;
            phase = 0;
            clampKnocks = 0;
            wrenchQuestionsOpen = false;
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
            CreateFullCanvasNightBackdrop(canvas.transform);

            title = Label(canvas.transform, SceneTitle, 28,
                new Vector2(0f, 209f), new Vector2(740f, 48f), Brass);
            stageSubtitle = Label(canvas.transform, Copy("Night roll call · the last bridge",
                "夜点名 · 最后的浮桥"), 17, new Vector2(0f, 168f), new Vector2(750f, 34f), Ink);
            Panel(canvas.transform, "PLACEHOLDER 16px ferry water",
                new Vector2(0f, 34f), new Vector2(790f, 180f),
                new Color(0.11f, 0.21f, 0.26f));
            bridgeWalkway = Panel(canvas.transform, "PLACEHOLDER bridge walkway",
                new Vector2(0f, 12f), new Vector2(720f, 58f),
                new Color(0.34f, 0.29f, 0.23f)).rectTransform;
            bridgeCable = Panel(canvas.transform, "PLACEHOLDER cable line",
                new Vector2(0f, 67f), new Vector2(690f, 7f),
                new Color(0.56f, 0.55f, 0.50f)).rectTransform;
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
            // The soldiers option is a real, visible helper at the rail,
            // not just a narration variant. It is non-interactive scenery:
            // only the player's earned station press completes the rescue.
            var soldierFigure = Panel(canvas.transform, "S00 soldier rescue helper",
                new Vector2(RescueStationX(2) - 38f, 12f),
                new Vector2(32f, 32f), Color.white);
            soldierReadySprite = MakeActorSprite("S00 16px soldier ready at the rail",
                SoldierReadyPixels, new Color32(89, 115, 131, 255),
                out soldierReadyTexture);
            soldierHaulingSprite = MakeActorSprite("S00 16px soldier hauling the line",
                SoldierHaulingPixels, new Color32(89, 115, 131, 255),
                out soldierHaulingTexture);
            youngSoldierShieldSprite = MakeActorSprite("S00 16px slipping young soldier with shield",
                YoungSoldierShieldPixels, new Color32(111, 128, 143, 255),
                out youngSoldierShieldTexture);
            soldierFigure.sprite = soldierReadySprite;
            soldierRescueHelper = soldierFigure.rectTransform;
            soldierRescueHelper.gameObject.SetActive(false);
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
            evacueePixelSprite = MakeActorSprite("S00 16px pot-carrying cook",
                EvacueePixels, new Color32(161, 126, 88, 255),
                out evacueePixelTexture);
            evacueeFigure.sprite = evacueePixelSprite;
            evacuee = evacueeFigure.rectTransform;
            // The wounded follower's status must remain readable without
            // distinguishing the green and amber sprite tints.
            evacueeEscortStatus = Label(evacuee, "", 12,
                new Vector2(0f, 25f), new Vector2(82f, 20f), Ink);
            evacueeEscortStatus.gameObject.name = "S00 evacuee escort status";
            evacueeEscortStatus.raycastTarget = false;
            evacueeEscortStatus.gameObject.SetActive(false);
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
            farBankGuardWalkSprite = MakeActorSprite("S00 16px far-bank spear guard walking",
                FarBankGuardWalkPixels, new Color32(166, 73, 65, 255),
                out farBankGuardWalkTexture);
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

            // A completed S00 can be skipped on replay, but never resumed from
            // an unearned checkpoint. The existing Makinom third-bell fact is
            // the sole gate; this button grants no battle or quest reward.
            skipCompletedReplay = Button(canvas.transform,
                "S00 skip completed prologue", Vector2.zero,
                new Vector2(180f, 28f), SkipCompletedPrologue);
            skipCompletedReplay.gameObject.AddComponent<SoundwellActionMenuCancelHandler>()
                .Initialize(Close);
            RectTransform skipRect = (RectTransform)skipCompletedReplay.transform;
            skipRect.anchorMin = skipRect.anchorMax = new Vector2(1f, 1f);
            skipRect.anchoredPosition = new Vector2(-18f, -18f);
            skipRect.sizeDelta = new Vector2(180f, 28f);
            skipCompletedReplayLabel = Label(skipCompletedReplay.transform, "", 13,
                Vector2.zero, new Vector2(176f, 26f), Ink);
            skipCompletedReplay.gameObject.SetActive(HasEarnedThirdBell());
            RefreshCompletedReplayLabel();
            ShowRollCall();
            RefreshStageLayout(true);
        }

        // Keep real UGUI options reachable at extreme aspect ratios. The
        // original 960x540 positions put left/right dialogue choices outside
        // a portrait viewport and the lower action row below a 32:9 screen.
        // Layout is presentation only: the same callbacks, ORK authority and
        // Makinom choice outcomes remain wired to these exact buttons.
        private static Vector2[] StageLayoutSlots(float aspect)
        {
            if (float.IsNaN(aspect) || float.IsInfinity(aspect) || aspect <= 0f)
                aspect = 16f / 9f;

            if (aspect <= 0.95f)
                return new[]
                {
                    new Vector2(0f, 209f), new Vector2(0f, 168f),
                    new Vector2(0f, -45f), new Vector2(0f, -105f),
                    new Vector2(0f, -150f), new Vector2(0f, -204f),
                    new Vector2(0f, -258f), new Vector2(0f, -300f),
                    new Vector2(0f, -343f)
                };

            if (aspect >= 2.55f)
            {
                float halfHeight = 0.5f * Mathf.Sqrt(960f * 540f / aspect);
                return new[]
                {
                    new Vector2(0f, halfHeight - 30f),
                    new Vector2(0f, halfHeight - 75f),
                    new Vector2(0f, halfHeight - 136f),
                    new Vector2(0f, halfHeight - 198f),
                    new Vector2(-250f, halfHeight - 245f),
                    new Vector2(0f, halfHeight - 245f),
                    new Vector2(250f, halfHeight - 245f),
                    new Vector2(145f, halfHeight - 296f),
                    new Vector2(-145f, halfHeight - 296f)
                };
            }

            return new[]
            {
                new Vector2(0f, 209f), new Vector2(0f, 168f),
                new Vector2(0f, -100f), new Vector2(0f, -143f),
                new Vector2(-250f, -192f), new Vector2(0f, -192f),
                new Vector2(250f, -192f), new Vector2(145f, -245f),
                new Vector2(-145f, -245f)
            };
        }

        private static Vector2[] TwoChoiceLayoutSlots(float aspect)
        {
            if (float.IsNaN(aspect) || float.IsInfinity(aspect) || aspect <= 0f)
                aspect = 16f / 9f;

            // Keep the two authored wrench questions balanced instead of
            // leaving the third, unused choice slot as an empty center gap.
            if (aspect <= 0.95f)
                return new[]
                {
                    new Vector2(0f, -150f), new Vector2(0f, -258f)
                };

            if (aspect >= 2.55f)
            {
                float halfHeight = 0.5f * Mathf.Sqrt(960f * 540f / aspect);
                return new[]
                {
                    new Vector2(-125f, halfHeight - 245f),
                    new Vector2(125f, halfHeight - 245f)
                };
            }

            return new[]
            {
                new Vector2(-125f, -192f), new Vector2(125f, -192f)
            };
        }

        private void RefreshStageLayout(bool force = false)
        {
            if (title == null || stageSubtitle == null || exit == null)
                return;
            float aspect = (float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height);
            if (!force && Mathf.Abs(lastStageLayoutAspect - aspect) < 0.01f)
                return;
            lastStageLayoutAspect = aspect;
            Vector2[] slots = StageLayoutSlots(aspect);
            title.rectTransform.anchoredPosition = slots[0];
            stageSubtitle.rectTransform.anchoredPosition = slots[1];
            narration.rectTransform.anchoredPosition = slots[2];
            instructions.rectTransform.anchoredPosition = slots[3];
            Vector2[] twoChoiceSlots = openChoicesEnglish != null &&
                openChoicesEnglish.Length == 2 ? TwoChoiceLayoutSlots(aspect) : null;
            for (int i = 0; i < choices.Length; i++)
            {
                Vector2 position = twoChoiceSlots != null && i < twoChoiceSlots.Length
                    ? twoChoiceSlots[i] : slots[i + 4];
                ((RectTransform)choices[i].transform).anchoredPosition = position;
            }
            ((RectTransform)next.transform).anchoredPosition = slots[7];
            ((RectTransform)exit.transform).anchoredPosition = slots[8];

            // In portrait, constrain narrative copy to the actual Canvas
            // width instead of letting labels bleed behind the entry screen.
            float logicalWidth = Mathf.Sqrt(960f * 540f * aspect);
            bool portrait = aspect <= 0.95f;
            float textWidth = Mathf.Max(240f, logicalWidth - 30f);
            title.rectTransform.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal, portrait ? Mathf.Min(740f, textWidth) : 740f);
            stageSubtitle.rectTransform.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal, portrait ? Mathf.Min(750f, textWidth) : 750f);
            narration.rectTransform.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal, portrait ? Mathf.Min(840f, textWidth) : 840f);
            instructions.rectTransform.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal, portrait ? Mathf.Min(790f, textWidth) : 790f);
        }

        private void ShowRollCall()
        {
            narration.text = Copy("Sergeant: Shen Li.  Shen Li: 'Here. Still fixing it.'\nThen the sergeant calls your name.",
                "军士：“沈砾。”沈砾头也不抬：“在，修着呢。”\n军士接着念到你的名字。");
            instructions.text = Copy("Choose what you say; none of these is a skill check.",
                "选择怎样应声。没有口才检定，也不会因此救不了谁。");
            // v1.2 P0: each reply is audible. The third reply occurs only
            // after the sergeant calls the player's name a second time.
            ShowChoices(new[] { "Here.", "Here. Has she fixed that nut?", "...Here. (Second call)" },
                new[] { "在。", "在。——她那个螺母，修好了吗？", "……在。（第二遍应声）" });
        }

        private void Choose(int choice)
        {
            if (choice < 0 || choice > 2)
                return;
            if (phase == 0)
            {
                if (skipCompletedReplay != null)
                    skipCompletedReplay.gameObject.SetActive(false);
                if (Maki.Initialized && Maki.Game?.Variables != null)
                    Maki.Game.Variables.Set(RollCallKey, choice + 1);
                phase = 1;
                wrenchQuestionsOpen = true;
                HideChoices();
                SoundwellInputAdapter.SetUiContext();
                string replyEnglish = choice == 0 ? "Sergeant: 'Right.' "
                    : choice == 1 ? "Shen Li: 'In a hurry? Come see.' Sergeant: 'Two alive.' "
                    : "The sergeant calls your name a second time. You answer '...Here.' "
                        + "The sergeant marks the list: 'Stay alert. Keep close when we cross the bridge.' ";
                string replyChinese = choice == 0 ? "军士划一笔：“行。”"
                    : choice == 1 ? "沈砾：“催命呢？自己来看。”军士：“行，俩活人。”"
                    : "军士又念了一遍你的名字。你：“……在。”"
                        + "军士划一笔：“别走神，待会儿过桥紧着点。”";
                // Keep the complete authored Ma Xiaoliu beat audible before
                // the wrench questions; the scene script includes his belt,
                // his latrine excuse and the soldiers' laugh.
                narration.text = Copy(
                    replyEnglish + "Ma Xiaoliu is called three times. As the sergeant raises his pen, a soldier rushes out of the brush, pulling up his trousers: 'Here, here, here!' "
                        + "The sergeant stops his pen beside the name: 'Three unanswered calls and you're marked dead, understand?' "
                        + "Ma Xiaoliu nods while tying his belt: 'I know, I know! Next time I have to run to the latrine, I'll answer first!' The others laugh. "
                        + "Shen Li hands you the 'Tax Collector' wrench.",
                    replyChinese + "军士连念“马小六”三遍，正要划名，草丛里有人提着裤子冲出来。"
                        + "马小六：“在在在！”军士收住笔：“三遍没人答，你就是死人了，知道吗？”"
                        + "马小六一边系腰带一边点头：“知道了知道了！下回拉肚子也先应一声！”周围有人笑。"
                        + "沈砾递给你“税务官”扳手。");
                instructions.text = Copy(
                    "Ask Shen Li why she named it that, or whether her own wrench has a name.",
                    "问沈砾为什么这样叫，或问她手里的扳手叫什么。");
                ShowChoices(new[] { "Why call it that?", "Does your wrench have a name?" },
                    new[] { "为什么叫这个？", "你手里那把也有名字吗？" });
            }
            else if (phase == 1 && wrenchQuestionsOpen)
            {
                // Both authored questions lead to the same instruction; the
                // second wrench's nickname is revealed after the two knocks.
                if (choice > 1)
                    return;
                wrenchQuestionsOpen = false;
                HideChoices();
                SoundwellInputAdapter.SetGameplayContext();
                narration.text = Copy(
                    "Shen Li: 'Knock twice and it will listen. Try it.'",
                    "沈砾：“敲两下，它才肯听话。试试。”");
                instructions.text = Copy(
                    "WASD / left stick: reach the red clamp. Press E / Pad A twice; the first knock does not repair the bridge. Esc / Pad B: back.",
                    "WASD／左摇杆走近红色缆桩，按 E／手柄 A 敲两下；第一下不会修好桥。Esc／手柄 B 返回。");
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
                // The workshop invitation remains a conversation choice,
                // not a hidden promise that changes rescue or battle outcomes.
                // These are the three authored reactions in v1.2 P0.
                narration.text = Copy(
                    choice == 0 ? "Shen Li: 'All right. I'll owe you the wages for now.'"
                    : choice == 1 ? "Shen Li: 'Fine if you don't come. I'll save you a chair. I'll charge for that, too.'"
                    : "Shen Li: 'A smart mouth goes on the bill twice.'",
                    choice == 0 ? "沈砾：“行。工钱先欠着。”"
                    : choice == 1 ? "沈砾：“不来也行，我给你留把椅子。椅子也收钱。”"
                    : "沈砾：“嘴贫的人，账上记两笔。”");
                // Do not expose internal S00 prototype numbering in playable prose.
                instructions.text = Copy("Someone cries out from the river. Go see who needs help near the last boat.",
                    "河水里传来呼救声。去最后一条船附近看看是谁需要帮忙。");
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
                shownRescueCueLanguage = null;
                HideChoices();
                SoundwellInputAdapter.SetGameplayContext();
                rescuePoint.anchoredPosition = new Vector2(RescueStationX(choice), 12f);
                rescuePoint.gameObject.SetActive(true);
                shenLiRescueLine.gameObject.SetActive(choice == 1);
                soldierRescueHelper.gameObject.SetActive(choice == 2);
                if (choice == 2)
                    soldierRescueHelper.GetComponent<Image>().sprite = soldierReadySprite;
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

        // A rescuer plan only places a station. Use the live player position
        // to explain when the physical hauling action becomes available,
        // including after a saved language or input-binding change. This is
        // presentation only and never performs the rescue automatically.
        private void RefreshRescueStationReachCue()
        {
            if (phase != 7 || instructions == null)
                return;
            bool canHaul = CanRescue;
            string language = SoundwellUiLocalization.Language;
            string bindings = StationPromptBindingSignature();
            if (shownRescueCueLanguage == language &&
                shownRescueCueReachable == canHaul &&
                shownRescueCueBindings == bindings)
                return;
            shownRescueCueReachable = canHaul;
            shownRescueCueLanguage = language;
            shownRescueCueBindings = bindings;
            instructions.text = canHaul
                ? Copy("At the gold rescue station: press E / Pad A to haul Han Qiao. Esc / Pad B: change plan.",
                    "已到金色救援点：按 E／手柄 A 拉起韩峤。Esc／手柄 B 更换方案。")
                : Copy("Not at the gold rescue station: use WASD / left stick to reach it, then E / Pad A. Esc / Pad B: change plan.",
                    "尚未到金色救援点：用 WASD／左摇杆走近，再按 E／手柄 A 拉人。Esc／手柄 B 更换方案。");
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
            soldierRescueHelper.gameObject.SetActive(false);
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
            if (rescueMethod == 2)
                soldierRescueHelper.GetComponent<Image>().sprite = soldierHaulingSprite;
            else
                soldierRescueHelper.gameObject.SetActive(false);
            SoundwellInputAdapter.SetUiContext();
            narration.text = Copy(
                rescueMethod == 0
                    ? "You heave Han Qiao ashore. He clutches your sleeve: 'The bridge?' 'Not yet.' 'Before the third watch...' He faints."
                    : rescueMethod == 1
                        ? "Shen Li's rope holds. You haul Han Qiao ashore. 'The bridge?' 'Not yet.' 'Before the third watch...' He faints."
                        : "The soldiers lift Han Qiao free. He grips your sleeve: 'The bridge?' 'Not yet.' 'Before the third watch...' He faints.",
                rescueMethod == 0
                    ? "你把韩峤拖上岸。他攥住你袖子：“……桥。桥过了没有？”你：“还没有。”“三更前……得过完……”他昏了过去。"
                    : rescueMethod == 1
                        ? "沈砾的绳索绷住，你们拉韩峤上岸。他问：“桥过了没有？”你：“还没有。”“三更前……得过完……”他昏了过去。"
                        : "士兵帮你把韩峤拖上岸。他问：“桥过了没有？”你：“还没有。”“三更前……得过完……”他昏了过去。");
            instructions.text = Copy("The medics carry Han Qiao onto the last boat across.",
                "医官把昏过去的韩峤抬上最后一条过河的小船。");
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
                Vector2 next = Vector2.Lerp(start,
                    new Vector2(290f, 100f), Mathf.Clamp01(elapsed / 2f));
                // Both endpoints are authored on whole canvas pixels. The
                // initial rescue must show movement on its FIRST frame,
                // even when the first 2-second interpolation is below 0.5px.
                // Ceil preserves eastward progress and remains integer-only;
                // nearest rounding could leave the officer visibly frozen.
                hanQiao.anchoredPosition = new Vector2(
                    Mathf.Ceil(next.x), Mathf.Ceil(next.y));
                yield return null;
            }
            if (phase == 8 && next != null)
            {
                instructions.text = Copy(
                    "Han Qiao is aboard. A cook with a pot on his back is stranded by the bridge railing.",
                    "韩峤已经上船。桥栏边还有一个背着锅、走不动的炊事兵。");
                next.gameObject.name = "S00 continue to final retreat";
                Text label = next.GetComponentInChildren<Text>(true);
                if (label != null)
                    label.text = Copy("Help the stranded cook", "扶起走不动的炊事兵");
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
            evacueesRescued = 0;
            shownEvacueePickupLanguage = null;
            shownEvacueePickupBindings = null;
            next.gameObject.SetActive(false);
            soldierRescueHelper.gameObject.SetActive(false);
            // The script says the last boat departs. Move the actual boat
            // and its rescued passenger out of the scene together instead
            // of leaving Han Qiao visible on a stationary boat.
            StartCoroutine(LastBoatDeparts());
            evacuee.anchoredPosition = new Vector2(EvacueeStartX, 12f);
            evacuee.GetComponent<Image>().sprite = evacueePixelSprite;
            evacuee.GetComponent<Image>().color = Color.white;
            evacueeEscortStatus.gameObject.SetActive(false);
            evacuee.gameObject.SetActive(true);
            evacueeSafeLine.gameObject.SetActive(true);
            SoundwellInputAdapter.SetGameplayContext();
            narration.text = Copy(
                "The last boat pulls away as enemy torches approach. A cook with a cooking pot on his back collapses by the railing: 'My legs won't move!'",
                "最后一条船离岸，敌军火把正在逼近。一个背锅的炊事兵腿软，跌坐在栏杆旁：“我走不动了——腿不听使唤——”");
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
                // Round the ONE shared travel displacement, not each actor
                // separately: both point-filtered sprites stay on whole
                // canvas pixels without drifting apart while the boat sails.
                float displacement = Mathf.Round(
                    travel * Mathf.Clamp01(elapsed / duration));
                Vector2 move = new Vector2(displacement, 0f);
                lastBoat.anchoredPosition = boatDock + move;
                hanQiao.anchoredPosition = passengerDock + move;
                yield return null;
            }
            if (lastBoat != null)
                lastBoat.gameObject.SetActive(false);
            if (hanQiao != null)
                hanQiao.gameObject.SetActive(false);
        }

        // The amber civilian is a physical pickup, not a dialogue shortcut.
        // Show immediate on/off-lane reach feedback before a player presses E.
        // This is UI only: it never changes phase, follower state or Makinom.
        private void RefreshEvacueePickupReachCue()
        {
            if (phase != 9 || instructions == null || evacuee == null)
                return;
            bool reachable = CanHelpEvacuee;
            bool offLane = Mathf.Abs(walker.y) > EvacueeContactReachY ||
                Mathf.Abs(walker.y - evacuee.anchoredPosition.y) > EvacueeContactReachY;
            string locale = SoundwellUiLocalization.Language;
            string bindings = StationPromptBindingSignature();
            // The civilian can remain unreachable while the player changes
            // from distant-on-bridge to off-lane. Both hints must still refresh.
            if (shownEvacueePickupLanguage == locale &&
                shownEvacueePickupBindings == bindings &&
                shownEvacueePickupReachable == reachable &&
                shownEvacueePickupOffLane == offLane)
                return;
            shownEvacueePickupReachable = reachable;
            shownEvacueePickupOffLane = offLane;
            shownEvacueePickupLanguage = locale;
            shownEvacueePickupBindings = bindings;
            instructions.text = reachable
                ? Copy("Beside the amber evacuee: press E / Pad A to help, then lead west.",
                    "已到金色伤者身旁：按 E／手柄 A 搀扶，再向西带路。")
                : offLane
                    ? Copy("Return to the bridge lane beside the amber evacuee, then press E / Pad A.",
                        "请回到金色伤者身旁的桥面通道，再按 E／手柄 A 搀扶。")
                    : Copy("Walk beside the amber evacuee, then press E / Pad A to help.",
                        "请走到金色伤者身旁，再按 E／手柄 A 搀扶。");
        }

        private void HelpEvacuee()
        {
            if (!CanHelpEvacuee)
            {
                // Pressing E away from the last wounded civilian must not
                // silently discard the real input or grant an unearned escort.
                if (phase == 9 && instructions != null)
                    instructions.text = Mathf.Abs(walker.y) > EvacueeContactReachY ||
                        (evacuee != null &&
                         Mathf.Abs(walker.y - evacuee.anchoredPosition.y) > EvacueeContactReachY)
                        ? Copy("Return to the bridge lane beside the amber evacuee, then press E / Pad A to help.",
                            "请回到金色伤者身旁的桥面通道，再按 E／手柄 A 搀扶。")
                        : Copy("Too far from the amber evacuee. Walk beside them, then press E / Pad A to help.",
                            "距离金色伤者还太远。先走到伤者身旁，再按 E／手柄 A 搀扶。");
                return;
            }
            phase = 10;
            if (evacueesRescued == 2)
                // The player's shield grip, not the timer, pulls the young
                // soldier back onto the actual bridge lane.
                evacuee.anchoredPosition = new Vector2(evacuee.anchoredPosition.x, 12f);
            evacueeWaiting = false;
            evacueeWaitedOffLane = false;
            shownEvacueeSafeLineReachable = false;
            shownEvacueeSafeLineLaneAligned = false;
            shownEvacueeSafeLineLanguage = null;
            shownEvacueeSafeLineBindings = null;
            SetEvacueeEscortCue(false);
            narration.text = evacueesRescued == 0
                ? Copy("You pull the cook to his feet and take his arm. 'Stay close. We have to cross.'",
                    "你架起腿软的炊事兵：“跟紧我，咱们得过桥。”")
                : evacueesRescued == 1
                    ? Copy("You lift the straggling wounded soldier onto your back. The bridge is still under fire.",
                        "你背起掉队的伤兵。敌军还在朝浮桥逼近。")
                    : Copy("You brace the young soldier's shield and haul him back from the edge.",
                        "你顶住年轻士兵的盾，把快滑下桥沿的他拉了回来。");
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
            // Explain why the escort stopped instead of always telling an
            // on-lane player to "return to the lane." If the cause changes
            // while still waiting, replace the stale hint immediately.
            // A language switch or saved input remap can happen while both
            // characters stand still. Refresh the visible escort instruction
            // and on-actor status without treating the change as movement.
            bool cueContextChanged = shownEvacueeSafeLineLanguage != null &&
                (shownEvacueeSafeLineLanguage != SoundwellUiLocalization.Language ||
                 shownEvacueeSafeLineBindings != StationPromptBindingSignature());
            if (waiting != evacueeWaiting ||
                (waiting && offLane != evacueeWaitedOffLane) ||
                cueContextChanged)
            {
                evacueeWaiting = waiting;
                evacueeWaitedOffLane = offLane;
                SetEvacueeEscortCue(waiting);
                instructions.text = waiting
                    ? offLane
                        ? Copy("Amber: the evacuee stopped. Return to the bridge lane.",
                            "橙色：伤者停下了。回到桥面通道重新接应。")
                        : Copy("Amber: the evacuee fell behind. Walk back toward them.",
                            "橙色：伤者落在后方。请往回走，重新接应。")
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
            // A soldier carried on the back should remain directly beside
            // the player rather than lag one whole sprite behind.
            float followOffset = evacueesRescued == 1 ? 12f : 24f;
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
            if (evacueeEscortStatus != null)
            {
                evacueeEscortStatus.text = waiting
                    ? Copy("WAIT", "等待") : Copy("FOLLOW", "跟随");
                evacueeEscortStatus.gameObject.SetActive(true);
            }
        }

        private void DeliverEvacuee()
        {
            if (!CanDeliverEvacuee)
            {
                // An early E / Pad A must explain why the physical safe-line
                // handoff is unavailable, never silently count as evacuation.
                if (!evacueeWaiting && instructions != null)
                {
                    bool laneAligned = evacuee != null &&
                        Mathf.Abs(walker.y - evacuee.anchoredPosition.y) <= EvacueeContactReachY;
                    instructions.text = !laneAligned
                        ? Copy("Return to the bridge lane and meet the evacuee at the green safe line before confirming.",
                            "回到桥面通道，在绿色安全线与伤者会合后再确认交接。")
                        : walker.x > EvacueeSafeX
                            ? Copy("Not at the green safe line. Lead the evacuee west before confirming.",
                                "尚未到绿色安全线。先带伤者向西撤离，再确认交接。")
                            : Copy("Wait for the evacuee to reach the green safe line before confirming.",
                                "请等伤者抵达绿色安全线，再确认交接。");
                }
                return;
            }
            evacueesRescued++;
            evacueeEscortStatus.gameObject.SetActive(false);
            if (evacueesRescued < 3)
            {
                SpawnNextEvacuee();
                return;
            }
            // The shared Makinom result means the complete v1.2 group was
            // rescued. Neither a pickup nor an intermediate delivery writes
            // the final flag or advances the rope/bell history.
            if (Maki.Initialized && Maki.Game?.Variables != null)
                Maki.Game.Variables.Set(EvacueeEscortedKey, 1);
            phase = 11;
            SoundwellInputAdapter.SetUiContext();
            narration.text = Copy(
                "All three are across: the cook, the wounded soldier, and the young shield-bearer. Enemy troops close in; Shen Li still watches the bridge cable.",
                "三次拉人都完成了：炊事兵、掉队的伤兵和险些滑落桥沿的年轻士兵已安全过桥。敌军越逼越近，沈砾仍在留意桥上的缆索。");
            instructions.text = Copy(
                "Call for spare rope to help the bridge crew.",
                "呼喊多余的绳索，提醒桥头同伴。");
            next.gameObject.name = "S00 call for spare rope";
            Text label = next.GetComponentInChildren<Text>(true);
            if (label != null)
                label.text = Copy("Who has spare rope?", "谁带了多余的绳子？");
            next.gameObject.SetActive(true);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(next.gameObject);
        }

        private void SpawnNextEvacuee()
        {
            // Stage 1 and 2 are distinct physical pickups using the same
            // gameplay input and safe-line follower. Reusing the already
            // tested lane avoids false instant rescues or another input owner.
            phase = 9;
            shownEvacueePickupLanguage = null;
            shownEvacueePickupBindings = null;
            shownEvacueePickupReachable = false;
            shownEvacueePickupOffLane = false;
            evacueeWaiting = false;
            evacueeWaitedOffLane = false;
            Image actor = evacuee.GetComponent<Image>();
            actor.color = Color.white;
            actor.sprite = evacueesRescued == 1
                ? soldierReadySprite : youngSoldierShieldSprite;
            evacuee.anchoredPosition = evacueesRescued == 1
                ? new Vector2(-72f, 12f) : new Vector2(-15f, 12f);
            evacueeEscortStatus.gameObject.SetActive(false);
            SoundwellInputAdapter.SetGameplayContext();
            narration.text = evacueesRescued == 1
                ? Copy("Shen Li: 'Get him up! More wounded are falling behind!' A wounded soldier staggers beside the railing.",
                    "沈砾：“把人拉起来！后面还有伤兵掉队！”一名掉队的伤兵踉跄着靠在桥栏旁。")
                : Copy("A young soldier slips toward the bridge edge. His shield catches the rail; reach him before he falls.",
                    "一个年轻士兵在湿滑的桥板上朝桥沿滑去，盾牌卡在栏杆上。快过去拉住他。");
            instructions.text = Copy(
                "Reach the amber figure with WASD / left stick and press E / Pad A. The next rescue has not been earned.",
                "用 WASD／左摇杆走到金色人影身边，按 E／手柄 A 救援。下一次救援尚未完成。");
            if (evacueesRescued == 2)
                StartCoroutine(YoungSoldierSlips());
        }

        private IEnumerator YoungSoldierSlips()
        {
            // A visible, bounded slide toward the bridge edge. The actor
            // never falls or grants rescue on a timer; only a real Ring
            // press at the soldier ends this danger and begins the escort.
            float elapsed = 0f;
            while (phase == 9 && evacueesRescued == 2 &&
                evacuee != null && elapsed < 1.1f)
            {
                elapsed += Time.unscaledDeltaTime;
                evacuee.anchoredPosition = new Vector2(-15f,
                    Mathf.Round(Mathf.Lerp(12f, -18f, Mathf.Clamp01(elapsed / 1.1f))));
                yield return null;
            }
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
                "WASD / left stick: reach Shen Li's amber bridge post; press E / Pad A to take position.",
                "用 WASD／左摇杆走到沈砾附近金色桥头守位，按 E／手柄 A 就位。");
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
                "You take the bridge post beside Shen Li. The cook, wounded soldier and young shield-bearer are all safe; you face the approaching troops together.",
                "你在沈砾身旁站定桥头守位。炊事兵、伤兵和年轻士兵都已撤离，你们一起望向逼近的敌军。");
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
                "The lights are going dark. Use WASD / left stick to reach the amber lookout, then E / Pad A to identify the movement.",
                "灯火正一盏盏熄灭。用 WASD／左摇杆走到桥上金色瞭望点，再按 E／手柄 A 辨认动静。");
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
                "Three armed silhouettes appear where the far-bank lamps vanished. You cannot make out their uniforms. You warn Shen Li and the bridge crew. The cable is still under strain.",
                "对岸熄灯的地方出现三道人影。你看不清他们穿哪一边的军服，便朝沈砾和桥头同伴示警。缆索仍绷得发紧。");
            instructions.text = Copy(
                "WASD / left stick: return to Shen Li's amber cable post; press E / Pad A to hold the cable. The bridgehead is still exposed.",
                "用 WASD／左摇杆返回沈砾身边金色缆桩，按 E／手柄 A 一起扶住缆索。桥头仍无遮拦。");
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        // Distant visual cue only: no combatants, collisions, turns, rewards
        // or third-bell trigger. The figures stop short of the bridge post.
        private IEnumerator AdvanceFarBankThreats()
        {
            var destinations = new float[farBankThreats.Length];
            // Keep a continuous motor separate from pixel-snapped rendering.
            // At 60 FPS the 24px/s advance is below half a pixel each frame:
            // rounding the motor itself would freeze the approaching guards.
            var positions = new float[farBankThreats.Length];
            for (int i = 0; i < farBankThreats.Length; i++)
            {
                positions[i] = farBankThreats[i].rectTransform.anchoredPosition.x;
                destinations[i] = positions[i] + FarBankThreatApproach;
            }
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
                    float x = Mathf.MoveTowards(positions[i],
                        destinations[i], FarBankThreatSpeed * Time.unscaledDeltaTime);
                    positions[i] = x;
                    figure.anchoredPosition = new Vector2(
                        Mathf.Round(x), Mathf.Round(figure.anchoredPosition.y));
                    // Authored pixel footwork tracks the existing visual motor.
                    // No second velocity, AI turn or combatant is introduced.
                    // The displayed guard is snapped to whole pixels, but
                    // its authored motor must still complete the full step.
                    // Stop the walking sprite on the final visible pixel
                    // without cutting off the subpixel motor prematurely.
                    bool movingMotor = x < destinations[i] - 0.01f;
                    bool visibleMarch = Mathf.Round(x) < Mathf.Round(destinations[i]);
                    farBankThreats[i].sprite = visibleMarch &&
                        Mathf.FloorToInt(Time.unscaledTime / 0.16f) % 2 == 1
                            ? farBankGuardWalkSprite : farBankGuardSprite;
                    approaching |= movingMotor;
                }
                if (!approaching && advancedAfterHold)
                {
                    BeginWhitewaterDefense();
                    yield break;
                }
                yield return null;
            }
        }

        private void BeginWhitewaterDefense()
        {
            if (phase != 16 || defenseStarted || defenseWon)
                return;

            if (defenseEncounter == null)
                defenseEncounter = GetComponentInParent<WhitewaterDefenseEncounter>();
            if (defenseEncounter == null)
            {
                instructions.text = Copy(
                    "The bridgehead fight is unavailable. Return to the entrance and try again.",
                    "桥头战斗暂时无法开始。请返回入口后重试。");
                return;
            }

            defenseStarted = defenseEncounter.Begin(OnWhitewaterDefenseCompleted);
            if (!defenseStarted)
            {
                instructions.text = Copy(
                    "The bridgehead fight could not begin. Return to the entrance and try again.",
                    "桥头战斗未能开始。请返回入口后重试。");
                return;
            }

            narration.text = Copy(
                "Spear points reach the bridgehead. On the dark far bank, you can no longer make out the figures. Shen Li grips the cable as you turn to hold the crossing.",
                "持矛的人逼到桥头。对岸已经看不清人影。沈砾攥紧缆索，你转身守桥。");
            instructions.text = Copy(
                "On your turn, choose Attack or Guard. The enemy soldiers act on their own turns.",
                "轮到你行动时选择攻击或防御；敌方士兵也会在他们的回合行动。");
        }

        private void OnWhitewaterDefenseCompleted(bool victory)
        {
            // Only the active encounter may deliver this callback; a stage
            // presentation, stale input or repeated completion cannot ring it.
            if (phase != 16 || !defenseStarted)
                return;
            defenseStarted = false;
            if (!victory)
            {
                Close();
                return;
            }

            defenseWon = true;
            StartCoroutine(PlayThirdBellCollapse());
        }

        // Historical S00 outcome is fixed, but never a reward for merely
        // watching the approaching soldiers or bracing the cable. It follows
        // only the live ORK encounter's accepted victory callback.
        private IEnumerator PlayThirdBellCollapse()
        {
            if (phase != 16 || !defenseWon || thirdBellStarted)
                yield break;
            thirdBellStarted = true;
            phase = 17;
            SoundwellInputAdapter.SetUiContext();
            if (next != null)
                next.gameObject.SetActive(false);
            if (Maki.Initialized && Maki.Game?.Variables != null)
                Maki.Game.Variables.Set(ThirdBellKey, 1);
            thirdBellClip = CreateThirdBellCue();
            bellAudio.Stop();
            bellAudio.clip = thirdBellClip;
            bellAudio.Play();
            narration.text = Copy(
                "A third bell strikes. The bridgehead is held, but the far-side cable breaks in the firelight.",
                "第三声钟响了。桥头虽守住，对岸的缆索却在火光里崩断。");
            instructions.text = Copy(
                "The far span is sinking. The cable gave way before the fire reached it.",
                "远端桥身正在下沉。火还没烧到缆索，缆索就断了。");

            yield return new WaitForSecondsRealtime(0.4f);
            if (phase != 17)
                yield break;
            // Only the far (left) section sinks. The near clamp and the
            // original rescue interactions remain in the same scene graph.
            float elapsed = 0f;
            const float fallDuration = 1.1f;
            while (phase == 17 && elapsed < fallDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fallDuration);
                if (bridgeWalkway != null)
                {
                    bridgeWalkway.sizeDelta = new Vector2(
                        Mathf.Round(Mathf.Lerp(720f, 360f, t)), 58f);
                    bridgeWalkway.anchoredPosition = new Vector2(
                        Mathf.Round(180f * t), Mathf.Round(12f - 52f * t));
                }
                if (bridgeCable != null)
                {
                    bridgeCable.sizeDelta = new Vector2(
                        Mathf.Round(Mathf.Lerp(690f, 330f, t)), 7f);
                    bridgeCable.anchoredPosition = new Vector2(
                        Mathf.Round(180f * t), Mathf.Round(67f - 36f * t));
                }
                yield return null;
            }
            if (phase != 17)
                yield break;

            narration.text = Copy(
                "The far span disappears under the water. Shen Li catches your hand: 'Don't look at the water. Look at me.' Torches close in; the crowd tears you apart.",
                "远端桥身沉入水下。沈砾抓住你的手：“别看水，看我。”四面火把亮起，人潮把你们冲开。");
            instructions.text = string.Empty;
            // Torches are existing noninteractive scenery, never an ORK squad.
            // Use one shared point-filtered 16px sprite for all four lights,
            // not four stretching solid rectangles or duplicate textures.
            Transform canvasRoot = title.transform.parent;
            encirclingTorchSprite = MakeActorSprite("S00 16px encircling torch",
                EncirclingTorchPixels, new Color32(255, 115, 42, 255),
                out encirclingTorchTexture);
            var torchPositions = new[]
            {
                new Vector2(-307f, 108f), new Vector2(304f, 105f),
                new Vector2(-293f, -25f), new Vector2(301f, -27f)
            };
            foreach (Vector2 torchPosition in torchPositions)
            {
                var torch = Panel(canvasRoot, "PLACEHOLDER S00 encircling torch light",
                    torchPosition, new Vector2(32f, 32f), Color.white);
                torch.sprite = encirclingTorchSprite;
            }
            yield return new WaitForSecondsRealtime(0.8f);
            if (phase != 17)
                yield break;

            Image blackout = CreateFullCanvasBlackout(canvasRoot);
            float fade = 0f;
            while (phase == 17 && fade < 0.5f)
            {
                fade += Time.unscaledDeltaTime;
                blackout.color = new Color(0f, 0f, 0f, Mathf.Clamp01(fade / 0.5f));
                yield return null;
            }
            if (phase != 17)
                yield break;
            blackout.color = Color.black;
            phase = 18;
            CreateResponsiveEpilogueCard(canvasRoot,
                Copy("TWELVE YEARS LATER", "十二年后"));
            // M00 is still a separate playable story task. Reuse the real
            // exit control rather than claiming a scene that does not exist.
            exit.transform.SetAsLastSibling();
            RefreshEpilogueExitPosition();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(exit.gameObject);
        }

        // The ending must always offer a clickable return to the entry
        // screen. The 960x540 CanvasScaler uses the geometric mean of width
        // and height; fixed y=-190 slips below the viewport on 32:9 screens.
        private static Vector2 EpilogueExitPosition(float aspect)
        {
            if (float.IsNaN(aspect) || float.IsInfinity(aspect) || aspect <= 0f)
                aspect = 16f / 9f;
            float halfHeight = 0.5f * Mathf.Sqrt(960f * 540f / aspect);
            // A 29px button retains 12px of visible bottom breathing room.
            return new Vector2(0f, Mathf.Max(-190f,
                -halfHeight + 14.5f + 12f));
        }

        private void RefreshEpilogueExitPosition()
        {
            if (exit == null)
                return;
            float aspect = (float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height);
            ((RectTransform)exit.transform).anchoredPosition =
                EpilogueExitPosition(aspect);
        }

        // The title appears after the full-screen third-bell blackout. Its
        // former fixed 780px width exceeded portrait canvas bounds, leaving
        // the ending unreadable even though the blackout itself stretched.
        // Keep a safe margin without altering the scene or ORK result.
        private static Text CreateResponsiveEpilogueCard(
            Transform canvasRoot, string message)
        {
            Text card = Label(canvasRoot, message, 35,
                new Vector2(0f, 25f), new Vector2(-40f, 95f), Ink);
            card.gameObject.name = "S00 twelve years later";
            RectTransform rect = card.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 25f);
            rect.sizeDelta = new Vector2(-40f, 95f);
            card.horizontalOverflow = HorizontalWrapMode.Wrap;
            card.resizeTextMinSize = 12;
            return card;
        }

        // The prelude starts with a dark night sky. A fixed 960x540 panel
        // does not cover a portrait or ultrawide CanvasScaler viewport and
        // exposes the underlying entry screen around the walkable bridge.
        // Only the backdrop stretches; authored 16px actors remain pixel-snapped.
        private static Image CreateFullCanvasNightBackdrop(Transform canvasRoot)
        {
            Image backdrop = Panel(canvasRoot, "S00 night backdrop",
                Vector2.zero, Vector2.zero, new Color(0.045f, 0.070f, 0.095f));
            RectTransform bounds = backdrop.rectTransform;
            bounds.anchorMin = Vector2.zero;
            bounds.anchorMax = Vector2.one;
            bounds.offsetMin = Vector2.zero;
            bounds.offsetMax = Vector2.zero;
            return backdrop;
        }

        // The same third-bell fade must cover the entire playable canvas
        // on portrait and ultrawide screens. A fixed 960x540 center panel
        // exposes scene edges after CanvasScaler adjusts the viewport.
        // This is purely a visual overlay: it cannot affect the ORK victory.
        private static Image CreateFullCanvasBlackout(Transform canvasRoot)
        {
            Image blackout = Panel(canvasRoot, "S00 blackout after bridge collapse",
                Vector2.zero, Vector2.zero, Color.clear);
            RectTransform cover = blackout.rectTransform;
            cover.anchorMin = Vector2.zero;
            cover.anchorMax = Vector2.one;
            cover.offsetMin = Vector2.zero;
            cover.offsetMax = Vector2.zero;
            return blackout;
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
            // Spend the coil the player visibly called for: move that SAME
            // scene object onto the clamp, not an invented second rope or
            // an automatically completed ORK defence action.
            if (spareRope != null && spareRope.gameObject.activeInHierarchy)
                StartCoroutine(FastenSpareRopeAtClamp());
            // A real held-cable interaction changes Shen Li's arm and
            // wrench silhouette at the existing 2x pixel scale. Do not
            // borrow the ORK combat animator or auto-advance the history.
            if (shenLiFigure != null)
                shenLiFigure.sprite = shenLiBracingSprite;
            SoundwellInputAdapter.SetUiContext();
            narration.text = Copy(
                "You brace the strained cable beside Shen Li, hauling the spare coil onto the clamp. For a moment it holds. There is still movement across the river.",
                "你在沈砾身边扶住缆索，把送来的绳卷拉上缆桩。缆索暂时稳住。河对岸还有动静。");
            instructions.text = Copy(
                "Hold the cable. There are spears at the bridgehead. The fight is about to begin. Choose Attack or Guard when your turn appears.",
                "继续扶住缆索。桥头有人举矛逼近，战斗即将开始。轮到你时选择攻击或防御。");
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(exit.gameObject);
        }

        // Only a visual cable-brace action. Keep the 16px coil on exact
        // canvas pixels so the saved point-filtered sprite never shimmers.
        // This cannot fire the third bell, complete a quest or resolve ORK.
        private IEnumerator FastenSpareRopeAtClamp()
        {
            if (spareRope == null)
                yield break;
            Vector2 from = spareRope.anchoredPosition;
            const float duration = 0.42f;
            float elapsed = 0f;
            while (phase == 16 && spareRope != null && elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                Vector2 position = Vector2.Lerp(from, SpareRopeBrace,
                    Mathf.Clamp01(elapsed / duration));
                spareRope.anchoredPosition = new Vector2(
                    Mathf.Round(position.x), Mathf.Round(position.y));
                yield return null;
            }
            if (phase == 16 && spareRope != null)
                spareRope.anchoredPosition = SpareRopeBrace;
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

        private static AudioClip CreateThirdBellCue() =>
            CreateWarningBellCue("PLACEHOLDER S00 third warning bell", 392f);

        private static AudioClip CreateWarningBellCue(string clipName, float frequency)
        {
            // An original short nonverbal scene cue, not a new encounter clock
            // or RPG authority. First/second bells use the same saved M6 SFX bus.
            const int sampleRate = 22050;
            const float duration = 1.3f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float seconds = (float)i / sampleRate;
                float phase = seconds * frequency * Mathf.PI * 2f;
                // Bell metal has inharmonic overtones and a sharp strike,
                // not a flat dual-sine beep. A quieter, delayed reflection
                // gives the opposite bank a readable acoustic distance.
                float strike = Mathf.Min(seconds * 850f, 1f) *
                    Mathf.Exp(-4.6f * seconds / duration);
                float release = Mathf.Min((duration - seconds) * 28f, 1f);
                float metal = Mathf.Sin(phase) +
                    0.39f * Mathf.Sin(phase * 2.37f) +
                    0.17f * Mathf.Sin(phase * 3.78f);
                float echoSeconds = seconds - 0.19f;
                float echo = echoSeconds > 0f
                    ? 0.22f * Mathf.Exp(-5.3f * echoSeconds / duration) *
                      Mathf.Sin(echoSeconds * frequency * Mathf.PI * 2f)
                    : 0f;
                samples[i] = 0.15f * release * (strike * metal + echo);
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

        // Water foam around Han Qiao and the cook's dark back-mounted pot
        // distinguish the two story figures at 2x scale. This is only the
        // first evacuation beat of v1.2; the later two remain to implement.
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
            ".....kkkk.......",
            "....kkffkk......",
            "....kkffkk......",
            ".....kffk...kk..",
            "......kk...kkkk.",
            ".....kcck..kggk.",
            "....kcccckkggkk.",
            "...gkcccckgkk...",
            "...gkcccckg.....",
            "....kbbbbk......",
            "....kcccck......",
            "....kk..kk......",
            "...kbb..bbk.....",
            "...kk....kk.....",
            "................",
        };

        // The third v1.2 rescue holds a distinct shield at the bridge edge.
        // An unchanged cook sprite would conceal the authored danger.
        private static readonly string[] YoungSoldierShieldPixels =
        {
            "................",
            ".....kkkk.......",
            "....kkffkk......",
            "....kkffkk......",
            ".....kffk.......",
            "......kk........",
            ".....kcck...bbbb",
            "....kcccck..bbbb",
            "...gkcccckg.bggb",
            "...gkcccckg.bggb",
            "....kbbbbk..bggb",
            "....kcccck...bbb",
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

        // An alternate 16px footfall keeps the helmet and spear fixed while
        // visibly separating the legs. The original sprite remains the rest
        // pose once the same approach motor reaches its existing destination.
        private static readonly string[] FarBankGuardWalkPixels =
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
            "....kk....kk.g..",
            "...kk......kk...",
        };

        // A nearby bridge soldier is a distinct, armored 16px figure.
        // Raising the arm is visible only after the actual station rescue.
        private static readonly string[] SoldierReadyPixels =
        {
            "................",
            ".....kkkkkk.....",
            "....kkggggkk....",
            "....kffffffk....",
            ".....kffffk.....",
            "......kkkk......",
            "...kkcccccckk...",
            "...kcccccccck...",
            "....kcccccck....",
            ".....kkkkkk.....",
            "....kkbbbbkk....",
            "....kk....kk....",
            "...kk......kk...",
            "...kk......kk...",
            "...kk......kk...",
            "................",
        };

        private static readonly string[] SoldierHaulingPixels =
        {
            "................",
            ".....kkkkkk.....",
            "....kkggggkk....",
            "....kffffffk....",
            ".....kffffk.....",
            "......kkkk......",
            "..kkkkcccccckkkk",
            "..kkcccccccckk..",
            "....kcccccck....",
            ".....kkkkkk.....",
            "....kkbbbbkk....",
            "....kk....kk....",
            "...kk......kk...",
            "...kk......kk...",
            "...kk......kk...",
            "................",
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

        // Fixed S00 encirclement already had four torches. Draw their
        // flame and grip at the same 16px/2x scale as the bridge actors.
        // f/g = hot flame, c = orange fire, h/k = wooden handle and rim.
        private static readonly string[] EncirclingTorchPixels =
        {
            "................",
            ".......g........",
            "......gfg.......",
            "......fcf.......",
            ".....fcccf......",
            "......ccc.......",
            "......hch.......",
            ".......h........",
            "......khk.......",
            "......khk.......",
            "......khk.......",
            "......khk.......",
            "......khk.......",
            "......khk.......",
            "......kkk.......",
            "................",
        };

        private static Sprite MakeActorSprite(string name, string[] art,
            Color32 coat, out Texture2D texture) =>
            SoundwellPixelSpriteBuilder.Create16Colored(art, name, coat, out texture);

        private void OnDestroy()
        {
            defenseEncounter?.Stop();
            // AudioSource belongs to the scene canvas and stops with it.
            // Release the generated sample clip when the temporary scene ends.
            if (firstBellClip != null)
                Destroy(firstBellClip);
            if (secondBellClip != null)
                Destroy(secondBellClip);
            if (thirdBellClip != null)
                Destroy(thirdBellClip);
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
            if (soldierReadySprite != null)
                Destroy(soldierReadySprite);
            if (soldierHaulingSprite != null)
                Destroy(soldierHaulingSprite);
            if (youngSoldierShieldSprite != null)
                Destroy(youngSoldierShieldSprite);
            if (evacueePixelSprite != null)
                Destroy(evacueePixelSprite);
            if (farBankGuardSprite != null)
                Destroy(farBankGuardSprite);
            if (encirclingTorchSprite != null)
                Destroy(encirclingTorchSprite);
            if (farBankGuardWalkSprite != null)
                Destroy(farBankGuardWalkSprite);
            if (spareRopePixelSprite != null)
                Destroy(spareRopePixelSprite);
            if (hanQiaoPixelTexture != null)
                Destroy(hanQiaoPixelTexture);
            if (soldierReadyTexture != null)
                Destroy(soldierReadyTexture);
            if (soldierHaulingTexture != null)
                Destroy(soldierHaulingTexture);
            if (youngSoldierShieldTexture != null)
                Destroy(youngSoldierShieldTexture);
            if (evacueePixelTexture != null)
                Destroy(evacueePixelTexture);
            if (farBankGuardTexture != null)
                Destroy(farBankGuardTexture);
            if (encirclingTorchTexture != null)
                Destroy(encirclingTorchTexture);
            if (farBankGuardWalkTexture != null)
                Destroy(farBankGuardWalkTexture);
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
                "Shen Li: 'After this war I want to open a shop. Let the quartermaster keep his paperwork. Anyone can bring me a job. I'll choose which ones to take. Will you come?'",
                "沈砾：“等这仗打完，我要开间铺子。军需官爱批不批，谁都能来问。活儿接不接，我自己挑。你来不来？”");
            instructions.text = Copy("Tell Shen Li what you think of her plan.",
                "告诉沈砾你的想法。");
            ShowChoices(new[] { "I'll come.", "Maybe later.", "Fix that nut first." },
                new[] { "来。", "再说吧。", "你先把那个螺母修好。" });
        }

        private void ShowChoices(string[] english, string[] chinese)
        {
            if (english == null || chinese == null || english.Length == 0 ||
                english.Length != chinese.Length || english.Length > choices.Length)
                throw new ArgumentException("S00 dialogue choices require matching authored English/Chinese labels.");

            openChoicesEnglish = english;
            openChoicesChinese = chinese;
            shownChoiceLanguage = null;
            for (int i = 0; i < choices.Length; i++)
                choices[i].gameObject.SetActive(i < english.Length);
            RefreshOpenChoiceLanguage();
            RefreshStageLayout(true);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(choices[0].gameObject);
        }

        private void RefreshOpenChoiceLanguage()
        {
            if (openChoicesEnglish == null || openChoicesChinese == null ||
                shownChoiceLanguage == SoundwellUiLocalization.Language)
                return;
            shownChoiceLanguage = SoundwellUiLocalization.Language;
            string[] labels = Chinese ? openChoicesChinese : openChoicesEnglish;
            for (int i = 0; i < choiceLabels.Length; i++)
            {
                bool visible = i < labels.Length;
                choices[i].gameObject.SetActive(visible);
                if (visible)
                    choiceLabels[i].text = labels[i];
            }
        }

        private void HideChoices()
        {
            openChoicesEnglish = null;
            openChoicesChinese = null;
            shownChoiceLanguage = null;
            foreach (Button choice in choices)
                choice.gameObject.SetActive(false);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        // An out-of-range interaction must explain what the amber station
        // requires instead of silently consuming the player's real E / Pad A.
        // This is only a UGUI hint: it cannot grant a post, warning or combat action.
        private void ShowBridgeStationOutOfReach()
        {
            if (instructions == null)
                return;
            switch (phase)
            {
                case 12:
                    instructions.text = Copy(
                        "Reach Shen Li's amber bridge post with WASD / left stick, then press E / Pad A.",
                        "用 WASD／左摇杆走到沈砾身边的金色桥头守位，再按 E／手柄 A。");
                    break;
                case 14:
                    instructions.text = Copy(
                        "Reach the amber lookout with WASD / left stick, then press E / Pad A to see what moved.",
                        "用 WASD／左摇杆走到金色瞭望点，再按 E／手柄 A 查看对岸动静。");
                    break;
                case 15:
                    instructions.text = Copy(
                        "Return to Shen Li's amber cable post with WASD / left stick, then press E / Pad A.",
                        "用 WASD／左摇杆回到沈砾身边的金色缆桩，再按 E／手柄 A。");
                    break;
            }
        }

        // Tell the player immediately when their REAL position makes the
        // amber station usable; stepping out again restores the approach cue.
        // The existing Ring action still decides whether the station is used.
        private void RefreshBridgeStationReachCue()
        {
            if (instructions == null || (phase != 12 && phase != 14 && phase != 15))
                return;
            bool reachable = phase == 12 ? CanTakeBridgePost :
                phase == 14 ? CanWarnFarBank : CanHoldCable;
            string locale = SoundwellUiLocalization.Language;
            string bindings = StationPromptBindingSignature();
            if (shownBridgeCuePhase == phase &&
                shownBridgeCueReachable == reachable &&
                shownBridgeCueLanguage == locale &&
                shownBridgeCueBindings == bindings)
                return;
            shownBridgeCuePhase = phase;
            shownBridgeCueReachable = reachable;
            shownBridgeCueLanguage = locale;
            shownBridgeCueBindings = bindings;
            if (!reachable)
            {
                ShowBridgeStationOutOfReach();
                return;
            }
            switch (phase)
            {
                case 12:
                    instructions.text = Copy(
                        "At Shen Li's amber bridge post: press E / Pad A to take position. WASD / left stick to reposition.",
                        "已到沈砾身边金色桥头守位：按 E／手柄 A 站定，WASD／左摇杆可调整位置。");
                    break;
                case 14:
                    instructions.text = Copy(
                        "At the amber lookout: press E / Pad A to see what moved. WASD / left stick to reposition.",
                        "已到金色瞭望点：按 E／手柄 A 查看对岸动静，WASD／左摇杆可调整位置。");
                    break;
                case 15:
                    instructions.text = Copy(
                        "At Shen Li's amber cable post: press E / Pad A to brace it. WASD / left stick to reposition.",
                        "已到沈砾身边金色缆桩：按 E／手柄 A 扶住缆索，WASD／左摇杆可调整位置。");
                    break;
            }
        }

        // Preserve fractional world motor movement for accurate reach and
        // input; only the 16px actor's Canvas presentation snaps to pixels.
        // Rounding the motor each frame would freeze slow/short inputs.
        private static Vector2 PixelSnappedWalkerPosition(Vector2 motor) =>
            new Vector2(Mathf.Round(motor.x), Mathf.Round(12f + motor.y));

        private void Update()
        {
            // Dialogue selection uses UI input, not the exploration motor.
            // Refresh visible options even when the player cannot walk.
            RefreshOpenChoiceLanguage();
            if (phase < 17)
                RefreshStageLayout();
            if (phase == 18)
            {
                // Resizing after the final bell cannot hide the only exit.
                RefreshEpilogueExitPosition();
                return;
            }
            if (phase == 1 && wrenchQuestionsOpen)
                return;
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
            marker.anchoredPosition = PixelSnappedWalkerPosition(walker);
            RefreshBridgeStationReachCue();
            if (phase == 15)
            {
                if (frame.Ring)
                {
                    if (CanHoldCable) HoldCableAtPost();
                    else ShowBridgeStationOutOfReach();
                }
                return;
            }
            if (phase == 14)
            {
                if (frame.Ring)
                {
                    if (CanWarnFarBank) WarnFarBank();
                    else ShowBridgeStationOutOfReach();
                }
                return;
            }
            if (phase == 12)
            {
                if (frame.Ring)
                {
                    if (CanTakeBridgePost) TakeBridgePost();
                    else ShowBridgeStationOutOfReach();
                }
                return;
            }
            if (phase == 7)
            {
                RefreshRescueStationReachCue();
                if (frame.Ring && CanRescue)
                    FinishRescue();
                return;
            }
            if (phase == 9)
            {
                RefreshEvacueePickupReachCue();
                if (frame.Ring)
                    HelpEvacuee();
                return;
            }
            if (phase == 10)
            {
                UpdateEvacueeFollow();
                // Show readiness only after BOTH bodies have reached the real
                // safe line. Keep the displayed action current after rebinds.
                bool ready = CanDeliverEvacuee;
                bool laneAligned = evacuee != null &&
                    Mathf.Abs(walker.y - evacuee.anchoredPosition.y) <= EvacueeContactReachY;
                string locale = SoundwellUiLocalization.Language;
                string bindings = StationPromptBindingSignature();
                if (ready && (!shownEvacueeSafeLineReachable ||
                    shownEvacueeSafeLineLanguage != locale ||
                    shownEvacueeSafeLineBindings != bindings))
                    instructions.text = Copy(
                        "At the green safe line: press E / Pad A to confirm the evacuee is safe.",
                        "已到绿色安全线：按 E／手柄 A 确认伤者安全撤离。");
                else if (!ready && !evacueeWaiting && !laneAligned &&
                    (shownEvacueeSafeLineLaneAligned ||
                     shownEvacueeSafeLineLanguage != locale ||
                     shownEvacueeSafeLineBindings != bindings))
                    instructions.text = Copy(
                        "Return to the bridge lane to meet the evacuee at the green safe line.",
                        "回到桥面通道，在绿色安全线与伤者会合。");
                else if (!ready && !evacueeWaiting && laneAligned &&
                    !shownEvacueeSafeLineLaneAligned)
                    instructions.text = Copy(
                        "Green: following. Stay close; press E / Pad A at the safe line.",
                        "绿色：伤者正在跟随。保持靠近，到安全线按 E／手柄 A。");
                else if (!ready && shownEvacueeSafeLineReachable && !evacueeWaiting)
                    instructions.text = Copy(
                        "Green: following. Stay close; press E / Pad A at the safe line.",
                        "绿色：伤者正在跟随。保持靠近，到安全线按 E／手柄 A。");
                shownEvacueeSafeLineReachable = ready;
                shownEvacueeSafeLineLaneAligned = laneAligned;
                shownEvacueeSafeLineLanguage = locale;
                shownEvacueeSafeLineBindings = bindings;
                if (frame.Ring)
                    DeliverEvacuee();
                return;
            }
            if (frame.Ring && CanRepair)
            {
                // Neither the first knock nor moving into tool reach may
                // credit a repair. Each E / Pad A is an actual gameplay action.
                clampKnocks++;
                if (clampKnocks == 1)
                {
                    narration.text = Copy(
                        "The 'Tax Collector' strikes the rusted nut once. It is still stuck.",
                        "“税务官”敲了生锈的螺母第一下，还卡着。");
                    instructions.text = Copy(
                        "Strike the clamp a second time: E / Pad A. The bridge is not repaired yet.",
                        "还要在缆桩敲第二下：E／手柄 A。桥还没有修好。");
                    return;
                }
                phase = 2;
                SoundwellInputAdapter.SetUiContext();
                narration.text = Copy(
                    "Two knocks loosen the nut. Shen Li: 'See? It understands. My wrench is called Inspector—leaks oil, but recognizes people. How shall we secure the cable and chain?'",
                    "敲过两下，螺母终于松了。沈砾：“看，它懂。我手里这把叫‘监察员’，漏油，但认人。缆索和铁链怎么固定？”");
                instructions.text = Copy(
                    "Choose how to secure the chain; the repair has not been recorded yet.",
                    "选择怎样固定铁链；尚未记入修缆结果。");
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
                stretcher.anchoredPosition = new Vector2(
                    Mathf.Round(Mathf.Lerp(-320f, 308f, Mathf.Clamp01(elapsed / 2f))), 23f);
                yield return null;
            }
            if (phase == 3 && next != null)
            {
                next.gameObject.SetActive(true);
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(next.gameObject);
            }
        }

        private void SkipCompletedPrologue()
        {
            if (phase != 0 || skipCompletedReplay == null ||
                !skipCompletedReplay.gameObject.activeInHierarchy || !HasEarnedThirdBell())
                return;

            // Reuse the normal earned S00 -> M00 owner callback. Do not replay
            // the third-bell animation, finish another quest, or mint rewards.
            skipCompletedReplay.interactable = false;
            thirdBellStarted = true;
            phase = 17;
            Close();
        }

        public void Close()
        {
            defenseEncounter?.Stop();
            // Leaving the unfinished prologue never awards quest experience.
            // Makinom retains only dialogue/repair outcomes already earned
            // through real player interactions.
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
