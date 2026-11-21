using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    public enum M12LuConfrontationReply
    {
        SomeWantNoMoreWar = 1,
        YouTookTheirRoad = 2,
        YouSaidThatLastTime = 3
    }

    [Flags]
    public enum M12StatementFlags
    {
        None = 0,
        JiangShenHeard = 1 << 0,
        HanAndYiLeiHeard = 1 << 1,
        ShenLiHeard = 1 << 2,
        RequiredBase = JiangShenHeard | HanAndYiLeiHeard,
        AllWithShenLi = JiangShenHeard | HanAndYiLeiHeard | ShenLiHeard
    }

    public enum M12TruthDisclosure
    {
        PublishTruth = 1,
        WillingKnowersLedger = 2,
        SealTruth = 3
    }

    /// <summary>
    /// Immutable M12 ("一面旗帜不等于每个人") truth-disclosure outcome persisted
    /// in Makinom. Enforces SCENE_SCRIPT_M12.md v1.2 (including 7bcd1577 and 0ffe8dcb):
    /// - Strictly gated on verified M11ReverseRescueFacts (and M10AssemblyFacts).
    /// - Preserves M03 lu_confession history: YouSaidThatLastTime is only valid
    ///   when Lu Jun already confessed in M03; otherwise he confesses here first.
    /// - Never summons an absent Shen Li to speak, yet never locks PublishTruth
    ///   when she is absent.
    /// - Cross-checks live M10AssemblyFacts.ExposedHanQiaoPublicly so sealing M12
    ///   never revokes an already-published M10 battle-report draft and Han Qiao's
    ///   office status stays consistent.
    /// </summary>
    public readonly struct M12BridgeTruthRecord
    {
        public M12BridgeTruthRecord(
            bool m03PriorConfession,
            bool m03SecrecyPromised,
            M12LuConfrontationReply luReply,
            bool shenLiParticipating,
            M12StatementFlags statementsHeard,
            bool linSuEchoHeard,
            bool m10HanExposedPublicly,
            M12TruthDisclosure disclosure)
        {
            M03PriorConfession = m03PriorConfession;
            M03SecrecyPromised = m03SecrecyPromised;
            LuReply = luReply;
            ShenLiParticipating = shenLiParticipating;
            StatementsHeard = statementsHeard;
            LinSuEchoHeard = linSuEchoHeard;
            M10HanExposedPublicly = m10HanExposedPublicly;
            Disclosure = disclosure;
        }

        public bool M03PriorConfession { get; }
        public bool M03SecrecyPromised { get; }
        public M12LuConfrontationReply LuReply { get; }
        public bool ShenLiParticipating { get; }
        public M12StatementFlags StatementsHeard { get; }
        public bool LinSuEchoHeard { get; }
        public bool M10HanExposedPublicly { get; }
        public M12TruthDisclosure Disclosure { get; }

        public bool FirstConfessionHeardHere => !M03PriorConfession;
        public bool HanQiaoAlreadyOutOfOffice => M10HanExposedPublicly;
        public bool M10PublishedDraftRemainsIrrevocable => M10HanExposedPublicly;
        public bool RequiresDraftHandoverToYiLei =>
            Disclosure == M12TruthDisclosure.WillingKnowersLedger;
        public bool BreaksLuSecrecyPromise =>
            M03SecrecyPromised && Disclosure != M12TruthDisclosure.SealTruth;
    }

    public static class M12BridgeTruthFacts
    {
        public const string ConfirmedKey = "rpg.return.m12.truth-confirmed";
        public const string M03PriorConfessionKey = "rpg.return.m12.m03-prior-confession";
        public const string M03SecrecyPromisedKey = "rpg.return.m12.m03-secrecy-promised";
        public const string LuReplyKey = "rpg.return.m12.lu-confrontation-reply";
        public const string ShenLiParticipatingKey = "rpg.return.m12.shen-li-participating";
        public const string StatementsMaskKey = "rpg.return.m12.statements-mask";
        public const string LinSuEchoKey = "rpg.return.m12.lin-su-echo-heard";
        public const string M10HanExposedKey = "rpg.return.m12.m10-han-exposed";
        public const string DisclosureKey = "rpg.return.m12.truth-disclosure";

        public static bool EvaluateM10HanExposedPublicly(Func<string, int> getInt)
        {
            return getInt != null &&
                M10AssemblyFacts.TryRead(getInt, out M10AssemblyRecord? m10) &&
                m10.HasValue &&
                m10.Value.ExposedHanQiaoPublicly;
        }

        public static bool IsValidRecord(M12BridgeTruthRecord record, Func<string, int> getInt)
        {
            if (getInt == null ||
                !Enum.IsDefined(typeof(M12LuConfrontationReply), record.LuReply) ||
                !Enum.IsDefined(typeof(M12TruthDisclosure), record.Disclosure))
                return false;

            // Cannot promise secrecy for a confession that was never heard in M03.
            if (record.M03SecrecyPromised && !record.M03PriorConfession)
                return false;

            // "You said that last time" only appears when M03 already heard confession.
            if (record.LuReply == M12LuConfrontationReply.YouSaidThatLastTime &&
                !record.M03PriorConfession)
                return false;

            M12StatementFlags expectedStatements = record.ShenLiParticipating
                ? M12StatementFlags.AllWithShenLi
                : M12StatementFlags.RequiredBase;
            if (record.StatementsHeard != expectedStatements)
                return false;

            if (!M11ReverseRescueFacts.TryRead(getInt, out M11ReverseRescueRecord? m11) ||
                !m11.HasValue)
                return false;

            bool expectedHanExposed = EvaluateM10HanExposedPublicly(getInt);
            if (record.M10HanExposedPublicly != expectedHanExposed)
                return false;

            return true;
        }

        public static bool HasAnyPersistedFacts(Func<string, int> getInt)
        {
            return getInt == null ||
                getInt(ConfirmedKey) != 0 ||
                getInt(M03PriorConfessionKey) != 0 ||
                getInt(M03SecrecyPromisedKey) != 0 ||
                getInt(LuReplyKey) != 0 ||
                getInt(ShenLiParticipatingKey) != 0 ||
                getInt(StatementsMaskKey) != 0 ||
                getInt(LinSuEchoKey) != 0 ||
                getInt(M10HanExposedKey) != 0 ||
                getInt(DisclosureKey) != 0;
        }

        public static bool HasOrphanedDownstreamFacts(Func<string, int> getInt)
        {
            return getInt == null ||
                M13DepartureRiskFacts.HasAnyPersistedFacts(getInt) ||
                M13DepartureRiskFacts.HasOrphanedDownstreamFacts(getInt);
        }

        private static bool CompatiblePending(Func<string, int> getInt, string key, int expected)
        {
            int raw = getInt(key);
            return raw == 0 || raw == expected;
        }

        public static bool CanRecord(M12BridgeTruthRecord record, Func<string, int> getInt)
        {
            return getInt != null &&
                getInt(ConfirmedKey) == 0 &&
                !HasOrphanedDownstreamFacts(getInt) &&
                IsValidRecord(record, getInt) &&
                CompatiblePending(getInt, M03PriorConfessionKey, record.M03PriorConfession ? 1 : 0) &&
                CompatiblePending(getInt, M03SecrecyPromisedKey, record.M03SecrecyPromised ? 1 : 0) &&
                CompatiblePending(getInt, LuReplyKey, (int)record.LuReply) &&
                CompatiblePending(getInt, ShenLiParticipatingKey, record.ShenLiParticipating ? 1 : 0) &&
                CompatiblePending(getInt, StatementsMaskKey, (int)record.StatementsHeard) &&
                CompatiblePending(getInt, LinSuEchoKey, record.LinSuEchoHeard ? 1 : 0) &&
                CompatiblePending(getInt, M10HanExposedKey, record.M10HanExposedPublicly ? 1 : 0) &&
                CompatiblePending(getInt, DisclosureKey, (int)record.Disclosure);
        }

        public static bool TryRecord(M12BridgeTruthRecord record)
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRecord(
                record,
                key => variables.GetInt(key),
                (key, value) => variables.Set(key, value));
        }

        public static bool TryRecord(
            M12BridgeTruthRecord record,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (getInt == null || setInt == null || !CanRecord(record, getInt))
                return false;

            // Write primary disclosure and confrontation reply first so an
            // interrupted save locks out competing choices while allowing retries.
            setInt(DisclosureKey, (int)record.Disclosure);
            setInt(LuReplyKey, (int)record.LuReply);
            setInt(StatementsMaskKey, (int)record.StatementsHeard);
            setInt(M03PriorConfessionKey, record.M03PriorConfession ? 1 : 0);
            setInt(M03SecrecyPromisedKey, record.M03SecrecyPromised ? 1 : 0);
            setInt(ShenLiParticipatingKey, record.ShenLiParticipating ? 1 : 0);
            setInt(LinSuEchoKey, record.LinSuEchoHeard ? 1 : 0);
            setInt(M10HanExposedKey, record.M10HanExposedPublicly ? 1 : 0);
            setInt(ConfirmedKey, 1);
            return true;
        }

        public static bool TryRead(out M12BridgeTruthRecord? record)
        {
            record = null;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRead(key => variables.GetInt(key), out record);
        }

        public static bool TryRead(Func<string, int> getInt, out M12BridgeTruthRecord? record)
        {
            record = null;
            if (getInt == null)
                return false;
            if (getInt(ConfirmedKey) != 1)
                return true;

            int rawPrior = getInt(M03PriorConfessionKey);
            int rawSecrecy = getInt(M03SecrecyPromisedKey);
            int rawReply = getInt(LuReplyKey);
            int rawShen = getInt(ShenLiParticipatingKey);
            int rawStatements = getInt(StatementsMaskKey);
            int rawLinSu = getInt(LinSuEchoKey);
            int rawHanExposed = getInt(M10HanExposedKey);
            int rawDisclosure = getInt(DisclosureKey);

            if ((rawPrior != 0 && rawPrior != 1) ||
                (rawSecrecy != 0 && rawSecrecy != 1) ||
                !Enum.IsDefined(typeof(M12LuConfrontationReply), rawReply) ||
                (rawShen != 0 && rawShen != 1) ||
                (rawLinSu != 0 && rawLinSu != 1) ||
                (rawHanExposed != 0 && rawHanExposed != 1) ||
                !Enum.IsDefined(typeof(M12TruthDisclosure), rawDisclosure))
                return true;

            var saved = new M12BridgeTruthRecord(
                rawPrior == 1,
                rawSecrecy == 1,
                (M12LuConfrontationReply)rawReply,
                rawShen == 1,
                (M12StatementFlags)rawStatements,
                rawLinSu == 1,
                rawHanExposed == 1,
                (M12TruthDisclosure)rawDisclosure);

            if (IsValidRecord(saved, getInt))
                record = saved;
            return true;
        }

        public static bool TryResetForNewVoyage()
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryResetForNewVoyage(
                (key, value) => variables.Set(key, value));
        }

        public static bool TryResetForNewVoyage(Action<string, int> setInt)
        {
            if (setInt == null)
                return false;
            setInt(ConfirmedKey, 0);
            setInt(M03PriorConfessionKey, 0);
            setInt(M03SecrecyPromisedKey, 0);
            setInt(LuReplyKey, 0);
            setInt(ShenLiParticipatingKey, 0);
            setInt(StatementsMaskKey, 0);
            setInt(LinSuEchoKey, 0);
            setInt(M10HanExposedKey, 0);
            setInt(DisclosureKey, 0);
            return true;
        }
    }
}
