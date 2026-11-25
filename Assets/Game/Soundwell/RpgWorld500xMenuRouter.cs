using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum RpgWorld500xModalKind : byte
    {
        None = 0,
        QuestJournal = 1,
        WorldAtlas = 2,
        CampMerchant = 3,
        Bestiary = 4,
        FastTravelBeacons = 5,
        CartographyReputation = 6,
        SaveSlots = 7,
        FieldRumorAndLore = 8,
        ExpeditionMilestones = 9,
        CacheAndShortcut = 10,
        ExpeditionDigest = 11,
        BountyBoard = 12,
        ExpeditionReadiness = 13,
        DangerTacticalBrief = 14,
        WorldGazetteer = 15,
        TravelItinerary = 16
    }

    /// <summary>
    /// Mutually-exclusive 16-panel modal UI router uniting QuestJournal, WorldAtlas,
    /// CampMerchant, Bestiary, FastTravelBeacons, CartographyReputation, SaveSlots,
    /// FieldRumorAndLore, ExpeditionMilestones, CacheAndShortcut, ExpeditionDigest,
    /// BountyBoard, ExpeditionReadiness, DangerTacticalBrief, WorldGazetteer, and
    /// TravelItinerary panels on top of RpgWorld500xSceneBootstrapper. Guarantees at
    /// most one modal is visible at a time and suppresses movement/world interaction
    /// while any modal is open.
    /// </summary>
    public sealed class RpgWorld500xMenuRouter : IDisposable
    {
        private readonly RpgWorld500xSceneBootstrapper _bootstrapper;
        private readonly RpgWorld500xWorldAtlasHudPanel _atlasPanel;
        private readonly RpgWorld500xCampMerchantHudPanel _merchantPanel;
        private readonly RpgWorld500xBestiaryHudPanel _bestiaryPanel;
        private readonly RpgWorld500xFastTravelBeaconHudPanel _beaconWarpPanel;
        private readonly RpgWorld500xCartographyReputationHudPanel _cartographyPanel;
        private readonly RpgWorld500xSaveSlotHudPanel _saveSlotPanel;
        private readonly RpgWorld500xFieldRumorAndLoreHudPanel _rumorLorePanel;
        private readonly RpgWorld500xExpeditionMilestonesHudPanel _milestonesPanel;
        private readonly RpgWorld500xCacheAndShortcutHudPanel _cacheShortcutPanel;
        private readonly RpgWorld500xExpeditionDigestHudPanel _expeditionDigestPanel;
        private readonly RpgWorld500xBountyBoardHudPanel _bountyBoardPanel;
        private readonly RpgWorld500xExpeditionReadinessHudPanel _readinessPanel;
        private readonly RpgWorld500xDangerTacticalBriefHudPanel _dangerBriefPanel;
        private readonly RpgWorld500xWorldGazetteerHudPanel _gazetteerPanel;
        private readonly RpgWorld500xTravelItineraryHudPanel _itineraryPanel;
        private readonly bool _previousAutomaticFrameUpdates;
        private bool _disposed;

        public RpgWorld500xSceneBootstrapper Bootstrapper => _bootstrapper;
        public RpgWorld500xWorldAtlasHudPanel AtlasPanel => _atlasPanel;
        public RpgWorld500xCampMerchantHudPanel MerchantPanel => _merchantPanel;
        public RpgWorld500xBestiaryHudPanel BestiaryPanel => _bestiaryPanel;
        public RpgWorld500xFastTravelBeaconHudPanel BeaconWarpPanel => _beaconWarpPanel;
        public RpgWorld500xCartographyReputationHudPanel CartographyPanel => _cartographyPanel;
        public RpgWorld500xSaveSlotHudPanel SaveSlotPanel => _saveSlotPanel;
        public RpgWorld500xFieldRumorAndLoreHudPanel RumorLorePanel => _rumorLorePanel;
        public RpgWorld500xExpeditionMilestonesHudPanel MilestonesPanel => _milestonesPanel;
        public RpgWorld500xCacheAndShortcutHudPanel CacheShortcutPanel => _cacheShortcutPanel;
        public RpgWorld500xExpeditionDigestHudPanel ExpeditionDigestPanel => _expeditionDigestPanel;
        public RpgWorld500xBountyBoardHudPanel BountyBoardPanel => _bountyBoardPanel;
        public RpgWorld500xExpeditionReadinessHudPanel ReadinessPanel => _readinessPanel;
        public RpgWorld500xDangerTacticalBriefHudPanel DangerBriefPanel => _dangerBriefPanel;
        public RpgWorld500xWorldGazetteerHudPanel GazetteerPanel => _gazetteerPanel;
        public RpgWorld500xTravelItineraryHudPanel ItineraryPanel => _itineraryPanel;
        public bool IsDisposed => _disposed || _bootstrapper.IsDisposed;

        public RpgWorld500xModalKind ActiveModal
        {
            get
            {
                if (IsDisposed)
                {
                    return RpgWorld500xModalKind.None;
                }

                RpgWorld500xQuestJournalPanel journal = _bootstrapper.Driver.Facade.Session.JournalPanel;
                if (journal != null && journal.IsVisible)
                {
                    return RpgWorld500xModalKind.QuestJournal;
                }

                if (_atlasPanel != null && _atlasPanel.IsVisible)
                {
                    return RpgWorld500xModalKind.WorldAtlas;
                }

                if (_merchantPanel != null && _merchantPanel.IsVisible)
                {
                    return RpgWorld500xModalKind.CampMerchant;
                }

                if (_bestiaryPanel != null && _bestiaryPanel.IsVisible)
                {
                    return RpgWorld500xModalKind.Bestiary;
                }

                if (_beaconWarpPanel != null && _beaconWarpPanel.IsVisible)
                {
                    return RpgWorld500xModalKind.FastTravelBeacons;
                }

                if (_cartographyPanel != null && _cartographyPanel.IsVisible)
                {
                    return RpgWorld500xModalKind.CartographyReputation;
                }

                if (_saveSlotPanel != null && _saveSlotPanel.IsVisible)
                {
                    return RpgWorld500xModalKind.SaveSlots;
                }

                if (_rumorLorePanel != null && _rumorLorePanel.IsVisible)
                {
                    return RpgWorld500xModalKind.FieldRumorAndLore;
                }

                if (_milestonesPanel != null && _milestonesPanel.IsVisible)
                {
                    return RpgWorld500xModalKind.ExpeditionMilestones;
                }

                if (_cacheShortcutPanel != null && _cacheShortcutPanel.IsVisible)
                {
                    return RpgWorld500xModalKind.CacheAndShortcut;
                }

                if (_expeditionDigestPanel != null && _expeditionDigestPanel.IsVisible)
                {
                    return RpgWorld500xModalKind.ExpeditionDigest;
                }

                if (_bountyBoardPanel != null && _bountyBoardPanel.IsVisible)
                {
                    return RpgWorld500xModalKind.BountyBoard;
                }

                if (_readinessPanel != null && _readinessPanel.IsVisible)
                {
                    return RpgWorld500xModalKind.ExpeditionReadiness;
                }

                if (_dangerBriefPanel != null && _dangerBriefPanel.IsVisible)
                {
                    return RpgWorld500xModalKind.DangerTacticalBrief;
                }

                if (_gazetteerPanel != null && _gazetteerPanel.IsVisible)
                {
                    return RpgWorld500xModalKind.WorldGazetteer;
                }

                if (_itineraryPanel != null && _itineraryPanel.IsVisible)
                {
                    return RpgWorld500xModalKind.TravelItinerary;
                }

                return RpgWorld500xModalKind.None;
            }
        }

        public bool HasAnyModalOpen => ActiveModal != RpgWorld500xModalKind.None;

        public RpgWorld500xMenuRouter(
            Transform uiParent,
            RpgWorld500xSceneBootstrapper bootstrapper,
            RpgWorld500xCampHavenService campHaven,
            RpgWorld500xSaveSlotManager saveSlotManager = null)
        {
            _bootstrapper = bootstrapper ?? throw new ArgumentNullException(nameof(bootstrapper));
            if (campHaven == null)
            {
                throw new ArgumentNullException(nameof(campHaven));
            }

            var resolvedSaveSlots = saveSlotManager ?? new RpgWorld500xSaveSlotManager();
            _atlasPanel = new RpgWorld500xWorldAtlasHudPanel(uiParent, _bootstrapper, campHaven, resolvedSaveSlots);
            _merchantPanel = new RpgWorld500xCampMerchantHudPanel(uiParent, _bootstrapper, campHaven);
            _bestiaryPanel = new RpgWorld500xBestiaryHudPanel(uiParent, _bootstrapper);
            _beaconWarpPanel = new RpgWorld500xFastTravelBeaconHudPanel(uiParent, _bootstrapper);
            _cartographyPanel = new RpgWorld500xCartographyReputationHudPanel(uiParent, _bootstrapper);
            _saveSlotPanel = new RpgWorld500xSaveSlotHudPanel(uiParent, _bootstrapper, resolvedSaveSlots);
            _rumorLorePanel = new RpgWorld500xFieldRumorAndLoreHudPanel(uiParent, _bootstrapper);
            _milestonesPanel = new RpgWorld500xExpeditionMilestonesHudPanel(uiParent, _bootstrapper);
            _cacheShortcutPanel = new RpgWorld500xCacheAndShortcutHudPanel(uiParent, _bootstrapper);
            _expeditionDigestPanel = new RpgWorld500xExpeditionDigestHudPanel(uiParent, _bootstrapper);
            _bountyBoardPanel = new RpgWorld500xBountyBoardHudPanel(uiParent, _bootstrapper);
            _readinessPanel = new RpgWorld500xExpeditionReadinessHudPanel(uiParent, _bootstrapper);
            _dangerBriefPanel = new RpgWorld500xDangerTacticalBriefHudPanel(uiParent, _bootstrapper);
            _gazetteerPanel = new RpgWorld500xWorldGazetteerHudPanel(uiParent, _bootstrapper);
            _itineraryPanel = new RpgWorld500xTravelItineraryHudPanel(uiParent, _bootstrapper);

            // The routed frame is the only frame allowed to advance the world while
            // menus exist; otherwise RuntimeDriver.Update bypasses the modal guard.
            _previousAutomaticFrameUpdates = _bootstrapper.Driver.AutomaticFrameUpdates;
            _bootstrapper.Driver.AutomaticFrameUpdates = false;
        }

        public bool OpenModal(RpgWorld500xModalKind kind)
        {
            // Reject stale or corrupted serialized modal IDs before closing
            // the current panel; opening an unknown kind must be a no-op.
            if (IsDisposed || !Enum.IsDefined(typeof(RpgWorld500xModalKind), kind))
            {
                return false;
            }

            CloseAll();
            switch (kind)
            {
                case RpgWorld500xModalKind.QuestJournal:
                    RpgWorld500xQuestJournalPanel journal = _bootstrapper.Driver.Facade.Session.JournalPanel;
                    return journal != null && journal.Show();
                case RpgWorld500xModalKind.WorldAtlas:
                    return _atlasPanel.Show();
                case RpgWorld500xModalKind.CampMerchant:
                    return _merchantPanel.Show();
                case RpgWorld500xModalKind.Bestiary:
                    return _bestiaryPanel.Show();
                case RpgWorld500xModalKind.FastTravelBeacons:
                    return _beaconWarpPanel.Show();
                case RpgWorld500xModalKind.CartographyReputation:
                    return _cartographyPanel.Show();
                case RpgWorld500xModalKind.SaveSlots:
                    return _saveSlotPanel.Show();
                case RpgWorld500xModalKind.FieldRumorAndLore:
                    return _rumorLorePanel.Show();
                case RpgWorld500xModalKind.ExpeditionMilestones:
                    return _milestonesPanel.Show();
                case RpgWorld500xModalKind.CacheAndShortcut:
                    return _cacheShortcutPanel.Show();
                case RpgWorld500xModalKind.ExpeditionDigest:
                    return _expeditionDigestPanel.Show();
                case RpgWorld500xModalKind.BountyBoard:
                    return _bountyBoardPanel.Show();
                case RpgWorld500xModalKind.ExpeditionReadiness:
                    return _readinessPanel.Show();
                case RpgWorld500xModalKind.DangerTacticalBrief:
                    return _dangerBriefPanel.Show();
                case RpgWorld500xModalKind.WorldGazetteer:
                    return _gazetteerPanel.Show();
                case RpgWorld500xModalKind.TravelItinerary:
                    return _itineraryPanel.Show();
                case RpgWorld500xModalKind.None:
                default:
                    return true;
            }
        }

        public bool ToggleModal(RpgWorld500xModalKind kind)
        {
            if (IsDisposed || kind == RpgWorld500xModalKind.None)
            {
                return false;
            }

            if (ActiveModal == kind)
            {
                CloseAll();
                return true;
            }

            return OpenModal(kind);
        }

        public void CloseAll()
        {
            if (IsDisposed)
            {
                return;
            }

            RpgWorld500xQuestJournalPanel journal = _bootstrapper.Driver.Facade.Session.JournalPanel;
            if (journal != null && journal.IsVisible)
            {
                journal.Hide();
            }

            if (_atlasPanel != null && _atlasPanel.IsVisible)
            {
                _atlasPanel.Hide();
            }

            if (_merchantPanel != null && _merchantPanel.IsVisible)
            {
                _merchantPanel.Hide();
            }

            if (_bestiaryPanel != null && _bestiaryPanel.IsVisible)
            {
                _bestiaryPanel.Hide();
            }

            if (_beaconWarpPanel != null && _beaconWarpPanel.IsVisible)
            {
                _beaconWarpPanel.Hide();
            }

            if (_cartographyPanel != null && _cartographyPanel.IsVisible)
            {
                _cartographyPanel.Hide();
            }

            if (_saveSlotPanel != null && _saveSlotPanel.IsVisible)
            {
                _saveSlotPanel.Hide();
            }

            if (_rumorLorePanel != null && _rumorLorePanel.IsVisible)
            {
                _rumorLorePanel.Hide();
            }

            if (_milestonesPanel != null && _milestonesPanel.IsVisible)
            {
                _milestonesPanel.Hide();
            }

            if (_cacheShortcutPanel != null && _cacheShortcutPanel.IsVisible)
            {
                _cacheShortcutPanel.Hide();
            }

            if (_expeditionDigestPanel != null && _expeditionDigestPanel.IsVisible)
            {
                _expeditionDigestPanel.Hide();
            }

            if (_bountyBoardPanel != null && _bountyBoardPanel.IsVisible)
            {
                _bountyBoardPanel.Hide();
            }

            if (_readinessPanel != null && _readinessPanel.IsVisible)
            {
                _readinessPanel.Hide();
            }

            if (_dangerBriefPanel != null && _dangerBriefPanel.IsVisible)
            {
                _dangerBriefPanel.Hide();
            }

            if (_gazetteerPanel != null && _gazetteerPanel.IsVisible)
            {
                _gazetteerPanel.Hide();
            }

            if (_itineraryPanel != null && _itineraryPanel.IsVisible)
            {
                _itineraryPanel.Hide();
            }
        }

        public RpgWorld500xEffectiveExplorationModifiers StepFrameWithModalGuard(
            Vector2 moveInput,
            float deltaSeconds,
            bool sprinting = false,
            bool interactPressed = false)
        {
            if (IsDisposed)
            {
                return default;
            }

            if (HasAnyModalOpen)
            {
                return _bootstrapper.StepFrame(Vector2.zero, 0f, false, false, false, false);
            }

            return _bootstrapper.StepFrame(moveInput, deltaSeconds, sprinting, interactPressed, false, false);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            // The scene owns the quest journal; unlike the other panels it
            // survives router disposal. Hide it before restoring world input.
            CloseAll();
            _disposed = true;
            _atlasPanel?.Dispose();
            _merchantPanel?.Dispose();
            _bestiaryPanel?.Dispose();
            _beaconWarpPanel?.Dispose();
            _cartographyPanel?.Dispose();
            _saveSlotPanel?.Dispose();
            _rumorLorePanel?.Dispose();
            _milestonesPanel?.Dispose();
            _cacheShortcutPanel?.Dispose();
            _expeditionDigestPanel?.Dispose();
            _bountyBoardPanel?.Dispose();
            _readinessPanel?.Dispose();
            _dangerBriefPanel?.Dispose();
            _gazetteerPanel?.Dispose();
            _itineraryPanel?.Dispose();
            if (_bootstrapper.Driver != null)
            {
                _bootstrapper.Driver.AutomaticFrameUpdates = _previousAutomaticFrameUpdates;
            }
        }
    }
}
