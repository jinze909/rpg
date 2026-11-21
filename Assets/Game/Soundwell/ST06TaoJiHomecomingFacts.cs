using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    public enum ST06ChairResolution
    {
        RefundAndRebuild = 1,
        TeachJointTogether = 2,
        TaoJiExplainsHerself = 3,
        CoverUp = 4
    }

    public enum ST06LetterStatus
    {
        NotSent = 0,
        InTransit = 1,
        Delivered = 2
    }

    /// <summary>
    /// Immutable ST-06 ("陶霁返乡") outcome persisted in Makinom.
    /// Enforces SCENE_SCRIPT_ST06.md, SCENE_SCRIPT_M15.md &amp; SCENE_SCRIPT_M16.md:
    /// - Strictly requires Tao Ji rescued in M07 (M07ExtractionFacts.TaoJiRescued).
    /// - CoverUp retains UnremediatedTrustLoss until explicitly remediated
    ///   (hanging a new shop sign never erases it).
    /// - M16 desk letter appears only when LetterStatus == Delivered.
    /// - MarketTicketEnclosed is valid only when Delivered AND TaoJiTraveledFar.
    /// </summary>
    public readonly struct ST06HomecomingRecord
    {
        public ST06HomecomingRecord(
            ST06ChairResolution chairResolution,
            bool coverUpRemediated,
            bool welcomeDinnerHeld,
            bool taoJiTraveledFar,
            ST06LetterStatus letterStatus,
            bool marketTicketEnclosed)
        {
            ChairResolution = chairResolution;
            CoverUpRemediated = coverUpRemediated;
            WelcomeDinnerHeld = welcomeDinnerHeld;
            TaoJiTraveledFar = taoJiTraveledFar;
            LetterStatus = letterStatus;
            MarketTicketEnclosed = marketTicketEnclosed;
        }

        public ST06ChairResolution ChairResolution { get; }
        public bool CoverUpRemediated { get; }
        public bool WelcomeDinnerHeld { get; }
        public bool TaoJiTraveledFar { get; }
        public ST06LetterStatus LetterStatus { get; }
        public bool MarketTicketEnclosed { get; }

        public bool HasUnremediatedCoverUpTrustLoss =>
            ChairResolution == ST06ChairResolution.CoverUp && !CoverUpRemediated;

        public bool ShouldPlaceLetterOnM16Desk =>
            LetterStatus == ST06LetterStatus.Delivered;
    }

    public static class ST06TaoJiHomecomingFacts
    {
        public const string ConfirmedKey = "rpg.return.st06.homecoming-confirmed";
        public const string ChairResolutionKey = "rpg.return.st06.chair-resolution";
        public const string CoverUpRemediatedKey = "rpg.return.st06.coverup-remediated";
        public const string WelcomeDinnerKey = "rpg.return.st06.welcome-dinner-held";
        public const string TraveledFarKey = "rpg.return.st06.traveled-far";
        public const string LetterStatusKey = "rpg.return.st06.letter-status";
        public const string MarketTicketKey = "rpg.return.st06.market-ticket-enclosed";

        private static bool CompatiblePending(Func<string, int> getInt, string key, int expected)
        {
            int raw = getInt(key);
            return raw == 0 || raw == expected;
        }

        public static bool CanRecord(ST06HomecomingRecord record, Func<string, int> getInt)
        {
            if (getInt == null || getInt(ConfirmedKey) != 0)
                return false;
            return IsValidRecord(record, getInt) &&
                CompatiblePending(getInt, ChairResolutionKey, (int)record.ChairResolution) &&
                CompatiblePending(getInt, CoverUpRemediatedKey, record.CoverUpRemediated ? 1 : 0) &&
                CompatiblePending(getInt, WelcomeDinnerKey, record.WelcomeDinnerHeld ? 1 : 0) &&
                CompatiblePending(getInt, TraveledFarKey, record.TaoJiTraveledFar ? 1 : 0) &&
                CompatiblePending(getInt, LetterStatusKey, (int)record.LetterStatus) &&
                CompatiblePending(getInt, MarketTicketKey, record.MarketTicketEnclosed ? 1 : 0);
        }

        public static bool TryRecord(ST06HomecomingRecord record)
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRecord(
                record,
                key => variables.GetInt(key),
                (key, value) => variables.Set(key, value));
        }

        public static bool TryRecord(
            ST06HomecomingRecord record,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (getInt == null || setInt == null || !CanRecord(record, getInt))
                return false;

            // Write primary chair resolution and letter status first so an
            // interrupted save locks out competing choices while allowing retries.
            setInt(ChairResolutionKey, (int)record.ChairResolution);
            setInt(LetterStatusKey, (int)record.LetterStatus);
            setInt(CoverUpRemediatedKey, record.CoverUpRemediated ? 1 : 0);
            setInt(WelcomeDinnerKey, record.WelcomeDinnerHeld ? 1 : 0);
            setInt(TraveledFarKey, record.TaoJiTraveledFar ? 1 : 0);
            setInt(MarketTicketKey, record.MarketTicketEnclosed ? 1 : 0);
            setInt(ConfirmedKey, 1);
            return true;
        }

        public static bool TryRead(out ST06HomecomingRecord? record)
        {
            record = null;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRead(key => variables.GetInt(key), out record);
        }

        public static bool TryRead(Func<string, int> getInt, out ST06HomecomingRecord? record)
        {
            record = null;
            if (getInt == null)
                return false;
            if (getInt(ConfirmedKey) != 1)
                return true;

            int rawChair = getInt(ChairResolutionKey);
            int rawRemediated = getInt(CoverUpRemediatedKey);
            int rawDinner = getInt(WelcomeDinnerKey);
            int rawTraveled = getInt(TraveledFarKey);
            int rawLetter = getInt(LetterStatusKey);
            int rawTicket = getInt(MarketTicketKey);

            if (!Enum.IsDefined(typeof(ST06ChairResolution), rawChair) ||
                !Enum.IsDefined(typeof(ST06LetterStatus), rawLetter) ||
                (rawRemediated != 0 && rawRemediated != 1) ||
                (rawDinner != 0 && rawDinner != 1) ||
                (rawTraveled != 0 && rawTraveled != 1) ||
                (rawTicket != 0 && rawTicket != 1))
                return true;

            var saved = new ST06HomecomingRecord(
                (ST06ChairResolution)rawChair,
                rawRemediated == 1,
                rawDinner == 1,
                rawTraveled == 1,
                (ST06LetterStatus)rawLetter,
                rawTicket == 1);
            if (IsValidRecord(saved, getInt))
                record = saved;
            return true;
        }

        public static bool TryResetForNewVoyage(Action<string, int> setInt)
        {
            if (setInt == null)
                return false;
            setInt(ConfirmedKey, 0);
            setInt(ChairResolutionKey, 0);
            setInt(CoverUpRemediatedKey, 0);
            setInt(WelcomeDinnerKey, 0);
            setInt(TraveledFarKey, 0);
            setInt(LetterStatusKey, 0);
            setInt(MarketTicketKey, 0);
            return true;
        }

        private static bool IsValidRecord(ST06HomecomingRecord record, Func<string, int> getInt)
        {
            if (!Enum.IsDefined(typeof(ST06ChairResolution), record.ChairResolution) ||
                !Enum.IsDefined(typeof(ST06LetterStatus), record.LetterStatus))
                return false;

            if (record.CoverUpRemediated &&
                record.ChairResolution != ST06ChairResolution.CoverUp)
                return false;

            if (record.MarketTicketEnclosed &&
                (!record.TaoJiTraveledFar || record.LetterStatus != ST06LetterStatus.Delivered))
                return false;

            if (!M07ExtractionFacts.TryRead(getInt, out M07ExtractionRecord? m07) ||
                !m07.HasValue || !m07.Value.TaoJiRescued)
                return false;

            return true;
        }
    }
}
