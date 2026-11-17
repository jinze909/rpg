using System;
using GamingIsLove.Makinom;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// The first playable M00 slice: inspect the memorial, observe Hao Lie's
    /// mother, and decide whether to prompt Xiaodou. Makinom stores only the
    /// two authored facts; transient scene presentation stays local.
    /// </summary>
    public sealed class M00MemorialSquare : MonoBehaviour
    {
        public const string OldWomanStoneKey = "rpg.return.m00.old-woman-stone";
        public const string XiaodouPromptedKey = "rpg.return.m00.xiaodou-prompted";

        private const float WalkSpeed = 175f;
        private const float LaneY = 12f;
        private const float ReachX = 40f;
        private const float ReachY = 28f;
        private const float StatueX = 70f;
        private const float OldWomanX = 210f;
        private const float StageX = -45f;
        private const float ChildForgetsAfter = 1.8f;

        private static readonly Color Ink = new Color(0.12f, 0.17f, 0.20f);
        private static readonly Color Gold = new Color(0.62f, 0.39f, 0.16f);
        private static readonly Color Stone = new Color(0.54f, 0.56f, 0.53f);
        private static readonly string[] StatueReactionEnglish =
        {
            "The nose is too straight.",
            "Who picked the cloak? I did not even have a blanket then.",
            "Take it down.",
            "Who paid for this?"
        };
        private static readonly string[] StatueReactionChinese =
        {
            "鼻子雕得太直了。",
            "谁给挑的披风？我那会儿连被子都没有。",
            "拆了吧。",
            "这钱是谁捐的？"
        };
        private static readonly string[] XiaodouChoiceEnglish =
        {
            "Step forward and prompt: “Who has spare rope?”",
            "Turn away and go look for someone.",
            "Stay where you are."
        };
        private static readonly string[] XiaodouChoiceChinese =
        {
            "上前提词：“谁带了多余的绳子。”",
            "绕开，去找人",
            "留在原地"
        };

        private static readonly string[] PlayerPixels =
        {
            "................",
            "......kkkk......",
            ".....khhhhk.....",
            ".....khffhk.....",
            ".....khffhk.....",
            "......kffk......",
            ".....kcccck.....",
            "....kcccccck....",
            "....kccggcck....",
            "....kcccccck....",
            "......kbbk......",
            "......kbbk......",
            ".....kk..kk.....",
            ".....kk..kk.....",
            "....kk....kk....",
            "................"
        };
        private static readonly string[] OldWomanPixels =
        {
            "................",
            ".....kkkkkk.....",
            "....khhhhkk.....",
            "....khffhhk.....",
            "....khffhhk.....",
            ".....kffk.......",
            "....kccccck.....",
            "...kccccccck....",
            "...kccggccck....",
            "...kccccccck....",
            "....kcc..cck....",
            "....kbb..bbk....",
            "...kk......kk...",
            "................",
            "................",
            "................"
        };
        private static readonly string[] ChildPixels =
        {
            "................",
            "......kkkk......",
            ".....khhhhk.....",
            ".....khffhk.....",
            ".....khffhk.....",
            "......kffk......",
            ".....kcccck.....",
            "....kcccccck....",
            "....kccggcck....",
            "....kcccccck....",
            "....kcccccck....",
            "....kcccccck....",
            "....kbb..bbk....",
            "....kbb..bbk....",
            "...kk......kk...",
            "................"
        };
        private static readonly string[] WatcherPixels =
        {
            "................",
            "......kkkk......",
            ".....khhhhk.....",
            ".....khffhk.....",
            ".....khffhk.....",
            "......kffk......",
            ".....kcccck.....",
            "....kcccccck....",
            "....kccwwcck....",
            "....kcccccck....",
            "....kcccccck....",
            "......kbbk......",
            "......kbbk......",
            ".....kk..kk.....",
            "....kk....kk....",
            "................"
        };
        private static readonly string[] StatuePixels =
        {
            "................",
            "......kkkk......",
            ".....kggggk.....",
            ".....kggggk.....",
            "......kggk......",
            ".....kggggk.....",
            "....kggggggk....",
            "....kggggggk....",
            ".....kggggk.....",
            ".....kggggk.....",
            "......kggk......",
            ".....kggggk.....",
            "....kggggggk....",
            "....kggggggk....",
            ".....kkkkkk.....",
            "................"
        };

        private Canvas canvas;
        private RectTransform player;
        private Image oldWomanImage;
        private Image childImage;
        private Text narration;
        private Text instructions;
        private Text inscription;
        private Text childLine;
        private Text crowdLine;
        private Button[] choices = new Button[4];
        private Button returnButton;
        private Sprite playerSprite;
        private Sprite oldWomanSprite;
        private Sprite childSprite;
        private Sprite watcherSprite;
        private Sprite statueSprite;
        private Texture2D playerTexture;
        private Texture2D oldWomanTexture;
        private Texture2D childTexture;
        private Texture2D watcherTexture;
        private Texture2D statueTexture;
        private Action onExit;
        private Vector2 walker = new Vector2(-360f, 0f);
        private float stageStartedAt;
        private int phase;
        private int statueReaction = -1;
        private bool childForgotLine;
        private bool oldWomanStoneObserved;
        private bool xiaodouPrompted;
        private bool closing;

        // 0: approach statue; 1: statue reaction; 2: approach old woman;
        // 3: approach Xiaodou; 4: prompt/leave/stay choice; 5: return.
        public int Phase => phase;
        public Vector2 WalkerPosition => walker;
        public bool OldWomanStoneObserved => oldWomanStoneObserved;
        public bool XiaodouPrompted => xiaodouPrompted;
        public int StatueReaction => statueReaction;
        public string ChildLineText => childLine != null ? childLine.text : string.Empty;
        public string CurrentNarration => narration != null ? narration.text : string.Empty;
        public string CurrentInstructions => instructions != null ? instructions.text : string.Empty;
        public bool CanInspectStatue => phase == 0 && WithinReach(StatueX);
        public bool CanInspectOldWoman => phase == 2 && WithinReach(OldWomanX);
        public bool CanPromptChild => phase == 3 && childForgotLine && WithinReach(StageX);

        public void Begin(Action exitAction)
        {
            if (canvas != null)
                throw new InvalidOperationException("M00 memorial square has already begun.");
            onExit = exitAction;
            phase = 0;
            SoundwellInputAdapter.EnsureEventSystem(transform);
            SoundwellInputAdapter.SetGameplayContext();
            BuildScene();
            RefreshObjective();
        }

        private void BuildScene()
        {
            CreateSprites();
            var canvasObject = new GameObject("M00 memorial square canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            Panel(canvas.transform, "M00 daylight harbor sky",
                Vector2.zero, new Vector2(960f, 540f),
                new Color(0.69f, 0.78f, 0.77f));
            Panel(canvas.transform, "M00 distant harbor water",
                new Vector2(0f, 34f), new Vector2(960f, 115f),
                new Color(0.35f, 0.57f, 0.61f));
            Panel(canvas.transform, "M00 memorial square paving",
                new Vector2(0f, -112f), new Vector2(960f, 220f),
                new Color(0.61f, 0.62f, 0.57f));
            Panel(canvas.transform, "M00 crowd line",
                new Vector2(0f, -4f), new Vector2(900f, 22f),
                new Color(0.48f, 0.52f, 0.49f));

            Label(canvas.transform,
                Copy("M00 · Your Name Carved in Stone", "M00 · 你的名字刻在石头上"),
                27, Ink, new Vector2(0f, 232f), new Vector2(820f, 38f));
            Label(canvas.transform,
                Copy("Memorial Day · Linchao Harbor", "纪念日 · 临潮港"),
                16, Ink, new Vector2(0f, 201f), new Vector2(820f, 28f));
            narration = Label(canvas.transform,
                Copy("Daytime. Linchao Harbor. The sea wind carries a vendor’s call for “hero sugar figures.” Children line up to touch the memorial; one patch of stone has been polished bright.",
                    "白天。临潮港。海风里有人叫卖“英雄糖人”。孩子们排着队，摸石碑上刻的字，那一块已经摸得发亮。"),
                16, Ink, new Vector2(0f, 151f), new Vector2(870f, 66f));
            narration.alignment = TextAnchor.MiddleCenter;

            // The polished memorial stone and the statue remain visible from
            // the start. The epitaph is revealed only after a real approach.
            Panel(canvas.transform, "M00 memorial stone",
                new Vector2(StatueX, 29f), new Vector2(118f, 112f), Stone);
            var statueObject = Panel(canvas.transform, "M00 memorial statue",
                new Vector2(StatueX, 112f), new Vector2(40f, 40f),
                Color.white, statueSprite);
            statueObject.rectTransform.sizeDelta = new Vector2(40f, 40f);
            inscription = Label(canvas.transform, "", 12, Ink,
                new Vector2(StatueX, 25f), new Vector2(190f, 102f));
            inscription.gameObject.SetActive(false);

            oldWomanImage = Panel(canvas.transform, "M00 old woman wiping Hao Lie’s name",
                new Vector2(OldWomanX, LaneY), new Vector2(32f, 32f),
                Color.white, oldWomanSprite);
            // The blue-cuffed former soldiers watch from the crowd edge.
            Panel(canvas.transform, "M00 blue-cuffed watcher left",
                new Vector2(-275f, LaneY), new Vector2(32f, 32f),
                Color.white, watcherSprite);
            Panel(canvas.transform, "M00 blue-cuffed watcher right",
                new Vector2(-230f, LaneY), new Vector2(32f, 32f),
                new Color(0.88f, 0.93f, 0.95f), watcherSprite);

            Panel(canvas.transform, "M00 small stage",
                new Vector2(StageX, 28f), new Vector2(176f, 18f),
                new Color(0.37f, 0.25f, 0.18f));
            childImage = Panel(canvas.transform, "M00 Xiaodou in oversized uniform",
                new Vector2(StageX, 72f), new Vector2(32f, 32f),
                Color.white, childSprite);
            childLine = Label(canvas.transform, "", 16, Ink,
                new Vector2(StageX, 132f), new Vector2(360f, 30f));
            childLine.gameObject.SetActive(false);
            crowdLine = Label(canvas.transform, "", 13, Ink,
                new Vector2(StageX, 108f), new Vector2(360f, 24f));
            crowdLine.gameObject.SetActive(false);

            var playerImage = Panel(canvas.transform, "M00 player",
                new Vector2(walker.x, LaneY), new Vector2(32f, 32f),
                Color.white, playerSprite);
            player = playerImage.rectTransform;
            playerImage.raycastTarget = false;
            playerImage.preserveAspect = true;
            playerImage.sprite = playerSprite;
            playerImage.rectTransform.sizeDelta = new Vector2(32f, 32f);
            playerImage.gameObject.name = "M00 player";
            playerImage.raycastTarget = false;

            instructions = Label(canvas.transform, "", 15, Ink,
                new Vector2(0f, -224f), new Vector2(880f, 34f));
            for (int i = 0; i < choices.Length; i++)
                choices[i] = CreateChoiceButton(i,
                    new Vector2(0f, -34f - i * 43f));
            returnButton = CreateButton("M00 return to entry",
                Copy("Return to entry", "返回入口"),
                new Vector2(0f, -165f), new Vector2(300f, 42f), Close);
            returnButton.gameObject.SetActive(false);
        }

        private void CreateSprites()
        {
            playerSprite = SoundwellPixelSpriteBuilder.Create16Colored(
                PlayerPixels, "M00 16px returning survivor",
                new Color32(82, 119, 143, 255), out playerTexture);
            oldWomanSprite = SoundwellPixelSpriteBuilder.Create16Colored(
                OldWomanPixels, "M00 16px old woman wiping the stone",
                new Color32(128, 132, 128, 255), out oldWomanTexture);
            childSprite = SoundwellPixelSpriteBuilder.Create16Colored(
                ChildPixels, "M00 16px Xiaodou in oversized uniform",
                new Color32(151, 117, 79, 255), out childTexture);
            watcherSprite = SoundwellPixelSpriteBuilder.Create16Colored(
                WatcherPixels, "M00 16px blue-cuffed former soldier",
                new Color32(74, 95, 112, 255), out watcherTexture);
            statueSprite = SoundwellPixelSpriteBuilder.Create16Colored(
                StatuePixels, "M00 16px memorial statue",
                new Color32(180, 181, 169, 255), out statueTexture);
        }

        private static Image Panel(Transform parent, string name,
            Vector2 position, Vector2 size, Color color, Sprite sprite = null)
        {
            var obj = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(parent, false);
            var image = obj.GetComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.raycastTarget = false;
            image.preserveAspect = sprite != null;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return image;
        }

        private static Text Label(Transform parent, string value, int fontSize,
            Color color, Vector2 position, Vector2 size)
        {
            Text label = SoundwellView.AddLabel(parent, value, fontSize,
                color, position, size);
            label.font = SoundwellUiLocalization.PresentationFont(label.font);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Mathf.Max(10, fontSize - 4);
            label.resizeTextMaxSize = fontSize;
            return label;
        }

        private Button CreateChoiceButton(int index, Vector2 position)
        {
            Button button = CreateButton("M00 choice " + index, "",
                position, new Vector2(700f, 38f), () => SelectChoice(index));
            button.gameObject.SetActive(false);
            return button;
        }

        private Button CreateButton(string name, string caption, Vector2 position,
            Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var obj = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            obj.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            obj.GetComponent<Image>().color = new Color(0.16f, 0.24f, 0.28f, 0.98f);
            var button = obj.GetComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.91f, 0.69f);
            colors.selectedColor = new Color(1f, 0.84f, 0.54f);
            colors.pressedColor = new Color(0.88f, 0.68f, 0.39f);
            button.colors = colors;
            button.onClick.AddListener(action);
            Text label = Label(rect, caption, 15, new Color(0.94f, 0.94f, 0.89f),
                Vector2.zero, size - new Vector2(16f, 4f));
            label.raycastTarget = false;
            return button;
        }

        private bool WithinReach(float targetX) =>
            Mathf.Abs(walker.x - targetX) <= ReachX &&
            Mathf.Abs(walker.y) <= ReachY &&
            Mathf.Abs(walker.y - LaneY) <= ReachY;

        private bool NearStage => WithinReach(StageX);

        private void Update()
        {
            if (phase != 0 && phase != 2 && phase != 3)
                return;

            if (phase == 3 && !childForgotLine &&
                Time.unscaledTime - stageStartedAt >= ChildForgetsAfter)
            {
                childForgotLine = true;
                childLine.text = Copy(
                    "Xiaodou (losing the line): “...the bridge...”",
                    "小豆（接下去，忘词了）：……桥……");
                crowdLine.text = Copy(
                    "Someone in the crowd laughs. The child reddens.",
                    "台下有人笑，孩子红了脸。");
                crowdLine.gameObject.SetActive(true);
                childImage.color = new Color(1f, 0.80f, 0.78f);
            }

            var frame = SoundwellInputAdapter.ReadGameplay();
            if (frame.Back)
            {
                Close();
                return;
            }

            Vector2 delta = Vector2.ClampMagnitude(frame.Movement, 1f) *
                WalkSpeed * Time.unscaledDeltaTime;
            walker = new Vector2(
                Mathf.Clamp(walker.x + delta.x, -430f, 430f),
                Mathf.Clamp(walker.y + delta.y, -42f, 42f));
            player.anchoredPosition = new Vector2(
                Mathf.Round(walker.x), Mathf.Round(LaneY + walker.y));
            RefreshObjective();

            if (!frame.Ring)
                return;

            if (phase == 0)
            {
                if (CanInspectStatue)
                    InspectStatue();
                else
                    RefreshObjective();
            }
            else if (phase == 2)
            {
                if (CanInspectOldWoman)
                    InspectOldWoman();
                else
                    RefreshObjective();
            }
            else if (phase == 3)
            {
                if (CanPromptChild)
                    ShowXiaodouChoices();
                else
                    RefreshObjective();
            }
        }

        private void RefreshObjective()
        {
            if (instructions == null)
                return;
            string movement = SoundwellInputAdapter.GameplayKeyboardMovementLabel();
            string keyboard = SoundwellInputAdapter.GameplayKeyboardInteractionLabel();
            string gamepad = SoundwellInputAdapter.GameplayGamepadInteractionLabel();
            if (string.IsNullOrEmpty(movement))
                movement = Copy("move", "移动");
            if (string.IsNullOrEmpty(keyboard))
                keyboard = Copy("Interact", "互动");
            if (string.IsNullOrEmpty(gamepad))
                gamepad = Copy("confirm", "确认");
            string controls = Copy(
                "Move " + movement + " / left stick · Interact " + keyboard + " / Pad " + gamepad + ". ",
                "移动 " + movement + "／左摇杆 · 互动 " + keyboard + "／手柄 " + gamepad + "。");

            string objective;
            if (phase == 0)
                objective = CanInspectStatue
                    ? Copy("At the statue: interact to read the inscription.", "已到雕像旁：按互动键阅读碑文。")
                    : Copy("Approach the statue and read the name carved in stone.", "走近石碑，看看刻在石头上的名字。");
            else if (phase == 2)
                objective = CanInspectOldWoman
                    ? Copy("Observe the old woman wiping the name beside the stone.", "观察石碑旁擦拭名字的老妇人。")
                    : Copy("Approach the old woman beside the memorial stone.", "走近纪念碑旁擦拭名字的老妇人。");
            else if (CanPromptChild)
                objective = Copy("At the stage: press interact to respond to Xiaodou’s forgotten line.", "已到高台旁：按互动键回应小豆忘掉的台词。");
            else if (NearStage && !childForgotLine)
                objective = Copy("Wait. Xiaodou is still reciting; he has not forgotten the line yet.", "稍等。小豆还在背词，还没有忘词。");
            else
                objective = Copy("Approach the stage. Xiaodou is performing for the crowd.", "走近高台，小豆正在为人群表演。");

            string text = controls + objective;
            if (instructions.text != text)
                instructions.text = text;
        }

        private void InspectStatue()
        {
            phase = 1;
            inscription.text = Copy(
                "Your name is carved beneath the memorial’s claim:\n“The enemy burned the pontoon bridge. The Third Brigade held the bridgehead; all perished.”",
                "你的名字刻在这段碑文之下：\n“敌军火攻浮桥，第三旅死守桥头，全员殉国。”");
            inscription.gameObject.SetActive(true);
            narration.text = Copy(
                "The statue stands above the polished stone. The epitaph says the entire Third Brigade died defending the bridge.",
                "雕像立在已经摸得发亮的石碑上方。碑文说，第三旅死守浮桥，全员殉国。");
            SetChoices(StatueReactionEnglish, StatueReactionChinese, 4);
        }

        private void InspectOldWoman()
        {
            oldWomanStoneObserved = true;
            oldWomanImage.color = new Color(1f, 0.93f, 0.82f);
            if (Maki.Initialized && Maki.Game?.Variables != null)
                Maki.Game.Variables.Set(OldWomanStoneKey, 1);
            phase = 3;
            childForgotLine = false;
            stageStartedAt = Time.unscaledTime;
            childLine.text = Copy(
                "Xiaodou (loudly): “Where there’s a bridge, I’m there!”",
                "小豆（台词，背得很响）：桥在我在！");
            childLine.gameObject.SetActive(true);
            crowdLine.gameObject.SetActive(false);
            childImage.color = Color.white;
            narration.text = Copy(
                "She is wiping the name “3rd Brigade, 1st Battalion, 3rd Platoon · Hao Lie.” Someone presses past; she steps aside half a pace without arguing. When they pass, she resumes.",
                "她擦的名字是“第三旅一营三班·郝烈”。有人挤过来，她不争，只往旁边让了半步，等那人走了又继续擦。");
            RefreshObjective();
        }

        private void ShowXiaodouChoices()
        {
            phase = 4;
            narration.text = Copy(
                "Xiaodou has lost the next line. Someone in the crowd laughs; the child’s face reddens.",
                "小豆接下去忘词了。台下有人笑，孩子红了脸。");
            SetChoices(XiaodouChoiceEnglish, XiaodouChoiceChinese, 3);
        }

        private void SetChoices(string[] english, string[] chinese, int count)
        {
            SoundwellInputAdapter.SetUiContext();
            for (int i = 0; i < choices.Length; i++)
            {
                bool active = i < count;
                choices[i].gameObject.SetActive(active);
                if (!active)
                    continue;
                Text label = choices[i].GetComponentInChildren<Text>(true);
                label.font = SoundwellUiLocalization.PresentationFont(label.font);
                label.text = Copy(english[i], chinese[i]);
            }
            if (EventSystem.current != null && count > 0)
                EventSystem.current.SetSelectedGameObject(choices[0].gameObject);
        }

        private void SelectChoice(int index)
        {
            if (phase == 1 && index >= 0 && index < StatueReactionEnglish.Length)
            {
                statueReaction = index;
                phase = 2;
                HideChoices();
                SoundwellInputAdapter.SetGameplayContext();
                narration.text = Copy(StatueReactionEnglish[index], StatueReactionChinese[index]);
                RefreshObjective();
                return;
            }

            if (phase != 4 || index < 0 || index >= XiaodouChoiceEnglish.Length)
                return;
            xiaodouPrompted = index == 0;
            if (Maki.Initialized && Maki.Game?.Variables != null)
                Maki.Game.Variables.Set(XiaodouPromptedKey, xiaodouPrompted ? 1 : 0);
            phase = 5;
            HideChoices();
            SoundwellInputAdapter.SetUiContext();
            narration.text = index == 0
                ? Copy("You step forward and give Xiaodou the line: “Who has spare rope?”",
                    "你上前提词：“谁带了多余的绳子。”")
                : index == 1
                    ? Copy("You turn away from the stage and go look for someone.",
                        "你绕开高台，去找人。")
                    : Copy("You stay where you are.", "你留在原地。");
            instructions.text = Copy(
                "The ceremony continues beyond the square. Return to the harbor entrance for now.",
                "纪念仪式还在继续，暂时先返回港口入口。");
            returnButton.gameObject.SetActive(true);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(returnButton.gameObject);
        }

        private void HideChoices()
        {
            foreach (Button choice in choices)
                choice.gameObject.SetActive(false);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        private static string Copy(string english, string chinese) =>
            SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese
                ? chinese : english;

        public void Close()
        {
            if (closing)
                return;
            closing = true;
            SoundwellInputAdapter.SetEntryContext();
            Action callback = onExit;
            onExit = null;
            callback?.Invoke();
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (playerSprite != null) Destroy(playerSprite);
            if (oldWomanSprite != null) Destroy(oldWomanSprite);
            if (childSprite != null) Destroy(childSprite);
            if (watcherSprite != null) Destroy(watcherSprite);
            if (statueSprite != null) Destroy(statueSprite);
            if (playerTexture != null) Destroy(playerTexture);
            if (oldWomanTexture != null) Destroy(oldWomanTexture);
            if (childTexture != null) Destroy(childTexture);
            if (watcherTexture != null) Destroy(watcherTexture);
            if (statueTexture != null) Destroy(statueTexture);
        }
    }
}