using Action = System.Action;
using System.Collections.Generic;
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
        private readonly Sprite leaderPixelSprite;
        private readonly Sprite leaderWalkPixelSprite;
        private readonly SpriteRenderer leaderVisualRenderer;
        private readonly Sprite beastPixelSprite;
        private readonly Sprite surveyorPixelSprite;
        private readonly Sprite surveyorWalkPixelSprite;
        private readonly Sprite bellStandPixelSprite;
        private readonly Sprite attackTargetPixelSprite;
        private readonly Sprite activeRescuerPixelSprite;
        private readonly SpriteRenderer surveyorPixelRenderer;
        private readonly SpriteRenderer beastVisualRenderer;
        private readonly Texture2D leaderPixelTexture;
        private readonly Texture2D leaderWalkPixelTexture;
        private readonly Texture2D beastPixelTexture;
        private readonly Texture2D surveyorPixelTexture;
        private readonly Texture2D surveyorWalkPixelTexture;
        private readonly Texture2D bellStandPixelTexture;
        private readonly Texture2D attackTargetPixelTexture;
        private readonly Texture2D activeRescuerPixelTexture;
        private readonly SoundwellPlayerVisualAnimation playerAnimation;
        private readonly Material ringMaterial;
        private readonly SpriteRenderer bellMarker;
        private readonly SpriteRenderer beastIntentMarker;
        private readonly SpriteRenderer attackTargetMarker;
        private readonly SpriteRenderer activeRescuerMarker;
        private readonly Transform beastActor;
        private readonly SpriteRenderer searchTimerTrack;
        private readonly SpriteRenderer searchTimerFill;
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
        private readonly SpriteRenderer[] revealedClawTrail;
        private readonly Transform escortLeader;
        private readonly Transform retreatMarker;
        private readonly BoxCollider2D gateBlocker;
        private readonly LineRenderer pulse;
        private readonly LineRenderer beastContactRing;
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
        private readonly Button attackButton;
        private readonly Button guardButton;
        private readonly Button openGateButton;
        private readonly Text openGateLabel;
        private readonly Text attackButtonLabel;
        // Default Submit follows the next legal ORK action, not an accidental retreat.
        // Explicitly choosing Retreat remains respected during an unchanged turn.
        private bool focusAttackWhenReady;
        private readonly Button carryButton;
        private readonly Button gateButton;
        private readonly Text gateButtonLabel;
        private readonly Text carryButtonLabel;
        private readonly Text wrapButtonLabel;
        private readonly Text menuStatus;
        private readonly Dictionary<Text, string> localizedLabels = new Dictionary<Text, string>();
        private readonly Button languageButton;
        private readonly EventSystem actionEventSystem;
        private readonly bool previousNavigationEvents;
        private readonly Canvas trialCanvas;
        private readonly Canvas actionCanvas;
        private readonly Canvas battleCanvas;
        private readonly Text battleReadout;
        private bool actionMenuOpen;
        private bool battleMode;
        private bool waitingForEscortLeader;
        private bool layoutReady;
        private bool compactHud;
        private float pulseStarted = float.NegativeInfinity;
        private bool mutedPulse;

        public SoundwellView(Transform root, Camera camera, Transform player, Transform bell,
            Transform beast, Transform safe, Action ring, Action wrap, Action retry, Action back,
            Action carry, Action gate, Action attackBeast, Action guard, Action openGate, Action cancelActionMenu)
        {
            this.camera = camera;
            beastActor = beast;
            escortLeader = player;
            retreatMarker = safe;
            square = Sprite.Create(Texture2D.whiteTexture,
                new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                new Vector2(0.5f, 0.5f), Texture2D.whiteTexture.width);
            leaderPixelSprite = CreatePixelSprite("PLACEHOLDER 16px leader",
                LeaderPixels, out leaderPixelTexture);
            leaderWalkPixelSprite = CreatePixelSprite("PLACEHOLDER 16px leader walk",
                LeaderWalkPixels, out leaderWalkPixelTexture);
            beastPixelSprite = CreatePixelSprite("PLACEHOLDER 16px beast",
                BeastPixels, out beastPixelTexture);
            surveyorPixelSprite = CreatePixelSprite("PLACEHOLDER 16px surveyor idle",
                SurveyorPixels, out surveyorPixelTexture);
            surveyorWalkPixelSprite = CreatePixelSprite("PLACEHOLDER 16px surveyor walk",
                SurveyorWalkPixels, out surveyorWalkPixelTexture);
            bellStandPixelSprite = CreatePixelSprite("PLACEHOLDER 16px copper bell stand",
                BellStandPixels, out bellStandPixelTexture);
            attackTargetPixelSprite = CreatePixelSprite("PLACEHOLDER 16px ORK target cue",
                AttackTargetPixels, out attackTargetPixelTexture);
            activeRescuerPixelSprite = CreatePixelSprite("PLACEHOLDER 16px ORK active rescuer cue",
                ActiveRescuerPixels, out activeRescuerPixelTexture);
            ringMaterial = new Material(Shader.Find("Sprites/Default"));
            camera.backgroundColor = new Color(0.055f, 0.086f, 0.115f);

            Plate(root, "Trial floor", Vector2.zero, new Vector2(14f, 6.4f), new Color(0.11f, 0.19f, 0.23f), -4);
            Plate(root, "Beast lane", new Vector2(3.7f, 0f), new Vector2(6.55f, 6.4f), new Color(0.22f, 0.16f, 0.18f), -3);
            Plate(safe, "Retreat mark", Vector2.zero, new Vector2(1.75f, 1.75f), new Color(0.16f, 0.38f, 0.36f), -1);
            // Two genuine 16x16 point-filtered pedestals instead of stretched
            // solid rectangles. Both remain visual only: the bell may be
            // placed at the same two authored world positions, and there is
            // no extra collider, lure source, or secret interactable.
            var entranceStand = Plate(root, "PLACEHOLDER entrance bell stand", bell.position,
                Vector2.one, new Color(0.55f, 0.84f, 0.88f), -1);
            entranceStand.sprite = bellStandPixelSprite;
            var sideStand = Plate(root, "PLACEHOLDER side bell stand", SoundwellTrial.SideBellStand,
                Vector2.one, new Color(0.97f, 0.76f, 0.40f), -1);
            sideStand.sprite = bellStandPixelSprite;
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
            // The observed claw direction is readable on the floor, not only
            // in a dialogue. Three physical scratch marks trace the old route
            // from the evidence site to the side stand; no hidden puzzle state
            // or alternate beast destination is created by these markers.
            revealedClawTrail = new SpriteRenderer[3];
            for (int i = 0; i < revealedClawTrail.Length; i++)
            {
                float distance = (i + 1f) / (revealedClawTrail.Length + 1f);
                Vector2 site = Vector2.Lerp(SoundwellTrial.ClawEvidencePosition,
                    SoundwellTrial.SideBellStand, distance);
                revealedClawTrail[i] = Plate(root,
                    "PLACEHOLDER revealed claw trail " + (i + 1), site,
                    new Vector2(0.31f, 0.10f), new Color(0.96f, 0.58f, 0.34f, 0.82f), 1);
                revealedClawTrail[i].transform.rotation = Quaternion.Euler(0f, 0f, -65f);
                revealedClawTrail[i].enabled = false;
            }
            // M5: fixed walls live in the saved TilemapCollider2D scene layer.
            // Only the Makinom-controlled gate retains a dynamic BoxCollider2D.
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
            // The actual survivor is one transparent, point-filtered 16px
            // character, not scaled whiteTexture plates plus a separate face.
            surveyorPixelRenderer = Plate(surveyorFigure, "PLACEHOLDER trapped surveyor",
                Vector2.zero, Vector2.one, Ink, 3);
            surveyorPixelRenderer.sprite = surveyorPixelSprite;
            upperBoardMarker = Plate(root, "PLACEHOLDER upper load-bearing plank",
                SoundwellTrial.UpperBoardPosition, new Vector2(1.65f, 0.3f),
                new Color(0.30f, 0.37f, 0.40f), 1);
            // A visible weak lower route and its collapsed state make the
            // surveyor's warning consequential without a hidden timer.
            lowerBoardMarker = Plate(root, "PLACEHOLDER unsafe lower plank",
                SoundwellTrial.LowerBoardPosition, new Vector2(1.65f, 0.34f),
                new Color(0.67f, 0.39f, 0.27f), 1);

            // Both silhouette and face animate under one visual-only Animator;
            // the ORK leader's physics/transform is never animated.
            var animatedPlayer = new GameObject("PLACEHOLDER animated player").transform;
            animatedPlayer.SetParent(player, false);
            // The original Animator still moves this complete visual-only child.
            // The face is part of one transparent pixel silhouette.
            leaderVisualRenderer = Plate(animatedPlayer, "PLACEHOLDER player silhouette",
                Vector2.zero, Vector2.one, new Color(0.43f, 0.91f, 0.72f), 3);
            leaderVisualRenderer.sprite = leaderPixelSprite;
            playerAnimation = animatedPlayer.gameObject.AddComponent<SoundwellPlayerVisualAnimation>();
            playerAnimation.Initialize();
            bellMarker = Plate(bell, "PLACEHOLDER copper bell", Vector2.zero, new Vector2(0.57f, 0.57f), Gold, 2);
            bellMarker.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            // A small diamond marks the beast's current physical destination.
            // Gold tracks the audible signal; blue tracks the last-heard search.
            // No simulated sound waves, pathfinding state or second AI target.
            beastIntentMarker = Plate(root, "PLACEHOLDER beast intent marker",
                Vector2.zero, new Vector2(0.46f, 0.46f), Gold, 1);
            beastIntentMarker.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            beastIntentMarker.enabled = false;
            // The gold 16px corner brackets mark ORK's *currently legal*
            // attack target. This is presentation only: it neither selects a
            // second combatant nor modifies damage, AI, or scene colliders.
            attackTargetMarker = Plate(root, "PLACEHOLDER ORK selected beast target",
                (Vector2)beast.position + new Vector2(0f, 1.12f), Vector2.one, Gold, 7);
            attackTargetMarker.sprite = attackTargetPixelSprite;
            attackTargetMarker.enabled = false;
            // A separate cyan 16px chevron follows only the actual ORK
            // combatant currently allowed to attack. Visual-only: no input,
            // collider, invented actor choice or second battle state.
            activeRescuerMarker = Plate(root, "PLACEHOLDER ORK current rescuer",
                Vector2.zero, Vector2.one, new Color(0.40f, 0.90f, 0.90f), 7);
            activeRescuerMarker.sprite = activeRescuerPixelSprite;
            activeRescuerMarker.enabled = false;
            // A horned pixel silhouette on the same authoritative ORK beast.
            beastVisualRenderer = Plate(beast, "PLACEHOLDER beast", Vector2.zero,
                Vector2.one, new Color(0.95f, 0.41f, 0.42f), 3);
            beastVisualRenderer.sprite = beastPixelSprite;

            // Telegraph the actual encounter radius rather than asking the
            // player to guess a hidden distance. The ring shares the same
            // authoritative radius as SoundwellTrial's knockback check and
            // is parented to the moving beast, never a copied AI position.
            var contactObject = new GameObject("PLACEHOLDER beast contact radius");
            contactObject.transform.SetParent(beast, false);
            beastContactRing = contactObject.AddComponent<LineRenderer>();
            beastContactRing.sharedMaterial = ringMaterial;
            beastContactRing.useWorldSpace = false;
            beastContactRing.loop = true;
            beastContactRing.positionCount = 40;
            beastContactRing.sortingOrder = 2;
            beastContactRing.widthMultiplier = 0.04f;
            beastContactRing.startColor = beastContactRing.endColor =
                new Color(1f, 0.30f, 0.26f, 0.85f);
            for (int i = 0; i < beastContactRing.positionCount; i++)
            {
                float angle = 2f * Mathf.PI * i / beastContactRing.positionCount;
                beastContactRing.SetPosition(i, new Vector3(
                    Mathf.Cos(angle) * SoundwellTrial.BeastContactRadius,
                    Mathf.Sin(angle) * SoundwellTrial.BeastContactRadius, 0f));
            }
            beastContactRing.enabled = false;

            // Visualizes the existing, short muffled-bell search window.
            // The timer uses the beast's actual remaining search fraction.
            searchTimerTrack = Plate(root, "PLACEHOLDER beast search timer track",
                (Vector2)beast.position + new Vector2(0f, 0.92f),
                new Vector2(1.20f, 0.15f),
                new Color(0.13f, 0.19f, 0.25f, 0.90f), 5);
            searchTimerFill = Plate(root, "PLACEHOLDER beast search timer fill",
                (Vector2)beast.position + new Vector2(0f, 0.92f),
                new Vector2(1.12f, 0.11f), Cloth, 6);
            searchTimerTrack.enabled = false;
            searchTimerFill.enabled = false;

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
            // M6: the saved mixer is the single output route for this bell.
            SoundwellAudioBus.Route(audio);
            clearBell = BellClip("Copper bell", 640f, 0.75f, 0.22f);
            mutedBell = BellClip("Wrapped bell", 230f, 0.20f, 0.10f);

            trialCanvas = new GameObject("PLACEHOLDER trial UI", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            trialCanvas.transform.SetParent(root, false);
            trialCanvas.renderMode = RenderMode.WorldSpace;
            trialCanvas.worldCamera = camera;
            trialCanvas.sortingOrder = 12;
            var rect = (RectTransform)trialCanvas.transform;
            rect.sizeDelta = new Vector2(840, 480);
            rect.localScale = Vector3.one * 0.02f;
            Localize(AddLabel(rect, "", 25, Ink, new Vector2(0, 224), new Vector2(700, 40)), "hud.title");
            Localize(AddLabel(rect, "", 14, Cloth,
                new Vector2(0, 194), new Vector2(650, 22)), "hud.entrance");
            Localize(AddLabel(rect, "", 13, Cloth, new Vector2(-278, -68), new Vector2(120, 24)), "hud.retreat");
            Localize(AddLabel(rect, "", 13, Gold, new Vector2(-88, -43), new Vector2(120, 22)), "hud.copper_bell");
            Localize(AddLabel(rect, "", 13, Cloth, new Vector2(-92, -122), new Vector2(180, 24)), "hud.side_stand");
            beastStatus = AddLabel(rect, "", 17, Ink, new Vector2(170, -100), new Vector2(290, 30));
            beastStatus.gameObject.name = "Soundwell beast status";
            gateStatus = AddLabel(rect, "GATE  /  LOCKED", 16, Ink,
                new Vector2(0, -102), new Vector2(220, 30));
            gateStatus.gameObject.name = "Soundwell gate status";
            escapeStatus = AddLabel(rect, "ESCAPE LANE  /  CLEAR", 13, Cloth,
                new Vector2(-260, -104), new Vector2(240, 28));
            escapeStatus.gameObject.name = "Soundwell escape status";
            surveyorStatus = AddLabel(rect, "", 15, Cloth,
                new Vector2(0, -148), new Vector2(760, 32));
            hint = AddLabel(rect, "", 18, Ink, new Vector2(0, -194), new Vector2(790, 34));
            hint.gameObject.name = "Soundwell trial hint";
            Localize(AddLabel(rect, "", 14, Cloth,
                new Vector2(0, -222), new Vector2(760, 26)), "hud.controls");

            // Pointer input remains available outside focus mode. The single
            // InputSystem UI module owns keyboard/gamepad navigation and Submit.
            actionCanvas = new GameObject("Soundwell actions HUD", typeof(RectTransform),
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

            // Read-only player-facing status, sourced exclusively from ORK combatants.
            // Do not add a raycaster or a second EventSystem: ORK owns combat actions.
            battleCanvas = new GameObject("Soundwell ORK battle HUD",
                typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            battleCanvas.transform.SetParent(root, false);
            battleCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            battleCanvas.sortingOrder = 21;
            var battleScaler = battleCanvas.gameObject.AddComponent<CanvasScaler>();
            battleScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            battleScaler.referenceResolution = new Vector2(960f, 540f);
            battleScaler.matchWidthOrHeight = 0.5f;
            battleReadout = AddLabel(battleCanvas.transform, "", 17, Ink,
                Vector2.zero, new Vector2(460f, 195f));
            battleReadout.gameObject.name = "Soundwell ORK battle readout";
            var battleRect = battleReadout.rectTransform;
            battleRect.anchorMin = battleRect.anchorMax = new Vector2(0f, 1f);
            battleRect.pivot = new Vector2(0f, 1f);
            battleRect.anchoredPosition = new Vector2(20f, -20f);
            battleReadout.alignment = TextAnchor.UpperLeft;
            battleReadout.font = SoundwellUiLocalization.PresentationFont(battleReadout.font);
            battleCanvas.gameObject.SetActive(false);
            ringButton = CreateActionButton(actionCanvas.transform, "Ring", "", 0, ring);
            Localize(ringButton.GetComponentInChildren<Text>(), "action.ring");
            wrapButton = CreateActionButton(actionCanvas.transform, "Wrap", "", 1, wrap);
            wrapButtonLabel = wrapButton.GetComponentInChildren<Text>();
            retryButton = CreateActionButton(actionCanvas.transform, "Retry", "", 2, retry);
            Localize(retryButton.GetComponentInChildren<Text>(), "action.retry");
            backButton = CreateActionButton(actionCanvas.transform, "Back", "", 3, back);
            Localize(backButton.GetComponentInChildren<Text>(), "action.back");
            // A view-only adapter into ORK's selected base attack. This button
            // never calculates damage or selects an unauthorized combatant.
            attackButton = CreateActionButton(actionCanvas.transform, "Attack", "", 6, () =>
            {
                // ORK may request another party member after this submitted action.
                // Re-arm focus for that future real choice without inventing a turn.
                focusAttackWhenReady = true;
                attackBeast();
            });
            attackButtonLabel = attackButton.GetComponentInChildren<Text>();
            attackButton.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            attackButton.interactable = false;
            attackButton.gameObject.SetActive(false);
            guardButton = CreateActionButton(actionCanvas.transform, "Guard", "", 7, () =>
            {
                focusAttackWhenReady = true;
                guard();
            });
            openGateButton = CreateActionButton(actionCanvas.transform, "Battle Gate", "", 8, () =>
            {
                focusAttackWhenReady = true;
                openGate();
            });
            Localize(guardButton.GetComponentInChildren<Text>(), "battle.guard");
            openGateLabel = openGateButton.GetComponentInChildren<Text>();
            Localize(openGateLabel, "battle.open_gate");
            foreach (var button in new[] { guardButton, openGateButton })
            {
                button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
                button.interactable = false;
                button.gameObject.SetActive(false);
            }
            carryButton = CreateActionButton(actionCanvas.transform, "Carry", "", 4, carry);
            carryButtonLabel = carryButton.GetComponentInChildren<Text>();
            gateButton = CreateActionButton(actionCanvas.transform, "Gate",
                SoundwellUiLocalization.Get("action.open_gate_focus"), 5, gate);
            gateButtonLabel = gateButton.GetComponentInChildren<Text>();
            menuStatus = AddLabel(actionCanvas.transform, "",
                14, Cloth, new Vector2(-270f, 220f), new Vector2(360f, 32f));
            menuStatus.resizeTextForBestFit = true;
            menuStatus.resizeTextMinSize = 11;
            menuStatus.resizeTextMaxSize = 14;

            // Pointer-accessible locale toggle remains separate from gameplay actions.
            languageButton = CreateActionButton(actionCanvas.transform, "Language", "", 7,
                SoundwellUiLocalization.Toggle);
            var langRect = (RectTransform)languageButton.transform;
            langRect.anchorMin = langRect.anchorMax = new Vector2(0.5f, 1f);
            langRect.pivot = new Vector2(0.5f, 1f);
            langRect.anchoredPosition = new Vector2(120f, -14f);
            langRect.sizeDelta = new Vector2(138f, 34f);
            Localize(languageButton.GetComponentInChildren<Text>(), "action.language");
            SoundwellUiLocalization.Changed += RefreshLocalizedLabels;
            RefreshLocalizedLabels();

            var cancelHandler = actionCanvas.gameObject.AddComponent<SoundwellActionMenuCancelHandler>();
            // Route Cancel through the trial owner so it restores the gameplay map,
            // not just the view's local focus state.
            cancelHandler.Initialize(cancelActionMenu);

            actionEventSystem = SoundwellInputAdapter.EnsureEventSystem(root);
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
            menuStatus.text = SoundwellUiLocalization.Get(open ? "hud.focus_active" : "hud.focus");
            if (open)
                EnsureActionSelection();
            else if (IsActionSelection(actionEventSystem.currentSelectedGameObject))
                actionEventSystem.SetSelectedGameObject(null);
        }

        public void SetBattleMode(bool active)
        {
            if (battleMode == active)
                return;

            battleMode = active;
            layoutReady = false;
            focusAttackWhenReady = active;
            attackTargetMarker.enabled = false;
            activeRescuerMarker.enabled = false;
            // The only battle-specific UGUI action abandons the live ORK
            // encounter. Name it explicitly; exploration still says Back.
            backButton.GetComponentInChildren<Text>().text =
                SoundwellUiLocalization.Get(active ? "action.retreat" : "action.back");
            trialCanvas.gameObject.SetActive(!active);
            battleCanvas.gameObject.SetActive(active);
            foreach (var button in new[] { attackButton, guardButton, openGateButton })
            {
                button.gameObject.SetActive(active);
                if (!active)
                    button.interactable = false;
            }
            foreach (var button in new[] { ringButton, wrapButton, retryButton,
                carryButton, gateButton, languageButton })
                button.gameObject.SetActive(!active);
            menuStatus.gameObject.SetActive(!active);
            actionCanvas.gameObject.SetActive(true);
            backButton.gameObject.SetActive(true);
            backButton.navigation = new Navigation
            {
                mode = active || actionMenuOpen ? Navigation.Mode.Automatic : Navigation.Mode.None
            };
            actionEventSystem.sendNavigationEvents = active || actionMenuOpen;
            if (active)
                actionEventSystem.SetSelectedGameObject(backButton.gameObject);
            else if (actionEventSystem.currentSelectedGameObject == backButton.gameObject)
                actionEventSystem.SetSelectedGameObject(null);
        }

        // The input comes from ORK's live action chooser, not a local timer or
        // invented per-character turn state. The active target is the one beast.
        public void SetBattleChoices(int activeRescuerIndex, Transform activeActor,
            bool canAttack, bool canGuard, bool canOpenGate, bool gateOpen)
        {
            if (!battleMode)
                return;
            attackButton.interactable = canAttack;
            guardButton.interactable = canGuard;
            openGateButton.interactable = canOpenGate;
            string gateCaption = SoundwellUiLocalization.Get(gateOpen ? "status.gate_open" : "battle.open_gate");
            if (openGateLabel.text != gateCaption)
                openGateLabel.text = gateCaption;
            // Match the visible world marker to the SAME ORK target and
            // legal action as the existing UGUI button. Follow the moving
            // beast in world space, but never inherit its rotation.
            attackTargetMarker.enabled = attackButton.interactable && beastActor != null;
            if (attackTargetMarker.enabled)
                attackTargetMarker.transform.position =
                    beastActor.position + new Vector3(0f, 1.12f, 0f);
            activeRescuerMarker.enabled = activeActor != null;
            if (activeRescuerMarker.enabled)
                activeRescuerMarker.transform.position =
                    activeActor.position + new Vector3(0f, 1.12f, 0f);
            attackButtonLabel.text = canAttack
                ? SoundwellUiLocalization.Get("battle.rescuer") + " " +
                    (activeRescuerIndex + 1) + "  >  " +
                    SoundwellUiLocalization.Get("battle.beast")
                : SoundwellUiLocalization.Get("battle.menu_pending");
            GameObject focused = actionEventSystem.currentSelectedGameObject;
            if ((focused == attackButton.gameObject && !canAttack) ||
                (focused == guardButton.gameObject && !canGuard) ||
                (focused == openGateButton.gameObject && !canOpenGate))
                actionEventSystem.SetSelectedGameObject(backButton.gameObject);
            // During startup Retreat is the only available button. As soon as
            // ORK exposes a legal action, keyboard/gamepad Submit should attack,
            // not immediately leave the encounter. Do not steal focus if the
            // player has navigated to a different UGUI control deliberately.
            if (attackButton.interactable && focusAttackWhenReady)
            {
                GameObject selected = actionEventSystem.currentSelectedGameObject;
                if (selected == null || selected == backButton.gameObject)
                    actionEventSystem.SetSelectedGameObject(attackButton.gameObject);
                focusAttackWhenReady = false;
            }
            attackButtonLabel.font = SoundwellUiLocalization.PresentationFont(
                Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        }

        public void SetBattleReadout(string summary)
        {
            if (battleMode && battleReadout.text != summary)
                battleReadout.text = summary;
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

            // Reserve the bottom row for combat so the retreat button does
            // not cover the beast or its target cue in the right-hand room.
            if (battleMode)
            {
                float width = compact ? 100f : 155f;
                var commands = new[] { attackButton, guardButton, openGateButton, backButton };
                for (int i = 0; i < commands.Length; i++)
                {
                    var rect = (RectTransform)commands[i].transform;
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
                    rect.pivot = new Vector2(0.5f, 0f);
                    rect.sizeDelta = new Vector2(width, 44f);
                    rect.anchoredPosition = new Vector2((i - 1.5f) * (width + 8f), 12f);
                    var label = commands[i].GetComponentInChildren<Text>();
                    label.rectTransform.sizeDelta = new Vector2(width - 8f, 38f);
                    label.resizeTextForBestFit = true;
                    label.resizeTextMinSize = 10;
                    label.resizeTextMaxSize = 15;
                }
                return;
            }

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

            var backLabel = backButton.GetComponentInChildren<Text>();
            backLabel.rectTransform.sizeDelta = new Vector2(190f, 38f);
            backLabel.resizeTextForBestFit = false;
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

        private void Localize(Text label, string key)
        {
            localizedLabels.Add(label, key);
            label.text = SoundwellUiLocalization.Get(key);
        }

        private void RefreshLocalizedLabels()
        {
            var displayFont = SoundwellUiLocalization.PresentationFont(
                Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            foreach (var kv in localizedLabels)
            {
                if (kv.Key == null)
                    continue;
                kv.Key.font = displayFont;
                kv.Key.text = SoundwellUiLocalization.Get(kv.Value);
            }
            if (backButton != null && battleMode)
                backButton.GetComponentInChildren<Text>().text =
                    SoundwellUiLocalization.Get("action.retreat");
            if (battleReadout != null)
                battleReadout.font = displayFont;
            // Dynamic exploration state text is also localized. A Chinese
            // StringTable entry without the pinned CJK font is not readable.
            foreach (Text statusLabel in new[] { beastStatus, gateStatus, escapeStatus })
                if (statusLabel != null)
                    statusLabel.font = displayFont;
            if (menuStatus != null)
            {
                menuStatus.font = displayFont;
                menuStatus.text = SoundwellUiLocalization.Get(actionMenuOpen ? "hud.focus_active" : "hud.focus");
            }
            if (wrapButtonLabel != null)
                wrapButtonLabel.font = displayFont;
            if (carryButtonLabel != null)
                carryButtonLabel.font = displayFont;
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
            surveyorPixelRenderer.sprite = surveyorPixelSprite;
            surveyorPixelRenderer.flipX = false;
            waitingForEscortLeader = false;
            escortLine.enabled = false;
            falseTrailMarker.enabled = true;
            safeTrailMarker.enabled = false;
            foreach (var trail in revealedClawTrail)
                trail.enabled = false;
        }

        public void Refresh(bool ringing, bool searching, bool returning, bool wrapped,
            bool atBell, bool atSafePoint, bool carrying, bool canPlaceBell,
            bool gateOpen, bool atGatePanel, bool canOpenGate, bool canEngageBeast,
            bool beastDefeated, bool atSurveyor,
            bool hasSurveyorClue, bool atUpperPlank, bool canSecurePlank,
            bool plankSecured, bool canEscortSurveyor, bool surveyorEscorting,
            bool rescueComplete, bool falseMarkerCleared, bool safeRouteMarked,
            bool atRopeEvidence, bool atCartEvidence, bool atClawEvidence, bool canInspectRope,
            bool canRecoverRope, bool canInspectCart, bool canInspectClaws, bool canTieEscortLine,
            bool canShareCartClue, bool ropeObserved, bool ropeRecovered, bool cartObserved,
            bool cartClueShared, bool cartKitReclaimed,
            bool clawsObserved, bool surveyorLineTied, bool beastBlocksEscape,
            bool beastContacted, bool lowerPlankBroken,
            float searchRemainingFraction, Vector2 beastDestination)
        {
            // The existing beast actor rotates continuously toward its real
            // sound target. Keep its 16px sprite upright on the screen and
            // flip on actual horizontal facing instead of rotating pixels
            // at arbitrary angles. This changes no ORK status, AI or collider.
            // ORK owns combatant death and can destroy the beast actor when
            // its HP reaches minimum. The scene persists for the survivor's
            // physical escort; never read a destroyed presentation component.
            if (beastVisualRenderer != null)
            {
                beastVisualRenderer.transform.rotation = Quaternion.identity;
                float beastFacingX = beastVisualRenderer.transform.parent.right.x;
                if (Mathf.Abs(beastFacingX) > 0.1f)
                    beastVisualRenderer.flipX = beastFacingX < 0f;
            }

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
            // The west side is protected by the locked gate. Telegraph the
            // danger only after opening the inner room, and remove the ring
            // after the survivor reaches safety.
            if (beastContactRing != null)
                beastContactRing.enabled = gateOpen && !rescueComplete && !beastDefeated;
            searchTimerTrack.enabled = searching;
            searchTimerFill.enabled = searching;
            if (searching && beastContactRing != null)
            {
                // World-horizontal despite rotations, placed over the
                // beast's actual location, with a right-to-left depletion.
                float remaining = Mathf.Clamp01(searchRemainingFraction);
                Vector2 center = (Vector2)beastContactRing.transform.position +
                    new Vector2(0f, 0.92f);
                searchTimerTrack.transform.position = new Vector3(center.x, center.y, 0f);
                searchTimerFill.transform.position = new Vector3(
                    center.x - 0.56f * (1f - remaining), center.y, 0f);
                searchTimerFill.transform.localScale =
                    new Vector3(1.12f * remaining, 0.11f, 1f);
            }
            escapeDangerMarker.enabled = beastBlocksEscape;
            string escapeCaption = SoundwellUiLocalization.Get(
                beastBlocksEscape ? "status.escape_blocked" : "status.escape_clear");
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
            foreach (var trail in revealedClawTrail)
                trail.enabled = clawsObserved;
            ringButton.interactable = atBell && !carrying;
            wrapButton.interactable = atBell && !carrying;
            retryButton.interactable = atSafePoint;
            backButton.interactable = true;
            carryButton.interactable = carrying ? canPlaceBell : atBell;
            gateButton.interactable = canEngageBeast || canInspectRope || canRecoverRope ||
                canInspectCart || canInspectClaws || canShareCartClue || canTieEscortLine ||
                canOpenGate || canSecurePlank || canEscortSurveyor || (atSurveyor && !hasSurveyorClue);
            // Timers and world interactions can invalidate a previously focused
            // action while the menu is open. Never leave keyboard/gamepad Submit
            // targeting a disabled button; return focus to an available action.
            if (actionMenuOpen)
                EnsureActionSelection();
            // The player's contextual G/RB choice follows the same authored
            // EN/zh StringTables as other Soundwell UGUI controls. Selecting
            // a translation cannot change the interaction's legal action.
            string interactKey = canEngageBeast ? "action.engage_beast"
                : canInspectRope ? "action.inspect_rope"
                : canRecoverRope ? "action.recover_cord"
                : canInspectCart ? "action.inspect_cart"
                : canShareCartClue ? "action.tell_cart"
                : canInspectClaws ? "action.inspect_claws"
                : canTieEscortLine ? "action.tie_guide_line"
                : canEscortSurveyor ? "action.lead_surveyor"
                : canSecurePlank ? "action.brace_plank"
                : atSurveyor && !hasSurveyorClue ? "action.ask_surveyor"
                : "action.open_gate_focus";
            string interactCaption = SoundwellUiLocalization.Get(interactKey);
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
            Vector3 previousSurveyorPosition = surveyorFigure.position;
            surveyorFigure.position = Vector3.MoveTowards(previousSurveyorPosition,
                new Vector3(routeTarget.x, routeTarget.y, 0f), 2.25f * Time.deltaTime);
            // Render only: use the *actual* escort displacement for both facing
            // and the pixel-step frame; no second NPC motor or route state.
            Vector3 surveyorStep = surveyorFigure.position - previousSurveyorPosition;
            if (Mathf.Abs(surveyorStep.x) > 0.001f)
                surveyorPixelRenderer.flipX = surveyorStep.x < 0f;
            bool surveyorMoving = surveyorStep.sqrMagnitude > 0.000001f;
            surveyorPixelRenderer.sprite = surveyorMoving &&
                Mathf.FloorToInt(Time.time / 0.18f) % 2 == 1
                ? surveyorWalkPixelSprite : surveyorPixelSprite;
            // When the guide runs too far ahead, mark the actual waiting NPC
            // amber instead of relying on a distant text-only warning. This
            // is presentation of the existing escort hysteresis, not a second
            // escort timer, movement authority or quest-state transition.
            surveyorPixelRenderer.color = surveyorEscorting && waitingForEscortLeader
                ? new Color(1f, 0.73f, 0.42f) : Ink;
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
            string gateCaption = SoundwellUiLocalization.Get(
                gateOpen ? "status.gate_open" : "status.gate_locked");
            if (gateStatus.text != gateCaption)
                gateStatus.text = gateCaption;
            gateStatus.color = gateOpen ? Cloth : Ink;
            string carryCaption = SoundwellUiLocalization.Get(carrying ? "action.drop" : "action.lift");
            if (carryButtonLabel.text != carryCaption)
                carryButtonLabel.text = carryCaption;
            if (actionMenuOpen)
                EnsureActionSelection();
            string wrapCaption = SoundwellUiLocalization.Get(wrapped ? "action.unwrap" : "action.wrap");
            if (wrapButtonLabel.text != wrapCaption)
                wrapButtonLabel.text = wrapCaption;
            bellMarker.color = wrapped ? Cloth : Gold;
            string status = SoundwellUiLocalization.Get(
                beastDefeated ? "status.beast_defeated"
                : ringing ? "status.beast_tracking" : searching ? "status.beast_searching"
                : returning ? "status.beast_returning" : "status.beast_listening");
            if (beastStatus.text != status)
            {
                beastStatus.text = status;
                beastStatus.color = ringing ? Gold : searching || returning ? Cloth : Ink;
            }

            string prompt;
            if (beastContacted && atSafePoint && !surveyorEscorting && !rescueComplete)
                prompt = "BEAST CONTACT: Pushed back to RETREAT. The gate remains open; choose a safer route.";
            else if (falseMarkerCleared)
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
            else if (beastDefeated && !gateOpen)
                prompt = "BEAST DEFEATED: The three-member ORK party cleared the inner lane. Open the gate.";
            else if (canEngageBeast)
                // UX-001: an unsolicited gate prompt must not solve the lure.
                // Inspecting the actual claw trail earns the precise side-stand
                // clue; combat remains a valid option without inspecting it.
                prompt = clawsObserved
                    ? "DIRECT ROUTE: Engage the ORK party, or lure the beast toward the SIDE BELL STAND."
                    : "DIRECT ROUTE: Engage the beast with the ORK party, or inspect nearby tracks for another approach.";
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
                prompt = clawsObserved
                    ? "The revealed claw trail runs toward the SIDE BELL STAND. The beast still needs a sound to follow."
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
                    : clawsObserved
                        ? "Move the bell to the SIDE STAND and draw the beast away to open this gate."
                        : "The beast guards this gate. Look for evidence of what draws its attention.";
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

        public void PlayBell(bool muffled)
        {
            mutedPulse = muffled;
            pulseStarted = Time.time;
            audio.Stop();
            audio.clip = muffled ? mutedBell : clearBell;
            audio.loop = !muffled;
            audio.Play();
        }

        // Reuse M4's sole presentation Animator; only the sprite image changes.
        // The rendered leader is still a child of the authoritative ORK/2D actor.
        public void AnimatePlayerMotion(bool moving, float horizontalMotion)
        {
            playerAnimation.UpdateMotion(moving);
            // This is renderer-only pixel facing. Preserve the last direction
            // while idle or moving vertically so the character does not flicker
            // between left and right; never flip the physics/ORK actor itself.
            if (moving && Mathf.Abs(horizontalMotion) > 0.01f)
                leaderVisualRenderer.flipX = horizontalMotion < 0f;
            bool walkFrame = playerAnimation.CurrentState == "Move" &&
                Mathf.FloorToInt(Time.time / 0.18f) % 2 == 1;
            leaderVisualRenderer.sprite = walkFrame ? leaderWalkPixelSprite : leaderPixelSprite;
        }

        public void AnimatePlayerAction()
        {
            playerAnimation.PlayAction();
            leaderVisualRenderer.sprite = leaderPixelSprite;
        }

        public void AnimatePlayerHit()
        {
            playerAnimation.PlayHit();
            leaderVisualRenderer.sprite = leaderPixelSprite;
        }

        public void ResetPlayerAnimation()
        {
            playerAnimation.ResetAnimation();
            leaderVisualRenderer.flipX = false;
            leaderVisualRenderer.sprite = leaderPixelSprite;
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
            SoundwellUiLocalization.Changed -= RefreshLocalizedLabels;
            // Return the EventSystem to its pre-trial state if ORK keeps it alive
            // across the scene transition.
            if (actionEventSystem != null)
            {
                actionEventSystem.sendNavigationEvents = previousNavigationEvents;
                if (IsActionSelection(actionEventSystem.currentSelectedGameObject))
                    actionEventSystem.SetSelectedGameObject(null);
            }
            Object.Destroy(square);
            Object.Destroy(leaderPixelSprite);
            Object.Destroy(leaderWalkPixelSprite);
            Object.Destroy(beastPixelSprite);
            Object.Destroy(surveyorPixelSprite);
            Object.Destroy(surveyorWalkPixelSprite);
            Object.Destroy(bellStandPixelSprite);
            Object.Destroy(attackTargetPixelSprite);
            Object.Destroy(activeRescuerPixelSprite);
            Object.Destroy(leaderPixelTexture);
            Object.Destroy(leaderWalkPixelTexture);
            Object.Destroy(beastPixelTexture);
            Object.Destroy(surveyorPixelTexture);
            Object.Destroy(surveyorWalkPixelTexture);
            Object.Destroy(bellStandPixelTexture);
            Object.Destroy(attackTargetPixelTexture);
            Object.Destroy(activeRescuerPixelTexture);
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

        // Render only: no collider, movement or ORK status depends on pixels.
        private static readonly string[] LeaderPixels =
        {
            "................",
            "......####......",
            ".....#++++#.....",
            ".....#++++#.....",
            ".....#++++#.....",
            "......####......",
            ".....######.....",
            "...##########...",
            "...##+####+##...",
            "...##########...",
            ".....######.....",
            ".....##..##.....",
            ".....##..##.....",
            "....###..###....",
            "....###..###....",
            "................",
        };

        // Same 16px body, with a readable alternating leg pose.
        // It is visual only: frame selection never changes movement or ORK status.
        private static readonly string[] LeaderWalkPixels =
        {
            "................",
            "......####......",
            ".....#++++#.....",
            ".....#++++#.....",
            ".....#++++#.....",
            "......####......",
            ".....######.....",
            "...##########...",
            "...##+####+##...",
            "...##########...",
            ".....######.....",
            ".....##..##.....",
            "....###...##....",
            "...###.....###..",
            "..###.......###.",
            "................",
        };

        private static readonly string[] BeastPixels =
        {
            "................",
            "..##........##..",
            "..###......###..",
            "...##########...",
            "..############..",
            "..##++####++##..",
            ".##############.",
            ".######++######.",
            "###############.",
            "...##########...",
            "...###....###...",
            "..####....####..",
            ".####......####.",
            ".###........###.",
            "................",
            "................",
        };

        // Worn cap, map-satchel coat, and boots: one whole 16px silhouette.
        private static readonly string[] SurveyorPixels =
        {
            "................",
            ".....######.....",
            "....###++###....",
            "....##++++##....",
            "....##++++##....",
            ".....######.....",
            "......####......",
            "....########....",
            "...###++++###...",
            "...###+##+###...",
            "....########....",
            ".....######.....",
            ".....##..##.....",
            ".....##..##.....",
            "....###..###....",
            "................",
        };

        private static readonly string[] SurveyorWalkPixels =
        {
            "................",
            ".....######.....",
            "....###++###....",
            "....##++++##....",
            "....##++++##....",
            ".....######.....",
            "......####......",
            "....########....",
            "...###++++###...",
            "...###+##+###...",
            "....########....",
            ".....######.....",
            ".....##..##.....",
            "....###...##....",
            "...###.....###..",
            "................",
        };

        // Silhouette for both actual named sound-source stands.
        // Square pixels, one world unit, no non-uniform stretching.
        private static readonly string[] BellStandPixels =
        {
            "................",
            "................",
            ".....######.....",
            "....########....",
            "...##++++++##...",
            "...##++++++##...",
            "....########....",
            "......####......",
            "......####......",
            "......####......",
            ".....######.....",
            "....###..###....",
            "...###....###...",
            "..###......###..",
            "..###......###..",
            "................",
        };

        // Non-interactive ORK target cue, not an alternative selection system.
        private static readonly string[] AttackTargetPixels =
        {
            "................",
            "....##....##....",
            "...#........#...",
            "..#..........#..",
            "..#..........#..",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "..#..........#..",
            "..#..........#..",
            "...#........#...",
            "....##....##....",
            "................",
        };

        // Current legal ORK actor, visibly distinct from beast target brackets.
        private static readonly string[] ActiveRescuerPixels =
        {
            "................",
            ".......##.......",
            "......####......",
            ".....######.....",
            "....########....",
            "...###....###...",
            "..##........##..",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
        };

        private static Sprite CreatePixelSprite(string name, string[] rows,
            out Texture2D texture)
        {
            const int size = 16;
            if (rows.Length != size)
                throw new System.InvalidOperationException("Expected 16 character rows.");
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                string row = rows[size - 1 - y];
                if (row.Length != size)
                    throw new System.InvalidOperationException("Expected a 16-pixel row.");
                for (int x = 0; x < size; x++)
                {
                    char value = row[x];
                    pixels[y * size + x] = value == '#'
                        ? new Color32(255, 255, 255, 255)
                        : value == '+'
                            ? new Color32(175, 175, 175, 255)
                            : new Color32(0, 0, 0, 0);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f), 16f, 0, SpriteMeshType.FullRect);
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

    internal sealed class SoundwellActionMenuCancelHandler : MonoBehaviour, ICancelHandler
    {
        private System.Action cancel;
        public void Initialize(System.Action callback) => cancel = callback;
        public void OnCancel(BaseEventData eventData) => cancel?.Invoke();
    }
}
