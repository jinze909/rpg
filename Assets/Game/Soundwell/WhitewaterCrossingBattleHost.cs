using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Playable 4x-scale S00 Whitewater Crossing (80x44) solo bridgehead defense
    /// stage host wiring WhitewaterCrossingMapLayout, RpgLargeMapChunkRenderer
    /// (6 Point-filtered 32x32 chunks + 4 flaming brazier halos), authored
    /// 16-PPU pixel-art cast SpriteRenderers from RpgPixelArtManifest (Hero,
    /// Shen Li on the pontoon cable, and approaching Bridge Guard/Sapper),
    /// S00BridgeheadCableTracker (1-hero solo bridgehead hold for 3 bells while
    /// Shen Li cuts the pontoon cable), CombatLockBreakHudView,
    /// RpgLargeMapMinimapAndBeaconHud, clamped stage camera framing with clash
    /// shake, and a visual-only bottom status strip.
    /// Strictly subordinate to ORK 3: owns no HP, damage, turn, or defeat state.
    /// </summary>
    public sealed class WhitewaterCrossingBattleHost : IDisposable
    {
        private readonly GameObject hostRoot;
        private readonly GameObject stageHudCanvasObject;
        private readonly RectTransform stagePanel;
        private readonly Font latinFont;
        private readonly Text stageTitleLabel;
        private readonly Text stageStatusLabel;
        private readonly RpgPixelArtManifest artManifest;
        private readonly SpriteRenderer heroSpriteRenderer;
        private readonly SpriteRenderer shenLiSpriteRenderer;
        private readonly SpriteRenderer enemySpriteRenderer;
        private string lastStatusZh;
        private string lastStatusEn;
        private float shakeRemaining;
        private float shakeIntensity;
        private bool thirdBellRaised;
        private bool disposed;

        public WhitewaterCrossingMapLayout Layout { get; }
        public RpgLargeMapChunkRenderer MapRenderer { get; }
        public S00BridgeheadCableTracker CableTracker { get; }
        public CombatLockBreakHudView LockBreakHud { get; }
        public RpgLargeMapMinimapAndBeaconHud MinimapHud { get; }

        public Vector3 HeroDefenseWorldPosition { get; }
        public Vector3 ShenLiCableWorldPosition { get; }
        public Vector3 EnemyFrontWorldPosition { get; }
        public SpriteRenderer HeroSpriteRenderer => heroSpriteRenderer;
        public SpriteRenderer ShenLiSpriteRenderer => shenLiSpriteRenderer;
        public SpriteRenderer EnemySpriteRenderer => enemySpriteRenderer;
        public float ShakeRemaining => shakeRemaining;
        public bool IsDisposed => disposed;
        public Text StageTitleLabel => stageTitleLabel;
        public Text StageStatusLabel => stageStatusLabel;

        public event Action<int> BellAdvanced;
        public event Action ThirdBellCollapseReady;

        public WhitewaterCrossingBattleHost(Transform parent,
            Sprite[] tileSlices = null, int startingMomentum = 1)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));

            hostRoot = new GameObject("Whitewater Crossing S00 battle host");
            hostRoot.transform.SetParent(parent, false);

            artManifest = RpgPixelArtManifest.LoadDefault();
            Layout = WhitewaterCrossingMapLayout.Build();
            MapRenderer = RpgLargeMapChunkRenderer.BuildForWhitewaterCrossing(
                hostRoot.transform, Layout, tileSlices);
            CableTracker = new S00BridgeheadCableTracker(startingMomentum);
            LockBreakHud = new CombatLockBreakHudView(hostRoot.transform, CableTracker);
            MinimapHud = RpgLargeMapMinimapAndBeaconHud.BuildForWhitewaterCrossing(
                hostRoot.transform, Layout);

            HeroDefenseWorldPosition = new Vector3(
                Layout.BridgeheadDefenseTile.x + 0.5f,
                Layout.BridgeheadDefenseTile.y + 0.5f, 0f);
            ShenLiCableWorldPosition = new Vector3(
                Layout.CableAnchorTile.x + 0.5f,
                Layout.CableAnchorTile.y + 0.5f, 0f);
            EnemyFrontWorldPosition = new Vector3(
                Layout.BridgeheadDefenseTile.x + 6.5f,
                Layout.BridgeheadDefenseTile.y + 0.5f, 0f);

            heroSpriteRenderer = CreateCastSprite("S00_Hero", HeroDefenseWorldPosition,
                artManifest != null ? artManifest.FerryGuideIdle : null, flipX: false, sortingOrder: 8);
            shenLiSpriteRenderer = CreateCastSprite("S00_ShenLi", ShenLiCableWorldPosition,
                artManifest != null ? artManifest.ShenLiBridgeIdle : null, flipX: false, sortingOrder: 8);
            enemySpriteRenderer = CreateCastSprite("S00_EnemyFront", EnemyFrontWorldPosition,
                artManifest != null ? artManifest.BridgeGuardIdle : null, flipX: true, sortingOrder: 8);

            MinimapHud.UpdatePlayerPosition(new Vector2(
                HeroDefenseWorldPosition.x, HeroDefenseWorldPosition.y));

            lastStatusZh = "单人独守西桥头 — 掩护沈砾完成三声钟断缆。";
            lastStatusEn = "Hold the west bridgehead alone — cover Shen Li across 3 bells.";

            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Pure visual stage overlay (no GraphicRaycaster) so ORK combat clicks stay unblocked.
            stageHudCanvasObject = new GameObject("Whitewater S00 stage HUD canvas",
                typeof(RectTransform), typeof(Canvas));
            stageHudCanvasObject.transform.SetParent(hostRoot.transform, false);
            var canvas = stageHudCanvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 120;
            var scaler = stageHudCanvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;

            var holder = new GameObject("Whitewater S00 stage strip", typeof(RectTransform));
            holder.transform.SetParent(stageHudCanvasObject.transform, false);
            stagePanel = (RectTransform)holder.transform;
            stagePanel.anchorMin = stagePanel.anchorMax = new Vector2(0.5f, 0f);
            stagePanel.pivot = new Vector2(0.5f, 0f);
            stagePanel.sizeDelta = new Vector2(720f, 52f);
            stagePanel.anchoredPosition = new Vector2(0f, 20f);

            SoundwellUiTheme.CreatePanel(stagePanel, "Stage border",
                new Vector2(720f, 52f), new Color(0.74f, 0.56f, 0.34f, 0.90f));
            SoundwellUiTheme.CreatePanel(stagePanel, "Stage backing",
                new Vector2(716f, 48f), new Color(0.06f, 0.08f, 0.11f, 0.90f));

            stageTitleLabel = SoundwellUiTheme.CreateLabel(stagePanel, 14,
                new Vector2(0f, 11f), new Vector2(690f, 20f), new Color(0.96f, 0.80f, 0.44f, 1f));
            stageTitleLabel.alignment = TextAnchor.MiddleCenter;

            stageStatusLabel = SoundwellUiTheme.CreateLabel(stagePanel, 13,
                new Vector2(0f, -11f), new Vector2(690f, 20f), new Color(0.94f, 0.90f, 0.82f, 1f));
            stageStatusLabel.alignment = TextAnchor.MiddleCenter;

            SoundwellUiLocalization.Changed += RefreshHud;

            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
            RefreshHud();
        }

        private SpriteRenderer CreateCastSprite(string name, Vector3 worldPos,
            Sprite sprite, bool flipX, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(hostRoot.transform, false);
            go.transform.localPosition = worldPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.flipX = flipX;
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        private void SyncCastPoses(CombatLockKind lastHeroAction)
        {
            if (artManifest == null) return;

            if (heroSpriteRenderer != null)
            {
                heroSpriteRenderer.sprite = lastHeroAction == CombatLockKind.ShieldParry
                    ? artManifest.FerryGuideBrace
                    : artManifest.FerryGuideIdle;
            }
            if (shenLiSpriteRenderer != null)
            {
                shenLiSpriteRenderer.sprite =
                    CableTracker.CurrentThreat == S00BridgeThreatKind.SapperRushCable ||
                    CableTracker.ReadyForThirdBellCollapse
                        ? artManifest.ShenLiBraceCable
                        : artManifest.ShenLiBridgeIdle;
            }
            if (enemySpriteRenderer != null)
            {
                enemySpriteRenderer.sprite =
                    CableTracker.CurrentThreat == S00BridgeThreatKind.SapperRushCable
                        ? artManifest.BridgeGuardThrust
                        : artManifest.BridgeGuardIdle;
            }
        }

        /// <summary>
        /// Records an ORK-resolved hero action against the current bridgehead threat.
        /// Returns true if the counter broke the current enemy lock.
        /// </summary>
        public bool ResolveAuthoritativeCounter(CombatLockKind counterAction)
        {
            if (disposed) return false;
            if (CanvasDestroyed())
            {
                Dispose();
                return false;
            }

            int previousBells = CableTracker.BellsCompleted;
            S00BridgeThreatKind threatBefore = CableTracker.CurrentThreat;
            bool brokeLock = CableTracker.ResolveHeroCounter(counterAction);

            if (brokeLock)
            {
                TriggerClashShake(0.24f, 0.14f);
                if (CableTracker.ReadyForThirdBellCollapse)
                {
                    MinimapHud.SetBeaconCompleted("s00.cable-anchor", true);
                    MinimapHud.SetBeaconCompleted("s00.bridgehead", true);
                    lastStatusZh = "第三声钟响！沈砾斩断主缆，浮桥即将倾覆！";
                    lastStatusEn = "Third bell tolls! Shen Li severs the main cable — bridge collapse ready!";
                }
                else if (threatBefore == S00BridgeThreatKind.SpearThrustAtHero)
                {
                    lastStatusZh = "举盾稳接长矛重刺，稳势 +1，沈砾继续砍缆。";
                    lastStatusEn = "Shield parry absorbed the spear thrust (+1 Momentum) — Shen Li cuts on.";
                }
                else
                {
                    lastStatusZh = "挥刀逼退扑缆工兵，稳势 +1，本拍断缆保住！";
                    lastStatusEn = "Blade repel drove off the cable sapper (+1 Momentum) — cut preserved!";
                }
            }
            else if (threatBefore == S00BridgeThreatKind.SapperRushCable &&
                !CableTracker.ReadyForThirdBellCollapse)
            {
                TriggerClashShake(0.16f, 0.10f);
                lastStatusZh = "未能逼退工兵，沈砾被迫暂避，本拍断缆进度受阻！";
                lastStatusEn = "Sapper was not repelled — Shen Li's cable cut stalled this bell!";
            }
            else if (!CableTracker.ReadyForThirdBellCollapse)
            {
                TriggerClashShake(0.14f, 0.08f);
                lastStatusZh = "硬扛长矛冲击，沈砾趁隙推进一拍断缆。";
                lastStatusEn = "Braced through the spear lunge — Shen Li pushes the cut forward.";
            }

            SyncCastPoses(counterAction);
            RefreshHud();

            if (CableTracker.BellsCompleted > previousBells)
                BellAdvanced?.Invoke(CableTracker.BellsCompleted);

            if (CableTracker.ReadyForThirdBellCollapse && !thirdBellRaised)
            {
                thirdBellRaised = true;
                ThirdBellCollapseReady?.Invoke();
            }

            return brokeLock;
        }

        public void TriggerClashShake(float intensity, float durationSeconds)
        {
            if (intensity <= 0f || durationSeconds <= 0f) return;
            shakeIntensity = Mathf.Max(shakeIntensity, intensity);
            shakeRemaining = Mathf.Max(shakeRemaining, durationSeconds);
        }

        public Vector3 ComputeFramedCameraPosition(Vector3 currentCameraPos, float deltaTime,
            float halfViewWidth = RpgWorldMapScale.LogicalScreenTilesX * 0.5f,
            float halfViewHeight = RpgWorldMapScale.LogicalScreenTilesY * 0.5f,
            float smoothSpeed = 6f)
        {
            Vector3 anchor = (ShenLiCableWorldPosition + EnemyFrontWorldPosition) * 0.5f;
            Bounds bounds = Layout.CameraBounds;
            float minX = bounds.min.x + halfViewWidth;
            float maxX = bounds.max.x - halfViewWidth;
            float minY = bounds.min.y + halfViewHeight;
            float maxY = bounds.max.y - halfViewHeight;

            float targetX = minX <= maxX ? Mathf.Clamp(anchor.x, minX, maxX) : bounds.center.x;
            float targetY = minY <= maxY ? Mathf.Clamp(anchor.y, minY, maxY) : bounds.center.y;

            float t = deltaTime > 0f ? 1f - Mathf.Exp(-Mathf.Max(0.1f, smoothSpeed) * deltaTime) : 1f;
            float x = Mathf.Lerp(currentCameraPos.x, targetX, t);
            float y = Mathf.Lerp(currentCameraPos.y, targetY, t);

            if (shakeRemaining > 0f && deltaTime > 0f)
            {
                shakeRemaining = Mathf.Max(0f, shakeRemaining - deltaTime);
                x += Mathf.Sin(shakeRemaining * 92f) * shakeIntensity;
                y += Mathf.Cos(shakeRemaining * 71f) * shakeIntensity * 0.55f;
                if (shakeRemaining <= 0f)
                    shakeIntensity = 0f;
            }

            if (minX <= maxX) x = Mathf.Clamp(x, minX, maxX);
            if (minY <= maxY) y = Mathf.Clamp(y, minY, maxY);
            return new Vector3(x, y, currentCameraPos.z);
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed) return;
            if (CanvasDestroyed())
            {
                Dispose();
                return;
            }
            SoundwellUiTheme.ApplyViewportAspectScale(stagePanel, aspect);
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
            stageTitleLabel.font = stageStatusLabel.font = font;

            stageTitleLabel.text = zh
                ? "S00 白水渡浮桥 · 西桥头独守"
                : "S00 Whitewater Crossing · West Bridgehead Hold";
            stageStatusLabel.text = zh ? lastStatusZh : lastStatusEn;
            LockBreakHud.Refresh();
            MinimapHud.Refresh();
        }

        private bool CanvasDestroyed()
        {
            return hostRoot == null || stageHudCanvasObject == null ||
                stagePanel == null || stageTitleLabel == null || stageStatusLabel == null;
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
            SoundwellUiLocalization.Changed -= RefreshHud;
            MinimapHud.Dispose();
            LockBreakHud.Dispose();
            MapRenderer.Dispose();
            SoundwellUiTheme.SafeDestroyCanvas(stageHudCanvasObject);
            DestroySafe(hostRoot);
        }
    }
}
