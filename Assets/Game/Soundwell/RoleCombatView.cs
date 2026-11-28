using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>One compact command surface for the live ORK encounter.</summary>
    public sealed class RoleCombatView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform top;
        private readonly RectTransform bottom;
        private readonly UnityEngine.UI.Text threat;
        private readonly UnityEngine.UI.Text exchange;
        private readonly UnityEngine.UI.Text[] cards = new UnityEngine.UI.Text[4];
        private readonly UnityEngine.UI.Image[] healthBars = new UnityEngine.UI.Image[4];
        private readonly UnityEngine.UI.Button[] commands = new UnityEngine.UI.Button[3];
        private readonly UnityEngine.UI.Text[] captions = new UnityEngine.UI.Text[3];
        private readonly OrkRoleEncounter encounter;
        private readonly Canvas canvas;
        private int focusedActor = -2;
        private float lastAspect;
        private bool disposed;

        public Canvas Canvas => canvas;
        public UnityEngine.UI.Button PrimaryButton => commands[0];
        public UnityEngine.UI.Button SecondaryButton => commands[1];
        public UnityEngine.UI.Button RetreatButton => commands[2];
        public UnityEngine.UI.Text ThreatLabel => threat;
        public UnityEngine.UI.Text ExchangeLabel => exchange;
        public bool IsDisposed => disposed || CanvasDestroyed();

        public RoleCombatView(Transform parent, OrkRoleEncounter encounter)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            if (encounter == null)
                throw new ArgumentNullException(nameof(encounter));

            this.encounter = encounter;
            canvasObject = new GameObject("Rescue battle HUD", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 180;
            var scaler = canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            top = Panel("Enemy intent", new Vector2(0.5f, 1f));
            bottom = Panel("Party commands", new Vector2(0.5f, 0f));
            threat = SoundwellUiTheme.CreateLabel(top, 18, Vector2.zero, new Vector2(850, 48), Color.white);
            threat.alignment = TextAnchor.MiddleCenter;
            exchange = SoundwellUiTheme.CreateLabel(bottom, 15, new Vector2(0, 50), new Vector2(850, 32), Color.white);
            for (int i = 0; i < 4; i++)
            {
                RectTransform parentPanel = i == 3 ? top : bottom;
                cards[i] = SoundwellUiTheme.CreateLabel(parentPanel, 15, Vector2.zero, new Vector2(250, 24), Color.white);
                var bar = new GameObject("ORK health " + i, typeof(RectTransform), typeof(UnityEngine.UI.Image));
                bar.transform.SetParent(parentPanel, false);
                healthBars[i] = bar.GetComponent<UnityEngine.UI.Image>();
                healthBars[i].color = i == 3 ? new Color(0.88f, 0.34f, 0.31f) : new Color(0.34f, 0.78f, 0.65f);
                healthBars[i].raycastTarget = false;
            }
            for (int i = 0; i < commands.Length; i++)
            {
                int index = i;
                commands[i] = SoundwellUiTheme.CreateButton(bottom, "Rescue command " + i,
                    Vector2.zero, new Vector2(250, 36), 17, out captions[i]);
                commands[i].onClick.AddListener(() => HandleCommandClick(index));
                commands[i].gameObject.AddComponent<SoundwellActionMenuCancelHandler>()
                    .Initialize(HandleCancel);
            }
            SoundwellInputAdapter.SetUiContext();
            foreach (Transform child in canvasObject.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = 5;
            Layout();
        }

        private bool CanvasDestroyed() => canvasObject == null || canvas == null;

        private bool IsDestroyedExternally()
        {
            if (CanvasDestroyed() || top == null || bottom == null || threat == null || exchange == null)
                return true;
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null || healthBars[i] == null)
                    return true;
            }
            for (int i = 0; i < commands.Length; i++)
            {
                if (commands[i] == null || captions[i] == null)
                    return true;
            }
            return false;
        }

        private void HandleCommandClick(int index)
        {
            if (disposed || IsDestroyedExternally())
            {
                Dispose();
                return;
            }
            if (encounter == null)
                return;
            if (index == 2)
            {
                encounter.Leave();
                return;
            }
            if (encounter.Party == null || encounter.Party.Beast == null || encounter.Party.Beast.Status == null)
                return;
            if (encounter.Party.Beast.Status.IsDead)
                encounter.Leave();
            else
                encounter.Choose(index);
        }

        private void HandleCancel()
        {
            if (disposed || IsDestroyedExternally())
            {
                Dispose();
                return;
            }
            if (encounter == null)
                return;
            encounter.Leave();
        }

        private RectTransform Panel(string name, Vector2 anchor)
        {
            var result = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            result.transform.SetParent(canvasObject.transform, false);
            var rect = (RectTransform)result.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            var image = result.GetComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.035f, 0.065f, 0.08f, 0.98f);
            image.raycastTarget = false;
            var outline = result.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0.58f, 0.53f, 0.39f);
            outline.effectDistance = Vector2.one;
            return rect;
        }

        private void Layout()
        {
            if (disposed || IsDestroyedExternally())
                return;
            Canvas.ForceUpdateCanvases();
            var canvasRect = canvas.transform as RectTransform;
            float rawWidth = canvasRect != null ? canvasRect.rect.width - 20f : 940f;
            if (float.IsNaN(rawWidth) || float.IsInfinity(rawWidth))
                rawWidth = 940f;
            float width = Mathf.Max(300f, rawWidth);
            top.sizeDelta = new Vector2(width, 86f);
            bottom.sizeDelta = new Vector2(width, 140f);
            top.anchoredPosition = new Vector2(0, -10);
            bottom.anchoredPosition = new Vector2(0, 10);
            foreach (RectTransform child in top)
                child.anchorMin = child.anchorMax = new Vector2(0.5f, 1f);
            foreach (RectTransform child in bottom)
                child.anchorMin = child.anchorMax = new Vector2(0.5f, 0f);
            threat.rectTransform.anchoredPosition = new Vector2(0, -26);
            threat.rectTransform.sizeDelta = new Vector2(width - 20, 44);
            cards[3].rectTransform.anchoredPosition = new Vector2(0, -60);
            healthBars[3].rectTransform.anchoredPosition = new Vector2(0, -76);
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * width / 3f;
                cards[i].rectTransform.anchoredPosition = new Vector2(x, 83);
                cards[i].rectTransform.sizeDelta = new Vector2(width / 3f - 12, 25);
                healthBars[i].rectTransform.anchoredPosition = new Vector2(x, 67);
                var buttonRect = commands[i].GetComponent<RectTransform>();
                if (buttonRect != null)
                {
                    buttonRect.anchoredPosition = new Vector2(x, 25);
                    buttonRect.sizeDelta = new Vector2(width / 3f - 12, 38);
                }
            }
            exchange.rectTransform.anchoredPosition = new Vector2(0, 119);
            exchange.rectTransform.sizeDelta = new Vector2(width - 18, 33);
            foreach (var label in captions)
            {
                label.rectTransform.sizeDelta = new Vector2(width / 3f - 22, 34);
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 11;
            }
            Camera camera = Camera.main;
            if (camera != null)
            {
                float scale = canvas.scaleFactor;
                if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f)
                    scale = 1f;
                float lower = 160f * scale / Mathf.Max(1, Screen.height);
                float upper = 106f * scale / Mathf.Max(1, Screen.height);
                camera.rect = new Rect(0, lower, 1, Mathf.Max(0.25f, 1f - lower - upper));
            }
        }

        public void Refresh(string lastExchange, bool ending)
        {
            if (disposed || IsDestroyedExternally())
            {
                Dispose();
                return;
            }
            if (encounter == null || encounter.Party == null || encounter.Party.Beast == null ||
                encounter.Party.Members == null || encounter.Party.Members.Count < 3)
            {
                return;
            }
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            if (!float.IsNaN(aspect) && !float.IsInfinity(aspect) && !Mathf.Approximately(aspect, lastAspect))
            {
                lastAspect = aspect;
                Layout();
                if (disposed || IsDestroyedExternally())
                {
                    Dispose();
                    return;
                }
            }
            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            int active = encounter.Party.ChoosingRescuerIndex;
            Font font = SoundwellUiLocalization.PresentationFont(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            threat.font = exchange.font = font;
            if (!ending && encounter.ActiveEnemyAction is { } action)
            {
                (threat.text, exchange.text) = EnemyActionCopy(action, zh);
            }
            else
            {
                threat.text = ending ? (zh ? "交锋结束" : "ENCOUNTER ENDED") :
                    encounter.IsInterrupted ? (zh ? "打断成功 · 下次重击可追击破绽" : "INTERRUPTED · Exploit the opening with a heavy strike") :
                    encounter.PendingThreat == OrkRoleEncounter.Threat.Howl
                        ? (zh ? "回声嚎叫 → 全队 · 湿布可以封住声口" : "ECHO HOWL → Whole party · Muffle the echo")
                        : (zh ? "蓄力猛扑 → " : "CHARGED LUNGE → ") + RoleName(encounter.TargetIndex, zh);
                exchange.text = lastExchange ?? string.Empty;
            }
            float width = bottom.sizeDelta.x;
            for (int i = 0; i < cards.Length; i++)
            {
                var actor = i == 3 ? encounter.Party.Beast : encounter.Party.Members[i];
                if (actor == null || actor.Status == null)
                    continue;
                int hp = actor.Status[1].GetValue();
                int max = Mathf.Max(1, actor.Status[1].GetMaxValue());
                cards[i].font = font;
                cards[i].text = (i == 3 ? (zh ? "借声兽" : "Echo beast") : RoleName(i, zh)) + "  " + hp + "/" + max;
                cards[i].color = i == active ? new Color(1f, 0.82f, 0.40f) : Color.white;
                healthBars[i].rectTransform.sizeDelta = new Vector2(
                    (i == 3 ? width * 0.55f : width / 3f - 24f) * Mathf.Clamp01((float)hp / max), 5);
            }
            string[] pair = Commands(active, zh);
            bool beastDead = encounter.Party.Beast.Status != null && encounter.Party.Beast.Status.IsDead;
            captions[0].text = ending
                ? beastDead
                    ? (zh ? "救出测路人" : "Rescue surveyor")
                    : (zh ? "回港整队" : "Regroup")
                : pair[0];
            captions[1].text = pair[1];
            captions[2].text = zh ? "撤退" : "Retreat";
            foreach (var caption in captions) caption.font = font;
            commands[0].interactable = !encounter.IsPresenting && (ending || encounter.CanChoose(0));
            commands[1].interactable = !ending && encounter.CanChoose(1);
            commands[2].interactable = !encounter.IsPresenting;
            if (active != focusedActor && active >= 0 && !encounter.IsPresenting)
            {
                focusedActor = active;
                SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform,
                    commands[0].interactable ? commands[0] : commands[1]);
            }
        }

        private static (string Threat, string Exchange) EnemyActionCopy(
            (OrkRoleEncounter.Threat Kind, int TargetIndex, bool Interrupted) action, bool zh)
        {
            if (action.Kind == OrkRoleEncounter.Threat.Gather)
                return zh ? ("借声兽正在蓄力", "借声兽压低身体，蓄势待发。")
                    : ("ECHO BEAST GATHERING STRENGTH", "The beast crouches and gathers strength.");

            bool howl = action.Kind == OrkRoleEncounter.Threat.Howl;
            string target = howl ? (zh ? "全队" : "Whole party") : RoleName(action.TargetIndex, zh);
            if (action.Interrupted)
            {
                string cancelled = howl ? (zh ? "回声嚎叫已打断" : "ECHO HOWL INTERRUPTED")
                    : (zh ? "猛扑已打断" : "LUNGE INTERRUPTED");
                return (cancelled + " → " + target, zh ? "袭击已被打断，小队毫发无伤。"
                    : "Interrupted. The party takes no damage.");
            }

            return howl
                ? ((zh ? "回声嚎叫中" : "ECHO HOWL IN PROGRESS") + " → " + target,
                    zh ? "回声冲向全队。" : "The echo sweeps over the party.")
                : ((zh ? "猛扑中" : "LUNGE IN PROGRESS") + " → " + target,
                    zh ? "借声兽猛扑向" + target + "。" : "The beast lunges at the " + target + ".");
        }

        private static string RoleName(int index, bool zh) => index == 0
            ? (zh ? "前卫" : "Vanguard") : index == 1
            ? (zh ? "破阵手" : "Breaker") : (zh ? "支援手" : "Support");

        private static string[] Commands(int index, bool zh)
        {
            if (index == 0) return zh ? new[] { "盾击", "举盾援护" } : new[] { "Shield bash", "Cover party" };
            if (index == 1) return zh ? new[] { "破绽重击", "敲铃断扑" } : new[] { "Heavy strike", "Decoy bell" };
            if (index == 2) return zh ? new[] { "湿布消音", "包扎" } : new[] { "Muffle howl", "Bandage" };
            return zh ? new[] { "等待交锋", "等待交锋" } : new[] { "Resolving…", "Resolving…" };
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            for (int i = 0; i < commands.Length; i++)
            {
                if (commands[i] != null)
                    commands[i].onClick.RemoveAllListeners();
            }
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }
}
