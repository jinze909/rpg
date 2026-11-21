using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class ST01RegionalStoryFactsTests
    {
        private static Func<string, int> Read(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static Action<string, int> Write(Dictionary<string, int> values)
        {
            return (key, value) => values[key] = value;
        }

        [Test]
        public void WhiteReedDepartureIsSavedOnceAndDoesNotFabricateJingwanOrTicket()
        {
            var values = new Dictionary<string, int>();
            var read = Read(values);
            var write = Write(values);
            Assert.That(ST01RegionalStoryFacts.TryRecordDeparture(
                ST01DepartureChoice.TalkTogether, read, write), Is.True);
            Assert.That(ST01RegionalStoryFacts.TryRecordDeparture(
                ST01DepartureChoice.TalkTogether, read, write), Is.True);
            Assert.That(ST01RegionalStoryFacts.TryRecordDeparture(
                ST01DepartureChoice.StagedAbduction, read, write), Is.False);
            Assert.That(ST01RegionalStoryFacts.TryRead(read, out ST01RegionalOutcome saved), Is.True);
            Assert.That(saved.Departure, Is.EqualTo(ST01DepartureChoice.TalkTogether));
            Assert.That(saved.Theater.HasValue, Is.False);
            Assert.That(saved.ShowM15Ticket, Is.False);
            Assert.That(saved.M15TicketInscription, Is.EqualTo(ST01TicketInscription.None));
            Assert.That(ST01RegionalStoryFacts.TryMarkTicketSent(read, write), Is.False);
        }

        [Test]
        public void JingwanFirstMeetingDoesNotInventEarlierBackstageHistory()
        {
            var values = new Dictionary<string, int>();
            var read = Read(values);
            var write = Write(values);
            Assert.That(ST01RegionalStoryFacts.TryRecordTheater(
                ST01TheaterChoice.KeepContractAndHearResidents, read, write), Is.True);
            Assert.That(ST01RegionalStoryFacts.TryRecordDeparture(
                ST01DepartureChoice.StagedAbduction, read, write), Is.False);
            Assert.That(ST01RegionalStoryFacts.TryMarkTicketSent(read, write), Is.True);
            Assert.That(ST01RegionalStoryFacts.TryRead(read, out ST01RegionalOutcome sent), Is.True);
            Assert.That(sent.ShowM15Ticket, Is.False,
                "Merely putting a ticket in the post must not create an M15 invitation.");
            Assert.That(ST01RegionalStoryFacts.TryMarkTicketDelivered(read, write), Is.True);
            Assert.That(ST01RegionalStoryFacts.TryRead(read, out ST01RegionalOutcome delivered), Is.True);
            Assert.That(delivered.Departure.HasValue, Is.False);
            Assert.That(delivered.ShowM15Ticket, Is.True);
            Assert.That(delivered.M15TicketInscription,
                Is.EqualTo(ST01TicketInscription.GeneralInvitation));
        }

        [Test]
        public void DeliveredTicketUsesOnlyActuallyRecordedFirstMeetingChoice()
        {
            foreach (ST01DepartureChoice choice in new[]
            {
                ST01DepartureChoice.TalkTogether, ST01DepartureChoice.StagedAbduction
            })
            {
                var values = new Dictionary<string, int>();
                var read = Read(values);
                var write = Write(values);
                Assert.That(ST01RegionalStoryFacts.TryRecordDeparture(choice, read, write), Is.True);
                Assert.That(ST01RegionalStoryFacts.TryRecordTheater(
                    ST01TheaterChoice.RewritePlayWithFundingRisk, read, write), Is.True);
                Assert.That(ST01RegionalStoryFacts.TryMarkTicketDelivered(read, write), Is.False);
                Assert.That(ST01RegionalStoryFacts.TryMarkTicketSent(read, write), Is.True);
                Assert.That(ST01RegionalStoryFacts.TryMarkTicketSent(read, write), Is.True);
                Assert.That(ST01RegionalStoryFacts.TryRead(read, out ST01RegionalOutcome inTransit), Is.True);
                Assert.That(inTransit.ShowM15Ticket, Is.False);
                Assert.That(ST01RegionalStoryFacts.TryMarkTicketDelivered(read, write), Is.True);
                Assert.That(ST01RegionalStoryFacts.TryMarkTicketDelivered(read, write), Is.True);
                Assert.That(ST01RegionalStoryFacts.TryRead(read, out ST01RegionalOutcome final), Is.True);
                Assert.That(final.ShowM15Ticket, Is.True);
                Assert.That(final.M15TicketInscription, Is.EqualTo(
                    choice == ST01DepartureChoice.TalkTogether
                        ? ST01TicketInscription.WelcomeToWatch
                        : ST01TicketInscription.ThanksForHelping));
            }
        }

        [Test]
        public void BoycottWithoutReconciliationNeverFabricatesThankYouOrTicket()
        {
            var values = new Dictionary<string, int>();
            var read = Read(values);
            var write = Write(values);
            Assert.That(ST01RegionalStoryFacts.TryRecordTheater(
                ST01TheaterChoice.PublicBoycott, read, write), Is.True);
            Assert.That(ST01RegionalStoryFacts.TryMarkTicketSent(read, write), Is.False);
            Assert.That(ST01RegionalStoryFacts.TryMarkTicketDelivered(read, write), Is.False);
            Assert.That(ST01RegionalStoryFacts.TryRead(read, out ST01RegionalOutcome saved), Is.True);
            Assert.That(saved.ShowM15Ticket, Is.False);
            values[ST01RegionalStoryFacts.TicketStatusKey] = (int)ST01TicketStatus.Delivered;
            Assert.That(ST01RegionalStoryFacts.TryRead(read, out _), Is.False,
                "Forged correspondence cannot create a reconciliation.");
            values[ST01RegionalStoryFacts.TicketStatusKey] = 0;
            values[ST01RegionalStoryFacts.TheaterChoiceKey] = 99;
            Assert.That(ST01RegionalStoryFacts.TryRead(read, out _), Is.False);
        }

        [Test]
        public void InterruptedTheaterCommitOnlyAllowsSameDecisionToFinish()
        {
            var values = new Dictionary<string, int>();
            var read = Read(values);
            var write = Write(values);
            Assert.That(ST01RegionalStoryFacts.TryRecordDeparture(
                ST01DepartureChoice.TalkTogether, read, write), Is.True);

            bool throwBeforeConfirm = true;
            Action<string, int> interrupted = (key, value) =>
            {
                if (key == ST01RegionalStoryFacts.TheaterConfirmedKey &&
                    throwBeforeConfirm)
                {
                    throwBeforeConfirm = false;
                    throw new InvalidOperationException("Theater confirmation interrupted");
                }
                write(key, value);
            };
            Assert.Throws<InvalidOperationException>(() =>
                ST01RegionalStoryFacts.TryRecordTheater(
                    ST01TheaterChoice.RewritePlayWithFundingRisk, read, interrupted));

            Assert.That(values.ContainsKey(ST01RegionalStoryFacts.TheaterConfirmedKey),
                Is.False);
            Assert.That(values[ST01RegionalStoryFacts.TheaterChoiceKey],
                Is.EqualTo((int)ST01TheaterChoice.RewritePlayWithFundingRisk));
            Assert.That(ST01RegionalStoryFacts.TryRead(read, out _), Is.False,
                "Unconfirmed choice must not be visible as an actual theater outcome.");
            Assert.That(ST01RegionalStoryFacts.TryRecordTheater(
                ST01TheaterChoice.KeepContractAndHearResidents, read, write), Is.False,
                "A different later decision cannot replace a partially written choice.");
            Assert.That(ST01RegionalStoryFacts.TryRecordTheater(
                ST01TheaterChoice.RewritePlayWithFundingRisk, read, write), Is.True);
            Assert.That(ST01RegionalStoryFacts.TryRead(read,
                out ST01RegionalOutcome completed), Is.True);
            Assert.That(completed.Theater,
                Is.EqualTo(ST01TheaterChoice.RewritePlayWithFundingRisk));

            // Lost acknowledgement after the marker was committed is also
            // idempotent; the same decision succeeds, competitors cannot.
            Assert.That(ST01RegionalStoryFacts.TryRecordTheater(
                ST01TheaterChoice.RewritePlayWithFundingRisk, read, write), Is.True);
            Assert.That(ST01RegionalStoryFacts.TryRecordTheater(
                ST01TheaterChoice.PublicBoycott, read, write), Is.False);
        }

        [Test]
        public void TornDepartureCannotBeUsedToCommitJingwanTheater()
        {
            var values = new Dictionary<string, int>();
            var read = Read(values);
            var write = Write(values);
            values[ST01RegionalStoryFacts.DepartureChoiceKey] =
                (int)ST01DepartureChoice.StagedAbduction;
            Assert.That(ST01RegionalStoryFacts.TryRecordTheater(
                ST01TheaterChoice.KeepContractAndHearResidents, read, write), Is.False);
            Assert.That(values.ContainsKey(ST01RegionalStoryFacts.TheaterConfirmedKey), Is.False);
            Assert.That(values.ContainsKey(ST01RegionalStoryFacts.TheaterChoiceKey), Is.False);
        }

        [Test]
        public void InterruptedCommitAndAcknowledgedMailRetryDoNotChangeRecordedChoice()
        {
            var values = new Dictionary<string, int>();
            var read = Read(values);
            var write = Write(values);
            bool dropDeparture = true;
            Action<string, int> interrupted = (key, value) =>
            {
                if (dropDeparture && key == ST01RegionalStoryFacts.DepartureConfirmedKey)
                {
                    dropDeparture = false;
                    throw new InvalidOperationException("Interrupted departure commit");
                }
                write(key, value);
            };
            Assert.Throws<InvalidOperationException>(() =>
                ST01RegionalStoryFacts.TryRecordDeparture(
                    ST01DepartureChoice.StagedAbduction, read, interrupted));
            Assert.That(ST01RegionalStoryFacts.TryRead(read, out _), Is.False);
            Assert.That(ST01RegionalStoryFacts.TryRecordDeparture(
                ST01DepartureChoice.TalkTogether, read, write), Is.False);
            Assert.That(ST01RegionalStoryFacts.TryRecordDeparture(
                ST01DepartureChoice.StagedAbduction, read, write), Is.True);

            Assert.That(ST01RegionalStoryFacts.TryRecordTheater(
                ST01TheaterChoice.KeepContractAndHearResidents, read, write), Is.True);
            bool dropAck = true;
            Assert.Throws<InvalidOperationException>(() =>
                ST01RegionalStoryFacts.TryMarkTicketSent(read, (key, value) =>
                {
                    write(key, value);
                    if (dropAck)
                    {
                        dropAck = false;
                        throw new InvalidOperationException("Mail store ack lost");
                    }
                }));
            Assert.That(ST01RegionalStoryFacts.TryMarkTicketSent(read, write), Is.True);
            Assert.That(ST01RegionalStoryFacts.TryMarkTicketDelivered(read, write), Is.True);
            Assert.That(ST01RegionalStoryFacts.TryRead(read, out ST01RegionalOutcome saved), Is.True);
            Assert.That(saved.M15TicketInscription, Is.EqualTo(ST01TicketInscription.ThanksForHelping));
            Assert.That(ST01RegionalStoryFacts.TryResetForNewGame(write), Is.True);
            Assert.That(ST01RegionalStoryFacts.TryRead(read, out ST01RegionalOutcome reset), Is.True);
            Assert.That(reset.Departure.HasValue, Is.False);
            Assert.That(reset.Theater.HasValue, Is.False);
            Assert.That(reset.ShowM15Ticket, Is.False);
        }
    }
}
