using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    public enum M10RosterAccuracy
    {
        DelayedRequeued = 1,
        VerifiedFirstPass = 2
    }

    public enum M10BattleReportSource
    {
        CarriedFromM02 = 1,
        RetrievedFromHanArchive = 2
    }

    public enum M10HanLedgerUse
    {
        PublishDraft = 1,
        TradeForConcessions = 2,
        KeepConfidential = 3
    }

    public enum M10SupplyContractStatus
    {
        SignedFullCoverage = 1,
        PartialGapToM13 = 2
    }

    /// <summary>
    /// Immutable M10 ("靖湾会盟") assembly outcome persisted in Makinom.
    /// Enforces SCENE_SCRIPT_M10.md v1.2:
    /// - Strictly gated on verified M09BerthTableFacts (second-batch arrival).
    /// - Records third world notice ("桥在我在"), three-record spelling
    ///   reconciliation (re-queueable without soft-lock), actual battle-report
    ///   copy retrieval before confronting Han Qiao, supply contract coverage,
    ///   and the Huilan Pass bell-tower seizure report.
    /// - Cross-checks live RC-02 (AllowByException leverage penalty) and RC-05
    ///   (CivilianPardon mountain-family supply coverage) so downstream M12/M13
    ///   never read forged leverage or phantom supply coverage.
    /// </summary>
    public readonly struct M10AssemblyRecord
    {
        public M10AssemblyRecord(
            bool worldNoticeThreeObserved,
            M10RosterAccuracy rosterAccuracy,
            M10BattleReportSource battleReportSource,
            M10HanLedgerUse hanLedgerUse,
            M10SupplyContractStatus supplyContract,
            bool leverageWeakenedByRC02,
            bool coversRC05MountainFamilies,
            bool bellTowerSeizureHeard)
        {
            WorldNoticeThreeObserved = worldNoticeThreeObserved;
            RosterAccuracy = rosterAccuracy;
            BattleReportSource = battleReportSource;
            HanLedgerUse = hanLedgerUse;
            SupplyContract = supplyContract;
            LeverageWeakenedByRC02 = leverageWeakenedByRC02;
            CoversRC05MountainFamilies = coversRC05MountainFamilies;
            BellTowerSeizureHeard = bellTowerSeizureHeard;
        }

        public bool WorldNoticeThreeObserved { get; }
        public M10RosterAccuracy RosterAccuracy { get; }
        public M10BattleReportSource BattleReportSource { get; }
        public M10HanLedgerUse HanLedgerUse { get; }
        public M10SupplyContractStatus SupplyContract { get; }
        public bool LeverageWeakenedByRC02 { get; }
        public bool CoversRC05MountainFamilies { get; }
        public bool BellTowerSeizureHeard { get; }

        public bool CarriesSupplyGapToM13 =>
            SupplyContract == M10SupplyContractStatus.PartialGapToM13;

        public bool ExposedHanQiaoPublicly =>
            HanLedgerUse == M10HanLedgerUse.PublishDraft;

        public bool SecuredHanQiaoEarlyConcessions =>
            HanLedgerUse == M10HanLedgerUse.TradeForConcessions;

        public bool OwesJiangShenExplanation =>
            HanLedgerUse == M10HanLedgerUse.KeepConfidential;
    }

    public static class M10AssemblyFacts
    {
        public const string ConfirmedKey = "rpg.return.m10.assembly-confirmed";
        public const string WorldNoticeThreeKey = "rpg.return.m10.world-notice-three";
        public const string RosterAccuracyKey = "rpg.return.m10.roster-accuracy";
        public const string BattleReportSourceKey = "rpg.return.m10.battle-report-source";
        public const string HanLedgerUseKey = "rpg.return.m10.han-ledger-use";
        public const string SupplyContractKey = "rpg.return.m10.supply-contract";
        public const string LeverageWeakenedRC02Key = "rpg.return.m10.leverage-weakened-rc02";
        public const string CoversRC05FamiliesKey = "rpg.return.m10.covers-rc05-families";
        public const string BellTowerSeizureKey = "rpg.return.m10.bell-tower-seizure-heard";

        public static bool EvaluateRC02LeveragePenalty(Func<string, int> getInt)
        {
            return getInt != null &&
                RC02ZhengYuanFacts.TryRead(getInt, out RC02ZhengYuanRecord? rc02) &&
                rc02.HasValue &&
                rc02.Value.WeakensNegotiationCard;
        }

        public static bool EvaluateRC05SupplyNeed(Func<string, int> getInt)
        {
            return getInt != null &&
                RC05ThirdSquadFacts.TryRead(getInt, out RC05ThirdSquadRecord? rc05) &&
                rc05.HasValue &&
                rc05.Value.NeedsM10SupplyContract;
        }

        public static bool IsValidRecord(M10AssemblyRecord record, Func<string, int> getInt)
        {
            if (getInt == null ||
                !record.WorldNoticeThreeObserved ||
                !record.BellTowerSeizureHeard ||
                !Enum.IsDefined(typeof(M10RosterAccuracy), record.RosterAccuracy) ||
                !Enum.IsDefined(typeof(M10BattleReportSource), record.BattleReportSource) ||
                !Enum.IsDefined(typeof(M10HanLedgerUse), record.HanLedgerUse) ||
                !Enum.IsDefined(typeof(M10SupplyContractStatus), record.SupplyContract))
                return false;

            if (!M09BerthTableFacts.TryRead(getInt, out M09BerthTableRecord? m09) ||
                !m09.HasValue)
                return false;

            bool expectedWeakened = EvaluateRC02LeveragePenalty(getInt);
            if (record.LeverageWeakenedByRC02 != expectedWeakened)
                return false;

            bool expectedCoversRC05 = EvaluateRC05SupplyNeed(getInt) &&
                record.SupplyContract == M10SupplyContractStatus.SignedFullCoverage;
            if (record.CoversRC05MountainFamilies != expectedCoversRC05)
                return false;

            return true;
        }

        public static bool CanRecord(M10AssemblyRecord record, Func<string, int> getInt)
        {
            return getInt != null &&
                getInt(ConfirmedKey) == 0 &&
                IsValidRecord(record, getInt);
        }

        public static bool TryRecord(M10AssemblyRecord record)
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRecord(
                record,
                key => variables.GetInt(key),
                (key, value) => variables.Set(key, value));
        }

        public static bool TryRecord(
            M10AssemblyRecord record,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (getInt == null || setInt == null || !CanRecord(record, getInt))
                return false;

            setInt(ConfirmedKey, 0);
            setInt(WorldNoticeThreeKey, record.WorldNoticeThreeObserved ? 1 : 0);
            setInt(RosterAccuracyKey, (int)record.RosterAccuracy);
            setInt(BattleReportSourceKey, (int)record.BattleReportSource);
            setInt(HanLedgerUseKey, (int)record.HanLedgerUse);
            setInt(SupplyContractKey, (int)record.SupplyContract);
            setInt(LeverageWeakenedRC02Key, record.LeverageWeakenedByRC02 ? 1 : 0);
            setInt(CoversRC05FamiliesKey, record.CoversRC05MountainFamilies ? 1 : 0);
            setInt(BellTowerSeizureKey, record.BellTowerSeizureHeard ? 1 : 0);
            setInt(ConfirmedKey, 1);
            return true;
        }

        public static bool TryRead(out M10AssemblyRecord? record)
        {
            record = null;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRead(key => variables.GetInt(key), out record);
        }

        public static bool TryRead(Func<string, int> getInt, out M10AssemblyRecord? record)
        {
            record = null;
            if (getInt == null)
                return false;
            if (getInt(ConfirmedKey) != 1)
                return true;

            int rawNotice = getInt(WorldNoticeThreeKey);
            int rawRoster = getInt(RosterAccuracyKey);
            int rawReportSource = getInt(BattleReportSourceKey);
            int rawHanUse = getInt(HanLedgerUseKey);
            int rawSupply = getInt(SupplyContractKey);
            int rawWeakened = getInt(LeverageWeakenedRC02Key);
            int rawCoversRC05 = getInt(CoversRC05FamiliesKey);
            int rawBellTower = getInt(BellTowerSeizureKey);

            if ((rawNotice != 0 && rawNotice != 1) ||
                !Enum.IsDefined(typeof(M10RosterAccuracy), rawRoster) ||
                !Enum.IsDefined(typeof(M10BattleReportSource), rawReportSource) ||
                !Enum.IsDefined(typeof(M10HanLedgerUse), rawHanUse) ||
                !Enum.IsDefined(typeof(M10SupplyContractStatus), rawSupply) ||
                (rawWeakened != 0 && rawWeakened != 1) ||
                (rawCoversRC05 != 0 && rawCoversRC05 != 1) ||
                (rawBellTower != 0 && rawBellTower != 1))
                return true;

            var saved = new M10AssemblyRecord(
                rawNotice == 1,
                (M10RosterAccuracy)rawRoster,
                (M10BattleReportSource)rawReportSource,
                (M10HanLedgerUse)rawHanUse,
                (M10SupplyContractStatus)rawSupply,
                rawWeakened == 1,
                rawCoversRC05 == 1,
                rawBellTower == 1);

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
            setInt(WorldNoticeThreeKey, 0);
            setInt(RosterAccuracyKey, 0);
            setInt(BattleReportSourceKey, 0);
            setInt(HanLedgerUseKey, 0);
            setInt(SupplyContractKey, 0);
            setInt(LeverageWeakenedRC02Key, 0);
            setInt(CoversRC05FamiliesKey, 0);
            setInt(BellTowerSeizureKey, 0);
            return true;
        }
    }
}
