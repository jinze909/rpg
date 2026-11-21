using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    [Flags]
    public enum RC02IntakeChecks
    {
        None = 0,
        UpperLeftToothCheck = 1 << 0,
        BattleFlankQuestion = 1 << 1,
        DominantHandQuestion = 1 << 2,
        All = UpperLeftToothCheck | BattleFlankQuestion | DominantHandQuestion
    }

    public enum RC02Resolution
    {
        ExposeImpostor = 1,
        AllowByException = 2,
        AdoptAsShiLeng = 3
    }

    /// <summary>
    /// Immutable RC-02 ("借来的名字") roster-case outcome persisted in Makinom.
    /// Enforces SCENE_SCRIPT_RC02.md v1.2:
    /// - Strictly gated on M08FirstWorldNoticeFacts case-2 availability.
    /// - Requires all 3 intake verification checks and Shi Leng's roof confession.
    /// - AdoptAsShiLeng strictly requires both the mother speaking "石棱" aloud
    ///   and Shi Leng's own consent; Expose/Exception never fake adoption consent.
    /// </summary>
    public readonly struct RC02ZhengYuanRecord
    {
        public RC02ZhengYuanRecord(
            RC02IntakeChecks checksCompleted,
            bool roofConfessionHeard,
            RC02Resolution resolution,
            bool motherCalledShiLeng,
            bool shiLengConsented,
            bool realZhengYuanFound)
        {
            ChecksCompleted = checksCompleted;
            RoofConfessionHeard = roofConfessionHeard;
            Resolution = resolution;
            MotherCalledShiLeng = motherCalledShiLeng;
            ShiLengConsented = shiLengConsented;
            RealZhengYuanFound = realZhengYuanFound;
        }

        public RC02IntakeChecks ChecksCompleted { get; }
        public bool RoofConfessionHeard { get; }
        public RC02Resolution Resolution { get; }
        public bool MotherCalledShiLeng { get; }
        public bool ShiLengConsented { get; }
        public bool RealZhengYuanFound { get; }

        public bool WeakensNegotiationCard =>
            Resolution == RC02Resolution.AllowByException;

        public bool RequiresSeparateRealZhengYuanVerification =>
            Resolution == RC02Resolution.AllowByException;
    }

    public static class RC02ZhengYuanFacts
    {
        public const int RosterCaseNumber = 2;
        public const string ConfirmedKey = "rpg.return.rc02.case-confirmed";
        public const string ChecksMaskKey = "rpg.return.rc02.intake-checks-mask";
        public const string RoofConfessionKey = "rpg.return.rc02.roof-confession";
        public const string ResolutionKey = "rpg.return.rc02.zheng-yuan";
        public const string MotherCalledKey = "rpg.return.rc02.mother-called-shileng";
        public const string ShiLengConsentedKey = "rpg.return.rc02.shileng-consented";
        public const string RealZhengYuanFoundKey = "rpg.return.rc02.real-zheng-yuan-found";

        public static bool IsValidRecord(RC02ZhengYuanRecord record, Func<string, int> getInt)
        {
            if (record.ChecksCompleted != RC02IntakeChecks.All ||
                !record.RoofConfessionHeard ||
                !Enum.IsDefined(typeof(RC02Resolution), record.Resolution))
                return false;

            if (record.Resolution == RC02Resolution.AdoptAsShiLeng)
            {
                if (!record.MotherCalledShiLeng || !record.ShiLengConsented)
                    return false;
            }
            else if (record.MotherCalledShiLeng || record.ShiLengConsented)
            {
                return false;
            }

            if (!M08FirstWorldNoticeFacts.TryCaseAvailable(
                    RosterCaseNumber, getInt, out bool available) ||
                !available)
                return false;

            return true;
        }

        public static bool CanRecord(RC02ZhengYuanRecord record, Func<string, int> getInt)
        {
            return getInt != null &&
                getInt(ConfirmedKey) == 0 &&
                IsValidRecord(record, getInt);
        }

        public static bool TryRecord(RC02ZhengYuanRecord record)
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRecord(
                record,
                key => variables.GetInt(key),
                (key, value) => variables.Set(key, value));
        }

        public static bool TryRecord(
            RC02ZhengYuanRecord record,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (getInt == null || setInt == null || !CanRecord(record, getInt))
                return false;

            setInt(ConfirmedKey, 0);
            setInt(ChecksMaskKey, (int)record.ChecksCompleted);
            setInt(RoofConfessionKey, record.RoofConfessionHeard ? 1 : 0);
            setInt(ResolutionKey, (int)record.Resolution);
            setInt(MotherCalledKey, record.MotherCalledShiLeng ? 1 : 0);
            setInt(ShiLengConsentedKey, record.ShiLengConsented ? 1 : 0);
            setInt(RealZhengYuanFoundKey, record.RealZhengYuanFound ? 1 : 0);
            setInt(ConfirmedKey, 1);
            return true;
        }

        public static bool TryRead(out RC02ZhengYuanRecord? record)
        {
            record = null;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRead(key => variables.GetInt(key), out record);
        }

        public static bool TryRead(Func<string, int> getInt, out RC02ZhengYuanRecord? record)
        {
            record = null;
            if (getInt == null)
                return false;
            if (getInt(ConfirmedKey) != 1)
                return true;

            int rawChecks = getInt(ChecksMaskKey);
            int rawRoof = getInt(RoofConfessionKey);
            int rawRes = getInt(ResolutionKey);
            int rawMother = getInt(MotherCalledKey);
            int rawConsent = getInt(ShiLengConsentedKey);
            int rawReal = getInt(RealZhengYuanFoundKey);

            if (rawChecks != (int)RC02IntakeChecks.All ||
                (rawRoof != 0 && rawRoof != 1) ||
                !Enum.IsDefined(typeof(RC02Resolution), rawRes) ||
                (rawMother != 0 && rawMother != 1) ||
                (rawConsent != 0 && rawConsent != 1) ||
                (rawReal != 0 && rawReal != 1))
                return true;

            var saved = new RC02ZhengYuanRecord(
                (RC02IntakeChecks)rawChecks,
                rawRoof == 1,
                (RC02Resolution)rawRes,
                rawMother == 1,
                rawConsent == 1,
                rawReal == 1);
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
            setInt(ChecksMaskKey, 0);
            setInt(RoofConfessionKey, 0);
            setInt(ResolutionKey, 0);
            setInt(MotherCalledKey, 0);
            setInt(ShiLengConsentedKey, 0);
            setInt(RealZhengYuanFoundKey, 0);
            return true;
        }
    }
}
