using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M04QiQueCupOrphanedDownstreamTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static M04QiQueCupEncounter CompletedCupEncounter()
        {
            var cup = new M04QiQueCupEncounter(true, false);
            cup.Begin();
            cup.ExamineGift();
            cup.Choose(M04QiQueCupReply.TeaseUglyCup);
            cup.Finish();
            Assert.That(cup.Stage, Is.EqualTo(M04QiQueCupStage.Complete));
            return cup;
        }

        [Test]
        public void TryRecordFailsClosedOnNullDelegatesOrOrphanedDownstreamFacts()
        {
            M04QiQueCupEncounter cup = CompletedCupEncounter();
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            int cupWrites = 0;
            Action<string, int> write = (key, value) =>
            {
                if (key == M04QiQueCupFacts.CupCarriedKey)
                    cupWrites++;
                values[key] = value;
            };

            Assert.That(M04QiQueCupFacts.TryResetForNewVisit(null), Is.False);
            Assert.That(M04QiQueCupFacts.TryRecord(cup, null, write), Is.False);
            Assert.That(M04QiQueCupFacts.TryRecord(cup, read, null), Is.False);
            Assert.That(M04QiQueCupFacts.TryRead(null, out bool missing), Is.False);
            Assert.That(missing, Is.False);
            Assert.That(cupWrites, Is.Zero);

            string[] orphanKeys =
            {
                M07ExtractionFacts.ExecutedRouteKey,
                M08FirstWorldNoticeFacts.ConfirmedKey,
                M09BerthTableFacts.ConfirmedKey,
                RC05ThirdSquadFacts.ConfirmedKey,
                M10AssemblyFacts.ConfirmedKey,
                M16EpilogueFacts.ConfirmedKey
            };

            foreach (string orphanKey in orphanKeys)
            {
                values[orphanKey] = 1;
                Assert.That(M04QiQueCupFacts.TryRecord(cup, read, write), Is.False, orphanKey);
                Assert.That(cupWrites, Is.Zero, orphanKey);
                values[orphanKey] = 0;
            }

            Assert.That(M04QiQueCupFacts.TryRecord(cup, read, write), Is.True);
            Assert.That(M04QiQueCupFacts.TryRead(read, out bool carrying), Is.True);
            Assert.That(carrying, Is.True);
            Assert.That(M04QiQueCupFacts.TryResetForNewVisit(write), Is.True);
            Assert.That(M04QiQueCupFacts.TryRead(read, out carrying), Is.False);
        }
    }
}
