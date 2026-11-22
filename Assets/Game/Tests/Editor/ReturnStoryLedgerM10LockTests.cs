using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class ReturnStoryLedgerM10LockTests
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
        public void LedgerReportsUnsettledRC02AndRC05AsLockedAfterM10WhilePreservingSettledState()
        {
            var values = new Dictionary<string, int>();
            SeedThroughM09WithM08Notice(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            // Before M10: RC-02 and RC-05 are Available.
            Assert.That(ReturnStoryLedger.TryBuildSnapshot(read, out var beforeM10), Is.True);
            Assert.That(beforeM10.RC02State, Is.EqualTo(ReturnRosterCaseState.Available));
            Assert.That(beforeM10.RC05State, Is.EqualTo(ReturnRosterCaseState.Available));

            // Settle RC-02 before M10, leave RC-05 unsettled, then settle M10.
            Assert.That(RC02ZhengYuanFacts.TryRecord(new RC02ZhengYuanRecord(
                RC02IntakeChecks.All, true, RC02Resolution.ExposeImpostor,
                false, false, false), read, write), Is.True);
            Assert.That(M10AssemblyFacts.TryRecord(new M10AssemblyRecord(
                true, M10RosterAccuracy.VerifiedFirstPass,
                M10BattleReportSource.CarriedFromM02,
                M10HanLedgerUse.PublishDraft,
                M10SupplyContractStatus.SignedFullCoverage,
                false, false, true), read, write), Is.True);

            Assert.That(ReturnStoryLedger.TryBuildSnapshot(read, out var afterM10), Is.True);
            Assert.That(afterM10.RC02State, Is.EqualTo(ReturnRosterCaseState.Settled));
            Assert.That(afterM10.RC05State, Is.EqualTo(ReturnRosterCaseState.Locked));
            Assert.That(afterM10.RC01State, Is.EqualTo(ReturnRosterCaseState.Available));

            // Orphaned M16 key after clearing M10 must also keep unsettled RC-05 Locked.
            Assert.That(M10AssemblyFacts.TryResetForNewVoyage(write), Is.True);
            values[M16EpilogueFacts.ConfirmedKey] = 1;
            Assert.That(ReturnStoryLedger.TryBuildSnapshot(read, out var orphanedM16), Is.True);
            Assert.That(orphanedM16.RC05State, Is.EqualTo(ReturnRosterCaseState.Locked));

            Assert.That(M16EpilogueFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(ReturnStoryLedger.TryBuildSnapshot(read, out var recovered), Is.True);
            Assert.That(recovered.RC05State, Is.EqualTo(ReturnRosterCaseState.Available));
        }
    }
}
