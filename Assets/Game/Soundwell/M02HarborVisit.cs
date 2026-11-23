using System;
using GamingIsLove.Makinom;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>Connects the M02 office, clue search and review to the live opening.</summary>
    public sealed class M02HarborVisit : IDisposable
    {
        private readonly Transform parent;
        private readonly M01PensionChoice priorChoice;
        private Action onFinished;
        private M02HarborDeskView desk;
        private M02BridgeInquiryView inquiry;
        private M02MissingReturnView review;
        private M02HarborDeskOutcome deskOutcome;
        private M02MissingReturnOutcome reviewOutcome;
        private M02BridgeEvidence evidenceFound;
        private M02BridgeEvidence unwrittenEvidence;
        private bool started;
        private bool finishedInquiry;
        private bool disposed;
        private float layoutAspect;

        public bool IsDisposed => disposed || parent == null;

        public M02HarborVisit(Transform parent, M01PensionChoice priorChoice, Action onFinished)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            if (!Enum.IsDefined(typeof(M01PensionChoice), priorChoice))
                throw new ArgumentOutOfRangeException(nameof(priorChoice));
            if (onFinished == null)
                throw new ArgumentNullException(nameof(onFinished));
            this.parent = parent;
            this.priorChoice = priorChoice;
            this.onFinished = onFinished;
            Tick((float)Mathf.Max(1, Screen.width) / Mathf.Max(1, Screen.height));
        }

        public void Tick(float aspect)
        {
            if (disposed || onFinished == null)
                return;
            if (parent == null)
            {
                Dispose();
                return;
            }

            bool validAspect = !float.IsNaN(aspect) && !float.IsInfinity(aspect) && aspect > 0f;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables != null)
            {
                if (!started)
                {
                    // A replay that reaches M02 starts a new visit. An approval
                    // or clue from that earlier opening must not qualify it.
                    variables.Set(M02OfficialDockFacts.LivingRegistrationStampedKey, 0);
                    variables.Set(M02MissingReturnFacts.ApprovalKey, 0);
                    variables.Set(M02BridgeEvidenceFacts.EvidenceKey, 0);
                    variables.Set(M02QiQueDebtFacts.DebtKnownKey, 0);
                    desk = new M02HarborDeskView(parent, priorChoice, LeaveDesk);
                    if (layoutAspect > 0f)
                        desk.LayoutForViewportAspect(layoutAspect);
                    started = true;
                }

                if (deskOutcome != null && inquiry == null && review == null)
                    OpenInquiry();

                if (unwrittenEvidence != M02BridgeEvidence.None)
                    RetryEvidence();

                if (finishedInquiry && unwrittenEvidence == M02BridgeEvidence.None && review == null)
                    OpenReview();

                if (reviewOutcome != null)
                {
                    if (reviewOutcome.Approved && !M02MissingReturnFacts.TryRecordApproved(reviewOutcome))
                        return;
                    CompleteReview();
                    return;
                }
            }

            if (validAspect && !Mathf.Approximately(layoutAspect, aspect))
            {
                layoutAspect = aspect;
                desk?.LayoutForViewportAspect(aspect);
                inquiry?.LayoutForViewportAspect(aspect);
                review?.LayoutForViewportAspect(aspect);
            }
        }

        // The host may fail to transition after Makinom has recorded an
        // approval. Keep the observed outcome and completion delegate until
        // the handoff actually succeeds, so a subsequent Tick can retry.
        // Clearing them before invoking the callback permanently strands the
        // completed review on transient host failures.
        private void CompleteReview()
        {
            if (reviewOutcome == null || onFinished == null)
                return;

            Action completed = onFinished;
            completed();
            onFinished = null;
            reviewOutcome = null;
            review?.Dispose();
            review = null;
        }

        private void LeaveDesk(M02HarborDeskOutcome outcome)
        {
            deskOutcome = outcome;
        }

        private void OpenInquiry()
        {
            if (deskOutcome.LivingRegistryStamped && !M02OfficialDockFacts.TryRecordLivingRegistration(deskOutcome))
                return;
            if (deskOutcome.QiQueDebtDiscovered && !M02QiQueDebtFacts.TryRecordDiscovered())
                return;

            desk?.Dispose();
            desk = null;
            var investigation = new M02BridgeInquiry();
            investigation.Begin();
            inquiry = new M02BridgeInquiryView(parent, investigation, FinishInquiry, ObserveClue);
            if (layoutAspect > 0f)
                inquiry.LayoutForViewportAspect(layoutAspect);
        }

        private void ObserveClue(M02BridgeEvidence clue)
        {
            if (!M02BridgeEvidenceFacts.TryRecordObserved(clue))
                unwrittenEvidence |= clue;
        }

        private void RetryEvidence()
        {
            foreach (M02BridgeEvidence clue in (M02BridgeEvidence[])Enum.GetValues(typeof(M02BridgeEvidence)))
            {
                if (clue != M02BridgeEvidence.None)
                    RetryClue(clue);
            }
        }

        private void RetryClue(M02BridgeEvidence clue)
        {
            if ((unwrittenEvidence & clue) != 0 && M02BridgeEvidenceFacts.TryRecordObserved(clue))
                unwrittenEvidence &= ~clue;
        }

        private void FinishInquiry(M02BridgeEvidence observed)
        {
            evidenceFound = observed;
            finishedInquiry = true;
        }

        private void OpenReview()
        {
            inquiry?.Dispose();
            inquiry = null;
            review = new M02MissingReturnView(parent, priorChoice, evidenceFound,
                deskOutcome.TransferAssistanceAccepted, outcome => reviewOutcome = outcome);
            if (layoutAspect > 0f)
                review.LayoutForViewportAspect(layoutAspect);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            onFinished = null;
            desk?.Dispose();
            desk = null;
            inquiry?.Dispose();
            inquiry = null;
            review?.Dispose();
            review = null;
        }
    }
}
