using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M04RosterReconciliationTests
    {
        [Test]
        public void DuplicateInvalidOrPrebookedEntriesAreRejected()
        {
            var ledger = new M04RosterLedger();
            ledger.AddEntry("ret-01", "Confirmed returnee 1", M04RosterStatus.ConfirmedAtDock);

            Assert.Throws<ArgumentException>(() =>
                ledger.AddEntry(" ", "Unnamed", M04RosterStatus.ListedDead));
            Assert.Throws<ArgumentException>(() =>
                ledger.AddEntry("ret-02", " ", M04RosterStatus.ListedDead));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ledger.AddEntry("ret-02", "Invalid status", (M04RosterStatus)99));
            Assert.Throws<InvalidOperationException>(() =>
                ledger.AddEntry("ret-02", "Prebooked", M04RosterStatus.BookedForVoyage));
            Assert.Throws<InvalidOperationException>(() =>
                ledger.AddEntry("RET-01", "Duplicate case-insensitive", M04RosterStatus.ListedDead));

            Assert.That(ledger.TotalEntries, Is.EqualTo(1));
        }

        [Test]
        public void ConfirmingLivingReturneeFromDeadRollFlagsAndResolvesDiscrepancy()
        {
            var ledger = new M04RosterLedger();
            M04RosterEntry listed = ledger.AddEntry(
                "roll-01", "Memorial listed person", M04RosterStatus.ListedDead);
            M04RosterEntry ransomed = ledger.AddEntry(
                "ransom-01", "Ransom ledger person", M04RosterStatus.RansomedUnconfirmed);

            Assert.That(ledger.UnresolvedDiscrepancyCount, Is.Zero);

            ledger.MarkConfirmedAtDock("roll-01");
            ledger.MarkConfirmedAtDock("ransom-01");

            Assert.That(listed.Status, Is.EqualTo(M04RosterStatus.ConfirmedAtDock));
            Assert.That(listed.RequiresRollCorrection, Is.True);
            Assert.That(ransomed.Status, Is.EqualTo(M04RosterStatus.ConfirmedAtDock));
            Assert.That(ransomed.RequiresRollCorrection, Is.False);
            Assert.That(ledger.UnresolvedDiscrepancyCount, Is.EqualTo(1));

            ledger.ResolveRollCorrection("roll-01");
            Assert.That(listed.RequiresRollCorrection, Is.False);
            Assert.That(ledger.UnresolvedDiscrepancyCount, Is.Zero);
            Assert.Throws<InvalidOperationException>(() =>
                ledger.ResolveRollCorrection("roll-01"));
        }

        [Test]
        public void OnlyConfirmedDocksideReturneesCanBookWithinVerifiedBerths()
        {
            var ledger = new M04RosterLedger();
            ledger.AddEntry("dead-01", "Listed dead", M04RosterStatus.ListedDead);
            ledger.AddEntry("unconfirmed-01", "Unconfirmed arrival", M04RosterStatus.RansomedUnconfirmed);
            ledger.AddEntry("dock-01", "Confirmed 1", M04RosterStatus.ConfirmedAtDock);
            ledger.AddEntry("dock-02", "Confirmed 2", M04RosterStatus.ConfirmedAtDock);

            Assert.That(ledger.TryBookForVoyage("dead-01", 2), Is.False);
            Assert.That(ledger.TryBookForVoyage("unconfirmed-01", 2), Is.False);
            Assert.That(ledger.TryBookForVoyage("dock-01", 0), Is.False);
            Assert.That(ledger.TryBookForVoyage("dock-01", 1), Is.True);
            Assert.That(ledger.TryBookForVoyage("dock-01", 2), Is.False,
                "An already booked returnee cannot be booked twice.");
            Assert.That(ledger.TryBookForVoyage("dock-02", 1), Is.False,
                "Booking cannot exceed verified berths.");
            Assert.That(ledger.BookedCount, Is.EqualTo(1));
            Assert.That(ledger.ConfirmedAtDockCount, Is.EqualTo(1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ledger.TryBookForVoyage("dock-02", -1));
        }

        [Test]
        public void DeadRollReturneeCannotBookBeforeTheirCorrectionIsResolved()
        {
            var ledger = new M04RosterLedger();
            M04RosterEntry survivor = ledger.AddEntry(
                "roll-01", "Survivor incorrectly listed dead", M04RosterStatus.ListedDead);
            ledger.AddEntry("dock-01", "Other confirmed returnee", M04RosterStatus.ConfirmedAtDock);
            ledger.MarkConfirmedAtDock("roll-01");

            Assert.That(ledger.TryBookForVoyage("roll-01", 2), Is.False,
                "Dockside confirmation alone must not bypass a pending dead-roll correction.");
            Assert.That(survivor.Status, Is.EqualTo(M04RosterStatus.ConfirmedAtDock));
            Assert.That(survivor.RequiresRollCorrection, Is.True);
            Assert.That(ledger.BookedCount, Is.Zero);
            Assert.That(ledger.UnresolvedDiscrepancyCount, Is.EqualTo(1));

            // An unrelated confirmed returnee can still be assigned a berth;
            // the unresolved ledger remains ineligible for final partitioning.
            Assert.That(ledger.TryBookForVoyage("dock-01", 2), Is.True);
            Assert.That(ledger.TryBookForVoyage("roll-01", 2), Is.False);
            Assert.Throws<InvalidOperationException>(() =>
                M04VoyageBatchPlan.PartitionConfirmed(ledger, 2));

            ledger.ResolveRollCorrection("roll-01");
            Assert.That(ledger.TryBookForVoyage("roll-01", 2), Is.True);
            Assert.That(ledger.BookedCount, Is.EqualTo(2));
            Assert.That(ledger.UnresolvedDiscrepancyCount, Is.Zero);
            Assert.That(M04VoyageBatchPlan.PartitionConfirmed(ledger, 2)[0].Length,
                Is.EqualTo(2));
        }

        [Test]
        public void DecliningOrUnbookingFreesBerthForAnotherConfirmedReturnee()
        {
            var ledger = new M04RosterLedger();
            ledger.AddEntry("dock-01", "Confirmed 1", M04RosterStatus.ConfirmedAtDock);
            ledger.AddEntry("dock-02", "Confirmed 2", M04RosterStatus.ConfirmedAtDock);
            ledger.AddEntry("dock-03", "Confirmed 3", M04RosterStatus.ConfirmedAtDock);

            Assert.That(ledger.TryBookForVoyage("dock-01", 1), Is.True);
            ledger.UnbookFromVoyage("dock-01");
            Assert.That(ledger.BookedCount, Is.Zero);
            Assert.That(ledger.ConfirmedAtDockCount, Is.EqualTo(3));

            Assert.That(ledger.TryBookForVoyage("dock-02", 1), Is.True);
            ledger.MarkDeclinedVoyage("dock-02");
            Assert.That(ledger.DeclinedCount, Is.EqualTo(1));
            Assert.That(ledger.BookedCount, Is.Zero);

            Assert.That(ledger.TryBookForVoyage("dock-03", 1), Is.True);
            Assert.That(ledger.BookedCount, Is.EqualTo(1));
            Assert.That(ledger.Snapshot(), Has.Length.EqualTo(3));
        }

        [Test]
        public void InvalidTransitionsAndUnknownEntriesThrowWithoutCorruptingCounts()
        {
            var ledger = new M04RosterLedger();
            ledger.AddEntry("dead-01", "Listed dead", M04RosterStatus.ListedDead);
            ledger.AddEntry("dock-01", "Confirmed 1", M04RosterStatus.ConfirmedAtDock);

            Assert.Throws<KeyNotFoundException>(() => ledger.MarkConfirmedAtDock("missing"));
            Assert.Throws<KeyNotFoundException>(() => ledger.ResolveRollCorrection("missing"));
            Assert.Throws<KeyNotFoundException>(() => ledger.UnbookFromVoyage("missing"));
            Assert.Throws<KeyNotFoundException>(() => ledger.MarkDeclinedVoyage("missing"));
            Assert.Throws<InvalidOperationException>(() => ledger.MarkConfirmedAtDock("dock-01"));
            Assert.Throws<InvalidOperationException>(() => ledger.UnbookFromVoyage("dock-01"));
            Assert.Throws<InvalidOperationException>(() => ledger.MarkDeclinedVoyage("dead-01"));

            Assert.That(ledger.TotalEntries, Is.EqualTo(2));
            Assert.That(ledger.ConfirmedAtDockCount, Is.EqualTo(1));
            Assert.That(ledger.BookedCount, Is.Zero);
        }
    }
}
