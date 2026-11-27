using System;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Authoritative M15 memorial roll-call and stele inscription session.
    /// Enforces the three-call stop rule (stop on first answer; call three
    /// times without crossing off if unanswered), preserves Xiaoqi's #72
    /// call when unnamed, distinguishes public vs. private identity closure,
    /// and guards stele name carving on actual verified consent.
    /// </summary>
    public sealed class M15FinalRollCallSession
    {
        public bool BridgeTruthSealed { get; }
        public int ConsentedVerifiedNamesCount { get; }
        public bool XiaoqiHasCustomName { get; }
        public string XiaoqiCustomName { get; }
        public bool IsCompleted { get; private set; }
        public M15FinalRollCallRecord? CompletedRecord { get; private set; }

        public M15FinalRollCallSession(
            bool bridgeTruthSealed,
            int consentedVerifiedNamesCount,
            string xiaoqiCustomName = null)
        {
            if (consentedVerifiedNamesCount < 0)
                throw new ArgumentOutOfRangeException(nameof(consentedVerifiedNamesCount));

            BridgeTruthSealed = bridgeTruthSealed;
            ConsentedVerifiedNamesCount = consentedVerifiedNamesCount;
            XiaoqiCustomName = string.IsNullOrWhiteSpace(xiaoqiCustomName)
                ? null
                : xiaoqiCustomName.Trim();
            XiaoqiHasCustomName = XiaoqiCustomName != null;
        }

        /// <summary>
        /// Yan military roll-call rule: up to 3 calls; stop immediately once
        /// the person answers; unanswered names take all 3 calls and remain
        /// on the roster ("下次"), never struck off.
        /// </summary>
        public static int CallsSpokenForEntry(bool answeredInPerson)
        {
            return answeredInPerson ? 1 : 3;
        }

        public string FormatXiaoqiRollCallEntry(bool zh)
        {
            if (XiaoqiHasCustomName)
                return XiaoqiCustomName;
            return zh ? "七十二（未起名）" : "#72 (unnamed)";
        }

        public bool CanChooseSteleInscription(M15SteleInscription inscription)
        {
            if (!Enum.IsDefined(typeof(M15SteleInscription), inscription))
                return false;
            if (inscription == M15SteleInscription.CarveConsentedVerifiedNames)
                return ConsentedVerifiedNamesCount > 0;
            // Anonymous "They returned alive" line is permitted even if bridge
            // truth is sealed, without disclosing unconsented names or liability.
            return true;
        }

        public bool TryCompleteRollCall(
            M15RollCallReader reader,
            M15FinalIdentityResponse identityResponse,
            M15SteleInscription steleInscription,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (IsCompleted || getInt == null || setInt == null ||
                !CanChooseSteleInscription(steleInscription))
                return false;

            M15PensionClosure pension = identityResponse == M15FinalIdentityResponse.PublicTrueName
                ? M15PensionClosure.StoppedAndReconciled
                : M15PensionClosure.UnchangedOrPrivate;

            var record = new M15FinalRollCallRecord(
                reader,
                identityResponse,
                pension,
                steleInscription,
                ConsentedVerifiedNamesCount,
                xiaoqiCalledByNumberSeventyTwo: !XiaoqiHasCustomName);

            if (!M15FinalRollCallFacts.IsValidRecord(record) ||
                !M15FinalRollCallFacts.TryRead(getInt, out M15FinalRollCallRecord? existing))
                return false;

            // A writer can commit the record before its acknowledgement is lost.
            // A retry may acknowledge only that exact record, without new writes.
            if (existing.HasValue)
            {
                if (!Matches(record, existing.Value))
                    return false;
            }
            else if (!M15FinalRollCallFacts.TryRecord(record, getInt, setInt))
            {
                return false;
            }

            // Returning from a writer is not proof that the confirmed record
            // actually landed. Do not move the UI to Completed until readback.
            if (!M15FinalRollCallFacts.TryRead(getInt, out M15FinalRollCallRecord? persisted) ||
                !persisted.HasValue || !Matches(record, persisted.Value))
                return false;

            CompletedRecord = persisted.Value;
            IsCompleted = true;
            return true;
        }

        private static bool Matches(M15FinalRollCallRecord expected, M15FinalRollCallRecord actual)
        {
            return expected.Reader == actual.Reader &&
                expected.IdentityResponse == actual.IdentityResponse &&
                expected.PensionClosure == actual.PensionClosure &&
                expected.SteleInscription == actual.SteleInscription &&
                expected.ConsentedVerifiedNamesCount == actual.ConsentedVerifiedNamesCount &&
                expected.XiaoqiCalledByNumberSeventyTwo == actual.XiaoqiCalledByNumberSeventyTwo;
        }
    }
}
