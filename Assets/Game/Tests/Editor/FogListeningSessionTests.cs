using System;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class FogListeningSessionTests
    {
        [Test]
        public void InvalidConstructorArgumentsAndCompletedSessionActionsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new FogListeningCue((FogBearing)99, FogCueKind.ClearWater, 50));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new FogListeningCue(FogBearing.Ahead, (FogCueKind)99, 50));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new FogListeningCue(FogBearing.Ahead, FogCueKind.ClearWater, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new FogListeningCue(FogBearing.Ahead, FogCueKind.ClearWater, 101));

            var singleCue = new[] { new FogListeningCue(FogBearing.Ahead, FogCueKind.ClearWater, 60) };
            Assert.Throws<ArgumentException>(() => new FogListeningSession(null, 2, 2));
            Assert.Throws<ArgumentException>(() => new FogListeningSession(Array.Empty<FogListeningCue>(), 2, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FogListeningSession(singleCue, 0, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FogListeningSession(singleCue, 2, 0));

            var session = new FogListeningSession(singleCue, 1, 2);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                session.ApplyHelmAction((FogHelmAction)99));
            Assert.That(session.ApplyHelmAction(FogHelmAction.HoldCourse),
                Is.EqualTo(FogResolution.SafePassage));
            Assert.That(session.IsComplete, Is.True);
            Assert.That(session.Succeeded, Is.True);

            Assert.That(session.TryFocusListen(), Is.False);
            Assert.Throws<InvalidOperationException>(() => { var _ = session.CurrentCue; });
            Assert.Throws<InvalidOperationException>(() =>
                session.ApplyHelmAction(FogHelmAction.HoldCourse));
            Assert.Throws<InvalidOperationException>(() => session.RecoverFocus(1));
        }

        [Test]
        public void RestrictedFocusBudgetClarifiesCurrentCueOnceAndStopsAtZero()
        {
            var cues = new[]
            {
                new FogListeningCue(FogBearing.Ahead, FogCueKind.ReefSurf, 40),
                new FogListeningCue(FogBearing.Port, FogCueKind.FogBell, 80)
            };
            var session = new FogListeningSession(cues, maxListenFocus: 1, maxHullIntegrity: 2);

            Assert.That(session.EffectiveClarity(), Is.EqualTo(40));
            Assert.That(session.TryFocusListen(), Is.True);
            Assert.That(session.RemainingFocus, Is.Zero);
            Assert.That(session.EffectiveClarity(), Is.EqualTo(75));
            Assert.That(session.TryFocusListen(), Is.False,
                "Cannot focus twice on the same cue.");

            Assert.That(session.ApplyHelmAction(FogHelmAction.SlowAndSound),
                Is.EqualTo(FogResolution.HazardAvoided));
            Assert.That(session.CurrentCueFocused, Is.False);
            Assert.That(session.TryFocusListen(), Is.False,
                "Cannot focus when listen budget is exhausted.");

            Assert.Throws<ArgumentOutOfRangeException>(() => session.RecoverFocus(0));
            session.RecoverFocus(5);
            Assert.That(session.RemainingFocus, Is.EqualTo(1));
            Assert.That(session.TryFocusListen(), Is.True);
            Assert.That(session.EffectiveClarity(), Is.EqualTo(100));
        }

        [Test]
        public void ReefHazardsDamageHullWhenSteeredIntoAndClearWhenEvaded()
        {
            var cues = new[]
            {
                new FogListeningCue(FogBearing.Ahead, FogCueKind.ReefSurf, 45),
                new FogListeningCue(FogBearing.Port, FogCueKind.ReefSurf, 50),
                new FogListeningCue(FogBearing.Starboard, FogCueKind.ReefSurf, 55)
            };
            var session = new FogListeningSession(cues, maxListenFocus: 2, maxHullIntegrity: 3);

            Assert.That(session.ApplyHelmAction(FogHelmAction.HoldCourse),
                Is.EqualTo(FogResolution.HullScraped));
            Assert.That(session.HullIntegrity, Is.EqualTo(2));

            Assert.That(session.ApplyHelmAction(FogHelmAction.TurnStarboard),
                Is.EqualTo(FogResolution.HazardAvoided));
            Assert.That(session.ApplyHelmAction(FogHelmAction.TurnStarboard),
                Is.EqualTo(FogResolution.HullScraped));

            Assert.That(session.HullIntegrity, Is.EqualTo(1));
            Assert.That(session.AvoidedHazards, Is.EqualTo(1));
            Assert.That(session.Succeeded, Is.True);
        }

        [Test]
        public void SignalsAndFalseEchoesDistinguishMatchedBearingsFromLostBearing()
        {
            var cues = new[]
            {
                new FogListeningCue(FogBearing.Starboard, FogCueKind.SignalResponse, 60, requiresReceiverLamp: true),
                new FogListeningCue(FogBearing.Port, FogCueKind.FalseEcho, 35),
                new FogListeningCue(FogBearing.Ahead, FogCueKind.FogBell, 70)
            };
            var session = new FogListeningSession(cues, maxListenFocus: 2, maxHullIntegrity: 2);

            Assert.That(session.CurrentCue.RequiresReceiverLamp, Is.True);
            Assert.That(session.ApplyHelmAction(FogHelmAction.AnswerSignal),
                Is.EqualTo(FogResolution.SignalMatched));
            Assert.That(session.ApplyHelmAction(FogHelmAction.AnswerSignal),
                Is.EqualTo(FogResolution.LostBearing));
            Assert.That(session.ApplyHelmAction(FogHelmAction.SlowAndSound),
                Is.EqualTo(FogResolution.SafePassage));

            Assert.That(session.MatchedSignals, Is.EqualTo(1));
            Assert.That(session.HullIntegrity, Is.EqualTo(2));
            Assert.That(session.Succeeded, Is.True);
        }

        [Test]
        public void ExhaustingHullIntegrityEndsVoyageEarlyAsFailed()
        {
            var cues = new[]
            {
                new FogListeningCue(FogBearing.Ahead, FogCueKind.ReefSurf, 30),
                new FogListeningCue(FogBearing.Ahead, FogCueKind.ClearWater, 90)
            };
            var session = new FogListeningSession(cues, maxListenFocus: 1, maxHullIntegrity: 1);

            Assert.That(session.ApplyHelmAction(FogHelmAction.HoldCourse),
                Is.EqualTo(FogResolution.HullScraped));
            Assert.That(session.HullIntegrity, Is.Zero);
            Assert.That(session.IsComplete, Is.True);
            Assert.That(session.Succeeded, Is.False);
            Assert.That(session.CurrentStep, Is.EqualTo(1));
        }
    }
}
