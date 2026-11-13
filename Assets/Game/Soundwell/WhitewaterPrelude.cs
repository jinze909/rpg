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
        private const float WalkSpeed = 175f;
        private const float ClampX = 178f;
        private const float RepairReach = 44f;
        private static readonly Color Ink = new Color(0.87f, 0.90f, 0.88f);
        private static readonly Color Brass = new Color(0.91f, 0.72f, 0.40f);
        private readonly Button[] choices = new Button[3];
        private readonly Text[] choiceLabels = new Text[3];
        private RectTransform marker;
        private RectTransform stretcher;
        private Image cable;
        private Text narration;
        private Text instructions;
        private Text title;
        private Button exit;
        private Action onExit;
        private int phase;
        private Vector2 walker = new Vector2(-245f, 0f);

        // 0: roll call; 1: walk to the clamp; 2: repair decision;
        // 3: visible bracing and stretcher crossing. Never a fake finished S00.
        public int Phase => phase;
        public Vector2 WalkerPosition => walker;
        public bool CanRepair => phase == 1 && Mathf.Abs(walker.x - ClampX) <= RepairReach &&
            Mathf.Abs(walker.y) <= RepairReach;

        private static bool Chinese => SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
        private static string Copy(string en, string zh) => Chinese ? zh : en;

        public void Begin(Action exitAction)
        {
            onExit = exitAction;
            phase = 0;
            // The existing InputSystemUIInputModule owns dialogue navigation.
            SoundwellInputAdapter.SetUiContext();
            SoundwellInputAdapter.EnsureEventSystem(transform);

            var canvas = new GameObject("S00 Whitewater playable canvas",
                typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
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
            exit = Button(canvas.transform, "S00 return to Bootstrap",
                new Vector2(0f, -248f), new Vector2(210f, 29f), Close);
            Label(exit.transform, Copy("Back to entry", "返回入口"), 14,
                Vector2.zero, new Vector2(208f, 28f), Ink);
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
                instructions.text = Copy("WASD / left stick: walk to the red clamp. E / Pad A: work.",
                    "WASD／左摇杆走到红色缆桩。按 E／手柄 A 进行加固。");
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
                instructions.text = Copy("S00-01 complete · the bridge can carry the wounded. Return to the entry for now.",
                    "S00-01 完成：伤员终于能过桥。此为第一段可玩切片，后续剧情尚未接入。");
                StartCoroutine(CrossStretcher());
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(exit.gameObject);
            }
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
            if (phase != 1)
                return;
            SoundwellControlFrame frame = SoundwellInputAdapter.ReadGameplay();
            Vector2 delta = Vector2.ClampMagnitude(frame.Movement, 1f) *
                WalkSpeed * Time.unscaledDeltaTime;
            walker = new Vector2(Mathf.Clamp(walker.x + delta.x, -325f, 275f),
                Mathf.Clamp(walker.y + delta.y, -43f, 43f));
            marker.anchoredPosition = new Vector2(walker.x, 12f + walker.y);
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
        }

        public void Close()
        {
            // Closing the partial prelude never starts an ORK encounter or
            // awards quest experience. Preserve only the Makinom dialogue
            // choice and cable-action facts for the eventual S00 continuation.
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
