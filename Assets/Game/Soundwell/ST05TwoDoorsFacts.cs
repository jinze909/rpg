using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    public enum ST05BedHandling
    {
        RemoveDoorframeToMove = 1,
        ConvertToTwoYardBenches = 2,
        ReturnToShopWithFee = 3
    }

    [Flags]
    public enum ST05GiftChecks
    {
        None = 0,
        NightDraftingLamp = 1 << 0,
        BreadCoolingStoneSlab = 1 << 1,
        All = NightDraftingLamp | BreadCoolingStoneSlab
    }

    public enum ST05LivingArrangement
    {
        KeepTwoDoorsWithYardTable = 1,
        TrialCohabitAndStudio = 2
    }

    public enum ST05ChengAssignment
    {
        LocalStay = 1,
        OutOfTownSurvey = 2
    }

    /// <summary>
    /// Immutable ST-05 ("两扇门的乔迁宴") outcome persisted in Makinom.
    /// Enforces SCENE_SCRIPT_ST05.md v1.2 (including 2b89ea9b and N27):
    /// - Requires resolving the stuck staircase bed, selecting both practical
    ///   flea-market gifts (drafting lamp &amp; bread cooling slab), and mediating
    ///   the courtyard wind/oven incident.
    /// - TrialCohabitAndStudio strictly requires mutual consent from both
    ///   Yu He and Cheng Wu; KeepTwoDoorsWithYardTable never fakes cohabitation.
    /// - Records Cheng Wu's own st05_cheng_assignment decision (LocalStay vs
    ///   OutOfTownSurvey) so long-distance map bread / remote chore notes and
    ///   M15 delivered-map fallback only activate when he actually took an
    ///   out-of-town survey.
    /// </summary>
    public readonly struct ST05TwoDoorsRecord
    {
        public ST05TwoDoorsRecord(
            ST05BedHandling bedHandling,
            ST05GiftChecks giftsSelected,
            bool yardIncidentResolved,
            ST05LivingArrangement livingArrangement,
            bool bothConsentedToCohabit,
            ST05ChengAssignment chengAssignment)
        {
            BedHandling = bedHandling;
            GiftsSelected = giftsSelected;
            YardIncidentResolved = yardIncidentResolved;
            LivingArrangement = livingArrangement;
            BothConsentedToCohabit = bothConsentedToCohabit;
            ChengAssignment = chengAssignment;
        }

        public ST05BedHandling BedHandling { get; }
        public ST05GiftChecks GiftsSelected { get; }
        public bool YardIncidentResolved { get; }
        public ST05LivingArrangement LivingArrangement { get; }
        public bool BothConsentedToCohabit { get; }
        public ST05ChengAssignment ChengAssignment { get; }

        public bool AllowsLongDistanceMapBread =>
            ChengAssignment == ST05ChengAssignment.OutOfTownSurvey;

        public bool SharedTableWhenBothPresent =>
            LivingArrangement == ST05LivingArrangement.TrialCohabitAndStudio;
    }

    public static class ST05TwoDoorsFacts
    {
        public const string ConfirmedKey = "rpg.return.st05.case-confirmed";
        public const string BedHandlingKey = "rpg.return.st05.bed-handling";
        public const string GiftsMaskKey = "rpg.return.st05.gifts-mask";
        public const string YardIncidentKey = "rpg.return.st05.yard-incident-resolved";
        public const string LivingArrangementKey = "rpg.return.st05.living-arrangement";
        public const string BothConsentedKey = "rpg.return.st05.both-consented-cohabit";
        public const string ChengAssignmentKey = "rpg.return.st05.cheng-assignment";

        public static bool IsValidRecord(ST05TwoDoorsRecord record)
        {
            if (!Enum.IsDefined(typeof(ST05BedHandling), record.BedHandling) ||
                record.GiftsSelected != ST05GiftChecks.All ||
                !record.YardIncidentResolved ||
                !Enum.IsDefined(typeof(ST05LivingArrangement), record.LivingArrangement) ||
                !Enum.IsDefined(typeof(ST05ChengAssignment), record.ChengAssignment))
                return false;

            if (record.LivingArrangement == ST05LivingArrangement.TrialCohabitAndStudio &&
                !record.BothConsentedToCohabit)
                return false;

            if (record.LivingArrangement == ST05LivingArrangement.KeepTwoDoorsWithYardTable &&
                record.BothConsentedToCohabit)
                return false;

            return true;
        }

        public static bool CanRecord(ST05TwoDoorsRecord record, Func<string, int> getInt)
        {
            return getInt != null &&
                getInt(ConfirmedKey) == 0 &&
                IsValidRecord(record);
        }

        public static bool TryRecord(ST05TwoDoorsRecord record)
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRecord(
                record,
                key => variables.GetInt(key),
                (key, value) => variables.Set(key, value));
        }

        public static bool TryRecord(
            ST05TwoDoorsRecord record,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (getInt == null || setInt == null || !CanRecord(record, getInt))
                return false;

            setInt(ConfirmedKey, 0);
            setInt(BedHandlingKey, (int)record.BedHandling);
            setInt(GiftsMaskKey, (int)record.GiftsSelected);
            setInt(YardIncidentKey, record.YardIncidentResolved ? 1 : 0);
            setInt(LivingArrangementKey, (int)record.LivingArrangement);
            setInt(BothConsentedKey, record.BothConsentedToCohabit ? 1 : 0);
            setInt(ChengAssignmentKey, (int)record.ChengAssignment);
            setInt(ConfirmedKey, 1);
            return true;
        }

        public static bool TryRead(out ST05TwoDoorsRecord? record)
        {
            record = null;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRead(key => variables.GetInt(key), out record);
        }

        public static bool TryRead(Func<string, int> getInt, out ST05TwoDoorsRecord? record)
        {
            record = null;
            if (getInt == null)
                return false;
            if (getInt(ConfirmedKey) != 1)
                return true;

            int rawBed = getInt(BedHandlingKey);
            int rawGifts = getInt(GiftsMaskKey);
            int rawYard = getInt(YardIncidentKey);
            int rawLiving = getInt(LivingArrangementKey);
            int rawConsent = getInt(BothConsentedKey);
            int rawCheng = getInt(ChengAssignmentKey);

            if (!Enum.IsDefined(typeof(ST05BedHandling), rawBed) ||
                rawGifts != (int)ST05GiftChecks.All ||
                rawYard != 1 ||
                !Enum.IsDefined(typeof(ST05LivingArrangement), rawLiving) ||
                (rawConsent != 0 && rawConsent != 1) ||
                !Enum.IsDefined(typeof(ST05ChengAssignment), rawCheng))
                return true;

            var saved = new ST05TwoDoorsRecord(
                (ST05BedHandling)rawBed,
                (ST05GiftChecks)rawGifts,
                yardIncidentResolved: true,
                (ST05LivingArrangement)rawLiving,
                rawConsent == 1,
                (ST05ChengAssignment)rawCheng);

            if (IsValidRecord(saved))
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
            setInt(BedHandlingKey, 0);
            setInt(GiftsMaskKey, 0);
            setInt(YardIncidentKey, 0);
            setInt(LivingArrangementKey, 0);
            setInt(BothConsentedKey, 0);
            setInt(ChengAssignmentKey, 0);
            return true;
        }
    }
}
