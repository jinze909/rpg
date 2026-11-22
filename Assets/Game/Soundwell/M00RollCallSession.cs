using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Runs M00's authored three-call ritual as a scene-owned interaction.
    /// The view owns presentation; M00RollCallSequence owns choices, and
    /// Makinom owns the settled identity fact before the scene may continue.
    /// The host attaches this component when the memorial crowd quiets.
    /// </summary>
    public sealed class M00RollCallSession : MonoBehaviour
    {
        public const float PauseBetweenCallsSeconds = 1.5f;

        private M00RollCallSequence sequence;
        private M00RollCallView view;
        private Action<M00RollCallOutcome> onFinished;
        private float nextCallAt = float.NaN;
        private bool awaitingPersistence;
        private float layoutAspect;

        public M00RollCallSequence Sequence => sequence;
        public M00RollCallView View => HasLiveView() ? view : null;
        public bool AwaitingPersistence => awaitingPersistence;

        private bool HasLiveView() => view != null && !view.IsDisposed;

        private void PruneDeadView()
        {
            if (view == null || !view.IsDisposed)
                return;
            view.Dispose();
            view = null;
            nextCallAt = float.NaN;
        }

        public void Begin(Action<M00RollCallOutcome> completed)
        {
            if (sequence != null)
                throw new InvalidOperationException("The M00 roll call has already begun.");
            if (completed == null)
                throw new ArgumentNullException(nameof(completed));

            SoundwellInputAdapter.EnsureEventSystem(transform);
            SoundwellInputAdapter.SetUiContext();

            sequence = new M00RollCallSequence();
            sequence.Begin();
            onFinished = completed;
            view = new M00RollCallView(transform, sequence, OnContinue);
        }

        private void Update()
        {
            LayoutForViewportAspect((float)Mathf.Max(1, Screen.width) / Mathf.Max(1, Screen.height));
            Tick(Time.unscaledTime);
        }

        public void LayoutForViewportAspect(float aspect)
        {
            PruneDeadView();
            if (view == null || float.IsNaN(aspect) || float.IsInfinity(aspect) ||
                aspect <= 0f || Mathf.Approximately(aspect, layoutAspect))
                return;

            layoutAspect = aspect;
            view.LayoutForViewportAspect(aspect);
        }

        /// <summary>
        /// Explicit time input keeps the exact pause testable without waiting
        /// for real frames; Update uses unscaled time so a paused game cannot
        /// trap the ritual between Han Qiao's calls.
        /// </summary>
        public void Tick(float now)
        {
            // If the save owner is momentarily unavailable, do not advance to
            // Han Qiao's meeting and do not drop the player's authored choice.
            if (awaitingPersistence)
            {
                DeliverWhenRecorded();
                return;
            }

            PruneDeadView();
            if (view == null || float.IsNaN(now) || float.IsInfinity(now))
                return;

            if (!view.IsWaitingForNextCall)
            {
                nextCallAt = float.NaN;
                return;
            }

            if (float.IsNaN(nextCallAt))
            {
                nextCallAt = now + PauseBetweenCallsSeconds;
                return;
            }

            if (now < nextCallAt)
                return;

            view.AdvanceAfterUnansweredCall();
            nextCallAt = float.NaN;
        }

        private void OnContinue(M00RollCallOutcome outcome)
        {
            if (sequence == null || !sequence.IsComplete || sequence.Outcome != outcome)
                throw new InvalidOperationException("M00 cannot finish an unsettled roll call.");

            awaitingPersistence = true;
            DeliverWhenRecorded();
        }

        private void DeliverWhenRecorded()
        {
            if (!awaitingPersistence || !M00RollCallFacts.TryRecord(sequence))
                return;

            awaitingPersistence = false;
            Action<M00RollCallOutcome> completed = onFinished;
            onFinished = null;
            view?.Dispose();
            view = null;
            completed?.Invoke(sequence.Outcome);
        }

        private void OnDestroy()
        {
            view?.Dispose();
            view = null;
            onFinished = null;
        }
    }
}
