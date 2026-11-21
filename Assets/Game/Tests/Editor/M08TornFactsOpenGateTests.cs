using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M08TornFactsOpenGateTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedThroughM07Extraction(Dictionary<string, int> values)
        {
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;

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
            Assert.That(M06RescuePlanFacts.TryRecord(
                new M06ReunionRecord(M06RescuePlan.OfficialHandover, true,
                    M06LetterDeliveryOutcome.NotCarried, true), read, write), Is.True);
            Assert.That(M07ExtractionFacts.TryRecord(
                new M07ExtractionRecord(
                    M07ExecutedRoute.OfficialHandover,
                    M07PumpShutdownState.KeptRunning,
                    M07RouteCostFlags.HarborSupplyDebt,
                    boardedFirstBatchCount: 10,
                    lowerLevelEvacuatedCount: 0,
                    M07RusFate.Arrested, M07LedgerState.Preserved,
                    M07LiangChoice.StayOnIsland,
                    M07YidaChoice.HandToInspector,
                    taoJiRescued: false), read, write), Is.True);
        }

        private static void SeedConfirmedWorkshop(Dictionary<string, int> values)
        {
            SeedThroughM07Extraction(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;

            var winch = new M08FirstWinchOrderSession();
            Assert.That(winch.TryAcquirePart(M08WinchPart.ScrapGear), Is.True);
            Assert.That(winch.TryAcquirePart(M08WinchPart.DockCable), Is.True);
            Assert.That(winch.TryAssemble(), Is.True);
            Assert.That(winch.TryTightenCable(), Is.True);
            Assert.That(winch.TryTestLift(), Is.True);
            Assert.That(M08WorkshopDepositFacts.TryConfirm(winch, read, write), Is.True);
        }

        [Test]
        public void DockTruthHostRefusesOpeningWhenTornResponseOrCorruptConfirmedKeyPersists()
        {
            var root = new GameObject("M08 dock torn facts gate");
            var values = new Dictionary<string, int>();
            SeedConfirmedWorkshop(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var tornCases = new (string Key, int Value)[]
            {
                (M08DockTruthFacts.ShenRelationResponseKey, (int)M08DockTruthResponse.StaySilent),
                (M08DockTruthFacts.ShenRelationResponseKey, 99),
                (M08DockTruthFacts.ConfirmedKey, 2)
            };

            try
            {
                foreach (var torn in tornCases)
                {
                    Assert.That(M08DockTruthFacts.TryResetForNewVoyage(write), Is.True);
                    values[torn.Key] = torn.Value;

                    using (var host = new M08DockTruthHost(root.transform, () => true, read, write))
                    {
                        Assert.That(host.TryOpen(), Is.False,
                            $"M08DockTruthHost should refuse opening when '{torn.Key}' = {torn.Value} persists.");
                        Assert.That(host.ActiveView, Is.Null);
                    }
                }

                Assert.That(M08DockTruthFacts.TryResetForNewVoyage(write), Is.True);
                using (var cleanHost = new M08DockTruthHost(root.transform, () => true, read, write))
                {
                    Assert.That(cleanHost.TryOpen(), Is.True);
                    Assert.That(cleanHost.ActiveView, Is.Not.Null);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void WorkshopOrderHostRefusesOpeningWhenCompletedKeyCorruptedOrArrivalSequenceNegative()
        {
            var root = new GameObject("M08 workshop torn facts gate");
            var values = new Dictionary<string, int>();
            SeedThroughM07Extraction(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            int validArrivalSequence = values[M05LandingFacts.ArrivalSequenceKey];

            try
            {
                foreach (int badCompleted in new[] { 2, -1 })
                {
                    values[M08WorkshopDepositFacts.CompletedKey] = badCompleted;
                    using (var host = new M08WorkshopOrderHost(root.transform, read, write))
                    {
                        Assert.That(host.TryOpen(), Is.False,
                            $"M08WorkshopOrderHost should refuse opening when CompletedKey = {badCompleted}.");
                        Assert.That(host.ActiveView, Is.Null);
                    }
                }

                Assert.That(M08WorkshopDepositFacts.TryResetForNewVoyage(write), Is.True);
                values[M05LandingFacts.ArrivalSequenceKey] = -1;
                using (var badArrivalHost = new M08WorkshopOrderHost(root.transform, read, write))
                {
                    Assert.That(badArrivalHost.TryOpen(), Is.False);
                    Assert.That(badArrivalHost.ActiveView, Is.Null);
                }

                values[M05LandingFacts.ArrivalSequenceKey] = validArrivalSequence;
                using (var cleanHost = new M08WorkshopOrderHost(root.transform, read, write))
                {
                    Assert.That(cleanHost.TryOpen(), Is.True);
                    Assert.That(cleanHost.ActiveView, Is.Not.Null);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
