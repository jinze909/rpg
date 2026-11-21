using System;

namespace Rpg.Soundwell
{
    public enum ST04EchoValleyPhase
    {
        InspectEmptyMuleCart = 0,
        ChooseValleyEntry = 1,
        RescueSurveyors = 2,
        VerifyEchoBehavior = 3,
        EnterInnerChamber = 4,
        MarkAlliesAndOpenDrain = 5,
        HandleBeasts = 6,
        DecideRoadAndRainWall = 7,
        Completed = 8
    }

    public enum ST04RoadClue
    {
        SlippedClawsAtOldBellRope = 1,
        HoofprintsTurningToRockCrack = 2,
        MossFellOnBellFacingSide = 3
    }

    /// <summary>
    /// Scene-callable ST-04 interaction order. The scene is responsible for
    /// actual visual feedback, the two surveyors, the ORK beast encounters,
    /// and sound experiments; this state machine never simulates those events.
    /// </summary>
    public sealed class ST04EchoValleySession
    {
        private int clues;
        private ST04ValleyEntry? entry;
        private ST04ChamberEntry? chamber;
        private ST04BeastHandling? handling;
        private ST04RoadResolution? resolution;
        private bool rulesVerified;
        private bool marked;
        private bool drainOpened;
        private bool longRoadFunded;
        private bool alternativeWorksiteReady;

        public ST04EchoValleyPhase Phase { get; private set; } =
            ST04EchoValleyPhase.InspectEmptyMuleCart;
        public int SurveyorsRescued { get; private set; }
        public ST04EchoValleyOutcome? CompletedOutcome { get; private set; }

        public static bool TryCreate(Func<string, int> read, out ST04EchoValleySession session)
        {
            session = null;
            if (read == null || read(ST04EchoValleyFacts.ConfirmedKey) != 0)
                return false;

            // A scene may replay after a torn save; the immutable pending
            // intent in ST04EchoValleyFacts determines which choice can commit.
            session = new ST04EchoValleySession();
            return true;
        }

        public bool TryInspectClue(ST04RoadClue clue)
        {
            if (Phase != ST04EchoValleyPhase.InspectEmptyMuleCart ||
                !Enum.IsDefined(typeof(ST04RoadClue), clue))
                return false;
            int bit = 1 << ((int)clue - 1);
            if ((clues & bit) != 0)
                return false;
            clues |= bit;
            if (clues == 7)
                Phase = ST04EchoValleyPhase.ChooseValleyEntry;
            return true;
        }

        public bool TryChooseEntry(ST04ValleyEntry approach)
        {
            if (Phase != ST04EchoValleyPhase.ChooseValleyEntry ||
                !Enum.IsDefined(typeof(ST04ValleyEntry), approach))
                return false;
            entry = approach;
            Phase = ST04EchoValleyPhase.RescueSurveyors;
            return true;
        }

        public bool TryRescueSurveyors(int actualRescuedCount)
        {
            if (Phase != ST04EchoValleyPhase.RescueSurveyors ||
                actualRescuedCount != 2)
                return false;
            SurveyorsRescued = actualRescuedCount;
            Phase = ST04EchoValleyPhase.VerifyEchoBehavior;
            return true;
        }

        public bool TryVerifySoundAndRainMechanism(
            bool continuousBrightEchoAttracts,
            bool lowVoiceDoesNotTurnBeast,
            bool dampBellMakesBeastLoseTarget,
            bool ancientRainAmplifierIdentified,
            bool quarryDiversionOfWaterEstablished)
        {
            if (Phase != ST04EchoValleyPhase.VerifyEchoBehavior ||
                !continuousBrightEchoAttracts || !lowVoiceDoesNotTurnBeast ||
                !dampBellMakesBeastLoseTarget ||
                !ancientRainAmplifierIdentified || !quarryDiversionOfWaterEstablished)
                return false;
            rulesVerified = true;
            Phase = ST04EchoValleyPhase.EnterInnerChamber;
            return true;
        }

        public bool TryEnterInnerChamber(ST04ChamberEntry approach)
        {
            if (Phase != ST04EchoValleyPhase.EnterInnerChamber ||
                !Enum.IsDefined(typeof(ST04ChamberEntry), approach))
                return false;
            chamber = approach;
            Phase = ST04EchoValleyPhase.MarkAlliesAndOpenDrain;
            return true;
        }

