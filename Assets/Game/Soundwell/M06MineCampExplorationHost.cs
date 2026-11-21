using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Playable M06 ("她没有在原地等你") Red Salt Mine Camp exploration host
    /// connecting the 96x64 4x-scale M06MineCampMapLayout with physical trigger
    /// discovery for the four M06MineCampReconView leads and the underground
    /// M06PumpRoomReunionView decision, strictly gated on verified M05 landing.
    /// </summary>
    public sealed class M06MineCampExplorationHost : IDisposable
    {
        public const float MoveSpeedTilesPerSecond = 6.5f;

        private readonly Transform parent;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private readonly M06MineCampMapLayout layout;
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
        public Vector2 PlayerPosition => playerPosition;
        public Vector2 Facing => facing;
        public RpgMapZone? CurrentDistrict => layout.FindDistrictAtTile(CurrentTile);
        public Vector2Int CurrentTile => new Vector2Int(
            Mathf.FloorToInt(playerPosition.x),
            Mathf.FloorToInt(playerPosition.y));
        public M06MineCampReconView ActiveReconView => activeReconView;
        public M06PumpRoomReunionView ActiveReunionView => activeReunionView;
        public bool IsDialogueOpen => activeReconView != null || activeReunionView != null;
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
            if (disposed || !IsWalkableTile(tile.x, tile.y))
                return false;

            playerPosition = new Vector2(tile.x + 0.5f, tile.y + 0.5f);
            RefreshHud();
            return true;
        }

        public bool StepMovement(Vector2 input, float deltaSeconds)
        {
            if (disposed || IsDialogueOpen || deltaSeconds <= 0f)
                return false;

            if (input.sqrMagnitude < 0.0001f)
                return false;

            Vector2 dir = input.normalized;
            facing = dir;
            Vector2 delta = dir * (MoveSpeedTilesPerSecond * deltaSeconds);
            // The final destination alone may be walkable even when a wall
            // separates it from the player. Check short intermediate steps so
            // a slow frame cannot skip the mine-camp walls or sealed rooms.
            // Preserve independent axis movement for wall sliding.
            int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / 0.35f));
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
                RefreshHud();
            }
            return moved;
        }

        public string FindNearbyInteractableTriggerId()
        {
            if (disposed || IsDialogueOpen)
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
            if (disposed || IsDialogueOpen)
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
            if (disposed || hudCanvasObject == null)
                return;

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

        private bool CanOfferTrigger(string triggerId)
        {
            if (!HasVerifiedLanding())
                return false;

            if (TryMapReconTrigger(triggerId, out M06MineCampLead lead))
            {
                return M06MineCampReconFacts.TryRead(getInt, out int mask) &&
                    !M06MineCampReconFacts.Contains(mask, lead);
            }

            if (triggerId == "m06.pump-reunion")
                return !RescuePlanSettled;

            return false;
        }

        private bool TryOpenReconLead(M06MineCampLead lead)
        {
            if (!M06MineCampReconSession.TryLoad(getInt, out M06MineCampReconSession session) ||
                session.IsLeadObserved(lead))
                return false;

            activeReconView = new M06MineCampReconView(parent, lead, inspected =>
            {
                if (!session.TryInspectLead(inspected, getInt, setInt))
                    throw new InvalidOperationException(
                        "Unable to persist the inspected M06 mine-camp reconnaissance lead.");

                activeReconView?.Dispose();
                activeReconView = null;
                RefreshHud();
            });
            RefreshHud();
            return true;
        }

        private bool TryOpenPumpRoomReunion()
        {
            if (RescuePlanSettled ||
                !M06PumpRoomReunionSession.TryCreateFromSavedFacts(
                    getInt, out M06PumpRoomReunionSession session))
                return false;

            activeReunionView = new M06PumpRoomReunionView(parent, session, plan =>
            {
                if (!session.TryConfirmRescuePlan(plan, getInt, setInt))
                    throw new InvalidOperationException(
                        "Unable to persist the M06 rescue plan decision.");

                activeReunionView?.Dispose();
                activeReunionView = null;
                RefreshHud();
            });
            RefreshHud();
            return true;
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
            SoundwellUiTheme.SafeDestroyCanvas(hudCanvasObject);
        }
    }
}
