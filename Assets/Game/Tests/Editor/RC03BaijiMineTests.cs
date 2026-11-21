using System;
using System.Collections.Generic;
using GamingIsLove.Makinom;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RC03BaijiMineTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedM08RosterCasesAvailable(
            Dictionary<string, int> values,
            M06RescuePlan plan = M06RescuePlan.OfficialHandover)
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
                    plan, true, M06LetterDeliveryOutcome.NotCarried, true), read, write), Is.True);

            bool shutdown = plan == M06RescuePlan.MinerShutdown;
            Assert.That(M07ExtractionFacts.TryRecord(new M07ExtractionRecord(
                shutdown ? M07ExecutedRoute.MinerShutdown : M07ExecutedRoute.OfficialHandover,
                shutdown ? M07PumpShutdownState.SafelyStoppedThreeHours : M07PumpShutdownState.KeptRunning,
                M07ExtractionFacts.RequiredCostForPlan(plan),
                boardedFirstBatchCount: 10,
                lowerLevelEvacuatedCount: shutdown ? M07ExtractionFacts.LowerLevelMinerCount : 0,
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

        private static RC03BaijiMineSession CompleteVisits(Func<string, int> read)
        {
            Assert.That(RC03BaijiMineSession.TryCreateFromSavedFacts(read, out var session), Is.True);
            Assert.That(session.TryCompleteVisit(RC03IslandVisits.CanteenAndNightSchool), Is.True);
            Assert.That(session.TryCompleteVisit(RC03IslandVisits.PumpRoomTwoAndLanRuo), Is.True);
            Assert.That(session.TryCompleteVisit(RC03IslandVisits.MutualAidCharterAndTalks), Is.True);
            Assert.That(session.Stage, Is.EqualTo(RC03Stage.ResolutionDecision));
            return session;
        }

        [Test]
        public void AmendCharterRequiresDiagramAndSuccessorTrainingAndTracksM07DiagramCopy()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(RC03BaijiMineSession.TryCreateFromSavedFacts(read, out _), Is.False);
            SeedM08RosterCasesAvailable(values, M06RescuePlan.MinerShutdown);

            var session = CompleteVisits(read);
            Assert.That(session.CarriedM07PumpDiagramCopy, Is.True);
            Assert.That(session.CanSettle(RC03Resolution.AmendCharterWithSuccessors), Is.False);
            Assert.That(session.TrySecureSuccessorTraining(true), Is.False,
                "Successor training requires verifying the pump diagram first.");

            Assert.That(session.TryVerifyPumpDiagram(false), Is.False);
            Assert.That(session.TryVerifyPumpDiagram(true), Is.True);
            Assert.That(session.CanSettle(RC03Resolution.AmendCharterWithSuccessors), Is.False);

            Assert.That(session.TrySecureSuccessorTraining(false), Is.False);
            Assert.That(session.TrySecureSuccessorTraining(true), Is.True);
            Assert.That(session.TrySettleResolution(
                RC03Resolution.AmendCharterWithSuccessors, read, write), Is.True);

            Assert.That(RC03BaijiMineFacts.TryRead(read, out RC03BaijiMineRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.LanRuoQueuedForNextBatch, Is.True);
            Assert.That(saved.Value.IslandWaterDisrupted, Is.False);
            Assert.That(saved.Value.ChairmanLuoTrustBroken, Is.False);
        }

        [Test]
        public void StayOneYearRequiresExplicitLanRuoConsentAndCannotBeForced()
        {
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values, M06RescuePlan.OfficialHandover);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var session = CompleteVisits(read);
            Assert.That(session.CarriedM07PumpDiagramCopy, Is.False);
            Assert.That(session.CanSettle(RC03Resolution.StayOneYearByConsent), Is.False);
            Assert.That(session.TryRecordLanRuoStayDecision(false), Is.False);
            Assert.That(session.TrySettleResolution(
                RC03Resolution.StayOneYearByConsent, read, write), Is.False);

            Assert.That(session.TryRecordLanRuoStayDecision(true), Is.True);
            Assert.That(session.TrySettleResolution(
                RC03Resolution.StayOneYearByConsent, read, write), Is.True);

            Assert.That(RC03BaijiMineFacts.TryRead(read, out RC03BaijiMineRecord? saved), Is.True);
            Assert.That(saved.Value.Resolution, Is.EqualTo(RC03Resolution.StayOneYearByConsent));
            Assert.That(saved.Value.LanRuoConsentedToStay, Is.True);
            Assert.That(saved.Value.LanRuoQueuedForNextBatch, Is.False);
        }

        [Test]
        public void SecretEvacuationRecordsWaterDisruptionAndRecoversInterruptedWrites()
        {
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values);
            Func<string, int> read = Reader(values);
            int preCommitFails = 0;
            int confirmCommits = 0;
            Action<string, int> unstable = (k, v) =>
            {
                if (k == RC03BaijiMineFacts.ResolutionKey && preCommitFails++ == 0)
                    throw new InvalidOperationException("Interrupted before confirm");
                values[k] = v;
                if (k == RC03BaijiMineFacts.ConfirmedKey && v == 1 && ++confirmCommits == 1)
                    throw new InvalidOperationException("Lost ack after confirm");
            };

            var session = CompleteVisits(read);
            Assert.Throws<InvalidOperationException>(() =>
                session.TrySettleResolution(RC03Resolution.SecretlyEvacuateLanRuo, read, unstable));
            Assert.That(session.IsResolved, Is.False);

            Assert.Throws<InvalidOperationException>(() =>
                session.TrySettleResolution(RC03Resolution.SecretlyEvacuateLanRuo, read, unstable));
            Assert.That(confirmCommits, Is.EqualTo(1));

            Assert.That(session.TrySettleResolution(
                RC03Resolution.SecretlyEvacuateLanRuo, read, unstable), Is.True);
            Assert.That(confirmCommits, Is.EqualTo(1));
            Assert.That(session.CompletedRecord.Value.LanRuoQueuedForNextBatch, Is.True);
            Assert.That(session.CompletedRecord.Value.IslandWaterDisrupted, Is.True);
            Assert.That(session.CompletedRecord.Value.ChairmanLuoTrustBroken, Is.True);
        }

        [Test]
        public void TornBaijiIntentProtectsResolutionConsentAndTrainingAcrossRetries()
        {
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            var original = new RC03BaijiMineRecord(
                RC03IslandVisits.All, RC03Resolution.StayOneYearByConsent,
                false, false, true, true);
            var differentChoice = new RC03BaijiMineRecord(
                RC03IslandVisits.All, RC03Resolution.SecretlyEvacuateLanRuo,
                false, false, false, true);
            var alternateFacts = new RC03BaijiMineRecord(
                RC03IslandVisits.All, RC03Resolution.StayOneYearByConsent,
                true, true, true, true);

            Assert.Throws<InvalidOperationException>(() =>
                RC03BaijiMineFacts.TryRecord(original, read, (key, value) =>
                {
                    write(key, value);
                    if (key == RC03BaijiMineFacts.PendingSignatureKey)
                        throw new InvalidOperationException("Interrupted after island intent");
                }));
            Assert.That(read(RC03BaijiMineFacts.PendingSignatureKey), Is.Not.Zero);
            Assert.That(RC03BaijiMineFacts.TryRead(read, out _), Is.False);
            Assert.That(RC03BaijiMineFacts.TryRecord(
                differentChoice, read, write), Is.False);
            Assert.That(RC03BaijiMineFacts.TryRecord(
                alternateFacts, read, write), Is.False);
            Assert.That(RC03BaijiMineFacts.TryRecord(original, read, write), Is.True);
            Assert.That(RC03BaijiMineFacts.TryRead(
                read, out RC03BaijiMineRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.Resolution, Is.EqualTo(original.Resolution));
            Assert.That(saved.Value.LanRuoConsentedToStay, Is.True);

            values[RC03BaijiMineFacts.PendingSignatureKey] = 0;
            Assert.That(RC03BaijiMineFacts.TryRead(
                read, out RC03BaijiMineRecord? legacy), Is.True);
            Assert.That(legacy.HasValue, Is.True,
                "Old confirmed island stories must stay readable.");

            Assert.That(RC03BaijiMineFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(read(RC03BaijiMineFacts.PendingSignatureKey), Is.Zero);
            values[RC03BaijiMineFacts.VisitsMaskKey] = (int)RC03IslandVisits.All;
            Assert.That(RC03BaijiMineFacts.TryRead(read, out _), Is.False);
            Assert.That(RC03BaijiMineFacts.TryRecord(original, read, write), Is.False);
        }

        [Test]
        public void CorruptOrResetUpstreamFactsInvalidateReadbackAndNullDelegatesFailClosed()
        {
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(RC03BaijiMineFacts.TryRecord(
                new RC03BaijiMineRecord(
                    RC03IslandVisits.All, RC03Resolution.AmendCharterWithSuccessors,
                    pumpDiagramVerified: true, successorTrainingSecured: false,
                    lanRuoConsentedToStay: false, chairmanLuoRegisterNoteRecorded: true),
                read, write), Is.False);

            Assert.That(RC03BaijiMineFacts.TryRecord(
                new RC03BaijiMineRecord(
                    RC03IslandVisits.All, RC03Resolution.SecretlyEvacuateLanRuo,
                    pumpDiagramVerified: false, successorTrainingSecured: false,
                    lanRuoConsentedToStay: false, chairmanLuoRegisterNoteRecorded: true),
                read, write), Is.True);

            values[RC03BaijiMineFacts.ResolutionKey] = 99;
            Assert.That(RC03BaijiMineFacts.TryRead(read, out RC03BaijiMineRecord? corrupt), Is.True);
            Assert.That(corrupt, Is.Null);

            values[RC03BaijiMineFacts.ResolutionKey] = (int)RC03Resolution.SecretlyEvacuateLanRuo;
            Assert.That(M08FirstWorldNoticeFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(RC03BaijiMineFacts.TryRead(read, out RC03BaijiMineRecord? stale), Is.True);
            Assert.That(stale, Is.Null);

            Assert.That(RC03BaijiMineFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(values[RC03BaijiMineFacts.ConfirmedKey], Is.Zero);
            Assert.That(RC03BaijiMineFacts.TryRead(null, out _), Is.False);
            Assert.That(RC03BaijiMineFacts.TryResetForNewVoyage(null), Is.False);
            if (!Maki.Initialized || Maki.Game?.Variables == null)
                Assert.That(RC03BaijiMineFacts.TryRead(out _), Is.False);
        }
    }
}
