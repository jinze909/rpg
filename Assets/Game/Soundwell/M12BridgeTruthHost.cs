using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-callable M12 ("一面旗帜不等于每个人") host. Enforces verified
    /// M11ReverseRescueFacts, live bell-tower/camp presence, live participant
    /// and M03-context revalidation at commit time, monotonic M05 arrival
    /// identity, and clean M12BridgeTruthFacts state (blocking torn/partial saves)
    /// before opening M12BridgeTruthView.
    /// </summary>
    public sealed class M12BridgeTruthHost : IDisposable
    {
        private readonly Transform parent;
        private readonly Func<bool> atBellTowerCamp;
        private readonly Func<bool> m03PriorConfession;
        private readonly Func<bool> m03SecrecyPromised;
        private readonly Func<bool> shenLiParticipating;
        private readonly Func<bool> linSuPresent;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private M12BridgeTruthSession session;
        private M12BridgeTruthView activeView;
        private int? openedArrivalSequence;
        private bool openedPriorConfession;
        private bool openedSecrecyPromised;
        private bool openedShenLiParticipating;
        private bool openedLinSuPresent;
        private bool disposed;
        private bool opening;

        public M12BridgeTruthSession Session => session;
        public M12BridgeTruthView ActiveView =>
            (activeView != null && !activeView.IsDisposed) ? activeView : null;
        public bool IsDisposed => disposed;
        public bool IsCompleted =>
            M12BridgeTruthFacts.TryRead(getInt, out M12BridgeTruthRecord? saved) &&
            saved.HasValue;

        public M12BridgeTruthHost(
            Transform parent,
            Func<bool> atBellTowerCamp,
            Func<bool> m03PriorConfession,
            Func<bool> m03SecrecyPromised,
            Func<bool> shenLiParticipating,
            Func<bool> linSuPresent,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.atBellTowerCamp = atBellTowerCamp ??
                throw new ArgumentNullException(nameof(atBellTowerCamp));
            this.m03PriorConfession = m03PriorConfession ??
                throw new ArgumentNullException(nameof(m03PriorConfession));
            this.m03SecrecyPromised = m03SecrecyPromised ??
                throw new ArgumentNullException(nameof(m03SecrecyPromised));
            this.shenLiParticipating = shenLiParticipating ??
                throw new ArgumentNullException(nameof(shenLiParticipating));
            this.linSuPresent = linSuPresent ??
                throw new ArgumentNullException(nameof(linSuPresent));
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
            if (arrivalSequence < 0 || !atBellTowerCamp() || HasAnyPersistedFacts())
                return false;

            bool priorConfession = m03PriorConfession();
            bool secrecyPromised = m03SecrecyPromised();
            bool shenLi = shenLiParticipating();
            bool linSu = linSuPresent();
            if (!M12BridgeTruthSession.TryCreateFromSavedFacts(
                    getInt,
                    priorConfession,
                    secrecyPromised,
                    shenLi,
                    linSu,
                    out M12BridgeTruthSession created))
                return false;

            // Readers can retire the host or begin another voyage synchronously.
            if (disposed || parent == null ||
                getInt(M05LandingFacts.ArrivalSequenceKey) != arrivalSequence ||
                disposed || parent == null)
                return false;

            session = created;
            openedArrivalSequence = arrivalSequence;
            openedPriorConfession = priorConfession;
            openedSecrecyPromised = secrecyPromised;
            openedShenLiParticipating = shenLi;
            openedLinSuPresent = linSu;
            activeView = new M12BridgeTruthView(
                parent,
                session,
                () => M12BridgeTruthFacts.EvaluateM10HanExposedPublicly(getInt),
                HandleDisclosureSubmitted);
            return true;
        }

        private bool HasAnyPersistedFacts()
        {
            return getInt(M12BridgeTruthFacts.ConfirmedKey) != 0 ||
                getInt(M12BridgeTruthFacts.M03PriorConfessionKey) != 0 ||
                getInt(M12BridgeTruthFacts.M03SecrecyPromisedKey) != 0 ||
                getInt(M12BridgeTruthFacts.LuReplyKey) != 0 ||
                getInt(M12BridgeTruthFacts.ShenLiParticipatingKey) != 0 ||
                getInt(M12BridgeTruthFacts.StatementsMaskKey) != 0 ||
                getInt(M12BridgeTruthFacts.LinSuEchoKey) != 0 ||
                getInt(M12BridgeTruthFacts.M10HanExposedKey) != 0 ||
                getInt(M12BridgeTruthFacts.DisclosureKey) != 0;
        }

        private void HandleDisclosureSubmitted(M12TruthDisclosure disclosure)
        {
            // Capture the submitting owner before invoking live evidence readers.
            // A callback may close it or reopen a replacement panel.
            var committingView = activeView;
            var committingSession = session;
            int? arrivalSequence = openedArrivalSequence;
            bool priorConfession = openedPriorConfession;
            bool secrecyPromised = openedSecrecyPromised;
            bool shenLi = openedShenLiParticipating;
            bool linSu = openedLinSuPresent;
            if (!IsCurrentDisclosure(committingView, committingSession) ||
                !arrivalSequence.HasValue || !atBellTowerCamp() ||
                m03PriorConfession() != priorConfession ||
                m03SecrecyPromised() != secrecyPromised ||
                shenLiParticipating() != shenLi ||
                linSuPresent() != linSu ||
                getInt(M05LandingFacts.ArrivalSequenceKey) != arrivalSequence.Value ||
                !IsCurrentDisclosure(committingView, committingSession))
                return;

            // Persistence may synchronously close this host or destroy its view.
            // Retire only the view that submitted the successful decision.
            if (!committingSession.TryDecideTruthAndCommit(disclosure, getInt, setInt))
                return;

            committingView.Dispose();
            if (ReferenceEquals(activeView, committingView))
            {
                activeView = null;
                openedArrivalSequence = null;
            }
        }

        private bool IsCurrentDisclosure(M12BridgeTruthView expectedView,
            M12BridgeTruthSession expectedSession)
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
