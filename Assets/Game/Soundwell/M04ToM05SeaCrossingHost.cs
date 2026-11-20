using System;
using GamingIsLove.Makinom;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum M04ToM05SeaCrossingStage
    {
        CastOff,
        FogPassage,
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
        private readonly M05LandingReport? observedLanding;
        private Action<M05OuterHarborOutcome> onOuterHarborCompleted;
        private Action<FogListeningRecord> onFogFailed;
        private M04CastOffView castOffView;
        private M04SailedFogLegHost fogHost;
        private M05OuterHarborFlowHost outerHarborHost;
        private M04ToM05SeaCrossingStage stage;
        private float? lastAspect;
        private bool finishedDelivered;
        private bool disposed;

        public M04ToM05SeaCrossingStage Stage => stage;
        public M04CastOffView CastOffView => castOffView;
        public M04SailedFogLegHost FogHost => fogHost;
        public M05OuterHarborFlowHost OuterHarborHost => outerHarborHost;
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
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (createFogLeg == null) throw new ArgumentNullException(nameof(createFogLeg));
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
                if (getInt(FogListeningFacts.CompletedKey) != 0)
                    return false;
                host = new M04ToM05SeaCrossingHost(
                    parent, getInt, setInt, createFogLeg, observedLanding,
                    onOuterHarborCompleted, onFogFailed);
                host.OpenCastOff();
                return true;
            }

            if (!FogListeningFacts.TryRead(getInt, out FogListeningRecord? savedFog))
                return false;

            if (!savedFog.HasValue)
            {
                if (getInt(FogListeningFacts.CompletedKey) != 0)
                    return false;
                var candidate = new M04ToM05SeaCrossingHost(
                    parent, getInt, setInt, createFogLeg, observedLanding,
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
                parent, getInt, setInt, createFogLeg, observedLanding,
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
            M05LandingReport? observedLanding,
            Action<M05OuterHarborOutcome> onOuterHarborCompleted,
            Action<FogListeningRecord> onFogFailed)
        {
            this.parent = parent;
            this.getInt = getInt;
            this.setInt = setInt;
            this.createFogLeg = createFogLeg;
            this.observedLanding = observedLanding;
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

            // Open the sailed fog leg before releasing castOffView only if valid;
            // dispose castOffView first so only one canvas is ever mounted.
            castOffView?.Dispose();
            castOffView = null;
            if (!TryOpenFogPassage())
                throw new InvalidOperationException(
                    "Unable to open sailed M04 fog passage after cast-off.");
        }

        private bool TryOpenFogPassage()
        {
            FogListeningSession leg = createFogLeg();
            if (leg == null ||
                !M04SailedFogLegHost.TryOpen(
                    parent, leg, getInt, setInt, OnFogCompleted, out fogHost))
                return false;

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
                fogHost?.Dispose();
                fogHost = null;
                stage = M04ToM05SeaCrossingStage.FogFailed;
                finishedDelivered = true;
                Action<FogListeningRecord> callback = onFogFailed;
                onFogFailed = null;
                onOuterHarborCompleted = null;
                callback?.Invoke(record);
                return;
            }

            // Dispose the fog host before opening M05 so only one canvas is active.
            fogHost?.Dispose();
            fogHost = null;
            if (!TryOpenOuterHarbor())
                throw new InvalidOperationException(
                    "Unable to open M05 outer-harbor flow after succeeded fog crossing.");
        }

        private bool TryOpenOuterHarbor()
        {
            if (!M05OuterHarborFlowHost.TryOpen(
                parent, observedLanding, getInt, setInt,
                OnOuterHarborFlowCompleted, out outerHarborHost))
                return false;

            stage = M04ToM05SeaCrossingStage.OuterHarbor;
            if (lastAspect.HasValue)
                outerHarborHost.LayoutForViewportAspect(lastAspect.Value);
            return true;
        }

        private void OnOuterHarborFlowCompleted(M05OuterHarborOutcome outcome)
        {
            if (disposed || finishedDelivered ||
                stage != M04ToM05SeaCrossingStage.OuterHarbor)
                return;

            finishedDelivered = true;
            stage = M04ToM05SeaCrossingStage.Completed;
            outerHarborHost?.Dispose();
            outerHarborHost = null;
            Action<M05OuterHarborOutcome> callback = onOuterHarborCompleted;
            onOuterHarborCompleted = null;
            onFogFailed = null;
            callback?.Invoke(outcome);
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
            outerHarborHost?.Dispose();
            outerHarborHost = null;
        }
    }
}
