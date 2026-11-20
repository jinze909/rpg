using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    public enum BattleContextMode
    {
        WhitewaterSolo,
        WhitewaterBridgehead,
        SoundwellParty
    }

    public enum RecommendedCommand
    {
        None,
        Attack,
        Guard
    }

    /// <summary>
    /// Live runtime combat clash VFX director strictly subordinate to ORK 3.
    /// Watches authoritative OrkPartyBridge HP/guard transitions and renders
    /// diagonal slash arcs, spear-impact flashes, shield ward pulses, drift-free
    /// status-card recoil, recommended command button pulse, and solo-vs-party
    /// S00 story framing without intercepting input or altering ORK combat math.
    /// </summary>
    public sealed class BattleClashVfxDirector : MonoBehaviour
    {
        private sealed class ClashEffect
        {
            public GameObject Root;
            public RectTransform Rect;
            public Image Image;
            public Color BaseColor;
            public Vector2 StartSize;
            public Vector2 EndSize;
            public float Elapsed;
            public float Duration;
        }

        private sealed class CardRecoil
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 RestPosition;
            public Color RestColor;
            public Vector2 Impulse;
            public Color FlashColor;
            public float Elapsed;
            public float Duration;
        }

        private static BattleClashVfxDirector instance;

        private GameObject canvasObject;
        private RectTransform fxRoot;
        private readonly List<ClashEffect> effects = new List<ClashEffect>();
        private readonly Dictionary<RectTransform, CardRecoil> recoils =
            new Dictionary<RectTransform, CardRecoil>();

        private OrkPartyBridge.BattleUnitSnapshot[] prevSnapshots;
        private bool wasBattleActive;
        private EnemyIntentKind visualIntent = EnemyIntentKind.ChargedLunge;
        private Transform pulsedButton;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureDirector()
        {
            if (!Application.isPlaying || instance != null)
                return;
            var go = new GameObject("BattleClashVfxDirector");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<BattleClashVfxDirector>();
        }

        public static BattleContextMode DetectContext(bool whitewaterActive, int allyCount)
        {
            if (whitewaterActive)
                return allyCount <= 1
                    ? BattleContextMode.WhitewaterSolo
                    : BattleContextMode.WhitewaterBridgehead;
            return BattleContextMode.SoundwellParty;
        }

        public static string FormatContextSubtitle(BattleContextMode mode, bool zh)
        {
            switch (mode)
            {
                case BattleContextMode.WhitewaterSolo:
                    return zh
                        ? "白水渡桥头 · 你独守渡口（沈砾攥紧缆索中）"
                        : "WHITEWATER BRIDGEHEAD · SOLO HOLD (SHEN LI ON CABLE)";
                case BattleContextMode.WhitewaterBridgehead:
                    return zh
                        ? "白水渡桥头 · 顶住持矛小队至第三声钟响"
                        : "WHITEWATER BRIDGEHEAD · HOLD UNTIL THE THIRD BELL";
                case BattleContextMode.SoundwellParty:
                    return zh
                        ? "钟岭声井 · 声学协同救援"
                        : "SOUNDWELL · ACOUSTIC RESCUE FORMATION";
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }

        public static RecommendedCommand ComputeRecommendedCommand(EnemyIntentKind intent)
        {
            switch (intent)
            {
                case EnemyIntentKind.ChargedLunge:
                    return RecommendedCommand.Guard;
                case EnemyIntentKind.Recovering:
                    return RecommendedCommand.Attack;
                default:
                    return RecommendedCommand.None;
            }
        }

        public static (Vector2 offset, float flashAlpha) EvaluateCardRecoil(
            float normalizedTime, Vector2 impulse)
        {
            float t = Mathf.Clamp01(normalizedTime);
            if (t >= 1f || impulse.sqrMagnitude < 0.0001f)
                return (Vector2.zero, 0f);

            float decay = (1f - t) * (1f - t);
            float osc = Mathf.Cos(t * Mathf.PI * 3f);
            Vector2 offset = impulse * (decay * osc);
            float flash = t < 0.4f ? 1f - (t / 0.4f) : 0f;
            return (offset, Mathf.Clamp01(flash));
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            BuildOverlay();
        }

        private void BuildOverlay()
        {
            // Pure visual VFX layer: no GraphicRaycaster, zero input blocking.
            canvasObject = new GameObject("Battle clash VFX canvas",
                typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 127;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;

            var rootObj = new GameObject("Battle clash VFX root", typeof(RectTransform));
            rootObj.transform.SetParent(canvasObject.transform, false);
            fxRoot = (RectTransform)rootObj.transform;
            fxRoot.anchorMin = Vector2.zero;
            fxRoot.anchorMax = Vector2.one;
            fxRoot.offsetMin = Vector2.zero;
            fxRoot.offsetMax = Vector2.zero;
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            UpdateEffects(dt);
            UpdateRecoils(dt);

            OrkPartyBridge party = FindFirstObjectByType<OrkPartyBridge>();
            bool active = party != null && party.BattleInProgress;
            if (!active)
            {
                if (wasBattleActive)
                {
                    wasBattleActive = false;
                    prevSnapshots = null;
                    RestorePulsedButton();
                    RestoreAllRecoils();
                }
                return;
            }

            if (!wasBattleActive)
            {
                wasBattleActive = true;
                visualIntent = EnemyIntentKind.ChargedLunge;
                prevSnapshots = null;
            }

            OrkPartyBridge.BattleUnitSnapshot[] current = party.CaptureBattleSnapshots();
            if (prevSnapshots != null && current != null)
                ObserveOrkExchange(prevSnapshots, current);
            prevSnapshots = current;

            UpdateRecommendedButtonPulse();
        }

        private void ObserveOrkExchange(
            OrkPartyBridge.BattleUnitSnapshot[] previous,
            OrkPartyBridge.BattleUnitSnapshot[] current)
        {
            int count = Mathf.Min(previous.Length, current.Length);
            int allyIdx = 0;
            for (int i = 0; i < count; i++)
            {
                var prev = previous[i];
                var cur = current[i];
                int loss = Mathf.Max(0, prev.Hp - cur.Hp);
                if (cur.IsEnemy)
                {
                    if (loss > 0)
                    {
                        SpawnSlashArc(new Vector2(322f, 144f), -32f,
                            new Color(1f, 0.88f, 0.48f, 0.95f));
                        TriggerNamedCardRecoil("S00 enemy card",
                            new Vector2(18f, 0f), new Color(1f, 0.92f, 0.72f, 1f));
                        if (visualIntent == EnemyIntentKind.Recovering)
                            visualIntent = EnemyIntentKind.ListeningAlert;
                    }
                }
                else
                {
                    if (loss > 0)
                    {
                        float y = 144f - Mathf.Clamp(allyIdx, 0, 2) * 50f;
                        SpawnSlashArc(new Vector2(-322f, y), 28f,
                            new Color(0.96f, 0.28f, 0.24f, 0.95f));
                        TriggerNamedCardRecoil("S00 ally card " + allyIdx,
                            new Vector2(-18f, 0f), new Color(0.95f, 0.30f, 0.28f, 1f));
                        visualIntent = visualIntent == EnemyIntentKind.ChargedLunge
                            ? EnemyIntentKind.Recovering
                            : EnemyIntentKind.ChargedLunge;
                    }
                    else if (!prev.Defending && cur.Defending)
                    {
                        float y = 144f - Mathf.Clamp(allyIdx, 0, 2) * 50f;
                        SpawnGuardPulse(new Vector2(-322f, y));
                    }
                    allyIdx++;
                }
            }
        }

        private void SpawnSlashArc(Vector2 anchoredPos, float angleDeg, Color color)
        {
            if (fxRoot == null) return;
            var obj = new GameObject("Battle slash VFX",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(fxRoot, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
            var img = obj.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;

            effects.Add(new ClashEffect
            {
                Root = obj,
                Rect = rect,
                Image = img,
                BaseColor = color,
                StartSize = new Vector2(36f, 10f),
                EndSize = new Vector2(210f, 3f),
                Elapsed = 0f,
                Duration = 0.22f
            });
        }

        private void SpawnGuardPulse(Vector2 anchoredPos)
        {
            if (fxRoot == null) return;
            var obj = new GameObject("Battle guard pulse VFX",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(fxRoot, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            var img = obj.GetComponent<Image>();
            Color color = new Color(0.52f, 0.80f, 1f, 0.55f);
            img.color = color;
            img.raycastTarget = false;

            effects.Add(new ClashEffect
            {
                Root = obj,
                Rect = rect,
                Image = img,
                BaseColor = color,
                StartSize = new Vector2(236f, 42f),
                EndSize = new Vector2(268f, 60f),
                Elapsed = 0f,
                Duration = 0.28f
            });
        }

        private void TriggerNamedCardRecoil(string cardName, Vector2 impulse, Color flashColor)
        {
            GameObject cardObj = GameObject.Find(cardName);
            if (cardObj == null) return;
            var rect = cardObj.GetComponent<RectTransform>();
            var img = cardObj.GetComponent<Image>();
            if (rect == null || img == null) return;

            if (recoils.TryGetValue(rect, out CardRecoil existing))
            {
                rect.anchoredPosition = existing.RestPosition;
                img.color = existing.RestColor;
            }

            recoils[rect] = new CardRecoil
            {
                Rect = rect,
                Image = img,
                RestPosition = rect.anchoredPosition,
                RestColor = img.color,
                Impulse = impulse,
                FlashColor = flashColor,
                Elapsed = 0f,
                Duration = 0.24f
            };
        }

        private void UpdateEffects(float dt)
        {
            if (dt <= 0f || effects.Count == 0) return;
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                ClashEffect fx = effects[i];
                if (fx.Root == null)
                {
                    effects.RemoveAt(i);
                    continue;
                }
                fx.Elapsed += dt;
                float t = Mathf.Clamp01(fx.Elapsed / fx.Duration);
                fx.Rect.sizeDelta = Vector2.Lerp(fx.StartSize, fx.EndSize, t);
                Color c = fx.BaseColor;
                c.a = fx.BaseColor.a * (1f - t * t);
                fx.Image.color = c;
                if (fx.Elapsed >= fx.Duration)
                {
                    DestroySafe(fx.Root);
                    effects.RemoveAt(i);
                }
            }
        }

        private void UpdateRecoils(float dt)
        {
            if (dt <= 0f || recoils.Count == 0) return;
            var finished = new List<RectTransform>();
            foreach (var pair in recoils)
            {
                CardRecoil r = pair.Value;
                if (r.Rect == null || r.Image == null)
                {
                    finished.Add(pair.Key);
                    continue;
                }
                r.Elapsed += dt;
                float t = Mathf.Clamp01(r.Elapsed / r.Duration);
                var (offset, flash) = EvaluateCardRecoil(t, r.Impulse);
                r.Rect.anchoredPosition = r.RestPosition + offset;
                r.Image.color = Color.Lerp(r.RestColor, r.FlashColor, flash * 0.65f);
                if (r.Elapsed >= r.Duration)
                {
                    r.Rect.anchoredPosition = r.RestPosition;
                    r.Image.color = r.RestColor;
                    finished.Add(pair.Key);
                }
            }
            for (int i = 0; i < finished.Count; i++)
                recoils.Remove(finished[i]);
        }

        private void UpdateRecommendedButtonPulse()
        {
            RecommendedCommand rec = ComputeRecommendedCommand(visualIntent);
            string targetName = rec == RecommendedCommand.Guard ? "S00 ORK guard"
                : rec == RecommendedCommand.Attack ? "S00 ORK attack"
                : null;

            GameObject targetObj = targetName != null ? GameObject.Find(targetName) : null;
            Button btn = targetObj != null ? targetObj.GetComponent<Button>() : null;
            Transform nextTarget = (btn != null && btn.interactable && targetObj.activeInHierarchy)
                ? targetObj.transform
                : null;

            if (pulsedButton != nextTarget)
                RestorePulsedButton();

            pulsedButton = nextTarget;
            if (pulsedButton != null)
            {
                float s = 1f + 0.045f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6.5f));
                pulsedButton.localScale = new Vector3(s, s, 1f);
            }
        }

        private void RestorePulsedButton()
        {
            if (pulsedButton != null)
                pulsedButton.localScale = Vector3.one;
            pulsedButton = null;
        }

        private void RestoreAllRecoils()
        {
            foreach (var pair in recoils)
            {
                CardRecoil r = pair.Value;
                if (r.Rect != null) r.Rect.anchoredPosition = r.RestPosition;
                if (r.Image != null) r.Image.color = r.RestColor;
            }
            recoils.Clear();
        }

        private static void DestroySafe(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        private void OnDestroy()
        {
            RestorePulsedButton();
            RestoreAllRecoils();
            for (int i = 0; i < effects.Count; i++)
                DestroySafe(effects[i]?.Root);
            effects.Clear();
            SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
            if (instance == this)
                instance = null;
        }
    }
}
