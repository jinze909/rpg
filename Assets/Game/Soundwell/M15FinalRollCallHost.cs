using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-callable M15 memorial roll-call and stele inscription host.
    /// Enforces live ceremony readiness, monotonic M05 arrival identity, and
    /// clean M15FinalRollCallFacts state (blocking torn/partial saves and
    /// orphaned downstream Act-3 facts) before opening M15FinalRollCallView.
    /// </summary>
    public sealed class M15FinalRollCallHost : IDisposable
    {
        private readonly Transform parent;
        private readonly Func<bool> ceremonyReady;
        private readonly Func<bool> bridgeTruthSealed;
        private readonly Func<int> consentedVerifiedNamesCount;
        private readonly Func<string> xiaoqiCustomName;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private M15FinalRollCallSession session;
        private M15FinalRollCallView activeView;
        private int? openedArrivalSequence;
        private bool disposed;
        private bool opening;

        public M15FinalRollCallSession Session => session;
        public M15FinalRollCallView ActiveView =>
            activeView != null && !activeView.IsDisposed ? activeView : null;
        public bool IsDisposed => disposed;
        public bool IsCompleted =>
            M15FinalRollCallFacts.TryRead(getInt, out M15FinalRollCallRecord? saved) &&
            saved.HasValue;
        public bool ShowZhonglingStoneOnMonument =>
            ST04EchoValleyFacts.TryRead(getInt, out ST04EchoValleyOutcome? st04) &&
            st04.HasValue &&
            st04.Value.ShowZhonglingStoneOnM15Monument;

        public M15FinalRollCallHost(
            Transform parent,
            Func<bool> ceremonyReady,
            Func<bool> bridgeTruthSealed,
            Func<int> consentedVerifiedNamesCount,
            Func<string> xiaoqiCustomName,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.ceremonyReady = ceremonyReady ??
                throw new ArgumentNullException(nameof(ceremonyReady));
            this.bridgeTruthSealed = bridgeTruthSealed ??
                throw new ArgumentNullException(nameof(bridgeTruthSealed));
            this.consentedVerifiedNamesCount = consentedVerifiedNamesCount ??
                throw new ArgumentNullException(nameof(consentedVerifiedNamesCount));
            this.xiaoqiCustomName = xiaoqiCustomName;
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
                openedArrivalSequence = null;
            }

            if (disposed || parent == null || activeView != null)
                return false;

            int arrivalSequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            if (arrivalSequence < 0 || !ceremonyReady() ||
                HasAnyPersistedFacts() ||
                M15FinalRollCallFacts.HasOrphanedDownstreamFacts(getInt))
                return false;

            int consentedCount = consentedVerifiedNamesCount();
            if (consentedCount < 0)
                return false;

            bool sealedTruth = bridgeTruthSealed();
            string customName = xiaoqiCustomName?.Invoke();
            bool showStone = ShowZhonglingStoneOnMonument;
            if (getInt(M05LandingFacts.ArrivalSequenceKey) != arrivalSequence ||
                disposed || parent == null)
                return false;

            session = new M15FinalRollCallSession(sealedTruth, consentedCount, customName);
            openedArrivalSequence = arrivalSequence;
            activeView = new M15FinalRollCallView(
                parent, session, HandleRollCallSubmitted, showStone);
            return true;
        }

        private bool HasAnyPersistedFacts()
        {
            return getInt(M15FinalRollCallFacts.ConfirmedKey) != 0 ||
                getInt(M15FinalRollCallFacts.ReaderKey) != 0 ||
                getInt(M15FinalRollCallFacts.IdentityResponseKey) != 0 ||
                getInt(M15FinalRollCallFacts.PensionClosureKey) != 0 ||
                getInt(M15FinalRollCallFacts.SteleInscriptionKey) != 0 ||
                getInt(M15FinalRollCallFacts.ConsentedNamesCountKey) != 0 ||
                getInt(M15FinalRollCallFacts.XiaoqiByNumberKey) != 0;
        }

        private bool LiveCeremonyEvidenceMatchesSession(M15FinalRollCallSession expectedSession)
        {
            if (expectedSession == null ||
                bridgeTruthSealed() != expectedSession.BridgeTruthSealed ||
                consentedVerifiedNamesCount() != expectedSession.ConsentedVerifiedNamesCount)
                return false;

            // The name may change after the page opened. Compare the same
            // trimmed representation used by the roll-call session.
            string currentName = xiaoqiCustomName?.Invoke();
            currentName = string.IsNullOrWhiteSpace(currentName)
                ? null : currentName.Trim();
            return string.Equals(currentName, expectedSession.XiaoqiCustomName,
                StringComparison.Ordinal);
        }

        private static bool MatchesPersistedRollCall(
            M15FinalRollCallRecord saved,
            M15FinalRollCallSession current,
            M15RollCallReader reader,
            M15FinalIdentityResponse identityResponse,
            M15SteleInscription steleInscription)
        {
            M15PensionClosure pension =
                identityResponse == M15FinalIdentityResponse.PublicTrueName
                    ? M15PensionClosure.StoppedAndReconciled
                    : M15PensionClosure.UnchangedOrPrivate;
            return saved.Reader == reader &&
                saved.IdentityResponse == identityResponse &&
                saved.PensionClosure == pension &&
                saved.SteleInscription == steleInscription &&
                saved.ConsentedVerifiedNamesCount == current.ConsentedVerifiedNamesCount &&
                saved.XiaoqiCalledByNumberSeventyTwo == !current.XiaoqiHasCustomName;
        }

        private void HandleRollCallSubmitted(
            M15RollCallReader reader,
            M15FinalIdentityResponse identityResponse,
            M15SteleInscription steleInscription)
        {
            // Live ceremony callbacks can retire or replace the submitting view.
            var submittingView = activeView;
            var submittingSession = session;
            int? arrivalSequence = openedArrivalSequence;
            if (!IsCurrentOwner(submittingView, submittingSession) ||
                !arrivalSequence.HasValue || !ceremonyReady() ||
                !IsCurrentOwner(submittingView, submittingSession) ||
                !LiveCeremonyEvidenceMatchesSession(submittingSession) ||
                getInt(M05LandingFacts.ArrivalSequenceKey) != arrivalSequence.Value ||
                !IsCurrentOwner(submittingView, submittingSession))
                return;

            if (!M15FinalRollCallFacts.TryRead(
                    getInt, out M15FinalRollCallRecord? existing) ||
                getInt(M05LandingFacts.ArrivalSequenceKey) != arrivalSequence.Value ||
                !IsCurrentOwner(submittingView, submittingSession))
                return;

            // Handle a write that succeeded before a lost acknowledgement
            // without issuing duplicate writes.
            if (existing.HasValue)
            {
                // A commit-then-throw retry must acknowledge the *entire*
                // previously saved outcome, not just the three UI buttons.
                if (!MatchesPersistedRollCall(
                        existing.Value, submittingSession, reader, identityResponse, steleInscription))
                    return;
            }
            else if (!submittingSession.TryCompleteRollCall(
                reader, identityResponse, steleInscription, getInt, setInt))
            {
                return;
            }

            if (!M15FinalRollCallFacts.TryRead(
                    getInt, out M15FinalRollCallRecord? persisted) ||
                !persisted.HasValue ||
                !MatchesPersistedRollCall(
                    persisted.Value, submittingSession, reader, identityResponse, steleInscription))
                return;

            submittingView.Dispose();
            if (ReferenceEquals(activeView, submittingView))
            {
                activeView = null;
                openedArrivalSequence = null;
            }
        }

        private bool IsCurrentOwner(M15FinalRollCallView expectedView,
            M15FinalRollCallSession expectedSession)
        {
            return !disposed && parent != null && expectedView != null &&
                !expectedView.IsDisposed && ReferenceEquals(activeView, expectedView) &&
                expectedSession != null && ReferenceEquals(session, expectedSession);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            activeView?.Dispose();
            activeView = null;
            openedArrivalSequence = null;
        }
    }
}
