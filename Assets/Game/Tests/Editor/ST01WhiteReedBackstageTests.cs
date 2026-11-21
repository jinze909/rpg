using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class ST01WhiteReedBackstageTests
    {
        private static Func<string, int> Read(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static Action<string, int> Write(Dictionary<string, int> values)
        {
            return (key, value) => values[key] = value;
        }

        private static ST01WhiteReedBackstageSession ReachDeparture(
            Dictionary<string, int> values, ST01DepartureChoice choice)
        {
            Assert.That(ST01WhiteReedBackstageSession.TryCreate(Read(values),
                out ST01WhiteReedBackstageSession session), Is.True);
            Assert.That(session.TryInspectBackstage(true, true, true, true), Is.True);
            Assert.That(session.TryHearBothVoices(true, true, true), Is.True);
            Assert.That(session.TryChooseDeparture(choice), Is.True);
            return session;
        }

        [Test]
        public void BackstageInvestigationHearingAndOnstageRescueCannotBeSkipped()
        {
            var values = new Dictionary<string, int>();
            Assert.That(ST01WhiteReedBackstageSession.TryCreate(Read(values),
                out var session), Is.True);
            Assert.That(session.TryChooseDeparture(ST01DepartureChoice.TalkTogether),
                Is.False);
            Assert.That(session.TryInspectBackstage(true, true, false, true), Is.False,
                "The advance already accepted by Liu Man cannot be erased.");
            Assert.That(session.TryInspectBackstage(true, true, true, true), Is.True);
            Assert.That(session.TryHearBothVoices(true, false, true), Is.False);
            Assert.That(session.TryHearBothVoices(true, true, true), Is.True);
            Assert.That(session.TryChooseDeparture(ST01DepartureChoice.TalkTogether), Is.True);
            Assert.That(session.TryCommit(Read(values), Write(values)), Is.False);
            Assert.That(session.TryHelpAShaAtCurtain(
                true, true, true, true, ST01PerformanceRescue.VoiceBehindCurtain), Is.False);
            Assert.That(session.TryHelpAShaAtCurtain(
                true, true, true, false, ST01PerformanceRescue.UnnamedMaidOnStage), Is.False,
                "Liu Ling herself, not the player, must choose to save A Sha.");
            Assert.That(session.TryHelpAShaAtCurtain(
                true, true, true, true, ST01PerformanceRescue.UnnamedMaidOnStage), Is.True);
            Assert.That(session.TryCommit(Read(values), Write(values)), Is.True);
            Assert.That(session.Phase, Is.EqualTo(ST01WhiteReedPhase.Completed));
            Assert.That(ST01RegionalStoryFacts.TryRead(Read(values),
                out ST01RegionalOutcome saved), Is.True);
            Assert.That(saved.Departure, Is.EqualTo(ST01DepartureChoice.TalkTogether));
            Assert.That(saved.Theater.HasValue, Is.False);
            Assert.That(saved.ShowM15Ticket, Is.False);
        }

        [Test]
        public void StagedAbductionCarriesRealCostsAndIsNotMisreportedAsOnstageHelp()
        {
            var values = new Dictionary<string, int>();
            var session = ReachDeparture(values, ST01DepartureChoice.StagedAbduction);
            Assert.That(session.DebtAndUnderstudyConsequencesRemain, Is.True);
            Assert.That(session.TryHelpAShaAtCurtain(
                true, true, true, true, ST01PerformanceRescue.UnnamedMaidOnStage),
                Is.False);
            Assert.That(session.TryHelpAShaAtCurtain(
                true, true, true, true, ST01PerformanceRescue.VoiceBehindCurtain),
                Is.True);
            Assert.That(session.TryCommit(Read(values), Write(values)), Is.True);
            Assert.That(ST01RegionalStoryFacts.TryRead(Read(values),
                out ST01RegionalOutcome saved), Is.True);
            Assert.That(saved.Departure,
                Is.EqualTo(ST01DepartureChoice.StagedAbduction));
            Assert.That(saved.Theater.HasValue, Is.False);
            Assert.That(ST01RegionalStoryFacts.TryMarkTicketSent(
                Read(values), Write(values)), Is.False);
        }

        [Test]
        public void HostChecksActualCastAndLocationAgainAtCommitTime()
        {
            var values = new Dictionary<string, int>();
            bool backstage = false;
            bool liuLingHere = true;
            var host = new ST01WhiteReedBackstageHost(
                () => backstage, () => liuLingHere, () => true, () => true,
                Read(values), Write(values));
            Assert.That(host.TryOpen(), Is.False);
            backstage = true;
            Assert.That(host.TryOpen(), Is.True);
            Assert.That(host.TryOpen(), Is.False);
            Assert.That(host.Session.TryInspectBackstage(true, true, true, true), Is.True);
            Assert.That(host.Session.TryHearBothVoices(true, true, true), Is.True);
            Assert.That(host.Session.TryChooseDeparture(
                ST01DepartureChoice.TalkTogether), Is.True);
            Assert.That(host.Session.TryHelpAShaAtCurtain(
                true, true, true, true, ST01PerformanceRescue.UnnamedMaidOnStage), Is.True);
            liuLingHere = false;
            Assert.That(host.TrySaveAfterPerformance(), Is.False);
            Assert.That(host.HasRecordedDeparture, Is.False);
            liuLingHere = true;
            Assert.That(host.TrySaveAfterPerformance(), Is.True);
            Assert.That(host.HasRecordedDeparture, Is.True);
        }

        [Test]
        public void InterruptedDepartureOnlyRecoversSameRescueAndSameChoice()
        {
            var values = new Dictionary<string, int>();
            var session = ReachDeparture(values, ST01DepartureChoice.StagedAbduction);
            Assert.That(session.TryHelpAShaAtCurtain(
                true, true, true, true, ST01PerformanceRescue.VoiceBehindCurtain), Is.True);
            bool interrupt = true;
            Assert.Throws<InvalidOperationException>(() => session.TryCommit(Read(values),
                (key, value) =>
                {
                    if (key == ST01RegionalStoryFacts.DepartureConfirmedKey && interrupt)
                    {
                        interrupt = false;
                        throw new InvalidOperationException("lost marker before commit");
                    }
                    values[key] = value;
                }));
            Assert.That(ST01RegionalStoryFacts.TryRead(Read(values), out _), Is.False);
            Assert.That(ST01WhiteReedBackstageSession.TryCreate(Read(values),
                out var recovered), Is.True);
            Assert.That(recovered.TryInspectBackstage(true, true, true, true), Is.True);
            Assert.That(recovered.TryHearBothVoices(true, true, true), Is.True);
            Assert.That(recovered.TryChooseDeparture(ST01DepartureChoice.TalkTogether),
                Is.False);
            Assert.That(recovered.TryChooseDeparture(ST01DepartureChoice.StagedAbduction),
                Is.True);
            Assert.That(recovered.TryHelpAShaAtCurtain(
                true, true, true, true, ST01PerformanceRescue.VoiceBehindCurtain), Is.True);
            Assert.That(recovered.TryCommit(Read(values), Write(values)), Is.True);
            Assert.That(ST01WhiteReedBackstageSession.TryCreate(Read(values),
                out _), Is.False);
            Assert.That(ST01RegionalStoryFacts.TryRead(Read(values),
                out ST01RegionalOutcome saved), Is.True);
            Assert.That(saved.Departure,
                Is.EqualTo(ST01DepartureChoice.StagedAbduction));
        }

        [Test]
        public void JingwanFirstMeetingAndUnrelatedStoryFactsAreNeverInventedOrOverwritten()
        {
            var values = new Dictionary<string, int>();
            Assert.That(ST01WhiteReedBackstageSession.TryCreate(Read(values),
                out _), Is.True);
            Assert.That(ST01RegionalStoryFacts.TryRead(Read(values),
                out ST01RegionalOutcome empty), Is.True);
            Assert.That(empty.Departure.HasValue, Is.False);
            Assert.That(empty.Theater.HasValue, Is.False);
            Assert.That(empty.Ticket, Is.EqualTo(ST01TicketStatus.NotSent));

            Assert.That(ST01RegionalStoryFacts.TryRecordTheater(
                ST01TheaterChoice.KeepContractAndHearResidents,
                Read(values), Write(values)), Is.True);
            Assert.That(ST01WhiteReedBackstageSession.TryCreate(Read(values),
                out _), Is.False);
            Assert.That(ST01RegionalStoryFacts.TryRead(Read(values),
                out ST01RegionalOutcome onlyJingwan), Is.True);
            Assert.That(onlyJingwan.Departure.HasValue, Is.False);
            Assert.That(onlyJingwan.Theater,
                Is.EqualTo(ST01TheaterChoice.KeepContractAndHearResidents));
        }
    }
}
