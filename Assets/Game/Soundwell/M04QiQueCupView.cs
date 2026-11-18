using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Optional bilingual UGUI for Qi Que's tangible pre-departure gift.
    /// The scene host decides whether Qi Que actually met the player.
    /// </summary>
    public sealed class M04QiQueCupView : IDisposable
    {
        private readonly M04QiQueCupEncounter cup;
        private readonly GameObject canvasObject;
        private readonly RectTransform authored;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text dialogue;
        private readonly Button[] choices = new Button[3];
        private readonly Text[] choiceLabels = new Text[3];
        private readonly Button advance;
        private readonly Text advanceLabel;
        private Action<M04QiQueCupEncounter> onFinished;
        private bool delivered;
        private bool disposed;

        public M04QiQueCupEncounter Encounter => cup;
        public Text Dialogue => dialogue;
        public Button AdvanceButton => advance;
        public Button GetChoiceButton(int index)
        {
            if (index < 0 || index >= choices.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return choices[index];
        }

        public M04QiQueCupView(Transform parent, bool qiQuePresent, bool debtKnown,
            Action<M04QiQueCupEncounter> onFinished)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (onFinished == null) throw new ArgumentNullException(nameof(onFinished));
            this.onFinished = onFinished;
            cup = new M04QiQueCupEncounter(qiQuePresent, debtKnown);
            cup.Begin();
            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("M04 Qi Que cup canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 128;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            Panel(canvas.transform, "M04 dock backdrop", new Vector2(950f, 530f),
                new Color(0.04f, 0.07f, 0.08f, 0.98f));
            var parentPanel = new GameObject("M04 cup dialogue panel", typeof(RectTransform));
            parentPanel.transform.SetParent(canvas.transform, false);
            authored = (RectTransform)parentPanel.transform;
            authored.anchorMin = authored.anchorMax = new Vector2(0.5f, 0.5f);
            authored.sizeDelta = new Vector2(960f, 540f);
            Panel(authored, "M04 cup paper", new Vector2(920f, 515f),
                new Color(0.92f, 0.89f, 0.81f));
            heading = Label(authored, 24, new Vector2(0f, 210f), new Vector2(840f, 48f));
            dialogue = Label(authored, 20, new Vector2(0f, 75f), new Vector2(840f, 175f));
            dialogue.alignment = TextAnchor.MiddleLeft;
            for (int i = 0; i < choices.Length; i++)
            {
                int index = i;
                choices[i] = NewButton(authored, "M04 Qi cup choice " + i,
                    new Vector2(0f, -93f - i * 45f), new Vector2(810f, 40f),
                    out choiceLabels[i]);
                choices[i].onClick.AddListener(() => Choose(index));
            }
            advance = NewButton(authored, "M04 Qi cup continue",
                new Vector2(0f, -239f), new Vector2(460f, 38f), out advanceLabel);
            advance.onClick.AddListener(Advance);
            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            Refresh();
            SoundwellUiLocalization.Changed += Refresh;
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed || !(aspect > 0f) || float.IsNaN(aspect) || float.IsInfinity(aspect))
                return;
            float width = Mathf.Sqrt(960f * 540f * aspect);
            float height = Mathf.Sqrt(960f * 540f / aspect);
            float scale = Mathf.Min(1f, Mathf.Min(width / 960f, height / 540f));
            if (scale < 1f) scale *= 0.95f;
            authored.localScale = new Vector3(scale, scale, 1f);
        }

        public void Refresh()
        {
            if (disposed) return;
            if (canvasObject == null || heading == null)
            {
                Dispose();
                return;
            }
            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            var font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = dialogue.font = advanceLabel.font = font;
            heading.text = zh ? "出航前 · 祁雀的丑杯子" : "BEFORE DEPARTURE · QI QUE'S CUP";
            dialogue.text = M04QiQueCupCopy.Narration(cup, zh);
            string[] options = M04QiQueCupCopy.Choices(cup, zh);
            for (int i = 0; i < choices.Length; i++)
            {
                choiceLabels[i].font = font;
                choiceLabels[i].text = i < options.Length ? options[i] : "";
                choices[i].gameObject.SetActive(!delivered && i < options.Length);
            }
            advanceLabel.text = cup.Stage == M04QiQueCupStage.OfferingCup
                ? (zh ? "看看那只杯子" : "Look at the cup")
                : (zh ? "继续出航准备" : "Continue preparations");
            advance.gameObject.SetActive(!delivered &&
                (cup.Stage == M04QiQueCupStage.OfferingCup ||
                 cup.Stage == M04QiQueCupStage.Responding ||
                 cup.Stage == M04QiQueCupStage.WithoutCup));
            if (!delivered)
                SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform,
                    advance.gameObject.activeInHierarchy ? advance : choices[0]);
        }

        private void Choose(int index)
        {
            if (disposed || delivered || cup.Stage != M04QiQueCupStage.ChoosingReply)
                return;
            if (index < 0 || index >= M04QiQueCupCopy.Choices(cup, false).Length)
                return;
            cup.Choose((M04QiQueCupReply)index);
            Refresh();
        }

        private void Advance()
        {
            if (disposed || delivered)
                return;
            if (cup.Stage == M04QiQueCupStage.OfferingCup)
                cup.ExamineGift();
            else if (cup.Stage == M04QiQueCupStage.Responding ||
                cup.Stage == M04QiQueCupStage.WithoutCup)
            {
                cup.Finish();
                delivered = true;
                Refresh();
                var callback = onFinished;
                onFinished = null;
                callback?.Invoke(cup);
                return;
            }
            else
                return;
            Refresh();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            onFinished = null;
            SoundwellUiLocalization.Changed -= Refresh;
            foreach (var option in choices)
                option.onClick.RemoveAllListeners();
            advance.onClick.RemoveAllListeners();
            if (canvasObject == null) return;
            canvasObject.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(canvasObject);
            else UnityEngine.Object.DestroyImmediate(canvasObject);
        }

        private static void Panel(Transform parent, string name, Vector2 size, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(parent, false);
            var img = obj.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            var rect = img.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
        }

        private static Text Label(Transform parent, int size, Vector2 pos, Vector2 bounds)
        {
            var t = SoundwellView.AddLabel(parent, "", size,
                new Color(0.12f, 0.17f, 0.20f), pos, bounds);
            t.raycastTarget = false;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 10;
            t.resizeTextMaxSize = size;
            return t;
        }

        private static Button NewButton(Transform parent, string name,
            Vector2 pos, Vector2 size, out Text label)
        {
            var obj = new GameObject(name, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            var img = obj.GetComponent<Image>();
            img.color = new Color(0.82f, 0.83f, 0.77f);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var button = obj.GetComponent<Button>();
            button.targetGraphic = img;
            label = Label(obj.transform, 17, Vector2.zero, size);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(10f, 2f);
            label.rectTransform.offsetMax = new Vector2(-10f, -2f);
            return button;
        }
    }
}
