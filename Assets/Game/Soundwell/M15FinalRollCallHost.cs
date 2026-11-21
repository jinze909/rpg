using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-callable M15 memorial roll-call and stele inscription host.
    /// Enforces live ceremony readiness and clean ConfirmedKey before opening
    /// M15FinalRollCallView, and persists the player's reader, identity, and
    /// stele choices to Makinom with interrupted-write retry and idempotent
    /// commit-then-throw recovery.
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
        private bool disposed;

        public M15FinalRollCallSession Session => session;
        public M15FinalRollCallView ActiveView => activeView;
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
            if (disposed || activeView != null || !ceremonyReady() ||
                getInt(M15FinalRollCallFacts.ConfirmedKey) != 0)
                return false;

            int consentedCount = consentedVerifiedNamesCount();
            if (consentedCount < 0)
                return false;

            session = new M15FinalRollCallSession(
                bridgeTruthSealed(),
                consentedCount,
                xiaoqiCustomName?.Invoke());
            activeView = new M15FinalRollCallView(
                parent, session, HandleRollCallSubmitted);
            return true;
        }

        private void HandleRollCallSubmitted(
            M15RollCallReader reader,
            M15FinalIdentityResponse identityResponse,
            M15SteleInscription steleInscription)
        {
            if (disposed || session == null || !ceremonyReady())
                return;

            if (!M15FinalRollCallFacts.TryRead(
                    getInt, out M15FinalRollCallRecord? existing))
                return;

            // Handle a write that succeeded before a lost acknowledgement
            // without issuing duplicate writes.
            if (existing.HasValue)
            {
                if (existing.Value.Reader != reader ||
                    existing.Value.IdentityResponse != identityResponse ||
                    existing.Value.SteleInscription != steleInscription)
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
                persisted.Value.Reader != reader ||
                persisted.Value.IdentityResponse != identityResponse ||
                persisted.Value.SteleInscription != steleInscription)
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
