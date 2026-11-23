using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M07ExtractionNightSessionTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedVerifiedM06(
            Dictionary<string, int> values,
            M06RescuePlan plan,
            M05LetterDecision? letterDecision = M05LetterDecision.TakePacket)
        {
            Action<string, int> write = (key, value) => values[key] = value;
            Func<string, int> read = Reader(values);

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

            var letterOutcome = M06LetterDeliveryOutcome.NotCarried;
            if (letterDecision.HasValue)
            {
                Assert.That(M05LettersFacts.TryRecord(letterDecision.Value, read, write), Is.True);
                if (letterDecision.Value == M05LetterDecision.TakePacket)
                {
                    letterOutcome = M06LetterDeliveryOutcome.DeliveredWithPending;
            foreach (M06FamilyLetterRecipient recipient in new[]
            {
                M06FamilyLetterRecipient.TaoJi,
                M06FamilyLetterRecipient.LedgerMiner,
                M06FamilyLetterRecipient.PendingMiner
            })
                Assert.That(M06FamilyLetterDeliveryFacts.TryRecord(
                    recipient, read, write), Is.True);
                }
            }

            Assert.That(M06RescuePlanFacts.TryRecord(
                new M06ReunionRecord(plan, true, letterOutcome, true), read, write), Is.True);
        }

        [Test]
        public void OldDockRollCallCannotSettleIdenticalNewVoyage()
        {
            var values = new Dictionary<string, int>();
            SeedVerifiedM06(values, M06RescuePlan.CargoCover,
                M05LetterDecision.LeaveForNextBoat);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(M07ExtractionNightSession.TryCreateFromSavedFacts(
                read, out var oldSession), Is.True);
            oldSession.ExecuteRoute(false, 30);
            oldSession.AdvanceTunnelOpening();
            oldSession.ResolveRusConfrontation(
                M07RusFate.Arrested, M07LedgerState.Preserved);
            oldSession.AdvanceReturnLettersCheck();
            int originalSequence = read(M05LandingFacts.ArrivalSequenceKey);

            Assert.That(ReturnOpeningContinuation.TryResetPreviousOutboundFacts(write),
                Is.True);
            Assert.That(oldSession.TryCompleteDockRollCall(
                M07LiangChoice.StayOnIsland, M07YidaChoice.HandToInspector,
                false, read, write), Is.False);
            SeedVerifiedM06(values, M06RescuePlan.CargoCover,
                M05LetterDecision.LeaveForNextBoat);
            Assert.That(read(M05LandingFacts.ArrivalSequenceKey),
                Is.GreaterThan(originalSequence));

            int staleWrites = 0;
            Assert.That(oldSession.TryCompleteDockRollCall(
                M07LiangChoice.StayOnIsland, M07YidaChoice.HandToInspector,
                false, read, (k, v) => { staleWrites++; values[k] = v; }), Is.False);
            Assert.That(staleWrites, Is.Zero);
            Assert.That(read(M07ExtractionFacts.ConfirmedKey), Is.Zero);
            Assert.That(oldSession.Stage, Is.EqualTo(M07ExtractionStage.DockRollCall));

            Assert.That(M07ExtractionNightSession.TryCreateFromSavedFacts(
                read, out var current), Is.True);
            current.ExecuteRoute(false, 30);
            current.AdvanceTunnelOpening();
            current.ResolveRusConfrontation(
                M07RusFate.Arrested, M07LedgerState.Preserved);
            current.AdvanceReturnLettersCheck();
            Assert.That(current.TryCompleteDockRollCall(
                M07LiangChoice.StayOnIsland, M07YidaChoice.HandToInspector,
                false, read, write), Is.True);
            Assert.That(M07ExtractionFacts.TryRead(
                read, out M07ExtractionRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.BoardedFirstBatchCount, Is.EqualTo(30));
            Assert.That(oldSession.TryCompleteDockRollCall(
                M07LiangChoice.StayOnIsland, M07YidaChoice.HandToInspector,
                false, read, write), Is.False);
        }

        [Test]
        public void MinerShutdownPlaysOldZhangFarewellSeparatesFiveHundredFromBoardedAndPersists()
        {
            var values = new Dictionary<string, int>();
            SeedVerifiedM06(values, M06RescuePlan.MinerShutdown, M05LetterDecision.TakePacket);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(M07ExtractionNightSession.TryCreateFromSavedFacts(read, out var session), Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() => session.ExecuteRoute(false, 500));

            session.ExecuteRoute(blockedInField: false, boardedFirstBatchCount: 300);
            Assert.That(session.PumpState, Is.EqualTo(M07PumpShutdownState.SafelyStoppedThreeHours));
            Assert.That(session.LowerLevelEvacuatedCount, Is.EqualTo(500));
            Assert.That(session.OldZhangFarewellSpoken, Is.True);
            Assert.That(session.RouteCosts, Is.EqualTo(M07RouteCostFlags.PumpOverhaulShiftCost));

            session.AdvanceTunnelOpening();
            Assert.That(session.TunnelGateOpened, Is.True);

            session.ResolveRusConfrontation(M07RusFate.Arrested, M07LedgerState.PartiallyBurned);
            Assert.That(session.AccountabilityPreserved, Is.True);
            Assert.That(session.FormatRollCallRosterSource(zh: true), Does.Contain("伊蕾监察副本"));

            session.AdvanceReturnLettersCheck();
            Assert.That(session.ReturnLettersCollected, Is.True);

            Assert.That(session.TryCompleteDockRollCall(
                M07LiangChoice.StayOnIsland,
                M07YidaChoice.HandToInspector,
                taoJiRescued: true,
                read, write), Is.True);

            Assert.That(session.Stage, Is.EqualTo(M07ExtractionStage.Completed));
            Assert.That(session.XiaoqiAnsweredByNumberSeventyTwo, Is.True);
            Assert.That(M07ExtractionFacts.TryRead(read, out M07ExtractionRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.ExecutedRoute, Is.EqualTo(M07ExecutedRoute.MinerShutdown));
            Assert.That(saved.Value.BoardedFirstBatchCount, Is.EqualTo(300));
            Assert.That(saved.Value.LowerLevelEvacuatedCount, Is.EqualTo(500));
        }

        [Test]
        public void NonShutdownRouteNeverTriggersPumpStopAndUnseenLettersProduceNoReplies()
        {
            var values = new Dictionary<string, int>();
            SeedVerifiedM06(values, M06RescuePlan.CargoCover, letterDecision: M05LetterDecision.LeaveForNextBoat);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(M07ExtractionNightSession.TryCreateFromSavedFacts(read, out var session), Is.True);
            Assert.That(session.HasReturnLetters, Is.False);

            session.ExecuteRoute(blockedInField: false, boardedFirstBatchCount: 220);
            Assert.That(session.PumpState, Is.EqualTo(M07PumpShutdownState.KeptRunning));
            Assert.That(session.LowerLevelEvacuatedCount, Is.Zero);
            Assert.That(session.OldZhangFarewellSpoken, Is.False);

            session.AdvanceTunnelOpening();
            session.ResolveRusConfrontation(M07RusFate.Fled, M07LedgerState.Preserved);
            Assert.That(session.FormatRollCallRosterSource(zh: true), Does.Contain("转运名册原件"));

            session.AdvanceReturnLettersCheck();
            Assert.That(session.ReturnLettersCollected, Is.False);

            Assert.That(session.TryCompleteDockRollCall(
                M07LiangChoice.ForcedToBoard,
                M07YidaChoice.TakeAlong,
                taoJiRescued: true,
                read, write), Is.True);
            Assert.That(session.CompletedRecord.Value.StrainsShenRelation, Is.True);
        }

        [Test]
        public void BlockedPlanPivotsToFallbackWithStackedCostsAndRetryableSave()
        {
            var values = new Dictionary<string, int>();
            SeedVerifiedM06(values, M06RescuePlan.ForceBreakout, letterDecision: null);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(M07ExtractionNightSession.TryCreateFromSavedFacts(read, out var session), Is.True);
            session.ExecuteRoute(blockedInField: true, boardedFirstBatchCount: 160);
            Assert.That(session.ExecutedRoute, Is.EqualTo(M07ExecutedRoute.FallbackOfficialHandover));
            Assert.That(session.RouteCosts.HasFlag(M07RouteCostFlags.PierRepairAndInjuryCost), Is.True);
            Assert.That(session.RouteCosts.HasFlag(M07RouteCostFlags.FallbackTransferCost), Is.True);
            Assert.That(session.OldZhangFarewellSpoken, Is.False);

            session.AdvanceTunnelOpening();
            session.ResolveRusConfrontation(M07RusFate.LostContract, M07LedgerState.PartiallyBurned);
            session.AdvanceReturnLettersCheck();

            int failOnce = 0;
            Assert.Throws<InvalidOperationException>(() =>
                session.TryCompleteDockRollCall(
                    M07LiangChoice.PersuadedToLeave,
                    M07YidaChoice.LeaveBehind,
                    taoJiRescued: true,
                    read, (k, v) =>
                    {
                        if (k == M07ExtractionFacts.LiangChoiceKey && failOnce++ == 0)
                            throw new InvalidOperationException("Interrupted M07 save");
                        write(k, v);
                    }));
            Assert.That(session.Stage, Is.EqualTo(M07ExtractionStage.DockRollCall));

            Assert.That(session.TryCompleteDockRollCall(
                M07LiangChoice.PersuadedToLeave,
                M07YidaChoice.LeaveBehind,
                taoJiRescued: true,
                read, write), Is.True);
            Assert.That(session.Stage, Is.EqualTo(M07ExtractionStage.Completed));
        }
    }
}
