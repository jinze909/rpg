using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Reusable, visual-only UGUI HUD bar for rendering an active combat host's
    /// EnemyLockBar, MomentumGauge (0..3), and S00 3-bell cable progress or
    /// Soundwell acoustic-break status in the top safe margin.
    /// Strictly presentation-only: owns no ORK HP, damage, turn, or win state,
    /// has no GraphicRaycaster (never blocks UGUI input), and renders only
    /// host-supplied authoritative tracker state.
    /// </summary>
    public sealed class CombatLockBreakHudView : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly RectTransform panel;
        private readonly Image borderImage;
        private readonly Font latinFont;
        private readonly Text objectiveLabel;
        private readonly Text locksLabel;
        private readonly Text momentumLabel;
        private S00BridgeheadCableTracker s00Tracker;
        private SoundwellAcousticBreakTracker soundwellTracker;
        private bool disposed;

        public RectTransform Panel => panel;
        public Text ObjectiveLabel => objectiveLabel;
        public Text LocksLabel => locksLabel;
        public Text MomentumLabel => momentumLabel;
        public bool IsDisposed => disposed || CanvasDestroyed();

        public CombatLockBreakHudView(Transform parent,
            S00BridgeheadCableTracker s00Tracker)
            : this(parent,
                s00Tracker ?? throw new ArgumentNullException(nameof(s00Tracker)),
                null)
        {
        }

        public CombatLockBreakHudView(Transform parent,
            SoundwellAcousticBreakTracker soundwellTracker)
            : this(parent,
                null,
                soundwellTracker ?? throw new ArgumentNullException(nameof(soundwellTracker)))
        {
        }

        private CombatLockBreakHudView(
            Transform parent,
            S00BridgeheadCableTracker s00Tracker,
            SoundwellAcousticBreakTracker soundwellTracker)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.s00Tracker = s00Tracker;
            this.soundwellTracker = soundwellTracker;
            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Pure visual HUD overlay (no GraphicRaycaster) so it never blocks clicks.
            canvasObject = new GameObject("Combat lock-break HUD canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 124;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;

            var holder = new GameObject("Combat lock-break top strip", typeof(RectTransform));
            holder.transform.SetParent(canvasObject.transform, false);
            panel = (RectTransform)holder.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 1f);
            panel.sizeDelta = new Vector2(780f, 58f);
            panel.anchoredPosition = new Vector2(0f, -44f);

            borderImage = SoundwellUiTheme.CreatePanel(panel, "Lock-break border",
                new Vector2(780f, 58f), new Color(0.68f, 0.58f, 0.40f, 0.92f));
            SoundwellUiTheme.CreatePanel(panel, "Lock-break backing",
                new Vector2(776f, 54f), new Color(0.06f, 0.08f, 0.11f, 0.92f));

            Color ink = new Color(0.94f, 0.90f, 0.82f, 1f);
            objectiveLabel = SoundwellUiTheme.CreateLabel(panel, 14,
                new Vector2(-120f, 13f), new Vector2(510f, 22f), ink);
            objectiveLabel.alignment = TextAnchor.MiddleLeft;

            momentumLabel = SoundwellUiTheme.CreateLabel(panel, 14,
                new Vector2(260f, 13f), new Vector2(220f, 22f), new Color(0.96f, 0.80f, 0.42f, 1f));
            momentumLabel.alignment = TextAnchor.MiddleRight;

            locksLabel = SoundwellUiTheme.CreateLabel(panel, 13,
                new Vector2(0f, -13f), new Vector2(746f, 22f), ink);
            locksLabel.alignment = TextAnchor.MiddleLeft;

            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            SoundwellUiLocalization.Changed += Refresh;
            Refresh();
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed) return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }
            if (float.IsNaN(aspect) || float.IsInfinity(aspect) || aspect <= 0f)
                return;
            SoundwellUiTheme.ApplyViewportAspectScale(panel, aspect);
        }

        public void Refresh()
        {
            if (disposed) return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }

            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            objectiveLabel.font = locksLabel.font = momentumLabel.font = font;

            if (s00Tracker != null)
            {
                objectiveLabel.text = FormatS00Objective(s00Tracker, zh);
                momentumLabel.text = FormatMomentum(s00Tracker.Momentum, zh);
                locksLabel.text = FormatLockBar(s00Tracker.CurrentLocks, zh) +
                    "  |  " + S00BridgeheadCableTracker.DescribeThreat(s00Tracker.CurrentThreat, zh);
                borderImage.color = s00Tracker.ReadyForThirdBellCollapse
                    ? new Color(0.42f, 0.78f, 0.56f, 0.95f)
                    : s00Tracker.CurrentThreat == S00BridgeThreatKind.SapperRushCable
                        ? new Color(0.92f, 0.30f, 0.24f, 0.95f)
                        : new Color(0.76f, 0.62f, 0.38f, 0.92f);
            }
            else if (soundwellTracker != null)
            {
                objectiveLabel.text = FormatSoundwellObjective(soundwellTracker, zh);
                momentumLabel.text = FormatMomentum(soundwellTracker.Momentum, zh);
                locksLabel.text = FormatLockBar(soundwellTracker.AcousticLocks, zh);
                borderImage.color = soundwellTracker.IsBeastBroken
                    ? new Color(0.42f, 0.78f, 0.92f, 0.95f)
                    : new Color(0.84f, 0.64f, 0.32f, 0.92f);
            }
        }

        public static string FormatMomentum(MomentumGauge gauge, bool zh)
        {
            if (gauge == null) return string.Empty;
            var sb = new StringBuilder();
            sb.Append(zh ? "稳势 " : "Momentum ");
            for (int i = 0; i < MomentumGauge.MaxPoints; i++)
                sb.Append(i < gauge.Current ? "◆" : "◇");
            sb.Append(" (").Append(gauge.Current).Append('/').Append(MomentumGauge.MaxPoints).Append(')');
            return sb.ToString();
        }

        public static string FormatS00Objective(S00BridgeheadCableTracker tracker, bool zh)
        {
            if (tracker == null) return string.Empty;
            var sb = new StringBuilder();
            sb.Append(zh ? "沈砾断缆进度 " : "Cable Cut ");
            for (int i = 0; i < S00BridgeheadCableTracker.RequiredBells; i++)
                sb.Append(i < tracker.BellsCompleted ? "●" : "○");
            sb.Append(" (").Append(tracker.BellsCompleted).Append('/')
                .Append(S00BridgeheadCableTracker.RequiredBells).Append(')');
            if (tracker.ReadyForThirdBellCollapse)
                sb.Append(zh ? " · 第三声钟就绪" : " · Third Bell Ready");
            return sb.ToString();
        }

        public static string FormatSoundwellObjective(SoundwellAcousticBreakTracker tracker, bool zh)
        {
            if (tracker == null) return string.Empty;
            if (tracker.IsBeastBroken)
                return zh
                    ? "借声兽已破防（BREAK）· 可趁机集火或安全脱身"
                    : "BEAST BROKEN · Punish via ORK or Disengage Safely";
            return zh
                ? "听觉护甲剩余 " + tracker.AcousticLocks.RemainingLocks + "/" + tracker.AcousticLocks.TotalLocks
                : "Acoustic Armor " + tracker.AcousticLocks.RemainingLocks + "/" + tracker.AcousticLocks.TotalLocks;
        }

        public static string FormatLockBar(EnemyLockBar bar, bool zh)
        {
            if (bar == null) return string.Empty;
            if (bar.IsBroken)
                return zh ? "[已破锁 BREAK · 威胁取消]" : "[LOCKS BROKEN · CANCELLED]";

            var sb = new StringBuilder();
            sb.Append(zh ? "破锁需求：" : "Locks: ");
            bool first = true;
            foreach (CombatLockKind kind in new[]
            {
                CombatLockKind.ShieldParry,
                CombatLockKind.BladeRepel,
                CombatLockKind.WetClothMuffle,
                CombatLockKind.DecoyBellRing
            })
            {
                if (!bar.HasRemainingLock(kind)) continue;
                if (!first) sb.Append(" + ");
                sb.Append('[').Append(LockName(kind, zh)).Append(']');
                first = false;
            }
            if (bar.ThreatMitigationPercent > 0)
                sb.Append(zh ? " (已减威 " : " (-").Append(bar.ThreatMitigationPercent).Append("%)");
            return sb.ToString();
        }

        private static string LockName(CombatLockKind kind, bool zh)
        {
            switch (kind)
            {
                case CombatLockKind.ShieldParry:
                    return zh ? "举盾格挡" : "Shield Parry";
                case CombatLockKind.BladeRepel:
                    return zh ? "挥刀逼退" : "Blade Repel";
                case CombatLockKind.WetClothMuffle:
                    return zh ? "湿布消音" : "Wet Cloth";
                case CombatLockKind.DecoyBellRing:
                    return zh ? "铜铃诱敌" : "Decoy Bell";
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private bool CanvasDestroyed()
        {
            return canvasObject == null || panel == null || borderImage == null ||
                objectiveLabel == null || locksLabel == null || momentumLabel == null;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            s00Tracker = null;
            soundwellTracker = null;
            SoundwellUiLocalization.Changed -= Refresh;
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
        }
    }
}
