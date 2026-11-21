using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class ST03BathhouseTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int found) ? found : 0;
        }

        private static Action<string, int> Writer(Dictionary<string, int> values)
        {
            return (key, value) => values[key] = value;
        }

        private static ST03BathhouseSession ReachDecision(Dictionary<string, int> values)
        {
            Assert.That(ST03BathhouseSession.TryCreate(Reader(values),
                out ST03BathhouseSession session), Is.True);
            Assert.That(session.TryInspectClue(ST03BathhouseClue.LeakingBoilerAndUnpaidWorkers),
                Is.True);
            Assert.That(session.TryInspectClue(ST03BathhouseClue.OldLockersAndRepairs),
                Is.True);
            Assert.That(session.TryInspectClue(ST03BathhouseClue.ThreeLettersAndWuSuHandwriting),
                Is.True);
            Assert.That(session.TryInterviewWuSu(true, true, true), Is.True);
            Assert.That(session.TryMakeSafeRoute(true, true, true), Is.True);
            Assert.That(session.TryCompleteTrialNight(true, true, true), Is.True);
            Assert.That(session.TryReconcileTransportBudget(true), Is.True);
            Assert.That(session.Phase,
                Is.EqualTo(ST03BathhousePhase.CompareOperatingPlans));
            return session;
        }

        [Test]
        public void ThirdBellRequiresCluesAndResidentsOwnWishesBeforeTrial()
        {
            var values = new Dictionary<string, int>();
            Assert.That(ST03BathhouseSession.TryCreate(Reader(values),
                out ST03BathhouseSession session), Is.True);
            Assert.That(session.TryInterviewWuSu(true, true, true), Is.False);
            Assert.That(session.TryInspectClue(ST03BathhouseClue.ThreeLettersAndWuSuHandwriting),
                Is.True);
            Assert.That(session.TryInspectClue(ST03BathhouseClue.ThreeLettersAndWuSuHandwriting),
                Is.False);
            Assert.That(session.TryInspectClue(ST03BathhouseClue.LeakingBoilerAndUnpaidWorkers),
                Is.True);
            Assert.That(session.TryInspectClue(ST03BathhouseClue.OldLockersAndRepairs),
                Is.True);
            Assert.That(session.InspectedClueCount, Is.EqualTo(3));
            Assert.That(session.TryInterviewWuSu(true, true, false), Is.False,
                "Residents who prefer staying at the care home must be heard.");
            Assert.That(session.TryInterviewWuSu(true, true, true), Is.True);
            Assert.That(session.TryMakeSafeRoute(true, true, false), Is.False,
                "Old shift hours cannot be reused with the new return timetable.");
            Assert.That(session.TryMakeSafeRoute(true, true, true), Is.True);
            Assert.That(session.TryCompleteTrialNight(true, false, true), Is.False);
            Assert.That(session.TryCompleteTrialNight(true, true, true), Is.True);
            Assert.That(session.TryReconcileTransportBudget(false), Is.False,
                "Wu Su's missed carriage cost must be balanced before deciding.");
            Assert.That(session.TryChoose(ST03BathhouseChoice.ReopenWithSubsidizedHours),
                Is.False);
            Assert.That(session.TryReconcileTransportBudget(true), Is.True);
            Assert.That(session.TryChoose(ST03BathhouseChoice.ReopenWithSubsidizedHours),
                Is.True);
            Assert.That(session.TryCommit(Reader(values), Writer(values)), Is.True);
        }

        [Test]
        public void ThreePermanentBusinessModelsProduceThreeDistinctM15Outcomes()
        {
            foreach (ST03BathhouseChoice choice in new[]
            {
                ST03BathhouseChoice.ReopenWithSubsidizedHours,
                ST03BathhouseChoice.ElderRunLaundryBackroom,
                ST03BathhouseChoice.SellAndRelocateToCareHome
            })
            {
                var values = new Dictionary<string, int>();
                ST03BathhouseSession session = ReachDecision(values);
                Assert.That(session.TryChoose(choice), Is.True);
                Assert.That(session.TryCommit(Reader(values), Writer(values)), Is.True);
                Assert.That(ST03BathhouseFacts.TryRead(Reader(values),
                    out ST03BathhouseRecord? saved), Is.True);
                Assert.That(saved.HasValue, Is.True);
                Assert.That(saved.Value.Choice, Is.EqualTo(choice));
                ST03M15BathhouseSign expectedSign =
                    choice == ST03BathhouseChoice.ReopenWithSubsidizedHours
                        ? ST03M15BathhouseSign.OriginalBathhouse
                        : choice == ST03BathhouseChoice.ElderRunLaundryBackroom
                            ? ST03M15BathhouseSign.LaundryBackroom
                            : ST03M15BathhouseSign.NoNewSignAtOldSite;
                Assert.That(saved.Value.M15Sign, Is.EqualTo(expectedSign));
                Assert.That(saved.Value.WusuWorkplace, Is.EqualTo(
                    choice == ST03BathhouseChoice.ReopenWithSubsidizedHours
                        ? ST03WusuWorkplace.OriginalBathhouseDesk
                        : choice == ST03BathhouseChoice.ElderRunLaundryBackroom
                            ? ST03WusuWorkplace.ElderRunBackroom
                            : ST03WusuWorkplace.CareHome));
            }
        }

        [Test]
        public void InterruptedWriteMayOnlyResumeIdenticalOutcomeAndAcknowledgementIsIdempotent()
        {
            var values = new Dictionary<string, int>();
            var session = ReachDecision(values);
            Assert.That(session.TryChoose(ST03BathhouseChoice.SellAndRelocateToCareHome),
                Is.True);
            var read = Reader(values);
            var write = Writer(values);
            bool interrupted = true;
            Assert.Throws<InvalidOperationException>(() => session.TryCommit(read,
                (key, value) =>
                {
                    if (key == ST03BathhouseFacts.ConfirmedKey && interrupted)
                    {
                        interrupted = false;
                        throw new InvalidOperationException("Lost confirmation");
                    }
                    write(key, value);
                }));
            Assert.That(ST03BathhouseFacts.TryRead(read, out _), Is.False,
                "Partially written facts are not a committed event.");
            Assert.That(ST03BathhouseFacts.TryRecord(
                new ST03BathhouseRecord(ST03BathhouseChoice.ReopenWithSubsidizedHours,
                    true, true, true, true), read, write), Is.False,
                "A competing decision cannot overwrite the pending choice.");
            Assert.That(session.TryCommit(read, write), Is.True);
            Assert.That(session.Phase, Is.EqualTo(ST03BathhousePhase.Completed));

            var resumed = ReachDecisionAfterResettingOnlySession(values);
            Assert.That(resumed, Is.Null,
                "A committed ST-03 decision never reopens as a new encounter.");
            Assert.That(ST03BathhouseFacts.TryRead(read, out ST03BathhouseRecord? saved),
                Is.True);
            Assert.That(saved.Value.Choice,
                Is.EqualTo(ST03BathhouseChoice.SellAndRelocateToCareHome));
        }

        private static ST03BathhouseSession ReachDecisionAfterResettingOnlySession(
            Dictionary<string, int> values)
        {
            ST03BathhouseSession.TryCreate(Reader(values),
                out ST03BathhouseSession session);
            return session;
        }


        [Test]
        public void FirstPersistedChoiceIntentSurvivesTornWritesAndDetectsValidOutcomeTampering()
        {
            var values = new Dictionary<string, int>();
            var read = Reader(values);
            var write = Writer(values);
            var original = new ST03BathhouseRecord(
                ST03BathhouseChoice.SellAndRelocateToCareHome, true, true, true, true);
            var competing = new ST03BathhouseRecord(
                ST03BathhouseChoice.ReopenWithSubsidizedHours, true, true, true, true);

            Assert.Throws<InvalidOperationException>(() =>
                ST03BathhouseFacts.TryRecord(original, read, (key, value) =>
                {
                    write(key, value);
                    if (key == ST03BathhouseFacts.PendingChoiceKey)
                        throw new InvalidOperationException("Interrupted after first intent");
                }));
            Assert.That(read(ST03BathhouseFacts.PendingChoiceKey),
                Is.EqualTo((int)original.Choice));
            Assert.That(read(ST03BathhouseFacts.ChoiceKey), Is.Zero);
            Assert.That(ST03BathhouseFacts.TryRead(read, out _), Is.False);
            Assert.That(ST03BathhouseSession.TryCreate(read, out _), Is.False);
            Assert.That(ST03BathhouseFacts.TryRecord(competing, read, write), Is.False);
            Assert.That(ST03BathhouseFacts.TryRecord(original, read, write), Is.True);
            Assert.That(ST03BathhouseFacts.TryRead(read,
                out ST03BathhouseRecord? completed), Is.True);
            Assert.That(completed.HasValue, Is.True);
            Assert.That(completed.Value.Choice, Is.EqualTo(original.Choice));

            // Two valid choices still must not be interchangeable in a signed save.
            values[ST03BathhouseFacts.ChoiceKey] = (int)competing.Choice;
            Assert.That(ST03BathhouseFacts.TryRead(read, out _), Is.False);
            values[ST03BathhouseFacts.ChoiceKey] = (int)original.Choice;
            Assert.That(ST03BathhouseFacts.TryRead(read, out _), Is.True);

            // Older already-confirmed saves did not store a choice lock.
            values[ST03BathhouseFacts.PendingChoiceKey] = 0;
            Assert.That(ST03BathhouseFacts.TryRead(read,
                out ST03BathhouseRecord? legacy), Is.True);
            Assert.That(legacy.HasValue, Is.True);
            Assert.That(legacy.Value.Choice, Is.EqualTo(original.Choice));

            Assert.That(ST03BathhouseFacts.TryResetForNewGame(write), Is.True);
            Assert.That(read(ST03BathhouseFacts.PendingChoiceKey), Is.Zero);
            Assert.That(ST03BathhouseFacts.TryRecord(competing, read, write), Is.True);
        }

        [Test]
        public void InvalidFlagsNeverCreateBathhouseSignAndOnlyNewGameResets()
        {
            var values = new Dictionary<string, int>();
            ST03BathhouseSession session = ReachDecision(values);
            Assert.That(session.TryChoose(ST03BathhouseChoice.ReopenWithSubsidizedHours),
                Is.True);
            Assert.That(session.TryCommit(Reader(values), Writer(values)), Is.True);
            var read = Reader(values);

            values[ST03BathhouseFacts.TransportCostKey] = 0;
            Assert.That(ST03BathhouseFacts.TryRead(read, out _), Is.False);
            values[ST03BathhouseFacts.TransportCostKey] = 1;
            values[ST03BathhouseFacts.ChoiceKey] = 99;
            Assert.That(ST03BathhouseFacts.TryRead(read, out _), Is.False);
            values[ST03BathhouseFacts.ChoiceKey] =
                (int)ST03BathhouseChoice.ReopenWithSubsidizedHours;
            Assert.That(ST03BathhouseFacts.TryRead(read, out ST03BathhouseRecord? valid),
                Is.True);
            Assert.That(valid.Value.M15Sign,
                Is.EqualTo(ST03M15BathhouseSign.OriginalBathhouse));

            // An outbound voyage replay never calls this reset method.
            Assert.That(ST03BathhouseFacts.TryResetForNewGame(Writer(values)), Is.True);
            Assert.That(ST03BathhouseFacts.TryRead(read, out ST03BathhouseRecord? fresh),
                Is.True);
            Assert.That(fresh.HasValue, Is.False);
        }
    }
}
