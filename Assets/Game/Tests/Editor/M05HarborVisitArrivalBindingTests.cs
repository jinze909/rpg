using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M05HarborVisitArrivalBindingTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedVerifiedLanding(Dictionary<string, int> values)
        {
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            var manifest = new M04DepartureManifest(
                M03VoyageChoice.QiQueMerchant,
                "Verified harbor hull", "A Yuan", 4, "Shallow tender",
                true, 16, 8, "Named receiver", false, false);
            Assert.That(manifest.ConfirmDeparture(), Is.True);
            Assert.That(M04DepartureFacts.TryRecord(manifest, write), Is.True);
            Assert.That(M04DepartureFacts.TryMarkSailed(read, write), Is.True);

            var fog = new FogListeningSession(
                new[] { new FogListeningCue(
                    FogBearing.Ahead, FogCueKind.ClearWater, 80) },
                maxListenFocus: 1, maxHullIntegrity: 2);
            fog.ApplyHelmAction(FogHelmAction.HoldCourse);
            Assert.That(fog.IsComplete && fog.Succeeded, Is.True);
            Assert.That(FogListeningFacts.TryRecord(fog, write), Is.True);
            Assert.That(M05LandingFacts.TryRecord(
                new M05LandingReport(3, 10, true, true), read, write), Is.True);
        }

        [Test]
        public void PartialVoyageResetCannotClaimOldHarborVisitsOnIdenticalNewLanding()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            SeedVerifiedLanding(values);
            Assert.That(M05HarborVisitFacts.TryRecord(
                M05HarborBeat.RepairStall, read, write), Is.True);
            int firstArrival = read(M05LandingFacts.ArrivalSequenceKey);
            Assert.That(M05HarborVisitFacts.TryRead(read, out int firstMask), Is.True);
            Assert.That(firstMask, Is.EqualTo(1 << (int)M05HarborBeat.RepairStall));

            // A crash after clearing the landing but before clearing its
            // conversations must not make them count as new observations.
            Assert.That(M05LandingFacts.TryResetForNewVisit(write), Is.True);
            Assert.That(M04DepartureFacts.TryResetForNewVisit(write), Is.True);
            Assert.That(FogListeningFacts.TryResetForNewLeg(write), Is.True);
            SeedVerifiedLanding(values);
            int secondArrival = read(M05LandingFacts.ArrivalSequenceKey);
            Assert.That(secondArrival, Is.GreaterThan(firstArrival));
            Assert.That(read(M05HarborVisitFacts.VisitedMaskKey), Is.Not.Zero);
            Assert.That(M05HarborVisitFacts.TryRead(read, out int staleMask), Is.True);
            Assert.That(staleMask, Is.Zero);

            // Real interaction rebinds this voyage without importing
            // the repair-stall dialogue that belonged to the old visit.
            Assert.That(M05HarborVisitFacts.TryRecord(
                M05HarborBeat.FisherRumor, read, write), Is.True);
            Assert.That(M05HarborVisitFacts.TryRead(read, out int newMask), Is.True);
            Assert.That(newMask, Is.EqualTo(1 << (int)M05HarborBeat.FisherRumor));
            Assert.That(read(M05HarborVisitFacts.VisitedArrivalIdentityKey),
                Is.EqualTo(secondArrival + 1));
        }

        [Test]
        public void UnsignedLegacyVisitsNeverBecomeCurrentAndTornRebindIsRetryable()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            SeedVerifiedLanding(values);
            values[M05HarborVisitFacts.VisitedMaskKey] =
                M05HarborVisitFacts.AllBeatsMask;
            Assert.That(read(M05HarborVisitFacts.VisitedArrivalIdentityKey), Is.Zero);
            Assert.That(M05HarborVisitFacts.TryRead(read, out int legacy), Is.True);
            Assert.That(legacy, Is.Zero);

            // First failure: the old mask has been cleared, but its new
            // landing identity has not been saved.
            Assert.Throws<InvalidOperationException>(() =>
                M05HarborVisitFacts.TryRecord(
                    M05HarborBeat.RepairStall, read, (key, value) =>
                    {
                        write(key, value);
                        if (key == M05HarborVisitFacts.VisitedMaskKey && value == 0)
                            throw new InvalidOperationException("Torn mask clear");
                    }));
            Assert.That(read(M05HarborVisitFacts.VisitedMaskKey), Is.Zero);
            Assert.That(M05HarborVisitFacts.TryRead(read, out int afterClear), Is.True);
            Assert.That(afterClear, Is.Zero);

            // Second failure: binding commits, but the actual new
            // observation has not been recorded.
            Assert.Throws<InvalidOperationException>(() =>
                M05HarborVisitFacts.TryRecord(
                    M05HarborBeat.RepairStall, read, (key, value) =>
                    {
                        write(key, value);
                        if (key == M05HarborVisitFacts.VisitedArrivalIdentityKey)
                            throw new InvalidOperationException("Torn identity write");
                    }));
            Assert.That(M05HarborVisitFacts.TryRead(read, out int afterBind), Is.True);
            Assert.That(afterBind, Is.Zero);

            Assert.That(M05HarborVisitFacts.TryRecord(
                M05HarborBeat.RepairStall, read, write), Is.True);
            Assert.That(M05HarborVisitFacts.TryRead(read, out int witnessed), Is.True);
            Assert.That(witnessed, Is.EqualTo(1 << (int)M05HarborBeat.RepairStall));

            int writes = 0;
            Assert.That(M05HarborVisitFacts.TryRecord(
                M05HarborBeat.RepairStall, read,
                (key, value) => { writes++; write(key, value); }), Is.True);
            Assert.That(writes, Is.Zero, "A completed dialogue retry is idempotent.");
        }
    }
}
