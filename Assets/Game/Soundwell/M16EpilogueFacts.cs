using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    [Flags]
    public enum M16WalkStops
    {
        None = 0,
        WorkshopStop = 1 << 0,
        QiQueWarehouseStop = 1 << 1,
        OulinDinnerTableStop = 1 << 2,
        WestLaneAndHarborOfficeStop = 1 << 3,
        All = WorkshopStop | QiQueWarehouseStop | OulinDinnerTableStop | WestLaneAndHarborOfficeStop
    }

    [Flags]
    public enum M16DeliveredLettersFlags
    {
        None = 0,
        RC01ChenYan = 1 << 0,
        RC02ZhengYuan = 1 << 1,
        RC05ThirdSquad = 1 << 2,
        ST02RiverLanterns = 1 << 3,
        AllTracked = RC01ChenYan | RC02ZhengYuan | RC05ThirdSquad | ST02RiverLanterns
    }

    public enum M16StandingSpot
    {
        PierSeaBreeze = 1,
        MemorialSteleSunset = 2,
        WorkshopDoorway = 3
    }

    public enum M16RespectAgencyTone
    {
        ShenLiDeclinedMeeting = 0,
        RespectedOthersChoices = 1,
        DecidedForOthersOften = 2,
        PartedWaysWithClosure = 3,
        NeutralMixedExperience = 4
    }

    /// <summary>
    /// Immutable M16 ("一个普通的下午") playable epilogue completion record.
    /// Enforces SCENE_SCRIPT_M16.md v1.2 (including 7bcd1577 and 4122fd46):
    /// - Strictly gated on verified M15FinalRollCallFacts.
    /// - Evaluates delivered RC/ST letters strictly from actually completed
    ///   upstream facts (never fabricating letters for unstarted cases).
    /// - Never speaks Shen Li's closing line when she declined to meet.
    /// </summary>
    public readonly struct M16EpilogueRecord
    {
        public M16EpilogueRecord(
            M16WalkStops stopsVisited,
            M16DeliveredLettersFlags lettersReadMask,
            bool deskRegisterReviewed,
            M16StandingSpot standingSpot,
            bool shenLiWillingToMeet,
            M16RespectAgencyTone closingTone)
        {
            StopsVisited = stopsVisited;
            LettersReadMask = lettersReadMask;
            DeskRegisterReviewed = deskRegisterReviewed;
            StandingSpot = standingSpot;
            ShenLiWillingToMeet = shenLiWillingToMeet;
            ClosingTone = closingTone;
        }

        public M16WalkStops StopsVisited { get; }
        public M16DeliveredLettersFlags LettersReadMask { get; }
        public bool DeskRegisterReviewed { get; }
        public M16StandingSpot StandingSpot { get; }
        public bool ShenLiWillingToMeet { get; }
        public M16RespectAgencyTone ClosingTone { get; }

        public bool ShenLiSpokeClosingLine =>
            ShenLiWillingToMeet && ClosingTone != M16RespectAgencyTone.ShenLiDeclinedMeeting;
    }

    public static class M16EpilogueFacts
    {
        public const string ConfirmedKey = "rpg.return.m16.epilogue-confirmed";
        public const string StopsVisitedKey = "rpg.return.m16.walk-stops-mask";
        public const string LettersReadKey = "rpg.return.m16.letters-read-mask";
        public const string DeskRegisterKey = "rpg.return.m16.desk-register-reviewed";
        public const string StandingSpotKey = "rpg.return.m16.standing-spot";
        public const string ShenWillingKey = "rpg.return.m16.shen-willing-to-meet";
        public const string ClosingToneKey = "rpg.return.m16.closing-tone";

        public static M16DeliveredLettersFlags EvaluateEligibleLetters(Func<string, int> getInt)
        {
            if (getInt == null)
                return M16DeliveredLettersFlags.None;

            M16DeliveredLettersFlags mask = M16DeliveredLettersFlags.None;
            if (RC01ChenYanFacts.TryRead(getInt, out RC01ChenYanRecord? rc01) && rc01.HasValue)
                mask |= M16DeliveredLettersFlags.RC01ChenYan;
            if (RC02ZhengYuanFacts.TryRead(getInt, out RC02ZhengYuanRecord? rc02) && rc02.HasValue)
                mask |= M16DeliveredLettersFlags.RC02ZhengYuan;
            if (RC05ThirdSquadFacts.TryRead(getInt, out RC05ThirdSquadRecord? rc05) && rc05.HasValue)
                mask |= M16DeliveredLettersFlags.RC05ThirdSquad;
            if (ST02RiverLanternsFacts.TryRead(getInt, out ST02RiverLanternsRecord? st02) && st02.HasValue)
                mask |= M16DeliveredLettersFlags.ST02RiverLanterns;
            return mask;
        }

        public static bool IsValidRecord(M16EpilogueRecord record, Func<string, int> getInt)
        {
            if (getInt == null ||
                ((int)record.StopsVisited & ~(int)M16WalkStops.All) != 0 ||
                ((int)record.LettersReadMask & ~(int)M16DeliveredLettersFlags.AllTracked) != 0 ||
                !Enum.IsDefined(typeof(M16StandingSpot), record.StandingSpot) ||
                !Enum.IsDefined(typeof(M16RespectAgencyTone), record.ClosingTone))
                return false;

            if (!M15FinalRollCallFacts.TryRead(getInt, out M15FinalRollCallRecord? m15) ||
                !m15.HasValue)
                return false;

            // Cannot record reading letters from uncompleted cases.
            M16DeliveredLettersFlags eligible = EvaluateEligibleLetters(getInt);
            if ((record.LettersReadMask & ~eligible) != 0)
                return false;

            if (!record.ShenLiWillingToMeet)
            {
                if (record.ClosingTone != M16RespectAgencyTone.ShenLiDeclinedMeeting)
                    return false;
            }
            else if (record.ClosingTone == M16RespectAgencyTone.ShenLiDeclinedMeeting)
            {
                return false;
            }

            return true;
        }

        private static bool CompatiblePending(Func<string, int> getInt, string key, int expected)
        {
            int raw = getInt(key);
            return raw == 0 || raw == expected;
        }

        public static bool CanRecord(M16EpilogueRecord record, Func<string, int> getInt)
        {
            return getInt != null &&
                getInt(ConfirmedKey) == 0 &&
                IsValidRecord(record, getInt) &&
                CompatiblePending(getInt, StopsVisitedKey, (int)record.StopsVisited) &&
                CompatiblePending(getInt, LettersReadKey, (int)record.LettersReadMask) &&
                CompatiblePending(getInt, DeskRegisterKey, record.DeskRegisterReviewed ? 1 : 0) &&
                CompatiblePending(getInt, StandingSpotKey, (int)record.StandingSpot) &&
                CompatiblePending(getInt, ShenWillingKey, record.ShenLiWillingToMeet ? 1 : 0) &&
                CompatiblePending(getInt, ClosingToneKey, (int)record.ClosingTone);
        }

        public static bool TryRecord(M16EpilogueRecord record)
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRecord(
                record,
                key => variables.GetInt(key),
                (key, value) => variables.Set(key, value));
        }

        public static bool TryRecord(
            M16EpilogueRecord record,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (getInt == null || setInt == null || !CanRecord(record, getInt))
                return false;

            // Write standing spot and closing tone first so an interrupted save
            // locks out competing epilogue choices while allowing identical retries.
            setInt(StandingSpotKey, (int)record.StandingSpot);
            setInt(ClosingToneKey, (int)record.ClosingTone);
            setInt(ShenWillingKey, record.ShenLiWillingToMeet ? 1 : 0);
            setInt(StopsVisitedKey, (int)record.StopsVisited);
            setInt(LettersReadKey, (int)record.LettersReadMask);
            setInt(DeskRegisterKey, record.DeskRegisterReviewed ? 1 : 0);
            setInt(ConfirmedKey, 1);
            return true;
        }

        public static bool TryRead(out M16EpilogueRecord? record)
        {
            record = null;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRead(key => variables.GetInt(key), out record);
        }

        public static bool TryRead(Func<string, int> getInt, out M16EpilogueRecord? record)
        {
            record = null;
            if (getInt == null)
                return false;
            if (getInt(ConfirmedKey) != 1)
                return true;

            int rawStops = getInt(StopsVisitedKey);
            int rawLetters = getInt(LettersReadKey);
            int rawDesk = getInt(DeskRegisterKey);
            int rawSpot = getInt(StandingSpotKey);
            int rawShen = getInt(ShenWillingKey);
            int rawTone = getInt(ClosingToneKey);

            if ((rawDesk != 0 && rawDesk != 1) ||
                !Enum.IsDefined(typeof(M16StandingSpot), rawSpot) ||
                (rawShen != 0 && rawShen != 1) ||
                !Enum.IsDefined(typeof(M16RespectAgencyTone), rawTone))
                return true;

            var saved = new M16EpilogueRecord(
                (M16WalkStops)rawStops,
                (M16DeliveredLettersFlags)rawLetters,
                rawDesk == 1,
                (M16StandingSpot)rawSpot,
                rawShen == 1,
                (M16RespectAgencyTone)rawTone);

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
            setInt(StopsVisitedKey, 0);
            setInt(LettersReadKey, 0);
            setInt(DeskRegisterKey, 0);
            setInt(StandingSpotKey, 0);
            setInt(ShenWillingKey, 0);
            setInt(ClosingToneKey, 0);
            return true;
        }
    }
}
