using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Bilingual UGUI panel for restricted fog-listening sailing backed strictly
    /// by an existing FogListeningSession. Preserves clarity uncertainty, focus
    /// limits, and completion guards without inventing omniscient sonar or a
    /// parallel save authority.
    /// </summary>
    public sealed class FogListeningView : IDisposable
    {
        public const int ClearCueThreshold = 60;

        private static readonly Color FogBackdropColor = new Color(0.04f, 0.07f, 0.09f, 0.98f);
        private static readonly Color FogPaperColor = new Color(0.90f, 0.91f, 0.87f, 1f);
        private static readonly Color FogInkColor = new Color(0.11f, 0.16f, 0.20f, 1f);
        private static readonly Color FogButtonColor = new Color(0.80f, 0.83f, 0.80f, 1f);

        private readonly FogListeningSession session;
        private readonly GameObject canvasObject;
        private readonly RectTransform authored;
        private readonly Font latinFont;
        private readonly Text heading;
        private readonly Text statusText;
        private readonly Text cueText;
        private readonly Button focusButton;
        private readonly Text focusLabel;
        private readonly Button holdButton;
        private readonly Text holdLabel;
        private readonly Button slowButton;
        private readonly Text slowLabel;
        private readonly Button portButton;
        private readonly Text portLabel;
        private readonly Button starboardButton;
        private readonly Text starboardLabel;
        private readonly Button signalButton;
        private readonly Text signalLabel;
        private readonly Button continueButton;
        private readonly Text continueLabel;
        private readonly Action<FogListeningSession> onFinished;
        private FogResolution? lastResolution;
        private bool delivered;
        private bool disposed;

        public FogListeningSession Session => session;
        public Text StatusText => statusText;
        public Text CueText => cueText;
        public Button FocusButton => focusButton;
        public Button HoldButton => holdButton;
        public Button SlowButton => slowButton;
        public Button PortButton => portButton;
        public Button StarboardButton => starboardButton;
        public Button SignalButton => signalButton;
        public Button ContinueButton => continueButton;

        public FogListeningView(
            Transform parent,
            FogListeningSession session,
            Action<FogListeningSession> onFinished)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.onFinished = onFinished ?? throw new ArgumentNullException(nameof(onFinished));

            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = SoundwellUiTheme.CreateOverlayCanvas(
                parent, "Fog listening canvas", 131, out authored,
                FogBackdropColor, FogPaperColor);

            heading = SoundwellUiTheme.CreateLabel(authored, 23, new Vector2(0f, 215f), new Vector2(840f, 42f), FogInkColor);
            statusText = SoundwellUiTheme.CreateLabel(authored, 17, new Vector2(0f, 155f), new Vector2(840f, 64f), FogInkColor);
            statusText.alignment = TextAnchor.UpperLeft;
            cueText = SoundwellUiTheme.CreateLabel(authored, 18, new Vector2(0f, 54f), new Vector2(840f, 122f), FogInkColor);
            cueText.alignment = TextAnchor.UpperLeft;

            focusButton = SoundwellUiTheme.CreateButton(authored, "Fog focus listen",
                new Vector2(0f, -44f), new Vector2(400f, 40f), 16, out focusLabel, FogButtonColor, FogInkColor);

            portButton = SoundwellUiTheme.CreateButton(authored, "Fog helm port",
                new Vector2(-280f, -98f), new Vector2(250f, 40f), 16, out portLabel, FogButtonColor, FogInkColor);
            holdButton = SoundwellUiTheme.CreateButton(authored, "Fog helm hold",
                new Vector2(0f, -98f), new Vector2(250f, 40f), 16, out holdLabel, FogButtonColor, FogInkColor);
            starboardButton = SoundwellUiTheme.CreateButton(authored, "Fog helm starboard",
                new Vector2(280f, -98f), new Vector2(250f, 40f), 16, out starboardLabel, FogButtonColor, FogInkColor);

            slowButton = SoundwellUiTheme.CreateButton(authored, "Fog helm slow",
                new Vector2(-210f, -152f), new Vector2(340f, 40f), 16, out slowLabel, FogButtonColor, FogInkColor);
            signalButton = SoundwellUiTheme.CreateButton(authored, "Fog helm signal",
                new Vector2(210f, -152f), new Vector2(340f, 40f), 16, out signalLabel, FogButtonColor, FogInkColor);

            continueButton = SoundwellUiTheme.CreateButton(authored, "Fog continue",
                new Vector2(0f, -210f), new Vector2(400f, 44f), 16, out continueLabel, FogButtonColor, FogInkColor);

            focusButton.onClick.AddListener(FocusListen);
            portButton.onClick.AddListener(() => Steer(FogHelmAction.TurnPort));
            holdButton.onClick.AddListener(() => Steer(FogHelmAction.HoldCourse));
            starboardButton.onClick.AddListener(() => Steer(FogHelmAction.TurnStarboard));
            slowButton.onClick.AddListener(() => Steer(FogHelmAction.SlowAndSound));
            signalButton.onClick.AddListener(() => Steer(FogHelmAction.AnswerSignal));
            continueButton.onClick.AddListener(Complete);

            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            SoundwellUiLocalization.Changed += Refresh;
            Refresh();
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed) return;
            SoundwellUiTheme.ApplyViewportAspectScale(authored, aspect);
        }

        public void Refresh()
        {
            if (disposed) return;
            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            heading.font = statusText.font = cueText.font = font;
            focusLabel.font = holdLabel.font = slowLabel.font = font;
            portLabel.font = starboardLabel.font = signalLabel.font = continueLabel.font = font;

            heading.text = zh ? "航行听雾 · 辨声掌舵" : "FOG LISTENING · LISTEN AND STEER";
            statusText.text = FogListeningCopy.DescribeStatus(session, zh);
            cueText.text = FogListeningCopy.DescribeCurrentCue(session, lastResolution, zh);

            focusLabel.text = zh ? "凝神听雾" : "Focus listen";
            portLabel.text = zh ? "向左舷避让" : "Turn port";
            holdLabel.text = zh ? "保持航向" : "Hold course";
            starboardLabel.text = zh ? "向右舷避让" : "Turn starboard";
            slowLabel.text = zh ? "减速测水深" : "Slow and sound";
            signalLabel.text = zh ? "回应信号" : "Answer signal";
            // A failed leg is reported to the host, not represented as
            // another successful voyage step. Do not promise safe passage
            // after the hull has been exhausted.
            continueLabel.text = session.IsComplete && !session.Succeeded
                ? (zh ? "确认本段雾航失败" : "Acknowledge failed leg")
                : (zh ? "继续航程" : "Continue voyage");

            bool activeLeg = !delivered && !session.IsComplete;
            focusButton.interactable = activeLeg &&
                !session.CurrentCueFocused && session.RemainingFocus > 0;
            portButton.interactable = activeLeg;
            holdButton.interactable = activeLeg;
            starboardButton.interactable = activeLeg;
            slowButton.interactable = activeLeg;
            signalButton.interactable = activeLeg;
            continueButton.interactable = !delivered && session.IsComplete;

            if (!delivered)
            {
                Button preferred = continueButton.interactable ? continueButton
                    : focusButton.interactable ? focusButton
                    : slowButton;
                SoundwellInputAdapter.KeepDialogFocus(canvasObject.transform, preferred);
            }
        }

        private void FocusListen()
        {
            if (disposed || delivered || !session.TryFocusListen())
                return;
            Refresh();
        }

        private void Steer(FogHelmAction action)
        {
            if (disposed || delivered || session.IsComplete)
                return;
            lastResolution = session.ApplyHelmAction(action);
            Refresh();
        }

        private void Complete()
        {
            if (disposed || delivered || !session.IsComplete)
                return;

            // Keep the exact finished fog leg while its outcome is recorded
            // through the existing Makinom host. Lock first so even direct
            // onClick invokes cannot deliver the same outcome reentrantly.
            delivered = true;
            Refresh();
            try
            {
                onFinished(session);
            }
            catch
            {
                // Interrupted host persistence cannot consume Continue.
                // The player may retry the identical completed leg without
                // replaying navigation or changing the outcome.
                delivered = false;
                Refresh();
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            SoundwellUiLocalization.Changed -= Refresh;
            focusButton.onClick.RemoveAllListeners();
            holdButton.onClick.RemoveAllListeners();
            slowButton.onClick.RemoveAllListeners();
            portButton.onClick.RemoveAllListeners();
            starboardButton.onClick.RemoveAllListeners();
            signalButton.onClick.RemoveAllListeners();
            continueButton.onClick.RemoveAllListeners();
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }

    public static class FogListeningCopy
    {
        public static string DescribeStatus(FogListeningSession session, bool chinese)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            var copy = new StringBuilder();
            int shownStep = session.IsComplete ? session.CurrentStep : session.CurrentStep + 1;
            copy.Append(chinese ? "航段进度：" : "Leg step: ")
                .Append(shownStep).Append('/').Append(session.TotalSteps)
                .Append(chinese ? "；听雾专注：" : "; listen focus: ")
                .Append(session.RemainingFocus).Append('/').Append(session.MaxListenFocus)
                .Append(chinese ? "；船体完好：" : "; hull integrity: ")
                .Append(session.HullIntegrity)
                .AppendLine();
            copy.Append(chinese ? "避开暗礁：" : "Hazards avoided: ")
                .Append(session.AvoidedHazards)
                .Append(chinese ? "；接上信号：" : "; signals matched: ")
                .Append(session.MatchedSignals);
            return copy.ToString();
        }

        public static string DescribeCurrentCue(
            FogListeningSession session, FogResolution? lastResolution, bool chinese)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            var copy = new StringBuilder();
            if (lastResolution.HasValue)
            {
                copy.Append(chinese ? "上一步结果：" : "Last result: ")
                    .AppendLine(ResolutionText(lastResolution.Value, chinese));
            }
            if (session.IsComplete)
            {
                copy.Append(session.Succeeded
                    ? (chinese ? "本段迷雾已安全通过，可继续航程。" : "Cleared this fog leg safely. Continue the voyage.")
                    : (chinese ? "船体受损过重，被迫中止本段雾航。" : "Hull integrity exhausted. This fog leg failed."));
                return copy.ToString();
            }

            FogListeningCue cue = session.CurrentCue;
            int clarity = session.EffectiveClarity();
            // A low-clarity cue must not reveal the precise port/starboard
            // bearing before the player chooses to listen more closely.
            // Keep the clarity visible so focus remains an informed choice.
            bool canIdentifyBearing = clarity >= FogListeningView.ClearCueThreshold;
            copy.Append(chinese ? "方位：" : "Bearing: ")
                .Append(canIdentifyBearing
                    ? BearingText(cue.Bearing, chinese)
                    : (chinese ? "难辨" : "indistinct"))
                .Append(chinese ? "；清晰度：" : "; clarity: ")
                .Append(clarity).AppendLine("%");

            if (!canIdentifyBearing)
            {
                copy.Append(chinese
                    ? "雾中声响模糊难辨，可凝神听雾或减速测水深。"
                    : "Sound is faint in the fog; focus listen or slow and sound.");
            }
            else
            {
                copy.Append(CueKindText(cue.Kind, chinese));
                if (cue.RequiresReceiverLamp)
                {
                    copy.AppendLine();
                    copy.Append(chinese
                        ? "对方等待约定识别灯回应。"
                        : "Receiver awaits the pre-arranged signal lamp.");
                }
            }
            return copy.ToString();
        }

        public static string BearingText(FogBearing bearing, bool chinese)
        {
            switch (bearing)
            {
                case FogBearing.Port: return chinese ? "左舷" : "Port";
                case FogBearing.Ahead: return chinese ? "正前" : "Ahead";
                case FogBearing.Starboard: return chinese ? "右舷" : "Starboard";
                default: throw new ArgumentOutOfRangeException(nameof(bearing));
            }
        }

        public static string CueKindText(FogCueKind kind, bool chinese)
        {
            switch (kind)
            {
                case FogCueKind.ClearWater:
                    return chinese ? "水声平稳，前方暂无障碍。" : "Steady water; path appears clear.";
                case FogCueKind.ReefSurf:
                    return chinese ? "低沉礁浪拍击声，需及时避让或减速。" : "Low reef surf; check the bearing and evade or slow down.";
                case FogCueKind.FogBell:
                    return chinese ? "雾钟节律清晰，可回应确认方位。" : "Distinct fog bell; answer to confirm bearing.";
                case FogCueKind.SignalResponse:
                    return chinese ? "远处有接应信号回应。" : "Distant signal response detected.";
                case FogCueKind.FalseEcho:
                    return chinese ? "杂乱回声，谨防误判方位。" : "Scattered false echo; hold steady.";
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public static string ResolutionText(FogResolution resolution, bool chinese)
        {
            switch (resolution)
            {
                case FogResolution.SafePassage: return chinese ? "平稳通过" : "Safe passage";
                case FogResolution.HazardAvoided: return chinese ? "避开暗礁" : "Hazard avoided";
                case FogResolution.SignalMatched: return chinese ? "信号对接成功" : "Signal matched";
                case FogResolution.HullScraped: return chinese ? "船体擦伤" : "Hull scraped";
                case FogResolution.LostBearing: return chinese ? "偏离方位" : "Lost bearing";
                default: throw new ArgumentOutOfRangeException(nameof(resolution));
            }
        }
    }
}
