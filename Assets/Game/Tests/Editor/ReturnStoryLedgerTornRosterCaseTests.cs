using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class ReturnStoryLedgerTornRosterCaseTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedThroughM08Notice(Dictionary<string, int> values)
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
        }

        [Test]
        public void LedgerReportsTornOrCorruptRC01ThroughRC05AsLockedWhilePreservingSettledCases()
        {
            var values = new Dictionary<string, int>();
            SeedThroughM08Notice(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            // Torn RC-01..RC-05 pending keys must report Locked instead of Available.
            values[RC01ChenYanFacts.PendingSignatureKey] = 1;
            values[RC02ZhengYuanFacts.PendingSignatureKey] = 1;
            values[RC03BaijiMineFacts.ResolutionKey] =
                (int)RC03Resolution.AmendCharterWithSuccessors;
            values[RC04DingYueReefFacts.PendingSignatureKey] = 1;
            values[RC05ThirdSquadFacts.PendingSignatureKey] = 1;

            Assert.That(ReturnStoryLedger.TryBuildSnapshot(read, out var tornSnap), Is.True);
            Assert.That(tornSnap.RC01State, Is.EqualTo(ReturnRosterCaseState.Locked));
            Assert.That(tornSnap.RC02State, Is.EqualTo(ReturnRosterCaseState.Locked));
            Assert.That(tornSnap.RC03State, Is.EqualTo(ReturnRosterCaseState.Locked));
            Assert.That(tornSnap.RC04State, Is.EqualTo(ReturnRosterCaseState.Locked));
            Assert.That(tornSnap.RC05State, Is.EqualTo(ReturnRosterCaseState.Locked));
            Assert.That(tornSnap.SettledRosterCaseCount, Is.Zero);

            // Clearing torn keys restores Available, and settling RC-01 still reports Settled.
            Assert.That(RC01ChenYanFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(RC02ZhengYuanFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(RC03BaijiMineFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(RC04DingYueReefFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(RC05ThirdSquadFacts.TryResetForNewVoyage(write), Is.True);

            Assert.That(ReturnStoryLedger.TryBuildSnapshot(read, out var cleanSnap), Is.True);
            Assert.That(cleanSnap.RC01State, Is.EqualTo(ReturnRosterCaseState.Available));
            Assert.That(cleanSnap.RC03State, Is.EqualTo(ReturnRosterCaseState.Available));

            Assert.That(RC01ChenYanFacts.TryRecord(new RC01ChenYanRecord(
                RC01FerryClues.All, RC01TalkOrder.LinSuFirst,
                RC01ChenYanOutcome.KeepMarkedDead, true), read, write), Is.True);

            Assert.That(ReturnStoryLedger.TryBuildSnapshot(read, out var settledSnap), Is.True);
            Assert.That(settledSnap.RC01State, Is.EqualTo(ReturnRosterCaseState.Settled));
            Assert.That(settledSnap.SettledRosterCaseCount, Is.EqualTo(1));
        }
    }
}
