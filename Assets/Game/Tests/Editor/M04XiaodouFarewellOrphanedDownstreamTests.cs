using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M04XiaodouFarewellOrphanedDownstreamTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static M04XiaodouFarewell CompletedFarewell()
        {
            var farewell = new M04XiaodouFarewell(promptedOnStage: true);
            farewell.Begin();
            farewell.ReceiveCandy();
            farewell.AnswerChild(M04XiaodouAnswer.TeachRealLine);
            farewell.SayGoodbye();
            Assert.That(farewell.Stage, Is.EqualTo(M04FarewellStage.Complete));
            return farewell;
        }

        [Test]
        public void TryRecordFailsClosedOnNullDelegatesOrOrphanedDownstreamFacts()
        {
            M04XiaodouFarewell farewell = CompletedFarewell();
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            int farewellWrites = 0;
            Action<string, int> write = (key, value) =>
            {
                if (key == M04XiaodouFarewellFacts.RealLineKey)
                    farewellWrites++;
                values[key] = value;
            };

            Assert.That(M04XiaodouFarewellFacts.TryResetForNewVisit(null), Is.False);
            Assert.That(M04XiaodouFarewellFacts.TryRecord(farewell, null, write), Is.False);
            Assert.That(M04XiaodouFarewellFacts.TryRecord(farewell, read, null), Is.False);
            Assert.That(farewellWrites, Is.Zero);

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
                Assert.That(M04XiaodouFarewellFacts.TryRecord(farewell, read, write),
                    Is.False, orphanKey);
                Assert.That(farewellWrites, Is.Zero, orphanKey);
                values[orphanKey] = 0;
            }

            Assert.That(M04XiaodouFarewellFacts.TryRecord(farewell, read, write), Is.True);
            Assert.That(M04XiaodouFarewellFacts.TryRead(read, out bool taught), Is.True);
            Assert.That(taught, Is.True);
            Assert.That(M04XiaodouFarewellFacts.TryResetForNewVisit(write), Is.True);
            Assert.That(M04XiaodouFarewellFacts.TryRead(read, out taught), Is.False);
        }
    }
}
