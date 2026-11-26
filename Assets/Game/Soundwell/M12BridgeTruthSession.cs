using System;

namespace Rpg.Soundwell
{
    public enum M12Phase
    {
        BellTowerConfrontation = 0,
        StakeholderStatements = 1,
        TruthDisclosureChoice = 2,
        Completed = 3
    }

    /// <summary>
    /// Authoritative M12 ("一面旗帜不等于每个人") state machine.
    /// Enforces Lu Jun's bell-tower confrontation (respecting M03 confession
    /// history), stakeholder statements in any order (never summoning an absent
    /// Shen Li, yet keeping PublishTruth open), optional RC-01 Lin Su echo, and
    /// retry-safe idempotent Makinom persistence.
    /// </summary>
    public sealed class M12BridgeTruthSession
    {
        public M12Phase Phase { get; private set; } = M12Phase.BellTowerConfrontation;
        public bool M03PriorConfession { get; }
        public bool M03SecrecyPromised { get; }
        public bool ShenLiParticipating { get; }
        public bool LinSuEligible { get; }
        public M12LuConfrontationReply? LuReply { get; private set; }
        public M12StatementFlags StatementsHeard { get; private set; } = M12StatementFlags.None;
        public bool LinSuEchoHeard { get; private set; }
        public M12TruthDisclosure? Disclosure { get; private set; }
        public M12BridgeTruthRecord? CompletedRecord { get; private set; }
        public bool IsCompleted => Phase == M12Phase.Completed;

        private M12BridgeTruthSession(
            bool m03PriorConfession,
            bool m03SecrecyPromised,
            bool shenLiParticipating,
            bool linSuEligible)
        {
            M03PriorConfession = m03PriorConfession;
            M03SecrecyPromised = m03PriorConfession && m03SecrecyPromised;
            ShenLiParticipating = shenLiParticipating;
            LinSuEligible = linSuEligible;
        }

        public static bool TryCreateFromSavedFacts(
            Func<string, int> getInt,
            bool m03PriorConfession,
            bool m03SecrecyPromised,
            bool shenLiParticipating,
            bool linSuPresent,
            out M12BridgeTruthSession session)
        {
            session = null;
            if (getInt == null || M12BridgeTruthFacts.HasOrphanedDownstreamFacts(getInt))
                return false;
            if (m03SecrecyPromised && !m03PriorConfession)
                return false;
            if (!M11ReverseRescueFacts.TryRead(getInt, out M11ReverseRescueRecord? m11) ||
                !m11.HasValue)
                return false;

            if (!M12BridgeTruthFacts.TryEvaluateRC01Completed(
                    getInt, out bool rc01Completed))
                return false;

            session = new M12BridgeTruthSession(
                m03PriorConfession,
                m03SecrecyPromised,
                shenLiParticipating,
                rc01Completed && linSuPresent);
            return true;
        }

        public bool TryConfrontLuJun(M12LuConfrontationReply reply)
        {
            if (Phase != M12Phase.BellTowerConfrontation ||
                !Enum.IsDefined(typeof(M12LuConfrontationReply), reply))
                return false;
            if (reply == M12LuConfrontationReply.YouSaidThatLastTime && !M03PriorConfession)
                return false;

            LuReply = reply;
            Phase = M12Phase.StakeholderStatements;
            return true;
        }

        public bool TryHearLinSuEcho()
        {
            if (Phase != M12Phase.StakeholderStatements || !LinSuEligible || LinSuEchoHeard)
                return false;

            LinSuEchoHeard = true;
            return true;
        }

        public bool TryHearStatement(M12StatementFlags statement)
        {
            if (Phase != M12Phase.StakeholderStatements)
                return false;
            if (statement != M12StatementFlags.JiangShenHeard &&
                statement != M12StatementFlags.HanAndYiLeiHeard &&
                statement != M12StatementFlags.ShenLiHeard)
                return false;
            if (statement == M12StatementFlags.ShenLiHeard && !ShenLiParticipating)
                return false;
            if ((StatementsHeard & statement) != 0)
                return false;

            StatementsHeard |= statement;
            M12StatementFlags required = ShenLiParticipating
                ? M12StatementFlags.AllWithShenLi
                : M12StatementFlags.RequiredBase;
            if (StatementsHeard == required)
                Phase = M12Phase.TruthDisclosureChoice;
            return true;
        }

        public bool TryDecideTruthAndCommit(
            M12TruthDisclosure disclosure,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (Phase != M12Phase.TruthDisclosureChoice ||
                !LuReply.HasValue ||
                !Enum.IsDefined(typeof(M12TruthDisclosure), disclosure) ||
                getInt == null || setInt == null)
                return false;

            bool m10HanExposed = M12BridgeTruthFacts.EvaluateM10HanExposedPublicly(getInt);
            var record = new M12BridgeTruthRecord(
                M03PriorConfession,
                M03SecrecyPromised,
                LuReply.Value,
                ShenLiParticipating,
                StatementsHeard,
                LinSuEchoHeard,
                m10HanExposed,
                disclosure);

            if (!M12BridgeTruthFacts.TryRead(getInt, out M12BridgeTruthRecord? existing))
                return false;

            // Recover idempotently if a prior write committed ConfirmedKey = 1
            // before its acknowledgement was lost.
            if (existing.HasValue)
            {
                if (!Matches(record, existing.Value))
                    return false;
            }
            else if (getInt(M12BridgeTruthFacts.ConfirmedKey) != 0 ||
                     !M12BridgeTruthFacts.TryRecord(record, getInt, setInt))
            {
                return false;
            }

            if (!M12BridgeTruthFacts.TryRead(getInt, out M12BridgeTruthRecord? saved) ||
                !saved.HasValue || !Matches(record, saved.Value))
                return false;

            Disclosure = disclosure;
            CompletedRecord = saved.Value;
            Phase = M12Phase.Completed;
            return true;
        }

        private static bool Matches(M12BridgeTruthRecord expected, M12BridgeTruthRecord saved)
        {
            return expected.M03PriorConfession == saved.M03PriorConfession &&
                expected.M03SecrecyPromised == saved.M03SecrecyPromised &&
                expected.LuReply == saved.LuReply &&
                expected.ShenLiParticipating == saved.ShenLiParticipating &&
                expected.StatementsHeard == saved.StatementsHeard &&
                expected.LinSuEchoHeard == saved.LinSuEchoHeard &&
                expected.M10HanExposedPublicly == saved.M10HanExposedPublicly &&
                expected.Disclosure == saved.Disclosure;
        }
    }
}
