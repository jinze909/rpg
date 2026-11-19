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

        // All authored player-visible bridgehead copy lives here. Internal
        // ORK diagnostics stay in logs, never in HUD dialogue or titles.
        private const string PlayerCopyTitleEnglish = "WHITEWATER BRIDGEHEAD";
        private const string PlayerCopyTitleChinese = "白水渡桥头";
        private const string PlayerCopyPreparingEnglish = "The defenders are gathering at the bridge…";
        private const string PlayerCopyPreparingChinese = "守桥的人正在桥头集结……";
        private const string PlayerCopyUnavailableEnglish = "The defence could not begin. Return to the harbor entrance.";
        private const string PlayerCopyUnavailableChinese = "守桥行动未能开始。请返回港口入口。";
        private const string PlayerCopyStartFailureEnglish = "The defence could not start. Regroup and try again.";
        private const string PlayerCopyStartFailureChinese = "守桥行动未能开始。重新整队，再试一次。";
        private const string PlayerCopyFirstTurnEnglish = "Spearmen are closing in. Your defenders are taking position…";
        private const string PlayerCopyFirstTurnChinese = "持矛的人正在逼近，守桥的人正在各就各位……";
        private const string PlayerCopyInactiveEnglish = "The defence did not begin. Regroup and try again.";
        private const string PlayerCopyInactiveChinese = "桥头防守尚未开始。重新整队，再试一次。";
        private const string PlayerCopyDefeatEnglish = "The spearmen still hold the span. Regroup and try again.";
        private const string PlayerCopyDefeatChinese = "持矛的人仍占着桥面。重新整队，再试一次。";
        private const string PlayerCopyVictoryEnglish = "The bridgehead is held. Shen Li is still by the strained cable.";
        private const string PlayerCopyVictoryChinese = "桥头守住了。沈砾还守在绷紧的缆索旁。";

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
        // Only a displayed retry may start another ORK encounter. Consumed
        // synchronously to reject duplicate gamepad Submit/cached callbacks.
        private bool retryArmed;
        private float layoutAspect = -1f;
