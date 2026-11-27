using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-callable M14 ("同一个海峡，不同的进场") host. Enforces verified
    /// M13DepartureRiskFacts, live strait-action presence, live route-proof
    /// revalidation at commit time, monotonic M05 arrival identity, and clean
    /// M14FinalPassageFacts state (blocking torn/partial saves) before opening
    /// M14FinalPassageView.
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
        private int? openedArrivalSequence;
        private bool disposed;
        private bool opening;

        public M14FinalPassageSession Session => session;
        public M14FinalPassageView ActiveView =>
            activeView != null && !activeView.IsDisposed ? activeView : null;
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
            if (arrivalSequence < 0 || !atStraitAction() || HasAnyPersistedFacts())
                return false;

            if (!M14FinalPassageSession.TryCreateFromSavedFacts(
                    getInt, out M14FinalPassageSession created) ||
                disposed || parent == null ||
                getInt(M05LandingFacts.ArrivalSequenceKey) != arrivalSequence ||
                disposed || parent == null)
                return false;

            session = created;
            openedArrivalSequence = arrivalSequence;
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

        private bool HasAnyPersistedFacts()
        {
            return getInt(M14FinalPassageFacts.ConfirmedKey) != 0 ||
                getInt(M14FinalPassageFacts.ExecutedRouteKey) != 0 ||
                getInt(M14FinalPassageFacts.PassageOutcomeKey) != 0 ||
                getInt(M14FinalPassageFacts.CivilianExitVerifiedKey) != 0 ||
                getInt(M14FinalPassageFacts.DepartingCountKey) != 0 ||
                getInt(M14FinalPassageFacts.StayingCountKey) != 0 ||
                getInt(M14FinalPassageFacts.UndecidedCountKey) != 0 ||
                getInt(M14FinalPassageFacts.BorderForceActionKey) != 0 ||
                getInt(M14FinalPassageFacts.LuJunDispositionKey) != 0 ||
                getInt(M14FinalPassageFacts.FogMasteryBonusKey) != 0;
        }

        private bool HasLiveRouteProofs(M14FinalPassageSession expectedSession)
        {
            switch (expectedSession.Departure.Route)
            {
                case M13ApproachRoute.RouteASupervisedCivilian:
                    return validSupervisionCredentials() && validPortGuarantee();
                case M13ApproachRoute.RouteBBellTowerAssault:
                    return threeOpenDoorBellsRung();
                case M13ApproachRoute.RouteCOldCanalNightRun:
                    return fogRelayCompleted() &&
                        (!expectedSession.FogMasteryBonusApplied || fogMasteryBonusAvailable());
                default:
                    return false;
            }
        }

        private void HandleAyuanSubmitted(M14AyuanOutcome outcome)
        {
            var settlingView = activeView;
            var settlingSession = session;
            int? arrivalSequence = openedArrivalSequence;
            if (!IsCurrentOwner(settlingView, settlingSession) ||
                !arrivalSequence.HasValue || !atStraitAction() ||
                getInt(M05LandingFacts.ArrivalSequenceKey) != arrivalSequence.Value ||
                !IsCurrentOwner(settlingView, settlingSession) ||
                !HasLiveRouteProofs(settlingSession) ||
                getInt(M05LandingFacts.ArrivalSequenceKey) != arrivalSequence.Value ||
                !IsCurrentOwner(settlingView, settlingSession))
                return;

            if (!settlingSession.TrySettleAyuanAndCommit(outcome, getInt, setInt))
                return;

            // Persistence can synchronously retire the old view. Never close
            // a replacement opened by another callback.
            settlingView.Dispose();
            if (ReferenceEquals(activeView, settlingView))
            {
                activeView = null;
                openedArrivalSequence = null;
            }
        }

        private bool IsCurrentOwner(M14FinalPassageView expectedView,
            M14FinalPassageSession expectedSession)
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
