using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RC02AndRC05PostM10LockTests
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
        public void RC02AndRC05RejectOpeningOrRecordingAfterM10OrOrphanedAct3FactsExist()
        {
            var values = new Dictionary<string, int>();
            SeedThroughM09WithM08Notice(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(RC02ZhengYuanFacts.HasOrphanedDownstreamFacts(null), Is.True);
            Assert.That(RC05ThirdSquadFacts.HasOrphanedDownstreamFacts(null), Is.True);

            // Settle M10 cleanly without RC-02 or RC-05.
            Assert.That(M10AssemblyFacts.TryRecord(new M10AssemblyRecord(
                worldNoticeThreeObserved: true,
                M10RosterAccuracy.VerifiedFirstPass,
                M10BattleReportSource.CarriedFromM02,
                M10HanLedgerUse.PublishDraft,
                M10SupplyContractStatus.SignedFullCoverage,
                leverageWeakenedByRC02: false,
                coversRC05MountainFamilies: false,
                bellTowerSeizureHeard: true), read, write), Is.True);

            var rc02 = new RC02ZhengYuanRecord(
                RC02IntakeChecks.All, true, RC02Resolution.AllowByException,
                false, false, false);
            var rc05 = new RC05ThirdSquadRecord(
                true, RC05Resolution.CivilianPardon, false, true, false);

            // Settling RC-02 or RC-05 after M10 must be blocked so M10 readback stays valid.
            Assert.That(RC02ZhengYuanFacts.HasOrphanedDownstreamFacts(read), Is.True);
            Assert.That(RC02ZhengYuanFacts.CanRecord(rc02, read), Is.False);
            Assert.That(RC02ZhengYuanFacts.TryRecord(rc02, read, write), Is.False);
            Assert.That(RC02ZhengYuanSession.TryCreateFromSavedFacts(read, false, out _), Is.False);

            Assert.That(RC05ThirdSquadFacts.HasOrphanedDownstreamFacts(read), Is.True);
            Assert.That(RC05ThirdSquadFacts.CanRecord(rc05, read), Is.False);
            Assert.That(RC05ThirdSquadFacts.TryRecord(rc05, read, write), Is.False);
            Assert.That(RC05ThirdSquadSession.TryCreateFromSavedFacts(read, false, out _), Is.False);

            Assert.That(M10AssemblyFacts.TryRead(read, out M10AssemblyRecord? m10StillValid), Is.True);
            Assert.That(m10StillValid.HasValue, Is.True);

            // Resetting M10 while leaving an orphaned M16 key must still block RC-02/RC-05.
            Assert.That(M10AssemblyFacts.TryResetForNewVoyage(write), Is.True);
            values[M16EpilogueFacts.ConfirmedKey] = 1;
            Assert.That(RC02ZhengYuanFacts.TryRecord(rc02, read, write), Is.False);
            Assert.That(RC05ThirdSquadFacts.TryRecord(rc05, read, write), Is.False);
            Assert.That(M16EpilogueFacts.TryResetForNewVoyage(write), Is.True);

            // Once Act-3 is clean, both RC-02 and RC-05 open and persist cleanly, and remain readable after M10.
            Assert.That(RC02ZhengYuanSession.TryCreateFromSavedFacts(read, false, out var s2), Is.True);
            Assert.That(s2, Is.Not.Null);
            Assert.That(RC02ZhengYuanFacts.TryRecord(rc02, read, write), Is.True);

            Assert.That(RC05ThirdSquadSession.TryCreateFromSavedFacts(read, false, out var s5), Is.True);
            Assert.That(s5, Is.Not.Null);
            Assert.That(RC05ThirdSquadFacts.TryRecord(rc05, read, write), Is.True);

            Assert.That(M10AssemblyFacts.TryRecord(new M10AssemblyRecord(
                worldNoticeThreeObserved: true,
                M10RosterAccuracy.VerifiedFirstPass,
                M10BattleReportSource.CarriedFromM02,
                M10HanLedgerUse.PublishDraft,
                M10SupplyContractStatus.SignedFullCoverage,
                leverageWeakenedByRC02: true,
                coversRC05MountainFamilies: true,
                bellTowerSeizureHeard: true), read, write), Is.True);

            Assert.That(RC02ZhengYuanFacts.TryRead(read, out RC02ZhengYuanRecord? saved2), Is.True);
            Assert.That(saved2.HasValue, Is.True);
            Assert.That(RC05ThirdSquadFacts.TryRead(read, out RC05ThirdSquadRecord? saved5), Is.True);
            Assert.That(saved5.HasValue, Is.True);
        }
    }
}