        public bool TryOpenDrain(bool companionsMarkedFirst, bool actualDrainOpened)
        {
            if (Phase != ST04EchoValleyPhase.MarkAlliesAndOpenDrain ||
                !companionsMarkedFirst || !actualDrainOpened)
                return false;
            marked = true;
            drainOpened = true;
            Phase = ST04EchoValleyPhase.HandleBeasts;
            return true;
        }

        public bool TryResolveBeasts(ST04BeastHandling actualHandling)
        {
            if (Phase != ST04EchoValleyPhase.HandleBeasts ||
                !Enum.IsDefined(typeof(ST04BeastHandling), actualHandling))
                return false;
            handling = actualHandling;
            Phase = ST04EchoValleyPhase.DecideRoadAndRainWall;
            return true;
        }

        public bool TryChooseResolution(ST04RoadResolution choice,
            bool fundedLongRoad, bool alternativeWorksiteArranged)
        {
            if (Phase != ST04EchoValleyPhase.DecideRoadAndRainWall ||
                !handling.HasValue || resolution.HasValue ||
                !Enum.IsDefined(typeof(ST04RoadResolution), choice))
                return false;

            ST04BeastHandling required =
                choice == ST04RoadResolution.KillAdultsAndCloseWell
                    ? ST04BeastHandling.Slain
                    : choice == ST04RoadResolution.ReturnBeastsAndUseCostlyLongRoad
                        ? ST04BeastHandling.LedBackToOldHabitat
                        : ST04BeastHandling.ContainedAwayFromClosedRoad;
            if (required != handling.Value ||
                (choice == ST04RoadResolution.ReturnBeastsAndUseCostlyLongRoad) != fundedLongRoad ||
                (choice == ST04RoadResolution.PreserveRainWallAndCloseRoad) != alternativeWorksiteArranged)
                return false;
            resolution = choice;
            longRoadFunded = fundedLongRoad;
            this.alternativeWorksiteReady = alternativeWorksiteArranged;
            return true;
        }

        public bool TryCommit(Func<string, int> read, Action<string, int> write)
        {
            if (Phase != ST04EchoValleyPhase.DecideRoadAndRainWall ||
                !entry.HasValue || !chamber.HasValue || !handling.HasValue ||
                !resolution.HasValue || SurveyorsRescued != 2 || !rulesVerified ||
                !marked || !drainOpened || read == null || write == null)
                return false;

            var expected = new ST04EchoValleyRecord(entry.Value, chamber.Value,
                handling.Value, resolution.Value, SurveyorsRescued, rulesVerified,
                marked, drainOpened, longRoadFunded, alternativeWorksiteReady);
            if (!ST04EchoValleyFacts.TryRead(read, out ST04EchoValleyOutcome? saved))
            {
                if (!ST04EchoValleyFacts.CanRecord(expected, read))
                    return false;
            }

            if (saved.HasValue)
            {
                if (!SameStory(expected, saved.Value.Story))
                    return false;
            }
            else if (!ST04EchoValleyFacts.TryRecord(expected, read, write))
            {
                return false;
            }

            if (!ST04EchoValleyFacts.TryRead(read, out ST04EchoValleyOutcome? checkedSave) ||
                !checkedSave.HasValue ||
                !SameStory(expected, checkedSave.Value.Story))
                return false;
            CompletedOutcome = checkedSave;
            Phase = ST04EchoValleyPhase.Completed;
            return true;
        }

        private static bool SameStory(ST04EchoValleyRecord a, ST04EchoValleyRecord b)
        {
            return a.Entry == b.Entry && a.Chamber == b.Chamber &&
                a.Handling == b.Handling && a.Resolution == b.Resolution &&
                a.RescuedSurveyors == b.RescuedSurveyors &&
                a.EchoRulesVerified == b.EchoRulesVerified &&
                a.CompanionsMarked == b.CompanionsMarked &&
                a.DrainOpened == b.DrainOpened &&
                a.LongRoadFunded == b.LongRoadFunded &&
                a.AlternativeWorksiteReady == b.AlternativeWorksiteReady;
        }
    }
}
