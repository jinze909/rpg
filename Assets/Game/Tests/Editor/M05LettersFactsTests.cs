using System;
using System.Collections.Generic;
using GamingIsLove.Makinom;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M05LettersFactsTests
    {
        [Test]
        public void AcceptanceIsEightCarriedLettersNotEightDeliveredLetters()
        {
            var save = new Dictionary<string, int>();
            Func<string, int> get = key => save.TryGetValue(key, out int v) ? v : 0;
            Action<string, int> set = (key, value) => save[key] = value;

            Assert.That(M05LettersFacts.TryRead(get, out M05LetterDecision? before), Is.True);
            Assert.That(before.HasValue, Is.False,
                "An unopened dialog must not be counted as a refusal.");
            Assert.That(M05LettersFacts.LettersCarried(before), Is.Zero);

            Assert.That(M05LettersFacts.TryRecord(M05LetterDecision.TakePacket, get, set),
                Is.True);
            Assert.That(M05LettersFacts.TryRead(get, out M05LetterDecision? after), Is.True);
            Assert.That(after, Is.EqualTo(M05LetterDecision.TakePacket));
            Assert.That(M05LettersFacts.LettersCarried(after),
                Is.EqualTo(M05LettersFacts.PacketLetterCount));
            Assert.That(M05LettersFacts.TryRecord(M05LetterDecision.TakePacket, get, set),
                Is.False, "Repeated Submit cannot count the packet twice.");
            Assert.That(M05LettersFacts.TryRecord(
                M05LetterDecision.LeaveForNextBoat, get, set), Is.False);
            Assert.That(save.Count, Is.EqualTo(1),
                "A packet decision must use one existing save authority key.");
        }

        [Test]
        public void RefusalLeavesPacketForNextBoatAndCanOnlyBeReconsideredOnNewVoyage()
        {
            var save = new Dictionary<string, int>();
            Func<string, int> get = key => save.TryGetValue(key, out int v) ? v : 0;
            Action<string, int> set = (key, value) => save[key] = value;

            Assert.That(M05LettersFacts.TryRecord(
                M05LetterDecision.LeaveForNextBoat, get, set), Is.True);
            Assert.That(M05LettersFacts.TryRead(get, out M05LetterDecision? recorded),
                Is.True);
            Assert.That(recorded, Is.EqualTo(M05LetterDecision.LeaveForNextBoat));
            Assert.That(M05LettersFacts.LettersCarried(recorded), Is.Zero);
            Assert.That(M05LettersFacts.TryRecord(
                M05LetterDecision.TakePacket, get, set), Is.False);

            Assert.That(M05LettersFacts.TryResetForNewVoyage(set), Is.True);
            Assert.That(M05LettersFacts.TryRead(get, out M05LetterDecision? reset), Is.True);
            Assert.That(reset, Is.Null);
            Assert.That(M05LettersFacts.TryRecord(
                M05LetterDecision.TakePacket, get, set), Is.True);
        }

        [Test]
        public void CorruptSavesAndFailedWritesCannotMakeUpAnAnswer()
        {
            var save = new Dictionary<string, int>();
            Func<string, int> get = key => save.TryGetValue(key, out int v) ? v : 0;
            Action<string, int> set = (key, value) => save[key] = value;
            foreach (int invalid in new[] { -1, 3, int.MaxValue })
            {
                save[M05LettersFacts.DecisionKey] = invalid;
                Assert.That(M05LettersFacts.TryRead(get, out M05LetterDecision? value), Is.True);
                Assert.That(value, Is.Null);
                Assert.That(M05LettersFacts.TryRecord(
                    M05LetterDecision.TakePacket, get, set), Is.False);
            }

            Assert.That(M05LettersFacts.TryResetForNewVoyage(set), Is.True);
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
        public void NextM03VoyageClearsOldOuterHarborLetterChoice()
        {
            var save = new Dictionary<string, int>();
            Func<string, int> get = key => save.TryGetValue(key, out int v) ? v : 0;
            Action<string, int> set = (key, value) => save[key] = value;
            Assert.That(M05LettersFacts.TryRecord(
                M05LetterDecision.TakePacket, get, set), Is.True);
            Assert.That(ReturnOpeningContinuation.TryResetPreviousOutboundFacts(set),
                Is.True);
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
