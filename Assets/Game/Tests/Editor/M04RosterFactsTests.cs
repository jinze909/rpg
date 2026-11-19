using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M04RosterFactsTests
    {
        private static M04RosterLedger ReconciledLedger()
        {
            var ledger = new M04RosterLedger();
            ledger.AddEntry("roll-01", "Listed person", M04RosterStatus.ListedDead);
            ledger.MarkConfirmedAtDock("roll-01");
            ledger.ResolveRollCorrection("roll-01");
            ledger.AddEntry("dock-01", "Confirmed 1", M04RosterStatus.ConfirmedAtDock);
            ledger.AddEntry("dock-02", "Confirmed 2", M04RosterStatus.ConfirmedAtDock);
            ledger.AddEntry("unconfirmed-01", "Unconfirmed 1", M04RosterStatus.RansomedUnconfirmed);

            Assert.That(ledger.TryBookForVoyage("roll-01", 2), Is.True);
            ledger.MarkDeclinedVoyage("dock-02");
            return ledger;
        }

        [Test]
        public void NullOrUnresolvedDiscrepancyLedgerIsRejectedWithoutWritingVariables()
        {
            var store = new Dictionary<string, int>();
            Action<string, int> setInt = (key, value) => store[key] = value;

            Assert.Throws<ArgumentNullException>(() =>
                M04RosterFacts.TryRecord(null, setInt));

            var unresolved = new M04RosterLedger();
            unresolved.AddEntry("roll-01", "Listed person", M04RosterStatus.ListedDead);
            unresolved.MarkConfirmedAtDock("roll-01");

            Assert.Throws<InvalidOperationException>(() =>
                M04RosterFacts.TryRecord(unresolved, setInt));
            Assert.That(store, Is.Empty);
        }

        [Test]
        public void MissingStorageGuardsDoNotAssumeGlobalMakinomIsUninitialized()
        {
            M04RosterLedger ledger = ReconciledLedger();

            // Other P0 fixtures can initialize Makinom before this test runs.
            // Null storage delegates must fail independently of test order.
            Assert.That(M04RosterFacts.TryResetForNewVisit((Action<string, int>)null), Is.False);
            Assert.That(M04RosterFacts.TryRecord(ledger, (Action<string, int>)null), Is.False);
            Assert.That(M04RosterFacts.TryRead((Func<string, int>)null,
                out M04RosterSummaryRecord? noStore), Is.False);
            Assert.That(noStore, Is.Null);

            bool hasGlobalStorage = GamingIsLove.Makinom.Maki.Initialized &&
                GamingIsLove.Makinom.Maki.Game?.Variables != null;
            Assert.That(M04RosterFacts.TryRead(out M04RosterSummaryRecord? globalRecord),
                Is.EqualTo(hasGlobalStorage));
            if (!hasGlobalStorage)
            {
                Assert.That(M04RosterFacts.TryResetForNewVisit(), Is.False);
                Assert.That(M04RosterFacts.TryRecord(ledger), Is.False);
                Assert.That(globalRecord, Is.Null);
            }
        }

        [Test]
        public void ReconciledLedgerPersistsAndReadsBackExactCounts()
        {
            var store = new Dictionary<string, int>();
            Action<string, int> setInt = (key, value) => store[key] = value;
            Func<string, int> getInt = key => store.TryGetValue(key, out int value) ? value : 0;

            M04RosterLedger ledger = ReconciledLedger();
            Assert.That(M04RosterFacts.TryRecord(ledger, setInt), Is.True);
            Assert.That(M04RosterFacts.TryRead(getInt, out M04RosterSummaryRecord? record), Is.True);
            Assert.That(record.HasValue, Is.True);
            Assert.That(record.Value.TotalEntries, Is.EqualTo(4));
            Assert.That(record.Value.ConfirmedAtDockCount, Is.EqualTo(1));
            Assert.That(record.Value.BookedCount, Is.EqualTo(1));
            Assert.That(record.Value.DeclinedCount, Is.EqualTo(1));
        }

        [Test]
        public void ResetForNewVisitClearsPriorRosterSummaryFacts()
        {
            var store = new Dictionary<string, int>();
            Action<string, int> setInt = (key, value) => store[key] = value;
            Func<string, int> getInt = key => store.TryGetValue(key, out int value) ? value : 0;

            Assert.That(M04RosterFacts.TryRecord(ReconciledLedger(), setInt), Is.True);
            Assert.That(M04RosterFacts.TryRead(getInt, out M04RosterSummaryRecord? beforeReset), Is.True);
            Assert.That(beforeReset.HasValue, Is.True);

            Assert.That(M04RosterFacts.TryResetForNewVisit(setInt), Is.True);
            Assert.That(store[M04RosterFacts.ReconciledKey], Is.Zero);
            Assert.That(store[M04RosterFacts.TotalEntriesKey], Is.Zero);
            Assert.That(store[M04RosterFacts.ConfirmedAtDockKey], Is.Zero);
            Assert.That(store[M04RosterFacts.BookedCountKey], Is.Zero);
            Assert.That(store[M04RosterFacts.DeclinedCountKey], Is.Zero);

            Assert.That(M04RosterFacts.TryRead(getInt, out M04RosterSummaryRecord? afterReset), Is.True);
            Assert.That(afterReset, Is.Null);
        }

        [Test]
        public void InterruptedReconciliationWriteCannotRetainEarlierConfirmedRoster()
        {
            var store = new Dictionary<string, int>();
            Action<string, int> setInt = (key, value) => store[key] = value;
            Func<string, int> getInt = key => store.TryGetValue(key, out int value) ? value : 0;

            Assert.That(M04RosterFacts.TryRecord(ReconciledLedger(), setInt), Is.True);
            Assert.That(M04RosterFacts.TryRead(getInt, out M04RosterSummaryRecord? original), Is.True);
            Assert.That(original.HasValue, Is.True);

            Assert.Throws<InvalidOperationException>(() =>
                M04RosterFacts.TryRecord(ReconciledLedger(), (key, value) =>
                {
                    if (key == M04RosterFacts.BookedCountKey)
                        throw new InvalidOperationException("Simulated interrupted roster save");
                    store[key] = value;
                }));

            Assert.That(store[M04RosterFacts.ReconciledKey], Is.Zero);
            Assert.That(M04RosterFacts.TryRead(getInt, out M04RosterSummaryRecord? incomplete), Is.True);
            Assert.That(incomplete, Is.Null,
                "A partially rewritten roster must never inherit the prior reconciled flag.");

            var writes = new List<string>();
            Assert.That(M04RosterFacts.TryRecord(ReconciledLedger(), (key, value) =>
            {
                writes.Add(key);
                store[key] = value;
            }), Is.True);
            Assert.That(writes[0], Is.EqualTo(M04RosterFacts.ReconciledKey));
            Assert.That(writes[writes.Count - 1], Is.EqualTo(M04RosterFacts.ReconciledKey));
            Assert.That(M04RosterFacts.TryRead(getInt, out M04RosterSummaryRecord? restored), Is.True);
            Assert.That(restored.HasValue, Is.True);
            Assert.That(restored.Value.BookedCount, Is.EqualTo(1));
        }

        [Test]
        public void CorruptedOrInflatedSavedVariablesNeverFabricateReconciledRoster()
        {
            var baseStore = new Dictionary<string, int>();
            M04RosterFacts.TryRecord(ReconciledLedger(), (key, value) => baseStore[key] = value);

            void AssertRejected(Action<Dictionary<string, int>> corrupt)
            {
                var copy = new Dictionary<string, int>(baseStore);
                corrupt(copy);
                Func<string, int> getInt = key => copy.TryGetValue(key, out int value) ? value : 0;
                Assert.That(M04RosterFacts.TryRead(getInt, out M04RosterSummaryRecord? record), Is.True);
                Assert.That(record, Is.Null);
            }

            AssertRejected(s => s[M04RosterFacts.ReconciledKey] = 0);
            AssertRejected(s => s[M04RosterFacts.TotalEntriesKey] = -1);
            AssertRejected(s => s[M04RosterFacts.ConfirmedAtDockKey] = -1);
            AssertRejected(s => s[M04RosterFacts.BookedCountKey] = -1);
            AssertRejected(s => s[M04RosterFacts.DeclinedCountKey] = -1);
            AssertRejected(s => s[M04RosterFacts.BookedCountKey] = 3);
        }
    }
}
