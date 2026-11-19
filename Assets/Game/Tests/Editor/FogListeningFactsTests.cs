using System;
using System.Collections.Generic;
using GamingIsLove.Makinom;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class FogListeningFactsTests
    {
        private static FogListeningSession CompletedSession(bool succeed)
        {
            var cues = new[]
            {
                new FogListeningCue(FogBearing.Ahead, FogCueKind.ReefSurf, 50),
                new FogListeningCue(FogBearing.Starboard, FogCueKind.SignalResponse, 80, requiresReceiverLamp: true)
            };
            var session = new FogListeningSession(cues, maxListenFocus: 2, maxHullIntegrity: 1);
            if (succeed)
            {
                Assert.That(session.TryFocusListen(), Is.True);
                Assert.That(session.ApplyHelmAction(FogHelmAction.TurnPort), Is.EqualTo(FogResolution.HazardAvoided));
                Assert.That(session.ApplyHelmAction(FogHelmAction.AnswerSignal), Is.EqualTo(FogResolution.SignalMatched));
            }
            else
            {
                Assert.That(session.ApplyHelmAction(FogHelmAction.HoldCourse), Is.EqualTo(FogResolution.HullScraped));
            }
            Assert.That(session.IsComplete, Is.True);
            return session;
        }

        [Test]
        public void NullOrIncompleteSessionIsRejectedWithoutWritingVariables()
        {
            var store = new Dictionary<string, int>();
            Action<string, int> setInt = (key, value) => store[key] = value;

            Assert.Throws<ArgumentNullException>(() => FogListeningFacts.TryRecord(null, setInt));

            var inProgress = new FogListeningSession(
                new[] { new FogListeningCue(FogBearing.Ahead, FogCueKind.ClearWater, 70) },
                maxListenFocus: 1,
                maxHullIntegrity: 2);
            Assert.Throws<InvalidOperationException>(() => FogListeningFacts.TryRecord(inProgress, setInt));
            Assert.That(store, Is.Empty);
        }

        [Test]
        public void StorageDelegateGuardsReturnFalseWithoutThrowing()
        {
            FogListeningSession completed = CompletedSession(succeed: true);
            Assert.That(FogListeningFacts.TryResetForNewLeg(null), Is.False);
            Assert.That(FogListeningFacts.TryRecord(completed, null), Is.False);
            Assert.That(FogListeningFacts.TryRead(null, out FogListeningRecord? record), Is.False);
            Assert.That(record, Is.Null);

            if (!Maki.Initialized || Maki.Game?.Variables == null)
            {
                Assert.That(FogListeningFacts.TryResetForNewLeg(), Is.False);
                Assert.That(FogListeningFacts.TryRecord(completed), Is.False);
                Assert.That(FogListeningFacts.TryRead(out FogListeningRecord? globalRecord), Is.False);
                Assert.That(globalRecord, Is.Null);
            }
        }

        [Test]
        public void CompletedSuccessAndFailureOutcomesPersistReadBackAndReset()
        {
            var store = new Dictionary<string, int>();
            Action<string, int> setInt = (key, value) => store[key] = value;
            Func<string, int> getInt = key => store.TryGetValue(key, out int value) ? value : 0;

            Assert.That(FogListeningFacts.TryRecord(CompletedSession(succeed: true), setInt), Is.True);
            Assert.That(FogListeningFacts.TryRead(getInt, out FogListeningRecord? success), Is.True);
            Assert.That(success.HasValue, Is.True);
            Assert.That(success.Value.Succeeded, Is.True);
            Assert.That(success.Value.HullIntegrity, Is.EqualTo(1));
            Assert.That(success.Value.AvoidedHazards, Is.EqualTo(1));
            Assert.That(success.Value.MatchedSignals, Is.EqualTo(1));
            Assert.That(success.Value.RemainingFocus, Is.EqualTo(1));

            Assert.That(FogListeningFacts.TryRecord(CompletedSession(succeed: false), setInt), Is.True);
            Assert.That(FogListeningFacts.TryRead(getInt, out FogListeningRecord? failure), Is.True);
            Assert.That(failure.HasValue, Is.True);
            Assert.That(failure.Value.Succeeded, Is.False);
            Assert.That(failure.Value.HullIntegrity, Is.Zero);

            Assert.That(FogListeningFacts.TryResetForNewLeg(setInt), Is.True);
            Assert.That(FogListeningFacts.TryRead(getInt, out FogListeningRecord? afterReset), Is.True);
            Assert.That(afterReset, Is.Null);
        }

        [Test]
        public void InterruptedWriteInvalidatesPriorCompletionUntilCommitCompletes()
        {
            var store = new Dictionary<string, int>();
            Action<string, int> setInt = (key, value) => store[key] = value;
            Func<string, int> getInt = key => store.TryGetValue(key, out int value) ? value : 0;

            Assert.That(FogListeningFacts.TryRecord(CompletedSession(succeed: true), setInt), Is.True);

            Action<string, int> failingWrite = (key, value) =>
            {
                if (key == FogListeningFacts.MatchedSignalsKey)
                    throw new InvalidOperationException("Simulated write interruption.");
                store[key] = value;
            };

            Assert.Throws<InvalidOperationException>(() =>
                FogListeningFacts.TryRecord(CompletedSession(succeed: false), failingWrite));
            Assert.That(store[FogListeningFacts.CompletedKey], Is.Zero);
            Assert.That(FogListeningFacts.TryRead(getInt, out FogListeningRecord? torn), Is.True);
            Assert.That(torn, Is.Null);

            Assert.That(FogListeningFacts.TryRecord(CompletedSession(succeed: false), setInt), Is.True);
            Assert.That(FogListeningFacts.TryRead(getInt, out FogListeningRecord? recovered), Is.True);
            Assert.That(recovered.HasValue, Is.True);
            Assert.That(recovered.Value.Succeeded, Is.False);
        }

        [Test]
        public void CorruptedOrContradictorySavedVariablesAreRejected()
        {
            var baseStore = new Dictionary<string, int>();
            FogListeningFacts.TryRecord(CompletedSession(succeed: true), (key, value) => baseStore[key] = value);

            void AssertRejected(Action<Dictionary<string, int>> corrupt)
            {
                var copy = new Dictionary<string, int>(baseStore);
                corrupt(copy);
                Func<string, int> getInt = key => copy.TryGetValue(key, out int value) ? value : 0;
                Assert.That(FogListeningFacts.TryRead(getInt, out FogListeningRecord? record), Is.True);
                Assert.That(record, Is.Null);
            }

            AssertRejected(s => s[FogListeningFacts.CompletedKey] = 0);
            AssertRejected(s => s[FogListeningFacts.SucceededKey] = 2);
            AssertRejected(s => s[FogListeningFacts.HullIntegrityKey] = 0);
            AssertRejected(s => s[FogListeningFacts.HullIntegrityKey] = -1);
            AssertRejected(s => s[FogListeningFacts.AvoidedHazardsKey] = -1);
            AssertRejected(s => s[FogListeningFacts.MatchedSignalsKey] = -1);
            AssertRejected(s => s[FogListeningFacts.RemainingFocusKey] = -1);
            AssertRejected(s =>
            {
                s[FogListeningFacts.SucceededKey] = 0;
                s[FogListeningFacts.HullIntegrityKey] = 1;
            });
        }
    }
}
