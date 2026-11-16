using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Runs the S00 bridgehead as a real ORK encounter. The three player
    /// rescuers and the far-bank guard squad are registered by OrkPartyBridge;
    /// this class only presents the live ORK chooser and accepted actions.
    /// </summary>
    public sealed class WhitewaterDefenseEncounter : MonoBehaviour
    {
        private static readonly Color Ink = new Color(0.87f, 0.90f, 0.88f);
        private static readonly Color Brass = new Color(0.91f, 0.72f, 0.40f);

        private OrkPartyBridge party;
        private Canvas hud;
        private Text heading;
        private Text readout;
        private string readoutEnglish;
        private string readoutChinese;
        private Button attackButton;
        private Button guardButton;
        private Button retryButton;
        private Button continueButton;
        private Button retreatButton;
        private Coroutine encounterRoutine;
        private Action<bool> onCompleted;
        private bool active;
        private float layoutAspect = -1f;

        public bool IsActive => active;
        public bool IsReady => party != null && party.IsReady;
        public bool BattleInProgress => party != null && party.BattleInProgress;
        public bool CanAttack => party != null && party.ReadyBaseAttackerIndex >= 0;
        public bool CanGuard => party != null && party.CanGuard(party.ChoosingRescuerIndex);
        public int ChoosingRescuerIndex => party != null ? party.ChoosingRescuerIndex : -1;
        public string InitializationError => party != null ? party.InitializationError : "ORK party is missing.";

        private void Awake()
        {
            party = GetComponent<OrkPartyBridge>();
        }

        private void OnEnable()
        {
            SoundwellUiLocalization.Changed += RefreshLocalizedPresentation;
        }

        private void OnDisable()
        {
            SoundwellUiLocalization.Changed -= RefreshLocalizedPresentation;
        }

        public bool Begin(Action<bool> completed)
        {
            if (active)
                return false;

            if (party == null)
                party = GetComponent<OrkPartyBridge>();
            if (party == null)
            {
                Debug.LogError("S00 bridge defence requires an OrkPartyBridge on the Bootstrap entrance.");
                return false;
            }

            onCompleted = completed;
            active = true;
            SoundwellInputAdapter.SetUiContext();
            BuildHud();
            if (!party.enabled)
                party.enabled = true;
            encounterRoutine = StartCoroutine(StartEncounter());
            return true;
        }

        // ORK may fail initialization before the normal deadline (e.g. a
        // missing saved asset). Once it reports an error, waiting 35 seconds
        // only traps the player on an unusable combat screen. No local ORK
        // readiness or victory is manufactured by this UI decision.
        private static bool ShouldWaitForPartyInitialization(
            bool encounterActive, bool partyReady, string error,
            float now, float deadline) =>
            encounterActive && !partyReady && string.IsNullOrEmpty(error) &&
            now < deadline;

        private IEnumerator StartEncounter()
        {
            SetReadout("Preparing the ORK bridgehead party…", "正在准备 ORK 桥头队伍……");
            float readyDeadline = Time.realtimeSinceStartup + 35f;
            while (ShouldWaitForPartyInitialization(active, party.IsReady,
                       party.InitializationError, Time.realtimeSinceStartup,
                       readyDeadline))
                yield return null;

            if (!active)
                yield break;

            if (!party.IsReady)
            {
                string error = string.IsNullOrEmpty(party.InitializationError)
                    ? string.Empty : " " + party.InitializationError;
                ShowRetry("The ORK party did not initialize." + error,
                    "ORK 队伍未能初始化。" + error, false);
                yield break;
            }

            if (party.BattleStarted)
                party.ResetEncounter();

            if (!party.TryStartDirectEncounter())
            {
                ShowRetry("ORK could not start the bridgehead encounter. Retry when the battle system is ready.",
                    "ORK 无法启动桥头遭遇战。战斗系统就绪后可以重试。", true);
                yield break;
            }

            SetReadout("The guard squad is closing. Waiting for the first ORK turn…",
                "持矛守军正在逼近，等待 ORK 首个回合……");
            float battleDeadline = Time.realtimeSinceStartup + 12f;
            while (active && !party.BattleInProgress &&
                   !party.EncounterStartFailed && !party.EncounterWon &&
                   !party.EncounterFailed &&
                   Time.realtimeSinceStartup < battleDeadline)
                yield return null;

            if (!active)
                yield break;

            if (party.EncounterWon)
            {
                ShowVictory();
                yield break;
            }

            if (!party.BattleInProgress)
            {
                ShowRetry("The ORK encounter did not become active. No victory was recorded; retry the defence.",
                    "ORK 遭遇战未能进入活动状态。没有记录胜利；请重试防守。", true);
                yield break;
            }

            while (active)
            {
                if (party.EncounterWon)
                {
                    ShowVictory();
                    yield break;
                }

                if (party.EncounterFailed || party.EncounterStartFailed)
                {
                    ShowRetry("The guard squad still holds the span. Regroup and retry the ORK defence.",
                        "持矛守军仍控制着桥面。重新整队，再次进行 ORK 防守。", true);
                    yield break;
                }

                RefreshReadout();
                yield return null;
            }
        }

        public bool TryAttack()
        {
            if (!active || !BattleInProgress || party == null)
                return false;
            return party.TryAttackBeast();
        }

        public bool TryGuard()
        {
            if (!active || !BattleInProgress || party == null)
                return false;
            return party.TryGuard();
        }

        private void RefreshReadout()
        {
            if (party == null || !party.BattleInProgress)
                return;

            string snapshot = party.BuildBattleReadout();
            string beastLabel = SoundwellUiLocalization.Get("battle.beast");
            if (!string.IsNullOrEmpty(beastLabel))
                snapshot = snapshot.Replace(beastLabel, Copy("SPEAR GUARD SQUAD", "持矛守军小队"));
            readoutEnglish = null;
            readoutChinese = null;
            readout.text = snapshot;
            attackButton.interactable = CanAttack;
            guardButton.interactable = CanGuard;
            // A gamepad can remain focused on Attack while ORK only permits
            // Guard this turn. Restore a legal command without stealing the
            // player's intentional focus on Leave or another enabled button.
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                Button candidate = ChooseAvailableActionFocus(
                    eventSystem.currentSelectedGameObject, attackButton, guardButton,
                    retreatButton);
                if (candidate != null)
                    eventSystem.SetSelectedGameObject(candidate.gameObject);
            }
        }

        // Only repairs stale/disabled selection; an intentional valid choice
        // (including Leave) always wins over automatic focus restoration.
        // When ORK has no legal command between turns, leave the player a
        // real UGUI escape route rather than focus a disabled command.
        private static Button ChooseAvailableActionFocus(
            GameObject selected, Button attack, Button guard, Button retreat)
        {
            if (selected != null)
            {
                Selectable selectedControl = selected.GetComponent<Selectable>();
                if (selectedControl != null && selectedControl.IsActive() &&
                    selectedControl.IsInteractable())
                    return null;
            }

            if (attack != null && attack.IsActive() && attack.IsInteractable())
                return attack;
            if (guard != null && guard.IsActive() && guard.IsInteractable())
                return guard;
            if (retreat != null && retreat.IsActive() && retreat.IsInteractable())
                return retreat;
            return null;
        }

        // Prevent phantom ORK commands while the battle system is preparing,
        // between retries, or before its first legal chooser is available.
        // RefreshReadout enables each command only when ORK permits it.
        private static void DisableActionsUntilOrkReady(Button attack, Button guard)
        {
            if (attack != null)
                attack.interactable = false;
            if (guard != null)
                guard.interactable = false;
        }

        private void ShowRetry(string english, string chinese, bool canRetry)
        {
            SetReadout(english, chinese);
            attackButton.gameObject.SetActive(false);
            guardButton.gameObject.SetActive(false);
            retryButton.gameObject.SetActive(canRetry);
            continueButton.gameObject.SetActive(false);
            retreatButton.gameObject.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(
                canRetry ? retryButton.gameObject : retreatButton.gameObject);
        }

        private void ShowVictory()
        {
            SetReadout(
                "The ORK party has held the bridgehead. Continue to return to Shen Li and the strained cable.",
                "ORK 队伍守住了桥头。继续后返回沈砾身边，缆索仍然绷紧。");
            attackButton.gameObject.SetActive(false);
            guardButton.gameObject.SetActive(false);
            retryButton.gameObject.SetActive(false);
            continueButton.gameObject.SetActive(true);
            retreatButton.gameObject.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(continueButton.gameObject);
        }

        private void Retry()
        {
            if (!active || party == null || !party.IsReady)
                return;

            if (party.BattleStarted)
                party.ResetEncounter();
            attackButton.gameObject.SetActive(true);
            guardButton.gameObject.SetActive(true);
            DisableActionsUntilOrkReady(attackButton, guardButton);
            retryButton.gameObject.SetActive(false);
            continueButton.gameObject.SetActive(false);
            // Retry is hidden now: keep a selectable escape while ORK restarts.
            EventSystem.current?.SetSelectedGameObject(retreatButton.gameObject);
            encounterRoutine = StartCoroutine(StartEncounter());
        }

        private void Continue() => Complete(true);

        private void Retreat() => Complete(false);

        private void Complete(bool victory)
        {
            if (!active)
                return;
            // Continue is presentation, not a victory authority. A hidden,
            // stale or reflected UI callback cannot award the fixed third
            // bell unless ORK has actually ended this encounter in victory.
            if (victory && (party == null || !party.EncounterWon))
                return;

            Action<bool> callback = onCompleted;
            onCompleted = null;
            Stop();
            callback?.Invoke(victory);
        }

        public void Stop()
        {
            active = false;
            if (encounterRoutine != null)
            {
                StopCoroutine(encounterRoutine);
                encounterRoutine = null;
            }
            if (party != null && party.BattleStarted)
                party.ResetEncounter();
            if (hud != null)
            {
                Destroy(hud.gameObject);
                hud = null;
            }
            onCompleted = null;
        }

        private void BuildHud()
        {
            hud = new GameObject("S00 ORK bridgehead defence HUD",
                typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            hud.transform.SetParent(transform, false);
            hud.renderMode = RenderMode.ScreenSpaceOverlay;
            hud.sortingOrder = 100;
            var scaler = hud.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            scaler.matchWidthOrHeight = 0.5f;
            hud.gameObject.AddComponent<GraphicRaycaster>();

            var backdropObject = new GameObject("S00 bridgehead defence backdrop",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            backdropObject.transform.SetParent(hud.transform, false);
            var backdropRect = (RectTransform)backdropObject.transform;
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;
            var backdrop = backdropObject.GetComponent<Image>();
            backdrop.color = new Color(0.025f, 0.040f, 0.055f, 0.92f);
            backdrop.raycastTarget = false;

            heading = SoundwellView.AddLabel(hud.transform,
                Copy("S00 · WHITEWATER BRIDGEHEAD", "S00 · 白水渡桥头"), 26,
                Brass, new Vector2(0f, 190f), new Vector2(820f, 44f));
            heading.alignment = TextAnchor.MiddleCenter;
            readout = SoundwellView.AddLabel(hud.transform,
                Copy("Preparing the ORK bridgehead party…", "正在准备 ORK 桥头队伍……"),
                18, Ink, new Vector2(0f, 28f), new Vector2(820f, 250f));
            readout.alignment = TextAnchor.MiddleCenter;
            readout.horizontalOverflow = HorizontalWrapMode.Wrap;
            readout.verticalOverflow = VerticalWrapMode.Overflow;
            readout.resizeTextForBestFit = true;
            readout.resizeTextMinSize = 14;
            readout.resizeTextMaxSize = 18;

            attackButton = CreateButton("S00 ORK attack", Copy("Attack", "攻击"),
                new Vector2(-320f, -190f), () => TryAttack());
            guardButton = CreateButton("S00 ORK guard", Copy("Guard", "防御"),
                new Vector2(-160f, -190f), () => TryGuard());
            retryButton = CreateButton("S00 ORK retry", Copy("Retry", "重试"),
                new Vector2(0f, -190f), Retry);
            continueButton = CreateButton("S00 ORK continue", Copy("Continue", "继续"),
                new Vector2(0f, -190f), Continue);
            retreatButton = CreateButton("S00 ORK retreat", Copy("Leave", "离开"),
                new Vector2(320f, -190f), Retreat);
            retryButton.gameObject.SetActive(false);
            continueButton.gameObject.SetActive(false);
            DisableActionsUntilOrkReady(attackButton, guardButton);

            // Before the first ORK chooser, Attack and Guard cannot execute.
            // Do not focus a disabled action and trap gamepad users.
            EventSystem eventSystem = SoundwellInputAdapter.EnsureEventSystem(transform);
            eventSystem?.SetSelectedGameObject(retreatButton.gameObject);
            RefreshLocalizedPresentation();
            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height));
        }

        private void LateUpdate()
        {
            if (hud == null)
                return;
            // Keep the real overlay playable after a window resize or
            // orientation change; do not touch ORK turns or input focus.
            float aspect = (float)Mathf.Max(1, Screen.width) /
                Mathf.Max(1, Screen.height);
            if (Mathf.Abs(layoutAspect - aspect) > 0.01f)
                LayoutForViewportAspect(aspect);
        }

        private void LayoutForViewportAspect(float aspect)
        {
            if (hud == null)
                return;
            aspect = float.IsNaN(aspect) || float.IsInfinity(aspect) || aspect <= 0f
                ? 16f / 9f : aspect;
            layoutAspect = aspect;
            bool compact = aspect <= 1.35f;

            // A portrait window's logical Canvas width can be far smaller
            // than the 960px reference. Fixed +/-320 buttons were cropped,
            // including the only retreat option between ORK turns.
            PositionButton(attackButton, compact
                ? new Vector2(-84f, -160f) : new Vector2(-320f, -190f));
            PositionButton(guardButton, compact
                ? new Vector2(84f, -160f) : new Vector2(-160f, -190f));
            PositionButton(retryButton, compact
                ? new Vector2(0f, -160f) : new Vector2(0f, -190f));
            PositionButton(continueButton, compact
                ? new Vector2(0f, -160f) : new Vector2(0f, -190f));
            PositionButton(retreatButton, compact
                ? new Vector2(0f, -235f) : new Vector2(320f, -190f));

            // CanvasScaler's 0.5 MatchWidthOrHeight is the geometric
            // mean: logical width = sqrt(referenceWidth * referenceHeight
            // * viewportAspect). Preserve a margin on narrow screens.
            float width = Mathf.Min(820f, Mathf.Max(240f,
                Mathf.Sqrt(960f * 540f * aspect) - 40f));
            if (heading != null)
                heading.rectTransform.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Horizontal, width);
            if (readout != null)
                readout.rectTransform.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Horizontal, width);
        }

        private static void PositionButton(Button button, Vector2 position)
        {
            if (button != null)
                ((RectTransform)button.transform).anchoredPosition = position;
        }

        private Button CreateButton(string objectName, string caption,
            Vector2 position, Action action)
        {
            var buttonObject = new GameObject(objectName,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(hud.transform, false);
            var rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(148f, 52f);
            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.15f, 0.24f, 0.30f);
            var button = buttonObject.GetComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.96f, 0.88f, 0.66f);
            colors.selectedColor = new Color(0.96f, 0.82f, 0.49f);
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            button.onClick.AddListener(() => action());
            Text label = SoundwellView.AddLabel(rect, caption, 18,
                Color.white, Vector2.zero, new Vector2(140f, 46f));
            label.alignment = TextAnchor.MiddleCenter;
            label.font = SoundwellUiLocalization.PresentationFont(label.font);
            buttonObject.AddComponent<SoundwellActionMenuCancelHandler>()
                .Initialize(Retreat);
            return button;
        }

        // S00 uses the same live language as the entry screen and trial UI.
        // A locale switch must not leave stale battle captions or unreadable
        // Chinese glyphs while ORK still owns turns, HP and victory.
        private void RefreshLocalizedPresentation()
        {
            if (hud == null)
                return;
            Font font = SoundwellUiLocalization.PresentationFont(
                Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            if (heading != null)
            {
                heading.text = Copy("S00 · WHITEWATER BRIDGEHEAD", "S00 · 白水渡桥头");
                heading.font = font;
            }
            RefreshButtonCaption(attackButton, "Attack", "攻击", font);
            RefreshButtonCaption(guardButton, "Guard", "防御", font);
            RefreshButtonCaption(retryButton, "Retry", "重试", font);
            RefreshButtonCaption(continueButton, "Continue", "继续", font);
            RefreshButtonCaption(retreatButton, "Leave", "离开", font);
            if (readout != null)
            {
                readout.font = font;
                if (readoutEnglish != null && readoutChinese != null)
                    readout.text = Copy(readoutEnglish, readoutChinese);
                else if (BattleInProgress)
                    RefreshReadout();
            }
        }

        private static void RefreshButtonCaption(
            Button button, string english, string chinese, Font font)
        {
            if (button == null)
                return;
            Text label = button.GetComponentInChildren<Text>(true);
            if (label == null)
                return;
            label.font = font;
            label.text = Copy(english, chinese);
        }

        private void SetReadout(string english, string chinese)
        {
            readoutEnglish = english;
            readoutChinese = chinese;
            if (readout != null)
                readout.text = Copy(english, chinese);
        }

        private static string Copy(string english, string chinese) =>
            SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese
                ? chinese : english;

        private void OnDestroy()
        {
            Stop();
        }
    }
}
