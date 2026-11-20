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
                Assert.That(host.MinimapHud, Is.Not.Null);
                Assert.That(host.MinimapHud.BeaconCount, Is.EqualTo(6));
                Assert.That(host.MinimapHud.ActiveBeaconCount, Is.EqualTo(6));
                Assert.That(host.MinimapHud.MinimapTexture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(root.GetComponentInChildren<UnityEngine.UI.GraphicRaycaster>(), Is.Null,
                    "The live minimap must not intercept exploration input.");
                Vector2 startMarker = host.MinimapHud.PlayerMarkerRect.anchoredPosition;

                // Walk east from spawn (16, 24) to landing-check trigger (22, 24).
                for (int i = 0; i < 10; i++)
                    host.StepExploration(Vector2.right, 0.1f);
                Assert.That(host.MinimapHud.PlayerMarkerRect.anchoredPosition.x,
                    Is.GreaterThan(startMarker.x),
                    "The minimap marker follows the actual player motor.");
                Assert.That(host.TryInteractAtCurrentPosition(), Is.True);
                Assert.That(host.IsDialogueOpen, Is.True);
                Assert.That(host.ActiveLandingView, Is.Not.Null);
                Assert.That(host.StepExploration(Vector2.right, 0.1f), Is.False,
                    "Exploration movement must pause while bottom dialogue is open.");

                host.ActiveLandingView.ConfirmButton.onClick.Invoke();
                Assert.That(host.IsDialogueOpen, Is.False);
                Assert.That(M05LandingFacts.TryRead(read, out M05LandingReport? saved), Is.True);
                Assert.That(saved.HasValue, Is.True);
                Assert.That(host.MinimapHud.ActiveBeaconCount, Is.EqualTo(5),
                    "Only the genuinely confirmed landing beacon is completed.");

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
                Assert.That(host.MinimapHud.ActiveBeaconCount, Is.EqualTo(4),
                    "The letter courier beacon completes only on an actual saved answer.");
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void WalkTo(RedSaltHarborExplorationHost host, float x, float y,
            bool yFirst = false)
        {
            Vector2 target = new Vector2(x, y);
            int[] axes = yFirst ? new[] { 1, 0 } : new[] { 0, 1 };
            for (int a = 0; a < axes.Length; a++)
            {
                int axis = axes[a];
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
            string prevLang = SoundwellUiLocalization.Language;
            RedSaltHarborExplorationHost host = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                host = new RedSaltHarborExplorationHost(root.transform, read, write,
                    observedLanding: new M05LandingReport(3, 10, true, true));
                WalkTo(host, 22.5f, 24.5f);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.True);
                host.ActiveLandingView.ConfirmButton.onClick.Invoke();
                Assert.That(host.MinimapHud.ActiveBeaconCount, Is.EqualTo(5));

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
                Assert.That(host.MinimapHud.ActiveBeaconCount, Is.EqualTo(4));
                Assert.That(host.TryInteractAtCurrentPosition(), Is.False,
                    "Already witnessed dialogue must not re-open at the same stall.");

                // Walk south first so the route clears the lantern post at (118, 25).
                WalkTo(host, 118.5f, 22.5f, yFirst: true);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.True);
                Assert.That(host.ActiveHarborWalkView.Beat, Is.EqualTo(M05HarborBeat.FisherRumor));
                Assert.That(host.ActiveHarborWalkView.Dialogue.text, Does.Contain("Three boats"));
                host.ActiveHarborWalkView.AdvanceButton.onClick.Invoke();
                Assert.That(host.HarborWalkCompleted, Is.False);
                Assert.That(host.MinimapHud.ActiveBeaconCount, Is.EqualTo(3));

                WalkTo(host, 76.5f, 56.5f);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.True);
                Assert.That(host.ActiveHarborWalkView.Beat, Is.EqualTo(M05HarborBeat.MarketAccusation));
                Assert.That(host.ActiveHarborWalkView.Dialogue.text, Does.Contain("half a bowl"));
                host.ActiveHarborWalkView.AdvanceButton.onClick.Invoke();
                Assert.That(host.HarborWalkCompleted, Is.True);
                Assert.That(host.MinimapHud.ActiveBeaconCount, Is.EqualTo(2),
                    "The unanswered courier and the later mine-camp gate remain active.");
                Assert.That(host.TryInteractAtCurrentPosition(), Is.False);
                Assert.That(host.IsDialogueOpen, Is.False);
            }
            finally
            {
                host?.Dispose();
                SoundwellUiLocalization.SetLanguage(prevLang);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LandingInteractionOnlyOpensForSailedAndSafelyCrossedCurrentVoyage()
        {
            var root = new GameObject("M05 physical landing eligibility");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            int writes = 0;
            Action<string, int> write = (key, value) =>
            {
                writes++;
                values[key] = value;
            };
            var report = new M05LandingReport(3, 10, true, true);
            RedSaltHarborExplorationHost host = null;
            try
            {
                host = new RedSaltHarborExplorationHost(root.transform, read, write, report);
                for (int i = 0; i < 10; i++)
                    host.StepExploration(Vector2.right, 0.1f);

                Assert.That(M05LandingFacts.CanRecord(report, read), Is.False);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.False,
                    "An observed boat cannot dock without verified departure and crossing.");
                Assert.That(host.IsDialogueOpen, Is.False);

                SeedCompletedCrossing(values);
                values[M04DepartureFacts.SailedKey] = 0;
                Assert.That(host.TryInteractAtCurrentPosition(), Is.False,
                    "A prepared but unlaunched boat must not open the landing panel.");
                values[M04DepartureFacts.SailedKey] = 1;
                values[FogListeningFacts.SucceededKey] = 0;
                Assert.That(host.TryInteractAtCurrentPosition(), Is.False,
                    "A failed fog leg must not offer an arrival.");
                values[FogListeningFacts.SucceededKey] = 1;
                Assert.That(M05LandingFacts.CanRecord(report, read), Is.True);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.True);
                Assert.That(host.ActiveLandingView, Is.Not.Null);
                host.ActiveLandingView.ConfirmButton.onClick.Invoke();
                Assert.That(M05LandingFacts.TryRead(read, out M05LandingReport? saved), Is.True);
                Assert.That(saved.HasValue, Is.True);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.False,
                    "The same landing cannot be reconfirmed.");
                Assert.That(writes, Is.GreaterThan(0));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ImplausibleOrCorruptLandingDoesNotPresentAnUnfinishablePanel()
        {
            var root = new GameObject("M05 impossible landing");
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            int writes = 0;
            Action<string, int> write = (key, value) =>
            {
                writes++;
                values[key] = value;
            };
            // The confirmed M04 manifest has only 16 berths.
            var impossible = new M05LandingReport(3, 17, true, true);
            RedSaltHarborExplorationHost host = null;
            try
            {
                host = new RedSaltHarborExplorationHost(root.transform,
                    read, write, impossible);
                for (int i = 0; i < 10; i++)
                    host.StepExploration(Vector2.right, 0.1f);

                Assert.That(M05LandingFacts.CanRecord(impossible, read), Is.False);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.False);
                values[M05LandingFacts.ConfirmedKey] = 1;
                values[M05LandingFacts.CrewArrivedKey] = 99;
                Assert.That(host.TryInteractAtCurrentPosition(), Is.False,
                    "A corrupt committed arrival must not reopen its confirmation.");
                Assert.That(host.IsDialogueOpen, Is.False);
                Assert.That(writes, Is.Zero);
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
