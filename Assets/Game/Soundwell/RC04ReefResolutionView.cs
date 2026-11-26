using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// RC04 on-reef choice panel, downstream from the three verified fog
    /// rounds and a real scene-owned landing. Never invents a safe boat,
    /// A Yuan's presence, combat victory, Ding Yue's arrival on shore,
    /// willing patrol crew, or money already paid for the decoy.
    ///
    /// The scene callback is the single authority to record the outcome
    /// through RC04DingYueReefSession.TryResolveReef/Makinom.
    /// </summary>
    public sealed class RC04ReefResolutionView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly Text heading;
        private readonly Text story;
        private readonly Button[] buttons = new Button[4];
        private readonly Text[] labels = new Text[4];
        private readonly RC04Resolution[] choices = new[]
        {
            RC04Resolution.KillMimicBeastWithAyuanSong,
            RC04Resolution.KillMimicBeastWithPreparedDecoy,
            RC04Resolution.BringDingYueBack,
            RC04Resolution.LeaveWithSignalBeacon
        };
        private readonly RC04DingYueReefSession session;
        private readonly Font latinFont;
        private Func<RC04Resolution, bool> verifiedSceneConditions;
        private Func<RC04Resolution, bool> onResolutionRequested;
        private bool submitting;
        private bool delivered;
        private bool disposed;

        public RectTransform Panel => panel;
        public Text Heading => heading;
        public Text Story => story;
        public Button SongKillButton => buttons[0];
        public Button DecoyKillButton => buttons[1];
        public Button BringDingYueButton => buttons[2];
        public Button BeaconButton => buttons[3];
        public bool ResolutionDelivered => delivered;
        public bool IsDisposed => disposed || CanvasDestroyed();

        public RC04ReefResolutionView(
            Transform parent,
            RC04DingYueReefSession session,
            Func<RC04Resolution, bool> verifiedSceneConditions,
            Func<RC04Resolution, bool> onResolutionRequested)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.verifiedSceneConditions = verifiedSceneConditions ??
                throw new ArgumentNullException(nameof(verifiedSceneConditions));
            this.onResolutionRequested = onResolutionRequested ??
                throw new ArgumentNullException(nameof(onResolutionRequested));

            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("RC04 reef resolution canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 141;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var holder = new GameObject("RC04 reef resolution bottom panel",
                typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(904f, 286f);
            panel.anchoredPosition = new Vector2(0f, 154f);
            SoundwellUiTheme.CreatePanel(panel, "RC04 resolution border",
                new Vector2(904f, 286f), new Color(0.64f, 0.56f, 0.42f, 1f));
            SoundwellUiTheme.CreatePanel(panel, "RC04 resolution backing",
                new Vector2(900f, 282f), new Color(0.06f, 0.09f, 0.11f, 0.97f));
            Color ink = new Color(0.93f, 0.89f, 0.81f, 1f);
            heading = SoundwellUiTheme.CreateLabel(panel, 19,
                new Vector2(0f, 115f), new Vector2(842f, 27f), ink);
            heading.alignment = TextAnchor.MiddleLeft;
            story = SoundwellUiTheme.CreateLabel(panel, 15,
                new Vector2(0f, 53f), new Vector2(842f, 89f), ink);
            story.alignment = TextAnchor.UpperLeft;
            story.horizontalOverflow = HorizontalWrapMode.Wrap;

            var positions = new[]
            {
                new Vector2(-222f, -50f),
                new Vector2(222f, -50f),
                new Vector2(-222f, -103f),
                new Vector2(222f, -103f)
            };
            for (int i = 0; i < buttons.Length; i++)
            {
                int choice = i;
                buttons[i] = SoundwellUiTheme.CreateButton(
                    panel, "RC04 resolution " + choices[i], positions[i],
                    new Vector2(426f, 40f), 14, out labels[i]);
                buttons[i].onClick.AddListener(() => Choose(choice));
            }

            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            SoundwellUiLocalization.Changed += Refresh;
            Refresh();
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed || IsDestroyedExternally()) return;
            if (float.IsNaN(aspect) || float.IsInfinity(aspect) || aspect <= 0f) return;
            SoundwellUiTheme.ApplyViewportAspectScale(panel, aspect);
        }

        public bool CanChoose(RC04Resolution resolution)
        {
            if (disposed || delivered || submitting || CanvasDestroyed() ||
                !session.TidalWindowLocated ||
                session.IsResolved || verifiedSceneConditions == null ||
                !verifiedSceneConditions(resolution))
                return false;

            switch (resolution)
            {
                case RC04Resolution.KillMimicBeastWithAyuanSong:
                    return session.AyuanSongPlayed && !session.DecoyAmbushPrepared;
                case RC04Resolution.KillMimicBeastWithPreparedDecoy:
                    return session.DecoyAmbushPrepared &&
                        session.DecoyBeastDefeatVerified &&
                        !session.AyuanSongPlayed;
                case RC04Resolution.BringDingYueBack:
                case RC04Resolution.LeaveWithSignalBeacon:
                    return true;
                default:
                    return false;
            }
        }

        public void Refresh()
        {
            if (disposed || IsDestroyedExternally()) return;
            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = story.font = font;
            foreach (Text label in labels)
                label.font = font;
            heading.text = zh ? "RC04 · 丁岳与借声兽：处置" :
                "RC04 · DING YUE AND THE VOICE MIMIC";

            if (delivered || session.IsResolved)
            {
                story.text = zh
                    ? "本案处置已交给实际场景保存；没有第二次杀兽、领人或巡护结算。"
                    : "The scene has recorded the case outcome; no duplicate beast kill, transfer or beacon settlement.";
                foreach (var button in buttons)
                {
                    button.gameObject.SetActive(false);
                    button.interactable = false;
                }
                return;
            }

            story.text = !session.TidalWindowLocated
                ? zh
                    ? "先在礁区外围完成三轮听雾，找到真人回声与短潮窗；现在不能靠想象作出处置。"
                    : "First distinguish the slow human echo over three rounds and find a real short tide window."
                : zh
                    ? "丁岳说他只是怕雾忘了旧部的名字。渔婆说儿子被借声骗上礁。杀兽要真战斗；带丁岳走须实际到岸；设航标须有人自愿接手。未完成的准备不会自动算成功。"
                    : "Ding Yue called names so the fog would not forget. A fisher's widow lost her son to the mimic. A kill needs real combat; rescue needs actual landing; a beacon needs a willing keeper.";

            labels[0].text = zh ? "阿鸢确实在场并唱变调歌：战胜借声兽"
                                : "A Yuan's varying song + real beast victory";
            labels[1].text = zh ? "无阿鸢：假声源 + 自愿船工伏击并战胜"
                                : "No A Yuan: paid decoy + crew + real victory";
            labels[2].text = zh ? "实际带丁岳上岸；赔偿仍须另议"
                                : "Bring Ding Yue ashore; restitution still pending";
            labels[3].text = zh ? "真设航标与自愿巡护；船队仍绕行"
                                : "Place beacon with consenting patrol; ships detour";

            Button focus = null;
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i].gameObject.SetActive(true);
                buttons[i].interactable = !submitting && CanChoose(choices[i]);
                if (focus == null && buttons[i].interactable)
                    focus = buttons[i];
            }
            if (!submitting && focus != null)
                SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, focus);
        }

        private void Choose(int index)
        {
            if (disposed || IsDestroyedExternally() || delivered || submitting ||
                index < 0 || index >= choices.Length ||
                !buttons[index].interactable)
                return;
            RC04Resolution choice = choices[index];
            // Scene facts can change between Refresh and button Submit.
            if (!CanChoose(choice))
            {
                Refresh();
                return;
            }
            submitting = true;
            Refresh();
            try
            {
                if (!onResolutionRequested(choice))
                    return;

                delivered = true;
                verifiedSceneConditions = null;
                onResolutionRequested = null;
            }
            finally
            {
                submitting = false;
                if (!disposed) Refresh();
            }
        }

        private bool IsDestroyedExternally()
        {
            if (!CanvasDestroyed()) return false;
            Dispose();
            return true;
        }

        private bool CanvasDestroyed()
        {
            if (canvasObject == null || panel == null || heading == null ||
                story == null)
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
            SoundwellUiLocalization.Changed -= Refresh;
            verifiedSceneConditions = null;
            onResolutionRequested = null;
            foreach (Button button in buttons)
                if (button != null) button.onClick.RemoveAllListeners();
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }
}