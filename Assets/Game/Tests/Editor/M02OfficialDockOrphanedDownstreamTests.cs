using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M02OfficialDockOrphanedDownstreamTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static M02HarborDeskOutcome StampedOutcome()
        {
            var desk = new M02HarborDesk(M01PensionChoice.RegisterLivingIdentity);
            desk.Begin();
            desk.Act(M02HarborDeskAction.PresentLivingRegistration);
            M02HarborDeskOutcome outcome = desk.Finish();
            Assert.That(outcome.LivingRegistryStamped, Is.True);
            return outcome;
        }

        [Test]
        public void TryRecordLivingRegistrationFailsClosedOnNullDelegatesOrOrphanedDownstreamFacts()
        {
            M02HarborDeskOutcome outcome = StampedOutcome();
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            int dockWrites = 0;
            Action<string, int> write = (key, value) =>
            {
                if (key == M02OfficialDockFacts.LivingRegistrationStampedKey)
                    dockWrites++;
                values[key] = value;
            };

            Assert.That(M02OfficialDockFacts.TryRecordLivingRegistration(
                outcome, null, write), Is.False);
            Assert.That(M02OfficialDockFacts.TryRecordLivingRegistration(
                outcome, read, null), Is.False);
            Assert.That(M02OfficialDockFacts.TryReadEffective(
                null, out bool missing), Is.False);
            Assert.That(missing, Is.False);
            Assert.That(dockWrites, Is.Zero);

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
                Assert.That(M02OfficialDockFacts.TryRecordLivingRegistration(
                    outcome, read, write), Is.False, orphanKey);
                Assert.That(dockWrites, Is.Zero, orphanKey);
                values[orphanKey] = 0;
            }

            Assert.That(M02OfficialDockFacts.TryRecordLivingRegistration(
                outcome, read, write), Is.True);
            Assert.That(M02OfficialDockFacts.TryReadEffective(
                read, out bool effective), Is.True);
            Assert.That(effective, Is.True);
        }
    }
}
