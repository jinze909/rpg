using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum RpgWorld500xModalKind : byte
    {
        None = 0,
        QuestJournal = 1,
        WorldAtlas = 2,
        CampMerchant = 3
    }

    /// <summary>
    /// Mutually-exclusive modal UI router uniting QuestJournal, WorldAtlas, and
    /// CampMerchant panels on top of RpgWorld500xSceneBootstrapper. Guarantees at
    /// most one modal is visible at a time and suppresses movement/world interaction
    /// while any modal is open.
    /// </summary>
    public sealed class RpgWorld500xMenuRouter : IDisposable
    {
        private readonly RpgWorld500xSceneBootstrapper _bootstrapper;
        private readonly RpgWorld500xWorldAtlasHudPanel _atlasPanel;
        private readonly RpgWorld500xCampMerchantHudPanel _merchantPanel;
        private bool _disposed;

        public RpgWorld500xSceneBootstrapper Bootstrapper => _bootstrapper;
        public RpgWorld500xWorldAtlasHudPanel AtlasPanel => _atlasPanel;
        public RpgWorld500xCampMerchantHudPanel MerchantPanel => _merchantPanel;
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
        }
    }
}
