using System;
using GamingIsLove.Makinom;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum M05OuterHarborFlowStage
    {
        LandingCheck,
        ArrivalSession,
        Completed
    }

    /// <summary>
    /// End-to-end M05 outer-harbor host that connects an observed arrival
    /// (M05LandingCheckView -> M05LandingFacts) into M05OuterHarborArrivalSession
    /// (Seran's eight letters -> repair stall, fog rumor and salt market).
    /// Requires a verified outbound crossing (M04 departure + sailed + succeeded
    /// fog leg) and disposes each canvas before opening the next.
    /// </summary>
    public sealed class M05OuterHarborFlowHost : IDisposable
    {
        private readonly Transform parent;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private Action<M05OuterHarborOutcome> onCompleted;
        private M05LandingCheckView landingCheckView;
        private M05OuterHarborArrivalSession arrivalSession;
        private M05OuterHarborFlowStage stage;
        private float? lastAspect;
        private bool completedDelivered;
        private bool disposed;

        public M05OuterHarborFlowStage Stage => stage;
        public M05LandingCheckView LandingCheckView => landingCheckView;
        public M05OuterHarborArrivalSession ArrivalSession => arrivalSession;
        public bool IsDisposed => disposed;

        public static bool TryOpen(Transform parent,
            M05LandingReport? observedLanding,
            Action<M05OuterHarborOutcome> onCompleted,
            out M05OuterHarborFlowHost host)
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
            {
                host = null;
                return false;
            }
            return TryOpen(parent, observedLanding,
                key => variables.GetInt(key),
                (key, value) => variables.Set(key, value),
                onCompleted,
                out host);
        }

        public static bool TryOpen(Transform parent,
            M05LandingReport? observedLanding,
            Func<string, int> getInt,
            Action<string, int> setInt,
            Action<M05OuterHarborOutcome> onCompleted,
            out M05OuterHarborFlowHost host)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (onCompleted == null) throw new ArgumentNullException(nameof(onCompleted));
            host = null;
            if (getInt == null || setInt == null)
                return false;

            if (!TryReadCompletedCrossing(getInt, out M04DepartureRecord departure))
                return false;

            if (!M05LandingFacts.TryRead(getInt, out M05LandingReport? savedLanding))
                return false;

            // Reject a corrupted nonzero letter choice *before* creating the
            // landing or Seran UI, where the choice cannot be saved again.
            if (!M05LettersFacts.TryRead(getInt, out M05LetterDecision? savedLetters) ||
                (!savedLetters.HasValue && getInt(M05LettersFacts.DecisionKey) != 0))
                return false;

            if (savedLanding.HasValue)
            {
                host = new M05OuterHarborFlowHost(parent, getInt, setInt,
                    observedLanding: null, resumeFromSavedLanding: true, onCompleted);
                return true;
            }

            if (!observedLanding.HasValue ||
                !IsValidObservedLanding(observedLanding.Value, departure))
                return false;

            host = new M05OuterHarborFlowHost(parent, getInt, setInt,
                observedLanding.Value, resumeFromSavedLanding: false, onCompleted);
            return true;
        }

        private M05OuterHarborFlowHost(Transform parent,
            Func<string, int> getInt,
            Action<string, int> setInt,
            M05LandingReport? observedLanding,
            bool resumeFromSavedLanding,
            Action<M05OuterHarborOutcome> onCompleted)
        {
            this.parent = parent;
            this.getInt = getInt;
            this.setInt = setInt;
            this.onCompleted = onCompleted;

            if (resumeFromSavedLanding)
            {
                OpenArrivalSession();
            }
            else
            {
                stage = M05OuterHarborFlowStage.LandingCheck;
                landingCheckView = new M05LandingCheckView(
                    parent, observedLanding.Value, OnLandingConfirmed);
            }
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed) return;
            if (parent == null)
            {
                Dispose();
                return;
            }
            lastAspect = aspect;
            landingCheckView?.LayoutForViewportAspect(aspect);
            arrivalSession?.LayoutForViewportAspect(aspect);
        }

        private void OnLandingConfirmed(M05LandingReport confirmed)
        {
            if (disposed || parent == null || stage != M05OuterHarborFlowStage.LandingCheck)
                return;

            // Save first, but keep the landing UI until the next scene is
            // successfully constructed. A failed handoff must remain retryable.
            // If the save already succeeded on a previous attempt, accept
            // only the exact same report and never write it a second time.
            if (!M05LandingFacts.TryRead(getInt, out M05LandingReport? saved))
                throw new InvalidOperationException(
                    "Unable to read verified M05 landing report.");
            if (saved.HasValue)
            {
                if (!SameLanding(saved.Value, confirmed))
                    throw new InvalidOperationException(
                        "Saved M05 landing differs from the confirmed report.");
            }
            else if (getInt(M05LandingFacts.ConfirmedKey) != 0 ||
                !M05LandingFacts.TryRecord(confirmed, getInt, setInt))
                throw new InvalidOperationException(
                    "Unable to persist verified M05 landing report.");

            OpenArrivalSession();
            landingCheckView?.Dispose();
            landingCheckView = null;
        }

        private void OpenArrivalSession()
        {
            if (!M05OuterHarborArrivalSession.TryOpen(
                parent, getInt, setInt, OnArrivalSessionCompleted,
                out M05OuterHarborArrivalSession next))
                throw new InvalidOperationException(
                    "Unable to open M05 outer-harbor arrival session after verified landing.");

            try
            {
                if (lastAspect.HasValue)
                    next.LayoutForViewportAspect(lastAspect.Value);
            }
            catch
            {
                next.Dispose();
                throw;
            }

            arrivalSession = next;
            stage = M05OuterHarborFlowStage.ArrivalSession;
        }

        private void OnArrivalSessionCompleted(M05OuterHarborOutcome outcome)
        {
            if (disposed || completedDelivered ||
                stage != M05OuterHarborFlowStage.ArrivalSession)
                return;

            onCompleted?.Invoke(outcome);
            completedDelivered = true;
            onCompleted = null;
            stage = M05OuterHarborFlowStage.Completed;
            arrivalSession?.Dispose();
            arrivalSession = null;
        }

        private static bool SameLanding(M05LandingReport a, M05LandingReport b)
        {
            return a.CrewArrived == b.CrewArrived &&
                a.ReadyRescueBerths == b.ReadyRescueBerths &&
                a.TenderArrived == b.TenderArrived &&
                a.ReceiverMet == b.ReceiverMet;
        }

        private static bool TryReadCompletedCrossing(Func<string, int> getInt,
            out M04DepartureRecord departure)
        {
            departure = default;
            if (!M04DepartureFacts.TryRead(getInt, out M04DepartureRecord? ready) ||
                !ready.HasValue ||
                !M04DepartureFacts.TryReadSailed(getInt, out bool sailed) ||
                !sailed ||
                !FogListeningFacts.TryRead(getInt, out FogListeningRecord? fog) ||
                !fog.HasValue || !fog.Value.Succeeded)
                return false;

            departure = ready.Value;
            return true;
        }

        private static bool IsValidObservedLanding(M05LandingReport report,
            M04DepartureRecord departure)
        {
            if (report.CrewArrived <= 0 ||
                report.CrewArrived > departure.AssignedCrew ||
                report.ReadyRescueBerths < 0 ||
                report.ReadyRescueBerths > departure.AvailableBerths)
                return false;

            return report.ReadyRescueBerths == 0 ||
                (report.TenderArrived && report.ReceiverMet);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            onCompleted = null;
            landingCheckView?.Dispose();
            landingCheckView = null;
            arrivalSession?.Dispose();
            arrivalSession = null;
        }
    }
}
