using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    public enum M04DockPreflightStage
    {
        DawnSupply,
        VerifyVoyageRoute,
        VerifyPhysicalReadiness,
        Finished
    }

    /// <summary>
    /// Last playable M04 dockside conversation after Qi Que's cup is recorded.
    /// Route is read from this visit's M03 Makinom record by the host.
    /// It cannot turn a chosen route into a verified vessel or an actual
    /// cast-off: those require a separate, real M04 departure manifest.
    /// </summary>
    public sealed class M04DockPreflightView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text dialogue;
        private readonly Text advanceLabel;
        private readonly Button advance;
        private Action onFinished;
        private bool delivering;
        private bool disposed;

        public M03VoyageChoice Route { get; }
        public M04DockPreflightStage Stage { get; private set; }
        public Button AdvanceButton => advance;
        public Text Dialogue => dialogue;
        public RectTransform Panel => panel;

        public M04DockPreflightView(Transform parent,
            M03VoyageChoice route, Action onFinished)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (!Enum.IsDefined(typeof(M03VoyageChoice), route))
                throw new ArgumentOutOfRangeException(nameof(route));
            this.onFinished = onFinished ?? throw new ArgumentNullException(nameof(onFinished));
            Route = route;
            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            canvasObject = new GameObject("M04 dock preflight canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 129;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var holder = new GameObject("M04 dock lower dialogue",
                typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(914f, 229f);
            panel.anchoredPosition = new Vector2(0f, 123f);

            // Do not use an opaque full-screen canvas behind the dock map.
            SoundwellUiTheme.CreatePanel(panel, "M04 preflight thin border",
                new Vector2(914f, 229f),
                new Color(0.67f, 0.60f, 0.45f, 1f));
            SoundwellUiTheme.CreatePanel(panel, "M04 preflight dark backing",
                new Vector2(910f, 225f),
                new Color(0.06f, 0.10f, 0.12f, 0.97f));

            var paper = new Color(0.93f, 0.89f, 0.81f, 1f);
            heading = SoundwellUiTheme.CreateLabel(panel, 20,
                new Vector2(0f, 91f), new Vector2(858f, 32f), paper);
            heading.alignment = TextAnchor.MiddleLeft;
            dialogue = SoundwellUiTheme.CreateLabel(panel, 18,
                new Vector2(0f, 14f), new Vector2(858f, 137f), paper);
            dialogue.alignment = TextAnchor.MiddleLeft;
            dialogue.horizontalOverflow = HorizontalWrapMode.Wrap;
            advance = SoundwellUiTheme.CreateButton(panel,
                "M04 dock preflight advance",
                new Vector2(295f, -91f), new Vector2(252f, 36f),
                16, out advanceLabel);
            advance.onClick.AddListener(Advance);
            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            Refresh();
            SoundwellUiLocalization.Changed += Refresh;
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed) return;
            if (canvasObject == null || panel == null)
            {
                Dispose();
                return;
            }
            SoundwellUiTheme.ApplyViewportAspectScale(panel, aspect);
        }

        public void Refresh()
        {
            if (disposed) return;
            if (canvasObject == null || panel == null || heading == null ||
                dialogue == null || advance == null || advanceLabel == null)
            {
                Dispose();
                return;
            }
            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = dialogue.font = advanceLabel.font = font;
            heading.text = M04DockPreflightCopy.Title(Stage, zh);
            dialogue.text = M04DockPreflightCopy.Describe(Stage, Route, zh);
            advanceLabel.text = Stage == M04DockPreflightStage.VerifyPhysicalReadiness
                ? (zh ? "返回码头准备" : "Return to dock preparations")
                : (zh ? "继续核对" : "Continue checking");
            advance.gameObject.SetActive(Stage != M04DockPreflightStage.Finished);
            advance.interactable = !delivering && Stage != M04DockPreflightStage.Finished;
            if (advance.interactable)
                SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, advance);
        }

        private void Advance()
        {
            if (disposed || delivering || canvasObject == null ||
                Stage == M04DockPreflightStage.Finished)
                return;
            if (Stage != M04DockPreflightStage.VerifyPhysicalReadiness)
            {
                Stage++;
                Refresh();
                return;
            }
            delivering = true;
            Refresh();
            try
            {
                onFinished?.Invoke();
                onFinished = null;
                Stage = M04DockPreflightStage.Finished;
            }
            catch
            {
                // A failed scene transition keeps the player at the same
                // checkpoint; nothing is booked, saved or sailed by this UI.
                delivering = false;
                if (!disposed) Refresh();
                throw;
            }
            if (!disposed) Refresh();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            onFinished = null;
            SoundwellUiLocalization.Changed -= Refresh;
            if (advance != null) advance.onClick.RemoveAllListeners();
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }

    public static class M04DockPreflightCopy
    {
        public static string Title(M04DockPreflightStage stage, bool zh)
        {
            switch (stage)
            {
                case M04DockPreflightStage.DawnSupply:
                    return zh ? "临潮港 · 黎明的两份善意" : "LINCHAO · TWO OFFERS AT DAWN";
                case M04DockPreflightStage.VerifyVoyageRoute:
                    return zh ? "临潮港 · 核对实际航路" : "LINCHAO · CHECK THE REAL VOYAGE";
                case M04DockPreflightStage.VerifyPhysicalReadiness:
                    return zh ? "临潮港 · 拔锚前的核验" : "LINCHAO · BEFORE CAST-OFF";
                case M04DockPreflightStage.Finished:
                    return string.Empty;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stage));
            }
        }

        public static string Describe(M04DockPreflightStage stage,
            M03VoyageChoice route, bool zh)
        {
            if (!Enum.IsDefined(typeof(M03VoyageChoice), route))
                throw new ArgumentOutOfRangeException(nameof(route));
            switch (stage)
            {
                case M04DockPreflightStage.DawnSupply:
                    return zh
                        ? "黎明，韩峤派人送来带有真实民用航次印记的补给箱。这份便利不是一艘已经到位的官船。\n陆钧的联络人在岸边提醒：『人带回来，总要有归属。』"
                        : "At dawn Han Qiao's courier delivers supplies bearing a genuine civilian-voyage stamp. The favor does not mean an official ship is ready.\nLu Jun's contact warns: “People you bring back will still need a place to belong.”";
                case M04DockPreflightStage.VerifyVoyageRoute:
                    return RouteCopy(route, zh);
                case M04DockPreflightStage.VerifyPhysicalReadiness:
                    return zh
                        ? "拔锚之前，必须核实实际船体、领航人、已到船员、吃水、浅水接驳艇、靠岸接应和可用舱位；至少为救援预留一个。\n准备齐全也不是已经开船。尚未核实的项目留在岸上清单里，不能自动把它们记成抵港事实。"
                        : "Before sailing, verify the real hull, pilot, boarded crew, draft, shallow tender, landing receiver and usable berths; reserve at least one for the rescue.\nA ready plan is not a cast-off. Unverified resources stay on the dock checklist, never becoming arrived vessels or passengers.";
                case M04DockPreflightStage.Finished:
                    return string.Empty;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stage));
            }
        }

        private static string RouteCopy(M03VoyageChoice route, bool zh)
        {
            switch (route)
            {
                case M03VoyageChoice.LuJunEscort:
                    return zh
                        ? "你选了陆钧护航。护卫和公开旗号只是约定；要核实护航船、领航人和实际船员，并确认旗号确实获准。"
                        : "You chose Lu Jun's escort. Guards and a public flag are promises, not a vessel. Check the actual escort ship, pilot, crew and accepted flag.";
                case M03VoyageChoice.QiQueMerchant:
                    return zh
                        ? "你选了祁雀商路。要亲眼核实民船、舵手阿鸢是否随船、浅水接驳和舱位。祁雀本人是否同去，不能靠选项假定。"
                        : "You chose Qi Que's merchant route. Verify the civilian hull, whether A Yuan sails, the tender and free berths. Choosing the route does not put Qi Que aboard.";
                case M03VoyageChoice.HanQiaoOfficial:
                    return zh
                        ? "你选了韩峤船坞。只有官署登记真正生效、官船与船工实到，才能按官船启航。那只补给箱不能替代官船。"
                        : "You chose Han Qiao's official dock. Check an effective registration and an actual official hull and crew. His supply crate is not a replacement for that ship.";
                case M03VoyageChoice.MixedWithOfficial:
                    return zh
                        ? "你选了含官船的混合航路。必须分清真实底船与护卫，核实生效的官署登记，不得把民船与官船混成同一艘。"
                        : "Your mixed route includes the official dock. Verify the registered base hull and separate escorts; do not count a civilian and official ship as one.";
                case M03VoyageChoice.MixedWithCivilian:
                    return zh
                        ? "你选了民船混合航路。先核实真正承担本航次的民船与领航人，再核对护卫、接驳和舱位；商路选择不等于船已到。"
                        : "Your mixed civilian route needs one real base hull and pilot, separately verified escorts, tender and berths. A route choice is not an arrived ship.";
                default:
                    throw new ArgumentOutOfRangeException(nameof(route));
            }
        }
    }
}
