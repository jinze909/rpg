using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M07VoyageIdentityAndTornFactsTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedVerifiedM06(
            Dictionary<string, int> values,
            M06RescuePlan plan)
        {
            Action<string, int> write = (key, value) => values[key] = value;
            Func<string, int> read = Reader(values);

            var manifest = new M04DepartureManifest(
                M03VoyageChoice.QiQueMerchant,
                "Confirmed hull", "A Yuan", 4, "Shallow tender",
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
            foreach (M06FamilyLetterRecipient recipient in new[]
            {
                M06FamilyLetterRecipient.TaoJi,
                M06FamilyLetterRecipient.LedgerMiner,
                M06FamilyLetterRecipient.PendingMiner
            })
                Assert.That(M06FamilyLetterDeliveryFacts.TryRecord(
                    recipient, read, write), Is.True);
            Assert.That(M06RescuePlanFacts.TryRecord(
                new M06ReunionRecord(
                    plan, true, M06LetterDeliveryOutcome.DeliveredWithPending, true),
                read, write), Is.True);
        }

        private static void AdvanceToDockRollCall(M07ExtractionNightView view)
        {
            view.ExecutePlanButton.onClick.Invoke();
            view.AdvanceButton.onClick.Invoke();
            view.ArrestRusButton.onClick.Invoke();
            view.AdvanceButton.onClick.Invoke();
        }

        [Test]
        public void HostRefusesOpeningWhenAnyUnconfirmedOrTornExtractionFactKeyRemainsPersisted()
        {
            var root = new GameObject("M07 torn facts gate");
            var values = new Dictionary<string, int>();
            SeedVerifiedM06(values, M06RescuePlan.OfficialHandover);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            int validArrivalSequence = values[M05LandingFacts.ArrivalSequenceKey];

            var tornCases = new (string Key, int Value)[]
            {
                (M07ExtractionFacts.ConfirmedKey, 2),
                (M07ExtractionFacts.ExecutedRouteKey, (int)M07ExecutedRoute.OfficialHandover),
                (M07ExtractionFacts.PumpStateKey, (int)M07PumpShutdownState.SafelyStoppedThreeHours),
                (M07ExtractionFacts.RouteCostsKey, (int)M07RouteCostFlags.HarborSupplyDebt),
                (M07ExtractionFacts.BoardedFirstBatchKey, 180),
                (M07ExtractionFacts.LowerLevelEvacuatedKey, 500),
                (M07ExtractionFacts.RusFateKey, (int)M07RusFate.Arrested),
                (M07ExtractionFacts.LedgerStateKey, (int)M07LedgerState.Preserved),
                (M07ExtractionFacts.LiangChoiceKey, (int)M07LiangChoice.StayOnIsland),
                (M07ExtractionFacts.YidaChoiceKey, (int)M07YidaChoice.HandToInspector),
                (M07ExtractionFacts.TaoJiRescuedKey, 1)
            };

            try
            {
                foreach (var torn in tornCases)
                {
                    Assert.That(M07ExtractionFacts.TryResetForNewVoyage(write), Is.True);
                    values[torn.Key] = torn.Value;

                    using (var host = new M07ExtractionNightHost(
                        root.transform, () => true, () => true, read, write))
                    {
                        Assert.That(host.TryOpen(), Is.False,
                            $"M07ExtractionNightHost should refuse opening when '{torn.Key}' = {torn.Value} persists.");
                        Assert.That(host.ActiveView, Is.Null);
                        Assert.That(host.Session, Is.Null);
                    }
                }

                Assert.That(M07ExtractionFacts.TryResetForNewVoyage(write), Is.True);
                values[M05LandingFacts.ArrivalSequenceKey] = -1;
                using (var badArrivalHost = new M07ExtractionNightHost(
                    root.transform, () => true, () => true, read, write))
                {
                    Assert.That(badArrivalHost.TryOpen(), Is.False);
                    Assert.That(badArrivalHost.ActiveView, Is.Null);
                }

                values[M05LandingFacts.ArrivalSequenceKey] = validArrivalSequence;
                using (var cleanHost = new M07ExtractionNightHost(
                    root.transform, () => true, () => true, read, write))
                {
                    Assert.That(cleanHost.TryOpen(), Is.True);
                    Assert.That(cleanHost.ActiveView, Is.Not.Null);
                    Assert.That(cleanHost.Session, Is.Not.Null);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HostRejectsStaleRollCallCommitAfterVoyageReplayWithIdenticalM06Plan()
        {
            var root = new GameObject("M07 voyage identity guard");
            var values = new Dictionary<string, int>();
            SeedVerifiedM06(values, M06RescuePlan.OfficialHandover);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            int firstArrival = values[M05LandingFacts.ArrivalSequenceKey];

            M07ExtractionNightHost staleHost = null;
            M07ExtractionNightHost currentHost = null;
            try
            {
                staleHost = new M07ExtractionNightHost(
                    root.transform, () => true, () => true, read, write);
                Assert.That(staleHost.TryOpen(), Is.True);
                var staleView = staleHost.ActiveView;
                AdvanceToDockRollCall(staleView);

                // Replay to a new voyage with identical M06 rescue plan; M05 arrival sequence increments.
                Assert.That(M07ExtractionFacts.TryResetForNewVoyage(write), Is.True);
                Assert.That(M06RescuePlanFacts.TryResetForNewVoyage(write), Is.True);
                Assert.That(M05LettersFacts.TryResetForNewVoyage(write), Is.True);
                Assert.That(M05LandingFacts.TryResetForNewVisit(write), Is.True);
                SeedVerifiedM06(values, M06RescuePlan.OfficialHandover);
                Assert.That(values[M05LandingFacts.ArrivalSequenceKey], Is.GreaterThan(firstArrival));

                staleView.LiangStayButton.onClick.Invoke();
                Assert.That(staleHost.IsCompleted, Is.False);
                Assert.That(M07ExtractionFacts.TryRead(read, out M07ExtractionRecord? staleSaved), Is.True);
                Assert.That(staleSaved.HasValue, Is.False);

                currentHost = new M07ExtractionNightHost(
                    root.transform, () => true, () => false, read, write);
                Assert.That(currentHost.TryOpen(), Is.True);
                AdvanceToDockRollCall(currentHost.ActiveView);
                currentHost.ActiveView.LiangPersuadeButton.onClick.Invoke();
                Assert.That(currentHost.IsCompleted, Is.True);
                Assert.That(currentHost.ActiveView, Is.Null);
                Assert.That(M07ExtractionFacts.TryRead(read, out M07ExtractionRecord? currentSaved), Is.True);
                Assert.That(currentSaved.HasValue, Is.True);
                Assert.That(currentSaved.Value.LiangChoice, Is.EqualTo(M07LiangChoice.PersuadedToLeave));
            }
            finally
            {
                currentHost?.Dispose();
                staleHost?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
