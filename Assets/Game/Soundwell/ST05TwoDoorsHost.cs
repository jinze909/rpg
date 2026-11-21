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

        public ST05TwoDoorsSession Session => session;
        public ST05TwoDoorsView ActiveView => activeView;
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
            if (disposed || activeView != null || !atJingwanCourtyard() ||
                !ST05TwoDoorsFacts.TryRead(getInt, out ST05TwoDoorsOutcome? existing) ||
                existing.HasValue)
                return false;

            if (!ST05TwoDoorsSession.TryCreate(getInt, out ST05TwoDoorsSession created))
                return false;

            session = created;
            activeView = new ST05TwoDoorsView(
                parent,
                session,
                returnHaulageFeePaid,
                marketGiftsTested,
                courtyardResolved,
                partnersHeard,
                mutualTrialConsent,
                chengJobTermsReady,
                HandleHomeCommitRequested,
                HandleAssignmentCommitRequested);
            return true;
        }

        private void HandleHomeCommitRequested()
        {
            if (disposed || session == null || !atJingwanCourtyard() ||
                !marketGiftsTested() || !courtyardResolved() || !partnersHeard())
                return;
            session.TryCommitLivingAgreement(getInt, setInt);
        }

        private void HandleAssignmentCommitRequested()
        {
            if (disposed || session == null || !atJingwanCourtyard() ||
                !chengJobTermsReady())
                return;
            if (!session.TryCommitChengAssignment(getInt, setInt))
                return;

            activeView?.Dispose();
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
