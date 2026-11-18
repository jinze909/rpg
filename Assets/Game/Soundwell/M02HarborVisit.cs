using System;
using GamingIsLove.Makinom;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>Connects the M02 office, clue search and review to the live opening.</summary>
    internal sealed class M02HarborVisit : IDisposable
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
        private float layoutAspect;

        internal M02HarborVisit(Transform parent, M01PensionChoice priorChoice, Action onFinished)
        {
            this.parent = parent;
            this.priorChoice = priorChoice;
            this.onFinished = onFinished;
            Tick((float)Mathf.Max(1, Screen.width) / Mathf.Max(1, Screen.height));
        }

        internal void Tick(float aspect)
        {
            if (onFinished == null)
                return;

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
                    Action completed = onFinished;
                    onFinished = null;
                    completed.Invoke();
                    return;
                }
            }

            if (!Mathf.Approximately(layoutAspect, aspect))
            {
                layoutAspect = aspect;
                desk?.LayoutForViewportAspect(aspect);
                inquiry?.LayoutForViewportAspect(aspect);
                review?.LayoutForViewportAspect(aspect);
            }
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

            desk.Dispose();
            desk = null;
            var investigation = new M02BridgeInquiry();
            investigation.Begin();
            inquiry = new M02BridgeInquiryView(parent, investigation, FinishInquiry, ObserveClue);
        }

        private void ObserveClue(M02BridgeEvidence clue)
        {
            if (!M02BridgeEvidenceFacts.TryRecordObserved(clue))
                unwrittenEvidence |= clue;
        }

        private void RetryEvidence()
        {
            RetryClue(M02BridgeEvidence.CleanCableCut);
            RetryClue(M02BridgeEvidence.FerrymanLog);
            RetryClue(M02BridgeEvidence.ThirdBellSignal);
            RetryClue(M02BridgeEvidence.WarReportDraft);
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
            inquiry.Dispose();
            inquiry = null;
            review = new M02MissingReturnView(parent, priorChoice, evidenceFound,
                deskOutcome.TransferAssistanceAccepted, outcome => reviewOutcome = outcome);
        }

        public void Dispose()
        {
            onFinished = null;
            desk?.Dispose();
            inquiry?.Dispose();
            review?.Dispose();
        }
    }
}
