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
        FastTravelBeacons = 5
    }

    /// <summary>
    /// Mutually-exclusive modal UI router uniting QuestJournal, WorldAtlas,
    /// CampMerchant, Bestiary, and FastTravelBeacons panels on top of
    /// RpgWorld500xSceneBootstrapper. Guarantees at most one modal is visible at
    /// a time and suppresses movement/world interaction while any modal is open.
    /// </summary>
    public sealed class RpgWorld500xMenuRouter : IDisposable
    {
        private readonly RpgWorld500xSceneBootstrapper _bootstrapper;
        private readonly RpgWorld500xWorldAtlasHudPanel _atlasPanel;
        private readonly RpgWorld500xCampMerchantHudPanel _merchantPanel;
        private readonly RpgWorld500xBestiaryHudPanel _bestiaryPanel;
        private readonly RpgWorld500xFastTravelBeaconHudPanel _beaconWarpPanel;
        private bool _disposed;

        public RpgWorld500xSceneBootstrapper Bootstrapper => _bootstrapper;
        public RpgWorld500xWorldAtlasHudPanel AtlasPanel => _atlasPanel;
        public RpgWorld500xCampMerchantHudPanel MerchantPanel => _merchantPanel;
        public RpgWorld500xBestiaryHudPanel BestiaryPanel => _bestiaryPanel;
        public RpgWorld500xFastTravelBeaconHudPanel BeaconWarpPanel => _beaconWarpPanel;
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

                return RpgWorld500xModalKind.None;
            }
        }

        public bool HasAnyModalOpen => ActiveModal != RpgWorld500xModalKind.None;

        public RpgWorld500xMenuRouter(
            Transform uiParent,
            RpgWorld500xSceneBootstrapper bootstrapper,
            RpgWorld500xCampHavenService campHaven)
        {
            _bootstrapper = bootstrapper ?? throw new ArgumentNullException(nameof(bootstrapper));
            if (campHaven == null)
            {
                throw new ArgumentNullException(nameof(campHaven));
            }

            _atlasPanel = new RpgWorld500xWorldAtlasHudPanel(uiParent, _bootstrapper, campHaven);
            _merchantPanel = new RpgWorld500xCampMerchantHudPanel(uiParent, _bootstrapper, campHaven);
            _bestiaryPanel = new RpgWorld500xBestiaryHudPanel(uiParent, _bootstrapper);
            _beaconWarpPanel = new RpgWorld500xFastTravelBeaconHudPanel(uiParent, _bootstrapper);
        }

        public bool OpenModal(RpgWorld500xModalKind kind)
        {
            if (IsDisposed)
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

            _disposed = true;
            _atlasPanel?.Dispose();
            _merchantPanel?.Dispose();
            _bestiaryPanel?.Dispose();
            _beaconWarpPanel?.Dispose();
        }
    }
}
