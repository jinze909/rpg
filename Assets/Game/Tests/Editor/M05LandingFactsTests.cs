using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M05LandingFactsTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedCompletedCrossing(Dictionary<string, int> values,
            bool sailed = true, bool crossedFog = true)
        {
            Action<string, int> write = (key, value) => values[key] = value;
            Func<string, int> read = Reader(values);
            var manifest = new M04DepartureManifest(M03VoyageChoice.QiQueMerchant,
                "Verified civilian hull", "A Yuan", 4, "Shallow tender",
                true, 16, 8, "Named receiver", false, false);
            Assert.That(manifest.ConfirmDeparture(), Is.True);
            Assert.That(M04DepartureFacts.TryRecord(manifest, write), Is.True);
            if (sailed)
                Assert.That(M04DepartureFacts.TryMarkSailed(read, write), Is.True);
            if (crossedFog)
            {
                var fog = new FogListeningSession(
                    new[] { new FogListeningCue(FogBearing.Ahead,
                        FogCueKind.ClearWater, 80) },
                    maxListenFocus: 1, maxHullIntegrity: 2);
                fog.ApplyHelmAction(FogHelmAction.HoldCourse);
                Assert.That(fog.IsComplete && fog.Succeeded, Is.True);
                Assert.That(FogListeningFacts.TryRecord(fog, write), Is.True);
            }
        }

        private static void SeedCurrentVoyage(Dictionary<string, int> values,
            M03VoyageChoice chosenRoute)
        {
            var encounter = new M03LuJunEncounter(M02BridgeEvidence.None, true);
            encounter.Begin();
            encounter.ObserveRollCall();
            encounter.Ask(M03LuJunOpening.NeedAShip);
            encounter.ChooseVoyage(chosenRoute);
            Assert.That(M03LuJunFacts.TryRecord(encounter,
                (key, value) => values[key] = value), Is.True);
        }

        [Test]
        public void CurrentRouteMismatchCannotConfirmOrReadAnOldLanding()
        {
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            var read = Reader(values);
            var report = new M05LandingReport(3, 9, true, true);
            int writes = 0;
            Action<string, int> write = (key, value) =>
            {
                writes++;
                values[key] = value;
            };

            SeedCurrentVoyage(values, M03VoyageChoice.QiQueMerchant);
            Assert.That(M05LandingFacts.CanRecord(report, read), Is.True);
            SeedCurrentVoyage(values, M03VoyageChoice.LuJunEscort);
            Assert.That(M05LandingFacts.CanRecord(report, read), Is.False,
                "An old merchant crossing cannot authorize a Lu Jun arrival.");
            Assert.That(M05LandingFacts.TryRecord(report, read, write), Is.False);
            Assert.That(writes, Is.Zero, "A rejected arrival must not alter the save.");

            SeedCurrentVoyage(values, M03VoyageChoice.QiQueMerchant);
            Assert.That(M05LandingFacts.TryRecord(report, read, write), Is.True);
            Assert.That(M05LandingFacts.TryRead(read, out M05LandingReport? arrived),
                Is.True);
            Assert.That(arrived.HasValue, Is.True);

            SeedCurrentVoyage(values, M03VoyageChoice.HanQiaoOfficial);
            Assert.That(M05LandingFacts.TryRead(read, out M05LandingReport? stale),
                Is.True);
            Assert.That(stale, Is.Null,
                "A saved merchant landing cannot be shown as this official voyage.");
            values[M03LuJunFacts.VoyageKey] = 999;
            Assert.That(M05LandingFacts.TryRead(read, out M05LandingReport? invalid),
                Is.True);
            Assert.That(invalid, Is.Null,
                "A corrupted recorded route must not reactivate an old landing.");
        }

        [Test]
        public void IdenticalNewLandingGetsNewPersistentSequenceWithoutReusingOldArrival()
        {
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            var report = new M05LandingReport(3, 10, true, true);

            Assert.That(M05LandingFacts.TryRecord(report, read, write), Is.True);
            int firstSequence = read(M05LandingFacts.ArrivalSequenceKey);
            Assert.That(firstSequence, Is.EqualTo(1));
            Assert.That(M05LandingFacts.TryRecord(report, read, write), Is.False);
            Assert.That(read(M05LandingFacts.ArrivalSequenceKey), Is.EqualTo(firstSequence));

            Assert.That(ReturnOpeningContinuation.TryResetPreviousOutboundFacts(write), Is.True);
            Assert.That(read(M05LandingFacts.ArrivalSequenceKey), Is.EqualTo(firstSequence));
            SeedCompletedCrossing(values);
            Assert.That(M05LandingFacts.TryRecord(report, read, write), Is.True);
            Assert.That(read(M05LandingFacts.ArrivalSequenceKey),
                Is.EqualTo(firstSequence + 1));
            Assert.That(M05LandingFacts.TryRead(read, out M05LandingReport? second), Is.True);
            Assert.That(second.HasValue, Is.True);
            Assert.That(second.Value.CrewArrived, Is.EqualTo(report.CrewArrived));

            values[M05LandingFacts.ArrivalSequenceKey] = -1;
            Assert.That(M05LandingFacts.TryRead(read, out _), Is.False);
            Assert.That(M05LandingFacts.CanRecord(report, read), Is.False);
        }

        [Test]
        public void ReadinessSailingAndFogDoNotAutomaticallyInventAnArrival()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            var landing = new M05LandingReport(3, 10, true, true);

            Assert.That(M05LandingFacts.TryRecord(landing, read, write), Is.False);
            SeedCompletedCrossing(values, sailed: false, crossedFog: false);
            Assert.That(M05LandingFacts.TryRecord(landing, read, write), Is.False);
            Assert.That(M04DepartureFacts.TryMarkSailed(read, write), Is.True);
            var fog = new FogListeningSession(
                new[] { new FogListeningCue(FogBearing.Ahead,
                    FogCueKind.ClearWater, 80) },
                maxListenFocus: 1, maxHullIntegrity: 2);
            fog.ApplyHelmAction(FogHelmAction.HoldCourse);
            Assert.That(FogListeningFacts.TryRecord(fog, write), Is.True);
            Assert.That(M05LandingFacts.TryRead(read, out M05LandingReport? before), Is.True);
            Assert.That(before, Is.Null, "Crossing the fog is not a port arrival.");
            Assert.That(M05LandingFacts.TryRecord(landing, read, write), Is.True);
            Assert.That(M05LandingFacts.TryRead(read, out M05LandingReport? after), Is.True);
            Assert.That(after.HasValue, Is.True);
            Assert.That(after.Value.CrewArrived, Is.EqualTo(3));
            Assert.That(after.Value.ReadyRescueBerths, Is.EqualTo(10));
            Assert.That(after.Value.HasUsableLandingCapacity, Is.True);
            Assert.That(M05LandingFacts.TryRecord(landing, read, write), Is.False,
                "Repeated M05 confirmation must not replace the observed landing.");
        }

        [Test]
        public void FailedOrMissingFogLegCannotBecomeAnArrivedVessel()
        {
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values, crossedFog: false);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            var landing = new M05LandingReport(4, 12, true, true);
            Assert.That(M05LandingFacts.TryRecord(landing, read, write), Is.False);
            values[FogListeningFacts.CompletedKey] = 1;
            values[FogListeningFacts.SucceededKey] = 0;
            values[FogListeningFacts.HullIntegrityKey] = 0;
            values[FogListeningFacts.AvoidedHazardsKey] = 0;
            values[FogListeningFacts.MatchedSignalsKey] = 0;
            values[FogListeningFacts.RemainingFocusKey] = 0;
            Assert.That(M05LandingFacts.TryRecord(landing, read, write), Is.False);
            Assert.That(M05LandingFacts.TryRead(read, out M05LandingReport? missing), Is.True);
            Assert.That(missing, Is.Null);
        }

        [Test]
        public void DelayedTenderOrMissingReceiverCannotInflateRescueCapacity()
        {
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;

            Assert.That(M05LandingFacts.TryRecord(
                new M05LandingReport(3, 5, false, true), read, write), Is.False);
            Assert.That(M05LandingFacts.TryRecord(
                new M05LandingReport(3, 5, true, false), read, write), Is.False);
            Assert.That(M05LandingFacts.TryRecord(
                new M05LandingReport(5, 5, true, true), read, write), Is.False);
            Assert.That(M05LandingFacts.TryRecord(
                new M05LandingReport(2, 17, true, true), read, write), Is.False);
            Assert.That(M05LandingFacts.TryRecord(
                new M05LandingReport(0, 0, false, false), read, write), Is.False);
            Assert.That(values.ContainsKey(M05LandingFacts.ConfirmedKey), Is.False);

            Assert.That(M05LandingFacts.TryRecord(
                new M05LandingReport(2, 0, false, false), read, write), Is.True);
            Assert.That(M05LandingFacts.TryRead(read, out M05LandingReport? delayed), Is.True);
            Assert.That(delayed.Value.ReadyRescueBerths, Is.Zero);
            Assert.That(delayed.Value.HasUsableLandingCapacity, Is.False);
        }

        [Test]
        public void CorruptSavedFieldsOrUpstreamCrossingCannotBeReadAsArrival()
        {
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            Assert.That(M05LandingFacts.TryRecord(
                new M05LandingReport(3, 9, true, true), read, write), Is.True);
            var valid = new Dictionary<string, int>(values);

            foreach (var bad in new[]
            {
                (M05LandingFacts.CrewArrivedKey, -1),
                (M05LandingFacts.CrewArrivedKey, 5),
                (M05LandingFacts.ReadyBerthsKey, 17),
                (M05LandingFacts.TenderArrivedKey, 2),
                (M05LandingFacts.ReceiverMetKey, -1),
                (M05LandingFacts.ConfirmedKey, 2),
            })
            {
                var altered = new Dictionary<string, int>(valid) { [bad.Item1] = bad.Item2 };
                Assert.That(M05LandingFacts.TryRead(Reader(altered),
                    out M05LandingReport? record),
                    Is.EqualTo(bad.Item1 != M05LandingFacts.ConfirmedKey),
                    bad.Item1);
                Assert.That(record, Is.Null, bad.Item1);
            }
            values[M04DepartureFacts.SailedKey] = 0;
            Assert.That(M05LandingFacts.TryRead(read, out M05LandingReport? noSail), Is.True);
            Assert.That(noSail, Is.Null);
            values[M04DepartureFacts.SailedKey] = 1;
            values[FogListeningFacts.SucceededKey] = 0;
            Assert.That(M05LandingFacts.TryRead(read, out M05LandingReport? failedFog), Is.True);
            Assert.That(failedFog, Is.Null);
        }

        [Test]
        public void InvalidNonzeroLandingMarkerCannotOpenAnUnfinishableArrivalCheck()
        {
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            var landing = new M05LandingReport(3, 8, true, true);
            foreach (int corruptMarker in new[] { -1, 2, int.MaxValue })
            {
                var root = new GameObject("M05 corrupt landing marker");
                int writes = 0;
                values[M05LandingFacts.ConfirmedKey] = corruptMarker;
                try
                {
                    Assert.That(M05LandingFacts.TryRead(read,
                        out M05LandingReport? saved), Is.False);
                    Assert.That(saved, Is.Null);
                    Assert.That(M05OuterHarborFlowHost.TryOpen(
                        root.transform, landing, read,
                        (key, value) => { writes++; values[key] = value; },
                        _ => { }, out var host), Is.False);
                    Assert.That(host, Is.Null);
                    Assert.That(root.transform.childCount, Is.Zero);
                    Assert.That(writes, Is.Zero);
                    Assert.That(values[M05LandingFacts.ConfirmedKey],
                        Is.EqualTo(corruptMarker));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        [Test]
        public void InterruptedWriteDoesNotPublishPartialLandingAndCanRetry()
        {
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            var report = new M05LandingReport(3, 9, true, true);

            Assert.Throws<InvalidOperationException>(() =>
                M05LandingFacts.TryRecord(report, read, (key, value) =>
                {
                    if (key == M05LandingFacts.ReadyBerthsKey)
                        throw new InvalidOperationException("Interrupted landing save");
                    write(key, value);
                }));
            Assert.That(M05LandingFacts.TryRead(read, out M05LandingReport? partial), Is.True);
            Assert.That(partial, Is.Null);
            Assert.That(M05LandingFacts.TryRecord(report, read, write), Is.True);
            Assert.That(M05LandingFacts.TryRead(read, out M05LandingReport? confirmed), Is.True);
            Assert.That(confirmed.Value.ReadyRescueBerths, Is.EqualTo(9));
        }


        [Test]
        public void TornLandingLocksFirstReportAndReusesTheReservedArrivalSequence()
        {
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            var first = new M05LandingReport(3, 9, true, true);
            var competing = new M05LandingReport(2, 8, true, true);

            // Crash after publishing only the first intent.
            Assert.Throws<InvalidOperationException>(() =>
                M05LandingFacts.TryRecord(first, read, (key, value) =>
                {
                    write(key, value);
                    if (key == M05LandingFacts.PendingSignatureKey)
                        throw new InvalidOperationException("Makinom interrupted at intent");
                }));
            Assert.That(M05LandingFacts.TryRead(read, out M05LandingReport? partial),
                Is.True);
            Assert.That(partial, Is.Null);
            Assert.That(M05LandingFacts.CanRecord(competing, read), Is.False);
            Assert.That(M05LandingFacts.TryRecord(competing, read, write), Is.False);
            Assert.That(M05LandingFacts.TryRecord(first, read, write), Is.True);
            Assert.That(read(M05LandingFacts.ArrivalSequenceKey), Is.EqualTo(1));

            // The next replay resets this visit but keeps the monotonic identity.
            Assert.That(M05LandingFacts.TryResetForNewVisit(write), Is.True);
            Assert.That(read(M05LandingFacts.ArrivalSequenceKey), Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() =>
                M05LandingFacts.TryRecord(first, read, (key, value) =>
                {
                    write(key, value);
                    if (key == M05LandingFacts.ArrivalSequenceKey)
                        throw new InvalidOperationException("Makinom stopped before confirmation");
                }));
            Assert.That(read(M05LandingFacts.ArrivalSequenceKey), Is.EqualTo(2));
            Assert.That(read(M05LandingFacts.ConfirmedKey), Is.Zero);
            Assert.That(read(M05LandingFacts.PendingArrivalSequenceKey),
                Is.EqualTo(2));
            Assert.That(M05LandingFacts.TryRecord(competing, read, write), Is.False);
            Assert.That(M05LandingFacts.TryRecord(first, read, write), Is.True);
            Assert.That(read(M05LandingFacts.ArrivalSequenceKey), Is.EqualTo(2),
                "Retry must not advance the persistent arrival identity twice.");

            // A different but otherwise valid berth count cannot replace an
            // immutable, signed landing after the completion marker.
            values[M05LandingFacts.ReadyBerthsKey] = 8;
            Assert.That(M05LandingFacts.TryRead(read, out M05LandingReport? altered),
                Is.True);
            Assert.That(altered, Is.Null);
            values[M05LandingFacts.ReadyBerthsKey] = 9;
            Assert.That(M05LandingFacts.TryRead(read, out M05LandingReport? saved),
                Is.True);
            Assert.That(saved.HasValue, Is.True);

            // Previously confirmed unsigned saves remain readable.
            values[M05LandingFacts.PendingSignatureKey] = 0;
            values[M05LandingFacts.PendingArrivalSequenceKey] = 0;
            Assert.That(M05LandingFacts.TryRead(read, out M05LandingReport? legacy),
                Is.True);
            Assert.That(legacy.HasValue, Is.True);
        }

        [Test]
        public void NextVoyageClearsArrivalAndItsCapacityBeforeReusingOldData()
        {
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            Assert.That(M05LandingFacts.TryRecord(
                new M05LandingReport(4, 12, true, true), read, write), Is.True);
            Assert.That(ReturnOpeningContinuation.TryResetPreviousOutboundFacts(write), Is.True);
            Assert.That(values[M05LandingFacts.ConfirmedKey], Is.Zero);
            Assert.That(values[M05LandingFacts.CrewArrivedKey], Is.Zero);
            Assert.That(values[M05LandingFacts.ReadyBerthsKey], Is.Zero);
            Assert.That(values[M05LandingFacts.TenderArrivedKey], Is.Zero);
            Assert.That(values[M05LandingFacts.ReceiverMetKey], Is.Zero);
            Assert.That(M05LandingFacts.TryRead(read, out M05LandingReport? reset), Is.True);
            Assert.That(reset, Is.Null);
        }

        [Test]
        public void MissingSaveAuthorityOrDelegatesNeverInventLanding()
        {
            var report = new M05LandingReport(1, 0, false, false);
            Assert.That(M05LandingFacts.TryRecord(report, null, (_, __) => { }), Is.False);
            Assert.That(M05LandingFacts.TryRecord(report, _ => 0, null), Is.False);
            Assert.That(M05LandingFacts.TryRead(null, out M05LandingReport? read), Is.False);
            Assert.That(read, Is.Null);
            Assert.That(M05LandingFacts.TryResetForNewVisit(null), Is.False);
        }
    }
}
