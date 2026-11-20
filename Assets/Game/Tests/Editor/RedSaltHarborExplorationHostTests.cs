using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RedSaltHarborExplorationHostTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedCompletedCrossing(Dictionary<string, int> values)
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
        }

        [Test]
        public void WalkingToBerthAndQuayTriggersLandingCheckThenSeranWithoutFabricatingArrival()
        {
            var root = new GameObject("Red Salt playable exploration test");
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            RedSaltHarborExplorationHost host = null;
            try
            {
                host = new RedSaltHarborExplorationHost(root.transform, read, write,
                    observedLanding: new M05LandingReport(3, 10, true, true));

                Assert.That(host.ChunkRenderer.ChunkCount, Is.EqualTo(15));
                Assert.That(host.IsDialogueOpen, Is.False);

                // Walk east from spawn (16, 24) to landing-check trigger (22, 24).
                for (int i = 0; i < 10; i++)
                    host.StepExploration(Vector2.right, 0.1f);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.True);
                Assert.That(host.IsDialogueOpen, Is.True);
                Assert.That(host.ActiveLandingView, Is.Not.Null);
                Assert.That(host.StepExploration(Vector2.right, 0.1f), Is.False,
                    "Exploration movement must pause while bottom dialogue is open.");

                host.ActiveLandingView.ConfirmButton.onClick.Invoke();
                Assert.That(host.IsDialogueOpen, Is.False);
                Assert.That(M05LandingFacts.TryRead(read, out M05LandingReport? saved), Is.True);
                Assert.That(saved.HasValue, Is.True);

                // Walk east to QuayPromenade Seran trigger (50, 26).
                for (int i = 0; i < 42; i++)
                    host.StepExploration(Vector2.right, 0.1f);
                for (int i = 0; i < 4; i++)
                    host.StepExploration(Vector2.up, 0.1f);

                Assert.That(host.TryInteractAtCurrentPosition(), Is.True);
                Assert.That(host.ActiveSeranView, Is.Not.Null);
                host.ActiveSeranView.TakeButton.onClick.Invoke();
                host.ActiveSeranView.ContinueButton.onClick.Invoke();
                Assert.That(host.IsDialogueOpen, Is.False);
                Assert.That(M05LettersFacts.TryRead(read, out M05LetterDecision? decision), Is.True);
                Assert.That(decision, Is.EqualTo(M05LetterDecision.TakePacket));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }


        private static void WalkTo(RedSaltHarborExplorationHost host, float x, float y)
        {
            Vector2 target = new Vector2(x, y);
            // The authored harbor quays are continuous and walkable, so follow
            // X and Y separately to exercise the actual explorer motor.
            for (int axis = 0; axis < 2; axis++)
            {
                for (int i = 0; i < 900; i++)
                {
                    float delta = axis == 0
                        ? target.x - host.PlayerPosition.x
                        : target.y - host.PlayerPosition.y;
                    if (Mathf.Abs(delta) <= 0.25f) break;
                    Vector2 input = axis == 0
                        ? new Vector2(Mathf.Sign(delta), 0f)
                        : new Vector2(0f, Mathf.Sign(delta));
                    host.StepExploration(input, 0.03f);
                }
            }
            Assert.That(Vector2.Distance(host.PlayerPosition, target),
                Is.LessThan(0.5f), "The scripted walking route must reach its location.");
        }

        [Test]
        public void HarborRumorMarketAndRepairAreDiscoveredOnlyAtTheirOwnLocations()
        {
            var root = new GameObject("M05 physical story beats");
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            RedSaltHarborExplorationHost host = null;
            try
            {
                host = new RedSaltHarborExplorationHost(root.transform, read, write,
                    observedLanding: new M05LandingReport(3, 10, true, true));
                WalkTo(host, 22.5f, 24.5f);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.True);
                host.ActiveLandingView.ConfirmButton.onClick.Invoke();

                // Visiting the repair stall cannot play the fisherman's
                // rumor or the market accusation without walking there.
                WalkTo(host, 84.5f, 30.5f);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.True);
                Assert.That(host.ActiveHarborWalkView.Beat, Is.EqualTo(M05HarborBeat.RepairStall));
                Assert.That(host.ActiveHarborWalkView.Dialogue.text,
                    Does.Contain("Shen Li checked our tools this morning"));
                host.ActiveHarborWalkView.AdvanceButton.onClick.Invoke();
                Assert.That(host.IsDialogueOpen, Is.False);
                Assert.That(host.HarborWalkCompleted, Is.False);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.False,
                    "Already witnessed dialogue must not re-open at the same stall.");

                WalkTo(host, 118.5f, 22.5f);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.True);
                Assert.That(host.ActiveHarborWalkView.Beat, Is.EqualTo(M05HarborBeat.FisherRumor));
                Assert.That(host.ActiveHarborWalkView.Dialogue.text, Does.Contain("Three boats"));
                host.ActiveHarborWalkView.AdvanceButton.onClick.Invoke();
                Assert.That(host.HarborWalkCompleted, Is.False);

                WalkTo(host, 76.5f, 56.5f);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.True);
                Assert.That(host.ActiveHarborWalkView.Beat, Is.EqualTo(M05HarborBeat.MarketAccusation));
                Assert.That(host.ActiveHarborWalkView.Dialogue.text, Does.Contain("half a bowl"));
                host.ActiveHarborWalkView.AdvanceButton.onClick.Invoke();
                Assert.That(host.HarborWalkCompleted, Is.True);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.False);
                Assert.That(host.IsDialogueOpen, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SeranTriggerRefusesEntryBeforeVerifiedLandingAndDisposesCleanly()
        {
            var root = new GameObject("Red Salt gate test");
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            RedSaltHarborExplorationHost host = null;
            try
            {
                host = new RedSaltHarborExplorationHost(root.transform, read, write,
                    observedLanding: null);

                // Walk straight past berth to Seran (50, 26) without confirming landing.
                for (int i = 0; i < 50; i++)
                    host.StepExploration(Vector2.right, 0.1f);
                for (int i = 0; i < 4; i++)
                    host.StepExploration(Vector2.up, 0.1f);

                Assert.That(host.TryInteractAtCurrentPosition(), Is.False,
                    "Unverified landing must never open Seran's letters.");
                Assert.That(host.IsDialogueOpen, Is.False);

                host.Dispose();
                Assert.That(host.IsDisposed, Is.True);
                Assert.That(root.transform.childCount, Is.Zero);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
