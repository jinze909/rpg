using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M05OuterHarborFlowHostTests
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

        [Test]
        public void TryOpenRefusesIncompleteCrossingOrInvalidObservedLanding()
        {
            var root = new GameObject("M05 flow gate test");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            var validLanding = new M05LandingReport(3, 10, true, true);
            try
            {
                Assert.That(M05OuterHarborFlowHost.TryOpen(
                    root.transform, validLanding, read, write, _ => { }, out _), Is.False);

                SeedCompletedCrossing(values, sailed: false, crossedFog: true);
                Assert.That(M05OuterHarborFlowHost.TryOpen(
                    root.transform, validLanding, read, write, _ => { }, out _), Is.False);

                Assert.That(M04DepartureFacts.TryMarkSailed(read, write), Is.True);
                Assert.That(M05OuterHarborFlowHost.TryOpen(
                    root.transform, observedLanding: null, read, write, _ => { }, out _), Is.False);
                Assert.That(M05OuterHarborFlowHost.TryOpen(
                    root.transform, new M05LandingReport(3, 5, false, true),
                    read, write, _ => { }, out _), Is.False);
                Assert.That(M05OuterHarborFlowHost.TryOpen(
                    root.transform, new M05LandingReport(5, 5, true, true),
                    read, write, _ => { }, out _), Is.False);
                Assert.That(root.transform.childCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FullOuterHarborFlowSequencesLandingCheckSeranAndHarborWalkWithSingleCanvas()
        {
            var root = new GameObject("M05 full flow test");
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            M05OuterHarborFlowHost host = null;
            try
            {
                int completions = 0;
                M05OuterHarborOutcome outcome = default;
                Assert.That(M05OuterHarborFlowHost.TryOpen(
                    root.transform, new M05LandingReport(3, 10, true, true),
                    read, write, result => { completions++; outcome = result; },
                    out host), Is.True);
                Assert.That(host.Stage, Is.EqualTo(M05OuterHarborFlowStage.LandingCheck));
                Assert.That(host.LandingCheckView, Is.Not.Null);
                Assert.That(host.ArrivalSession, Is.Null);
                Assert.That(root.transform.childCount, Is.EqualTo(1));

                host.LandingCheckView.ConfirmButton.onClick.Invoke();
                Assert.That(M05LandingFacts.TryRead(read, out M05LandingReport? persisted), Is.True);
                Assert.That(persisted.HasValue, Is.True);
                Assert.That(host.Stage, Is.EqualTo(M05OuterHarborFlowStage.ArrivalSession));
                Assert.That(host.LandingCheckView, Is.Null);
                Assert.That(host.ArrivalSession, Is.Not.Null);
                Assert.That(host.ArrivalSession.Stage, Is.EqualTo(M05OuterHarborStage.SeranLetters));
                Assert.That(root.transform.childCount, Is.EqualTo(1));

                host.ArrivalSession.SeranView.TakeButton.onClick.Invoke();
                host.ArrivalSession.SeranView.ContinueButton.onClick.Invoke();
                Assert.That(host.ArrivalSession.Stage, Is.EqualTo(M05OuterHarborStage.HarborWalk));
                Assert.That(root.transform.childCount, Is.EqualTo(1));

                host.ArrivalSession.HarborWalkView.AdvanceButton.onClick.Invoke();
                host.ArrivalSession.HarborWalkView.AdvanceButton.onClick.Invoke();
                host.ArrivalSession.HarborWalkView.AdvanceButton.onClick.Invoke();

                Assert.That(completions, Is.EqualTo(1));
                Assert.That(host.Stage, Is.EqualTo(M05OuterHarborFlowStage.Completed));
                Assert.That(host.ArrivalSession, Is.Null);
                Assert.That(outcome.Landing.CrewArrived, Is.EqualTo(3));
                Assert.That(outcome.Landing.ReadyRescueBerths, Is.EqualTo(10));
                Assert.That(outcome.LetterDecision, Is.EqualTo(M05LetterDecision.TakePacket));
                Assert.That(outcome.LettersCarried, Is.EqualTo(M05LettersFacts.PacketLetterCount));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SavedLandingResumesDirectlyAtArrivalSessionWithoutReconfirmingLanding()
        {
            var root = new GameObject("M05 flow resume test");
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            Assert.That(M05LandingFacts.TryRecord(
                new M05LandingReport(2, 0, false, false), read, write), Is.True);

            M05OuterHarborFlowHost host = null;
            try
            {
                Assert.That(M05OuterHarborFlowHost.TryOpen(
                    root.transform, observedLanding: null, read, write, _ => { },
                    out host), Is.True);
                Assert.That(host.Stage, Is.EqualTo(M05OuterHarborFlowStage.ArrivalSession));
                Assert.That(host.LandingCheckView, Is.Null);
                Assert.That(host.ArrivalSession, Is.Not.Null);
                Assert.That(host.ArrivalSession.Landing.HasUsableLandingCapacity, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InterruptedLandingWriteAllowsRetryWithoutAdvancingToArrivalSession()
        {
            var root = new GameObject("M05 flow retry test");
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            bool failNext = true;
            Action<string, int> flakyWrite = (key, value) =>
            {
                if (failNext && key == M05LandingFacts.ReadyBerthsKey)
                {
                    failNext = false;
                    throw new InvalidOperationException("Interrupted landing save");
                }
                values[key] = value;
            };

            M05OuterHarborFlowHost host = null;
            try
            {
                Assert.That(M05OuterHarborFlowHost.TryOpen(
                    root.transform, new M05LandingReport(4, 8, true, true),
                    read, flakyWrite, _ => { }, out host), Is.True);
                Assert.Throws<InvalidOperationException>(() =>
                    host.LandingCheckView.ConfirmButton.onClick.Invoke());
                Assert.That(host.Stage, Is.EqualTo(M05OuterHarborFlowStage.LandingCheck));
                Assert.That(host.LandingCheckView.ConfirmButton.interactable, Is.True);
                Assert.That(host.ArrivalSession, Is.Null);

                host.LandingCheckView.ConfirmButton.onClick.Invoke();
                Assert.That(host.Stage, Is.EqualTo(M05OuterHarborFlowStage.ArrivalSession));
                Assert.That(host.ArrivalSession, Is.Not.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SavedLandingSurvivesInterruptedScreenHandoffAndRetriesWithoutDuplicateWrite()
        {
            var root = new GameObject("M05 landing handoff retry test");
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> ordinaryRead = Reader(values);
            bool failFirstLetterRead = true;
            Func<string, int> flakyRead = key =>
            {
                // Simulate a temporary read error *after* the landing save
                // succeeded, while opening the following letter scene.
                if (key == M05LettersFacts.DecisionKey &&
                    ordinaryRead(M05LandingFacts.ConfirmedKey) == 1 &&
                    failFirstLetterRead)
                {
                    failFirstLetterRead = false;
                    throw new InvalidOperationException("Interrupted next-screen read");
                }
                return ordinaryRead(key);
            };
            int publishedLandings = 0;
            Action<string, int> write = (key, value) =>
            {
                values[key] = value;
                if (key == M05LandingFacts.ConfirmedKey && value == 1)
                    publishedLandings++;
            };

            M05OuterHarborFlowHost host = null;
            try
            {
                Assert.That(M05OuterHarborFlowHost.TryOpen(
                    root.transform, new M05LandingReport(3, 10, true, true),
                    flakyRead, write, _ => { }, out host), Is.True);

                Assert.Throws<InvalidOperationException>(() =>
                    host.LandingCheckView.ConfirmButton.onClick.Invoke());
                Assert.That(publishedLandings, Is.EqualTo(1));
                Assert.That(M05LandingFacts.TryRead(ordinaryRead,
                    out M05LandingReport? saved), Is.True);
                Assert.That(saved.HasValue, Is.True);
                Assert.That(saved.Value.CrewArrived, Is.EqualTo(3));
                Assert.That(host.Stage, Is.EqualTo(M05OuterHarborFlowStage.LandingCheck));
                Assert.That(host.ArrivalSession, Is.Null);
                Assert.That(host.LandingCheckView.ConfirmButton.interactable, Is.True);
                Assert.That(root.transform.childCount, Is.EqualTo(1));

                host.LandingCheckView.ConfirmButton.onClick.Invoke();
                Assert.That(publishedLandings, Is.EqualTo(1),
                    "The retry must reuse the committed landing, not publish another.");
                Assert.That(host.Stage, Is.EqualTo(M05OuterHarborFlowStage.ArrivalSession));
                Assert.That(host.LandingCheckView, Is.Null);
                Assert.That(host.ArrivalSession, Is.Not.Null);
                Assert.That(host.ArrivalSession.Stage, Is.EqualTo(M05OuterHarborStage.SeranLetters));
                Assert.That(root.transform.childCount, Is.EqualTo(1),
                    "The landing canvas must be retired once the next scene is ready.");
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AspectPropagationAndParentTeardownDisposeCleanly()
        {
            var root = new GameObject("M05 flow teardown test");
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            M05OuterHarborFlowHost host = null;
            try
            {
                Assert.Throws<ArgumentNullException>(() =>
                    M05OuterHarborFlowHost.TryOpen(
                        null, new M05LandingReport(3, 6, true, true),
                        read, write, _ => { }, out _));
                Assert.Throws<ArgumentNullException>(() =>
                    M05OuterHarborFlowHost.TryOpen(
                        root.transform, new M05LandingReport(3, 6, true, true),
                        read, write, null, out _));

                Assert.That(M05OuterHarborFlowHost.TryOpen(
                    root.transform, new M05LandingReport(3, 6, true, true),
                    read, write, _ => { }, out host), Is.True);
                Assert.DoesNotThrow(() => host.LayoutForViewportAspect(9f / 16f));
                host.LandingCheckView.ConfirmButton.onClick.Invoke();
                Assert.DoesNotThrow(() => host.LayoutForViewportAspect(16f / 9f));

                UnityEngine.Object.DestroyImmediate(root);
                root = null;
                Assert.DoesNotThrow(() => host.LayoutForViewportAspect(32f / 9f));
                Assert.That(host.IsDisposed, Is.True);
            }
            finally
            {
                host?.Dispose();
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
