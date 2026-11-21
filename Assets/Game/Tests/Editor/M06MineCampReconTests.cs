using System;
using System.Collections.Generic;
using GamingIsLove.Makinom;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M06MineCampReconTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedVerifiedM05Arrival(
            Dictionary<string, int> values,
            M03VoyageChoice route = M03VoyageChoice.QiQueMerchant)
        {
            Action<string, int> write = (key, value) => values[key] = value;
            Func<string, int> read = Reader(values);

            bool officialStamp = route == M03VoyageChoice.HanQiaoOfficial ||
                route == M03VoyageChoice.MixedWithOfficial;
            bool escortFlag = route == M03VoyageChoice.LuJunEscort;

            var manifest = new M04DepartureManifest(
                route,
                "Confirmed hull",
                "A Yuan",
                4,
                "Shallow tender",
                true,
                16,
                8,
                "Named receiver",
                officialStamp,
                escortFlag);
            Assert.That(manifest.ConfirmDeparture(), Is.True);
            Assert.That(M04DepartureFacts.TryRecord(manifest, write), Is.True);
            Assert.That(M04DepartureFacts.TryMarkSailed(read, write), Is.True);

            var fog = new FogListeningSession(
                new[] { new FogListeningCue(FogBearing.Ahead, FogCueKind.ClearWater, 80) },
                maxListenFocus: 1,
                maxHullIntegrity: 2);
            fog.ApplyHelmAction(FogHelmAction.HoldCourse);
            Assert.That(fog.IsComplete && fog.Succeeded, Is.True);
            Assert.That(FogListeningFacts.TryRecord(fog, write), Is.True);

            var landing = new M05LandingReport(3, 10, true, true);
            Assert.That(M05LandingFacts.TryRecord(landing, read, write), Is.True);
        }

        [Test]
        public void ReconCannotRecordWithoutVerifiedM05Landing()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;

            Assert.That(M06MineCampReconSession.TryLoad(read, out var session), Is.False);
            Assert.That(session, Is.Null);
            Assert.That(M06MineCampReconFacts.TryRecord(
                M06MineCampLead.EscortSchedule, read, write), Is.False);
            Assert.That(values, Is.Empty);
        }

        [Test]
        public void EachOfFourLeadsPersistsIdempotentlyAcrossReload()
        {
            var values = new Dictionary<string, int>();
            SeedVerifiedM05Arrival(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;

            Assert.That(M06MineCampReconSession.TryLoad(read, out var session), Is.True);
            Assert.That(session.ObservedCount, Is.Zero);
            Assert.That(session.AllLeadsObserved, Is.False);

            Assert.That(session.TryInspectLead(M06MineCampLead.EscortSchedule, read, write), Is.True);
            Assert.That(session.TryInspectLead(M06MineCampLead.EscortSchedule, read, write), Is.False,
                "Already inspected lead must not record twice.");
            Assert.That(session.TryInspectLead(M06MineCampLead.MineTunnelAndPump, read, write), Is.True);
            Assert.That(session.ObservedCount, Is.EqualTo(2));

            // Reloading from Makinom preserves only the two actually inspected leads.
            Assert.That(M06MineCampReconSession.TryLoad(read, out var reloaded), Is.True);
            Assert.That(reloaded.IsLeadObserved(M06MineCampLead.EscortSchedule), Is.True);
            Assert.That(reloaded.IsLeadObserved(M06MineCampLead.MineTunnelAndPump), Is.True);
            Assert.That(reloaded.IsLeadObserved(M06MineCampLead.GuardPatrol), Is.False);
            Assert.That(reloaded.IsLeadObserved(M06MineCampLead.SupplyLedger), Is.False);
            Assert.That(reloaded.AllLeadsObserved, Is.False);

            Assert.That(reloaded.TryInspectLead(M06MineCampLead.GuardPatrol, read, write), Is.True);
            Assert.That(reloaded.TryInspectLead(M06MineCampLead.SupplyLedger, read, write), Is.True);
            Assert.That(reloaded.ObservedCount, Is.EqualTo(4));
            Assert.That(reloaded.AllLeadsObserved, Is.True);
            Assert.That(values[M06MineCampReconFacts.ObservedMaskKey],
                Is.EqualTo(M06MineCampReconFacts.AllLeadsMask));
        }

        [Test]
        public void FailedSaveWriteAllowsCleanRetryWithoutMarkingLeadObserved()
        {
            var values = new Dictionary<string, int>();
            SeedVerifiedM05Arrival(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;

            Assert.That(M06MineCampReconSession.TryLoad(read, out var session), Is.True);
            int attempts = 0;
            Action<string, int> flakyWrite = (key, value) =>
            {
                if (key == M06MineCampReconFacts.ObservedMaskKey && attempts++ == 0)
                    throw new InvalidOperationException("Interrupted recon save");
                write(key, value);
            };

            Assert.Throws<InvalidOperationException>(() =>
                session.TryInspectLead(M06MineCampLead.SupplyLedger, read, flakyWrite));
            Assert.That(session.IsLeadObserved(M06MineCampLead.SupplyLedger), Is.False);
            Assert.That(read(M06MineCampReconFacts.ObservedMaskKey), Is.Zero);

            Assert.That(session.TryInspectLead(M06MineCampLead.SupplyLedger, read, flakyWrite), Is.True);
            Assert.That(session.IsLeadObserved(M06MineCampLead.SupplyLedger), Is.True);
            Assert.That(attempts, Is.EqualTo(2));
        }

        [Test]
        public void CorruptMaskOrChangedM03RouteInvalidatesObservedRecon()
        {
            var values = new Dictionary<string, int>();
            SeedVerifiedM05Arrival(values, M03VoyageChoice.QiQueMerchant);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;

            Assert.That(M06MineCampReconFacts.TryRecord(
                M06MineCampLead.GuardPatrol, read, write), Is.True);

            foreach (int corrupt in new[] { -1, 16, 255 })
            {
                values[M06MineCampReconFacts.ObservedMaskKey] = corrupt;
                Assert.That(M06MineCampReconFacts.TryRead(read, out _), Is.False);
                Assert.That(M06MineCampReconSession.TryLoad(read, out _), Is.False);
                Assert.That(M06MineCampReconFacts.TryRecord(
                    M06MineCampLead.EscortSchedule, read, write), Is.False);
            }

            Assert.That(M06MineCampReconFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(M06MineCampReconFacts.TryRecord(
                M06MineCampLead.GuardPatrol, read, write), Is.True);

            // Changing M03 route invalidates upstream M05 landing and hides stale M06 recon.
            var encounter = new M03LuJunEncounter(M02BridgeEvidence.None, true);
            encounter.Begin();
            encounter.ObserveRollCall();
            encounter.Ask(M03LuJunOpening.NeedAShip);
            encounter.ChooseVoyage(M03VoyageChoice.HanQiaoOfficial);
            Assert.That(M03LuJunFacts.TryRecord(encounter, write), Is.True);

            Assert.That(M06MineCampReconFacts.TryRead(read, out int staleMask), Is.True);
            Assert.That(staleMask, Is.Zero);
            Assert.That(M06MineCampReconSession.TryLoad(read, out _), Is.False);
        }

        [Test]
        public void OldReconSessionCannotCreditSameNumbersOnASecondConfirmedLanding()
        {
            var values = new Dictionary<string, int>();
            SeedVerifiedM05Arrival(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;

            Assert.That(M06MineCampReconSession.TryLoad(read, out var oldSession), Is.True);
            int firstArrivalSequence = read(M05LandingFacts.ArrivalSequenceKey);
            Assert.That(oldSession.ObservedCount, Is.Zero);
            Assert.That(ReturnOpeningContinuation.TryResetPreviousOutboundFacts(write), Is.True);
            Assert.That(oldSession.TryInspectLead(
                M06MineCampLead.GuardPatrol, read, write), Is.False);
            SeedVerifiedM05Arrival(values);
            Assert.That(read(M05LandingFacts.ArrivalSequenceKey),
                Is.GreaterThan(firstArrivalSequence));
            Assert.That(oldSession.TryInspectLead(
                M06MineCampLead.GuardPatrol, read, write), Is.False);
            Assert.That(read(M06MineCampReconFacts.ObservedMaskKey), Is.Zero);
            Assert.That(oldSession.ObservedCount, Is.Zero);

            Assert.That(M06MineCampReconSession.TryLoad(read, out var newSession), Is.True);
            Assert.That(newSession.TryInspectLead(
                M06MineCampLead.GuardPatrol, read, write), Is.True);
            Assert.That(read(M06MineCampReconFacts.ObservedMaskKey), Is.EqualTo(2));
            Assert.That(oldSession.TryInspectLead(
                M06MineCampLead.SupplyLedger, read, write), Is.False);
            Assert.That(read(M06MineCampReconFacts.ObservedMaskKey), Is.EqualTo(2));
        }

        [Test]
        public void InterruptedVoyageResetCannotTransferOldReconIntoIdenticalNewLanding()
        {
            var values = new Dictionary<string, int>();
            SeedVerifiedM05Arrival(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;

            Assert.That(M06MineCampReconFacts.TryRecord(
                M06MineCampLead.EscortSchedule, read, write), Is.True);
            Assert.That(M06MineCampReconFacts.TryRecord(
                M06MineCampLead.GuardPatrol, read, write), Is.True);
            int firstSequence = read(M05LandingFacts.ArrivalSequenceKey);
            Assert.That(read(M06MineCampReconFacts.ObservedArrivalIdentityKey),
                Is.EqualTo(firstSequence + 1));
            Assert.That(M06MineCampReconFacts.TryRead(read, out int original), Is.True);
            Assert.That(original, Is.EqualTo(3));

            // A real reset was interrupted before the M06 recon fields were
            // reached. The previous observed mask remains in the save.
            Assert.That(M05LandingFacts.TryResetForNewVisit(write), Is.True);
            Assert.That(M04DepartureFacts.TryResetForNewVisit(write), Is.True);
            Assert.That(FogListeningFacts.TryResetForNewLeg(write), Is.True);
            SeedVerifiedM05Arrival(values);
            Assert.That(read(M05LandingFacts.ArrivalSequenceKey),
                Is.GreaterThan(firstSequence));
            Assert.That(read(M06MineCampReconFacts.ObservedMaskKey), Is.EqualTo(3));
            Assert.That(M06MineCampReconFacts.TryRead(read, out int stale), Is.True);
            Assert.That(stale, Is.Zero);
            Assert.That(M06MineCampReconSession.TryLoad(read, out var clean), Is.True);
            Assert.That(clean.ObservedCount, Is.Zero);

            Assert.That(clean.TryInspectLead(M06MineCampLead.SupplyLedger,
                read, write), Is.True);
            Assert.That(clean.ObservedMask, Is.EqualTo(4));
            Assert.That(read(M06MineCampReconFacts.ObservedMaskKey), Is.EqualTo(4));
            Assert.That(read(M06MineCampReconFacts.ObservedArrivalIdentityKey),
                Is.EqualTo(read(M05LandingFacts.ArrivalSequenceKey) + 1));
            Assert.That(clean.IsLeadObserved(M06MineCampLead.EscortSchedule), Is.False);
            Assert.That(clean.IsLeadObserved(M06MineCampLead.GuardPatrol), Is.False);
        }

        [Test]
        public void InterruptedReconRebindingRemainsHiddenAndCanRetryRealInspection()
        {
            var values = new Dictionary<string, int>();
            SeedVerifiedM05Arrival(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            Assert.That(M06MineCampReconFacts.TryRecord(
                M06MineCampLead.EscortSchedule, read, write), Is.True);

            Assert.That(M05LandingFacts.TryResetForNewVisit(write), Is.True);
            Assert.That(M04DepartureFacts.TryResetForNewVisit(write), Is.True);
            Assert.That(FogListeningFacts.TryResetForNewLeg(write), Is.True);
            SeedVerifiedM05Arrival(values);
            Assert.That(M06MineCampReconSession.TryLoad(read, out var session), Is.True);
            Assert.That(session.ObservedCount, Is.Zero);

            Assert.Throws<InvalidOperationException>(() =>
                session.TryInspectLead(M06MineCampLead.MineTunnelAndPump,
                    read, (key, value) =>
                    {
                        write(key, value);
                        if (key == M06MineCampReconFacts.ObservedMaskKey && value == 0)
                            throw new InvalidOperationException("Clear acknowledged late");
                    }));
            Assert.That(read(M06MineCampReconFacts.ObservedMaskKey), Is.Zero);
            Assert.That(M06MineCampReconFacts.TryRead(read, out int afterClear), Is.True);
            Assert.That(afterClear, Is.Zero);
            Assert.That(session.ObservedCount, Is.Zero);

            Assert.Throws<InvalidOperationException>(() =>
                session.TryInspectLead(M06MineCampLead.MineTunnelAndPump,
                    read, (key, value) =>
                    {
                        write(key, value);
                        if (key == M06MineCampReconFacts.ObservedArrivalIdentityKey)
                            throw new InvalidOperationException("Binding acknowledged late");
                    }));
            Assert.That(read(M06MineCampReconFacts.ObservedMaskKey), Is.Zero);
            Assert.That(M06MineCampReconFacts.TryRead(read, out int afterBinding), Is.True);
            Assert.That(afterBinding, Is.Zero);

            Assert.That(session.TryInspectLead(M06MineCampLead.MineTunnelAndPump,
                read, write), Is.True);
            Assert.That(session.ObservedMask, Is.EqualTo(8));
            Assert.That(session.AllLeadsObserved, Is.False);
            Assert.That(M06MineCampReconFacts.TryRead(read, out int saved), Is.True);
            Assert.That(saved, Is.EqualTo(8));
        }

        [Test]
        public void UnsignedOldReconBitsRequireFreshInspectionWithoutInventingOtherLeads()
        {
            var values = new Dictionary<string, int>();
            SeedVerifiedM05Arrival(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;

            values[M06MineCampReconFacts.ObservedMaskKey] = 15;
            Assert.That(read(M06MineCampReconFacts.ObservedArrivalIdentityKey), Is.Zero);
            Assert.That(M06MineCampReconFacts.TryRead(read, out int unsigned), Is.True);
            Assert.That(unsigned, Is.Zero);
            Assert.That(M06MineCampReconFacts.TryRecord(
                M06MineCampLead.GuardPatrol, read, write), Is.True);
            Assert.That(M06MineCampReconFacts.TryRead(read, out int witnessed), Is.True);
            Assert.That(witnessed, Is.EqualTo(2));
            values[M06MineCampReconFacts.ObservedArrivalIdentityKey] = -1;
            Assert.That(M06MineCampReconFacts.TryRead(read, out _), Is.False);
            Assert.That(M06MineCampReconFacts.TryRecord(
                M06MineCampLead.EscortSchedule, read, write), Is.False);
            Assert.That(M06MineCampReconFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(read(M06MineCampReconFacts.ObservedArrivalIdentityKey), Is.Zero);
        }

        [Test]
        public void BilingualLeadNotesCoverAllFourMineCampLeads()
        {
            foreach (M06MineCampLead lead in new[]
            {
                M06MineCampLead.EscortSchedule,
                M06MineCampLead.GuardPatrol,
                M06MineCampLead.SupplyLedger,
                M06MineCampLead.MineTunnelAndPump
            })
            {
                Assert.That(M06MineCampReconSession.FormatLeadTitle(lead, zh: true), Is.Not.Empty);
                Assert.That(M06MineCampReconSession.FormatLeadTitle(lead, zh: false), Is.Not.Empty);
                Assert.That(M06MineCampReconSession.FormatLeadFindings(lead, zh: true), Is.Not.Empty);
                Assert.That(M06MineCampReconSession.FormatLeadFindings(lead, zh: false), Is.Not.Empty);
            }
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                M06MineCampReconSession.FormatLeadTitle((M06MineCampLead)99, zh: true));
        }

        [Test]
        public void MissingSaveAuthorityOrNullDelegatesNeverFabricateReconFacts()
        {
            Assert.That(M06MineCampReconFacts.TryRead(null, out int mask), Is.False);
            Assert.That(mask, Is.Zero);
            Assert.That(M06MineCampReconFacts.TryRecord(
                M06MineCampLead.EscortSchedule, null, (_, __) => { }), Is.False);
            Assert.That(M06MineCampReconFacts.TryRecord(
                M06MineCampLead.EscortSchedule, _ => 0, null), Is.False);
            Assert.That(M06MineCampReconFacts.TryResetForNewVoyage(null), Is.False);

            if (!Maki.Initialized || Maki.Game?.Variables == null)
            {
                Assert.That(M06MineCampReconFacts.TryRead(out _), Is.False);
                Assert.That(M06MineCampReconFacts.TryRecord(M06MineCampLead.EscortSchedule), Is.False);
                Assert.That(M06MineCampReconFacts.TryResetForNewVoyage(), Is.False);
            }
        }
    }
}
