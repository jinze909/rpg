using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M06VoyageIdentityAndTornFactsTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedVerifiedM05Arrival(Dictionary<string, int> values)
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
            Assert.That(M05LettersFacts.TryRecord(
                M05LetterDecision.TakePacket, read, write), Is.True);
        }

        [Test]
        public void PumpReunionRefusesOpeningWhenAnyTornOrCorruptRescuePlanFactRemainsPersisted()
        {
            var root = new GameObject("M06 torn rescue plan gate");
            var values = new Dictionary<string, int>();
            SeedVerifiedM05Arrival(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            int validArrival = values[M05LandingFacts.ArrivalSequenceKey];

            var tornCases = new (string Key, int Value)[]
            {
                (M06RescuePlanFacts.ConfirmedKey, 2),
                (M06RescuePlanFacts.PendingSignatureKey, 1),
                (M06RescuePlanFacts.PlanKey, (int)M06RescuePlan.OfficialHandover),
                (M06RescuePlanFacts.UsableCapacityKey, 1),
                (M06RescuePlanFacts.LetterDeliveryKey, (int)M06LetterDeliveryOutcome.DeliveredWithPending),
                (M06RescuePlanFacts.RetaliationHeardKey, 1),
                (M06RescuePlanFacts.ShenResponseKey, (int)M06ShenResponse.LetHerSpeak)
            };

            try
            {
                foreach (var torn in tornCases)
                {
                    Assert.That(M06RescuePlanFacts.TryResetForNewVoyage(write), Is.True);
                    values[torn.Key] = torn.Value;

                    using (var host = new M06MineCampExplorationHost(root.transform, read, write))
                    {
                        Assert.That(host.TryPlaceAtWalkableTile(new Vector2Int(54, 36)), Is.True);
                        Assert.That(host.FindNearbyInteractableTriggerId(), Is.Null,
                            $"Pump reunion trigger should not be offered when '{torn.Key}' = {torn.Value}.");
                        Assert.That(host.TryInteractAtCurrentPosition(), Is.False);
                        Assert.That(host.ActiveReunionView, Is.Null);
                    }
                }

                Assert.That(M06RescuePlanFacts.TryResetForNewVoyage(write), Is.True);
                values[M05LandingFacts.ArrivalSequenceKey] = -1;
                using (var badArrivalHost = new M06MineCampExplorationHost(root.transform, read, write))
                {
                    Assert.That(badArrivalHost.TryPlaceAtWalkableTile(new Vector2Int(54, 36)), Is.True);
                    Assert.That(badArrivalHost.TryInteractAtCurrentPosition(), Is.False);
                    Assert.That(badArrivalHost.TryPlaceAtWalkableTile(new Vector2Int(42, 12)), Is.True);
                    Assert.That(badArrivalHost.TryInteractAtCurrentPosition(), Is.False);
                }

                values[M05LandingFacts.ArrivalSequenceKey] = validArrival;
                using (var cleanHost = new M06MineCampExplorationHost(root.transform, read, write))
                {
                    Assert.That(cleanHost.TryPlaceAtWalkableTile(new Vector2Int(54, 36)), Is.True);
                    Assert.That(cleanHost.FindNearbyInteractableTriggerId(), Is.EqualTo("m06.pump-reunion"));
                    Assert.That(cleanHost.TryInteractAtCurrentPosition(), Is.True);
                    Assert.That(cleanHost.ActiveReunionView, Is.Not.Null);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void AdvanceReunionToRescuePlanDecision(M06PumpRoomReunionView view)
        {
            view.GreetingBringBackButton.onClick.Invoke();
            view.AdvanceButton.onClick.Invoke();
            view.TaoJiLetterButton.onClick.Invoke();
            view.LedgerLetterButton.onClick.Invoke();
            view.ReturnedLetterButton.onClick.Invoke();
            view.AdvanceButton.onClick.Invoke();
            view.AdvanceButton.onClick.Invoke();
            view.ShenWaitButton.onClick.Invoke();
        }

        [Test]
        public void OpenReconAndReunionViewsRejectCommitAfterIdenticalVoyageReplay()
        {
            var root = new GameObject("M06 voyage identity guard");
            var values = new Dictionary<string, int>();
            SeedVerifiedM05Arrival(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            int firstArrival = values[M05LandingFacts.ArrivalSequenceKey];

            M06MineCampExplorationHost staleReconHost = null;
            M06MineCampExplorationHost staleReunionHost = null;
            M06MineCampExplorationHost currentHost = null;
            try
            {
                staleReconHost = new M06MineCampExplorationHost(root.transform, read, write);
                Assert.That(staleReconHost.TryPlaceAtWalkableTile(new Vector2Int(42, 12)), Is.True);
                Assert.That(staleReconHost.TryInteractAtCurrentPosition(), Is.True);
                var staleReconView = staleReconHost.ActiveReconView;

                staleReunionHost = new M06MineCampExplorationHost(root.transform, read, write);
                Assert.That(staleReunionHost.TryPlaceAtWalkableTile(new Vector2Int(54, 36)), Is.True);
                Assert.That(staleReunionHost.TryInteractAtCurrentPosition(), Is.True);
                var staleReunionView = staleReunionHost.ActiveReunionView;
                AdvanceReunionToRescuePlanDecision(staleReunionView);

                // Replay to a new voyage with identical M05 landing; ArrivalSequenceKey increments.
                Assert.That(M06RescuePlanFacts.TryResetForNewVoyage(write), Is.True);
                Assert.That(M06MineCampReconFacts.TryResetForNewVoyage(write), Is.True);
                Assert.That(M05LettersFacts.TryResetForNewVoyage(write), Is.True);
                Assert.That(M05LandingFacts.TryResetForNewVisit(write), Is.True);
                SeedVerifiedM05Arrival(values);
                Assert.That(values[M05LandingFacts.ArrivalSequenceKey], Is.GreaterThan(firstArrival));

                Assert.Throws<InvalidOperationException>(() =>
                    staleReconView.ConfirmButton.onClick.Invoke());
                Assert.That(M06MineCampReconFacts.TryRead(read, out int reconAfterStale), Is.True);
                Assert.That(reconAfterStale, Is.Zero);

                Assert.Throws<InvalidOperationException>(() =>
                    staleReunionView.OfficialHandoverButton.onClick.Invoke());
                Assert.That(M06RescuePlanFacts.TryRead(read, out M06ReunionRecord? planAfterStale), Is.True);
                Assert.That(planAfterStale.HasValue, Is.False);

                currentHost = new M06MineCampExplorationHost(root.transform, read, write);
                Assert.That(currentHost.TryPlaceAtWalkableTile(new Vector2Int(42, 12)), Is.True);
                Assert.That(currentHost.TryInteractAtCurrentPosition(), Is.True);
                currentHost.ActiveReconView.ConfirmButton.onClick.Invoke();
                Assert.That(currentHost.ObservedReconCount, Is.EqualTo(1));

                Assert.That(currentHost.TryPlaceAtWalkableTile(new Vector2Int(54, 36)), Is.True);
                Assert.That(currentHost.TryInteractAtCurrentPosition(), Is.True);
                AdvanceReunionToRescuePlanDecision(currentHost.ActiveReunionView);
                currentHost.ActiveReunionView.OfficialHandoverButton.onClick.Invoke();
                Assert.That(currentHost.RescuePlanSettled, Is.True);
            }
            finally
            {
                currentHost?.Dispose();
                staleReunionHost?.Dispose();
                staleReconHost?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
