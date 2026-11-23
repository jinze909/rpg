using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    [Flags]
    public enum ST02InspectionFlags
    {
        None = 0,
        XieZhiOldBoat = 1 << 0,
        WenShengNewBoat = 1 << 1,
        All = XieZhiOldBoat | WenShengNewBoat
    }

    public enum ST02TrialDisclosure
    {
        PublishToBoth = 1,
        ReturnPrivatelyToEach = 2
    }

    public enum ST02BidMode
    {
        IndependentWithSubcontract = 1,
        JointBidWithWrittenTerms = 2
    }

    public enum ST02LockRescueMethod
    {
        BraceHullStressPoint = 1,
        DismantleLanternFrame = 2
    }

    /// <summary>
    /// Immutable ST-02 ("河面上两盏头灯") outcome persisted in Makinom.
    /// Enforces SCENE_SCRIPT_ST02.md v1.2 (including 3cc627a4 and 7bcd1577):
    /// - Requires inspecting both Xie Zhi's old sample boat (crooked egret)
    ///   and Wen Sheng's new boat (Xie Zhi's hidden mortise-and-tenon joinery).
    /// - Requires gathering dock feedback before resolving the trial disclosure,
    ///   bidding mode, and old-lock rescue.
    /// - Only triggers the joint-bid "free repair" customer dispute when a joint
    ///   bid was actually formed; independent bids never fabricate a joint claim.
    /// </summary>
    public readonly struct ST02RiverLanternsRecord
    {
        public ST02RiverLanternsRecord(
            ST02InspectionFlags inspections,
            bool originalWordsRelayed,
            bool dockFeedbackGathered,
            ST02TrialDisclosure trialDisclosure,
            ST02BidMode bidMode,
            ST02LockRescueMethod lockRescue)
        {
            Inspections = inspections;
            OriginalWordsRelayed = originalWordsRelayed;
            DockFeedbackGathered = dockFeedbackGathered;
            TrialDisclosure = trialDisclosure;
            BidMode = bidMode;
            LockRescue = lockRescue;
        }

        public ST02InspectionFlags Inspections { get; }
        public bool OriginalWordsRelayed { get; }
        public bool DockFeedbackGathered { get; }
        public ST02TrialDisclosure TrialDisclosure { get; }
        public ST02BidMode BidMode { get; }
        public ST02LockRescueMethod LockRescue { get; }

        public bool JointFreeRepairDisputeEligible =>
            BidMode == ST02BidMode.JointBidWithWrittenTerms;

        public bool LanternPreservedIntact =>
            LockRescue == ST02LockRescueMethod.BraceHullStressPoint;

        public bool SharedJointLanternSignature =>
            BidMode == ST02BidMode.JointBidWithWrittenTerms;

        // M15 may show a new lantern display only if the actual next
        // river-lantern festival overlaps the anniversary, never by default.
        public bool ShowM15LanternDisplay(bool nextFestivalCoincides) =>
            nextFestivalCoincides;
    }

    public static class ST02RiverLanternsFacts
    {
        public const string ConfirmedKey = "rpg.return.st02.case-confirmed";
        public const string PendingSignatureKey = "rpg.return.st02.pending-signature";
        public const string InspectionsMaskKey = "rpg.return.st02.inspections-mask";
        public const string OriginalWordsRelayedKey = "rpg.return.st02.original-words-relayed";
        public const string DockFeedbackKey = "rpg.return.st02.dock-feedback-gathered";
        public const string TrialDisclosureKey = "rpg.return.st02.trial-disclosure";
        public const string BidModeKey = "rpg.return.st02.bid-mode";
        public const string LockRescueKey = "rpg.return.st02.lock-rescue";

        public static bool HasOrphanedDownstreamFacts(Func<string, int> getInt)
        {
            return getInt == null ||
                M15FinalRollCallFacts.HasAnyPersistedFacts(getInt) ||
                M16EpilogueFacts.HasAnyPersistedFacts(getInt);
        }

        public static bool IsValidRecord(ST02RiverLanternsRecord record)
        {
            return record.Inspections == ST02InspectionFlags.All &&
                record.DockFeedbackGathered &&
                Enum.IsDefined(typeof(ST02TrialDisclosure), record.TrialDisclosure) &&
                Enum.IsDefined(typeof(ST02BidMode), record.BidMode) &&
                Enum.IsDefined(typeof(ST02LockRescueMethod), record.LockRescue);
        }

        // Commit intent is written before every other regional fact. A torn
        // save can be replayed using precisely the original decisions; a
        // second story encounter cannot overwrite another pending outcome.
        private static int Signature(ST02RiverLanternsRecord record)
        {
            return 1000 + (record.OriginalWordsRelayed ? 1000 : 0) +
                (int)record.TrialDisclosure * 100 +
                (int)record.BidMode * 10 + (int)record.LockRescue;
        }

        private static bool CompatiblePending(Func<string, int> read, string key, int expected)
        {
            int raw = read(key);
            return raw == 0 || raw == expected;
        }

        public static bool CanRecord(ST02RiverLanternsRecord record, Func<string, int> getInt)
        {
            if (getInt == null || getInt(ConfirmedKey) != 0 ||
                !IsValidRecord(record) || HasOrphanedDownstreamFacts(getInt))
                return false;
            int intent = getInt(PendingSignatureKey);
            if (intent == 0)
            {
                return getInt(InspectionsMaskKey) == 0 &&
                    getInt(OriginalWordsRelayedKey) == 0 &&
                    getInt(DockFeedbackKey) == 0 &&
                    getInt(TrialDisclosureKey) == 0 &&
                    getInt(BidModeKey) == 0 &&
                    getInt(LockRescueKey) == 0;
            }
            return intent == Signature(record) &&
                CompatiblePending(getInt, InspectionsMaskKey, (int)record.Inspections) &&
                CompatiblePending(getInt, OriginalWordsRelayedKey, record.OriginalWordsRelayed ? 1 : 0) &&
                CompatiblePending(getInt, DockFeedbackKey, 1) &&
                CompatiblePending(getInt, TrialDisclosureKey, (int)record.TrialDisclosure) &&
                CompatiblePending(getInt, BidModeKey, (int)record.BidMode) &&
                CompatiblePending(getInt, LockRescueKey, (int)record.LockRescue);
        }

        public static bool TryRecord(ST02RiverLanternsRecord record)
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRecord(
                record,
                key => variables.GetInt(key),
                (key, value) => variables.Set(key, value));
        }

        public static bool TryRecord(
            ST02RiverLanternsRecord record,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (getInt == null || setInt == null || !CanRecord(record, getInt))
                return false;

            setInt(PendingSignatureKey, Signature(record));
            setInt(InspectionsMaskKey, (int)record.Inspections);
            setInt(OriginalWordsRelayedKey, record.OriginalWordsRelayed ? 1 : 0);
            setInt(DockFeedbackKey, record.DockFeedbackGathered ? 1 : 0);
            setInt(TrialDisclosureKey, (int)record.TrialDisclosure);
            setInt(BidModeKey, (int)record.BidMode);
            setInt(LockRescueKey, (int)record.LockRescue);
            setInt(ConfirmedKey, 1);
            return true;
        }

        public static bool TryRead(out ST02RiverLanternsRecord? record)
        {
            record = null;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRead(key => variables.GetInt(key), out record);
        }

        public static bool TryRead(Func<string, int> getInt, out ST02RiverLanternsRecord? record)
        {
            record = null;
            if (getInt == null)
                return false;
            int confirmed = getInt(ConfirmedKey);
            if (confirmed == 0)
            {
                // Never expose a torn write as a completed regional story.
                return getInt(PendingSignatureKey) == 0 &&
                    getInt(InspectionsMaskKey) == 0 &&
                    getInt(OriginalWordsRelayedKey) == 0 &&
                    getInt(DockFeedbackKey) == 0 &&
                    getInt(TrialDisclosureKey) == 0 &&
                    getInt(BidModeKey) == 0 &&
                    getInt(LockRescueKey) == 0;
            }
            if (confirmed != 1)
                return false;

            int rawInspections = getInt(InspectionsMaskKey);
            int rawWords = getInt(OriginalWordsRelayedKey);
            int rawFeedback = getInt(DockFeedbackKey);
            int rawDisclosure = getInt(TrialDisclosureKey);
            int rawBid = getInt(BidModeKey);
            int rawRescue = getInt(LockRescueKey);

            if (rawInspections != (int)ST02InspectionFlags.All ||
                (rawWords != 0 && rawWords != 1) ||
                rawFeedback != 1 ||
                !Enum.IsDefined(typeof(ST02TrialDisclosure), rawDisclosure) ||
                !Enum.IsDefined(typeof(ST02BidMode), rawBid) ||
                !Enum.IsDefined(typeof(ST02LockRescueMethod), rawRescue))
                return true;

            var saved = new ST02RiverLanternsRecord(
                (ST02InspectionFlags)rawInspections,
                rawWords == 1,
                dockFeedbackGathered: true,
                (ST02TrialDisclosure)rawDisclosure,
                (ST02BidMode)rawBid,
                (ST02LockRescueMethod)rawRescue);

            if (IsValidRecord(saved) &&
                getInt(PendingSignatureKey) == Signature(saved))
                record = saved;
            return true;
        }

        // Only a genuinely new game resets a concluded regional tale;
        // replaying the M03/M04 voyage does not.
        public static bool TryResetForNewGame()
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryResetForNewGame(
                (key, value) => variables.Set(key, value));
        }

        public static bool TryResetForNewGame(Action<string, int> setInt)
        {
            if (setInt == null)
                return false;
            setInt(ConfirmedKey, 0);
            setInt(PendingSignatureKey, 0);
            setInt(InspectionsMaskKey, 0);
            setInt(OriginalWordsRelayedKey, 0);
            setInt(DockFeedbackKey, 0);
            setInt(TrialDisclosureKey, 0);
            setInt(BidModeKey, 0);
            setInt(LockRescueKey, 0);
            return true;
        }
    }
}
