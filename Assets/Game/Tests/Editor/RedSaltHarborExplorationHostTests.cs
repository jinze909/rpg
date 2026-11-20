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
        public void M05ResumeRestoresOnlyActuallyWalkedWalkableTileAfterVerifiedLanding()
        {
            var root = new GameObject("M05 position continuity");
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            RedSaltHarborExplorationHost host = null;
            try
            {
                // Sailing successfully is not landing. Walking the preflight
                // map may not publish a reusable post-arrival location.
                host = new RedSaltHarborExplorationHost(root.transform, read, write);
                host.StepExploration(Vector2.right, 0.4f);
                Assert.That(read(M05HarborPositionFacts.PositionKey), Is.Zero);
                host.Dispose();
                UnityEngine.Object.DestroyImmediate(root);

                root = new GameObject("M05 verified harbor walk");
                var landing = new M05LandingReport(3, 10, true, true);
                Assert.That(M05LandingFacts.TryRecord(landing, read, write), Is.True);
                host = new RedSaltHarborExplorationHost(root.transform, read, write);
                WalkTo(host, 84.5f, 30.5f);
                Vector2Int witnessedTile = new Vector2Int(
                    Mathf.FloorToInt(host.PlayerPosition.x),
                    Mathf.FloorToInt(host.PlayerPosition.y));
                Assert.That(M05HarborPositionFacts.TryRead(
                    read, host.Layout, out Vector2Int? saved), Is.True);
                Assert.That(saved, Is.EqualTo(witnessedTile));

                host.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
                root = new GameObject("M05 resumed harbor walk");
                host = new RedSaltHarborExplorationHost(root.transform, read, write);
                Assert.That(host.PlayerPosition,
                    Is.EqualTo(new Vector2(witnessedTile.x + 0.5f, witnessedTile.y + 0.5f)));
                Assert.That(host.TryInteractAtCurrentPosition(), Is.True,
                    "The previously reached repair stall remains physically reachable.");
                Assert.That(host.ActiveHarborWalkView.Beat, Is.EqualTo(M05HarborBeat.RepairStall));
                host.ActiveHarborWalkView.AdvanceButton.onClick.Invoke();
                Assert.That(read(M05HarborVisitFacts.VisitedMaskKey), Is.EqualTo(1),
                    "Restoring physical position cannot invent an unwitnessed dialogue.");

                // A corrupt or now-impassable saved tile must safely fall
                // back to authored berth spawn, never into deep water.
                values[M05HarborPositionFacts.PositionKey] = 1 + host.Layout.Width * 3;
                host.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
                root = new GameObject("M05 invalid saved harbor tile");
                host = new RedSaltHarborExplorationHost(root.transform, read, write);
                Assert.That(host.PlayerPosition,
                    Is.EqualTo(new Vector2(host.Layout.PlayerSpawn.x + 0.5f,
                        host.Layout.PlayerSpawn.y + 0.5f)));
                Assert.That(M05HarborPositionFacts.TryRead(
                    read, host.Layout, out _), Is.False);
                Assert.That(host.HarborWalkCompleted, Is.False);

                Assert.That(ReturnOpeningContinuation.TryResetPreviousOutboundFacts(write),
                    Is.True);
                Assert.That(read(M05HarborPositionFacts.PositionKey), Is.Zero,
                    "Starting a new voyage cannot re-use last voyage's map position.");
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void VisitedHarborScenesPersistAcrossMapReloadAndResetOnlyForNewVoyage()
        {
            var root = new GameObject("M05 saved map observations");
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            var landing = new M05LandingReport(3, 10, true, true);
            Assert.That(M05LandingFacts.TryRecord(landing, read, write), Is.True);

            RedSaltHarborExplorationHost host = null;
            try
            {
                host = new RedSaltHarborExplorationHost(root.transform, read, write, landing);
                WalkTo(host, 84.5f, 30.5f);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.True);
                Assert.That(read(M05HarborVisitFacts.VisitedMaskKey), Is.Zero,
                    "Opening a conversation is not completing it.");
                host.ActiveHarborWalkView.AdvanceButton.onClick.Invoke();
                Assert.That(read(M05HarborVisitFacts.VisitedMaskKey), Is.EqualTo(1));
                Assert.That(host.MinimapHud.ActiveBeaconCount, Is.EqualTo(4));

                host.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
                root = new GameObject("M05 loaded map observations");
                host = new RedSaltHarborExplorationHost(root.transform, read, write, landing);
                Assert.That(host.HarborWalkCompleted, Is.False);
                Assert.That(host.MinimapHud.ActiveBeaconCount, Is.EqualTo(4),
                    "Reload must preserve the already-witnessed repair scene.");
                WalkTo(host, 84.5f, 30.5f);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.False,
                    "Reloaded map must not replay the repair stall or award the beat twice.");

                WalkTo(host, 118.5f, 22.5f, yFirst: true);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.True);
                host.ActiveHarborWalkView.AdvanceButton.onClick.Invoke();
                Assert.That(read(M05HarborVisitFacts.VisitedMaskKey), Is.EqualTo(3));

                WalkTo(host, 76.5f, 56.5f);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.True);
                host.ActiveHarborWalkView.AdvanceButton.onClick.Invoke();
                Assert.That(host.HarborWalkCompleted, Is.True);
                Assert.That(read(M05HarborVisitFacts.VisitedMaskKey),
                    Is.EqualTo(M05HarborVisitFacts.AllBeatsMask));

                Assert.That(ReturnOpeningContinuation.TryResetPreviousOutboundFacts(write),
                    Is.True);
                Assert.That(read(M05HarborVisitFacts.VisitedMaskKey), Is.Zero,
                    "A fresh M03 voyage clears only the previous voyage's witnessed scenes.");
                Assert.That(host.HarborWalkCompleted, Is.False);
                host.RefreshHud();
                Assert.That(host.MinimapHud.ActiveBeaconCount, Is.EqualTo(6),
                    "An old landing and its old visits must not become new-voyage beacons.");
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SavedSeranChoiceDoesNotCompleteMapBeaconWithoutCurrentVerifiedLanding()
        {
            var root = new GameObject("M05 stale Seran beacon");
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            Assert.That(M05LandingFacts.TryRecord(
                new M05LandingReport(3, 10, true, true), read, write), Is.True);
            Assert.That(M05LettersFacts.TryRecord(
                M05LetterDecision.TakePacket, read, write), Is.True);

            RedSaltHarborExplorationHost host = null;
            try
            {
                host = new RedSaltHarborExplorationHost(root.transform, read, write);
                Assert.That(host.MinimapHud.ActiveBeaconCount, Is.EqualTo(4),
                    "The verified arrival and witnessed Seran decision initially complete two beacons.");

                // A partial new-visit reset can invalidate the landing before
                // the older letter bit is cleared. No old completion may leak
                // into the still-unverified new harbor visit's map.
                Assert.That(M05LandingFacts.TryResetForNewVisit(write), Is.True);
                Assert.That(read(M05LettersFacts.DecisionKey),
                    Is.EqualTo((int)M05LetterDecision.TakePacket));
                host.RefreshHud();
                Assert.That(host.MinimapHud.ActiveBeaconCount, Is.EqualTo(6),
                    "The old letters are not this voyage's verified Seran encounter.");
                Assert.That(host.HarborWalkCompleted, Is.False);

                // Even a corrupt committed landing must not revive the
                // stale letter beacon.
                values[M05LandingFacts.ConfirmedKey] = 1;
                values[M05LandingFacts.CrewArrivedKey] = 99;
                host.RefreshHud();
                Assert.That(host.MinimapHud.ActiveBeaconCount, Is.EqualTo(6));
                Assert.That(read(M05LettersFacts.DecisionKey),
                    Is.EqualTo((int)M05LetterDecision.TakePacket),
                    "Presentation must not silently rewrite the saved choice.");
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FailedVisitSaveCanRetryAndCorruptMaskCannotInventVisitedLocations()
        {
            var root = new GameObject("M05 interrupted map visit");
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            Assert.That(M05LandingFacts.TryRecord(
                new M05LandingReport(3, 10, true, true), read, write), Is.True);
            int attemptedWrites = 0;
            Action<string, int> flakyWrite = (key, value) =>
            {
                if (key == M05HarborVisitFacts.VisitedMaskKey && attemptedWrites++ == 0)
                    throw new InvalidOperationException("Interrupted Makinom visit write");
                values[key] = value;
            };
            RedSaltHarborExplorationHost host = null;
            try
            {
                host = new RedSaltHarborExplorationHost(root.transform, read, flakyWrite);
                WalkTo(host, 84.5f, 30.5f);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.True);
                Assert.Throws<InvalidOperationException>(() =>
                    host.ActiveHarborWalkView.AdvanceButton.onClick.Invoke());
                Assert.That(read(M05HarborVisitFacts.VisitedMaskKey), Is.Zero);
                Assert.That(host.IsDialogueOpen, Is.True,
                    "An interrupted save must keep the identical dialogue open for retry.");
                host.ActiveHarborWalkView.AdvanceButton.onClick.Invoke();
                Assert.That(host.IsDialogueOpen, Is.False);
                Assert.That(read(M05HarborVisitFacts.VisitedMaskKey), Is.EqualTo(1));
                Assert.That(attemptedWrites, Is.EqualTo(2));

                values[M05HarborVisitFacts.VisitedMaskKey] = 32;
                Assert.That(M05HarborVisitFacts.TryRead(read, out _), Is.False);
                Assert.That(host.HarborWalkCompleted, Is.False);
                Assert.That(M05HarborVisitFacts.TryRecord(
                    M05HarborBeat.FisherRumor, read, flakyWrite), Is.False);
                Assert.That(values[M05HarborVisitFacts.VisitedMaskKey], Is.EqualTo(32),
                    "A corrupted mask cannot be silently repaired by a scene interaction.");

                Assert.That(M05HarborVisitFacts.TryResetForNewVoyage(write), Is.True);
                Assert.That(read(M05HarborVisitFacts.VisitedMaskKey), Is.Zero);
                Assert.That(M05HarborVisitFacts.TryRecord(
                    M05HarborBeat.Finished, read, write), Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void OldHarborDialogueCannotCreditChangedVoyageLanding()
        {
            var root = new GameObject("M05 stale harbor visit after new landing");
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            int visitWrites = 0;
            Action<string, int> write = (key, value) =>
            {
                if (key == M05HarborVisitFacts.VisitedMaskKey && value != 0)
                    visitWrites++;
                values[key] = value;
            };
            Assert.That(M05LandingFacts.TryRecord(
                new M05LandingReport(3, 10, true, true), read, write), Is.True);
            RedSaltHarborExplorationHost host = null;
            try
            {
                host = new RedSaltHarborExplorationHost(root.transform, read, write);
                WalkTo(host, 84.5f, 30.5f);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.True);
                var oldDialogue = host.ActiveHarborWalkView;
                Assert.That(oldDialogue.Beat, Is.EqualTo(M05HarborBeat.RepairStall));

                Assert.That(ReturnOpeningContinuation.TryResetPreviousOutboundFacts(write),
                    Is.True);
                Assert.Throws<InvalidOperationException>(() =>
                    oldDialogue.AdvanceButton.onClick.Invoke());
                Assert.That(read(M05HarborVisitFacts.VisitedMaskKey), Is.Zero);
                Assert.That(visitWrites, Is.Zero);
                Assert.That(host.IsDialogueOpen, Is.True);
                Assert.That(oldDialogue.AdvanceButton.interactable, Is.True);

                // A different valid landing does not turn the original
                // repair-stall exchange into a newly observed event.
                SeedCompletedCrossing(values);
                Assert.That(M05LandingFacts.TryRecord(
                    new M05LandingReport(2, 0, false, false), read, write), Is.True);
                Assert.Throws<InvalidOperationException>(() =>
                    oldDialogue.AdvanceButton.onClick.Invoke());
                Assert.That(read(M05HarborVisitFacts.VisitedMaskKey), Is.Zero);
                Assert.That(visitWrites, Is.Zero);
                Assert.That(host.IsDialogueOpen, Is.True);
                Assert.That(oldDialogue.AdvanceButton.interactable, Is.True);
                host.RefreshHud();
                Assert.That(host.MinimapHud.ActiveBeaconCount, Is.EqualTo(5),
                    "Only the new verified landing is complete; the old conversation must not count.");
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
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
                Assert.That(host.ChunkRenderer.UsedRealArtSlices, Is.True);
                Assert.That(host.HeroVisual.SpriteRenderer.sprite, Is.Not.Null);
                Assert.That(host.SeranVisual.SpriteRenderer.sprite, Is.Not.Null);
                Assert.That(host.RepairerVisual.SpriteRenderer.sprite, Is.Not.Null);
                Assert.That(host.FisherVisual.SpriteRenderer.sprite, Is.Not.Null);
                Assert.That(host.SaltVendorVisual.SpriteRenderer.sprite, Is.Not.Null);
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
                Assert.That(host.HeroVisual.ActorObject.transform.localPosition.x,
                    Is.EqualTo(host.Explorer.Position.x).Within(0.01f));
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

        [Test]
        public void StaleSeranMapDialogueCannotWriteLettersAfterVoyageResetOrChangedLanding()
        {
            var root = new GameObject("M05 stale Seran map dialogue");
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            int letterWrites = 0;
            Action<string, int> write = (key, value) =>
            {
                if (key == M05LettersFacts.DecisionKey && value != 0)
                    letterWrites++;
                values[key] = value;
            };
            var firstLanding = new M05LandingReport(3, 10, true, true);
            Assert.That(M05LandingFacts.TryRecord(firstLanding, read, write), Is.True);
            RedSaltHarborExplorationHost host = null;
            try
            {
                host = new RedSaltHarborExplorationHost(root.transform, read, write);
                WalkTo(host, 50.5f, 26.5f);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.True);
                Assert.That(host.ActiveSeranView, Is.Not.Null);
                host.ActiveSeranView.TakeButton.onClick.Invoke();
                Assert.That(host.ActiveSeranView.SelectedDecision,
                    Is.EqualTo(M05LetterDecision.TakePacket));

                // A new visit invalidates the previously open UGUI action.
                // It must not silently assign old letters to a new voyage.
                Assert.That(ReturnOpeningContinuation.TryResetPreviousOutboundFacts(write),
                    Is.True);
                Assert.Throws<InvalidOperationException>(() =>
                    host.ActiveSeranView.ContinueButton.onClick.Invoke());
                Assert.That(read(M05LettersFacts.DecisionKey), Is.Zero);
                Assert.That(letterWrites, Is.Zero);
                Assert.That(host.IsDialogueOpen, Is.True,
                    "The refused callback leaves the choice visible without changing facts.");

                // Even if a different ship visit subsequently verifies a
                // different actual landing, this older choice stays stale.
                SeedCompletedCrossing(values);
                var secondLanding = new M05LandingReport(2, 0, false, false);
                Assert.That(M05LandingFacts.TryRecord(secondLanding, read, write), Is.True);
                Assert.Throws<InvalidOperationException>(() =>
                    host.ActiveSeranView.ContinueButton.onClick.Invoke());
                Assert.That(read(M05LettersFacts.DecisionKey), Is.Zero);
                Assert.That(letterWrites, Is.Zero);
                Assert.That(host.IsDialogueOpen, Is.True);
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
                Assert.That(host.HeroVisual.IsDisposed, Is.True);
                Assert.That(host.SeranVisual.IsDisposed, Is.True);
                Assert.That(host.RepairerVisual.IsDisposed, Is.True);
                Assert.That(host.FisherVisual.IsDisposed, Is.True);
                Assert.That(host.SaltVendorVisual.IsDisposed, Is.True);
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
