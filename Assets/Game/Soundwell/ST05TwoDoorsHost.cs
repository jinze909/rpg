using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-callable ST-05 ("两扇门的乔迁宴") host. Enforces live Jingwan
    /// courtyard presence, live scene proof revalidation at commit time, and
    /// clean ST05TwoDoorsFacts state before opening ST05TwoDoorsView,
    /// and commits both the living agreement and Cheng Wu's own assignment
    /// through ST05TwoDoorsSession with retry safety.
    /// </summary>
    public sealed class ST05TwoDoorsHost : IDisposable
    {
        private readonly Transform parent;
        private readonly Func<bool> atJingwanCourtyard;
        private readonly Func<bool> returnHaulageFeePaid;
        private readonly Func<bool> marketGiftsTested;
        private readonly Func<bool> courtyardResolved;
        private readonly Func<bool> partnersHeard;
        private readonly Func<bool> mutualTrialConsent;
        private readonly Func<bool> chengJobTermsReady;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private ST05TwoDoorsSession session;
        private ST05TwoDoorsView activeView;
        private bool disposed;
        private bool opening;

        public ST05TwoDoorsSession Session => session;
        public ST05TwoDoorsView ActiveView =>
            (activeView != null && !activeView.IsDisposed) ? activeView : null;
        public bool IsDisposed => disposed;
        public bool IsCompleted =>
            ST05TwoDoorsFacts.TryRead(getInt, out ST05TwoDoorsOutcome? saved) &&
            saved.HasValue && saved.Value.Assignment.HasValue;

        public ST05TwoDoorsHost(
            Transform parent,
            Func<bool> atJingwanCourtyard,
            Func<bool> returnHaulageFeePaid,
            Func<bool> marketGiftsTested,
            Func<bool> courtyardResolved,
            Func<bool> partnersHeard,
            Func<bool> mutualTrialConsent,
            Func<bool> chengJobTermsReady,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.atJingwanCourtyard = atJingwanCourtyard ??
                throw new ArgumentNullException(nameof(atJingwanCourtyard));
            this.returnHaulageFeePaid = returnHaulageFeePaid ??
                throw new ArgumentNullException(nameof(returnHaulageFeePaid));
            this.marketGiftsTested = marketGiftsTested ??
                throw new ArgumentNullException(nameof(marketGiftsTested));
            this.courtyardResolved = courtyardResolved ??
                throw new ArgumentNullException(nameof(courtyardResolved));
            this.partnersHeard = partnersHeard ??
                throw new ArgumentNullException(nameof(partnersHeard));
            this.mutualTrialConsent = mutualTrialConsent ??
                throw new ArgumentNullException(nameof(mutualTrialConsent));
            this.chengJobTermsReady = chengJobTermsReady ??
                throw new ArgumentNullException(nameof(chengJobTermsReady));
            this.getInt = getInt ?? throw new ArgumentNullException(nameof(getInt));
            this.setInt = setInt ?? throw new ArgumentNullException(nameof(setInt));
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
            if (disposed || parent == null || activeView != null || !atJingwanCourtyard())
                return false;

            if (disposed || parent == null ||
                !ST05TwoDoorsSession.TryCreate(getInt, out ST05TwoDoorsSession created) ||
                disposed || parent == null || activeView != null)
                return false;

            // A live scene or save callback may retire this opening.
            // Keep the new session private until its view can be published.
            var createdView = new ST05TwoDoorsView(
                parent,
                created,
                returnHaulageFeePaid,
                marketGiftsTested,
                courtyardResolved,
                partnersHeard,
                mutualTrialConsent,
                chengJobTermsReady,
                HandleHomeCommitRequested,
                HandleAssignmentCommitRequested);
            if (disposed || parent == null || createdView.IsDisposed)
            {
                createdView.Dispose();
                return false;
            }

            session = created;
            activeView = createdView;
            return true;
        }

        private bool IsCurrentSubmission(ST05TwoDoorsView expectedView,
            ST05TwoDoorsSession expectedSession) =>
            !disposed && parent != null && expectedView != null &&
            !expectedView.IsDisposed && expectedSession != null &&
            ReferenceEquals(activeView, expectedView) &&
            ReferenceEquals(session, expectedSession);

        private void HandleHomeCommitRequested()
        {
            var view = activeView;
            var expectedSession = session;
            if (!IsCurrentSubmission(view, expectedSession) ||
                !atJingwanCourtyard() || !marketGiftsTested() ||
                !courtyardResolved() || !partnersHeard() ||
                !IsCurrentSubmission(view, expectedSession))
                return;

            expectedSession.TryCommitLivingAgreement(getInt, (key, value) =>
            {
                if (IsCurrentSubmission(view, expectedSession))
                    setInt(key, value);
            });
        }

        private void HandleAssignmentCommitRequested()
        {
            var view = activeView;
            var expectedSession = session;
            if (!IsCurrentSubmission(view, expectedSession) ||
                !atJingwanCourtyard() || !chengJobTermsReady() ||
                !IsCurrentSubmission(view, expectedSession))
                return;

            // Fact callbacks may retire the UI during either commit. Keep
            // the original session and stop writing when it loses ownership.
            bool abandoned = false;
            if (!expectedSession.TryCommitChengAssignment(getInt, (key, value) =>
                {
                    if (!IsCurrentSubmission(view, expectedSession))
                    {
                        abandoned = true;
                        return;
                    }
                    setInt(key, value);
                }) || abandoned || !IsCurrentSubmission(view, expectedSession))
                return;

            view.Dispose();
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
