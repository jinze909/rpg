using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    public enum RC05Resolution
    {
        CivilianPardon = 1,
        EnlistUnderLuJun = 2,
        ContinueSmuggling = 3
    }

    /// <summary>
    /// Immutable RC-05 ("山上的第三班") roster-case outcome persisted in Makinom.
    /// Enforces SCENE_SCRIPT_RC05.md v1.2:
    /// - Strictly gated on M08FirstWorldNoticeFacts case-5 availability.
    /// - Requires completing the mountain-pass roll-call observation and the
    ///   salt-grain boat delivery trial before Hao Lie meets the player.
    /// - Real Zheng Yuan discovery requires explicit squad-roster and testimony
    ///   verification (never auto-inferred from left-handedness alone).
    /// - Tracks skirmish status (avoidable via negotiation) and M00 stone callback.
    /// </summary>
    public readonly struct RC05ThirdSquadRecord
    {
        public RC05ThirdSquadRecord(
            bool trialDeliveryVerified,
            RC05Resolution resolution,
            bool skirmishOccurred,
            bool realZhengYuanVerified,
            bool oldWomanStoneMentioned)
        {
            TrialDeliveryVerified = trialDeliveryVerified;
            Resolution = resolution;
            SkirmishOccurred = skirmishOccurred;
            RealZhengYuanVerified = realZhengYuanVerified;
            OldWomanStoneMentioned = oldWomanStoneMentioned;
        }

        public bool TrialDeliveryVerified { get; }
        public RC05Resolution Resolution { get; }
        public bool SkirmishOccurred { get; }
        public bool RealZhengYuanVerified { get; }
        public bool OldWomanStoneMentioned { get; }

        public bool NeedsM10SupplyContract =>
            Resolution == RC05Resolution.CivilianPardon;

        public bool EnlistedUnderLuJun =>
            Resolution == RC05Resolution.EnlistUnderLuJun;

        public bool HarmsHanQiaoTaxRoute =>
            Resolution == RC05Resolution.ContinueSmuggling;

        public bool SquadRemainsOnMountain =>
            Resolution == RC05Resolution.ContinueSmuggling;
    }

    public static class RC05ThirdSquadFacts
    {
        public const int RosterCaseNumber = 5;
        public const string ConfirmedKey = "rpg.return.rc05.case-confirmed";
        public const string TrialDeliveryKey = "rpg.return.rc05.trial-delivery-verified";
        public const string ResolutionKey = "rpg.return.rc05.third-squad";
        public const string SkirmishKey = "rpg.return.rc05.skirmish-occurred";
        public const string RealZhengYuanVerifiedKey = "rpg.return.rc05.real-zheng-yuan-verified";
        public const string OldWomanStoneMentionedKey = "rpg.return.rc05.old-woman-stone-mentioned";

        public static bool IsValidRecord(RC05ThirdSquadRecord record, Func<string, int> getInt)
        {
            if (!record.TrialDeliveryVerified ||
                !Enum.IsDefined(typeof(RC05Resolution), record.Resolution))
                return false;

            if (!M08FirstWorldNoticeFacts.TryCaseAvailable(
                    RosterCaseNumber, getInt, out bool available) ||
                !available)
                return false;

            return true;
        }

        public static bool CanRecord(RC05ThirdSquadRecord record, Func<string, int> getInt)
        {
            return getInt != null &&
                getInt(ConfirmedKey) == 0 &&
                IsValidRecord(record, getInt);
        }

        public static bool TryRecord(RC05ThirdSquadRecord record)
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRecord(
                record,
                key => variables.GetInt(key),
                (key, value) => variables.Set(key, value));
        }

        public static bool TryRecord(
            RC05ThirdSquadRecord record,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (getInt == null || setInt == null || !CanRecord(record, getInt))
                return false;

            setInt(ConfirmedKey, 0);
            setInt(TrialDeliveryKey, record.TrialDeliveryVerified ? 1 : 0);
            setInt(ResolutionKey, (int)record.Resolution);
            setInt(SkirmishKey, record.SkirmishOccurred ? 1 : 0);
            setInt(RealZhengYuanVerifiedKey, record.RealZhengYuanVerified ? 1 : 0);
            setInt(OldWomanStoneMentionedKey, record.OldWomanStoneMentioned ? 1 : 0);
            setInt(ConfirmedKey, 1);
            return true;
        }

        public static bool TryRead(out RC05ThirdSquadRecord? record)
        {
            record = null;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRead(key => variables.GetInt(key), out record);
        }

        public static bool TryRead(Func<string, int> getInt, out RC05ThirdSquadRecord? record)
        {
            record = null;
            if (getInt == null)
                return false;
            if (getInt(ConfirmedKey) != 1)
                return true;

            int rawTrial = getInt(TrialDeliveryKey);
            int rawRes = getInt(ResolutionKey);
            int rawSkirmish = getInt(SkirmishKey);
            int rawRealZy = getInt(RealZhengYuanVerifiedKey);
            int rawStone = getInt(OldWomanStoneMentionedKey);

            if (rawTrial != 1 ||
                !Enum.IsDefined(typeof(RC05Resolution), rawRes) ||
                (rawSkirmish != 0 && rawSkirmish != 1) ||
                (rawRealZy != 0 && rawRealZy != 1) ||
                (rawStone != 0 && rawStone != 1))
                return true;

            var saved = new RC05ThirdSquadRecord(
                trialDeliveryVerified: true,
                (RC05Resolution)rawRes,
                rawSkirmish == 1,
                rawRealZy == 1,
                rawStone == 1);
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
            setInt(TrialDeliveryKey, 0);
            setInt(ResolutionKey, 0);
            setInt(SkirmishKey, 0);
            setInt(RealZhengYuanVerifiedKey, 0);
            setInt(OldWomanStoneMentionedKey, 0);
            return true;
        }
    }
}
