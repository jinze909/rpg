using System;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Authoritative ST-06 Tao Ji homecoming session: requires Tao Ji rescued
    /// in M07, tracks broken-chair resolution (preserving unremediated cover-up
    /// trust loss unless explicitly compensated), welcome dinner occurrence,
    /// and actual letter/ticket delivery state.
    /// </summary>
    public sealed class ST06TaoJiHomecomingSession
    {
        public ST06ChairResolution? ChairResolution { get; private set; }
        public bool CoverUpRemediated { get; private set; }
        public bool WelcomeDinnerHeld { get; private set; }
        public bool TaoJiTraveledFar { get; private set; }
        public ST06LetterStatus LetterStatus { get; private set; }
        public bool MarketTicketEnclosed { get; private set; }
        public bool IsSettled { get; private set; }

        public bool HasUnremediatedCoverUpTrustLoss =>
            ChairResolution == ST06ChairResolution.CoverUp && !CoverUpRemediated;

        public static bool TryCreateFromSavedFacts(
            Func<string, int> getInt,
            out ST06TaoJiHomecomingSession session)
        {
            session = null;
            if (getInt == null)
                return false;
            if (!M07ExtractionFacts.TryRead(getInt, out M07ExtractionRecord? m07) ||
                !m07.HasValue || !m07.Value.TaoJiRescued)
                return false;

            session = new ST06TaoJiHomecomingSession();
            return true;
        }

        public void ResolveBrokenChair(ST06ChairResolution resolution)
        {
            if (IsSettled)
                throw new InvalidOperationException("ST-06 homecoming is already settled.");
            if (!Enum.IsDefined(typeof(ST06ChairResolution), resolution))
                throw new ArgumentOutOfRangeException(nameof(resolution));

            ChairResolution = resolution;
            CoverUpRemediated = false;
        }

        public bool TryRemediateCoverUp()
        {
            if (IsSettled || ChairResolution != ST06ChairResolution.CoverUp || CoverUpRemediated)
                return false;
            CoverUpRemediated = true;
            return true;
        }

        public void RecordWelcomeDinner()
        {
            if (IsSettled)
                throw new InvalidOperationException("ST-06 homecoming is already settled.");
            WelcomeDinnerHeld = true;
        }

        public void SetPostReturnLetterProgress(
            bool taoJiTraveledFar,
            ST06LetterStatus letterStatus,
            bool marketTicketEnclosed)
        {
            if (IsSettled)
                throw new InvalidOperationException("ST-06 homecoming is already settled.");
            if (!Enum.IsDefined(typeof(ST06LetterStatus), letterStatus))
                throw new ArgumentOutOfRangeException(nameof(letterStatus));
            if (marketTicketEnclosed && (!taoJiTraveledFar || letterStatus != ST06LetterStatus.Delivered))
                throw new ArgumentException(
                    "Market ticket can only be enclosed when Tao Ji traveled far and the letter is delivered.");

            TaoJiTraveledFar = taoJiTraveledFar;
            LetterStatus = letterStatus;
            MarketTicketEnclosed = marketTicketEnclosed;
        }

        public bool TrySettle(Func<string, int> getInt, Action<string, int> setInt)
        {
            if (IsSettled || !ChairResolution.HasValue)
                return false;

            var record = new ST06HomecomingRecord(
                ChairResolution.Value,
                CoverUpRemediated,
                WelcomeDinnerHeld,
                TaoJiTraveledFar,
                LetterStatus,
                MarketTicketEnclosed);

            if (!ST06TaoJiHomecomingFacts.TryRecord(record, getInt, setInt))
                return false;

            IsSettled = true;
            return true;
        }
    }
}
