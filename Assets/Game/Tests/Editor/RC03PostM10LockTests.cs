using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RC03PostM10LockTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedThroughM09WithM08Notice(Dictionary<string, int> values)
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

            var winch = new M08FirstWinchOrderSession();
            Assert.That(winch.TryAcquirePart(M08WinchPart.ScrapGear), Is.True);
            Assert.That(winch.TryAcquirePart(M08WinchPart.DockCable), Is.True);
            Assert.That(winch.TryAssemble(), Is.True);
            Assert.That(winch.TryTightenCable(), Is.True);
            Assert.That(winch.TryTestLift(), Is.True);
            Assert.That(M08WorkshopDepositFacts.TryConfirm(winch, read, write), Is.True);
            Assert.That(M08FirstWorldNoticeFacts.TryRecord(true, read, write), Is.True);
            Assert.That(M09BerthTableFacts.TryRecord(new M09BerthTableRecord(
                20, 23, M09BerthExchangeFlags.HanQiaoCivilianVoyage,
                M09RuiyaOutcome.Rescued, false, false), read, write), Is.True);
        }

        [Test]
        public void RC03RejectsOpeningOrRecordingAfterM10OrOrphanedAct3FactsExist()
        {
            var values = new Dictionary<string, int>();
            SeedThroughM09WithM08Notice(values);
            Func<string, int> read = Reader(values);
            int rc03Writes = 0;
            Action<string, int> write = (k, v) =>
            {
                if (k.StartsWith("rpg.return.rc03.", StringComparison.Ordinal))
                    rc03Writes++;
                values[k] = v;
            };

            Assert.That(RC03BaijiMineFacts.HasOrphanedDownstreamFacts(null), Is.True);
            Assert.That(RC03BaijiMineFacts.HasOrphanedDownstreamFacts(read), Is.False);

            Assert.That(M10AssemblyFacts.TryRecord(new M10AssemblyRecord(
                worldNoticeThreeObserved: true,
                M10RosterAccuracy.VerifiedFirstPass,
                M10BattleReportSource.CarriedFromM02,
                M10HanLedgerUse.PublishDraft,
                M10SupplyContractStatus.SignedFullCoverage,
                leverageWeakenedByRC02: false,
                coversRC05MountainFamilies: false,
                bellTowerSeizureHeard: true), read, write), Is.True);

            var rc03 = new RC03BaijiMineRecord(
                RC03IslandVisits.All,
                RC03Resolution.SecretlyEvacuateLanRuo,
                pumpDiagramVerified: false,
                successorTrainingSecured: false,
                lanRuoConsentedToStay: false,
                chairmanLuoRegisterNoteRecorded: true);

            Assert.That(RC03BaijiMineFacts.HasOrphanedDownstreamFacts(read), Is.True);
            Assert.That(RC03BaijiMineFacts.CanRecord(rc03, read), Is.False);
            Assert.That(RC03BaijiMineFacts.TryRecord(rc03, read, write), Is.False);
            Assert.That(RC03BaijiMineSession.TryCreateFromSavedFacts(read, out _), Is.False);
            Assert.That(rc03Writes, Is.Zero);

            Assert.That(M10AssemblyFacts.TryResetForNewVoyage(write), Is.True);
            values[M16EpilogueFacts.ConfirmedKey] = 1;
            Assert.That(RC03BaijiMineFacts.TryRecord(rc03, read, write), Is.False);
            Assert.That(RC03BaijiMineSession.TryCreateFromSavedFacts(read, out _), Is.False);
            Assert.That(rc03Writes, Is.Zero);
            Assert.That(M16EpilogueFacts.TryResetForNewVoyage(write), Is.True);

            Assert.That(RC03BaijiMineSession.TryCreateFromSavedFacts(read, out var session), Is.True);
            Assert.That(session, Is.Not.Null);
            Assert.That(RC03BaijiMineFacts.TryRecord(rc03, read, write), Is.True);

            Assert.That(M10AssemblyFacts.TryRecord(new M10AssemblyRecord(
                worldNoticeThreeObserved: true,
                M10RosterAccuracy.VerifiedFirstPass,
                M10BattleReportSource.CarriedFromM02,
                M10HanLedgerUse.PublishDraft,
                M10SupplyContractStatus.SignedFullCoverage,
                leverageWeakenedByRC02: false,
                coversRC05MountainFamilies: false,
                bellTowerSeizureHeard: true), read, write), Is.True);

            Assert.That(RC03BaijiMineFacts.TryRead(read, out RC03BaijiMineRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.LanRuoQueuedForNextBatch, Is.True);
        }
    }
}
