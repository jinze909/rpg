using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum ReturnCommandHubMode
    {
        None = 0,
        StoryLedger = 1,
        RosterCase = 2,
        RegionalStory = 3,
        FinaleFlow = 4
    }

    /// <summary>
    /// Top-level scene-callable Linchao Harbor / World-Map command hub coordinator.
    /// Owns and enforces strict single-active-panel mutual exclusion across:
    /// - ReturnStoryLedgerHost (progress &amp; journal overlay),
    /// - ReturnRosterCasesHubHost (RC-01..RC-05),
    /// - ReturnRegionalStoriesHubHost (ST-02, ST-03, ST-05, ST-06), and
    /// - ReturnFinaleFlowHost (M10-M16 Act-3 sequence).
    /// </summary>
    public sealed class ReturnHarborCommandHubHost : IDisposable
    {
        private readonly ReturnStoryLedgerHost ledgerHost;
        private readonly ReturnRosterCasesHubHost rosterHub;
        private readonly ReturnRegionalStoriesHubHost regionalHub;
        private readonly ReturnFinaleFlowHost finaleHub;
        private readonly Func<string, int> getInt;
        private bool disposed;

        public ReturnStoryLedgerHost LedgerHost => ledgerHost;
        public ReturnRosterCasesHubHost RosterHub => rosterHub;
        public ReturnRegionalStoriesHubHost RegionalHub => regionalHub;
        public ReturnFinaleFlowHost FinaleHub => finaleHub;
        public bool IsDisposed => disposed;

        public ReturnCommandHubMode ActiveMode
        {
            get
            {
                if (ledgerHost.IsOpen) return ReturnCommandHubMode.StoryLedger;
                if (rosterHub.HasActiveCase) return ReturnCommandHubMode.RosterCase;
                if (regionalHub.HasActiveStory) return ReturnCommandHubMode.RegionalStory;
                if (finaleHub.HasActiveHost) return ReturnCommandHubMode.FinaleFlow;
                return ReturnCommandHubMode.None;
            }
        }

        public bool HasAnyActivePanel => ActiveMode != ReturnCommandHubMode.None;

        public ReturnHarborCommandHubHost(
            Transform uiParent,
            Func<string, int> getInt,
            Action<string, int> setInt,
            ReturnRosterCasesHubContext rosterContext = null,
            ReturnRegionalStoriesHubContext regionalContext = null,
            ReturnFinaleFlowContext finaleContext = null)
        {
            if (uiParent == null) throw new ArgumentNullException(nameof(uiParent));
            this.getInt = getInt ?? throw new ArgumentNullException(nameof(getInt));
            if (setInt == null) throw new ArgumentNullException(nameof(setInt));

            ledgerHost = new ReturnStoryLedgerHost(uiParent, getInt);
            rosterHub = new ReturnRosterCasesHubHost(
                uiParent, rosterContext ?? new ReturnRosterCasesHubContext(), getInt, setInt);
            regionalHub = new ReturnRegionalStoriesHubHost(
                uiParent, regionalContext ?? new ReturnRegionalStoriesHubContext(), getInt, setInt);
            finaleHub = new ReturnFinaleFlowHost(
                uiParent, finaleContext ?? new ReturnFinaleFlowContext(), getInt, setInt);
        }

        public bool TryBuildProgressSnapshot(out ReturnStoryProgressSnapshot snapshot)
        {
            snapshot = default;
            if (disposed)
                return false;
            return ReturnStoryLedger.TryBuildSnapshot(getInt, out snapshot);
        }

        public bool TryOpenStoryLedger(Action onClosed = null)
        {
            if (disposed)
                return false;
            if (ActiveMode != ReturnCommandHubMode.None &&
                ActiveMode != ReturnCommandHubMode.StoryLedger)
                return false;
            return ledgerHost.TryOpenOrRefresh(onClosed);
        }

        public bool TryOpenRosterCase(ReturnRosterCaseId caseId)
        {
            if (disposed)
                return false;
            if (ActiveMode != ReturnCommandHubMode.None &&
                ActiveMode != ReturnCommandHubMode.RosterCase)
                return false;
            return rosterHub.TryOpenCase(caseId);
        }

        public bool TryOpenRegionalStory(ReturnRegionalStoryId storyId)
        {
            if (disposed)
                return false;
            if (ActiveMode != ReturnCommandHubMode.None &&
                ActiveMode != ReturnCommandHubMode.RegionalStory)
                return false;
            return regionalHub.TryOpenStory(storyId);
        }

        public bool TryOpenNextFinaleStage()
        {
            if (disposed)
                return false;
            if (ActiveMode != ReturnCommandHubMode.None &&
                ActiveMode != ReturnCommandHubMode.FinaleFlow)
                return false;
            return finaleHub.TryOpenNextStage();
        }

        public void CloseActivePanel()
        {
            ledgerHost.Close();
            rosterHub.CloseActiveCase();
            regionalHub.CloseActiveStory();
            finaleHub.CloseActiveHost();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            ledgerHost.Dispose();
            rosterHub.Dispose();
            regionalHub.Dispose();
            finaleHub.Dispose();
        }
    }
}
