using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M01PensionOrphanedDownstreamTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void TryRecordPensionChoiceFailsClosedOnInvalidInputOrOrphanedDownstreamFacts()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            int pensionWrites = 0;
            Action<string, int> write = (key, value) =>
            {
                if (key == M01HomecomingResponse.PensionChoiceKey)
                    pensionWrites++;
                values[key] = value;
            };

            Assert.That(M01HomecomingResponse.TryRecordPensionChoice(
                (M01PensionChoice)99, read, write), Is.False);
            Assert.That(M01HomecomingResponse.TryRecordPensionChoice(
                M01PensionChoice.RegisterLivingIdentity, null, write), Is.False);
            Assert.That(M01HomecomingResponse.TryRecordPensionChoice(
                M01PensionChoice.RegisterLivingIdentity, read, null), Is.False);
            Assert.That(pensionWrites, Is.Zero);

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
                Assert.That(M01HomecomingResponse.TryRecordPensionChoice(
                    M01PensionChoice.RequestMissingReturnReview, read, write),
                    Is.False, orphanKey);
                Assert.That(pensionWrites, Is.Zero, orphanKey);
                values[orphanKey] = 0;
            }

            Assert.That(M01HomecomingResponse.TryRecordPensionChoice(
                M01PensionChoice.RequestMissingReturnReview, read, write), Is.True);
            Assert.That(values[M01HomecomingResponse.PensionChoiceKey], Is.EqualTo(3));
        }
    }
}
