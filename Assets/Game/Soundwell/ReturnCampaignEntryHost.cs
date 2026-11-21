using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum ReturnCampaignEntryMode
    {
        None = 0,
        MidgameFlow = 1,
        StoryLedger = 2,
        RosterCase = 3,
        RegionalStory = 4,
        FinaleFlow = 5
    }

    /// <summary>
    /// Unified post-M07 campaign entry host bridging ReturnMidgameFlowHost
    /// (M08-M09 + optional M08 dock truth) with ReturnHarborCommandHubHost
    /// (Story Ledger, RC-01..RC-05, ST-02/03/05/06, and M10-M16 Finale Flow).
    /// Enforces strict single-active-panel mutual exclusion across all modes and
    /// guarantees any active UI is closed before executing a new-voyage reset.
    /// </summary>
    public sealed class ReturnCampaignEntryHost : IDisposable
    {
        private readonly ReturnMidgameFlowHost midgameHub;
        private readonly ReturnHarborCommandHubHost commandHub;
        private readonly Action<string, int> setInt;
        private bool disposed;

        public ReturnMidgameFlowHost MidgameHub => midgameHub;
        public ReturnHarborCommandHubHost CommandHub => commandHub;
        public bool IsDisposed => disposed;

        public ReturnCampaignEntryMode ActiveMode
        {
            get
            {
                if (midgameHub.HasActiveHost)
                    return ReturnCampaignEntryMode.MidgameFlow;
                switch (commandHub.ActiveMode)
                {
                    case ReturnCommandHubMode.StoryLedger:
                        return ReturnCampaignEntryMode.StoryLedger;
                    case ReturnCommandHubMode.RosterCase:
                        return ReturnCampaignEntryMode.RosterCase;
                    case ReturnCommandHubMode.RegionalStory:
                        return ReturnCampaignEntryMode.RegionalStory;
                    case ReturnCommandHubMode.FinaleFlow:
                        return ReturnCampaignEntryMode.FinaleFlow;
                    default:
                        return ReturnCampaignEntryMode.None;
                }
            }
        }

        public bool HasAnyActivePanel => ActiveMode != ReturnCampaignEntryMode.None;

        public ReturnCampaignEntryHost(
            Transform uiParent,
            Func<string, int> getInt,
            Action<string, int> setInt,
            ReturnMidgameFlowContext midgameContext = null,
            ReturnRosterCasesHubContext rosterContext = null,
            ReturnRegionalStoriesHubContext regionalContext = null,
            ReturnFinaleFlowContext finaleContext = null)
        {
            if (uiParent == null) throw new ArgumentNullException(nameof(uiParent));
            if (getInt == null) throw new ArgumentNullException(nameof(getInt));
            this.setInt = setInt ?? throw new ArgumentNullException(nameof(setInt));

            midgameHub = new ReturnMidgameFlowHost(
                uiParent, midgameContext ?? new ReturnMidgameFlowContext(), getInt, setInt);
            commandHub = new ReturnHarborCommandHubHost(
                uiParent, getInt, setInt, rosterContext, regionalContext, finaleContext);
        }

        public bool TryBuildProgressSnapshot(out ReturnStoryProgressSnapshot snapshot)
        {
            snapshot = default;
            return !disposed && commandHub.TryBuildProgressSnapshot(out snapshot);
        }

        /// <summary>
        /// Automatically routes to the next required main-story chapter across
        /// M08-M09 (via ReturnMidgameFlowHost) and M10-M16 (via ReturnFinaleFlowHost).
        /// Fails closed before M07 or when an unrelated panel is already open.
        /// </summary>
        public bool TryOpenNextMainStoryStage()
        {
            if (disposed)
                return false;

            ReturnMidgameStage midNext = midgameHub.EvaluateNextRequiredStage();
            if (midNext == ReturnMidgameStage.None)
                return false;

            if (midNext != ReturnMidgameStage.ReadyForActThree)
            {
                if (commandHub.HasAnyActivePanel)
                    return false;
                return midgameHub.TryOpenNextRequiredStage();
            }

            if (midgameHub.HasActiveHost)
                return false;
            return commandHub.TryOpenNextFinaleStage();
        }

        public bool TryOpenOptionalM08DockTruth()
        {
            if (disposed || commandHub.HasAnyActivePanel)
                return false;
            return midgameHub.TryOpenOptionalDockTruth();
        }

        public bool TryOpenStoryLedger(Action onClosed = null)
        {
            if (disposed || midgameHub.HasActiveHost)
                return false;
            return commandHub.TryOpenStoryLedger(onClosed);
        }

        public bool TryOpenRosterCase(ReturnRosterCaseId caseId)
        {
            if (disposed || midgameHub.HasActiveHost)
                return false;
            return commandHub.TryOpenRosterCase(caseId);
        }

        public bool TryOpenRegionalStory(ReturnRegionalStoryId storyId)
        {
            if (disposed || midgameHub.HasActiveHost)
                return false;
            return commandHub.TryOpenRegionalStory(storyId);
        }

        public void CloseActivePanel()
        {
            midgameHub.CloseActiveHost();
            commandHub.CloseActivePanel();
        }

        /// <summary>
        /// Closes any open story UI before resetting all downstream voyage-bound
        /// facts (M06-M16, RC-01..RC-05, ST-06) so stale panels from a prior voyage
        /// can never commit into a fresh replay. Permanent regional tales (ST-02,
        /// ST-03, ST-05) are preserved.
        /// </summary>
        public bool TryStartNewVoyageReset()
        {
            if (disposed)
                return false;
            CloseActivePanel();
            return ReturnStoryLedger.TryResetAllDownstreamVoyageFacts(setInt);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            midgameHub.Dispose();
            commandHub.Dispose();
        }
    }
}
