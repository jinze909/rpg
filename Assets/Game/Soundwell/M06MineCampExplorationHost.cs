using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Playable M06 ("她没有在原地等你") Red Salt Mine Camp exploration host
    /// connecting the 96x64 4x-scale M06MineCampMapLayout with physical trigger
    /// discovery for the four M06MineCampReconView leads and the underground
    /// M06PumpRoomReunionView decision, strictly gated on verified M05 landing,
    /// M05 arrival voyage identity, and clean M06RescuePlanFacts state.
    /// </summary>
    public sealed class M06MineCampExplorationHost : IDisposable
    {
        public const float MoveSpeedTilesPerSecond = 6.5f;
        private const int MaxMovementSubsteps = 4096;

        private readonly Transform parent;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private readonly M06MineCampMapLayout layout;
        private readonly RpgLargeMapChunkRenderer chunkRenderer;
        private readonly RpgLargeMapMinimapAndBeaconHud minimapHud;
        private readonly RpgMapActorVisualBinder heroVisual;
        private readonly RpgMapActorVisualBinder shenLiVisual;
        private readonly GameObject hudCanvasObject;
        private readonly Font latinFont;
        private readonly Text districtBanner;
        private readonly Text promptBanner;
        private M06MineCampReconView activeReconView;
        private M06PumpRoomReunionView activeReunionView;
        private Vector2 playerPosition;
        private Vector2 facing = Vector2.up;
        private bool disposed;

        public M06MineCampMapLayout Layout => layout;
        public RpgLargeMapChunkRenderer ChunkRenderer => chunkRenderer;
        public RpgLargeMapMinimapAndBeaconHud MinimapHud => minimapHud;
        public RpgMapActorVisualBinder HeroVisual => heroVisual;
        public RpgMapActorVisualBinder ShenLiVisual => shenLiVisual;
        public Vector2 PlayerPosition => playerPosition;
        public Vector2 Facing => facing;
        public RpgMapZone? CurrentDistrict => layout.FindDistrictAtTile(CurrentTile);
        public Vector2Int CurrentTile => new Vector2Int(
            Mathf.FloorToInt(playerPosition.x),
            Mathf.FloorToInt(playerPosition.y));
        public M06MineCampReconView ActiveReconView
        {
            get { PruneDisposedDialogs(); return activeReconView; }
        }
        public M06PumpRoomReunionView ActiveReunionView
        {
            get { PruneDisposedDialogs(); return activeReunionView; }
        }
        public bool IsDialogueOpen
        {
            get { PruneDisposedDialogs(); return activeReconView != null || activeReunionView != null; }
        }

        private void PruneDisposedDialogs()
        {
            if (activeReconView != null && activeReconView.IsDisposed)
                activeReconView = null;
            if (activeReunionView != null && activeReunionView.IsDisposed)
                activeReunionView = null;
        }
        public bool IsDisposed => disposed;
        public Text DistrictBanner => districtBanner;
        public Text PromptBanner => promptBanner;

        public bool RescuePlanSettled =>
            M06RescuePlanFacts.TryRead(getInt, out M06ReunionRecord? saved) &&
            saved.HasValue;

        public int ObservedReconCount =>
            HasVerifiedLanding() && M06MineCampReconFacts.TryRead(getInt, out int mask)
                ? M06MineCampReconFacts.CountObserved(mask)
                : 0;

        public M06MineCampExplorationHost(
            Transform parent,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.getInt = getInt ?? throw new ArgumentNullException(nameof(getInt));
            this.setInt = setInt ?? throw new ArgumentNullException(nameof(setInt));

            layout = M06MineCampMapLayout.Build();
            playerPosition = new Vector2(layout.PlayerSpawn.x + 0.5f, layout.PlayerSpawn.y + 0.5f);

            // The previous host only constructed a HUD. Mount the authored
            // 96x64 map and its actual 16-PPU party/NPC sprites as visible
            // world objects without giving Unity a second ORK fact store.
            RpgPixelArtManifest art = RpgPixelArtManifest.LoadDefault();
            chunkRenderer = RpgLargeMapChunkRenderer.BuildForMineCamp(parent, layout);
            minimapHud = RpgLargeMapMinimapAndBeaconHud.BuildForMineCamp(parent, layout);
            Transform actors = chunkRenderer.RootObject.transform;
            heroVisual = new RpgMapActorVisualBinder(
                actors, "M06 mine camp player",
                art != null ? art.FerryGuideIdle : null,
                art != null ? art.FerryGuideWalk1 : null,
                art != null ? art.FerryGuideWalk2 : null,
                art != null ? art.FerryGuideBrace : null);
            heroVisual.Sync(playerPosition, Vector2Int.up, RpgActorVisualState.Idle);
            shenLiVisual = new RpgMapActorVisualBinder(
                actors, "M06 Shen Li by main pump",
                art != null ? art.ShenLiBridgeIdle : null);
            shenLiVisual.Sync(new Vector2(54.5f, 36.5f),
                Vector2Int.down, RpgActorVisualState.Idle);
            // Never reveal an NPC in this voyage before a real M05 landing.
            shenLiVisual.ActorObject.SetActive(HasVerifiedLanding());

            latinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hudCanvasObject = new GameObject("M06 mine camp HUD canvas",
                typeof(RectTransform), typeof(Canvas));
            hudCanvasObject.transform.SetParent(parent, false);
            var canvas = hudCanvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 120;
            var scaler = hudCanvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                SoundwellUiTheme.ReferenceWidth, SoundwellUiTheme.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;

            var hudRoot = (RectTransform)hudCanvasObject.transform;
            Color ink = new Color(0.93f, 0.89f, 0.81f, 1f);
            districtBanner = SoundwellUiTheme.CreateLabel(hudRoot, 16,
                new Vector2(-240f, 232f), new Vector2(460f, 32f), ink);
            districtBanner.alignment = TextAnchor.MiddleLeft;
            promptBanner = SoundwellUiTheme.CreateLabel(hudRoot, 15,
                new Vector2(0f, -236f), new Vector2(560f, 28f), ink);
            promptBanner.alignment = TextAnchor.MiddleCenter;

            RefreshHud();
            SoundwellUiLocalization.Changed += RefreshHud;
        }

        public bool TryPlaceAtWalkableTile(Vector2Int tile)
        {
            if (disposed || parent == null || !IsWalkableTile(tile.x, tile.y))
                return false;

            playerPosition = new Vector2(tile.x + 0.5f, tile.y + 0.5f);
            SyncHero(moving: false, deltaSeconds: 0f);
            if (minimapHud != null && !minimapHud.IsDisposed)
                minimapHud.UpdatePlayerPosition(playerPosition);
            RefreshHud();
            return true;
        }

        public bool StepMovement(Vector2 input, float deltaSeconds)
        {
            if (disposed || parent == null)
                return false;
            float inputMagnitudeSquared = input.sqrMagnitude;
            if (IsDialogueOpen || !IsFinite(input) ||
                !IsFinite(inputMagnitudeSquared) || !IsFinite(deltaSeconds) ||
                deltaSeconds <= 0f || inputMagnitudeSquared < 0.0001f ||
                !IsFinite(playerPosition))
            {
                SyncHero(moving: false, deltaSeconds: 0f);
                return false;
            }

            Vector2 dir = input.normalized;
            Vector2 delta = dir * (MoveSpeedTilesPerSecond * deltaSeconds);
            float distance = delta.magnitude;
            float requiredSteps = Mathf.Ceil(distance / 0.35f);
            // A paused or corrupted frame must not turn into an unbounded
            // collision loop. Reject it without changing position or facing.
            if (!IsFinite(delta) || !IsFinite(distance) ||
                !IsFinite(requiredSteps) || requiredSteps > MaxMovementSubsteps)
            {
                SyncHero(moving: false, deltaSeconds: 0f);
                return false;
            }

            facing = dir;
            // The final destination alone may be walkable even when a wall
            // separates it from the player. Check short intermediate steps so
            // a slow frame cannot skip the mine-camp walls or sealed rooms.
            // Preserve independent axis movement for wall sliding.
            int steps = Mathf.Max(1, Mathf.CeilToInt(requiredSteps));
            Vector2 step = delta / steps;
            Vector2 next = playerPosition;
            bool moved = false;

            for (int i = 0; i < steps; i++)
            {
                float candidateX = next.x + step.x;
                if (!Mathf.Approximately(candidateX, next.x) &&
                    IsWalkableWorld(candidateX, next.y))
                {
                    next.x = candidateX;
                    moved = true;
                }

                float candidateY = next.y + step.y;
                if (!Mathf.Approximately(candidateY, next.y) &&
                    IsWalkableWorld(next.x, candidateY))
                {
                    next.y = candidateY;
                    moved = true;
                }
            }

            if (moved)
            {
                playerPosition = next;
                if (minimapHud != null && !minimapHud.IsDisposed)
                    minimapHud.UpdatePlayerPosition(playerPosition);
                RefreshHud();
            }
            SyncHero(moving: moved, deltaSeconds: deltaSeconds);
            return moved;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private void SyncHero(bool moving, float deltaSeconds)
        {
            if (heroVisual == null || heroVisual.IsDisposed)
                return;
            Vector2Int cardinal = Mathf.Abs(facing.x) >= Mathf.Abs(facing.y)
                ? (facing.x >= 0f ? Vector2Int.right : Vector2Int.left)
                : (facing.y >= 0f ? Vector2Int.up : Vector2Int.down);
            heroVisual.Sync(playerPosition, cardinal,
                moving ? RpgActorVisualState.Moving : RpgActorVisualState.Idle,
                deltaSeconds);
        }

        // The map is larger than one screen; let the owning scene drive its
        // only camera (Cinemachine/Pixel Perfect remains the final authority).
        public Vector3 StepCamera(Vector3 currentCameraPosition, float deltaSeconds)
        {
            if (disposed || parent == null || !IsFinite(currentCameraPosition) ||
                !IsFinite(playerPosition) || !IsFinite(deltaSeconds) ||
                deltaSeconds < 0f)
                return currentCameraPosition;

            float halfW = RpgWorldMapScale.LogicalScreenTilesX * 0.5f;
            float halfH = RpgWorldMapScale.LogicalScreenTilesY * 0.5f;
            float minX = layout.CameraBounds.min.x + halfW;
            float maxX = layout.CameraBounds.max.x - halfW;
            float minY = layout.CameraBounds.min.y + halfH;
            float maxY = layout.CameraBounds.max.y - halfH;
            float targetX = minX <= maxX
                ? Mathf.Clamp(playerPosition.x, minX, maxX) : layout.CameraBounds.center.x;
            float targetY = minY <= maxY
                ? Mathf.Clamp(playerPosition.y, minY, maxY) : layout.CameraBounds.center.y;
            float t = deltaSeconds > 0f ? 1f - Mathf.Exp(-8f * deltaSeconds) : 0f;
            return new Vector3(
                Mathf.Clamp(Mathf.Lerp(currentCameraPosition.x, targetX, t), minX, maxX),
                Mathf.Clamp(Mathf.Lerp(currentCameraPosition.y, targetY, t), minY, maxY),
                currentCameraPosition.z);
        }

        public string FindNearbyInteractableTriggerId()
        {
            if (disposed || parent == null || IsDialogueOpen)
                return null;

            string bestId = null;
            float bestDistSq = float.MaxValue;
            for (int i = 0; i < layout.Triggers.Count; i++)
            {
                RpgMapTriggerAnchor anchor = layout.Triggers[i];
                if (!CanOfferTrigger(anchor.Id))
                    continue;

                Vector2 center = new Vector2(anchor.Tile.x + 0.5f, anchor.Tile.y + 0.5f);
                float distSq = (playerPosition - center).sqrMagnitude;
                float radiusSq = anchor.RadiusTiles * anchor.RadiusTiles;
                if (distSq <= radiusSq && distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestId = anchor.Id;
                }
            }
            return bestId;
        }

        public bool TryInteractAtCurrentPosition()
        {
            if (disposed || parent == null || IsDialogueOpen)
                return false;

            string triggerId = FindNearbyInteractableTriggerId();
            if (string.IsNullOrEmpty(triggerId))
                return false;

            if (TryMapReconTrigger(triggerId, out M06MineCampLead lead))
                return TryOpenReconLead(lead);

            if (triggerId == "m06.pump-reunion")
                return TryOpenPumpRoomReunion();

            return false;
        }

        public void RefreshHud()
        {
            if (disposed || parent == null || hudCanvasObject == null ||
                districtBanner == null || promptBanner == null)
                return;

            PruneDisposedDialogs();

            // The established M05 landing is a live scene prerequisite;
            // do not leak Shen Li into an unverified visit.
            bool landed = HasVerifiedLanding();
            if (shenLiVisual != null && !shenLiVisual.IsDisposed && shenLiVisual.ActorObject != null)
                shenLiVisual.ActorObject.SetActive(landed);

            // Do not advertise active objectives from a prior, unverified
            // voyage. Recon completion reads only the Makinom facts, never
            // a shadow UI-progress store.
            if (minimapHud != null && !minimapHud.IsDisposed && minimapHud.Panel != null)
            {
                minimapHud.Panel.gameObject.SetActive(landed);
                minimapHud.UpdatePlayerPosition(playerPosition);
                M06MineCampReconFacts.TryRead(getInt, out int mask);
                for (int i = 0; i < 4; i++)
                {
                    M06MineCampLead lead = (M06MineCampLead)i;
                    string id;
                    switch (lead)
                    {
                        case M06MineCampLead.EscortSchedule:
                            id = "m06.recon.escort-schedule"; break;
                        case M06MineCampLead.GuardPatrol:
                            id = "m06.recon.guard-patrol"; break;
                        case M06MineCampLead.SupplyLedger:
                            id = "m06.recon.supply-ledger"; break;
                        default:
                            id = "m06.recon.mine-tunnel-pump"; break;
                    }
                    minimapHud.SetBeaconCompleted(id,
                        landed && M06MineCampReconFacts.Contains(mask, lead));
                }
                minimapHud.SetBeaconCompleted("m06.pump-reunion",
                    landed && (RescuePlanSettled ||
                        HasAnyPersistedRescuePlanFacts() ||
                        M06RescuePlanFacts.HasOrphanedDownstreamFacts(getInt)));
            }

            bool zh = SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese;
            Font font = SoundwellUiLocalization.PresentationFont(latinFont);
            districtBanner.font = promptBanner.font = font;

            RpgMapZone? zone = CurrentDistrict;
            string zoneTitle = zone.HasValue
                ? (zh ? zone.Value.DisplayNameZh : zone.Value.DisplayNameEn)
                : (zh ? "赤盐矿营连廊" : "Red Salt Mine Camp Corridor");

            string planStatus = RescuePlanSettled
                ? (zh ? "营救方案已定" : "Rescue Plan Settled")
                : (zh ? $"侦察 {ObservedReconCount}/4" : $"Recon {ObservedReconCount}/4");

            districtBanner.text = $"{zoneTitle} · {planStatus}";

            string nearby = FindNearbyInteractableTriggerId();
            promptBanner.text = string.IsNullOrEmpty(nearby)
                ? string.Empty
                : FormatTriggerPrompt(nearby, zh);
        }

        private bool HasVerifiedLanding()
        {
            return M05LandingFacts.TryRead(getInt, out M05LandingReport? saved) &&
                saved.HasValue;
        }

        private bool HasAnyPersistedRescuePlanFacts()
        {
            return getInt(M06RescuePlanFacts.ConfirmedKey) != 0 ||
                getInt(M06RescuePlanFacts.PendingSignatureKey) != 0 ||
                getInt(M06RescuePlanFacts.PlanKey) != 0 ||
                getInt(M06RescuePlanFacts.UsableCapacityKey) != 0 ||
                getInt(M06RescuePlanFacts.LetterDeliveryKey) != 0 ||
                getInt(M06RescuePlanFacts.RetaliationHeardKey) != 0 ||
                getInt(M06RescuePlanFacts.ShenResponseKey) != 0;
        }

        private bool CanOfferTrigger(string triggerId)
        {
            if (!HasVerifiedLanding() || getInt(M05LandingFacts.ArrivalSequenceKey) < 0)
                return false;

            if (TryMapReconTrigger(triggerId, out M06MineCampLead lead))
            {
                return M06MineCampReconFacts.TryRead(getInt, out int mask) &&
                    !M06MineCampReconFacts.Contains(mask, lead);
            }

            if (triggerId == "m06.pump-reunion")
                return !RescuePlanSettled &&
                    !HasAnyPersistedRescuePlanFacts() &&
                    !M06RescuePlanFacts.HasOrphanedDownstreamFacts(getInt);

            return false;
        }

        private bool TryOpenReconLead(M06MineCampLead lead)
        {
            if (parent == null)
                return false;
            int arrivalSequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            if (arrivalSequence < 0)
                return false;

            if (!M06MineCampReconSession.TryLoad(getInt, out M06MineCampReconSession session) ||
                session.IsLeadObserved(lead))
                return false;

            M06MineCampReconView openedView = null;
            openedView = new M06MineCampReconView(parent, lead, inspected =>
            {
                if (!CanSubmitDialog(() => activeReconView == openedView &&
                        openedView != null && !openedView.IsDisposed, arrivalSequence))
                    throw new InvalidOperationException(
                        "The M06 mine-camp recon belongs to a previous or invalid voyage.");

                if (!session.TryInspectLead(inspected, getInt, setInt))
                    throw new InvalidOperationException(
                        "Unable to persist the inspected M06 mine-camp reconnaissance lead.");

                if (activeReconView == openedView)
                {
                    openedView.Dispose();
                    activeReconView = null;
                }
                RefreshHud();
            });
            activeReconView = openedView;
            RefreshHud();
            return true;
        }

        private bool TryOpenPumpRoomReunion()
        {
            if (parent == null)
                return false;
            int arrivalSequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            if (arrivalSequence < 0 ||
                RescuePlanSettled ||
                HasAnyPersistedRescuePlanFacts() ||
                M06RescuePlanFacts.HasOrphanedDownstreamFacts(getInt) ||
                !M06PumpRoomReunionSession.TryCreateFromSavedFacts(
                    getInt, setInt, out M06PumpRoomReunionSession session))
                return false;

            M06PumpRoomReunionView openedView = null;
            openedView = new M06PumpRoomReunionView(parent, session, plan =>
            {
                if (!CanSubmitDialog(() => activeReunionView == openedView &&
                        openedView != null && !openedView.IsDisposed, arrivalSequence))
                    throw new InvalidOperationException(
                        "The M06 pump-room reunion belongs to a previous or invalid voyage.");

                if (!session.TryConfirmRescuePlan(plan, getInt, setInt))
                    throw new InvalidOperationException(
                        "Unable to persist the M06 rescue plan decision.");

                if (activeReunionView == openedView)
                {
                    openedView.Dispose();
                    activeReunionView = null;
                }
                RefreshHud();
            });
            activeReunionView = openedView;
            RefreshHud();
            return true;
        }

        // Fact readers may synchronously retire the scene or its dialogue.
        // Recheck the captured view after each external read before settlement.
        private bool CanSubmitDialog(Func<bool> isSameLiveView, int arrivalSequence)
        {
            bool IsLive() => !disposed && parent != null && isSameLiveView();
            return IsLive() &&
                getInt(M05LandingFacts.ArrivalSequenceKey) == arrivalSequence &&
                IsLive() && HasVerifiedLanding() && IsLive() &&
                getInt(M05LandingFacts.ArrivalSequenceKey) == arrivalSequence &&
                IsLive();
        }

        private static bool TryMapReconTrigger(string triggerId, out M06MineCampLead lead)
        {
            switch (triggerId)
            {
                case "m06.recon.escort-schedule":
                    lead = M06MineCampLead.EscortSchedule;
                    return true;
                case "m06.recon.guard-patrol":
                    lead = M06MineCampLead.GuardPatrol;
                    return true;
                case "m06.recon.supply-ledger":
                    lead = M06MineCampLead.SupplyLedger;
                    return true;
                case "m06.recon.mine-tunnel-pump":
                    lead = M06MineCampLead.MineTunnelAndPump;
                    return true;
                default:
                    lead = default;
                    return false;
            }
        }

        private static string FormatTriggerPrompt(string triggerId, bool zh)
        {
            switch (triggerId)
            {
                case "m06.pump-reunion":
                    return zh ? "[交互] 走近主排水泵与沈砾会合" : "[Interact] Meet Shen Li at the Main Pump";
                case "m06.recon.escort-schedule":
                    return zh ? "[交互] 查探押运窗口与登岛核验" : "[Interact] Inspect Escort Schedule";
                case "m06.recon.guard-patrol":
                    return zh ? "[交互] 观察看守与巡逻艇间隔" : "[Interact] Observe Guard Patrol";
                case "m06.recon.supply-ledger":
                    return zh ? "[交互] 核对物资账与盐箱货单" : "[Interact] Inspect Supply Ledger";
                case "m06.recon.mine-tunnel-pump":
                    return zh ? "[交互] 查勘矿道与主排水泵安全底线" : "[Interact] Inspect Mine Tunnels & Pump";
                default:
                    return zh ? "[交互] 查看" : "[Interact] Inspect";
            }
        }

        private bool IsWalkableWorld(float wx, float wy)
        {
            return IsWalkableTile(Mathf.FloorToInt(wx), Mathf.FloorToInt(wy));
        }

        private bool IsWalkableTile(int tx, int ty)
        {
            if (tx < 0 || tx >= layout.Width || ty < 0 || ty >= layout.Height)
                return false;
            return M06MineCampMapLayout.IsWalkable(layout.Tiles[tx, ty]);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            SoundwellUiLocalization.Changed -= RefreshHud;
            activeReconView?.Dispose();
            activeReconView = null;
            activeReunionView?.Dispose();
            activeReunionView = null;
            heroVisual?.Dispose();
            shenLiVisual?.Dispose();
            minimapHud?.Dispose();
            chunkRenderer?.Dispose();
            SoundwellUiTheme.SafeDestroyCanvas(hudCanvasObject);
        }
    }
}
