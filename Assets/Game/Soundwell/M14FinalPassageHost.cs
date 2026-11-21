using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-callable M14 ("同一个海峡，不同的进场") host. Enforces verified
    /// M13DepartureRiskFacts, live strait-action presence, and clean ConfirmedKey
    /// before opening M14FinalPassageView, and persists the outcome through
    /// M14FinalPassageSession with retry-safe idempotent commit recovery.
    /// </summary>
    public sealed class M14FinalPassageHost : IDisposable
    {
        private readonly Transform parent;
        private readonly Func<bool> atStraitAction;
        private readonly Func<bool> validSupervisionCredentials;
        private readonly Func<bool> validPortGuarantee;
        private readonly Func<bool> threeOpenDoorBellsRung;
        private readonly Func<bool> fogRelayCompleted;
        private readonly Func<bool> fogMasteryBonusAvailable;
        private readonly Func<M14CivilianCounts> readCivilianCounts;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private M14FinalPassageSession session;
        private M14FinalPassageView activeView;
        private bool disposed;

        public M14FinalPassageSession Session => session;
        public M14FinalPassageView ActiveView => activeView;
        public bool IsDisposed => disposed;
        public bool IsCompleted =>
            M14FinalPassageFacts.TryRead(getInt, out M14FinalPassageRecord? saved) &&
            saved.HasValue;

        public M14FinalPassageHost(
            Transform parent,
            Func<bool> atStraitAction,
            Func<bool> validSupervisionCredentials,
            Func<bool> validPortGuarantee,
            Func<bool> threeOpenDoorBellsRung,
            Func<bool> fogRelayCompleted,
            Func<bool> fogMasteryBonusAvailable,
            Func<M14CivilianCounts> readCivilianCounts,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.atStraitAction = atStraitAction ??
                throw new ArgumentNullException(nameof(atStraitAction));
            this.validSupervisionCredentials = validSupervisionCredentials ??
                throw new ArgumentNullException(nameof(validSupervisionCredentials));
            this.validPortGuarantee = validPortGuarantee ??
                throw new ArgumentNullException(nameof(validPortGuarantee));
            this.threeOpenDoorBellsRung = threeOpenDoorBellsRung ??
                throw new ArgumentNullException(nameof(threeOpenDoorBellsRung));
            this.fogRelayCompleted = fogRelayCompleted ??
                throw new ArgumentNullException(nameof(fogRelayCompleted));
            this.fogMasteryBonusAvailable = fogMasteryBonusAvailable ??
                throw new ArgumentNullException(nameof(fogMasteryBonusAvailable));
            this.readCivilianCounts = readCivilianCounts ??
                throw new ArgumentNullException(nameof(readCivilianCounts));
            this.getInt = getInt ?? throw new ArgumentNullException(nameof(getInt));
            this.setInt = setInt ?? throw new ArgumentNullException(nameof(setInt));
        }

        public bool TryOpen()
        {
            if (disposed || activeView != null || !atStraitAction() ||
                getInt(M14FinalPassageFacts.ConfirmedKey) != 0)
                return false;

            if (!M14FinalPassageSession.TryCreateFromSavedFacts(
                    getInt, out M14FinalPassageSession created))
                return false;

            session = created;
            activeView = new M14FinalPassageView(
                parent,
                session,
                validSupervisionCredentials,
                validPortGuarantee,
                threeOpenDoorBellsRung,
                fogRelayCompleted,
                fogMasteryBonusAvailable,
                readCivilianCounts,
                HandleAyuanSubmitted);
            return true;
        }

        private void HandleAyuanSubmitted(M14AyuanOutcome outcome)
        {
            if (disposed || session == null || !atStraitAction())
                return;

            if (!session.TrySettleAyuanAndCommit(outcome, getInt, setInt))
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