private sealed class StatusCard { public GameObject Root; public Outline Border; public Text Label; public RectTransform FillRect; public Image FillImage; public float BarWidth; }
private readonly System.Collections.Generic.List<StatusCard> allyCards = new System.Collections.Generic.List<StatusCard>();
        private StatusCard enemyCard;
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
            // A scene handoff can deactivate the Bootstrap root while the
            // ORK first-turn wait or chooser coroutine is still running.
            // Unity stops coroutines on deactivation but leaves our active
            // latch set, otherwise preventing a fresh bridgehead attempt.
            // Teardown must never report an unearned S00 victory callback.
            if (active)
                Stop();
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
            retryArmed = false;
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
            SetReadout(PlayerCopyPreparingEnglish, PlayerCopyPreparingChinese);
            float readyDeadline = Time.realtimeSinceStartup + 35f;
            while (ShouldWaitForPartyInitialization(active, party.IsReady,
                       party.InitializationError, Time.realtimeSinceStartup,
                       readyDeadline))
                yield return null;

            if (!active)
                yield break;

            if (!party.IsReady)
            {
                // The full framework diagnostic belongs in the developer log,
                // not on the player's story screen (including its localization).
                if (!string.IsNullOrEmpty(party.InitializationError))
                    Debug.LogError("S00 bridge defence initialization failed: " +
                        party.InitializationError);
                ShowRetry(PlayerCopyUnavailableEnglish,
                    PlayerCopyUnavailableChinese, false);
                yield break;
            }

            if (party.BattleStarted)
                party.ResetEncounter();

            if (!party.TryStartDirectEncounter())
            {
                ShowRetry(PlayerCopyStartFailureEnglish,
                    PlayerCopyStartFailureChinese, true);
                yield break;
            }

            SetReadout(PlayerCopyFirstTurnEnglish,
                PlayerCopyFirstTurnChinese);
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
                ShowRetry(PlayerCopyInactiveEnglish,
                    PlayerCopyInactiveChinese, true);
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
                    ShowRetry(PlayerCopyDefeatEnglish,
                        PlayerCopyDefeatChinese, true);
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
               { HideStatusCards();  return; }

            string snapshot = party.BuildBattleReadout(
                Copy("HOLD THE CROSSING", "守住渡口"),
                Copy("Defender", "守桥队员"),
                Copy("Spear squad", "持矛小队)", includeMoveBudget: false);
            readoutEnglish = null;
            readoutChinese = null;
            readout.text =FormatCompactReadout( snapshot);
                        RefreshStatusCards();
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
            retryArmed = canRetry;
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
            retryArmed = false;
            SetReadout(PlayerCopyVictoryEnglish,
                PlayerCopyVictoryChinese);
            attackButton.gameObject.SetActive(false);
            guardButton.gameObject.SetActive(false);
            retryButton.gameObject.SetActive(false);
            continueButton.gameObject.SetActive(true);
            retreatButton.gameObject.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(continueButton.gameObject);
        }

        // The real retry UGUI button can receive more than one Submit in a
        // frame. Consume the offered retry before resetting ORK or launching
        // any coroutine; later stale callbacks cannot restart the battle.
        private bool TryClaimRetry(bool partyReady)
        {
            if (!active || !partyReady || !retryArmed)
                return false;
            retryArmed = false;
            return true;
        }

        private void Retry()
        {
            if (party == null || !TryClaimRetry(party.IsReady))
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

        // A retiring ORK HUD must not leave a gamepad focus target or an
        // interactable button behind during Unity's deferred Destroy().
        // Never clear a selection owned by the entry screen or another menu.
        private static bool ShouldClearRetiredHudFocus(
            GameObject selected, Transform retiringHud) =>
            selected != null && retiringHud != null &&
            selected.transform.IsChildOf(retiringHud);

        public void Stop()
        {
            active = false;
            retryArmed = false;
            if (encounterRoutine != null)
            {
                StopCoroutine(encounterRoutine);
                encounterRoutine = null;
            }
            if (party != null && party.BattleStarted)
                party.ResetEncounter();
            if (hud != null)
            {
                Canvas retiringHud = hud;
                hud = null;
                EventSystem eventSystem = EventSystem.current;
                if (eventSystem != null &&
                    ShouldClearRetiredHudFocus(eventSystem.currentSelectedGameObject,
                        retiringHud.transform))
                    eventSystem.SetSelectedGameObject(null);
                // Destroy happens after the current frame. Deactivate now so
                // stale Attack/Guard/Continue can never receive another click.
                retiringHud.gameObject.SetActive(false);
                Destroy(retiringHud.gameObject);
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

            CreatePanel(hud.transform, "S00 battle top bar", new Vector2(0f, 234f), new Vector2(880f, 40f), new Color(0.06f, 0.08f, 0.11f, 0.90f), true);
            heading = SoundwellView.AddLabel(hud.transform,
                Copy(PlayerCopyTitleEnglish, PlayerCopyTitleChinese), 22,
                          Brass, new Vector2(0f, 234f), new Vector2(820f, 34f)));
                          heading.alignment = TextAnchor.MiddleCenter;
          CreatePanel(hud.transform, "S00 bridge defence readout bar", new Vector2(0f, 194f), new Vector2(856f, 32f), new Color(0.06f, 0.08f, 0.11f, 0.80f), false);
          readout = SoundwellView.AddLabel(hud.transform,
                Copy(PlayerCopyPreparingEnglish, PlayerCopyPreparingChinese),
                            14, Ink, new Vector2(0f, 194f), new Vector2(840f, 30f));
                            readout.gameObject.name = "S00 bridge defence readout";
            readout.alignment = TextAnchor.MiddleCenter;
            readout.horizontalOverflow = HorizontalWrapMode.Wrap;
            readout.verticalOverflow = VerticalWrapMode.Overflow;
            readout.resizeTextForBestFit = true;
            readout.resizeTextMinSize = 11; readout.resizeTextMaxSize = 14; allyCards.Clear(); for (int i = 0; i < 3; i++) allyCards.Add(CreateStatusCard(hud.transform, "S00 ally card " + i, new Vector2(-322f, 144f - i * 50f), new Vector2(236f, 42f))); enemyCard = CreateStatusCard(hud.transform, "S00 enemy card", new Vector2(322f, 144f), new Vector2(236f, 42f)); CreatePanel(hud.transform, "S00 battle command bar", new Vector2(0f, -214f), new Vector2(880f, 68f), new Color(0.06f, 0.08f, 0.11f, 0.92f), true);
            
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

            // The portrait menu needs a compact horizontal arrangement.
            // Ultrawide screens are the inverse problem: CanvasScaler makes
            // the logical height too short for fixed +/-190 UI positions,
            // clipping ORK commands and the only safe retreat route.
            Vector2 vertical = VerticalHudSlots(aspect);
            bool shallow = aspect > 2.55f;
            float actionY = vertical.y;
            PositionButton(attackButton, compact
                ? new Vector2(-84f, -160f) : new Vector2(-320f, actionY));
            PositionButton(guardButton, compact
                ? new Vector2(84f, -160f) : new Vector2(-160f, actionY));
            PositionButton(retryButton, compact
                ? new Vector2(0f, -160f) : new Vector2(0f, actionY));
            PositionButton(continueButton, compact
                ? new Vector2(0f, -160f) : new Vector2(0f, actionY));
            PositionButton(retreatButton, compact
                ? new Vector2(0f, -235f) : new Vector2(320f, actionY));

            // CanvasScaler's 0.5 MatchWidthOrHeight is the geometric
            // mean: logical width = sqrt(referenceWidth * referenceHeight
            // * viewportAspect). Preserve a margin on narrow screens.
            float width = Mathf.Min(820f, Mathf.Max(240f,
                Mathf.Sqrt(960f * 540f * aspect) - 40f));
            if (heading != null)
            {
                heading.rectTransform.anchoredPosition =
                    new Vector2(0f, vertical.x);
                heading.rectTransform.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Horizontal, width);
            }
            if (readout != null)
            {
                readout.rectTransform.anchoredPosition = new Vector2(0f,
                    shallow ? 0f : 28f);
                readout.rectTransform.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Horizontal, width);
                readout.rectTransform.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical,
                    shallow ? Mathf.Clamp(
                        Mathf.Sqrt(960f * 540f / aspect) - 170f, 100f, 250f)
                    : 250f);
            }
                    if (!compact && !shallow) { PositionButton(attackButton, new Vector2(-310f, -214f)); PositionButton(guardButton, new Vector2(-150f, -214f)); PositionButton(retryButton, new Vector2(0f, -214f)); PositionButton(continueButton, new Vector2(0f, -214f)); PositionButton(retreatButton, new Vector2(310f, -214f)); if (heading != null) heading.rectTransform.anchoredPosition = new Vector2(0f, 234f); } if (readout != null) { readout.rectTransform.anchoredPosition = new Vector2(0f, shallow ? vertical.x - 36f : 194f); readout.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Min(840f, width + 20f)); readout.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 30f); }
        }

        // Full-height 16:9 and portrait keep their existing HUD positions.
        // A 32:9/5:1 viewport instead uses its actual logical Canvas height:
        // the heading leaves 16px above its 44px rect and the 52px action
        // row leaves 12px below, even after a live resolution change.
        private static Vector2 VerticalHudSlots(float aspect)
        {
            float halfHeight = 0.5f * Mathf.Sqrt(960f * 540f / aspect);
            return new Vector2(Mathf.Min(190f, halfHeight - 38f),
                Mathf.Max(-190f, -halfHeight + 38f));
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
                heading.text = Copy(PlayerCopyTitleEnglish, PlayerCopyTitleChinese);
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

        private GameObject CreatePanel(string objectName, Vector2 position, Vector2 size,
            Color backgroundColor, Color borderColor)
        {
            var panel = new GameObject(objectName,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            panel.transform.SetParent(hud.transform, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = panel.GetComponent<Image>();
            image.color = backgroundColor;
            image.raycastTarget = false;
            var outline = panel.GetComponent<Outline>();
            outline.effectColor = borderColor;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return panel;
        }
        
        private StatusCard CreateStatusCard(string objectName, Vector2 position, bool isEnemy)
        {
            const float cardWidth = 236f;
            const float cardHeight = 42f;
            const float barWidth = 216f;
            Color border = isEnemy
                ? new Color(0.68f, 0.28f, 0.26f, 0.90f)
                : new Color(0.45f, 0.52f, 0.58f, 0.85f);
            GameObject root = CreatePanel(objectName, position, new Vector2(cardWidth, cardHeight),
                new Color(0.06f, 0.08f, 0.11f, 0.90f), border);

            Text label = SoundwellView.AddLabel(root.transform, string.Empty, 13,
                Ink, new Vector2(0f, 8f), new Vector2(barWidth, 20f));
            label.alignment = TextAnchor.MiddleLeft;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 10;
            label.resizeTextMaxSize = 13;

            var track = new GameObject(objectName + " hp track",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            track.transform.SetParent(root.transform, false);
            var trackRect = (RectTransform)track.transform;
            trackRect.anchorMin = trackRect.anchorMax = new Vector2(0.5f, 0.5f);
            trackRect.pivot = new Vector2(0f, 0.5f);
            trackRect.anchoredPosition = new Vector2(-barWidth * 0.5f, -11f);
            trackRect.sizeDelta = new Vector2(barWidth, 8f);
            var trackImage = track.GetComponent<Image>();
            trackImage.color = new Color(0.14f, 0.17f, 0.20f, 0.95f);
            trackImage.raycastTarget = false;

            var fill = new GameObject(objectName + " hp fill",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fill.transform.SetParent(track.transform, false);
            var fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = fillRect.anchorMax = new Vector2(0f, 0.5f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(barWidth, 8f);
            var fillImage = fill.GetComponent<Image>();
            fillImage.color = isEnemy
                ? new Color(0.76f, 0.25f, 0.23f, 1f)
                : new Color(0.32f, 0.72f, 0.44f, 1f);
            fillImage.raycastTarget = false;

            root.SetActive(false);
            return new StatusCard
            {
                Root = root,
                Border = root.GetComponent<Outline>(),
                Label = label,
                FillRect = fillRect,
                FillImage = fillImage,
                BarWidth = barWidth
            };
        }

        private static string FormatCompactReadout(string snapshot)
        {
            if (string.IsNullOrWhiteSpace(snapshot))
                return string.Empty;
            string[] lines = snapshot.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join("  |  ", lines);
        }

        private void HideStatusCards()
        {
            for (int i = 0; i < allyCards.Count; i++)
                if (allyCards[i]?.Root != null)
                    allyCards[i].Root.SetActive(false);
            if (enemyCard?.Root != null)
                enemyCard.Root.SetActive(false);
        }
        
        private void RefreshStatusCards()
        {
            if (party == null || !party.BattleInProgress)
            {
                HideStatusCards();
                return;
            }
            OrkPartyBridge.BattleUnitSnapshot[] snapshots = party.CaptureBattleSnapshots(
                Copy("Defender", "守桥队员"), Copy("Spear squad", "持矛小队"));
            int allyIndex = 0;
            bool enemyUpdated = false;
            for (int i = 0; i < snapshots.Length; i++)
            {
                OrkPartyBridge.BattleUnitSnapshot unit = snapshots[i];
                float ratio = unit.MaxHp > 0 ? Mathf.Clamp01((float)unit.Hp / unit.MaxHp) : 0f;
                if (unit.IsEnemy)
                {
                    if (enemyCard?.Root != null)
                    {
                        enemyCard.Root.SetActive(true);
                        enemyCard.Label.text = unit.Name + "  HP " + unit.Hp + "/" + unit.MaxHp;
                        enemyCard.FillRect.sizeDelta = new Vector2(enemyCard.BarWidth * ratio, 8f);
                        enemyUpdated = true;
                    }
                    continue;
                }
                if (allyIndex >= allyCards.Count)
                    continue;
                StatusCard card = allyCards[allyIndex++];
                if (card?.Root == null)
                    continue;
                card.Root.SetActive(true);
                string tag = unit.IsChoosing ? Copy(" [TURN]", " [行动]")
                    : unit.Defending ? Copy(" [GUARD]", " [防御]")
                    : string.Empty;
                card.Label.text = unit.Name + "  HP " + unit.Hp + "/" + unit.MaxHp + tag;
                card.FillRect.sizeDelta = new Vector2(card.BarWidth * ratio, 8f);
                card.FillImage.color = ratio > 0.35f
                    ? new Color(0.32f, 0.72f, 0.44f, 1f)
                    : new Color(0.86f, 0.58f, 0.22f, 1f);
                card.Border.effectColor = unit.IsChoosing
                    ? new Color(0.95f, 0.80f, 0.36f, 1f)
                    : new Color(0.45f, 0.52f, 0.58f, 0.85f);
                card.Border.effectDistance = unit.IsChoosing
                    ? new Vector2(2.5f, -2.5f)
                    : new Vector2(1.5f, -1.5f);
            }
            for (int i = allyIndex; i < allyCards.Count; i++)
                if (allyCards[i]?.Root != null)
                    allyCards[i].Root.SetActive(false);
            if (!enemyUpdated && enemyCard?.Root != null)
                enemyCard.Root.SetActive(false);
        }
    }
}
