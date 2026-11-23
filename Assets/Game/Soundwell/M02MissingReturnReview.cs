using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    public enum M02ReviewStage
    {
        NotStarted,
        AtOffice,
        Completed
    }

    /// <summary>
    /// Formal M02 missing-return review, distinct from the M01 wish to apply.
    /// Only a signed war-report draft or an actual transfer-assistance pledge
    /// can support Han Qiao's later approval. Merely seeing the clean cable,
    /// the keeper's notes, or the bell signal cannot substitute for them.
    /// </summary>
    public sealed class M02MissingReturnReview
    {
        private readonly M01PensionChoice priorChoice;
        private readonly bool hasReportDraft;
        private readonly bool assistanceAccepted;
        private bool requested;
        private bool stamped;

        public M02MissingReturnReview(M01PensionChoice priorChoice,
            M02BridgeEvidence recordedEvidence, bool assistanceAccepted)
        {
            M01HomecomingResponse.PensionPathFor(priorChoice);
            const M02BridgeEvidence known =
                M02BridgeEvidence.CleanCableCut | M02BridgeEvidence.FerrymanLog |
                M02BridgeEvidence.ThirdBellSignal | M02BridgeEvidence.WarReportDraft;
            if ((recordedEvidence & ~known) != M02BridgeEvidence.None)
                throw new ArgumentOutOfRangeException(nameof(recordedEvidence));

            this.priorChoice = priorChoice;
            hasReportDraft = (recordedEvidence & M02BridgeEvidence.WarReportDraft) != 0;
            this.assistanceAccepted = assistanceAccepted;
        }

        public M02ReviewStage Stage { get; private set; }
        public bool Requested => requested;
        public bool Approved => stamped;
        public bool HasEligibleSupportingRecord => hasReportDraft || assistanceAccepted;
        public bool CanRequest =>
            priorChoice == M01PensionChoice.RequestMissingReturnReview ||
            priorChoice == M01PensionChoice.LeaveWithoutDecision;
        public bool CanStamp => Stage == M02ReviewStage.AtOffice &&
            requested && !stamped && HasEligibleSupportingRecord;

        public void Begin()
        {
            Require(M02ReviewStage.NotStarted);
            Stage = M02ReviewStage.AtOffice;
        }

        public void Request()
        {
            Require(M02ReviewStage.AtOffice);
            if (!CanRequest || requested)
                throw new InvalidOperationException(
                    "A missing-return review can only be requested explicitly on an unsettled path.");
            requested = true;
        }

        public void Stamp()
        {
            Require(M02ReviewStage.AtOffice);
            if (!CanStamp)
                throw new InvalidOperationException(
                    "Han Qiao cannot approve missing-return status without a request and qualifying evidence.");
            stamped = true;
        }

        public M02MissingReturnOutcome Leave()
        {
            Require(M02ReviewStage.AtOffice);
            Stage = M02ReviewStage.Completed;
            return new M02MissingReturnOutcome(requested, stamped);
        }

        // Makinom records approval only after the player leaves the desk.
        // When that host write fails, restore AtOffice while keeping the
        // player's request and stamp state so the same outcome can be retried.
        public void Leave(Action<M02MissingReturnOutcome> recordWithHost)
        {
            if (recordWithHost == null)
                throw new ArgumentNullException(nameof(recordWithHost));
            M02ReviewStage before = Stage;
            M02MissingReturnOutcome outcome = Leave();
            try
            {
                recordWithHost(outcome);
            }
            catch
            {
                Stage = before;
                throw;
            }
        }

        private void Require(M02ReviewStage expected)
        {
            if (Stage != expected)
                throw new InvalidOperationException("Missing-return review is not at " + expected + ".");
        }
    }

    /// <summary>
    /// Effective pension status is derived from the original M01 historical
    /// choice plus the M02 stamp. Never overwrite the M01 choice or pretend
    /// that requesting a review is an approval.
    /// </summary>
    public sealed class M02MissingReturnOutcome
    {
        internal M02MissingReturnOutcome(bool requested, bool approved)
        {
            Requested = requested;
            Approved = approved;
        }

        public bool Requested { get; }
        public bool Approved { get; }

        public string EffectivePensionPath(M01PensionChoice priorChoice)
        {
            if (Approved && (priorChoice == M01PensionChoice.RequestMissingReturnReview ||
                             priorChoice == M01PensionChoice.LeaveWithoutDecision))
                return "改登记为失踪归还";
            return M01HomecomingResponse.PensionPathFor(priorChoice);
        }
    }

    /// <summary>
    /// Makinom owns the story variables, ORK owns save lifecycle. The host
    /// calls this only after the player actually receives the M02 stamp.
    /// A failed store write must not be represented as successful approval.
    /// </summary>
    public static class M02MissingReturnFacts
    {
        public const string ApprovalKey = "rpg.return.m02.missing-return-approved";

        public static bool TryRecordApproved(M02MissingReturnOutcome outcome)
        {
            if (outcome == null)
                throw new ArgumentNullException(nameof(outcome));
            if (!outcome.Approved)
                throw new InvalidOperationException("Only a stamped approval may be recorded.");
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
                return false;
            return TryRecordApproved(outcome,
                key => variables.GetInt(key),
                (key, value) => variables.Set(key, value));
        }

        public static bool TryRecordApproved(M02MissingReturnOutcome outcome,
            Func<string, int> getInt, Action<string, int> setInt)
        {
            if (outcome == null)
                throw new ArgumentNullException(nameof(outcome));
            if (!outcome.Approved)
                throw new InvalidOperationException("Only a stamped approval may be recorded.");
            if (getInt == null || setInt == null ||
                M06RescuePlanFacts.HasOrphanedDownstreamFacts(getInt))
                return false;
            setInt(ApprovalKey, 1);
            return true;
        }

        public static bool TryReadApproved(out bool approved)
        {
            approved = false;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
                return false;
            return TryReadApproved(key => variables.GetInt(key), out approved);
        }

        public static bool TryReadApproved(Func<string, int> getInt, out bool approved)
        {
            approved = false;
            if (getInt == null)
                return false;
            approved = getInt(ApprovalKey) == 1;
            return true;
        }
    }

    public static class M02MissingReturnCopy
    {
        public static string Description(M02MissingReturnReview review, bool chinese)
        {
            if (review == null)
                throw new ArgumentNullException(nameof(review));
            if (review.Stage == M02ReviewStage.NotStarted)
                throw new InvalidOperationException("Begin the review before rendering it.");

            if (!review.CanRequest)
                return chinese
                    ? "你在西巷已选定公开登记或隐瞒身份，此处不会替你改写旧选择。"
                    : "Your West Lane decision was public registration or concealment; this desk cannot silently rewrite it.";
            if (review.Approved)
                return chinese
                    ? "韩峤在“失踪归还”的文书上盖了印：“拿给船坞看，他们就能给你备船了。”姜婶的旧账仍挂在港务署，留待会盟前后清算。"
                    : "Han Qiao stamps the missing-return papers. 'Show these at the official dock and they can hold a ship for you.' Aunt Jiang's benefit account stays on the harbor ledger for settlement around the summit.";
            if (review.Requested && !review.HasEligibleSupportingRecord)
                return chinese
                    ? "申请已提出，却没有可供韩峤批注的战报底稿，也未答应协理移交；仍未获准。"
                    : "You asked for review, but have neither the war-report draft nor a pledge to check the transfer list. No approval has been granted.";
            if (review.Requested)
                return chinese
                    ? "韩峤看过合格的凭据，愿意批注。必须实际盖印，才能改登记为“失踪归还”。"
                    : "Han Qiao has acceptable grounds for review. The status changes only once he actually stamps the papers.";
            return chinese
                ? "你可提出“失踪归还”申请。仅拿到战报底稿副本，或答应协理移交名册，才有批注条件；申请本身不算批准。"
                : "You can request missing-return review. Only the war-report draft or agreeing to check the transfer list supports a later stamp; requesting alone is not approval.";
        }
    }
}
