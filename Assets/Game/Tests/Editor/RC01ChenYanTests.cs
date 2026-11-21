using System;
using System.Collections.Generic;
using GamingIsLove.Makinom;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RC01ChenYanTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedM08RosterCasesAvailable(Dictionary<string, int> values)
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
        public void CaseRequiresM08NoticeAndAllThreeFerryCluesBeforeConfrontation()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(RC01ChenYanSession.TryCreateFromSavedFacts(read, out var blocked), Is.False);
            Assert.That(blocked, Is.Null);

            SeedM08RosterCasesAvailable(values);
            Assert.That(RC01ChenYanSession.TryCreateFromSavedFacts(read, out var session), Is.True);
            Assert.That(session.TryConfrontAtFerry(), Is.False);
            Assert.That(session.TryObserveClue(RC01FerryClues.WristScar), Is.True);
            Assert.That(session.TryObserveClue(RC01FerryClues.WristScar), Is.False);
            Assert.That(session.TryObserveClue(RC01FerryClues.DoughHumTune), Is.True);
            Assert.That(session.TryConfrontAtFerry(), Is.False);

            Assert.That(session.TryObserveClue(RC01FerryClues.BlankShrineTablet), Is.True);
            Assert.That(session.Stage, Is.EqualTo(RC01InvestigationStage.IdentityConfrontation));
            Assert.That(session.TryConfrontAtFerry(), Is.True);
            Assert.That(session.IdentityAdmitted, Is.True);
            Assert.That(session.Stage, Is.EqualTo(RC01InvestigationStage.TwoHomesTalk));
            Assert.That(session.TrySettleRosterEntry(RC01ChenYanOutcome.KeepMarkedDead, read, write), Is.False);
        }

        [Test]
        public void TalkOrderDeterminesAtmosphereAndBothHouseholdsMustBeHeard()
        {
            var values1 = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values1);
            Func<string, int> read1 = Reader(values1);
            Action<string, int> write1 = (k, v) => values1[k] = v;

            Assert.That(RC01ChenYanSession.TryCreateFromSavedFacts(read1, out var linFirst), Is.True);
            Assert.That(linFirst.TryObserveClue(RC01FerryClues.WristScar), Is.True);
            Assert.That(linFirst.TryObserveClue(RC01FerryClues.DoughHumTune), Is.True);
            Assert.That(linFirst.TryObserveClue(RC01FerryClues.BlankShrineTablet), Is.True);
            Assert.That(linFirst.TryConfrontAtFerry(), Is.True);

            Assert.That(linFirst.TryTalkToLinSu(), Is.True);
            Assert.That(linFirst.TalkOrder, Is.EqualTo(RC01TalkOrder.LinSuFirst));
            Assert.That(linFirst.Atmosphere, Is.EqualTo(RC01TalkAtmosphere.GuardedWithQiuniang));
            Assert.That(linFirst.TrySettleRosterEntry(RC01ChenYanOutcome.RestoreLivingName, read1, write1), Is.False);

            Assert.That(linFirst.TryTalkToQiuniang(), Is.True);
            Assert.That(linFirst.Stage, Is.EqualTo(RC01InvestigationStage.RosterDecision));
            Assert.That(linFirst.TrySettleRosterEntry(RC01ChenYanOutcome.RestoreLivingName, read1, write1), Is.True);
            Assert.That(RC01ChenYanFacts.TryRead(read1, out RC01ChenYanRecord? saved1), Is.True);
            Assert.That(saved1.Value.Atmosphere, Is.EqualTo(RC01TalkAtmosphere.GuardedWithQiuniang));
            Assert.That(saved1.Value.LinSuRegisterNoteRecorded, Is.True);

            var values2 = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values2);
            Func<string, int> read2 = Reader(values2);
            Action<string, int> write2 = (k, v) => values2[k] = v;

            Assert.That(RC01ChenYanSession.TryCreateFromSavedFacts(read2, out var qiuFirst), Is.True);
            Assert.That(qiuFirst.TryObserveClue(RC01FerryClues.WristScar), Is.True);
            Assert.That(qiuFirst.TryObserveClue(RC01FerryClues.DoughHumTune), Is.True);
            Assert.That(qiuFirst.TryObserveClue(RC01FerryClues.BlankShrineTablet), Is.True);
            Assert.That(qiuFirst.TryConfrontAtFerry(), Is.True);
            Assert.That(qiuFirst.TryTalkToQiuniang(), Is.True);
            Assert.That(qiuFirst.TryTalkToLinSu(), Is.True);
            Assert.That(qiuFirst.TrySettleRosterEntry(RC01ChenYanOutcome.IssueNewReturneeName, read2, write2), Is.True);
            Assert.That(RC01ChenYanFacts.TryRead(read2, out RC01ChenYanRecord? saved2), Is.True);
            Assert.That(saved2.Value.Atmosphere, Is.EqualTo(RC01TalkAtmosphere.CalmWithLinSu));
            Assert.That(saved2.Value.Outcome, Is.EqualTo(RC01ChenYanOutcome.IssueNewReturneeName));
        }

        [Test]
        public void InterruptedWriteAndCommitThenThrowRecoverWithoutDuplicateWrites()
        {
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values);
            Func<string, int> read = Reader(values);
            int preCommitFails = 0;
            int confirmCommits = 0;
            Action<string, int> unstable = (k, v) =>
            {
                if (k == RC01ChenYanFacts.OutcomeKey && preCommitFails++ == 0)
                    throw new InvalidOperationException("Interrupted before confirm");
                values[k] = v;
                if (k == RC01ChenYanFacts.ConfirmedKey && v == 1 && ++confirmCommits == 1)
                    throw new InvalidOperationException("Lost ack after confirm");
            };

            Assert.That(RC01ChenYanSession.TryCreateFromSavedFacts(read, out var session), Is.True);
            Assert.That(session.TryObserveClue(RC01FerryClues.WristScar), Is.True);
            Assert.That(session.TryObserveClue(RC01FerryClues.DoughHumTune), Is.True);
            Assert.That(session.TryObserveClue(RC01FerryClues.BlankShrineTablet), Is.True);
            Assert.That(session.TryConfrontAtFerry(), Is.True);
            Assert.That(session.TryTalkToLinSu(), Is.True);
            Assert.That(session.TryTalkToQiuniang(), Is.True);

            Assert.Throws<InvalidOperationException>(() =>
                session.TrySettleRosterEntry(RC01ChenYanOutcome.KeepMarkedDead, read, unstable));
            Assert.That(session.IsResolved, Is.False);

            Assert.Throws<InvalidOperationException>(() =>
                session.TrySettleRosterEntry(RC01ChenYanOutcome.KeepMarkedDead, read, unstable));
            Assert.That(confirmCommits, Is.EqualTo(1));

            // Contradictory choice after commit must be rejected.
            Assert.That(session.TrySettleRosterEntry(
                RC01ChenYanOutcome.RestoreLivingName, read, unstable), Is.False);

            // Exact matching retry recovers cleanly without rewriting.
            Assert.That(session.TrySettleRosterEntry(
                RC01ChenYanOutcome.KeepMarkedDead, read, unstable), Is.True);
            Assert.That(confirmCommits, Is.EqualTo(1));
            Assert.That(session.IsResolved, Is.True);
        }

        [Test]
        public void InterruptedRosterIntentCannotSwapFamilyChoiceOrTalkOrder()
        {
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            var original = new RC01ChenYanRecord(
                RC01FerryClues.All, RC01TalkOrder.LinSuFirst,
                RC01ChenYanOutcome.RestoreLivingName, true);
            var differentChoice = new RC01ChenYanRecord(
                RC01FerryClues.All, RC01TalkOrder.LinSuFirst,
                RC01ChenYanOutcome.KeepMarkedDead, true);
            var changedOrder = new RC01ChenYanRecord(
                RC01FerryClues.All, RC01TalkOrder.QiuniangFirst,
                RC01ChenYanOutcome.RestoreLivingName, true);

            Assert.Throws<InvalidOperationException>(() =>
                RC01ChenYanFacts.TryRecord(original, read, (key, value) =>
                {
                    write(key, value);
                    if (key == RC01ChenYanFacts.PendingSignatureKey)
                        throw new InvalidOperationException("Lost save after intent");
                }));
            Assert.That(read(RC01ChenYanFacts.PendingSignatureKey), Is.Not.Zero);
            Assert.That(RC01ChenYanFacts.TryRead(read, out _), Is.False);
            Assert.That(RC01ChenYanFacts.TryRecord(differentChoice, read, write), Is.False);
            Assert.That(RC01ChenYanFacts.TryRecord(changedOrder, read, write), Is.False);
            Assert.That(RC01ChenYanFacts.TryRecord(original, read, write), Is.True);

            Assert.That(RC01ChenYanFacts.TryRead(
                read, out RC01ChenYanRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.Outcome, Is.EqualTo(original.Outcome));
            Assert.That(saved.Value.TalkOrder, Is.EqualTo(original.TalkOrder));

            // Fully confirmed legacy registrations have no intent signature.
            values[RC01ChenYanFacts.PendingSignatureKey] = 0;
            Assert.That(RC01ChenYanFacts.TryRead(
                read, out RC01ChenYanRecord? oldSave), Is.True);
            Assert.That(oldSave.HasValue, Is.True);

            Assert.That(RC01ChenYanFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(read(RC01ChenYanFacts.PendingSignatureKey), Is.Zero);
            values[RC01ChenYanFacts.CluesMaskKey] = (int)RC01FerryClues.All;
            Assert.That(RC01ChenYanFacts.TryRead(read, out _), Is.False);
            Assert.That(RC01ChenYanFacts.TryRecord(original, read, write), Is.False);
        }

        [Test]
        public void CorruptOrResetUpstreamFactsInvalidateReadbackAndNullDelegatesFailClosed()
        {
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(RC01ChenYanFacts.TryRecord(
                new RC01ChenYanRecord(
                    RC01FerryClues.All,
                    RC01TalkOrder.QiuniangFirst,
                    RC01ChenYanOutcome.KeepMarkedDead,
                    linSuRegisterNoteRecorded: true),
                read, write), Is.True);

            values[RC01ChenYanFacts.OutcomeKey] = 99;
            Assert.That(RC01ChenYanFacts.TryRead(read, out RC01ChenYanRecord? corrupt), Is.True);
            Assert.That(corrupt, Is.Null);

            values[RC01ChenYanFacts.OutcomeKey] = (int)RC01ChenYanOutcome.KeepMarkedDead;
            Assert.That(M08FirstWorldNoticeFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(RC01ChenYanFacts.TryRead(read, out RC01ChenYanRecord? stale), Is.True);
            Assert.That(stale, Is.Null);

            Assert.That(RC01ChenYanFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(values[RC01ChenYanFacts.ConfirmedKey], Is.Zero);
            Assert.That(RC01ChenYanFacts.TryRead(null, out _), Is.False);
            Assert.That(RC01ChenYanFacts.TryResetForNewVoyage(null), Is.False);
            if (!Maki.Initialized || Maki.Game?.Variables == null)
                Assert.That(RC01ChenYanFacts.TryRead(out _), Is.False);
        }
    }
}
