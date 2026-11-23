using System;
using System.Collections.Generic;
using GamingIsLove.Makinom;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M10AssemblyTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedThroughM09BerthTable(
            Dictionary<string, int> values,
            bool seedM08Notice = false)
        {
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var manifest = new M04DepartureManifest(
                M03VoyageChoice.QiQueMerchant,
                "Confirmed hull", "A Yuan", 4, "Shallow tender",
                true, 16, 8, "Named receiver", false, false);
            Assert.That(manifest.ConfirmDeparture(), Is.True);
            Assert.That(M04DepartureFacts.TryRecord(manifest, write), Is.True);
            Assert.That(M04DepartureFacts.TryMarkSailed(read, write), Is.True);

            var fog = new FogListeningSession(
                new[] { new FogListeningCue(FogBearing.Ahead, FogCueKind.ClearWater, 80) },
                maxListenFocus: 1, maxHullIntegrity: 2);
            fog.ApplyHelmAction(FogHelmAction.HoldCourse);
            Assert.That(FogListeningFacts.TryRecord(fog, write), Is.True);
            Assert.That(M05LandingFacts.TryRecord(
                new M05LandingReport(3, 10, true, true), read, write), Is.True);
            Assert.That(M06RescuePlanFacts.TryRecord(
                new M06ReunionRecord(
                    M06RescuePlan.OfficialHandover, true,
                    M06LetterDeliveryOutcome.NotCarried, true), read, write), Is.True);
            Assert.That(M07ExtractionFacts.TryRecord(new M07ExtractionRecord(
                M07ExecutedRoute.OfficialHandover,
                M07PumpShutdownState.KeptRunning,
                M07RouteCostFlags.HarborSupplyDebt,
                boardedFirstBatchCount: 10,
                lowerLevelEvacuatedCount: 0,
                M07RusFate.Arrested, M07LedgerState.Preserved,
                M07LiangChoice.StayOnIsland,
                M07YidaChoice.HandToInspector,
                taoJiRescued: false), read, write), Is.True);

            if (seedM08Notice)
            {
                var winch = new M08FirstWinchOrderSession();
                Assert.That(winch.TryAcquirePart(M08WinchPart.ScrapGear), Is.True);
                Assert.That(winch.TryAcquirePart(M08WinchPart.DockCable), Is.True);
                Assert.That(winch.TryAssemble(), Is.True);
                Assert.That(winch.TryTightenCable(), Is.True);
                Assert.That(winch.TryTestLift(), Is.True);
                Assert.That(M08WorkshopDepositFacts.TryConfirm(winch, read, write), Is.True);
                Assert.That(M08FirstWorldNoticeFacts.TryRecord(true, read, write), Is.True);
            }

            Assert.That(M09BerthTableFacts.TryRecord(new M09BerthTableRecord(
                totalBerths: 20,
                pendingNames: 23,
                M09BerthExchangeFlags.HanQiaoCivilianVoyage,
                M09RuiyaOutcome.Rescued,
                qiDebtDisclosedHere: false,
                shenReconciled: false), read, write), Is.True);
        }

        [Test]
        public void AssemblyRequiresM09BerthTableRequeuesMismatchWithoutSoftLockAndGatesBattleReport()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(M10AssemblySession.TryCreateFromSavedFacts(read, out var blocked), Is.False);
            Assert.That(blocked, Is.Null);

            SeedThroughM09BerthTable(values);
            Assert.That(M10AssemblySession.TryCreateFromSavedFacts(read, out var session), Is.True);
            Assert.That(session.TryReconcileThreeRecords(true), Is.False);

            Assert.That(session.TryObserveWorldNoticeThree(), Is.True);
            Assert.That(session.Phase, Is.EqualTo(M10AssemblyPhase.ThreeRecordReconciliation));

            // Misidentifying on first pass delays and re-queues without soft-locking.
            Assert.That(session.TryReconcileThreeRecords(false), Is.False);
            Assert.That(session.HadReconciliationDelay, Is.True);
            Assert.That(session.Phase, Is.EqualTo(M10AssemblyPhase.ThreeRecordReconciliation));

            Assert.That(session.TryReconcileThreeRecords(true), Is.True);
            Assert.That(session.RosterAccuracy, Is.EqualTo(M10RosterAccuracy.DelayedRequeued));
            Assert.That(session.Phase, Is.EqualTo(M10AssemblyPhase.HanQiaoConfrontation));

            // Confronting Han Qiao requires an actual battle-report copy first.
            Assert.That(session.TryDecideHanLedgerUse(M10HanLedgerUse.PublishDraft), Is.False);
            Assert.That(session.TrySecureBattleReportCopy(
                M10BattleReportSource.RetrievedFromHanArchive), Is.True);
            Assert.That(session.TryDecideHanLedgerUse(M10HanLedgerUse.PublishDraft), Is.True);

            Assert.That(session.TrySignSupplyContract(M10SupplyContractStatus.PartialGapToM13), Is.True);
            Assert.That(session.TryCommit(read, write), Is.False);
            Assert.That(session.TryReceiveBellTowerSeizureReport(), Is.True);
            Assert.That(session.TryCommit(read, write), Is.True);

            Assert.That(session.IsCompleted, Is.True);
            Assert.That(session.CompletedRecord.Value.ExposedHanQiaoPublicly, Is.True);
            Assert.That(session.CompletedRecord.Value.CarriesSupplyGapToM13, Is.True);
            Assert.That(session.CompletedRecord.Value.LeverageWeakenedByRC02, Is.False);
            Assert.That(session.CompletedRecord.Value.CoversRC05MountainFamilies, Is.False);
        }

        [Test]
        public void LiveRC02PenaltyAndRC05SupplyCoverageAreEnforcedAndForgedFlagsAreRejected()
        {
            var values = new Dictionary<string, int>();
            SeedThroughM09BerthTable(values, seedM08Notice: true);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(RC02ZhengYuanFacts.TryRecord(new RC02ZhengYuanRecord(
                RC02IntakeChecks.All, true, RC02Resolution.AllowByException,
                motherCalledShiLeng: false, shiLengConsented: false,
                realZhengYuanFound: false), read, write), Is.True);
            Assert.That(RC05ThirdSquadFacts.TryRecord(new RC05ThirdSquadRecord(
                trialDeliveryVerified: true, RC05Resolution.CivilianPardon,
                skirmishOccurred: false, realZhengYuanVerified: true,
                oldWomanStoneMentioned: false), read, write), Is.True);

            // Forging LeverageWeakenedByRC02 = false must be rejected.
            Assert.That(M10AssemblyFacts.TryRecord(new M10AssemblyRecord(
                worldNoticeThreeObserved: true,
                M10RosterAccuracy.VerifiedFirstPass,
                M10BattleReportSource.CarriedFromM02,
                M10HanLedgerUse.TradeForConcessions,
                M10SupplyContractStatus.SignedFullCoverage,
                leverageWeakenedByRC02: false,
                coversRC05MountainFamilies: true,
                bellTowerSeizureHeard: true), read, write), Is.False);

            // Forging CoversRC05MountainFamilies = true on a partial gap must be rejected.
            Assert.That(M10AssemblyFacts.TryRecord(new M10AssemblyRecord(
                worldNoticeThreeObserved: true,
                M10RosterAccuracy.VerifiedFirstPass,
                M10BattleReportSource.CarriedFromM02,
                M10HanLedgerUse.TradeForConcessions,
                M10SupplyContractStatus.PartialGapToM13,
                leverageWeakenedByRC02: true,
                coversRC05MountainFamilies: true,
                bellTowerSeizureHeard: true), read, write), Is.False);

            Assert.That(M10AssemblySession.TryCreateFromSavedFacts(read, out var session), Is.True);
            Assert.That(session.TryObserveWorldNoticeThree(), Is.True);
            Assert.That(session.TryReconcileThreeRecords(true), Is.True);
            Assert.That(session.TrySecureBattleReportCopy(M10BattleReportSource.CarriedFromM02), Is.True);
            Assert.That(session.TryDecideHanLedgerUse(M10HanLedgerUse.TradeForConcessions), Is.True);
            Assert.That(session.TrySignSupplyContract(M10SupplyContractStatus.SignedFullCoverage), Is.True);
            Assert.That(session.TryReceiveBellTowerSeizureReport(), Is.True);
            Assert.That(session.TryCommit(read, write), Is.True);

            Assert.That(M10AssemblyFacts.TryRead(read, out M10AssemblyRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.LeverageWeakenedByRC02, Is.True);
            Assert.That(saved.Value.CoversRC05MountainFamilies, Is.True);
            Assert.That(saved.Value.SecuredHanQiaoEarlyConcessions, Is.True);
        }

        [Test]
        public void CommitRecoversFromInterruptedWriteAndLostAcknowledgementAfterConfirm()
        {
            var values = new Dictionary<string, int>();
            SeedThroughM09BerthTable(values);
            Func<string, int> read = Reader(values);
            int preCommitFails = 0;
            int confirmCommits = 0;
            Action<string, int> unstable = (k, v) =>
            {
                if (k == M10AssemblyFacts.SupplyContractKey && preCommitFails++ == 0)
                    throw new InvalidOperationException("Interrupted before confirm");
                values[k] = v;
                if (k == M10AssemblyFacts.ConfirmedKey && v == 1 && ++confirmCommits == 1)
                    throw new InvalidOperationException("Lost ack after confirm");
            };

            Assert.That(M10AssemblySession.TryCreateFromSavedFacts(read, out var session), Is.True);
            Assert.That(session.TryObserveWorldNoticeThree(), Is.True);
            Assert.That(session.TryReconcileThreeRecords(true), Is.True);
            Assert.That(session.TrySecureBattleReportCopy(M10BattleReportSource.CarriedFromM02), Is.True);
            Assert.That(session.TryDecideHanLedgerUse(M10HanLedgerUse.KeepConfidential), Is.True);
            Assert.That(session.TrySignSupplyContract(M10SupplyContractStatus.SignedFullCoverage), Is.True);
            Assert.That(session.TryReceiveBellTowerSeizureReport(), Is.True);

            Assert.Throws<InvalidOperationException>(() => session.TryCommit(read, unstable));
            Assert.That(session.IsCompleted, Is.False);

            Assert.Throws<InvalidOperationException>(() => session.TryCommit(read, unstable));
            Assert.That(confirmCommits, Is.EqualTo(1));

            Assert.That(session.TryCommit(read, unstable), Is.True);
            Assert.That(confirmCommits, Is.EqualTo(1));
            Assert.That(session.IsCompleted, Is.True);
            Assert.That(session.CompletedRecord.Value.OwesJiangShenExplanation, Is.True);
        }

        [Test]
        public void CorruptOrResetUpstreamFactsInvalidateReadbackAndResetClearsAllKeys()
        {
            var values = new Dictionary<string, int>();
            SeedThroughM09BerthTable(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(M10AssemblyFacts.TryRecord(new M10AssemblyRecord(
                worldNoticeThreeObserved: true,
                M10RosterAccuracy.VerifiedFirstPass,
                M10BattleReportSource.CarriedFromM02,
                M10HanLedgerUse.PublishDraft,
                M10SupplyContractStatus.SignedFullCoverage,
                leverageWeakenedByRC02: false,
                coversRC05MountainFamilies: false,
                bellTowerSeizureHeard: true), read, write), Is.True);

            values[M10AssemblyFacts.HanLedgerUseKey] = 99;
            Assert.That(M10AssemblyFacts.TryRead(read, out M10AssemblyRecord? corrupt), Is.True);
            Assert.That(corrupt, Is.Null);

            values[M10AssemblyFacts.HanLedgerUseKey] = (int)M10HanLedgerUse.PublishDraft;
            Assert.That(M09BerthTableFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(M10AssemblyFacts.TryRead(read, out M10AssemblyRecord? stale), Is.True);
            Assert.That(stale, Is.Null);

            Assert.That(M10AssemblyFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(values[M10AssemblyFacts.ConfirmedKey], Is.Zero);
            Assert.That(M10AssemblyFacts.TryRead(null, out _), Is.False);
            Assert.That(M10AssemblyFacts.TryResetForNewVoyage(null), Is.False);
            if (!Maki.Initialized || Maki.Game?.Variables == null)
                Assert.That(M10AssemblyFacts.TryRead(out _), Is.False);
        }

        [Test]
        public void M10RefusesCorruptOrInterruptedRC02AndRC05InsteadOfErasingConsequences()
        {
            var values = new Dictionary<string, int>();
            SeedThroughM09BerthTable(values);
            Func<string, int> read = Reader(values);
            int committed = 0;
            Action<string, int> write = (key, value) =>
            {
                if (key == M10AssemblyFacts.ConfirmedKey && value == 1)
                    committed++;
                values[key] = value;
            };

            var noCases = new M10AssemblyRecord(
                worldNoticeThreeObserved: true,
                M10RosterAccuracy.VerifiedFirstPass,
                M10BattleReportSource.CarriedFromM02,
                M10HanLedgerUse.KeepConfidential,
                M10SupplyContractStatus.SignedFullCoverage,
                leverageWeakenedByRC02: false,
                coversRC05MountainFamilies: false,
                bellTowerSeizureHeard: true);

            // A clean never-played case is not an error.
            Assert.That(M10AssemblyFacts.IsValidRecord(noCases, read), Is.True);

            // RC-02 is marked confirmed, but the saved decision is corrupt.
            values[RC02ZhengYuanFacts.ConfirmedKey] = 1;
            values[RC02ZhengYuanFacts.ResolutionKey] = 99;
            Assert.That(RC02ZhengYuanFacts.TryRead(read, out _), Is.True);
            Assert.That(M10AssemblyFacts.IsValidRecord(noCases, read), Is.False);
            Assert.That(M10AssemblyFacts.TryRecord(noCases, read, write), Is.False);

            // An interrupted RC-02 intent is likewise not an unplayed case.
            values[RC02ZhengYuanFacts.ConfirmedKey] = 0;
            values[RC02ZhengYuanFacts.ResolutionKey] = 0;
            values[RC02ZhengYuanFacts.PendingSignatureKey] = 1200;
            Assert.That(RC02ZhengYuanFacts.TryRead(read, out _), Is.False);
            Assert.That(M10AssemblyFacts.TryRecord(noCases, read, write), Is.False);
            values[RC02ZhengYuanFacts.PendingSignatureKey] = 0;

            // RC-05 corruption could otherwise silently drop a mountain-family
            // supply contract obligation from the M10 assembly record.
            values[RC05ThirdSquadFacts.ConfirmedKey] = 1;
            values[RC05ThirdSquadFacts.ResolutionKey] = 99;
            Assert.That(RC05ThirdSquadFacts.TryRead(read, out _), Is.True);
            Assert.That(M10AssemblyFacts.TryRecord(noCases, read, write), Is.False);
            values[RC05ThirdSquadFacts.ConfirmedKey] = 0;
            values[RC05ThirdSquadFacts.ResolutionKey] = 0;
            values[RC05ThirdSquadFacts.PendingSignatureKey] = 1200;
            Assert.That(RC05ThirdSquadFacts.TryRead(read, out _), Is.False);
            Assert.That(M10AssemblyFacts.TryRecord(noCases, read, write), Is.False);
            Assert.That(committed, Is.Zero);

            // Restoring the clean upstream snapshot allows the real choice.
            values[RC05ThirdSquadFacts.PendingSignatureKey] = 0;
            Assert.That(M10AssemblyFacts.IsValidRecord(noCases, read), Is.True);
            Assert.That(M10AssemblyFacts.TryRecord(noCases, read, write), Is.True);
            Assert.That(committed, Is.EqualTo(1));
        }
    }
}
