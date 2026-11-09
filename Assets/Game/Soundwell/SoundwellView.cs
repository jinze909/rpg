using Action = System.Action;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    // Scene-local presentation for the explicitly labelled placeholder trial.
    internal sealed class SoundwellView
    {
        private static readonly Color Ink = new Color(0.79f, 0.87f, 0.90f);
        private static readonly Color Gold = new Color(1f, 0.72f, 0.33f);
        private static readonly Color Cloth = new Color(0.45f, 0.73f, 0.83f);
        private readonly Camera camera;
        private readonly Sprite square;
        private readonly Material ringMaterial;
        private readonly SpriteRenderer bellMarker;
        private readonly SpriteRenderer beastIntentMarker;
        private readonly SpriteRenderer gateMarker;
        private readonly SpriteRenderer upperBoardMarker;
        private readonly SpriteRenderer lowerBoardMarker;
        private readonly Transform surveyorFigure;
        private readonly SpriteRenderer falseTrailMarker;
        private readonly SpriteRenderer safeTrailMarker;
        private readonly SpriteRenderer escapeDangerMarker;
        private readonly SpriteRenderer ropeEvidenceMarker;
        private readonly SpriteRenderer cartEvidenceMarker;
        private readonly SpriteRenderer cartMapKitMarker;
        private readonly SpriteRenderer clawEvidenceMarker;
        private readonly Transform escortLeader;
        private readonly Transform retreatMarker;
        private readonly BoxCollider2D gateBlocker;
        private readonly LineRenderer pulse;
        private readonly LineRenderer escortLine;
        private readonly AudioSource audio;
        private readonly AudioClip clearBell;
        private readonly AudioClip mutedBell;
        private readonly Text hint;
        private readonly Text beastStatus;
        private readonly Text gateStatus;
        private readonly Text escapeStatus;
        private readonly Text surveyorStatus;
        private readonly Button ringButton;
        private readonly Button wrapButton;
        private readonly Button retryButton;
        private readonly Button backButton;
        private readonly Button carryButton;
        private readonly Button gateButton;
        private readonly Text gateButtonLabel;
        private readonly Text carryButtonLabel;
        private readonly Text wrapButtonLabel;
        private readonly Text menuStatus;
        private readonly EventSystem actionEventSystem;
        private readonly bool previousNavigationEvents;
        private bool actionMenuOpen;
        private bool waitingForEscortLeader;
        private bool layoutReady;
        private bool compactHud;
        private float pulseStarted = float.NegativeInfinity;
        private bool mutedPulse;

        public SoundwellView(Transform root, Camera camera, Transform player, Transform bell,
            Transform beast, Transform safe, Action ring, Action wrap, Action retry, Action back,
            Action carry, Action gate)
        {
            this.camera = camera;
            escortLeader = player;
            retreatMarker = safe;
            square = Sprite.Create(Texture2D.whiteTexture,
                new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                new Vector2(0.5f, 0.5f), Texture2D.whiteTexture.width);
            ringMaterial = new Material(Shader.Find("Sprites/Default"));
            camera.backgroundColor = new Color(0.055f, 0.086f, 0.115f);
            FitCamera();

            Plate(root, "Trial floor", Vector2.zero, new Vector2(14f, 6.4f), new Color(0.11f, 0.19f, 0.23f), -4);
            Plate(root, "Beast lane", new Vector2(3.7f, 0f), new Vector2(6.55f, 6.4f), new Color(0.22f, 0.16f, 0.18f), -3);
            Plate(safe, "Retreat mark", Vector2.zero, new Vector2(1.75f, 1.75f), new Color(0.16f, 0.38f, 0.36f), -1);
            Plate(root, "PLACEHOLDER entrance bell stand", bell.position,
                new Vector2(0.8f, 0.35f), new Color(0.24f, 0.43f, 0.48f), -1);
            Plate(root, "PLACEHOLDER side bell stand", SoundwellTrial.SideBellStand,
                new Vector2(0.8f, 0.35f), new Color(0.24f, 0.43f, 0.48f), -1);
            // Two optional pieces of physical evidence at the entrance.
            // Inspection changes their visual status but never gates ringing
            // or grants an automatic solution to the sound lure.
            ropeEvidenceMarker = Plate(root, "PLACEHOLDER severed bell rope",
                SoundwellTrial.RopeEvidencePosition, new Vector2(0.22f, 0.78f),
                new Color(0.53f, 0.38f, 0.27f), 2);
            // The cart anchors the missing-person clue without revealing
            // the sound-lure solution. It is a distinct physical prop.
            cartEvidenceMarker = Plate(root, "PLACEHOLDER abandoned mule cart",
                SoundwellTrial.CartEvidencePosition, new Vector2(1.02f, 0.46f),
                new Color(0.49f, 0.35f, 0.24f), 2);
            // A recoverable personal map kit: the surveyor must actually
            // return to this cart after rescue to retrieve it.
            cartMapKitMarker = Plate(root, "PLACEHOLDER cart map kit",
                SoundwellTrial.CartEvidencePosition + new Vector2(0f, 0.22f),
                new Vector2(0.31f, 0.26f), Gold, 4);
            Plate(root, "PLACEHOLDER cart left wheel",
                SoundwellTrial.CartEvidencePosition + new Vector2(-0.34f, -0.30f),
                new Vector2(0.23f, 0.23f), Ink, 3);
            Plate(root, "PLACEHOLDER cart right wheel",
                SoundwellTrial.CartEvidencePosition + new Vector2(0.34f, -0.30f),
                new Vector2(0.23f, 0.23f), Ink, 3);
            clawEvidenceMarker = Plate(root, "PLACEHOLDER claw marks",
                SoundwellTrial.ClawEvidencePosition, new Vector2(0.86f, 0.24f),
                new Color(0.56f, 0.29f, 0.27f), 2);
            clawEvidenceMarker.transform.rotation = Quaternion.Euler(0f, 0f, -24f);
            // Separate two solid rails from the controlled middle opening.
            Wall(root, "Safety grating upper", new Vector2(0.25f, 2.0f), new Vector2(0.32f, 2.4f));
            Wall(root, "Safety grating lower", new Vector2(0.25f, -2.0f), new Vector2(0.32f, 2.4f));
            gateMarker = Wall(root, "PLACEHOLDER soundwell gate", new Vector2(0.25f, 0f),
                new Vector2(0.32f, 1.6f));
            gateBlocker = gateMarker.GetComponent<BoxCollider2D>();
            // This warning is anchored at the same physical crossing that the
            // rescued surveyor must use; it follows the beast's live position.
            escapeDangerMarker = Plate(root, "PLACEHOLDER escape-lane risk",
                SoundwellTrial.EscapeLanePosition, new Vector2(0.78f, 0.78f),
                new Color(0.90f, 0.30f, 0.25f, 0.35f), 0);
            escapeDangerMarker.enabled = false;
            Plate(root, "PLACEHOLDER gate control", new Vector2(-0.55f, 0f),
                new Vector2(0.32f, 0.58f), Gold, 1);
            // This wrong-way trail sign is a physical consequence of the
            // surveyor's mistaken directions. He removes it after rescue.
            falseTrailMarker = Plate(root, "PLACEHOLDER false trail marker",
                (Vector2)safe.position + new Vector2(1.10f, -0.40f),
                new Vector2(0.30f, 0.66f), new Color(0.94f, 0.43f, 0.29f), 2);
            falseTrailMarker.transform.rotation = Quaternion.Euler(0f, 0f, -24f);
            // The new sign occupies the same physical site but remains absent
            // until the surveyor returns with his recovered map kit.
            safeTrailMarker = Plate(root, "PLACEHOLDER verified return route marker",
                (Vector2)safe.position + new Vector2(1.10f, -0.40f),
                new Vector2(0.32f, 0.72f), new Color(0.38f, 0.90f, 0.72f), 2);
            safeTrailMarker.transform.rotation = Quaternion.Euler(0f, 0f, 24f);
            safeTrailMarker.enabled = false;
            // The surveyor is a scene character, not a global quest popup.
            // The plank is visible before dialogue; only his firsthand
            // warning marks which part can take the load.
            surveyorFigure = new GameObject("Surveyor figure").transform;
            surveyorFigure.SetParent(root, false);
            surveyorFigure.position = SoundwellTrial.SurveyorPosition;
            Plate(surveyorFigure, "PLACEHOLDER trapped surveyor", Vector2.zero,
                new Vector2(0.58f, 0.74f), Ink, 3);
            Plate(surveyorFigure, "Surveyor face", new Vector2(0f, 0.14f),
                new Vector2(0.25f, 0.15f), Gold, 4);
            upperBoardMarker = Plate(root, "PLACEHOLDER upper load-bearing plank",
                SoundwellTrial.UpperBoardPosition, new Vector2(1.65f, 0.3f),
                new Color(0.30f, 0.37f, 0.40f), 1);
            // A visible weak lower route and its collapsed state make the
            // surveyor's warning consequential without a hidden timer.
            lowerBoardMarker = Plate(root, "PLACEHOLDER unsafe lower plank",
                SoundwellTrial.LowerBoardPosition, new Vector2(1.65f, 0.34f),
                new Color(0.67f, 0.39f, 0.27f), 1);
            Wall(root, "West wall", new Vector2(-7f, 0f), new Vector2(0.32f, 6.4f));
            Wall(root, "North wall", new Vector2(-3.4f, 3.2f), new Vector2(7.3f, 0.28f));
            Wall(root, "South wall", new Vector2(-3.4f, -3.2f), new Vector2(7.3f, 0.28f));

            Plate(player, "PLACEHOLDER player silhouette", Vector2.zero, new Vector2(0.64f, 0.64f), new Color(0.43f, 0.91f, 0.72f), 3);
            Plate(player, "Player face", new Vector2(0f, 0.12f), new Vector2(0.26f, 0.16f), Color.white, 4);
            bellMarker = Plate(bell, "PLACEHOLDER copper bell", Vector2.zero, new Vector2(0.57f, 0.57f), Gold, 2);
            bellMarker.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            // A small diamond marks the beast's current physical destination.
            // Gold tracks the audible signal; blue tracks the last-heard search.
            // No simulated sound waves, pathfinding state or second AI target.
            beastIntentMarker = Plate(root, "PLACEHOLDER beast intent marker",
                Vector2.zero, new Vector2(0.46f, 0.46f), Gold, 1);
            beastIntentMarker.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            beastIntentMarker.enabled = false;
            Plate(beast, "PLACEHOLDER beast", Vector2.zero, new Vector2(1.03f, 0.78f), new Color(0.95f, 0.41f, 0.42f), 3);
            Plate(beast, "Beast muzzle", new Vector2(0.56f, 0f), new Vector2(0.35f, 0.3f), new Color(1f, 0.67f, 0.49f), 4);

            var pulseObject = new GameObject("Bell pressure ripple");
            pulseObject.transform.SetParent(bell, false);
            pulse = pulseObject.AddComponent<LineRenderer>();
            pulse.sharedMaterial = ringMaterial;
            pulse.useWorldSpace = false;
            pulse.loop = true;
            pulse.positionCount = 40;
            pulse.sortingOrder = 1;
            pulse.widthMultiplier = 0.06f;

            var guideObject = new GameObject("PLACEHOLDER surveyor guide rope");
            guideObject.transform.SetParent(root, false);
            escortLine = guideObject.AddComponent<LineRenderer>();
            escortLine.sharedMaterial = ringMaterial;
            escortLine.useWorldSpace = true;
            escortLine.positionCount = 2;
            escortLine.sortingOrder = 4;
            escortLine.widthMultiplier = 0.055f;
            escortLine.startColor = escortLine.endColor = Cloth;
            escortLine.enabled = false;

            audio = bell.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            clearBell = BellClip("Copper bell", 640f, 0.75f, 0.22f);
            mutedBell = BellClip("Wrapped bell", 230f, 0.20f, 0.10f);

            var canvas = new GameObject("PLACEHOLDER trial UI", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.transform.SetParent(root, false);
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvas.sortingOrder = 12;
            var rect = (RectTransform)canvas.transform;
            rect.sizeDelta = new Vector2(840, 480);
            rect.localScale = Vector3.one * 0.02f;
            AddLabel(rect, "SOUNDWELL  /  SIGNAL TRIAL", 25, Ink, new Vector2(0, 224), new Vector2(700, 40));
            AddLabel(rect, "PLACEHOLDER   /   Entrance and observation lane", 14, Cloth,
                new Vector2(0, 194), new Vector2(650, 22));
            AddLabel(rect, "RETREAT", 13, Cloth, new Vector2(-278, -68), new Vector2(120, 24));
            AddLabel(rect, "COPPER BELL", 13, Gold, new Vector2(-88, -43), new Vector2(120, 22));
            AddLabel(rect, "SIDE BELL STAND", 13, Cloth, new Vector2(-92, -122), new Vector2(180, 24));
            beastStatus = AddLabel(rect, "", 17, Ink, new Vector2(170, -100), new Vector2(290, 30));
            gateStatus = AddLabel(rect, "GATE  /  LOCKED", 16, Ink,
                new Vector2(0, -102), new Vector2(220, 30));
            escapeStatus = AddLabel(rect, "ESCAPE LANE  /  CLEAR", 13, Cloth,
                new Vector2(-260, -104), new Vector2(240, 28));
            surveyorStatus = AddLabel(rect, "", 15, Cloth,
                new Vector2(0, -148), new Vector2(760, 32));
            hint = AddLabel(rect, "", 18, Ink, new Vector2(0, -194), new Vector2(790, 34));
            AddLabel(rect, "[WASD / Arrows / Stick] Move    [Esc / Pad B] Back to entry", 14, Cloth,
                new Vector2(0, -222), new Vector2(760, 26));

            // Pointer-only HUD: keyboard/controller buttons already route through the
            // one SoundwellControlFrame input adapter. Prevent duplicate gamepad Submit.
            var actionCanvas = new GameObject("Soundwell actions HUD", typeof(RectTransform),
                typeof(Canvas)).GetComponent<Canvas>();
            actionCanvas.transform.SetParent(root, false);
            actionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            actionCanvas.sortingOrder = 20;
            var scaler = actionCanvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            // Balance width and height scaling so a portrait viewport does not
            // shrink every action target to a tiny fraction of the reference UI.
            scaler.matchWidthOrHeight = 0.5f;
            actionCanvas.gameObject.AddComponent<GraphicRaycaster>();
            ringButton = CreateActionButton(actionCanvas.transform, "Ring", "RING  [E / A]", 0, ring);
            wrapButton = CreateActionButton(actionCanvas.transform, "Wrap", "WRAP  [Q / X]", 1, wrap);
            wrapButtonLabel = wrapButton.GetComponentInChildren<Text>();
            retryButton = CreateActionButton(actionCanvas.transform, "Retry", "RESET  [R / Y]", 2, retry);
            backButton = CreateActionButton(actionCanvas.transform, "Back", "BACK  [Esc / B]", 3, back);
            carryButton = CreateActionButton(actionCanvas.transform, "Carry", "LIFT  [F / LB]", 4, carry);
            carryButtonLabel = carryButton.GetComponentInChildren<Text>();
            gateButton = CreateActionButton(actionCanvas.transform, "Gate", "OPEN GATE [G / RB]", 5, gate);
            gateButtonLabel = gateButton.GetComponentInChildren<Text>();
            menuStatus = AddLabel(actionCanvas.transform, "TAB / START: FOCUS ACTIONS",
                14, Cloth, new Vector2(-270f, 220f), new Vector2(360f, 32f));
            menuStatus.resizeTextForBestFit = true;
            menuStatus.resizeTextMinSize = 11;
            menuStatus.resizeTextMaxSize = 14;

            if (EventSystem.current == null)
            {
                var events = new GameObject("Soundwell actions EventSystem",
                    typeof(EventSystem), typeof(StandaloneInputModule));
                events.transform.SetParent(root, false);
            }

            actionEventSystem = EventSystem.current;
            if (actionEventSystem == null)
                throw new System.InvalidOperationException("Soundwell action HUD has no EventSystem.");
            previousNavigationEvents = actionEventSystem.sendNavigationEvents;
            actionEventSystem.sendNavigationEvents = false;
        }

        // Explicit focus mode: the same stick/keys either control the player OR UGUI,
        // never both. Outside this mode only pointer clicks reach the action buttons.
        public void SetActionMenuOpen(bool open)
        {
            actionMenuOpen = open;
            foreach (var button in new[] { ringButton, wrapButton, retryButton, backButton,
                carryButton, gateButton })
                button.navigation = new Navigation
                {
                    mode = open ? Navigation.Mode.Automatic : Navigation.Mode.None
                };

            actionEventSystem.sendNavigationEvents = open;
            menuStatus.text = open
                ? "ACTION FOCUS  /  MOVE: SELECT  /  ENTER OR A: USE  /  ESC OR B: RESUME"
                : "TAB / START: FOCUS ACTIONS";
            if (open)
                EnsureActionSelection();
            else if (IsActionSelection(actionEventSystem.currentSelectedGameObject))
                actionEventSystem.SetSelectedGameObject(null);
        }

        private void ArrangeActionHud(float viewportAspect)
        {
            // Below landscape width, use two bottom rows instead of a tall
            // right-hand stack. This leaves the trial and its world-space hints
            // clear while keeping the action targets inside the visible viewport.
            bool compact = viewportAspect <= 1.25f;
            if (layoutReady && compactHud == compact)
                return;
            layoutReady = true;
            compactHud = compact;

            var buttons = new[] { ringButton, wrapButton, retryButton, backButton,
                carryButton, gateButton };
            for (int i = 0; i < buttons.Length; i++)
            {
                var rect = (RectTransform)buttons[i].transform;
                if (compact)
                {
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
                    rect.pivot = new Vector2(0.5f, 0f);
                    rect.anchoredPosition = new Vector2(i % 2 == 0 ? -105f : 105f,
                        i < 2 ? 138f : i < 4 ? 80f : 22f);
                    rect.sizeDelta = new Vector2(190f, 46f);
                }
                else
                {
                    rect.anchorMin = rect.anchorMax = Vector2.one;
                    rect.pivot = Vector2.one;
                    rect.anchoredPosition = new Vector2(-16f, -26f - i * 52f);
                    rect.sizeDelta = new Vector2(198f, 44f);
                }
            }

            var statusRect = menuStatus.rectTransform;
            if (compact)
            {
                statusRect.anchorMin = statusRect.anchorMax = new Vector2(0.5f, 0f);
                statusRect.pivot = new Vector2(0.5f, 0f);
                statusRect.anchoredPosition = new Vector2(0f, 202f);
                statusRect.sizeDelta = new Vector2(435f, 36f);
            }
            else
            {
                statusRect.anchorMin = statusRect.anchorMax = new Vector2(0f, 1f);
                statusRect.pivot = new Vector2(0f, 1f);
                statusRect.anchoredPosition = new Vector2(20f, -15f);
                statusRect.sizeDelta = new Vector2(440f, 34f);
            }
        }

        private bool IsActionSelection(GameObject selected) =>
            selected == ringButton.gameObject || selected == wrapButton.gameObject ||
            selected == retryButton.gameObject || selected == backButton.gameObject ||
            selected == carryButton.gameObject || selected == gateButton.gameObject;

        private void EnsureActionSelection()
        {
            var selected = actionEventSystem.currentSelectedGameObject;
            var button = selected != null ? selected.GetComponent<Button>() : null;
            if (IsActionSelection(selected) && button != null && button.IsActive() && button.interactable)
                return;

            var target = ringButton.interactable ? ringButton :
                wrapButton.interactable ? wrapButton :
                carryButton.interactable ? carryButton :
                gateButton.interactable ? gateButton :
                retryButton.interactable ? retryButton : backButton;
            actionEventSystem.SetSelectedGameObject(target.gameObject);
        }

        private static Button CreateActionButton(Transform parent, string name, string caption, int index, Action action)
        {
            var obj = new GameObject("Soundwell " + name + " button",
                typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-16f, -26f - index * 52f);
            rect.sizeDelta = new Vector2(198f, 44f);
            var image = obj.GetComponent<Image>();
            image.color = new Color(0.14f, 0.23f, 0.28f, 0.94f);
            var button = obj.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.90f, 0.69f);
            colors.pressedColor = Gold;
            colors.disabledColor = new Color(0.50f, 0.55f, 0.57f, 0.50f);
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => action());
            AddLabel(rect, caption, 15, Ink, Vector2.zero, new Vector2(190f, 38f));
            return button;
        }

        public Vector2 SurveyorWorldPosition => surveyorFigure.position;
        public bool WaitingForEscortLeader => waitingForEscortLeader;
        public Vector2 FalseMarkerWorldPosition => falseTrailMarker.transform.position;

        public void ResetSurveyor()
        {
            surveyorFigure.position = SoundwellTrial.SurveyorPosition;
            waitingForEscortLeader = false;
            escortLine.enabled = false;
            falseTrailMarker.enabled = true;
            safeTrailMarker.enabled = false;
        }

        public void Refresh(bool ringing, bool searching, bool returning, bool wrapped,
            bool atBell, bool atSafePoint, bool carrying, bool canPlaceBell,
            bool gateOpen, bool atGatePanel, bool canOpenGate, bool atSurveyor,
            bool hasSurveyorClue, bool atUpperPlank, bool canSecurePlank,
            bool plankSecured, bool canEscortSurveyor, bool surveyorEscorting,
            bool rescueComplete, bool falseMarkerCleared, bool safeRouteMarked,
            bool atRopeEvidence, bool atCartEvidence, bool atClawEvidence, bool canInspectRope,
            bool canRecoverRope, bool canInspectCart, bool canInspectClaws, bool canTieEscortLine,
            bool canShareCartClue, bool ropeObserved, bool ropeRecovered, bool cartObserved,
            bool cartClueShared, bool cartKitReclaimed,
            bool clawsObserved, bool surveyorLineTied, bool beastBlocksEscape,
            bool lowerPlankBroken, Vector2 beastDestination)
        {
            FitCamera();
            beastIntentMarker.enabled = ringing || searching;
            if (beastIntentMarker.enabled)
            {
                beastIntentMarker.transform.position =
                    new Vector3(beastDestination.x, beastDestination.y, 0f);
                Color intentColor = ringing ? Gold : Cloth;
                intentColor.a = ringing ? 0.84f : 0.62f;
                beastIntentMarker.color = intentColor;
            }
            ArrangeActionHud(camera.aspect);
            escapeDangerMarker.enabled = beastBlocksEscape;
            string escapeCaption = beastBlocksEscape
                ? "ESCAPE LANE  /  BLOCKED" : "ESCAPE LANE  /  CLEAR";
            if (escapeStatus.text != escapeCaption)
                escapeStatus.text = escapeCaption;
            escapeStatus.color = beastBlocksEscape ? new Color(1f, 0.56f, 0.47f) : Cloth;
            falseTrailMarker.enabled = !falseMarkerCleared;
            safeTrailMarker.enabled = safeRouteMarked;
            ropeEvidenceMarker.enabled = !ropeRecovered;
            ropeEvidenceMarker.color = ropeObserved ? Cloth : new Color(0.53f, 0.38f, 0.27f);
            cartEvidenceMarker.color = cartObserved ? new Color(0.72f, 0.80f, 0.65f)
                : new Color(0.49f, 0.35f, 0.24f);
            cartMapKitMarker.enabled = !cartKitReclaimed;
            clawEvidenceMarker.color = clawsObserved ? Gold : new Color(0.56f, 0.29f, 0.27f);
            ringButton.interactable = atBell && !carrying;
            wrapButton.interactable = atBell && !carrying;
            retryButton.interactable = atSafePoint;
            backButton.interactable = true;
            carryButton.interactable = carrying ? canPlaceBell : atBell;
            gateButton.interactable = canInspectRope || canRecoverRope || canInspectCart || canInspectClaws ||
                canShareCartClue || canTieEscortLine || canOpenGate || canSecurePlank || canEscortSurveyor ||
                (atSurveyor && !hasSurveyorClue);
            string interactCaption = canInspectRope ? "INSPECT ROPE [G / RB]"
                : canRecoverRope ? "RECOVER CORD [G / RB]"
                : canInspectCart ? "INSPECT CART [G / RB]"
                : canShareCartClue ? "TELL ABOUT CART [G / RB]"
                : canInspectClaws ? "INSPECT CLAWS [G / RB]"
                : canTieEscortLine ? "TIE GUIDE LINE [G / RB]"
                : canEscortSurveyor ? "LEAD SURVEYOR [G / RB]"
                : canSecurePlank ? "BRACE PLANK [G / RB]"
                : atSurveyor && !hasSurveyorClue ? "ASK SURVEYOR [G / RB]"
                : "OPEN GATE [G / RB]";
            if (gateButtonLabel.text != interactCaption)
                gateButtonLabel.text = interactCaption;
            upperBoardMarker.color = plankSecured ? new Color(0.45f, 0.92f, 0.74f)
                : hasSurveyorClue ? Cloth : new Color(0.30f, 0.37f, 0.40f);
            lowerBoardMarker.color = lowerPlankBroken
                ? new Color(0.90f, 0.24f, 0.24f, 0.22f)
                : new Color(0.67f, 0.39f, 0.27f);
            Vector2 npc = surveyorFigure.position;
            Vector2 routeTarget;
            if (!surveyorEscorting || rescueComplete)
                waitingForEscortLeader = false;
            if (rescueComplete)
                routeTarget = !falseMarkerCleared ? FalseMarkerWorldPosition
                    : cartObserved && cartClueShared && !cartKitReclaimed
                        ? SoundwellTrial.CartEvidencePosition
                        : cartKitReclaimed && !safeRouteMarked
                            ? FalseMarkerWorldPosition
                            : (Vector2)retreatMarker.position;
            else if (surveyorEscorting)
            {
                // Cross the only real doorway instead of taking a straight
                // diagonal through the closed upper section of the grating.
                if (npc.x > 0.85f)
                {
                    // Bracing and asking to follow are not enough on their own:
                    // the survivor waits on the proven plank until the player
                    // physically leads through the gate. No hidden countdown.
                    routeTarget = beastBlocksEscape ? npc
                        : escortLeader.position.x <= 0.85f
                            ? new Vector2(0.85f, 0f)
                            : SoundwellTrial.UpperBoardPosition;
                }
                else if (npc.x > -0.65f)
                    routeTarget = beastBlocksEscape ? npc : new Vector2(-0.65f, 0f);
                else
                {
                    // The surveyor will not catch up from arbitrarily far away.
                    // A small hysteresis prevents toggling on every frame at
                    // the boundary; players must return and genuinely escort.
                    float separation = Vector2.Distance(npc, escortLeader.position);
                    if (waitingForEscortLeader)
                        waitingForEscortLeader = separation >
                            (surveyorLineTied ? 3.0f : 2.0f);
                    else
                        waitingForEscortLeader = separation >
                            (surveyorLineTied ? 4.7f : 3.15f);
                    routeTarget = waitingForEscortLeader ? npc
                        : (Vector2)escortLeader.position + new Vector2(0.45f, 0f);
                }
            }
            else
                routeTarget = plankSecured ? SoundwellTrial.UpperBoardPosition
                    : SoundwellTrial.SurveyorPosition;
            surveyorFigure.position = Vector3.MoveTowards(surveyorFigure.position,
                new Vector3(routeTarget.x, routeTarget.y, 0f), 2.25f * Time.deltaTime);
            escortLine.enabled = surveyorLineTied && surveyorEscorting && !rescueComplete;
            if (escortLine.enabled)
            {
                escortLine.SetPosition(0, escortLeader.position);
                escortLine.SetPosition(1, surveyorFigure.position);
            }
            string speech = falseMarkerCleared
                ? safeRouteMarked
                    ? "SURVEYOR: My map kit is safe. The verified route is posted where the false sign stood."
                    : cartKitReclaimed
                        ? "SURVEYOR: My map kit is safe. I'll mark the correct route by my old sign."
                    : cartObserved && cartClueShared
                        ? "SURVEYOR: I've taken down that false trail sign. You found my cart; I'll get my map kit."
                        : "SURVEYOR: My marker was wrong. I've taken down that false trail sign."
                : rescueComplete
                    ? "SURVEYOR: Safe at last. My marker was wrong; I'll take it down myself."
                    : surveyorEscorting
                        ? beastBlocksEscape && npc.x > -0.65f
                            ? "SURVEYOR: Beast at the gate! Draw it toward the SIDE BELL STAND."
                            : waitingForEscortLeader
                                ? "SURVEYOR: Too far ahead! Come back and lead me to RETREAT."
                            : escortLeader.position.x > 0.85f && npc.x > 0.85f
                                ? "SURVEYOR: Show me the way through the gate. I'll wait on this plank."
                                : "SURVEYOR: Keep the gate open. I'm right behind you."
                : plankSecured
                    ? "SURVEYOR: The upper plank holds. I can cross now—don't leave me here."
                    : hasSurveyorClue
                        ? "SURVEYOR: That upper board holds two. My marker misled you; I'll take it down."
                        : atSurveyor ? "SURVEYOR: Stop! The lower plank won't hold you." : "";
            if (lowerPlankBroken && !hasSurveyorClue && !surveyorEscorting && !rescueComplete)
                speech = "SURVEYOR: The lower plank snapped! Ask me about the safe upper route.";
            if (speech.Length == 0 && atCartEvidence && cartObserved)
                speech = "OBSERVED: The abandoned cart was dragged to the gate; its tracks lead inside.";
            else if (speech.Length == 0 && atRopeEvidence && ropeObserved)
                speech = "OBSERVED: The bell cord was cut. A rough knot remains.";
            else if (speech.Length == 0 && atClawEvidence && clawsObserved)
                speech = "OBSERVED: Claw marks turn toward the bell stand, not RETREAT.";
            if (surveyorStatus.text != speech)
                surveyorStatus.text = speech;
            gateBlocker.enabled = !gateOpen;
            gateMarker.color = gateOpen ? new Color(0.30f, 0.84f, 0.65f, 0.16f)
                : new Color(0.32f, 0.43f, 0.48f);
            string gateCaption = gateOpen ? "GATE  /  OPEN" : "GATE  /  LOCKED";
            if (gateStatus.text != gateCaption)
                gateStatus.text = gateCaption;
            gateStatus.color = gateOpen ? Cloth : Ink;
            string carryCaption = carrying ? "DROP  [F / LB]" : "LIFT  [F / LB]";
            if (carryButtonLabel.text != carryCaption)
                carryButtonLabel.text = carryCaption;
            if (actionMenuOpen)
                EnsureActionSelection();
            string wrapCaption = wrapped ? "UNWRAP  [Q / X]" : "WRAP  [Q / X]";
            if (wrapButtonLabel.text != wrapCaption)
                wrapButtonLabel.text = wrapCaption;
            bellMarker.color = wrapped ? Cloth : Gold;
            string status = ringing ? "BEAST  /  TRACKING" : searching ? "BEAST  /  SEARCHING"
                : returning ? "BEAST  /  RETURNING" : "BEAST  /  LISTENING";
            if (beastStatus.text != status)
            {
                beastStatus.text = status;
                beastStatus.color = ringing ? Gold : searching || returning ? Cloth : Ink;
            }

            string prompt;
            if (falseMarkerCleared)
                prompt = safeRouteMarked
                    ? "SAFE ROUTE POSTED: The surveyor replaced his false marker with a verified route."
                    : cartKitReclaimed
                        ? "MAP KIT RECOVERED: The surveyor is returning to post the safe route."
                    : cartObserved && !cartClueShared
                        ? canShareCartClue
                            ? "ROUTE CORRECTED: [G / RB] Tell the surveyor about his cart, or leave it for now."
                            : "ROUTE CORRECTED: You can tell the surveyor about his cart when nearby."
                        : cartObserved
                            ? "ROUTE CORRECTED: The surveyor is returning to the cart for his map kit."
                            : "ROUTE CORRECTED: The surveyor removed the false trail marker himself.";
            else if (rescueComplete)
                prompt = "RESCUED: The surveyor reached RETREAT. He'll remove his false trail marker.";
            else if (surveyorEscorting && beastBlocksEscape && npc.x > -0.65f)
                prompt = "ESCAPE BLOCKED: Lure the beast away from the gate with the SIDE BELL STAND.";
            else if (surveyorEscorting && waitingForEscortLeader)
                prompt = "ESCORT PAUSED: Return to the surveyor, then lead him toward RETREAT.";
            else if (carrying)
                prompt = canPlaceBell
                    ? "[F / LB] Place the bell onto this marked stand to set its sound source."
                    : "Carry the bell to the ENTRANCE or SIDE BELL STAND. No free drops.";
            else if (atRopeEvidence)
                prompt = canRecoverRope
                    ? "[G / RB] Recover the usable cord for a guide line."
                    : ropeRecovered ? "You recovered the usable cord for the surveyor."
                    : "[G / RB] Inspect the torn bell cord.";
            else if (atCartEvidence)
                prompt = cartObserved
                    ? "The cart tracks point toward the inner gate."
                    : "[G / RB] Inspect the abandoned mule cart.";
            else if (atClawEvidence)
                prompt = clawsObserved ? "The claw tracks are evidence. What drew the beast here?"
                    : "[G / RB] Inspect the beast's claw tracks.";
            else if (atUpperPlank)
                prompt = canTieEscortLine ? "[G / RB] Tie the recovered cord to guide the surveyor. Rescue not complete."
                    : canEscortSurveyor ? "[G / RB] Lead the surveyor through the gate. Rescue not complete."
                    : surveyorEscorting ? "Lead through the open gate first. The surveyor is waiting for you."
                    : plankSecured ? "Upper plank braced; surveyor can stand on it. Rescue not complete."
                    : canSecurePlank ? "[G / RB] Brace the upper plank for the surveyor."
                    : "Ask the surveyor which plank is safe before bracing anything.";
            else if (atSurveyor)
                prompt = hasSurveyorClue
                    ? "Upper plank marked. The surveyor is still trapped; brace it before he can cross."
                    : "[G / RB] Ask the surveyor which plank can carry someone.";
            else if (atGatePanel)
                prompt = surveyorEscorting ? "Escort the surveyor through the open gate to RETREAT."
                    : gateOpen ? "Gate open: cross into the soundwell inner room."
                    : canOpenGate ? "[G / RB] Open the gate while the beast follows the side bell."
                    : "Move the bell to the SIDE STAND and draw the beast away to open this gate.";
            else if (atBell)
                prompt = wrapped ? "[E / Pad A] Try the wrapped bell   [Q / Pad X] Remove the cloth"
                    : "[E / Pad A] Ring the bell   [Q / Pad X] Wrap with the wet cloth   [F / LB] Lift";
            else if (ringing || wrapped)
                prompt = atSafePoint ? "[R / Pad Y] Reset the experiment"
                    : surveyorEscorting ? "Lead the surveyor to RETREAT. Do not leave him behind."
                    : "Return to RETREAT to reset the experiment.";
            else
                prompt = "Walk to the copper bell. Watch the beast beyond the grate.";

            if (hint.text != prompt)
                hint.text = prompt;
            float age = Time.time - pulseStarted;
            pulse.enabled = ringing || age < (mutedPulse ? 0.33f : clearBell.length);
            if (!pulse.enabled)
                return;
            float progress = ringing ? Mathf.Repeat(age / clearBell.length, 1f) : Mathf.Clamp01(age / 0.33f);
            float radius = Mathf.Lerp(0.32f, mutedPulse ? 0.78f : 2.15f, progress);
            Color color = mutedPulse ? Cloth : Gold;
            color.a = 0.95f - progress * 0.68f;
            pulse.startColor = pulse.endColor = color;
            for (int i = 0; i < pulse.positionCount; i++)
            {
                float angle = i * (2f * Mathf.PI / pulse.positionCount);
                pulse.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius));
            }
        }

        private void FitCamera()
        {
            // Preserve the hall and its world-space labels as the window narrows.
            float aspect = camera.aspect;
            if (aspect <= 0f)
                return;

            float size = Mathf.Max(5.5f, 8.4f / aspect);
            if (!Mathf.Approximately(camera.orthographicSize, size))
                camera.orthographicSize = size;
        }

        public void PlayBell(bool muffled)
        {
            mutedPulse = muffled;
            pulseStarted = Time.time;
            audio.Stop();
            audio.clip = muffled ? mutedBell : clearBell;
            audio.loop = !muffled;
            audio.Play();
        }

        public void ResetPulse()
        {
            pulseStarted = float.NegativeInfinity;
            pulse.enabled = false;
            audio.Stop();
            audio.loop = false;
            audio.clip = null;
        }

        public void Dispose()
        {
            // Return the EventSystem to its pre-trial state if ORK keeps it alive
            // across the scene transition.
            if (actionEventSystem != null)
            {
                actionEventSystem.sendNavigationEvents = previousNavigationEvents;
                if (IsActionSelection(actionEventSystem.currentSelectedGameObject))
                    actionEventSystem.SetSelectedGameObject(null);
            }
            Object.Destroy(square);
            Object.Destroy(ringMaterial);
            Object.Destroy(clearBell);
            Object.Destroy(mutedBell);
        }

        internal static Text AddLabel(Transform parent, string content, int fontSize, Color color,
            Vector2 position, Vector2 size)
        {
            var label = new GameObject("Trial label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text))
                .GetComponent<Text>();
            label.transform.SetParent(parent, false);
            label.rectTransform.anchoredPosition = position;
            label.rectTransform.sizeDelta = size;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = color;
            label.raycastTarget = false;
            label.text = content;
            return label;
        }

        private SpriteRenderer Plate(Transform parent, string name, Vector2 position, Vector2 size, Color color, int order)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.sortingOrder = order;
            renderer.color = color;
            return renderer;
        }

        private SpriteRenderer Wall(Transform root, string name, Vector2 position, Vector2 size)
        {
            var wall = Plate(root, name, position, size, new Color(0.32f, 0.43f, 0.48f), 0);
            wall.gameObject.AddComponent<BoxCollider2D>().size = Vector2.one;
            return wall;
        }

        private static AudioClip BellClip(string name, float frequency, float duration, float volume)
        {
            const int rate = 22050;
            var samples = new float[Mathf.RoundToInt(rate * duration)];
            for (int i = 0; i < samples.Length; i++)
            {
                float time = (float)i / rate;
                float phase = time * frequency * Mathf.PI * 2f;
                samples[i] = (Mathf.Sin(phase) + 0.38f * Mathf.Sin(phase * 2.73f))
                    * Mathf.Min(time * 1600f, 1f) * Mathf.Exp(-5f * time / duration) * volume;
            }
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
