using System;
using System.Collections.Generic;
using GamingIsLove.Makinom;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M06PumpRoomReunionTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedVerifiedM05Arrival(
            Dictionary<string, int> values,
            M03VoyageChoice route = M03VoyageChoice.QiQueMerchant,
            int assignedCrew = 4,
            int availableBerths = 16,
            int crewArrived = 3,
            int readyBerths = 10,
            bool tenderArrived = true,
            bool receiverMet = true,
            M05LetterDecision? letterDecision = null)
        {
            Action<string, int> write = (key, value) => values[key] = value;
            Func<string, int> read = Reader(values);

            bool officialStamp = route == M03VoyageChoice.HanQiaoOfficial ||
                route == M03VoyageChoice.MixedWithOfficial;
            bool escortFlag = route == M03VoyageChoice.LuJunEscort;

            var manifest = new M04DepartureManifest(
                route,
                "Confirmed hull",
                "A Yuan",
                assignedCrew,
                "Shallow tender",
                true,
                availableBerths,
                Math.Min(8, availableBerths),
                "Named receiver",
                officialStamp,
                escortFlag);
            Assert.That(manifest.ConfirmDeparture(), Is.True);
            Assert.That(M04DepartureFacts.TryRecord(manifest, write), Is.True);
            Assert.That(M04DepartureFacts.TryMarkSailed(read, write), Is.True);

            var fog = new FogListeningSession(
                new[] { new FogListeningCue(FogBearing.Ahead, FogCueKind.ClearWater, 80) },
                maxListenFocus: 1,
                maxHullIntegrity: 2);
            fog.ApplyHelmAction(FogHelmAction.HoldCourse);
            Assert.That(fog.IsComplete && fog.Succeeded, Is.True);
            Assert.That(FogListeningFacts.TryRecord(fog, write), Is.True);

            var landing = new M05LandingReport(
                crewArrived, readyBerths, tenderArrived, receiverMet);
            Assert.That(M05LandingFacts.TryRecord(landing, read, write), Is.True);

            if (letterDecision.HasValue)
                Assert.That(M05LettersFacts.TryRecord(letterDecision.Value, read, write), Is.True);
        }

        [Test]
        public void ReunionCannotStartOrPersistWithoutVerifiedM05Landing()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;

            Assert.That(M06PumpRoomReunionSession.TryCreateFromSavedFacts(read, out var session), Is.False);
            Assert.That(session, Is.Null);

            var record = new M06ReunionRecord(
                M06RescuePlan.OfficialHandover,
                reportedUsableCapacity: true,
                M06LetterDeliveryOutcome.NotCarried,
                retaliationHistoryHeard: true);
            Assert.That(M06RescuePlanFacts.TryRecord(record, read, write), Is.False);
            Assert.That(values, Is.Empty);
        }

        [Test]
        public void ZeroUsableBerthsBlocksTonightDeparturePromiseAndReportsLeadBoatOnly()
        {
            var values = new Dictionary<string, int>();
            SeedVerifiedM05Arrival(
                values,
                route: M03VoyageChoice.QiQueMerchant,
                crewArrived: 2,
                readyBerths: 0,
                tenderArrived: false,
                receiverMet: false);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;

            Assert.That(M06PumpRoomReunionSession.TryCreateFromSavedFacts(read, out var session), Is.True);
            Assert.That(session.CanAskTonightFirstBatch, Is.False,
                "Validation N77: lead boat arrival with 0 ready berths cannot promise tonight's extraction.");
            Assert.That(session.FormatVesselBriefing(zh: true),
                Does.Contain("领航船到了，可用的接应舱位还没落实"));
            Assert.That(session.FormatWithdrawalOrChannelChoice(zh: true),
                Does.Contain("先把能走的水道核一下"));
            Assert.That(session.FormatWithdrawalOrChannelChoice(zh: true),
                Does.Not.Contain("今晚能不能先送一批"));

            // Attempting to record usable capacity as true must be rejected.
            Assert.That(M06RescuePlanFacts.TryRecord(
                new M06ReunionRecord(
                    M06RescuePlan.OfficialHandover,
                    reportedUsableCapacity: true,
                    M06LetterDeliveryOutcome.NotCarried,
                    retaliationHistoryHeard: true),
                read, write), Is.False);

            session.AnswerVesselBriefing(M06ReunionGreeting.DiscussWithdrawalOrChannel);
            session.WitnessPumpRoomAndInspection();
            session.HearOulinRetaliationTruth();
            session.RespondToShen(M06ShenResponse.HelpRepairValve);
            Assert.That(session.TryConfirmRescuePlan(M06RescuePlan.OfficialHandover, read, write), Is.True);
            Assert.That(M06RescuePlanFacts.TryRead(read, out M06ReunionRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.ReportedUsableCapacity, Is.False);
        }

        [Test]
        public void VerifiedUsableCapacityAllowsFirstBatchInquiryAndMatchesDepartureDraft()
        {
            var values = new Dictionary<string, int>();
            SeedVerifiedM05Arrival(
                values,
                route: M03VoyageChoice.LuJunEscort,
                assignedCrew: 4,
                availableBerths: 20,
                crewArrived: 4,
                readyBerths: 12,
                tenderArrived: true,
                receiverMet: true);
            Func<string, int> read = Reader(values);

            Assert.That(M06PumpRoomReunionSession.TryCreateFromSavedFacts(read, out var session), Is.True);
            Assert.That(session.CanAskTonightFirstBatch, Is.True);
            Assert.That(session.FormatVesselBriefing(zh: true), Does.Contain("吃水四尺半"));
            Assert.That(session.FormatVesselBriefing(zh: true), Does.Contain("12"));
            Assert.That(session.FormatWithdrawalOrChannelChoice(zh: true),
                Does.Contain("今晚能不能先送一批"));
        }

        [Test]
        public void RefusedOrUnopenedM05LettersSkipsDeliveryAndRejectsFabricatedDeliverySave()
        {
            foreach (M05LetterDecision? decision in new M05LetterDecision?[]
                { null, M05LetterDecision.LeaveForNextBoat })
            {
                var values = new Dictionary<string, int>();
                SeedVerifiedM05Arrival(values, letterDecision: decision);
                Func<string, int> read = Reader(values);
                Action<string, int> write = (key, value) => values[key] = value;

                Assert.That(M06PumpRoomReunionSession.TryCreateFromSavedFacts(read, out var session), Is.True);
                Assert.That(session.HasCarriedLetters, Is.False);
                Assert.That(session.LettersCarriedCount, Is.Zero);

                session.AnswerVesselBriefing(M06ReunionGreeting.ComeToBringYouBack);
                session.WitnessPumpRoomAndInspection();
                Assert.That(session.Stage, Is.EqualTo(M06ReunionStage.OulinRetaliationTruth),
                    "Validation N76: M06 must skip letter delivery if M05 did not take the packet.");
                Assert.Throws<InvalidOperationException>(() => session.DeliverCarriedLetters());

                Assert.That(M06RescuePlanFacts.TryRecord(
                    new M06ReunionRecord(
                        M06RescuePlan.CargoCover,
                        reportedUsableCapacity: true,
                        M06LetterDeliveryOutcome.DeliveredWithPending,
                        retaliationHistoryHeard: true),
                    read, write), Is.False,
                    "Validation N76: cannot record delivered letters without a carried M05 packet.");

                session.HearOulinRetaliationTruth();

                session.RespondToShen(M06ShenResponse.HelpRepairValve);
                Assert.That(session.TryConfirmRescuePlan(M06RescuePlan.CargoCover, read, write), Is.True);
                Assert.That(M06RescuePlanFacts.TryRead(read, out M06ReunionRecord? saved), Is.True);
                Assert.That(saved.HasValue, Is.True);
                Assert.That(saved.Value.LetterDelivery, Is.EqualTo(M06LetterDeliveryOutcome.NotCarried));
            }
        }

        [Test]
        public void TakenM05LettersPlaysDeliveryWithPendingReturnBeforeOulinTruth()
        {
            var values = new Dictionary<string, int>();
            SeedVerifiedM05Arrival(values, letterDecision: M05LetterDecision.TakePacket);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;

            Assert.That(M06PumpRoomReunionSession.TryCreateFromSavedFacts(read, out var session), Is.True);
            Assert.That(session.HasCarriedLetters, Is.True);
            Assert.That(session.LettersCarriedCount, Is.EqualTo(M05LettersFacts.PacketLetterCount));

            session.AnswerVesselBriefing(M06ReunionGreeting.UglyWrenchNames);
            session.WitnessPumpRoomAndInspection();
            Assert.That(session.Stage, Is.EqualTo(M06ReunionStage.LetterHandoff));
            Assert.Throws<InvalidOperationException>(() => session.HearOulinRetaliationTruth());

            session.DeliverCarriedLetters();
            Assert.That(session.LettersHandled, Is.True);
            Assert.That(session.PendingReturnLetterNoted, Is.True);
            Assert.That(session.Stage, Is.EqualTo(M06ReunionStage.OulinRetaliationTruth));

            session.HearOulinRetaliationTruth();

            session.RespondToShen(M06ShenResponse.HelpRepairValve);
            Assert.That(session.RetaliationHistoryHeard, Is.True);
            Assert.That(session.TryConfirmRescuePlan(M06RescuePlan.MinerShutdown, read, write), Is.True);
            Assert.That(session.Stage, Is.EqualTo(M06ReunionStage.Completed));
            Assert.That(session.ConfirmedPlan, Is.EqualTo(M06RescuePlan.MinerShutdown));

            Assert.That(M06RescuePlanFacts.TryRead(read, out M06ReunionRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.Plan, Is.EqualTo(M06RescuePlan.MinerShutdown));
            Assert.That(saved.Value.ShenResponse, Is.EqualTo(M06ShenResponse.HelpRepairValve));
            Assert.That(saved.Value.LetterDelivery, Is.EqualTo(M06LetterDeliveryOutcome.DeliveredWithPending));
            Assert.That(saved.Value.RetaliationHistoryHeard, Is.True);
        }

        [Test]
        public void AllFourRescuePlansPersistAndReadBackWithoutCorruptingEachOther()
        {
            foreach (M06RescuePlan plan in new[]
            {
                M06RescuePlan.OfficialHandover,
                M06RescuePlan.CargoCover,
                M06RescuePlan.MinerShutdown,
                M06RescuePlan.ForceBreakout
            })
            {
                var values = new Dictionary<string, int>();
                SeedVerifiedM05Arrival(values);
                Func<string, int> read = Reader(values);
                Action<string, int> write = (key, value) => values[key] = value;

                Assert.That(M06PumpRoomReunionSession.TryCreateFromSavedFacts(read, out var session), Is.True);
                session.AnswerVesselBriefing(M06ReunionGreeting.ComeToBringYouBack);
                session.WitnessPumpRoomAndInspection();
                session.HearOulinRetaliationTruth();
                session.RespondToShen(M06ShenResponse.HelpRepairValve);
                Assert.That(session.TryConfirmRescuePlan(plan, read, write), Is.True);

                Assert.That(M06RescuePlanFacts.TryRead(read, out M06ReunionRecord? saved), Is.True);
                Assert.That(saved.HasValue, Is.True);
                Assert.That(saved.Value.Plan, Is.EqualTo(plan));
                Assert.That(M06PumpRoomReunionSession.FormatPlanLabel(plan, zh: true), Is.Not.Empty);
                Assert.That(M06PumpRoomReunionSession.FormatPlanLabel(plan, zh: false), Is.Not.Empty);

                // Duplicate confirmation cannot overwrite the settled rescue plan.
                Assert.That(M06RescuePlanFacts.TryRecord(
                    new M06ReunionRecord(
                        M06RescuePlan.OfficialHandover,
                        reportedUsableCapacity: true,
                        M06LetterDeliveryOutcome.NotCarried,
                        retaliationHistoryHeard: true),
                    read, write), Is.False);
            }
        }

        [Test]
        public void InterruptedSaveWriteKeepsDecisionOpenForRetryAndCorruptOrChangedVoyageInvalidatesRead()
        {
            var values = new Dictionary<string, int>();
            SeedVerifiedM05Arrival(values, route: M03VoyageChoice.QiQueMerchant);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;

            Assert.That(M06PumpRoomReunionSession.TryCreateFromSavedFacts(read, out var session), Is.True);
            session.AnswerVesselBriefing(M06ReunionGreeting.ComeToBringYouBack);
            session.WitnessPumpRoomAndInspection();
            session.HearOulinRetaliationTruth();
            session.RespondToShen(M06ShenResponse.HelpRepairValve);

            int failOnce = 0;
            Assert.Throws<InvalidOperationException>(() =>
                session.TryConfirmRescuePlan(M06RescuePlan.ForceBreakout, read, (key, value) =>
                {
                    if (key == M06RescuePlanFacts.PlanKey && failOnce++ == 0)
                        throw new InvalidOperationException("Interrupted Makinom write");
                    write(key, value);
                }));
            Assert.That(session.Stage, Is.EqualTo(M06ReunionStage.RescuePlanDecision),
                "An interrupted write must keep the decision open for retry.");
            Assert.That(M06RescuePlanFacts.TryRead(read, out M06ReunionRecord? partial), Is.True);
            Assert.That(partial, Is.Null);

            Assert.That(session.TryConfirmRescuePlan(M06RescuePlan.ForceBreakout, read, write), Is.True);
            Assert.That(M06RescuePlanFacts.TryRead(read, out M06ReunionRecord? confirmed), Is.True);
            Assert.That(confirmed.HasValue, Is.True);
            Assert.That(confirmed.Value.Plan, Is.EqualTo(M06RescuePlan.ForceBreakout));

            // Changing M03 route invalidates upstream M05 landing and therefore M06 readback.
            var encounter = new M03LuJunEncounter(M02BridgeEvidence.None, true);
            encounter.Begin();
            encounter.ObserveRollCall();
            encounter.Ask(M03LuJunOpening.NeedAShip);
            encounter.ChooseVoyage(M03VoyageChoice.HanQiaoOfficial);
            Assert.That(M03LuJunFacts.TryRecord(encounter, write), Is.True);
            Assert.That(M06RescuePlanFacts.TryRead(read, out M06ReunionRecord? stale), Is.True);
            Assert.That(stale, Is.Null);

            Assert.That(M06RescuePlanFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(values[M06RescuePlanFacts.ConfirmedKey], Is.Zero);
            Assert.That(values[M06RescuePlanFacts.PlanKey], Is.Zero);
        }

        [Test]
        public void ThreeDistinctShenResponsesAreRequiredAndPersistAcrossSaveRead()
        {
            foreach (M06ShenResponse response in new[]
            {
                M06ShenResponse.AskAboutMiners,
                M06ShenResponse.HelpRepairValve,
                M06ShenResponse.LetHerSpeak
            })
            {
                var values = new Dictionary<string, int>();
                SeedVerifiedM05Arrival(values);
                Func<string, int> read = Reader(values);
                Action<string, int> write = (key, value) => values[key] = value;
                Assert.That(M06PumpRoomReunionSession.TryCreateFromSavedFacts(read, out var session), Is.True);
                session.AnswerVesselBriefing(M06ReunionGreeting.ComeToBringYouBack);
                session.WitnessPumpRoomAndInspection();
                session.HearOulinRetaliationTruth();
                Assert.That(session.Stage, Is.EqualTo(M06ReunionStage.ShenResponse));
                Assert.Throws<InvalidOperationException>(() =>
                    session.TryConfirmRescuePlan(M06RescuePlan.OfficialHandover, read, write));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    session.RespondToShen((M06ShenResponse)99));
                Assert.That(session.Stage, Is.EqualTo(M06ReunionStage.ShenResponse));
                session.RespondToShen(response);
                Assert.That(session.Stage, Is.EqualTo(M06ReunionStage.RescuePlanDecision));
                Assert.That(session.TryConfirmRescuePlan(M06RescuePlan.OfficialHandover, read, write), Is.True);
                Assert.That(M06RescuePlanFacts.TryRead(read, out M06ReunionRecord? saved), Is.True);
                Assert.That(saved.HasValue, Is.True);
                Assert.That(saved.Value.ShenResponse, Is.EqualTo(response));
            }
        }

        [Test]
        public void OldSavedM06RecordWithoutResponseRemainsReadable()
        {
            var values = new Dictionary<string, int>();
            SeedVerifiedM05Arrival(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            Assert.That(M06RescuePlanFacts.TryRecord(
                new M06ReunionRecord(M06RescuePlan.CargoCover, true,
                    M06LetterDeliveryOutcome.NotCarried, true), read, write), Is.True);
            Assert.That(M06RescuePlanFacts.TryRead(read, out M06ReunionRecord? oldRecord), Is.True);
            Assert.That(oldRecord.HasValue, Is.True);
            Assert.That(oldRecord.Value.ShenResponse, Is.Null);
            values[M06RescuePlanFacts.ShenResponseKey] = 99;
            Assert.That(M06RescuePlanFacts.TryRead(read, out M06ReunionRecord? corrupt), Is.True);
            Assert.That(corrupt, Is.Null);
        }

        [Test]
        public void MissingSaveAuthorityOrNullDelegatesNeverFabricateM06Facts()
        {
            var record = new M06ReunionRecord(
                M06RescuePlan.OfficialHandover,
                reportedUsableCapacity: true,
                M06LetterDeliveryOutcome.NotCarried,
                retaliationHistoryHeard: true);

            Assert.That(M06RescuePlanFacts.TryRead(null, out M06ReunionRecord? missing), Is.False);
            Assert.That(missing, Is.Null);
            Assert.That(M06RescuePlanFacts.TryRecord(record, null, (_, __) => { }), Is.False);
            Assert.That(M06RescuePlanFacts.TryRecord(record, _ => 0, null), Is.False);
            Assert.That(M06RescuePlanFacts.TryResetForNewVoyage(null), Is.False);

            if (!Maki.Initialized || Maki.Game?.Variables == null)
            {
                Assert.That(M06RescuePlanFacts.TryRead(out M06ReunionRecord? unbound), Is.False);
                Assert.That(unbound, Is.Null);
                Assert.That(M06RescuePlanFacts.TryRecord(record), Is.False);
                Assert.That(M06RescuePlanFacts.TryResetForNewVoyage(), Is.False);
            }
        }
    }
}
