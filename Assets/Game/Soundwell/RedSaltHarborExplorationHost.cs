using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// End-to-end playable 4x Red Salt Outer Harbor exploration host (140x84 tiles).
    /// Connects RpgLargeMapChunkRenderer + RpgLargeMapExplorerController +
    /// RpgMapActorVisualBinder (16-PPU animated Hero + 4 Outer Harbor NPCs from
    /// RpgPixelArtManifest) + RpgLargeMapMinimapAndBeaconHud + a compact
    /// district/interact HUD + physical proximity triggers that open the delivered
    /// M05LandingCheckView, M05SeranLettersView, and M05HarborWalkView scenes
    /// without fabricating arrivals or overlapping canvases.
    /// </summary>
    public sealed class RedSaltHarborExplorationHost : IDisposable
    {
        private readonly Transform parent;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private readonly M05LandingReport? observedLanding;
        private readonly RedSaltHarborMapLayout layout;
        private readonly RpgLargeMapChunkRenderer chunkRenderer;
        private readonly RpgLargeMapExplorerController explorer;
        private readonly RpgLargeMapMinimapAndBeaconHud minimapHud;
        private readonly RpgMapActorVisualBinder heroVisual;
        private readonly RpgMapActorVisualBinder seranVisual;
        private readonly RpgMapActorVisualBinder repairerVisual;
        private readonly RpgMapActorVisualBinder fisherVisual;
        private readonly RpgMapActorVisualBinder saltVendorVisual;
        private readonly GameObject hudCanvas;
        private readonly Text districtLabel;
        private readonly GameObject promptBox;
        private readonly Text promptLabel;
        private readonly Font latinFont;

        private M05LandingCheckView activeLandingView;
        private M05SeranLettersView activeSeranView;
        private M05HarborWalkView activeHarborWalkView;
        private Vector2Int? lastPersistedTile;
        private bool disposed;

        public RedSaltHarborMapLayout Layout => layout;
        public RpgLargeMapChunkRenderer ChunkRenderer => chunkRenderer;
        public RpgLargeMapExplorerController Explorer => explorer;
        public RpgLargeMapMinimapAndBeaconHud MinimapHud => minimapHud;
        public RpgMapActorVisualBinder HeroVisual => heroVisual;
        public RpgMapActorVisualBinder SeranVisual => seranVisual;
        public RpgMapActorVisualBinder RepairerVisual => repairerVisual;
        public RpgMapActorVisualBinder FisherVisual => fisherVisual;
        public RpgMapActorVisualBinder SaltVendorVisual => saltVendorVisual;
        public Vector2 PlayerPosition => explorer.Position;
        public M05LandingCheckView ActiveLandingView =>
            activeLandingView != null && !activeLandingView.IsDisposed ? activeLandingView : null;
        public M05SeranLettersView ActiveSeranView =>
            activeSeranView != null && !activeSeranView.IsDisposed ? activeSeranView : null;
        public M05HarborWalkView ActiveHarborWalkView =>
            activeHarborWalkView != null && !activeHarborWalkView.IsDisposed ? activeHarborWalkView : null;
        public bool IsDialogueOpen =>
            ActiveLandingView != null || ActiveSeranView != null || ActiveHarborWalkView != null;
        public bool HarborWalkCompleted => HasVerifiedLanding() &&
            M05HarborVisitFacts.TryRead(getInt, out int mask) &&
            mask == M05HarborVisitFacts.AllBeatsMask;
        public bool IsDisposed => disposed;

        public RedSaltHarborExplorationHost(Transform parent,
            Func<string, int> getInt,
            Action<string, int> setInt,
            M05LandingReport? observedLanding = null,
            Sprite[] harborTileSlices = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.parent = parent;
            this.getInt = getInt ?? throw new ArgumentNullException(nameof(getInt));
            this.setInt = setInt ?? throw new ArgumentNullException(nameof(setInt));
            this.observedLanding = observedLanding;
            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            RpgPixelArtManifest manifest = RpgPixelArtManifest.LoadDefault();
            layout = RedSaltHarborMapLayout.Build();
            chunkRenderer = RpgLargeMapChunkRenderer.BuildForRedSaltHarbor(
                parent, layout, harborTileSlices);
            explorer = RpgLargeMapExplorerController.ForRedSaltHarbor(layout);
            // Never trust an old save location without a real, verified M05
            // arrival and a walkable tile from this exact map layout.
            if (HasVerifiedLanding() &&
                M05HarborPositionFacts.TryRead(getInt, layout, out Vector2Int? savedTile) &&
                savedTile.HasValue && explorer.TryPlaceAtWalkableTile(savedTile.Value))
                lastPersistedTile = savedTile.Value;
            explorer.ZoneEntered += _ => RefreshHud();
            minimapHud = RpgLargeMapMinimapAndBeaconHud.BuildForRedSaltHarbor(parent, layout);
            minimapHud.UpdatePlayerPosition(explorer.Position);

            Transform actorParent = chunkRenderer.RootObject.transform;
            heroVisual = new RpgMapActorVisualBinder(
                actorParent, "RedSalt_Hero",
                manifest != null ? manifest.FerryGuideIdle : null,
                manifest != null ? manifest.FerryGuideWalk1 : null,
                manifest != null ? manifest.FerryGuideWalk2 : null,
                manifest != null ? manifest.FerryGuideBrace : null,
                baseSortingOrder: 6);
            heroVisual.Sync(explorer.Position, explorer.Facing, RpgActorVisualState.Idle, 0f);

            seranVisual = MountStaticNpc(actorParent, "RedSalt_SeranCourier",
                FindTriggerCenter("m05.seran-letters", new Vector2(50.5f, 26.5f)),
                manifest != null ? manifest.SeranLetterCarrier : null);
            repairerVisual = MountStaticNpc(actorParent, "RedSalt_HarborRepairer",
                FindTriggerCenter("m05.repair-stall", new Vector2(84.5f, 30.5f)),
                manifest != null ? manifest.HarborRepairer : null);
            fisherVisual = MountStaticNpc(actorParent, "RedSalt_OuterHarborFisher",
                FindTriggerCenter("m05.fisher-rumor", new Vector2(118.5f, 22.5f)),
                manifest != null ? manifest.OuterHarborFisher : null);
            saltVendorVisual = MountStaticNpc(actorParent, "RedSalt_SaltVendor",
                FindTriggerCenter("m05.salt-market", new Vector2(76.5f, 56.5f)),
                manifest != null ? manifest.MiningFamilySaltVendor : null);

            // Compact non-blocking exploration HUD (no GraphicRaycaster).
            hudCanvas = new GameObject("RedSalt exploration HUD",
                typeof(RectTransform), typeof(Canvas));
            hudCanvas.transform.SetParent(parent, false);
            var canvas = hudCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 120;
            var scaler = hudCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;

            var topLeft = new GameObject("District badge", typeof(RectTransform));
            topLeft.transform.SetParent(hudCanvas.transform, false);
            var badgeRect = (RectTransform)topLeft.transform;
            badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(0f, 1f);
            badgeRect.anchoredPosition = new Vector2(138f, -34f);
            badgeRect.sizeDelta = new Vector2(240f, 34f);
            SoundwellUiTheme.CreatePanel(badgeRect, "District border",
                new Vector2(240f, 34f), new Color(0.68f, 0.58f, 0.40f, 0.90f));
            SoundwellUiTheme.CreatePanel(badgeRect, "District backing",
                new Vector2(236f, 30f), new Color(0.06f, 0.08f, 0.11f, 0.90f));
            districtLabel = SoundwellUiTheme.CreateLabel(badgeRect, 14,
                Vector2.zero, new Vector2(224f, 24f), new Color(0.94f, 0.90f, 0.82f, 1f));
            districtLabel.alignment = TextAnchor.MiddleCenter;

            promptBox = new GameObject("Interact prompt", typeof(RectTransform));
            promptBox.transform.SetParent(hudCanvas.transform, false);
            var promptRect = (RectTransform)promptBox.transform;
            promptRect.anchorMin = promptRect.anchorMax = new Vector2(0.5f, 0f);
            promptRect.anchoredPosition = new Vector2(0f, 44f);
            promptRect.sizeDelta = new Vector2(360f, 32f);
            SoundwellUiTheme.CreatePanel(promptRect, "Prompt border",
                new Vector2(360f, 32f), new Color(0.76f, 0.64f, 0.42f, 0.92f));
            SoundwellUiTheme.CreatePanel(promptRect, "Prompt backing",
                new Vector2(356f, 28f), new Color(0.06f, 0.08f, 0.11f, 0.92f));
            promptLabel = SoundwellUiTheme.CreateLabel(promptRect, 14,
                Vector2.zero, new Vector2(344f, 24f), new Color(0.96f, 0.88f, 0.66f, 1f));
            promptLabel.alignment = TextAnchor.MiddleCenter;

            SoundwellUiLocalization.Changed += RefreshHud;
            RefreshHud();
        }

        private Vector2 FindTriggerCenter(string triggerId, Vector2 fallback)
        {
            for (int i = 0; i < layout.Triggers.Count; i++)
            {
                RpgMapTriggerAnchor t = layout.Triggers[i];
                if (string.Equals(t.Id, triggerId, StringComparison.Ordinal))
                    return new Vector2(t.Tile.x + 0.5f, t.Tile.y + 0.5f);
            }
            return fallback;
        }

        private static RpgMapActorVisualBinder MountStaticNpc(Transform actorParent,
            string actorName, Vector2 worldPosition, Sprite sprite)
        {
            var binder = new RpgMapActorVisualBinder(
                actorParent, actorName,
                sprite, sprite, sprite, sprite,
                baseSortingOrder: 6);
            binder.Sync(worldPosition, Vector2Int.left, RpgActorVisualState.Idle, 0f);
            return binder;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void PruneDisposedDialogueViews()
        {
            if (activeLandingView != null && activeLandingView.IsDisposed)
            {
                activeLandingView.Dispose();
                activeLandingView = null;
            }
            if (activeSeranView != null && activeSeranView.IsDisposed)
            {
                activeSeranView.Dispose();
                activeSeranView = null;
            }
            if (activeHarborWalkView != null && activeHarborWalkView.IsDisposed)
            {
                activeHarborWalkView.Dispose();
                activeHarborWalkView = null;
            }
        }

        public bool StepExploration(Vector2 moveInput, float deltaTime)
        {
            if (disposed || parent == null)
                return false;
            PruneDisposedDialogueViews();
            if (IsDialogueOpen)
                return false;
            if (!IsFinite(deltaTime) || deltaTime < 0f ||
                !IsFinite(moveInput.x) || !IsFinite(moveInput.y))
                return false;

            bool moved = explorer.StepMovement(moveInput, deltaTime);
            heroVisual?.Sync(
                explorer.Position,
                explorer.Facing,
                moved ? RpgActorVisualState.Moving : RpgActorVisualState.Idle,
                deltaTime);
            if (moved)
            {
                minimapHud.UpdatePlayerPosition(explorer.Position);
                if (HasVerifiedLanding())
                {
                    var walkedTile = new Vector2Int(
                        Mathf.FloorToInt(explorer.Position.x),
                        Mathf.FloorToInt(explorer.Position.y));
                    if (!lastPersistedTile.HasValue || lastPersistedTile.Value != walkedTile)
                    {
                        if (!M05HarborPositionFacts.TryRecord(
                            walkedTile, layout, getInt, setInt))
                            throw new InvalidOperationException(
                                "Unable to persist the walkable M05 player tile.");
                        lastPersistedTile = walkedTile;
                    }
                }
            }
            RefreshHud();
            return moved;
        }

        public Vector3 StepCamera(Vector3 currentCameraPos, float deltaTime)
        {
            if (disposed || parent == null ||
                !IsFinite(deltaTime) || deltaTime < 0f ||
                !IsFinite(currentCameraPos.x) ||
                !IsFinite(currentCameraPos.y) ||
                !IsFinite(currentCameraPos.z))
                return currentCameraPos;
            return explorer.ComputeClampedCameraPosition(currentCameraPos, deltaTime);
        }

        public bool TryInteractAtCurrentPosition()
        {
            if (disposed || parent == null)
                return false;
            PruneDisposedDialogueViews();
            if (IsDialogueOpen)
                return false;
            if (!explorer.TryFindNearestTrigger(out RpgMapTriggerAnchor trigger))
                return false;

            switch (trigger.Id)
            {
                case "m05.landing-check":
                    return TryOpenLandingCheck();
                case "m05.seran-letters":
                    return TryOpenSeranLetters();
                case "m05.repair-stall":
                    return TryOpenHarborWalk(M05HarborBeat.RepairStall);
                case "m05.fisher-rumor":
                    return TryOpenHarborWalk(M05HarborBeat.FisherRumor);
                case "m05.salt-market":
                    return TryOpenHarborWalk(M05HarborBeat.MarketAccusation);
                default:
                    return false;
            }
        }

        private bool CanConfirmObservedLanding()
        {
            return observedLanding.HasValue &&
                M05LandingFacts.CanRecord(observedLanding.Value, getInt);
        }

        private bool TryOpenLandingCheck()
        {
            if (parent == null || !CanConfirmObservedLanding())
                return false;

            M05LandingCheckView openedView = null;
            openedView = new M05LandingCheckView(
                parent, observedLanding.Value, report =>
                {
                    if (disposed || activeLandingView != openedView)
                        return;

                    // A confirmed write may succeed before its acknowledgement
                    // throws. Re-read the authoritative Makinom record on
                    // retry; accept only this exact observed landing, never
                    // rewrite it or treat an invalid confirmed flag as empty.
                    if (!M05LandingFacts.TryRead(getInt, out M05LandingReport? saved))
                        throw new InvalidOperationException(
                            "Unable to read the current M05 landing report.");

                    if (saved.HasValue)
                    {
                        if (!SameLanding(saved.Value, report))
                            throw new InvalidOperationException(
                                "Saved M05 landing differs from the witnessed arrival.");
                    }
                    else if (getInt(M05LandingFacts.ConfirmedKey) != 0 ||
                        !M05LandingFacts.TryRecord(report, getInt, setInt))
                    {
                        throw new InvalidOperationException(
                            "Unable to persist M05 landing report.");
                    }

                    // Never retire the only confirm control until a valid
                    // record has actually been read back for this voyage.
                    if (!M05LandingFacts.TryRead(getInt, out saved) ||
                        !saved.HasValue || !SameLanding(saved.Value, report))
                        throw new InvalidOperationException(
                            "M05 landing confirmation was not verified after save.");

                    activeLandingView?.Dispose();
                    activeLandingView = null;
                    RefreshHud();
                });
            activeLandingView = openedView;
            RefreshHud();
            return true;
        }

        private bool TryOpenSeranLetters()
        {
            if (parent == null)
                return false;
            // Requires a verified arrival and rejects corrupt nonzero letter codes.
            if (!M05LandingFacts.TryRead(getInt, out M05LandingReport? savedLanding) ||
                !savedLanding.HasValue)
                return false;
            if (getInt(M05LettersFacts.DecisionKey) != 0)
                return false;

            // Capture the verified physical arrival at the time the player
            // meets Seran. The dialogue can remain open while a new voyage
            // resets these Makinom facts, so recheck before writing a choice.
            M05LandingReport openedLanding = savedLanding.Value;
            int openedSequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            M05SeranLettersView openedView = null;
            openedView = new M05SeranLettersView(parent, decision =>
            {
                if (disposed || activeSeranView != openedView)
                    return;

                if (!HasSameCurrentLanding(openedLanding, openedSequence))
                    throw new InvalidOperationException(
                        "Seran's open dialogue no longer matches the verified M05 landing.");
                // A write can commit before the UI handoff throws.
                // A retry accepts only the identical saved choice and never
                // writes a second letter event or overwrites another answer.
                if (!M05LettersFacts.TryRead(getInt, out M05LetterDecision? savedChoice))
                    throw new InvalidOperationException(
                        "Unable to read M05 Seran letter choice.");
                if (savedChoice.HasValue)
                {
                    if (savedChoice.Value != decision)
                        throw new InvalidOperationException(
                            "The saved M05 Seran choice differs from the selected answer.");
                }
                else if (getInt(M05LettersFacts.DecisionKey) != 0 ||
                    !M05LettersFacts.TryRecord(decision, getInt, setInt))
                    throw new InvalidOperationException(
                        "Unable to persist M05 Seran letter choice.");
                activeSeranView?.Dispose();
                activeSeranView = null;
                RefreshHud();
            });
            activeSeranView = openedView;
            RefreshHud();
            return true;
        }

        private static bool SameLanding(M05LandingReport a, M05LandingReport b)
        {
            return a.CrewArrived == b.CrewArrived &&
                a.ReadyRescueBerths == b.ReadyRescueBerths &&
                a.TenderArrived == b.TenderArrived &&
                a.ReceiverMet == b.ReceiverMet;
        }

        private bool HasVerifiedLanding()
        {
            return M05LandingFacts.TryRead(getInt, out M05LandingReport? saved) &&
                saved.HasValue;
        }

        private bool HasSameCurrentLanding(M05LandingReport openedLanding,
            int openedSequence)
        {
            // Persisted M05 arrival is the only authority. A dialogue may
            // outlive the voyage it was opened for; reject missing, invalid,
            // or different observed landings before committing its outcome.
            return M05LandingFacts.TryRead(getInt, out M05LandingReport? current) &&
                current.HasValue &&
                getInt(M05LandingFacts.ArrivalSequenceKey) == openedSequence &&
                current.Value.CrewArrived == openedLanding.CrewArrived &&
                current.Value.ReadyRescueBerths == openedLanding.ReadyRescueBerths &&
                current.Value.TenderArrived == openedLanding.TenderArrived &&
                current.Value.ReceiverMet == openedLanding.ReceiverMet;
        }

        private bool CanOfferHarborBeat(M05HarborBeat beat)
        {
            return HasVerifiedLanding() &&
                M05HarborVisitFacts.TryRead(getInt, out int mask) &&
                !M05HarborVisitFacts.Contains(mask, beat);
        }

        private bool TryOpenHarborWalk(M05HarborBeat beat)
        {
            if (parent == null || !CanOfferHarborBeat(beat) ||
                !M05LandingFacts.TryRead(getInt, out M05LandingReport? savedLanding) ||
                !savedLanding.HasValue)
                return false;

            M05LandingReport openedLanding = savedLanding.Value;
            int openedSequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            M05HarborWalkView openedView = null;
            openedView = new M05HarborWalkView(parent, () =>
            {
                if (disposed || activeHarborWalkView != openedView)
                    return;

                // A new voyage may have replaced the physical arrival while
                // this location's dialogue remained open. Never credit an
                // old repair, rumor, or accusation to the new ship's visit.
                if (!HasSameCurrentLanding(openedLanding, openedSequence))
                    throw new InvalidOperationException(
                        "The observed harbor conversation no longer belongs to this landing.");
                // Save the witnessed beat first. A failed Makinom write leaves
                // the same dialogue available for retry and does not advance
                // the beacon or replace another scene's save authority.
                if (!M05HarborVisitFacts.TryRecord(beat, getInt, setInt))
                    throw new InvalidOperationException(
                        "Unable to persist the observed outer-harbor conversation.");
                activeHarborWalkView?.Dispose();
                activeHarborWalkView = null;
                RefreshHud();
            }, beat);
            activeHarborWalkView = openedView;
            RefreshHud();
            return true;
        }

        private bool CanOfferPrompt(string id)
        {
            switch (id)
            {
                case "m05.landing-check":
                    return CanConfirmObservedLanding();
                case "m05.seran-letters":
                    return HasVerifiedLanding() &&
                        getInt(M05LettersFacts.DecisionKey) == 0;
                case "m05.repair-stall":
                    return CanOfferHarborBeat(M05HarborBeat.RepairStall);
                case "m05.fisher-rumor":
                    return CanOfferHarborBeat(M05HarborBeat.FisherRumor);
                case "m05.salt-market":
                    return CanOfferHarborBeat(M05HarborBeat.MarketAccusation);
                default:
                    return false;
            }
        }

        public void RefreshHud()
        {
            if (disposed || parent == null || hudCanvas == null ||
                districtLabel == null || promptBox == null || promptLabel == null)
                return;
            PruneDisposedDialogueViews();

            // Beacon colors are presentation only. Authoritative Makinom
            // landing/letter decisions and this map's actually witnessed
            // location beats determine which goals have been completed.
            bool landingVerified = HasVerifiedLanding();
            minimapHud?.SetBeaconCompleted("m05.landing-check", landingVerified);
            // A previous voyage's Seran answer must never complete this map
            // objective if its arrival was reset or invalidated. The raw
            // letter flag alone is not proof of the current physical landing.
            bool letterDecisionRecorded = landingVerified &&
                M05LettersFacts.TryRead(getInt,
                    out M05LetterDecision? recordedLetters) && recordedLetters.HasValue;
            minimapHud?.SetBeaconCompleted("m05.seran-letters", letterDecisionRecorded);
            // C# definite-assignment must not depend on the preceding
            // short-circuited landing check. No valid landing => no
            // completed map objectives, even if stale visit bits exist.
            int observedMask = 0;
            bool savedBeatsValid = landingVerified &&
                M05HarborVisitFacts.TryRead(getInt, out observedMask);
            minimapHud?.SetBeaconCompleted("m05.repair-stall",
                savedBeatsValid && M05HarborVisitFacts.Contains(observedMask, M05HarborBeat.RepairStall));
            minimapHud?.SetBeaconCompleted("m05.fisher-rumor",
                savedBeatsValid && M05HarborVisitFacts.Contains(observedMask, M05HarborBeat.FisherRumor));
            minimapHud?.SetBeaconCompleted("m05.salt-market",
                savedBeatsValid && M05HarborVisitFacts.Contains(observedMask, M05HarborBeat.MarketAccusation));

            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            districtLabel.font = promptLabel.font = font;

            if (explorer.CurrentZone.HasValue)
            {
                RpgMapZone z = explorer.CurrentZone.Value;
                districtLabel.text = zh ? z.DisplayNameZh : z.DisplayNameEn;
            }
            else
            {
                districtLabel.text = zh ? "赤盐外港" : "Red Salt Harbor";
            }

            // Initialize before the short-circuit so the trigger is definitely assigned
            // even when a dialogue is already open.
            RpgMapTriggerAnchor trigger = default(RpgMapTriggerAnchor);
            bool showPrompt = !IsDialogueOpen &&
                explorer.TryFindNearestTrigger(out trigger) &&
                CanOfferPrompt(trigger.Id);
            promptBox.SetActive(showPrompt);
            if (showPrompt)
                promptLabel.text = FormatTriggerPrompt(trigger.Id, zh);
        }

        private static string FormatTriggerPrompt(string triggerId, bool zh)
        {
            switch (triggerId)
            {
                case "m05.landing-check":
                    return zh ? "[交互] 核查靠岸与驳船运力" : "[Interact] Verify Landing & Berth";
                case "m05.seran-letters":
                    return zh ? "[交互] 与瑟兰老人对话（八封家书）" : "[Interact] Speak with Seran Courier";
                case "m05.repair-stall":
                    return zh ? "[交互] 靠近修船摊听矿工交谈" : "[Interact] Listen at Repair Stall";
                case "m05.fisher-rumor":
                    return zh ? "[交互] 询问东南防波堤渔民" : "[Interact] Ask Breakwater Fisherman";
                case "m05.salt-market":
                    return zh ? "[交互] 走进赤盐盐市摊位" : "[Interact] Approach Salt Market Stall";
                default:
                    return zh ? "[交互] 查看" : "[Interact] Inspect";
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            SoundwellUiLocalization.Changed -= RefreshHud;
            activeLandingView?.Dispose();
            activeLandingView = null;
            activeSeranView?.Dispose();
            activeSeranView = null;
            activeHarborWalkView?.Dispose();
            activeHarborWalkView = null;
            heroVisual?.Dispose();
            seranVisual?.Dispose();
            repairerVisual?.Dispose();
            fisherVisual?.Dispose();
            saltVendorVisual?.Dispose();
            minimapHud?.Dispose();
            chunkRenderer?.Dispose();
            SoundwellUiTheme.SafeDestroyCanvas(hudCanvas);
        }
    }
}
