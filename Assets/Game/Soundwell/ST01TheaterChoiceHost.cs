using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-callable Jingwan ST-01 host; it cannot create a script reading,
    /// rehearsal, displacement testimony, alternate theater/funding, post-show
    /// forum or public boycott. Scene integrations must supply live evidence
    /// for each actual event. All actions are checked again at commit time.
    /// </summary>
    public sealed class ST01TheaterChoiceHost : IDisposable
    {
        private readonly Transform parent;
        private readonly Func<bool> atJingwanTheater;
        private readonly Func<bool> liuLingPresent;
        private readonly Func<bool> originalScriptRead;
        private readonly Func<bool> revisedScriptRead;
        private readonly Func<bool> rehearsalObserved;
        private readonly Func<bool> displacedResidentsHeard;
        private readonly Func<bool> alternateStageAndFundingPrepared;
        private readonly Func<bool> postshowResidentsForumArranged;
        private readonly Func<bool> publicBoycottActuallyDeclared;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private ST01TheaterChoiceView activeView;
        private bool disposed;
        private bool opening;

        public ST01TheaterChoiceView ActiveView =>
            (activeView != null && !activeView.IsDisposed) ? activeView : null;
        public bool IsDisposed => disposed;

        public ST01TheaterChoiceHost(
            Transform parent,
            Func<bool> atJingwanTheater,
            Func<bool> liuLingPresent,
            Func<bool> originalScriptRead,
            Func<bool> revisedScriptRead,
            Func<bool> rehearsalObserved,
            Func<bool> displacedResidentsHeard,
            Func<bool> alternateStageAndFundingPrepared,
            Func<bool> postshowResidentsForumArranged,
            Func<bool> publicBoycottActuallyDeclared,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.atJingwanTheater = atJingwanTheater ??
                throw new ArgumentNullException(nameof(atJingwanTheater));
            this.liuLingPresent = liuLingPresent ??
                throw new ArgumentNullException(nameof(liuLingPresent));
            this.originalScriptRead = originalScriptRead ??
                throw new ArgumentNullException(nameof(originalScriptRead));
            this.revisedScriptRead = revisedScriptRead ??
                throw new ArgumentNullException(nameof(revisedScriptRead));
            this.rehearsalObserved = rehearsalObserved ??
                throw new ArgumentNullException(nameof(rehearsalObserved));
            this.displacedResidentsHeard = displacedResidentsHeard ??
                throw new ArgumentNullException(nameof(displacedResidentsHeard));
            this.alternateStageAndFundingPrepared = alternateStageAndFundingPrepared ??
                throw new ArgumentNullException(nameof(alternateStageAndFundingPrepared));
            this.postshowResidentsForumArranged = postshowResidentsForumArranged ??
                throw new ArgumentNullException(nameof(postshowResidentsForumArranged));
            this.publicBoycottActuallyDeclared = publicBoycottActuallyDeclared ??
                throw new ArgumentNullException(nameof(publicBoycottActuallyDeclared));
            this.getInt = getInt ?? throw new ArgumentNullException(nameof(getInt));
            this.setInt = setInt ?? throw new ArgumentNullException(nameof(setInt));
        }

        private bool PrerequisitesVerified()
        {
            return atJingwanTheater() && liuLingPresent() &&
                originalScriptRead() && revisedScriptRead() &&
                rehearsalObserved() && displacedResidentsHeard();
        }

        private bool CanCommit(ST01TheaterChoice choice)
        {
            if (!PrerequisitesVerified())
                return false;
            int existing = getInt(ST01RegionalStoryFacts.TheaterChoiceKey);
            if (getInt(ST01RegionalStoryFacts.TheaterConfirmedKey) != 0 ||
                (existing != 0 && existing != (int)choice))
                return false;

            switch (choice)
            {
                case ST01TheaterChoice.RewritePlayWithFundingRisk:
                    return alternateStageAndFundingPrepared();
                case ST01TheaterChoice.KeepContractAndHearResidents:
                    return postshowResidentsForumArranged();
                case ST01TheaterChoice.PublicBoycott:
                    return publicBoycottActuallyDeclared();
                default:
                    return false;
            }
        }

        public bool TryOpen()
        {
            if (disposed || parent == null || opening)
                return false;

            opening = true;
            try
            {
                return TryOpenCore();
            }
            finally
            {
                opening = false;
            }
        }

        private bool TryOpenCore()
        {
            if (activeView != null && activeView.IsDisposed)
            {
                activeView.Dispose();
                activeView = null;
            }
            if (disposed || parent == null || activeView != null || !PrerequisitesVerified() ||
                getInt(ST01RegionalStoryFacts.TheaterConfirmedKey) != 0)
                return false;

            int pendingRaw = getInt(ST01RegionalStoryFacts.TheaterChoiceKey);
            if (pendingRaw != 0 &&
                !Enum.IsDefined(typeof(ST01TheaterChoice), pendingRaw))
                return false;

            int departureRaw = getInt(ST01RegionalStoryFacts.DepartureChoiceKey);
            int departureMarker = getInt(ST01RegionalStoryFacts.DepartureConfirmedKey);
            if (departureMarker != 0 && departureMarker != 1)
                return false;
            if ((departureMarker == 0 && departureRaw != 0) ||
                (departureMarker == 1 &&
                 !Enum.IsDefined(typeof(ST01DepartureChoice), departureRaw)))
                return false;

            // A torn theater write deliberately fails public TryRead. Its
            // exact pending decision may still be retried in this view.
            if (pendingRaw == 0 && !ST01RegionalStoryFacts.TryRead(getInt, out _))
                return false;
            ST01DepartureChoice? history = departureMarker == 1
                ? (ST01DepartureChoice?)departureRaw : null;
            ST01TheaterChoice? pending = pendingRaw != 0
                ? (ST01TheaterChoice?)pendingRaw : null;

            // Scene or save callbacks may retire the host while the
            // opening is in progress. Never publish a late view.
            if (disposed || parent == null || activeView != null)
                return false;

            var createdView = new ST01TheaterChoiceView(
                parent, history, pending, CanCommit, HandleChoice);
            if (disposed || parent == null || createdView.IsDisposed)
            {
                createdView.Dispose();
                return false;
            }

            activeView = createdView;
            return true;
        }

        private bool IsCurrentSubmission(ST01TheaterChoiceView expectedView) =>
            !disposed && parent != null && expectedView != null &&
            !expectedView.IsDisposed && ReferenceEquals(activeView, expectedView);

        private void HandleChoice(ST01TheaterChoice choice)
        {
            var expectedView = activeView;
            if (!IsCurrentSubmission(expectedView) || !CanCommit(choice) ||
                !IsCurrentSubmission(expectedView))
                return;

            bool abandoned = false;
            void WriteWhileCurrent(string key, int value)
            {
                if (!IsCurrentSubmission(expectedView))
                {
                    abandoned = true;
                    return;
                }
                setInt(key, value);
            }

            // A synchronous fact callback can close this view and open a
            // replacement. Do not let the retired submission write or close it.
            if (!ST01RegionalStoryFacts.TryRecordTheater(
                choice, getInt, WriteWhileCurrent) || abandoned ||
                !IsCurrentSubmission(expectedView))
                return;
            if (!ST01RegionalStoryFacts.TryRead(getInt, out ST01RegionalOutcome saved) ||
                saved.Theater != choice || !IsCurrentSubmission(expectedView))
                return;

            expectedView.Dispose();
            activeView = null;
        }

        public void RefreshView()
        {
            if (disposed || activeView == null)
                return;
            if (activeView.IsDisposed)
            {
                activeView.Dispose();
                activeView = null;
                return;
            }
            activeView.Refresh();
            if (activeView.IsDisposed)
                activeView = null;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            activeView?.Dispose();
            activeView = null;
        }
    }
}
