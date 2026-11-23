using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M04RosterOrphanedDownstreamTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static M04RosterLedger ReconciledLedger()
        {
            var ledger = new M04RosterLedger();
            ledger.AddEntry("dock-01", "Confirmed 1", M04RosterStatus.ConfirmedAtDock);
            ledger.AddEntry("dock-02", "Confirmed 2", M04RosterStatus.ConfirmedAtDock);
            Assert.That(ledger.TryBookForVoyage("dock-01", 2), Is.True);
            return ledger;
        }

        [Test]
        public void ReadAwareTryRecordFailsClosedOnNullDelegatesOrOrphanedDownstreamFacts()
        {
            M04RosterLedger ledger = ReconciledLedger();
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            int rosterWrites = 0;
            Action<string, int> write = (key, value) =>
            {
                if (key == M04RosterFacts.ReconciledKey ||
                    key == M04RosterFacts.TotalEntriesKey ||
                    key == M04RosterFacts.ConfirmedAtDockKey ||
                    key == M04RosterFacts.BookedCountKey ||
                    key == M04RosterFacts.DeclinedCountKey)
                    rosterWrites++;
                values[key] = value;
            };

            Assert.That(M04RosterFacts.TryRecord(ledger, null, write), Is.False);
            Assert.That(M04RosterFacts.TryRecord(ledger, read, null), Is.False);
            Assert.That(rosterWrites, Is.Zero);

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
                Assert.That(M04RosterFacts.TryRecord(ledger, read, write), Is.False, orphanKey);
                Assert.That(rosterWrites, Is.Zero, orphanKey);
                values[orphanKey] = 0;
            }

            Assert.That(M04RosterFacts.TryRecord(ledger, read, write), Is.True);
            Assert.That(M04RosterFacts.TryRead(read, out M04RosterSummaryRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.TotalEntries, Is.EqualTo(2));
            Assert.That(saved.Value.BookedCount, Is.EqualTo(1));
        }
    }
}
