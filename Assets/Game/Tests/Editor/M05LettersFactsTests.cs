using System;
using System.Collections.Generic;
using GamingIsLove.Makinom;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M05LettersFactsTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> save)
        {
            return key => save.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedVerifiedLanding(Dictionary<string, int> save,
            M05LandingReport? landing = null)
        {
            Func<string, int> read = Reader(save);
            Action<string, int> write = (key, value) => save[key] = value;
            var manifest = new M04DepartureManifest(M03VoyageChoice.QiQueMerchant,
                "Verified civilian hull", "A Yuan", 4, "Shallow tender",
                true, 16, 8, "Named receiver", false, false);
            Assert.That(manifest.ConfirmDeparture(), Is.True);
            Assert.That(M04DepartureFacts.TryRecord(manifest, write), Is.True);
            Assert.That(M04DepartureFacts.TryMarkSailed(read, write), Is.True);

            var fog = new FogListeningSession(
                new[] { new FogListeningCue(FogBearing.Ahead,
                    FogCueKind.ClearWater, 80) },
                maxListenFocus: 1, maxHullIntegrity: 2);
            fog.ApplyHelmAction(FogHelmAction.HoldCourse);
            Assert.That(fog.IsComplete && fog.Succeeded, Is.True);
            Assert.That(FogListeningFacts.TryRecord(fog, write), Is.True);
            if (landing.HasValue)
                Assert.That(M05LandingFacts.TryRecord(landing.Value, read, write), Is.True);
        }

        [Test]
        public void AcceptanceIsEightCarriedLettersNotEightDeliveredLetters()
        {
            var save = new Dictionary<string, int>();
            SeedVerifiedLanding(save, new M05LandingReport(3, 8, true, true));
            Func<string, int> get = Reader(save);
            int letterWrites = 0;
            Action<string, int> set = (key, value) =>
            {
                save[key] = value;
                if (key == M05LettersFacts.DecisionKey ||
                    key == M05LettersFacts.DecisionArrivalIdentityKey)
                    letterWrites++;
            };

            Assert.That(M05LettersFacts.TryRead(get, out M05LetterDecision? before), Is.True);
            Assert.That(before.HasValue, Is.False,
                "An unopened dialog must not be counted as a refusal.");
            Assert.That(M05LettersFacts.LettersCarried(before), Is.Zero);

            Assert.That(M05LettersFacts.TryRecord(M05LetterDecision.TakePacket, get, set),
                Is.True);
            Assert.That(letterWrites, Is.EqualTo(2));
            Assert.That(M05LettersFacts.TryRead(get, out M05LetterDecision? after), Is.True);
            Assert.That(after, Is.EqualTo(M05LetterDecision.TakePacket));
            Assert.That(M05LettersFacts.LettersCarried(after),
                Is.EqualTo(M05LettersFacts.PacketLetterCount));
            Assert.That(M05LettersFacts.TryRecord(M05LetterDecision.TakePacket, get, set),
                Is.True, "A retry of the same saved choice is idempotent.");
            Assert.That(letterWrites, Is.EqualTo(2));
            Assert.That(M05LettersFacts.TryRecord(
                M05LetterDecision.LeaveForNextBoat, get, set), Is.False);
            Assert.That(letterWrites, Is.EqualTo(2));
        }

        [Test]
        public void RefusalCanBeReconsideredOnlyForANewVerifiedArrival()
        {
            var save = new Dictionary<string, int>();
            SeedVerifiedLanding(save, new M05LandingReport(3, 8, true, true));
            Func<string, int> get = Reader(save);
            Action<string, int> set = (key, value) => save[key] = value;

            Assert.That(M05LettersFacts.TryRecord(
                M05LetterDecision.LeaveForNextBoat, get, set), Is.True);
            Assert.That(M05LettersFacts.TryRead(get, out M05LetterDecision? recorded),
                Is.True);
            Assert.That(recorded, Is.EqualTo(M05LetterDecision.LeaveForNextBoat));
            Assert.That(M05LettersFacts.LettersCarried(recorded), Is.Zero);
            Assert.That(M05LettersFacts.TryRecord(
                M05LetterDecision.TakePacket, get, set), Is.False);

            int firstArrival = get(M05LandingFacts.ArrivalSequenceKey);
            Assert.That(M05LandingFacts.TryResetForNewVisit(set), Is.True);
            Assert.That(M04DepartureFacts.TryResetForNewVisit(set), Is.True);
            Assert.That(FogListeningFacts.TryResetForNewLeg(set), Is.True);
            SeedVerifiedLanding(save, new M05LandingReport(3, 8, true, true));
            int secondArrival = get(M05LandingFacts.ArrivalSequenceKey);
            Assert.That(secondArrival, Is.GreaterThan(firstArrival));
            Assert.That(M05LettersFacts.TryRead(get, out M05LetterDecision? stale), Is.True);
            Assert.That(stale, Is.Null,
                "The previous voyage's refusal is not an answer on this arrival.");

            Assert.That(M05LettersFacts.TryRecord(
                M05LetterDecision.TakePacket, get, set), Is.True);
            Assert.That(M05LettersFacts.TryRead(get, out M05LetterDecision? current), Is.True);
            Assert.That(current, Is.EqualTo(M05LetterDecision.TakePacket));
            Assert.That(get(M05LettersFacts.DecisionArrivalIdentityKey),
                Is.EqualTo(secondArrival + 1));
        }

        [Test]
        public void UnsignedLegacyChoiceIsNotReusedOnAVerifiedArrival()
        {
            var save = new Dictionary<string, int>();
            SeedVerifiedLanding(save, new M05LandingReport(3, 8, true, true));
            Func<string, int> get = Reader(save);
            Action<string, int> set = (key, value) => save[key] = value;
            save[M05LettersFacts.DecisionKey] = (int)M05LetterDecision.TakePacket;
            save[M05LettersFacts.DecisionArrivalIdentityKey] = 0;

            Assert.That(M05LettersFacts.TryRead(get, out M05LetterDecision? legacy), Is.True);
            Assert.That(legacy, Is.Null);
            Assert.That(M05LettersFacts.TryRecord(
                M05LetterDecision.LeaveForNextBoat, get, set), Is.True);
            Assert.That(M05LettersFacts.TryRead(get, out M05LetterDecision? current), Is.True);
            Assert.That(current, Is.EqualTo(M05LetterDecision.LeaveForNextBoat));
            Assert.That(get(M05LettersFacts.DecisionArrivalIdentityKey),
                Is.EqualTo(get(M05LandingFacts.ArrivalSequenceKey) + 1));
        }

        [Test]
        public void InterruptedRebindingNeverResurrectsThePreviousPacketChoice()
        {
            var save = new Dictionary<string, int>();
            SeedVerifiedLanding(save, new M05LandingReport(3, 8, true, true));
            Func<string, int> get = Reader(save);
            Action<string, int> write = (key, value) => save[key] = value;
            Assert.That(M05LettersFacts.TryRecord(
                M05LetterDecision.TakePacket, get, write), Is.True);
            int firstArrival = get(M05LandingFacts.ArrivalSequenceKey);

            Assert.That(M05LandingFacts.TryResetForNewVisit(write), Is.True);
            Assert.That(M04DepartureFacts.TryResetForNewVisit(write), Is.True);
            Assert.That(FogListeningFacts.TryResetForNewLeg(write), Is.True);
            SeedVerifiedLanding(save, new M05LandingReport(3, 8, true, true));
            int secondArrival = get(M05LandingFacts.ArrivalSequenceKey);
            Assert.That(secondArrival, Is.GreaterThan(firstArrival));
            Assert.That(M05LettersFacts.TryRead(get, out M05LetterDecision? hidden), Is.True);
            Assert.That(hidden, Is.Null);

            Assert.Throws<InvalidOperationException>(() =>
                M05LettersFacts.TryRecord(M05LetterDecision.LeaveForNextBoat, get,
                    (key, value) =>
                    {
                        write(key, value);
                        if (key == M05LettersFacts.DecisionKey && value == 0)
                            throw new InvalidOperationException("Interrupted after clearing old choice");
                    }));
            Assert.That(M05LettersFacts.TryRead(get, out M05LetterDecision? afterClear), Is.True);
            Assert.That(afterClear, Is.Null);

            Assert.Throws<InvalidOperationException>(() =>
                M05LettersFacts.TryRecord(M05LetterDecision.LeaveForNextBoat, get,
                    (key, value) =>
                    {
                        write(key, value);
                        if (key == M05LettersFacts.DecisionArrivalIdentityKey &&
                            value == secondArrival + 1)
                            throw new InvalidOperationException("Interrupted after new arrival binding");
                    }));
            Assert.That(M05LettersFacts.TryRead(get, out M05LetterDecision? afterBind), Is.True);
            Assert.That(afterBind, Is.Null);

            Assert.Throws<InvalidOperationException>(() =>
                M05LettersFacts.TryRecord(M05LetterDecision.LeaveForNextBoat, get,
                    (key, value) =>
                    {
                        write(key, value);
                        if (key == M05LettersFacts.DecisionKey &&
                            value == (int)M05LetterDecision.LeaveForNextBoat)
                            throw new InvalidOperationException("Interrupted after choice write");
                    }));
            Assert.That(M05LettersFacts.TryRead(get, out M05LetterDecision? committed), Is.True);
            Assert.That(committed, Is.EqualTo(M05LetterDecision.LeaveForNextBoat));
            Assert.That(M05LettersFacts.TryRecord(
                M05LetterDecision.LeaveForNextBoat, get, write), Is.True);
        }

        [Test]
        public void CorruptSavesAndFailedWritesCannotMakeUpAnAnswer()
        {
            var save = new Dictionary<string, int>();
            SeedVerifiedLanding(save, new M05LandingReport(3, 8, true, true));
            Func<string, int> get = Reader(save);
            Action<string, int> set = (key, value) => save[key] = value;
            foreach (int invalid in new[] { -1, 3, int.MaxValue })
            {
                save[M05LettersFacts.DecisionKey] = invalid;
                Assert.That(M05LettersFacts.TryRead(get, out M05LetterDecision? value), Is.False);
                Assert.That(value, Is.Null);
                Assert.That(M05LettersFacts.TryRecord(
                    M05LetterDecision.TakePacket, get, set), Is.False);
            }

            Assert.That(M05LettersFacts.TryResetForNewVoyage(set), Is.True);
            Assert.That(M05LettersFacts.TryRead(get, out M05LetterDecision? reset), Is.True);
            Assert.That(reset, Is.Null);
            Assert.Throws<InvalidOperationException>(() =>
                M05LettersFacts.TryRecord(M05LetterDecision.TakePacket, get,
                    (_, __) => throw new InvalidOperationException("Interrupted save")));
            Assert.That(M05LettersFacts.TryRead(get, out M05LetterDecision? interrupted),
                Is.True);
            Assert.That(interrupted, Is.Null);
            Assert.That(M05LettersFacts.TryRecord(
                M05LetterDecision.LeaveForNextBoat, get, set), Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                M05LettersFacts.TryRecord((M05LetterDecision)99, get, set));
        }

        [Test]
        public void NextM03VoyageClearsOldOuterHarborLetterChoiceAndBinding()
        {
            var save = new Dictionary<string, int>();
            SeedVerifiedLanding(save, new M05LandingReport(3, 8, true, true));
            Func<string, int> get = Reader(save);
            Action<string, int> set = (key, value) => save[key] = value;
            Assert.That(M05LettersFacts.TryRecord(
                M05LetterDecision.TakePacket, get, set), Is.True);
            Assert.That(get(M05LettersFacts.DecisionArrivalIdentityKey),
                Is.EqualTo(get(M05LandingFacts.ArrivalSequenceKey) + 1));

            Assert.That(ReturnOpeningContinuation.TryResetPreviousOutboundFacts(set),
                Is.True);
            Assert.That(get(M05LettersFacts.DecisionKey), Is.Zero);
            Assert.That(get(M05LettersFacts.DecisionArrivalIdentityKey), Is.Zero);
            Assert.That(M05LettersFacts.TryRead(get, out M05LetterDecision? outcome), Is.True);
            Assert.That(outcome, Is.Null);
            Assert.That(M05LettersFacts.LettersCarried(outcome), Is.Zero);
        }

        [Test]
        public void MissingMakinomOrNullDelegatesDoNotCreateLetterFacts()
        {
            Assert.That(M05LettersFacts.TryRead(null, out M05LetterDecision? missing), Is.False);
            Assert.That(missing, Is.Null);
            Assert.That(M05LettersFacts.TryRecord(
                M05LetterDecision.TakePacket, null, (_, __) => { }), Is.False);
            Assert.That(M05LettersFacts.TryRecord(
                M05LetterDecision.TakePacket, _ => 0, null), Is.False);
            Assert.That(M05LettersFacts.TryRecord(
                M05LetterDecision.TakePacket, _ => 0, (_, __) => { }), Is.False,
                "A Seran answer cannot be written before a verified landing.");
            Assert.That(M05LettersFacts.TryResetForNewVoyage(null), Is.False);
            if (!Maki.Initialized || Maki.Game?.Variables == null)
            {
                Assert.That(M05LettersFacts.TryRead(out M05LetterDecision? unbound), Is.False);
                Assert.That(unbound, Is.Null);
                Assert.That(M05LettersFacts.TryRecord(M05LetterDecision.TakePacket), Is.False);
                Assert.That(M05LettersFacts.TryResetForNewVoyage(), Is.False);
            }
        }
    }
}
