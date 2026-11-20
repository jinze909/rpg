using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Playable 4x-scale Soundwell (96x60) exploration and acoustic lock-break
    /// host wiring RpgLargeMapChunkRenderer (6 Point-filtered 32x32 chunks + 4
    /// warm light halos), RpgLargeMapExplorerController (wall-sliding movement,
    /// iron-gate collision gating, zone events, proximity triggers, clamped
    /// camera follow), SoundwellAcousticBreakTracker, SoundwellBeastPatrolTracker,
    /// CombatLockBreakHudView, RpgLargeMapMinimapAndBeaconHud (with live beast
    /// threat marker), and a visual-only district/interact HUD.
    /// Strictly subordinate to ORK 3: owns no HP, damage, turn, or defeat state.
    /// </summary>
    public sealed class SoundwellLargeMapExplorationHost : IDisposable
    {
        private readonly GameObject hostRoot;
        private readonly GameObject hudCanvasObject;
        private readonly RectTransform zonePanel;
        private readonly RectTransform promptPanel;
        private readonly Font latinFont;
        private readonly Text zoneLabel;
        private readonly Text promptLabel;
        private readonly Text feedbackLabel;
        private string lastFeedbackZh = string.Empty;
        private string lastFeedbackEn = string.Empty;
        private bool disposed;

        public SoundwellLargeMapLayout Layout { get; }
        public RpgLargeMapChunkRenderer MapRenderer { get; }
        public RpgLargeMapExplorerController Explorer { get; }
        public SoundwellAcousticBreakTracker AcousticTracker { get; }
        public SoundwellBeastPatrolTracker BeastPatrol { get; }
        public CombatLockBreakHudView LockBreakHud { get; }
        public RpgLargeMapMinimapAndBeaconHud MinimapHud { get; }

        public bool IsGateOpen { get; private set; }
        public bool ExtractionCompleted { get; private set; }
        public bool IsDisposed => disposed;
        public string CurrentZoneId => Explorer.CurrentZone.HasValue
            ? Explorer.CurrentZone.Value.Id
            : string.Empty;
        public Text ZoneLabel => zoneLabel;
        public Text PromptLabel => promptLabel;
        public Text FeedbackLabel => feedbackLabel;

        public event Action<string> ZoneChanged;
        public event Action<string> InteractionResolved;
        public event Action GateOpened;
        public event Action ExtractionReached;
        public event Action BeastEncounterTriggered;

        public SoundwellLargeMapExplorationHost(Transform parent,
            Sprite[] tileSlices = null, int startingMomentum = 1)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));

            hostRoot = new GameObject("Soundwell large map exploration host");
            hostRoot.transform.SetParent(parent, false);

            Layout = SoundwellLargeMapLayout.Build();
            MapRenderer = RpgLargeMapChunkRenderer.BuildForSoundwell(
                hostRoot.transform, Layout, tileSlices);
            Explorer = RpgLargeMapExplorerController.ForSoundwell(
                Layout, () => IsGateOpen);
            AcousticTracker = new SoundwellAcousticBreakTracker(startingMomentum);
            BeastPatrol = new SoundwellBeastPatrolTracker(Layout, AcousticTracker);
            LockBreakHud = new CombatLockBreakHudView(hostRoot.transform, AcousticTracker);
            MinimapHud = RpgLargeMapMinimapAndBeaconHud.BuildForSoundwell(
                hostRoot.transform, Layout);

            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Pure visual HUD overlay (no GraphicRaycaster) so it never blocks clicks.
            hudCanvasObject = new GameObject("Soundwell exploration HUD canvas",
                typeof(RectTransform), typeof(Canvas));
            hudCanvasObject.transform.SetParent(hostRoot.transform, false);
            var canvas = hudCanvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 120;
            var scaler = hudCanvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;

            var zoneHolder = new GameObject("Soundwell zone badge", typeof(RectTransform));
            zoneHolder.transform.SetParent(hudCanvasObject.transform, false);
            zonePanel = (RectTransform)zoneHolder.transform;
            zonePanel.anchorMin = zonePanel.anchorMax = new Vector2(0f, 1f);
            zonePanel.pivot = new Vector2(0f, 1f);
            zonePanel.sizeDelta = new Vector2(260f, 36f);
            zonePanel.anchoredPosition = new Vector2(20f, -110f);

            SoundwellUiTheme.CreatePanel(zonePanel, "Zone border",
                new Vector2(260f, 36f), new Color(0.68f, 0.58f, 0.40f, 0.90f));
            SoundwellUiTheme.CreatePanel(zonePanel, "Zone backing",
                new Vector2(256f, 32f), new Color(0.06f, 0.08f, 0.11f, 0.90f));

            Color ink = new Color(0.94f, 0.90f, 0.82f, 1f);
            zoneLabel = SoundwellUiTheme.CreateLabel(zonePanel, 14,
                Vector2.zero, new Vector2(240f, 26f), ink);
            zoneLabel.alignment = TextAnchor.MiddleLeft;

            var promptHolder = new GameObject("Soundwell prompt bar", typeof(RectTransform));
            promptHolder.transform.SetParent(hudCanvasObject.transform, false);
            promptPanel = (RectTransform)promptHolder.transform;
            promptPanel.anchorMin = promptPanel.anchorMax = new Vector2(0.5f, 0f);
            promptPanel.pivot = new Vector2(0.5f, 0f);
            promptPanel.sizeDelta = new Vector2(680f, 54f);
            promptPanel.anchoredPosition = new Vector2(0f, 20f);

            SoundwellUiTheme.CreatePanel(promptPanel, "Prompt border",
                new Vector2(680f, 54f), new Color(0.64f, 0.56f, 0.40f, 0.88f));
            SoundwellUiTheme.CreatePanel(promptPanel, "Prompt backing",
                new Vector2(676f, 50f), new Color(0.06f, 0.08f, 0.11f, 0.90f));

            promptLabel = SoundwellUiTheme.CreateLabel(promptPanel, 14,
                new Vector2(0f, 11f), new Vector2(650f, 22f), new Color(0.96f, 0.82f, 0.48f, 1f));
            promptLabel.alignment = TextAnchor.MiddleCenter;

            feedbackLabel = SoundwellUiTheme.CreateLabel(promptPanel, 13,
                new Vector2(0f, -12f), new Vector2(650f, 22f), ink);
            feedbackLabel.alignment = TextAnchor.MiddleCenter;

            Explorer.ZoneEntered += HandleZoneEntered;
            BeastPatrol.EncounterTriggered += HandleBeastEncounter;
            SoundwellUiLocalization.Changed += RefreshHud;

            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            UpdateBeastMinimapMarker();
            RefreshHud();
        }

        public bool Step(Vector2 moveInput, float deltaTime, Transform cameraTransform = null)
        {
            if (disposed) return false;
            if (CanvasDestroyed())
            {
                Dispose();
                return false;
            }

            bool moved = Explorer.StepMovement(moveInput, deltaTime);
            BeastPatrol.Step(deltaTime, Explorer.Position, moved);
            MinimapHud.UpdatePlayerPosition(Explorer.Position);
            UpdateBeastMinimapMarker();

            if (cameraTransform != null)
            {
                cameraTransform.position = Explorer.ComputeClampedCameraPosition(
                    cameraTransform.position, deltaTime);
            }

            RefreshHud();
            return moved;
        }

        public bool TryInteract()
        {
            if (disposed) return false;
            if (CanvasDestroyed())
            {
                Dispose();
                return false;
            }

            RpgMapTriggerAnchor trigger = default;
            if (!Explorer.TryFindNearestTrigger(out trigger))
            {
                SetFeedback("附近没有可交互的井下机关。", "No Soundwell mechanism within reach.");
                RefreshHud();
                return false;
            }

            switch (trigger.Id)
            {
                case "soundwell.wet-cloth":
                    if (AcousticTracker.ApplyAcousticCounter(CombatLockKind.WetClothMuffle))
                    {
                        MinimapHud.SetBeaconCompleted("soundwell.wet-cloth", true);
                        UpdateBeastMinimapMarker();
                        SetFeedback(
                            "已敷上湿布消音 — 听觉护甲削弱。",
                            "Wet cloth applied — acoustic lock weakened.");
                        RefreshHud();
                        InteractionResolved?.Invoke(trigger.Id);
                        return true;
                    }
                    SetFeedback(
                        "湿布消音已生效，前往铜铃或趁破防撤离。",
                        "Wet cloth already active; ring the bronze bell or move out.");
                    RefreshHud();
                    return false;

                case "soundwell.bell":
                    if (AcousticTracker.ApplyAcousticCounter(CombatLockKind.DecoyBellRing))
                    {
                        BeastPatrol.NotifyBronzeBellRung(Layout.BronzeBellTile);
                        MinimapHud.SetBeaconCompleted("soundwell.bell", true);
                        UpdateBeastMinimapMarker();
                        Explorer.TriggerScreenShake(0.22f, 0.14f);
                        if (AcousticTracker.CanTriggerNonViolentEscape)
                        {
                            SetFeedback(
                                "铜铃引偏回声！借声兽失衡破防（BREAK），可前往西侧拉闸脱身。",
                                "Bronze bell echoes! Beast BROKEN — pull the west iron gate lever to escape.");
                        }
                        else
                        {
                            SetFeedback(
                                "铜铃已敲响，还需湿布消音以彻底破锁。",
                                "Bronze bell rung — apply wet cloth to break the remaining acoustic lock.");
                        }
                        RefreshHud();
                        InteractionResolved?.Invoke(trigger.Id);
                        return true;
                    }
                    SetFeedback(
                        "铜铃已触发，借声兽声学锁已处理。",
                        "Bronze bell already used for this cycle.");
                    RefreshHud();
                    return false;

                case "soundwell.gate-lever":
                    if (IsGateOpen)
                    {
                        SetFeedback("西侧铁闸已升起，可通行至南侧撤退井口。", "Iron gate is already open.");
                        RefreshHud();
                        return false;
                    }
                    if (!AcousticTracker.CanTriggerNonViolentEscape)
                    {
                        SetFeedback(
                            "借声兽仍锁定通道！先用湿布消音并敲响铜铃破防后再拉闸。",
                            "Beast still guards the corridor! Break both acoustic locks (wet cloth + bell) first.");
                        RefreshHud();
                        return false;
                    }
                    IsGateOpen = true;
                    MinimapHud.SetBeaconCompleted("soundwell.gate-lever", true);
                    Explorer.TriggerScreenShake(0.18f, 0.12f);
                    SetFeedback(
                        "铁闸已开启！前往东侧撤退井口安全脱身。",
                        "Iron gate opened! Head east to the extraction hatch.");
                    RefreshHud();
                    GateOpened?.Invoke();
                    InteractionResolved?.Invoke(trigger.Id);
                    return true;

                case "soundwell.extraction":
                    if (!IsGateOpen)
                    {
                        SetFeedback("铁闸尚未开启，无法进入撤退井口。", "Open the iron gate before extracting.");
                        RefreshHud();
                        return false;
                    }
                    ExtractionCompleted = true;
                    MinimapHud.SetBeaconCompleted("soundwell.extraction", true);
                    SetFeedback(
                        "小队已抵达南侧撤退井口，完成钟岭声井零杀伤脱身。",
                        "Party reached the extraction hatch — non-violent Soundwell escape complete.");
                    RefreshHud();
                    ExtractionReached?.Invoke();
                    InteractionResolved?.Invoke(trigger.Id);
                    return true;

                default:
                    return false;
            }
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed) return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }
            SoundwellUiTheme.ApplyViewportAspectScale(zonePanel, aspect);
            SoundwellUiTheme.ApplyViewportAspectScale(promptPanel, aspect);
            LockBreakHud.LayoutForViewportAspect(aspect);
            MinimapHud.LayoutForViewportAspect(aspect);
        }

        public void RefreshHud()
        {
            if (disposed) return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }

            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            zoneLabel.font = promptLabel.font = feedbackLabel.font = font;

            if (Explorer.CurrentZone.HasValue)
            {
                RpgMapZone zone = Explorer.CurrentZone.Value;
                zoneLabel.text = zh ? zone.DisplayNameZh : zone.DisplayNameEn;
            }
            else
            {
                zoneLabel.text = zh ? "钟岭声井" : "Soundwell";
            }

            RpgMapTriggerAnchor nearest = default;
            bool hasTrigger = Explorer.TryFindNearestTrigger(out nearest);
            promptLabel.text = hasTrigger
                ? FormatTriggerPrompt(nearest.Id, zh)
                : (zh
                    ? "WASD / 方向键探索 96×60 声井 · 寻找湿布、铜铃与西侧铁闸"
                    : "Move to explore Soundwell (96x60) · Find wet cloth, bell, and west gate");

            feedbackLabel.text = zh ? lastFeedbackZh : lastFeedbackEn;
            LockBreakHud.Refresh();
            MinimapHud.Refresh();
        }

        private void UpdateBeastMinimapMarker()
        {
            Color tint;
            switch (BeastPatrol.State)
            {
                case SoundwellBeastAlertState.Alerted:
                    tint = new Color(0.96f, 0.28f, 0.24f, 1f);
                    break;
                case SoundwellBeastAlertState.InvestigatingBell:
                    tint = new Color(0.42f, 0.84f, 0.96f, 1f);
                    break;
                case SoundwellBeastAlertState.StaggeredBroken:
                    tint = new Color(0.52f, 0.58f, 0.64f, 0.70f);
                    break;
                default:
                    tint = new Color(0.98f, 0.64f, 0.24f, 1f);
                    break;
            }
            MinimapHud.UpdateThreatMarker(BeastPatrol.Position, visible: true, tint: tint);
        }

        private string FormatTriggerPrompt(string triggerId, bool zh)
        {
            switch (triggerId)
            {
                case "soundwell.wet-cloth":
                    return zh ? "[E] 取用湿布消音" : "[E] Apply Wet Cloth Muffle";
                case "soundwell.bell":
                    return zh ? "[E] 敲响铜铃诱敌" : "[E] Ring Bronze Decoy Bell";
                case "soundwell.gate-lever":
                    return IsGateOpen
                        ? (zh ? "西侧铁闸已开启" : "Iron Gate Open")
                        : (zh ? "[E] 拉动西侧铁闸机关" : "[E] Pull Iron Gate Lever");
                case "soundwell.extraction":
                    return zh ? "[E] 经南侧井口撤退脱身" : "[E] Extract via South Hatch";
                default:
                    return string.Empty;
            }
        }

        private void HandleZoneEntered(RpgMapZone zone)
        {
            ZoneChanged?.Invoke(zone.Id);
        }

        private void HandleBeastEncounter()
        {
            BeastEncounterTriggered?.Invoke();
        }

        private void SetFeedback(string zh, string en)
        {
            lastFeedbackZh = zh ?? string.Empty;
            lastFeedbackEn = en ?? string.Empty;
        }

        private bool CanvasDestroyed()
        {
            return hostRoot == null || hudCanvasObject == null ||
                zonePanel == null || promptPanel == null ||
                zoneLabel == null || promptLabel == null || feedbackLabel == null;
        }

        private static void DestroySafe(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Explorer.ZoneEntered -= HandleZoneEntered;
            BeastPatrol.EncounterTriggered -= HandleBeastEncounter;
            SoundwellUiLocalization.Changed -= RefreshHud;
            MinimapHud.Dispose();
            LockBreakHud.Dispose();
            MapRenderer.Dispose();
            SoundwellUiTheme.SafeDestroyCanvas(hudCanvasObject);
            DestroySafe(hostRoot);
        }
    }
}
