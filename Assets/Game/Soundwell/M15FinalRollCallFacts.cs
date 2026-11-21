using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    public enum M15RollCallReader
    {
        Protagonist = 1,
        HanQiao = 2,
        AuntJiang = 3
    }

    public enum M15FinalIdentityResponse
    {
        PublicTrueName = 1,
        PrivateCustodianResponse = 2
    }

    public enum M15PensionClosure
    {
        UnchangedOrPrivate = 1,
        StoppedAndReconciled = 2
    }

    public enum M15SteleInscription
    {
        KeepOriginal = 1,
        AddReturnedAliveLine = 2,
        CarveConsentedVerifiedNames = 3,
        LeaveBlank = 4
    }

    /// <summary>
    /// Immutable M15 memorial roll-call and stele inscription outcome.
    /// Enforces SCENE_SCRIPT_M15.md &amp; STORY_OUTLINE.md invariants:
    /// - PublicTrueName must settle PensionClosure.StoppedAndReconciled.
    /// - PrivateCustodianResponse never forces public disclosure or erases
    ///   earlier M00/M01/M02/M10/M12 history.
    /// - CarveConsentedVerifiedNames requires consentedVerifiedNamesCount > 0.
    /// - Unanswered names are called 3 times and NEVER struck from the roster.
    /// </summary>
    public readonly struct M15FinalRollCallRecord
    {
        public M15FinalRollCallRecord(
            M15RollCallReader reader,
            M15FinalIdentityResponse identityResponse,
            M15PensionClosure pensionClosure,
            M15SteleInscription steleInscription,
            int consentedVerifiedNamesCount,
            bool xiaoqiCalledByNumberSeventyTwo)
        {
            Reader = reader;
            IdentityResponse = identityResponse;
            PensionClosure = pensionClosure;
            SteleInscription = steleInscription;
            ConsentedVerifiedNamesCount = consentedVerifiedNamesCount;
            XiaoqiCalledByNumberSeventyTwo = xiaoqiCalledByNumberSeventyTwo;
        }

        public M15RollCallReader Reader { get; }
        public M15FinalIdentityResponse IdentityResponse { get; }
        public M15PensionClosure PensionClosure { get; }
        public M15SteleInscription SteleInscription { get; }
        public int ConsentedVerifiedNamesCount { get; }
        public bool XiaoqiCalledByNumberSeventyTwo { get; }
    }

    public static class M15FinalRollCallFacts
    {
        public const string ConfirmedKey = "rpg.return.m15.roll-call-confirmed";
        public const string ReaderKey = "rpg.return.m15.roll-call-reader";
        public const string IdentityResponseKey = "rpg.return.m15.final-identity-response";
        public const string PensionClosureKey = "rpg.return.m15.pension-closure";
        public const string SteleInscriptionKey = "rpg.return.m15.stele-inscription";
        public const string ConsentedNamesCountKey = "rpg.return.m15.consented-names-count";
        public const string XiaoqiByNumberKey = "rpg.return.m15.xiaoqi-called-by-number";

        public static bool IsValidRecord(M15FinalRollCallRecord record)
        {
            if (!Enum.IsDefined(typeof(M15RollCallReader), record.Reader) ||
                !Enum.IsDefined(typeof(M15FinalIdentityResponse), record.IdentityResponse) ||
                !Enum.IsDefined(typeof(M15PensionClosure), record.PensionClosure) ||
                !Enum.IsDefined(typeof(M15SteleInscription), record.SteleInscription))
                return false;

            if (record.ConsentedVerifiedNamesCount < 0)
                return false;

            // Public true-name response must settle pension stop & old accounts.
            if (record.IdentityResponse == M15FinalIdentityResponse.PublicTrueName &&
                record.PensionClosure != M15PensionClosure.StoppedAndReconciled)
                return false;

            // Carving names on the public stele requires at least one consented, verified name.
            if (record.SteleInscription == M15SteleInscription.CarveConsentedVerifiedNames &&
                record.ConsentedVerifiedNamesCount <= 0)
                return false;

            return true;
        }

        public static bool HasAnyPersistedFacts(Func<string, int> getInt)
        {
            return getInt == null ||
                getInt(ConfirmedKey) != 0 ||
                getInt(ReaderKey) != 0 ||
                getInt(IdentityResponseKey) != 0 ||
                getInt(PensionClosureKey) != 0 ||
                getInt(SteleInscriptionKey) != 0 ||
                getInt(ConsentedNamesCountKey) != 0 ||
                getInt(XiaoqiByNumberKey) != 0;
        }

        public static bool HasOrphanedDownstreamFacts(Func<string, int> getInt)
        {
            return getInt == null || M16EpilogueFacts.HasAnyPersistedFacts(getInt);
        }

        private static bool CompatiblePending(Func<string, int> getInt, string key, int expected)
        {
            int raw = getInt(key);
            return raw == 0 || raw == expected;
        }

        public static bool CanRecord(M15FinalRollCallRecord record, Func<string, int> getInt)
        {
            return getInt != null &&
                getInt(ConfirmedKey) == 0 &&
                !HasOrphanedDownstreamFacts(getInt) &&
                IsValidRecord(record) &&
                CompatiblePending(getInt, ReaderKey, (int)record.Reader) &&
                CompatiblePending(getInt, IdentityResponseKey, (int)record.IdentityResponse) &&
                CompatiblePending(getInt, PensionClosureKey, (int)record.PensionClosure) &&
                CompatiblePending(getInt, SteleInscriptionKey, (int)record.SteleInscription) &&
                CompatiblePending(getInt, ConsentedNamesCountKey, record.ConsentedVerifiedNamesCount) &&
                CompatiblePending(getInt, XiaoqiByNumberKey, record.XiaoqiCalledByNumberSeventyTwo ? 1 : 0);
        }

        public static bool TryRecord(M15FinalRollCallRecord record)
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRecord(
                record,
                key => variables.GetInt(key),
                (key, value) => variables.Set(key, value));
        }

        public static bool TryRecord(
            M15FinalRollCallRecord record,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (getInt == null || setInt == null || !CanRecord(record, getInt))
                return false;

            // Write primary ceremony decisions first so an interrupted save
            // locks out competing choices while allowing identical retries.
            setInt(IdentityResponseKey, (int)record.IdentityResponse);
            setInt(SteleInscriptionKey, (int)record.SteleInscription);
            setInt(ReaderKey, (int)record.Reader);
            setInt(PensionClosureKey, (int)record.PensionClosure);
            setInt(ConsentedNamesCountKey, record.ConsentedVerifiedNamesCount);
            setInt(XiaoqiByNumberKey, record.XiaoqiCalledByNumberSeventyTwo ? 1 : 0);
            setInt(ConfirmedKey, 1);
            return true;
        }

        public static bool TryRead(out M15FinalRollCallRecord? record)
        {
            record = null;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRead(key => variables.GetInt(key), out record);
        }

        public static bool TryRead(Func<string, int> getInt, out M15FinalRollCallRecord? record)
        {
            record = null;
            if (getInt == null)
                return false;
            if (getInt(ConfirmedKey) != 1)
                return true;

            int rawReader = getInt(ReaderKey);
            int rawIdentity = getInt(IdentityResponseKey);
            int rawPension = getInt(PensionClosureKey);
            int rawStele = getInt(SteleInscriptionKey);
            int rawConsented = getInt(ConsentedNamesCountKey);
            int rawXiaoqi = getInt(XiaoqiByNumberKey);

            if (!Enum.IsDefined(typeof(M15RollCallReader), rawReader) ||
                !Enum.IsDefined(typeof(M15FinalIdentityResponse), rawIdentity) ||
                !Enum.IsDefined(typeof(M15PensionClosure), rawPension) ||
                !Enum.IsDefined(typeof(M15SteleInscription), rawStele) ||
                (rawXiaoqi != 0 && rawXiaoqi != 1))
                return true;

            var saved = new M15FinalRollCallRecord(
                (M15RollCallReader)rawReader,
                (M15FinalIdentityResponse)rawIdentity,
                (M15PensionClosure)rawPension,
                (M15SteleInscription)rawStele,
                rawConsented,
                rawXiaoqi == 1);
            if (IsValidRecord(saved))
                record = saved;
            return true;
        }

        public static bool TryResetForNewVoyage(Action<string, int> setInt)
        {
            if (setInt == null)
                return false;
            setInt(ConfirmedKey, 0);
            setInt(ReaderKey, 0);
            setInt(IdentityResponseKey, 0);
            setInt(PensionClosureKey, 0);
            setInt(SteleInscriptionKey, 0);
            setInt(ConsentedNamesCountKey, 0);
            setInt(XiaoqiByNumberKey, 0);
            return true;
        }
    }
}
