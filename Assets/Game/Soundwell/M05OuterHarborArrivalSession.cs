using System;
using GamingIsLove.Makinom;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum M05OuterHarborStage
    {
        SeranLetters,
        HarborWalk,
        Completed
    }

    /// <summary>
    /// Summary of an actually played M05 outer-harbor visit. Carries the
    /// verified arrival snapshot and Seran's letter-packet choice without
    /// claiming that any letter has been delivered in the mining camp.
    /// </summary>
    public readonly struct M05OuterHarborOutcome
    {
        public M05OuterHarborOutcome(M05LandingReport landing,
            M05LetterDecision letterDecision)
        {
            Landing = landing;
            LetterDecision = letterDecision;
        }

        public M05LandingReport Landing { get; }
        public M05LetterDecision LetterDecision { get; }
        public int LettersCarried => M05LettersFacts.LettersCarried(LetterDecision);
        public bool HasUsableLandingCapacity => Landing.HasUsableLandingCapacity;
    }

    /// <summary>
    /// Sequences the authored M05 outer-harbor scenes after a verified arrival:
    /// Seran's eight-letter choice (if not already persisted for this voyage)
    /// followed by the repair-stall, fog-rumor and salt-market walk. Disposes
    /// each UGUI view before opening the next so canvases never overlap.
    /// </summary>
    public sealed class M05OuterHarborArrivalSession : IDisposable
    {
        private readonly Transform parent;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private readonly M05LandingReport landing;
        private readonly int arrivalSequence;
        private Action<M05OuterHarborOutcome> onCompleted;
        private M05SeranLettersView seranView;
        private M05HarborWalkView harborWalkView;
        private M05LetterDecision? letterDecision;
        private M05OuterHarborStage stage;
        private float? lastAspect;
        private bool completedDelivered;
        private bool disposed;

        public M05OuterHarborStage Stage => stage;
        public M05LandingReport Landing => landing;
        public M05LetterDecision? LetterDecision => letterDecision;
        public M05SeranLettersView SeranView => seranView;
        public M05HarborWalkView HarborWalkView => harborWalkView;
        public bool IsDisposed => disposed;

        public static bool TryOpen(Transform parent,
            Action<M05OuterHarborOutcome> onCompleted,
            out M05OuterHarborArrivalSession session)
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
            {
                session = null;
                return false;
            }
            return TryOpen(parent,
                key => variables.GetInt(key),
                (key, value) => variables.Set(key, value),
                onCompleted,
                out session);
        }

        public static bool TryOpen(Transform parent,
            Func<string, int> getInt,
            Action<string, int> setInt,
            Action<M05OuterHarborOutcome> onCompleted,
            out M05OuterHarborArrivalSession session)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (onCompleted == null) throw new ArgumentNullException(nameof(onCompleted));
            session = null;
            if (getInt == null || setInt == null)
                return false;

            // Only a verified M05 arrival may open the outer-harbor sequence.
            // A completed fog leg or M04 manifest never invents an arrival.
            if (!M05LandingFacts.TryRead(getInt, out M05LandingReport? verifiedLanding) ||
                !verifiedLanding.HasValue)
                return false;

            if (!M05LettersFacts.TryRead(getInt, out M05LetterDecision? savedDecision))
                return false; // Corrupt codes are not missing answers.

            session = new M05OuterHarborArrivalSession(parent, getInt, setInt,
                verifiedLanding.Value, getInt(M05LandingFacts.ArrivalSequenceKey),
                savedDecision, onCompleted);
            return true;
        }

        private M05OuterHarborArrivalSession(Transform parent,
            Func<string, int> getInt,
            Action<string, int> setInt,
            M05LandingReport landing, int arrivalSequence,
            M05LetterDecision? existingDecision,
            Action<M05OuterHarborOutcome> onCompleted)
        {
            this.parent = parent;
            this.getInt = getInt;
            this.setInt = setInt;
            this.landing = landing;
            this.arrivalSequence = arrivalSequence;
            this.onCompleted = onCompleted;

            if (existingDecision.HasValue)
            {
                letterDecision = existingDecision.Value;
                OpenHarborWalk();
            }
            else
            {
                stage = M05OuterHarborStage.SeranLetters;
                seranView = new M05SeranLettersView(parent, OnSeranFinished);
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
            seranView?.LayoutForViewportAspect(aspect);
            harborWalkView?.LayoutForViewportAspect(aspect);
        }

        private void OnSeranFinished(M05LetterDecision decision)
        {
            if (disposed || parent == null || stage != M05OuterHarborStage.SeranLetters)
                return;

            // The player may leave/restart a voyage while Seran's panel is
            // still visible. Never attach an old packet decision to a new or
            // now-invalid arrival, even via direct UGUI onClick invocation.
            RequireCurrentLanding();

            // A Makinom write can persist the answer before an interrupted
            // transition throws. Retrying that exact choice must continue,
            // not dead-end on the already committed decision; a different
            // answer or corrupt state must never be silently accepted.
            if (!M05LettersFacts.TryRead(getInt, out M05LetterDecision? saved))
                throw new InvalidOperationException(
                    "Unable to read M05 Seran letter choice.");
            if (saved.HasValue)
            {
                if (saved.Value != decision)
                    throw new InvalidOperationException(
                        "Saved M05 Seran letter choice does not match this visit.");
            }
            else if (!M05LettersFacts.TryRecord(decision, getInt, setInt))
            {
                throw new InvalidOperationException(
                    "Unable to persist M05 Seran letter choice.");
            }

            // Keep the original choice panel usable until the next scene is
            // actually constructed. If that handoff fails, its Complete
            // callback can retry without asking Seran or writing twice.
            OpenHarborWalk();
            letterDecision = decision;
            seranView?.Dispose();
            seranView = null;
        }

        private void OpenHarborWalk()
        {
            M05HarborWalkView next = new M05HarborWalkView(
                parent, OnHarborWalkFinished, OnHarborBeatObserved);
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
            harborWalkView = next;
            stage = M05OuterHarborStage.HarborWalk;
        }

        private void OnHarborBeatObserved(M05HarborBeat observed)
        {
            // Sequential M05 conversations are the same physical visits as
            // the free-roam harbor. Record each witnessed beat in the existing
            // Makinom mask, never fabricate another arrival or story authority.
            RequireCurrentLanding();
            if (!letterDecision.HasValue ||
                !M05LettersFacts.TryRead(getInt, out M05LetterDecision? currentChoice) ||
                currentChoice != letterDecision ||
                !M05HarborVisitFacts.TryRecord(observed, getInt, setInt))
                throw new InvalidOperationException(
                    "Unable to record the current M05 harbor conversation.");
        }

        private void OnHarborWalkFinished()
        {
            if (disposed || completedDelivered || !letterDecision.HasValue ||
                stage != M05OuterHarborStage.HarborWalk)
                return;

            // Both the landing and the letter choice may have been reset
            // after this view opened. Never publish an old harbor visit for
            // the new voyage or silently replace the player's saved answer.
            RequireCurrentLanding();
            if (!M05LettersFacts.TryRead(getInt, out M05LetterDecision? currentChoice) ||
                currentChoice != letterDecision)
                throw new InvalidOperationException(
                    "The saved Seran letter decision no longer belongs to this visit.");

            // Invoke the host before retiring the view so an interrupted
            // transition keeps the final beat armed for retry.
            var outcome = new M05OuterHarborOutcome(landing, letterDecision.Value);
            onCompleted?.Invoke(outcome);

            completedDelivered = true;
            onCompleted = null;
            stage = M05OuterHarborStage.Completed;
            harborWalkView?.Dispose();
            harborWalkView = null;
        }

        private void RequireCurrentLanding()
        {
            // No new save authority: compare the live Makinom arrival with
            // the one actually observed when this scene opened. TryRead
            // rejects changed M03 routes, incomplete voyages, and malformed
            // M05 arrival data before a stale UI can publish anything.
            if (!M05LandingFacts.TryRead(getInt, out M05LandingReport? current) ||
                !current.HasValue ||
                getInt(M05LandingFacts.ArrivalSequenceKey) != arrivalSequence ||
                current.Value.CrewArrived != landing.CrewArrived ||
                current.Value.ReadyRescueBerths != landing.ReadyRescueBerths ||
                current.Value.TenderArrived != landing.TenderArrived ||
                current.Value.ReceiverMet != landing.ReceiverMet)
                throw new InvalidOperationException(
                    "The M05 arrival no longer belongs to the current voyage.");
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            onCompleted = null;
            seranView?.Dispose();
            seranView = null;
            harborWalkView?.Dispose();
            harborWalkView = null;
        }
    }
}
