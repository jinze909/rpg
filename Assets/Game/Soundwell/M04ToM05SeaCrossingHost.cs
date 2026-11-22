using System;
using GamingIsLove.Makinom;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum M04ToM05SeaCrossingStage
    {
        CastOff,
        FogPassage,
        LandingObservation,
        OuterHarbor,
        FogFailed,
        Completed
    }

    /// <summary>
    /// End-to-end M04-to-M05 sea-crossing orchestrator that sequences an actual
    /// cast-off (M04CastOffView), the live sailed fog leg (M04SailedFogLegHost),
    /// and—only after a succeeded fog crossing—the Red Salt outer-harbor flow
    /// (M05OuterHarborFlowHost), passing observed tender delay, usable rescue
    /// berths, and Seran letter choices through verbatim without inventing any
    /// departure, crossing, or arrival facts.
    /// </summary>
    public sealed class M04ToM05SeaCrossingHost : IDisposable
    {
        private readonly Transform parent;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private readonly Func<FogListeningSession> createFogLeg;
        // Read actual dock observations when the voyage reaches shore, not
        // at pre-departure setup. The owning scene remains the authority.
        private readonly Func<M05LandingReport?> observeLandingAtArrival;
        private readonly M04DepartureRecord departure;
        private int openedSailingSequence;
        private readonly bool useInteractiveLandingObservation;
        private Action<M05OuterHarborOutcome> onOuterHarborCompleted;
        private Action<FogListeningRecord> onFogFailed;
        private M04CastOffView castOffView;
        private M04SailedFogLegHost fogHost;
        private M05OuterHarborFlowHost outerHarborHost;
        private M05LandingObservationView landingObservationView;
        private M04ToM05SeaCrossingStage stage;
        private float? lastAspect;
        private bool finishedDelivered;
        private bool deliveringCompletion;
        private bool disposed;

        public M04ToM05SeaCrossingStage Stage => stage;
        public M04CastOffView CastOffView => castOffView;
        public M04SailedFogLegHost FogHost => fogHost;
        public M05OuterHarborFlowHost OuterHarborHost => outerHarborHost;
        public M05LandingObservationView LandingObservationView => landingObservationView;
        public bool IsDisposed => disposed;

        public static bool TryOpen(
            Transform parent,
            Func<FogListeningSession> createFogLeg,
            M05LandingReport? observedLanding,
            Action<M05OuterHarborOutcome> onOuterHarborCompleted,
            Action<FogListeningRecord> onFogFailed,
            out M04ToM05SeaCrossingHost host)
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
            {
                host = null;
                return false;
            }
            return TryOpen(
                parent,
                createFogLeg,
                observedLanding,
                key => variables.GetInt(key),
                (key, value) => variables.Set(key, value),
                onOuterHarborCompleted,
                onFogFailed,
                out host);
        }

        // Compatibility with existing callers that already hold a witnessed
        // landing report (or are resuming a previously saved landing).
        public static bool TryOpen(
            Transform parent,
            Func<FogListeningSession> createFogLeg,
            M05LandingReport? observedLanding,
            Func<string, int> getInt,
            Action<string, int> setInt,
            Action<M05OuterHarborOutcome> onOuterHarborCompleted,
            Action<FogListeningRecord> onFogFailed,
            out M04ToM05SeaCrossingHost host)
        {
            return TryOpenWithLandingObservation(
                parent, createFogLeg, () => observedLanding, getInt, setInt,
                onOuterHarborCompleted, onFogFailed, out host);
        }

        // Use this path for real gameplay: observed crew, tender and berth
        // counts must come from a live dock observation AFTER the fog crossing.
        // The provider is not evaluated at cast-off or during the fog leg.
        public static bool TryOpenWithLandingObservation(
            Transform parent,
            Func<FogListeningSession> createFogLeg,
            Func<M05LandingReport?> observeLandingAtArrival,
            Func<string, int> getInt,
            Action<string, int> setInt,
            Action<M05OuterHarborOutcome> onOuterHarborCompleted,
            Action<FogListeningRecord> onFogFailed,
            out M04ToM05SeaCrossingHost host)
        {
            return TryOpenWithLandingObservationCore(parent, createFogLeg,
                observeLandingAtArrival, getInt, setInt, onOuterHarborCompleted,
                onFogFailed, useInteractiveLandingObservation: false, out host);
        }

        /// <summary>
        /// Real-play path for a scene that has no separate world observer yet:
        /// after a successful fog crossing, ask the player to inspect and enter
        /// the actual dock state. Nothing is copied from the departure manifest.
        /// </summary>
        public static bool TryOpenWithInteractiveLandingObservation(
            Transform parent,
            Func<FogListeningSession> createFogLeg,
            Action<M05OuterHarborOutcome> onOuterHarborCompleted,
            Action<FogListeningRecord> onFogFailed,
            out M04ToM05SeaCrossingHost host)
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
            {
                host = null;
                return false;
            }
            return TryOpenWithInteractiveLandingObservation(parent, createFogLeg,
                key => variables.GetInt(key),
                (key, value) => variables.Set(key, value),
                onOuterHarborCompleted, onFogFailed, out host);
        }

        public static bool TryOpenWithInteractiveLandingObservation(
            Transform parent,
            Func<FogListeningSession> createFogLeg,
            Func<string, int> getInt,
            Action<string, int> setInt,
            Action<M05OuterHarborOutcome> onOuterHarborCompleted,
            Action<FogListeningRecord> onFogFailed,
            out M04ToM05SeaCrossingHost host)
        {
            return TryOpenWithLandingObservationCore(parent, createFogLeg,
                () => null, getInt, setInt, onOuterHarborCompleted, onFogFailed,
                useInteractiveLandingObservation: true, out host);
        }

        private static bool TryOpenWithLandingObservationCore(
            Transform parent,
            Func<FogListeningSession> createFogLeg,
            Func<M05LandingReport?> observeLandingAtArrival,
            Func<string, int> getInt,
            Action<string, int> setInt,
            Action<M05OuterHarborOutcome> onOuterHarborCompleted,
            Action<FogListeningRecord> onFogFailed,
            bool useInteractiveLandingObservation,
            out M04ToM05SeaCrossingHost host)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (createFogLeg == null) throw new ArgumentNullException(nameof(createFogLeg));
            if (observeLandingAtArrival == null)
                throw new ArgumentNullException(nameof(observeLandingAtArrival));
            if (onOuterHarborCompleted == null) throw new ArgumentNullException(nameof(onOuterHarborCompleted));
            if (onFogFailed == null) throw new ArgumentNullException(nameof(onFogFailed));
            host = null;
            if (getInt == null || setInt == null)
                return false;

            // Require a verified M04 manifest matching the currently chosen M03 route.
            if (!M03LuJunFacts.TryRead(getInt, out _, out _, out M03VoyageChoice? chosenRoute) ||
                !chosenRoute.HasValue ||
                !M04DepartureFacts.TryRead(getInt, out M04DepartureRecord? departure) ||
                !departure.HasValue ||
                departure.Value.Route != chosenRoute.Value ||
                !M04DepartureFacts.TryReadSailed(getInt, out bool sailed))
                return false;

            int sailedBit = getInt(M04DepartureFacts.SailedKey);
            if (sailedBit != 0 && sailedBit != 1)
                return false;

            if (!sailed)
            {
                // Reject torn fog data *before* cast-off. Otherwise the UI
                // could sail and then strand the player at the fog handoff.
                if (getInt(FogListeningFacts.CompletedKey) != 0 ||
                    FogListeningFacts.HasUnconfirmedPayload(getInt))
                    return false;
                host = new M04ToM05SeaCrossingHost(
                    parent, getInt, setInt, createFogLeg, observeLandingAtArrival,
                    departure.Value, useInteractiveLandingObservation,
                    onOuterHarborCompleted, onFogFailed);
                host.OpenCastOff();
                return true;
            }

            if (!FogListeningFacts.TryRead(getInt, out FogListeningRecord? savedFog))
                return false;

            if (!savedFog.HasValue)
            {
                if (getInt(FogListeningFacts.CompletedKey) != 0 ||
                    FogListeningFacts.HasUnconfirmedPayload(getInt))
                    return false;
                var candidate = new M04ToM05SeaCrossingHost(
                    parent, getInt, setInt, createFogLeg, observeLandingAtArrival,
                    departure.Value, useInteractiveLandingObservation,
                    onOuterHarborCompleted, onFogFailed);
                if (!candidate.TryOpenFogPassage())
                {
                    candidate.Dispose();
                    return false;
                }
                host = candidate;
                return true;
            }

            // A failed fog crossing cannot silently enter M05 or overwrite itself.
            if (!savedFog.Value.Succeeded)
                return false;

            var resumeCandidate = new M04ToM05SeaCrossingHost(
                parent, getInt, setInt, createFogLeg, observeLandingAtArrival,
                departure.Value, useInteractiveLandingObservation,
                onOuterHarborCompleted, onFogFailed);
            if (!resumeCandidate.TryOpenOuterHarbor())
            {
                resumeCandidate.Dispose();
                return false;
            }
            host = resumeCandidate;
            return true;
        }

        private M04ToM05SeaCrossingHost(
            Transform parent,
            Func<string, int> getInt,
            Action<string, int> setInt,
            Func<FogListeningSession> createFogLeg,
            Func<M05LandingReport?> observeLandingAtArrival,
            M04DepartureRecord departure,
            bool useInteractiveLandingObservation,
            Action<M05OuterHarborOutcome> onOuterHarborCompleted,
            Action<FogListeningRecord> onFogFailed)
        {
            this.parent = parent;
            this.getInt = getInt;
            this.setInt = setInt;
            this.createFogLeg = createFogLeg;
            this.observeLandingAtArrival = observeLandingAtArrival;
            this.departure = departure;
            openedSailingSequence = getInt(M04DepartureFacts.SailingSequenceKey);
            this.useInteractiveLandingObservation = useInteractiveLandingObservation;
            this.onOuterHarborCompleted = onOuterHarborCompleted;
            this.onFogFailed = onFogFailed;
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
            castOffView?.LayoutForViewportAspect(aspect);
            fogHost?.LayoutForViewportAspect(aspect);
            landingObservationView?.LayoutForViewportAspect(aspect);
            outerHarborHost?.LayoutForViewportAspect(aspect);
        }

        private void OpenCastOff()
        {
            stage = M04ToM05SeaCrossingStage.CastOff;
            castOffView = new M04CastOffView(parent, getInt, setInt, OnCastOffSailed);
            if (lastAspect.HasValue)
                castOffView.LayoutForViewportAspect(lastAspect.Value);
        }

        private void OnCastOffSailed()
        {
            if (disposed || parent == null || stage != M04ToM05SeaCrossingStage.CastOff)
                return;

            // Open the fog host before releasing castOffView so a failed
            // handoff keeps M04CastOffView armed for a clean retry without
            // double-sailing or stranding the player with zero canvases.
            if (!TryOpenFogPassage())
                throw new InvalidOperationException(
                    "Unable to open sailed M04 fog passage after cast-off.");

            // Cast-off increments the monotonic sailing identity. Capture the
            // actual new trip, not the pre-departure sequence from host creation.
            openedSailingSequence = getInt(M04DepartureFacts.SailingSequenceKey);
            castOffView?.Dispose();
            castOffView = null;
        }

        private bool TryOpenFogPassage()
        {
            FogListeningSession leg = createFogLeg();
            if (leg == null ||
                !M04SailedFogLegHost.TryOpen(
                    parent, leg, getInt, setInt, OnFogCompleted, out M04SailedFogLegHost nextFog))
                return false;

            fogHost = nextFog;
            stage = M04ToM05SeaCrossingStage.FogPassage;
            if (lastAspect.HasValue)
                fogHost.LayoutForViewportAspect(lastAspect.Value);
            return true;
        }

        private void OnFogCompleted(FogListeningRecord record)
        {
            if (disposed || finishedDelivered || parent == null ||
                stage != M04ToM05SeaCrossingStage.FogPassage)
                return;

            if (!record.Succeeded)
            {
                // The fog outcome has already been saved by M04SailedFogLegHost.
                // Do not tear down Continue until the scene's failure handoff
                // actually accepts it. If navigation throws, the same saved
                // failure can be delivered again without replaying the fog
                // encounter, rewriting its result, or claiming M05 arrival.
                stage = M04ToM05SeaCrossingStage.FogFailed;
                try
                {
                    onFogFailed?.Invoke(record);
                }
                catch
                {
                    if (!disposed)
                        stage = M04ToM05SeaCrossingStage.FogPassage;
                    throw;
                }
                if (disposed) return;
                finishedDelivered = true;
                onFogFailed = null;
                onOuterHarborCompleted = null;
                fogHost?.Dispose();
                fogHost = null;
                return;
            }

            // Open M05 before disposing fogHost so a failed outer-harbor
            // handoff leaves FogListeningView's Continue button armed for
            // retry without double-writing FogListeningFacts.
            if (!TryOpenOuterHarbor())
                throw new InvalidOperationException(
                    "Unable to open M05 outer-harbor flow after succeeded fog crossing.");

            fogHost?.Dispose();
            fogHost = null;
        }

        private bool TryOpenOuterHarbor()
        {
            // A persisted landing needs no new observation. Otherwise sample
            // an owning scene provider after the fog crossing; if none exists,
            // the interactive observer records what the player actually sees.
            if (!M05LandingFacts.TryRead(getInt, out M05LandingReport? savedLanding))
                return false;
            if (savedLanding.HasValue)
                return TryOpenOuterHarborFlow(null);

            M05LandingReport? observation = observeLandingAtArrival();
            if (observation.HasValue)
                return TryOpenOuterHarborFlow(observation);

            if (!useInteractiveLandingObservation)
                return false;

            landingObservationView = new M05LandingObservationView(
                parent, departure, getInt, OnLandingObserved);
            stage = M04ToM05SeaCrossingStage.LandingObservation;
            if (lastAspect.HasValue)
                landingObservationView.LayoutForViewportAspect(lastAspect.Value);
            return true;
        }

        private void OnLandingObserved(M05LandingReport report)
        {
            if (disposed || parent == null ||
                stage != M04ToM05SeaCrossingStage.LandingObservation)
                return;

            // A dock panel can remain open while another voyage is started.
            // Never let its observation attach to a later cast-off.
            if (getInt(M04DepartureFacts.SailingSequenceKey) != openedSailingSequence)
                throw new InvalidOperationException(
                    "The M05 observation belongs to a previous sailing.");

            if (!M05LandingFacts.TryRead(getInt, out M05LandingReport? savedLanding))
                throw new InvalidOperationException(
                    "Unable to read the current M05 landing state.");
            if (savedLanding.HasValue)
            {
                if (!SameLanding(savedLanding.Value, report))
                    throw new InvalidOperationException(
                        "The saved M05 landing differs from the current dock observation.");
            }
            else if (getInt(M05LandingFacts.ConfirmedKey) != 0 ||
                !M05LandingFacts.TryRecord(report, getInt, setInt))
            {
                throw new InvalidOperationException(
                    "Unable to persist the observed M05 arrival.");
            }

            if (!TryOpenOuterHarborFlow(null))
                throw new InvalidOperationException(
                    "Unable to open M05 outer-harbor flow after the observed arrival.");
        }

        private bool TryOpenOuterHarborFlow(M05LandingReport? observation)
        {
            if (!M05OuterHarborFlowHost.TryOpen(
                parent, observation, getInt, setInt,
                OnOuterHarborFlowCompleted, out M05OuterHarborFlowHost nextHarbor))
                return false;

            try
            {
                if (lastAspect.HasValue)
                    nextHarbor.LayoutForViewportAspect(lastAspect.Value);
            }
            catch
            {
                nextHarbor.Dispose();
                throw;
            }

            outerHarborHost = nextHarbor;
            stage = M04ToM05SeaCrossingStage.OuterHarbor;
            landingObservationView?.Dispose();
            landingObservationView = null;
            return true;
        }

        private static bool SameLanding(M05LandingReport a, M05LandingReport b)
        {
            return a.CrewArrived == b.CrewArrived &&
                a.ReadyRescueBerths == b.ReadyRescueBerths &&
                a.TenderArrived == b.TenderArrived &&
                a.ReceiverMet == b.ReceiverMet;
        }

        private void OnOuterHarborFlowCompleted(M05OuterHarborOutcome outcome)
        {
            if (disposed || finishedDelivered || deliveringCompletion ||
                stage != M04ToM05SeaCrossingStage.OuterHarbor)
                return;

            // The next scene must accept this verified M05 outcome before we
            // retire the last harbor beat. Otherwise a transient navigation
            // error strands the player after Seran's saved letter choice with
            // no remaining Continue button to retry the same journey.
            deliveringCompletion = true;
            try
            {
                onOuterHarborCompleted?.Invoke(outcome);
            }
            finally
            {
                deliveringCompletion = false;
            }

            // The callback can legitimately dispose the host while replacing
            // the scene. Never revive its UI or call it a second time.
            if (disposed) return;
            finishedDelivered = true;
            stage = M04ToM05SeaCrossingStage.Completed;
            outerHarborHost?.Dispose();
            outerHarborHost = null;
            onOuterHarborCompleted = null;
            onFogFailed = null;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            onOuterHarborCompleted = null;
            onFogFailed = null;
            castOffView?.Dispose();
            castOffView = null;
            fogHost?.Dispose();
            fogHost = null;
            landingObservationView?.Dispose();
            landingObservationView = null;
            outerHarborHost?.Dispose();
            outerHarborHost = null;
        }
    }
}
