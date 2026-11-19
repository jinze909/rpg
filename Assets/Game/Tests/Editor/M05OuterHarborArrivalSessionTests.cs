using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M05OuterHarborArrivalSessionTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedVerifiedLanding(Dictionary<string, int> values,
            M05LandingReport? landing = null)
        {
            Action<string, int> write = (key, value) => values[key] = value;
            Func<string, int> read = Reader(values);
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
        public void TryOpenRefusesUnverifiedCrossingAndNeverCreatesUiOrFacts()
        {
            var root = new GameObject("M05 arrival gate test");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            try
            {
                int completed = 0;
                Assert.That(M05OuterHarborArrivalSession.TryOpen(
                    root.transform, read, write, _ => completed++,
                    out M05OuterHarborArrivalSession emptySession), Is.False);
                Assert.That(emptySession, Is.Null);
                Assert.That(root.transform.childCount, Is.Zero);

                // Even after sailing and crossing the fog, an unrecorded M05
                // landing must not open the outer-harbor scenes.
                SeedVerifiedLanding(values, landing: null);
                Assert.That(M05OuterHarborArrivalSession.TryOpen(
                    root.transform, read, write, _ => completed++,
                    out M05OuterHarborArrivalSession unlandedSession), Is.False);
                Assert.That(unlandedSession, Is.Null);
                Assert.That(root.transform.childCount, Is.Zero);
                Assert.That(completed, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void VerifiedLandingSequencesSeranChoiceIntoHarborWalkWithoutOverlappingCanvases()
        {
            var root = new GameObject("M05 arrival sequence test");
            var values = new Dictionary<string, int>();
            SeedVerifiedLanding(values, new M05LandingReport(3, 10, true, true));
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            M05OuterHarborArrivalSession session = null;
            try
            {
                int completions = 0;
                M05OuterHarborOutcome outcome = default;
                Assert.That(M05OuterHarborArrivalSession.TryOpen(
                    root.transform, read, write,
                    result => { completions++; outcome = result; },
                    out session), Is.True);
                Assert.That(session.Stage, Is.EqualTo(M05OuterHarborStage.SeranLetters));
                Assert.That(session.SeranView, Is.Not.Null);
                Assert.That(session.HarborWalkView, Is.Null);
                Assert.That(root.transform.childCount, Is.EqualTo(1));

                session.SeranView.TakeButton.onClick.Invoke();
                session.SeranView.ContinueButton.onClick.Invoke();

                Assert.That(M05LettersFacts.TryRead(read, out M05LetterDecision? saved), Is.True);
                Assert.That(saved, Is.EqualTo(M05LetterDecision.TakePacket));
                Assert.That(session.Stage, Is.EqualTo(M05OuterHarborStage.HarborWalk));
                Assert.That(session.SeranView, Is.Null,
                    "Seran canvas must be disposed before HarborWalk opens.");
                Assert.That(session.HarborWalkView, Is.Not.Null);
                Assert.That(root.transform.childCount, Is.EqualTo(1));
                Assert.That(completions, Is.Zero);

                session.HarborWalkView.AdvanceButton.onClick.Invoke();
                session.HarborWalkView.AdvanceButton.onClick.Invoke();
                session.HarborWalkView.AdvanceButton.onClick.Invoke();

                Assert.That(completions, Is.EqualTo(1));
                Assert.That(session.Stage, Is.EqualTo(M05OuterHarborStage.Completed));
                Assert.That(session.HarborWalkView, Is.Null);
                Assert.That(outcome.Landing.CrewArrived, Is.EqualTo(3));
                Assert.That(outcome.Landing.ReadyRescueBerths, Is.EqualTo(10));
                Assert.That(outcome.HasUsableLandingCapacity, Is.True);
                Assert.That(outcome.LetterDecision, Is.EqualTo(M05LetterDecision.TakePacket));
                Assert.That(outcome.LettersCarried, Is.EqualTo(M05LettersFacts.PacketLetterCount));
            }
            finally
            {
                session?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ExistingPersistedLetterDecisionResumesDirectlyAtHarborWalkWithoutReasking()
        {
            var root = new GameObject("M05 arrival resume test");
            var values = new Dictionary<string, int>();
            SeedVerifiedLanding(values, new M05LandingReport(2, 0, false, false));
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            Assert.That(M05LettersFacts.TryRecord(
                M05LetterDecision.LeaveForNextBoat, read, write), Is.True);

            M05OuterHarborArrivalSession session = null;
            try
            {
                M05OuterHarborOutcome outcome = default;
                int completions = 0;
                Assert.That(M05OuterHarborArrivalSession.TryOpen(
                    root.transform, read, write,
                    result => { completions++; outcome = result; },
                    out session), Is.True);
                Assert.That(session.Stage, Is.EqualTo(M05OuterHarborStage.HarborWalk));
                Assert.That(session.SeranView, Is.Null);
                Assert.That(session.HarborWalkView, Is.Not.Null);
                Assert.That(session.LetterDecision,
                    Is.EqualTo(M05LetterDecision.LeaveForNextBoat));

                session.HarborWalkView.AdvanceButton.onClick.Invoke();
                session.HarborWalkView.AdvanceButton.onClick.Invoke();
                session.HarborWalkView.AdvanceButton.onClick.Invoke();

                Assert.That(completions, Is.EqualTo(1));
                Assert.That(outcome.LetterDecision,
                    Is.EqualTo(M05LetterDecision.LeaveForNextBoat));
                Assert.That(outcome.LettersCarried, Is.Zero);
                Assert.That(outcome.HasUsableLandingCapacity, Is.False);
            }
            finally
            {
                session?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InterruptedSeranSaveAndInterruptedCompletionBothAllowCleanRetry()
        {
            var root = new GameObject("M05 arrival retry test");
            var values = new Dictionary<string, int>();
            SeedVerifiedLanding(values, new M05LandingReport(4, 8, true, true));
            Func<string, int> read = Reader(values);
            int writeAttempts = 0;
            Action<string, int> flakyWrite = (key, value) =>
            {
                writeAttempts++;
                if (writeAttempts == 1)
                    throw new InvalidOperationException("Interrupted Makinom write");
                values[key] = value;
            };

            M05OuterHarborArrivalSession session = null;
            try
            {
                int hostAttempts = 0;
                int hostSuccesses = 0;
                Assert.That(M05OuterHarborArrivalSession.TryOpen(
                    root.transform, read, flakyWrite,
                    _ =>
                    {
                        hostAttempts++;
                        if (hostAttempts == 1)
                            throw new InvalidOperationException("Host unavailable");
                        hostSuccesses++;
                    },
                    out session), Is.True);

                session.SeranView.TakeButton.onClick.Invoke();
                Assert.Throws<InvalidOperationException>(() =>
                    session.SeranView.ContinueButton.onClick.Invoke());
                Assert.That(session.Stage, Is.EqualTo(M05OuterHarborStage.SeranLetters));
                Assert.That(session.SeranView.ContinueButton.interactable, Is.True);
                Assert.That(session.HarborWalkView, Is.Null);

                session.SeranView.ContinueButton.onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(M05OuterHarborStage.HarborWalk));
                Assert.That(session.HarborWalkView, Is.Not.Null);

                session.HarborWalkView.AdvanceButton.onClick.Invoke();
                session.HarborWalkView.AdvanceButton.onClick.Invoke();
                Assert.Throws<InvalidOperationException>(() =>
                    session.HarborWalkView.AdvanceButton.onClick.Invoke());
                Assert.That(session.Stage, Is.EqualTo(M05OuterHarborStage.HarborWalk));
                Assert.That(session.HarborWalkView.Beat,
                    Is.EqualTo(M05HarborBeat.MarketAccusation));
                Assert.That(session.HarborWalkView.AdvanceButton.interactable, Is.True);

                session.HarborWalkView.AdvanceButton.onClick.Invoke();
                Assert.That(hostAttempts, Is.EqualTo(2));
                Assert.That(hostSuccesses, Is.EqualTo(1));
                Assert.That(session.Stage, Is.EqualTo(M05OuterHarborStage.Completed));
            }
            finally
            {
                session?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ViewportAspectPropagationAndParentTeardownDisposalGuardLateCallbacks()
        {
            var root = new GameObject("M05 arrival teardown test");
            var values = new Dictionary<string, int>();
            SeedVerifiedLanding(values, new M05LandingReport(3, 6, true, true));
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            M05OuterHarborArrivalSession session = null;
            try
            {
                Assert.Throws<ArgumentNullException>(() =>
                    M05OuterHarborArrivalSession.TryOpen(
                        null, read, write, _ => { }, out _));
                Assert.Throws<ArgumentNullException>(() =>
                    M05OuterHarborArrivalSession.TryOpen(
                        root.transform, read, write, null, out _));
                Assert.That(M05OuterHarborArrivalSession.TryOpen(
                    root.transform, null, write, _ => { }, out _), Is.False);
                Assert.That(M05OuterHarborArrivalSession.TryOpen(
                    root.transform, read, null, _ => { }, out _), Is.False);

                Assert.That(M05OuterHarborArrivalSession.TryOpen(
                    root.transform, read, write, _ => { }, out session), Is.True);
                Assert.DoesNotThrow(() => session.LayoutForViewportAspect(9f / 16f));
                session.SeranView.LeaveButton.onClick.Invoke();
                session.SeranView.ContinueButton.onClick.Invoke();
                Assert.DoesNotThrow(() => session.LayoutForViewportAspect(16f / 9f));

                UnityEngine.Object.DestroyImmediate(root);
                root = null;
                Assert.DoesNotThrow(() => session.LayoutForViewportAspect(32f / 9f));
                Assert.That(session.IsDisposed, Is.True);
            }
            finally
            {
                session?.Dispose();
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
