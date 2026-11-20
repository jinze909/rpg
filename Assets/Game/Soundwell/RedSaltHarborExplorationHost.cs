using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    /// <summary>
    /// End-to-end playable 4x Red Salt Outer Harbor exploration host (140x84 tiles).
    /// Connects RpgLargeMapChunkRenderer + RpgLargeMapExplorerController + a
    /// compact district/interact HUD + physical proximity triggers that open the
    /// delivered M05LandingCheckView, M05SeranLettersView, and M05HarborWalkView
    /// scenes without fabricating arrivals or overlapping canvases.
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
        private readonly GameObject playerAvatar;
        private readonly GameObject hudCanvas;
        private readonly Text districtLabel;
        private readonly GameObject promptBox;
        private readonly Text promptLabel;
        private readonly Font latinFont;

        private M05LandingCheckView activeLandingView;
        private M05SeranLettersView activeSeranView;
        private M05HarborWalkView activeHarborWalkView;
        private readonly bool[] visitedHarborBeats = new bool[(int)M05HarborBeat.Finished];
        private bool harborWalkCompleted;
        private bool disposed;

        public RedSaltHarborMapLayout Layout => layout;
        public RpgLargeMapChunkRenderer ChunkRenderer => chunkRenderer;
        public RpgLargeMapExplorerController Explorer => explorer;
        public RpgLargeMapMinimapAndBeaconHud MinimapHud => minimapHud;
        public Vector2 PlayerPosition => explorer.Position;
        public bool IsDialogueOpen =>
            activeLandingView != null || activeSeranView != null || activeHarborWalkView != null;
        public M05LandingCheckView ActiveLandingView => activeLandingView;
        public M05SeranLettersView ActiveSeranView => activeSeranView;
        public M05HarborWalkView ActiveHarborWalkView => activeHarborWalkView;
        public bool HarborWalkCompleted => harborWalkCompleted;
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

            layout = RedSaltHarborMapLayout.Build();
            chunkRenderer = RpgLargeMapChunkRenderer.BuildForRedSaltHarbor(
                parent, layout, harborTileSlices);
            explorer = RpgLargeMapExplorerController.ForRedSaltHarbor(layout);
            explorer.ZoneEntered += _ => RefreshHud();
            minimapHud = RpgLargeMapMinimapAndBeaconHud.BuildForRedSaltHarbor(parent, layout);

            playerAvatar = new GameObject("RedSaltPlayerAvatar");
            playerAvatar.transform.SetParent(chunkRenderer.RootObject.transform, false);
            playerAvatar.transform.localPosition = new Vector3(
                explorer.Position.x, explorer.Position.y, 0f);

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

        public bool StepExploration(Vector2 moveInput, float deltaTime)
        {
            if (disposed || IsDialogueOpen)
                return false;

            bool moved = explorer.StepMovement(moveInput, deltaTime);
            if (moved && playerAvatar != null)
            {
                playerAvatar.transform.localPosition = new Vector3(
                    explorer.Position.x, explorer.Position.y, 0f);
                minimapHud.UpdatePlayerPosition(explorer.Position);
            }
            RefreshHud();
            return moved;
        }

        public Vector3 StepCamera(Vector3 currentCameraPos, float deltaTime)
        {
            if (disposed) return currentCameraPos;
            return explorer.ComputeClampedCameraPosition(currentCameraPos, deltaTime);
        }

        public bool TryInteractAtCurrentPosition()
        {
            if (disposed || IsDialogueOpen)
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
            if (!CanConfirmObservedLanding())
                return false;

            activeLandingView = new M05LandingCheckView(
                parent, observedLanding.Value, report =>
                {
                    if (!M05LandingFacts.TryRecord(report, getInt, setInt))
                        throw new InvalidOperationException("Unable to persist M05 landing report.");
                    activeLandingView?.Dispose();
                    activeLandingView = null;
                    RefreshHud();
                });
            RefreshHud();
            return true;
        }

        private bool TryOpenSeranLetters()
        {
            // Requires a verified arrival and rejects corrupt nonzero letter codes.
            if (!M05LandingFacts.TryRead(getInt, out M05LandingReport? savedLanding) ||
                !savedLanding.HasValue)
                return false;
            if (getInt(M05LettersFacts.DecisionKey) != 0)
                return false;

            activeSeranView = new M05SeranLettersView(parent, decision =>
            {
                if (!M05LettersFacts.TryRecord(decision, getInt, setInt))
                    throw new InvalidOperationException("Unable to persist M05 Seran letter choice.");
                activeSeranView?.Dispose();
                activeSeranView = null;
                RefreshHud();
            });
            RefreshHud();
            return true;
        }

        private bool HasVerifiedLanding()
        {
            return M05LandingFacts.TryRead(getInt, out M05LandingReport? saved) &&
                saved.HasValue;
        }

        private bool TryOpenHarborWalk(M05HarborBeat beat)
        {
            if (visitedHarborBeats[(int)beat] || !HasVerifiedLanding())
                return false;

            activeHarborWalkView = new M05HarborWalkView(parent, () =>
            {
                visitedHarborBeats[(int)beat] = true;
                harborWalkCompleted = visitedHarborBeats[0] &&
                    visitedHarborBeats[1] && visitedHarborBeats[2];
                activeHarborWalkView?.Dispose();
                activeHarborWalkView = null;
                RefreshHud();
            }, beat);
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
                    return HasVerifiedLanding() && !visitedHarborBeats[0];
                case "m05.fisher-rumor":
                    return HasVerifiedLanding() && !visitedHarborBeats[1];
                case "m05.salt-market":
                    return HasVerifiedLanding() && !visitedHarborBeats[2];
                default:
                    return false;
            }
        }

        public void RefreshHud()
        {
            if (disposed || hudCanvas == null) return;

            // Beacon colors are presentation only. Authoritative Makinom
            // landing/letter decisions and this map's actually witnessed
            // location beats determine which goals have been completed.
            minimapHud?.SetBeaconCompleted("m05.landing-check", HasVerifiedLanding());
            bool letterDecisionRecorded = M05LettersFacts.TryRead(getInt,
                out M05LetterDecision? recordedLetters) && recordedLetters.HasValue;
            minimapHud?.SetBeaconCompleted("m05.seran-letters", letterDecisionRecorded);
            minimapHud?.SetBeaconCompleted("m05.repair-stall", visitedHarborBeats[0]);
            minimapHud?.SetBeaconCompleted("m05.fisher-rumor", visitedHarborBeats[1]);
            minimapHud?.SetBeaconCompleted("m05.salt-market", visitedHarborBeats[2]);

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
                case "m06.mine-camp-gate":
                    return zh ? "[交互] 矿营入山口（前往下一章）" : "[Interact] Mine Camp Gate (Next Chapter)";
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
            minimapHud?.Dispose();
            chunkRenderer?.Dispose();
            SoundwellUiTheme.SafeDestroyCanvas(hudCanvas);
        }
    }
}
