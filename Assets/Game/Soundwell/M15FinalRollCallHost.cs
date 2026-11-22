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

        public M15FinalRollCallSession Session => session;
        public M15FinalRollCallView ActiveView =>
            activeView != null && !activeView.IsDisposed ? activeView : null;
        public bool IsDisposed => disposed;
        public bool IsCompleted =>
            M15FinalRollCallFacts.TryRead(getInt, out M15FinalRollCallRecord? saved) &&
            saved.HasValue;

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
            if (activeView != null && activeView.IsDisposed)
            {
                activeView.Dispose();
                activeView = null;
                openedArrivalSequence = null;
            }

            if (disposed || parent == null || activeView != null || !ceremonyReady() ||
                HasAnyPersistedFacts() ||
                M15FinalRollCallFacts.HasOrphanedDownstreamFacts(getInt))
                return false;

            int consentedCount = consentedVerifiedNamesCount();
            if (consentedCount < 0)
                return false;

            session = new M15FinalRollCallSession(
                bridgeTruthSealed(),
                consentedCount,
                xiaoqiCustomName?.Invoke());
            openedArrivalSequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            activeView = new M15FinalRollCallView(
                parent, session, HandleRollCallSubmitted);
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

        private bool LiveCeremonyEvidenceMatchesSession()
        {
            if (session == null ||
                bridgeTruthSealed() != session.BridgeTruthSealed ||
                consentedVerifiedNamesCount() != session.ConsentedVerifiedNamesCount)
                return false;

            // The name may change after the page opened. Compare the same
            // trimmed representation used by the roll-call session.
            string currentName = xiaoqiCustomName?.Invoke();
            currentName = string.IsNullOrWhiteSpace(currentName)
                ? null : currentName.Trim();
            return string.Equals(currentName, session.XiaoqiCustomName,
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
            // A ceremony can remain open while consent, sealed bridge truth,
            // or Xiaoqi's chosen name changes. Do not settle stale choices.
            if (disposed || session == null || activeView == null || activeView.IsDisposed ||
                !ceremonyReady() ||
                !LiveCeremonyEvidenceMatchesSession() ||
                !openedArrivalSequence.HasValue ||
                getInt(M05LandingFacts.ArrivalSequenceKey) != openedArrivalSequence.Value)
                return;

            if (!M15FinalRollCallFacts.TryRead(
                    getInt, out M15FinalRollCallRecord? existing))
                return;

            // Handle a write that succeeded before a lost acknowledgement
            // without issuing duplicate writes.
            if (existing.HasValue)
            {
                // A commit-then-throw retry must acknowledge the *entire*
                // previously saved outcome, not just the three UI buttons.
                if (!MatchesPersistedRollCall(
                        existing.Value, session, reader, identityResponse, steleInscription))
                    return;
            }
            else if (!session.TryCompleteRollCall(
                reader, identityResponse, steleInscription, getInt, setInt))
            {
                return;
            }

            if (!M15FinalRollCallFacts.TryRead(
                    getInt, out M15FinalRollCallRecord? persisted) ||
                !persisted.HasValue ||
                !MatchesPersistedRollCall(
                    persisted.Value, session, reader, identityResponse, steleInscription))
                return;

            activeView?.Dispose();
            activeView = null;
            openedArrivalSequence = null;
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
