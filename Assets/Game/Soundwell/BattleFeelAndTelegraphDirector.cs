using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    public readonly struct BattleSnapshotDelta
    {
        public BattleSnapshotDelta(int totalEnemyHpLoss, int totalAllyHpLoss,
            int struckAllyIndex, int newGuardAllyIndex)
        {
            TotalEnemyHpLoss = totalEnemyHpLoss;
            TotalAllyHpLoss = totalAllyHpLoss;
            StruckAllyIndex = struckAllyIndex;
            NewGuardAllyIndex = newGuardAllyIndex;
        }

        public int TotalEnemyHpLoss { get; }
        public int TotalAllyHpLoss { get; }
        public int StruckAllyIndex { get; }
        public int NewGuardAllyIndex { get; }
    }

    /// <summary>
    /// Live runtime combat-feel and tactical-telegraph director that automatically
    /// activates during any ORK battle (including S00 Whitewater bridgehead defense
    /// and Soundwell encounters). Strictly subordinate to ORK 3: reads authoritative
    /// HP/turn snapshots from OrkPartyBridge and adds floating damage popups,
    /// camera impact shake, procedural hit/guard SFX, top threat banner, and
    /// per-slot 3-role tactical cues without intercepting any UGUI input.
    /// </summary>
    public sealed class BattleFeelAndTelegraphDirector : MonoBehaviour
    {
        private sealed class FloatingPopup
        {
            public GameObject Root;
            public RectTransform Rect;
            public Text Label;
            public Color BaseColor;
            public float Elapsed;
            public float Duration;
        }

        private static BattleFeelAndTelegraphDirector instance;

        private GameObject canvasObject;
        private RectTransform overlayRoot;
        private GameObject threatBanner;
        private Image threatBorder;
        private Text threatText;
        private GameObject roleStrip;
        private Text roleStripText;
        private Font latinFont;
        private AudioSource sfxSource;
        private AudioClip hitClip;
        private AudioClip guardClip;

        private readonly List<FloatingPopup> popups = new List<FloatingPopup>();
        private AcousticIntentTelegraph telegraph = new AcousticIntentTelegraph();
        private OrkPartyBridge.BattleUnitSnapshot[] previousSnapshots;
        private bool wasBattleActive;
        private int lockedAllySlot;

        private float shakeRemaining;
        private float shakeIntensity;
        private Vector3 appliedShakeOffset;
        private Camera shakenCamera;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeDirector()
        {
            if (!Application.isPlaying || instance != null)
                return;
            var go = new GameObject("BattleFeelAndTelegraphDirector");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<BattleFeelAndTelegraphDirector>();
        }

        public static BattleSnapshotDelta ComputeSnapshotDeltas(
            OrkPartyBridge.BattleUnitSnapshot[] previous,
            OrkPartyBridge.BattleUnitSnapshot[] current)
        {
            if (previous == null || current == null || previous.Length == 0 || current.Length == 0)
                return default;

            int enemyLoss = 0;
            int allyLoss = 0;
            int struckAlly = -1;
            int newGuardAlly = -1;
            int allyIdx = 0;

            int count = Mathf.Min(previous.Length, current.Length);
            for (int i = 0; i < count; i++)
            {
                var prev = previous[i];
                var cur = current[i];
                int loss = Mathf.Max(0, prev.Hp - cur.Hp);
                if (cur.IsEnemy)
                {
                    enemyLoss += loss;
                }
                else
                {
                    if (loss > 0)
                    {
                        allyLoss += loss;
                        if (struckAlly < 0)
                            struckAlly = allyIdx;
                    }
                    if (!prev.Defending && cur.Defending && newGuardAlly < 0)
                        newGuardAlly = allyIdx;
                    allyIdx++;
                }
            }

            return new BattleSnapshotDelta(enemyLoss, allyLoss, struckAlly, newGuardAlly);
        }

        public static string FormatThreatBanner(EnemyIntentKind intent, int lockedSlot, bool zh)
        {
            int displaySlot = Mathf.Clamp(lockedSlot, 0, 2) + 1;
            switch (intent)
            {
                case EnemyIntentKind.ChargedLunge:
                    return zh
                        ? "⚠ 敌方蓄力锁定 " + displaySlot + " 号位 · 推荐前排举盾挡伤或趁破绽反击"
                        : "⚠ LOCKED ON SLOT " + displaySlot + " · GUARD TO MITIGATE OR COUNTER";
                case EnemyIntentKind.ListeningAlert:
                    return zh
                        ? "◎ 敌方警觉监听中 · 注意阵型节奏，适时防反"
                        : "◎ ENEMY ALERT · WATCH NOISE & GUARD LOCKED ALLY";
                case EnemyIntentKind.Recovering:
                    return zh
                        ? "★ 敌方失衡破绽 · 趁现在全力集火输出！"
                        : "★ ENEMY STAGGERED · PRESS ATTACK NOW!";
                default:
                    return string.Empty;
            }
        }

        public static string FormatRoleHint(int chooserIndex, EnemyIntentKind intent, bool zh)
        {
            bool charging = intent == EnemyIntentKind.ChargedLunge;
            bool opening = intent == EnemyIntentKind.Recovering;
            if (chooserIndex <= 0)
            {
                if (zh)
                    return charging
                        ? "【1号·前排护阵】 推荐 [防御] 举盾挡下蓄力  ｜  [攻击] 强行压阵"
                        : "【1号·前排护阵】 [攻击] 稳击压阵  ｜  [防御] 举盾减伤稳阵";
                return charging
                    ? "[Slot 1 · Vanguard] TIP: [Guard] Shield Charge  |  [Attack] Press Strike"
                    : "[Slot 1 · Vanguard] [Attack] Press Strike  |  [Guard] Shield Cover";
            }
            if (chooserIndex == 1)
            {
                if (zh)
                    return opening
                        ? "【2号·破阵主力】 推荐 [攻击] 趁破绽重击！  ｜  [防御] 稳索防反"
                        : "【2号·破阵主力】 [攻击] 破阵斩击  ｜  [防御] 稳索防反";
                return opening
                    ? "[Slot 2 · Breaker] TIP: [Attack] Punish Opening!  |  [Guard] Brace"
                    : "[Slot 2 · Breaker] [Attack] Break Strike  |  [Guard] Brace Cable";
            }

            if (zh)
                return charging
                    ? "【3号·侧翼支援】 [防御] 稳阵保血  ｜  [攻击] 侧翼补击"
                    : "【3号·侧翼支援】 [攻击] 侧翼补击  ｜  [防御] 掩护稳阵";
            return charging
                ? "[Slot 3 · Support] [Guard] Hold Line  |  [Attack] Flank Strike"
                : "[Slot 3 · Support] [Attack] Flank Strike  |  [Guard] Cover & Steady";
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildOverlay();
            BuildProceduralSfx();
        }

        private void BuildOverlay()
        {
            // Visual-only overlay (no GraphicRaycaster) so it never blocks UGUI input.
            canvasObject = new GameObject("Battle feel telegraph canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 126;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;

            var rootObj = new GameObject("Battle feel root", typeof(RectTransform));
            rootObj.transform.SetParent(canvasObject.transform, false);
            overlayRoot = (RectTransform)rootObj.transform;
            overlayRoot.anchorMin = Vector2.zero;
            overlayRoot.anchorMax = Vector2.one;
            overlayRoot.offsetMin = Vector2.zero;
            overlayRoot.offsetMax = Vector2.zero;

            // Top threat banner sits cleanly below top bar without covering characters.
            var bannerObj = new GameObject("Battle threat banner", typeof(RectTransform));
            bannerObj.transform.SetParent(overlayRoot, false);
            var bannerRect = (RectTransform)bannerObj.transform;
            bannerRect.anchorMin = bannerRect.anchorMax = new Vector2(0.5f, 0.5f);
            bannerRect.anchoredPosition = new Vector2(0f, 158f);
            bannerRect.sizeDelta = new Vector2(540f, 28f);
            threatBorder = SoundwellUiTheme.CreatePanel(bannerRect, "Threat border",
                new Vector2(540f, 28f), new Color(0.92f, 0.28f, 0.24f, 0.95f));
            SoundwellUiTheme.CreatePanel(bannerRect, "Threat backing",
                new Vector2(536f, 24f), new Color(0.07f, 0.08f, 0.11f, 0.92f));
            threatText = SoundwellUiTheme.CreateLabel(bannerRect, 13,
                Vector2.zero, new Vector2(520f, 22f), new Color(0.98f, 0.92f, 0.80f, 1f));
            threatText.alignment = TextAnchor.MiddleCenter;
            threatBanner = bannerObj;

            // Bottom tactical role strip sits cleanly above command bar.
            var stripObj = new GameObject("Battle role hint strip", typeof(RectTransform));
            stripObj.transform.SetParent(overlayRoot, false);
            var stripRect = (RectTransform)stripObj.transform;
            stripRect.anchorMin = stripRect.anchorMax = new Vector2(0.5f, 0.5f);
            stripRect.anchoredPosition = new Vector2(0f, -162f);
            stripRect.sizeDelta = new Vector2(680f, 26f);
            SoundwellUiTheme.CreatePanel(stripRect, "Role strip border",
                new Vector2(680f, 26f), new Color(0.68f, 0.58f, 0.38f, 0.85f));
            SoundwellUiTheme.CreatePanel(stripRect, "Role strip backing",
                new Vector2(676f, 22f), new Color(0.06f, 0.08f, 0.11f, 0.90f));
            roleStripText = SoundwellUiTheme.CreateLabel(stripRect, 13,
                Vector2.zero, new Vector2(660f, 20f), new Color(0.94f, 0.90f, 0.82f, 1f));
            roleStripText.alignment = TextAnchor.MiddleCenter;
            roleStrip = stripObj;

            SetOverlayVisible(false);
        }

        private void BuildProceduralSfx()
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;
            hitClip = CreateToneClip("BattleHitSfx", 0.11f, 175f, 75f, noiseMix: 0.45f);
            guardClip = CreateToneClip("BattleGuardSfx", 0.10f, 520f, 340f, noiseMix: 0.15f);
        }

        private static AudioClip CreateToneClip(string clipName, float duration,
            float startFreq, float endFreq, float noiseMix)
        {
            const int sampleRate = 22050;
            int sampleCount = Mathf.Max(64, Mathf.RoundToInt(sampleRate * duration));
            var samples = new float[sampleCount];
            uint seed = 0x9E3779B9u;
            float phase = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                float env = (1f - t) * (1f - t);
                float freq = Mathf.Lerp(startFreq, endFreq, t);
                phase += (2f * Mathf.PI * freq) / sampleRate;
                seed = seed * 1664525u + 1013904223u;
                float white = ((seed >> 9) / (float)(1 << 23)) * 2f - 1f;
                samples[i] = (Mathf.Sin(phase) * (1f - noiseMix) + white * noiseMix) * env * 0.35f;
            }
            var clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void LateUpdate()
        {
            UpdatePopups(Time.unscaledDeltaTime);
            UpdateCameraShake(Time.unscaledDeltaTime);

            OrkPartyBridge party = FindFirstObjectByType<OrkPartyBridge>();
            bool active = party != null && party.BattleInProgress;
            if (!active)
            {
                if (wasBattleActive)
                {
                    wasBattleActive = false;
                    previousSnapshots = null;
                    ClearCameraShake();
                    SetOverlayVisible(false);
                }
                return;
            }

            if (!wasBattleActive)
            {
                wasBattleActive = true;
                telegraph = new AcousticIntentTelegraph(20, EnemyIntentKind.ChargedLunge);
                lockedAllySlot = 0;
                previousSnapshots = null;
                SetOverlayVisible(true);
            }

            OrkPartyBridge.BattleUnitSnapshot[] current = party.CaptureBattleSnapshots();
            if (previousSnapshots != null)
            {
                BattleSnapshotDelta delta = ComputeSnapshotDeltas(previousSnapshots, current);
                HandleOrkDelta(delta);
            }
            previousSnapshots = current;

            RefreshHudText(party.ChoosingRescuerIndex);
        }

        private void HandleOrkDelta(BattleSnapshotDelta delta)
        {
            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;

            if (delta.TotalEnemyHpLoss > 0)
            {
                bool exposed = telegraph.EnemyIntentExposed;
                telegraph.RecordPlayerAction(AcousticActionKind.OrkAttack);
                var (_, shake) = AcousticIntentTelegraph.ComputeOrkHitFeedback(
                    delta.TotalEnemyHpLoss, 0, exposed);
                TriggerCameraShake(Mathf.Max(0.14f, shake), 0.14f);
                SpawnPopup("-" + delta.TotalEnemyHpLoss + (exposed ? "!" : string.Empty),
                    new Vector2(310f, 168f),
                    exposed ? new Color(1f, 0.84f, 0.32f, 1f) : new Color(1f, 0.45f, 0.36f, 1f));
                PlayClipIfAudioAvailable(hitClip);
            }

            if (delta.TotalAllyHpLoss > 0)
            {
                var (_, shake) = AcousticIntentTelegraph.ComputeOrkHitFeedback(
                    0, delta.TotalAllyHpLoss, wasExposedTelegraph: false);
                TriggerCameraShake(Mathf.Max(0.18f, shake), 0.16f);
                float y = 160f - Mathf.Clamp(delta.StruckAllyIndex, 0, 2) * 50f;
                SpawnPopup("-" + delta.TotalAllyHpLoss,
                    new Vector2(-310f, y), new Color(0.98f, 0.28f, 0.26f, 1f));
                PlayClipIfAudioAvailable(hitClip);
                telegraph.AdvanceTelegraphAfterOrkEnemyTurn();
                lockedAllySlot = (lockedAllySlot + 1) % 3;
            }

            if (delta.NewGuardAllyIndex >= 0)
            {
                telegraph.RecordPlayerAction(AcousticActionKind.OrkGuard);
                float y = 160f - Mathf.Clamp(delta.NewGuardAllyIndex, 0, 2) * 50f;
                SpawnPopup(zh ? "举盾" : "GUARD",
                    new Vector2(-240f, y), new Color(0.55f, 0.82f, 1f, 1f));
                PlayClipIfAudioAvailable(guardClip);
            }
        }

        private void RefreshHudText(int chooserIndex)
        {
            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            threatText.font = roleStripText.font = font;
            threatText.text = FormatThreatBanner(telegraph.CurrentIntent, lockedAllySlot, zh);
            roleStripText.text = FormatRoleHint(chooserIndex, telegraph.CurrentIntent, zh);

            if (threatBorder != null)
            {
                threatBorder.color = telegraph.CurrentIntent == EnemyIntentKind.ChargedLunge
                    ? new Color(0.92f, 0.28f, 0.24f, 0.95f)
                    : telegraph.CurrentIntent == EnemyIntentKind.Recovering
                        ? new Color(0.42f, 0.78f, 0.92f, 0.95f)
                        : new Color(0.94f, 0.72f, 0.30f, 0.95f);
            }
        }

        private void SpawnPopup(string text, Vector2 anchoredPos, Color color)
        {
            if (overlayRoot == null) return;
            var obj = new GameObject("Battle damage popup", typeof(RectTransform));
            obj.transform.SetParent(overlayRoot, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(180f, 34f);

            Text label = SoundwellUiTheme.CreateLabel(rect, 22, Vector2.zero,
                new Vector2(180f, 34f), color);
            label.font = SoundwellUiLocalization.PresentationFont(latinFont);
            label.alignment = TextAnchor.MiddleCenter;
            label.text = text;
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.03f, 0.04f, 0.06f, 0.95f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            popups.Add(new FloatingPopup
            {
                Root = obj,
                Rect = rect,
                Label = label,
                BaseColor = color,
                Elapsed = 0f,
                Duration = 0.75f
            });
        }

        private void UpdatePopups(float dt)
        {
            if (dt <= 0f || popups.Count == 0) return;
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                FloatingPopup p = popups[i];
                if (p.Root == null)
                {
                    popups.RemoveAt(i);
                    continue;
                }
                p.Elapsed += dt;
                float t = Mathf.Clamp01(p.Elapsed / p.Duration);
                p.Rect.anchoredPosition += new Vector2(0f, 48f * dt);
                Color c = p.BaseColor;
                c.a = 1f - t * t;
                p.Label.color = c;
                if (p.Elapsed >= p.Duration)
                {
                    DestroyObjectSafe(p.Root);
                    popups.RemoveAt(i);
                }
            }
        }

        private void TriggerCameraShake(float intensity, float duration)
        {
            shakeIntensity = Mathf.Max(shakeIntensity, intensity);
            shakeRemaining = Mathf.Max(shakeRemaining, duration);
        }

        private void UpdateCameraShake(float dt)
        {
            if (shakenCamera != null && appliedShakeOffset != Vector3.zero)
            {
                shakenCamera.transform.position -= appliedShakeOffset;
                appliedShakeOffset = Vector3.zero;
            }

            if (shakeRemaining <= 0f || dt <= 0f)
                return;

            Camera cam = Camera.main;
            if (cam == null)
            {
                shakeRemaining = 0f;
                shakeIntensity = 0f;
                return;
            }

            shakeRemaining = Mathf.Max(0f, shakeRemaining - dt);
            if (shakeRemaining <= 0f)
            {
                shakeIntensity = 0f;
                return;
            }

            float amp = shakeIntensity * (shakeRemaining / 0.16f) * 0.25f;
            appliedShakeOffset = new Vector3(
                Mathf.Sin(shakeRemaining * 95f) * amp,
                Mathf.Cos(shakeRemaining * 78f) * amp * 0.6f,
                0f);
            shakenCamera = cam;
            cam.transform.position += appliedShakeOffset;
        }

        private void ClearCameraShake()
        {
            if (shakenCamera != null && appliedShakeOffset != Vector3.zero)
                shakenCamera.transform.position -= appliedShakeOffset;
            appliedShakeOffset = Vector3.zero;
            shakeRemaining = 0f;
            shakeIntensity = 0f;
        }

        private void PlayClipIfAudioAvailable(AudioClip clip)
        {
            if (clip == null || sfxSource == null) return;
            if (FindFirstObjectByType<AudioListener>() == null) return;
            sfxSource.PlayOneShot(clip, 0.65f);
        }

        private void SetOverlayVisible(bool visible)
        {
            if (threatBanner != null) threatBanner.SetActive(visible);
            if (roleStrip != null) roleStrip.SetActive(visible);
        }

        private static void DestroyObjectSafe(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        private void OnDestroy()
        {
            ClearCameraShake();
            for (int i = 0; i < popups.Count; i++)
                DestroyObjectSafe(popups[i]?.Root);
            popups.Clear();
            DestroyObjectSafe(hitClip);
            DestroyObjectSafe(guardClip);
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
            if (instance == this)
                instance = null;
        }
    }
}
