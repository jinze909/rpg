using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M06MineCampExplorationHostTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedVerifiedM05Arrival(
            Dictionary<string, int> values,
            M05LetterDecision? letterDecision = M05LetterDecision.TakePacket)
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
                new[] { new FogListeningCue(FogBearing.Ahead, FogCueKind.ClearWater, 80) },
                maxListenFocus: 1, maxHullIntegrity: 2);
            fog.ApplyHelmAction(FogHelmAction.HoldCourse);
            Assert.That(FogListeningFacts.TryRecord(fog, write), Is.True);

            Assert.That(M05LandingFacts.TryRecord(
                new M05LandingReport(3, 10, true, true), read, write), Is.True);
            if (letterDecision.HasValue)
                Assert.That(M05LettersFacts.TryRecord(letterDecision.Value, read, write), Is.True);
        }

        [Test]
        public void HostRequiresVerifiedM05LandingToOpenReconOrReunion()
        {
            var root = new GameObject("M06 host unverified landing");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M06MineCampExplorationHost host = null;
            try
            {
                host = new M06MineCampExplorationHost(root.transform, read, write);
                Assert.That(host.TryPlaceAtWalkableTile(new Vector2Int(42, 12)), Is.True);
                Assert.That(host.FindNearbyInteractableTriggerId(), Is.Null);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.False);

                Assert.That(host.TryPlaceAtWalkableTile(new Vector2Int(54, 36)), Is.True);
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
        public void WalkingBetweenDistrictsClearsStaleNameInCorridorAndBlocksWalls()
        {
            var root = new GameObject("M06 host movement");
            var values = new Dictionary<string, int>();
            SeedVerifiedM05Arrival(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            string previousLanguage = SoundwellUiLocalization.Language;
            M06MineCampExplorationHost host = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                host = new M06MineCampExplorationHost(root.transform, read, write);
                Assert.That(host.CurrentDistrict.HasValue, Is.True);
                Assert.That(host.CurrentDistrict.Value.Id, Is.EqualTo("minecamp.south-gate"));
                Assert.That(host.DistrictBanner.text, Does.Contain("矿营入山口"));

                // Place into connector corridor between SouthGate and EscortCheckpost.
                Assert.That(host.TryPlaceAtWalkableTile(new Vector2Int(30, 12)), Is.True);
                Assert.That(host.CurrentDistrict.HasValue, Is.False);
                Assert.That(host.DistrictBanner.text, Does.Contain("赤盐矿营连廊"));

                // Walk east into EscortCheckpost.
                for (int i = 0; i < 12; i++)
                    host.StepMovement(Vector2.right, 0.1f);
                Assert.That(host.CurrentDistrict.HasValue, Is.True);
                Assert.That(host.CurrentDistrict.Value.Id, Is.EqualTo("minecamp.escort-checkpost"));

                // Walking into solid outer wall at (0,0) is rejected.
                Assert.That(host.TryPlaceAtWalkableTile(new Vector2Int(0, 0)), Is.False);
            }
            finally
            {
                host?.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InspectingReconLeadsAtTheirPhysicalTriggersPersistsAcrossHostReload()
        {
            var root = new GameObject("M06 host recon");
            var values = new Dictionary<string, int>();
            SeedVerifiedM05Arrival(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M06MineCampExplorationHost host = null;
            try
            {
                host = new M06MineCampExplorationHost(root.transform, read, write);
                Assert.That(host.ObservedReconCount, Is.Zero);

                Assert.That(host.TryPlaceAtWalkableTile(new Vector2Int(42, 12)), Is.True);
                Assert.That(host.FindNearbyInteractableTriggerId(), Is.EqualTo("m06.recon.escort-schedule"));
                Assert.That(host.TryInteractAtCurrentPosition(), Is.True);
                Assert.That(host.ActiveReconView, Is.Not.Null);
                Assert.That(host.StepMovement(Vector2.right, 0.2f), Is.False,
                    "Movement must lock while a dialogue view is open.");

                host.ActiveReconView.ConfirmButton.onClick.Invoke();
                Assert.That(host.IsDialogueOpen, Is.False);
                Assert.That(host.ObservedReconCount, Is.EqualTo(1));
                Assert.That(host.TryInteractAtCurrentPosition(), Is.False,
                    "Already inspected lead must not re-open at the same anchor.");

                // Reload host and verify persistence.
                host.Dispose();
                host = new M06MineCampExplorationHost(root.transform, read, write);
                Assert.That(host.ObservedReconCount, Is.EqualTo(1));
                Assert.That(host.TryPlaceAtWalkableTile(new Vector2Int(42, 12)), Is.True);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void StaleReconViewCannotSaveIntoIdenticalSecondVoyage()
        {
            var root = new GameObject("M06 old recon view versus next voyage");
            var values = new Dictionary<string, int>();
            SeedVerifiedM05Arrival(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            M06MineCampExplorationHost host = null;
            try
            {
                host = new M06MineCampExplorationHost(root.transform, read, write);
                Assert.That(host.TryPlaceAtWalkableTile(new Vector2Int(42, 12)), Is.True);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.True);
                var oldView = host.ActiveReconView;
                int oldSequence = read(M05LandingFacts.ArrivalSequenceKey);

                Assert.That(ReturnOpeningContinuation.TryResetPreviousOutboundFacts(write),
                    Is.True);
                SeedVerifiedM05Arrival(values);
                Assert.That(read(M05LandingFacts.ArrivalSequenceKey),
                    Is.GreaterThan(oldSequence));
                Assert.Throws<InvalidOperationException>(() =>
                    oldView.ConfirmButton.onClick.Invoke());
                Assert.That(read(M06MineCampReconFacts.ObservedMaskKey), Is.Zero);
                Assert.That(host.ActiveReconView, Is.SameAs(oldView),
                    "A rejected stale save leaves the old conversation retryable.");

                host.Dispose();
                host = new M06MineCampExplorationHost(root.transform, read, write);
                Assert.That(host.TryPlaceAtWalkableTile(new Vector2Int(42, 12)), Is.True);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.True);
                host.ActiveReconView.ConfirmButton.onClick.Invoke();
                Assert.That(read(M06MineCampReconFacts.ObservedMaskKey), Is.EqualTo(1));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void WalkingToPumpHallCompletesReunionAndSettlesRescuePlan()
        {
            var root = new GameObject("M06 host reunion");
            var values = new Dictionary<string, int>();
            SeedVerifiedM05Arrival(values, letterDecision: M05LetterDecision.TakePacket);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M06MineCampExplorationHost host = null;
            try
            {
                host = new M06MineCampExplorationHost(root.transform, read, write);
                Assert.That(host.RescuePlanSettled, Is.False);

                Assert.That(host.TryPlaceAtWalkableTile(new Vector2Int(54, 36)), Is.True);
                Assert.That(host.FindNearbyInteractableTriggerId(), Is.EqualTo("m06.pump-reunion"));
                Assert.That(host.TryInteractAtCurrentPosition(), Is.True);
                Assert.That(host.ActiveReunionView, Is.Not.Null);

                host.ActiveReunionView.GreetingBringBackButton.onClick.Invoke();
                host.ActiveReunionView.AdvanceButton.onClick.Invoke();
                host.ActiveReunionView.AdvanceButton.onClick.Invoke();
                host.ActiveReunionView.AdvanceButton.onClick.Invoke();
                Assert.That(host.ActiveReunionView.Session.Stage, Is.EqualTo(M06ReunionStage.ShenResponse));
                host.ActiveReunionView.ShenWaitButton.onClick.Invoke();
                host.ActiveReunionView.OfficialHandoverButton.onClick.Invoke();

                Assert.That(host.IsDialogueOpen, Is.False);
                Assert.That(host.RescuePlanSettled, Is.True);
                Assert.That(M06RescuePlanFacts.TryRead(read, out M06ReunionRecord? saved), Is.True);
                Assert.That(saved.HasValue, Is.True);
                Assert.That(saved.Value.Plan, Is.EqualTo(M06RescuePlan.OfficialHandover));
                Assert.That(host.TryInteractAtCurrentPosition(), Is.False,
                    "Settled reunion must not re-open.");
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PumpRoomViewRetriesCommittedPlanAfterLostAcknowledgement()
        {
            var root = new GameObject("M06 pump plan lost ack");
            var values = new Dictionary<string, int>();
            SeedVerifiedM05Arrival(values, letterDecision: null);
            Func<string, int> read = Reader(values);
            int committed = 0;
            Action<string, int> write = (key, value) =>
            {
                values[key] = value;
                if (key == M06RescuePlanFacts.ConfirmedKey && value == 1 && ++committed == 1)
                    throw new InvalidOperationException("Lost acknowledgement");
            };
            M06MineCampExplorationHost host = null;
            try
            {
                host = new M06MineCampExplorationHost(root.transform, read, write);
                Assert.That(host.TryPlaceAtWalkableTile(new Vector2Int(54, 36)), Is.True);
                Assert.That(host.TryInteractAtCurrentPosition(), Is.True);
                var view = host.ActiveReunionView;
                view.GreetingBringBackButton.onClick.Invoke();
                view.AdvanceButton.onClick.Invoke();
                view.AdvanceButton.onClick.Invoke();
                view.ShenWaitButton.onClick.Invoke();
                Assert.That(view.Session.Stage, Is.EqualTo(M06ReunionStage.RescuePlanDecision));
                Assert.Throws<InvalidOperationException>(() =>
                    view.OfficialHandoverButton.onClick.Invoke());
                Assert.That(host.IsDialogueOpen, Is.True);
                Assert.That(host.RescuePlanSettled, Is.True);
                view.OfficialHandoverButton.onClick.Invoke();
                Assert.That(host.IsDialogueOpen, Is.False);
                Assert.That(view.Session.Stage, Is.EqualTo(M06ReunionStage.Completed));
                Assert.That(committed, Is.EqualTo(1));
                Assert.That(host.TryInteractAtCurrentPosition(), Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
