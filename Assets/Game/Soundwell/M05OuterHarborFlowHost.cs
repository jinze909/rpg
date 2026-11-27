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
        // A previous M05 landing UI must never confirm into a new sailing,
        // even when the replayed route, hull, crew and berths are identical.
        private readonly int openedSailingSequence;
        private Action<M05OuterHarborOutcome> onCompleted;
        private M05LandingCheckView landingCheckView;
        private M05OuterHarborArrivalSession arrivalSession;
        private M05OuterHarborFlowStage stage;
        private float? lastAspect;
        private bool completedDelivered;
        private bool disposed;

        public M05OuterHarborFlowStage Stage => stage;
        public M05LandingCheckView LandingCheckView =>
            landingCheckView != null && !landingCheckView.IsDisposed ? landingCheckView : null;
        public M05OuterHarborArrivalSession ArrivalSession =>
            arrivalSession != null && !arrivalSession.IsDisposed ? arrivalSession : null;
        public bool IsDisposed => disposed || parent == null;

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

            if (getInt(M04DepartureFacts.SailingSequenceKey) < 0 ||
                !TryReadCompletedCrossing(getInt, out M04DepartureRecord departure))
                return false;

            if (!M05LandingFacts.TryRead(getInt, out M05LandingReport? savedLanding))
                return false;
            // A committed-but-invalid landing is not an unsaved landing.
            // Never show a confirm button that M05LandingFacts.TryRecord
            // must reject because its completion marker is already set.
            if (!savedLanding.HasValue &&
                (getInt(M05LandingFacts.ConfirmedKey) != 0 ||
                 // A torn landing save can leave payload fields written
                 // without publishing the final confirmation marker. A new
                 // arrival must not replace those facts; only the original
                 // open landing panel may retry its interrupted write.
                 getInt(M05LandingFacts.CrewArrivedKey) != 0 ||
                 getInt(M05LandingFacts.ReadyBerthsKey) != 0 ||
                 getInt(M05LandingFacts.TenderArrivedKey) != 0 ||
                 getInt(M05LandingFacts.ReceiverMetKey) != 0 ||
                 // Letters and harbor visits require an actual saved landing.
                 // Refuse orphaned downstream facts on a fresh entrance.
                 getInt(M05LettersFacts.DecisionKey) != 0 ||
                 getInt(M05HarborVisitFacts.VisitedMaskKey) != 0))
                return false;

            // A valid decision from an earlier or unsigned arrival is not
            // this landing's answer; TryRead hides it so Seran can ask again.
            // Corrupt codes still reject the flow before creating an
            // unfinishable landing or dialogue UI.
            if (!M05LettersFacts.TryRead(getInt, out _))
                return false;

            if (savedLanding.HasValue)
            {
                host = new M05OuterHarborFlowHost(parent, getInt, setInt,
                    observedLanding: null, resumeFromSavedLanding: true, onCompleted);
                return true;
            }

            // The save authority also checks the current M03 route against
            // the vessel that actually sailed. Never show a landing confirmation
            // that M05LandingFacts.TryRecord would reject on this visit.
            if (!observedLanding.HasValue ||
                !IsValidObservedLanding(observedLanding.Value, departure) ||
                !M05LandingFacts.CanRecord(observedLanding.Value, getInt))
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
            openedSailingSequence = getInt(M04DepartureFacts.SailingSequenceKey);
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
            if (float.IsNaN(aspect) || float.IsInfinity(aspect) || aspect <= 0f)
                return;
            lastAspect = aspect;
            if (landingCheckView != null)
            {
                if (landingCheckView.IsDisposed) landingCheckView = null;
                else landingCheckView.LayoutForViewportAspect(aspect);
            }
            if (arrivalSession != null)
            {
                if (arrivalSession.IsDisposed) arrivalSession = null;
                else arrivalSession.LayoutForViewportAspect(aspect);
            }
        }

        private void OnLandingConfirmed(M05LandingReport confirmed)
        {
            if (disposed || parent == null || stage != M05OuterHarborFlowStage.LandingCheck)
                return;

            M05LandingCheckView submittingView = landingCheckView;
            void RequireLiveLanding()
            {
                if (disposed || parent == null ||
                    stage != M05OuterHarborFlowStage.LandingCheck ||
                    submittingView == null || submittingView.IsDisposed ||
                    landingCheckView != submittingView)
                    throw new InvalidOperationException("The M05 landing view is no longer active.");
            }

            int ReadLive(string key)
            {
                RequireLiveLanding();
                int value = getInt(key);
                RequireLiveLanding();
                return value;
            }

            void RequireSameSailing()
            {
                if (ReadLive(M04DepartureFacts.SailingSequenceKey) != openedSailingSequence)
                    throw new InvalidOperationException(
                        "The M05 landing check belongs to a previous sailing.");
            }

            void WriteLive(string key, int value)
            {
                // Scene callbacks may reset the voyage without destroying
                // this canvas. Never publish the remaining old report.
                RequireSameSailing();
                setInt(key, value);
                RequireSameSailing();
            }

            // A fact reader can synchronously close the scene. Check the
            // captured view throughout validation, not only before reading.
            // The actual cast-off identity must still match on replay.
            RequireCurrentSailing(ReadLive);

            // Save first, but keep the landing UI until the next scene is
            // successfully constructed. A failed handoff must remain retryable.
            // If the save already succeeded on a previous attempt, accept
            // only the exact same report and never write it a second time.
            if (!M05LandingFacts.TryRead(ReadLive, out M05LandingReport? saved))
                throw new InvalidOperationException(
                    "Unable to read verified M05 landing report.");
            if (saved.HasValue)
            {
                if (!SameLanding(saved.Value, confirmed))
                    throw new InvalidOperationException(
                        "Saved M05 landing differs from the confirmed report.");
            }
            else if (ReadLive(M05LandingFacts.ConfirmedKey) != 0 ||
                !M05LandingFacts.TryRecord(confirmed, ReadLive, WriteLive))
                throw new InvalidOperationException(
                    "Unable to persist verified M05 landing report.");

            RequireSameSailing();
            OpenArrivalSession(RequireSameSailing);
            landingCheckView?.Dispose();
            landingCheckView = null;
        }

        private void OpenArrivalSession(Action requireLive = null)
        {
            requireLive?.Invoke();
            if (!M05OuterHarborArrivalSession.TryOpen(
                parent, getInt, setInt, OnArrivalSessionCompleted,
                out M05OuterHarborArrivalSession next))
                throw new InvalidOperationException(
                    "Unable to open M05 outer-harbor arrival session after verified landing.");

            try
            {
                requireLive?.Invoke();
                if (lastAspect.HasValue)
                    next.LayoutForViewportAspect(lastAspect.Value);
                requireLive?.Invoke();
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
            if (disposed || parent == null || completedDelivered ||
                stage != M05OuterHarborFlowStage.ArrivalSession)
                return;

            RequireCurrentSailing();
            onCompleted?.Invoke(outcome);
            completedDelivered = true;
            onCompleted = null;
            stage = M05OuterHarborFlowStage.Completed;
            arrivalSession?.Dispose();
            arrivalSession = null;
        }

        private void RequireCurrentSailing(Func<string, int> read = null)
        {
            read = read ?? getInt;
            if (read(M04DepartureFacts.SailingSequenceKey) != openedSailingSequence ||
                !M04DepartureFacts.TryReadSailed(read, out bool sailed) || !sailed ||
                !FogListeningFacts.TryRead(read, out FogListeningRecord? fog) ||
                !fog.HasValue || !fog.Value.Succeeded)
                throw new InvalidOperationException(
                    "The M05 landing check belongs to a previous or invalid sailing.");
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
