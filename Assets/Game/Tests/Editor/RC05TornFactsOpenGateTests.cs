using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    /// <summary>
    /// Verifies that RC05ThirdSquadHost refuses to open a fresh encounter when
    /// any torn/partial RC-05 payload key remains persisted with ConfirmedKey == 0,
    /// and opens cleanly once explicitly reset for a new voyage.
    /// </summary>
    public sealed class RC05TornFactsOpenGateTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedThroughM08Notice(Dictionary<string, int> values)
        {
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

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
                new M06ReunionRecord(
                    M06RescuePlan.OfficialHandover, true,
                    M06LetterDeliveryOutcome.NotCarried, true), read, write), Is.True);
            Assert.That(M07ExtractionFacts.TryRecord(new M07ExtractionRecord(
                M07ExecutedRoute.OfficialHandover,
                M07PumpShutdownState.KeptRunning,
                M07RouteCostFlags.HarborSupplyDebt,
                boardedFirstBatchCount: 10,
                lowerLevelEvacuatedCount: 0,
                M07RusFate.Arrested, M07LedgerState.Preserved,
                M07LiangChoice.StayOnIsland,
                M07YidaChoice.HandToInspector,
                taoJiRescued: false), read, write), Is.True);

            var winch = new M08FirstWinchOrderSession();
            Assert.That(winch.TryAcquirePart(M08WinchPart.ScrapGear), Is.True);
            Assert.That(winch.TryAcquirePart(M08WinchPart.DockCable), Is.True);
            Assert.That(winch.TryAssemble(), Is.True);
            Assert.That(winch.TryTightenCable(), Is.True);
            Assert.That(winch.TryTestLift(), Is.True);
            Assert.That(M08WorkshopDepositFacts.TryConfirm(winch, read, write), Is.True);
            Assert.That(M08FirstWorldNoticeFacts.TryRecord(true, read, write), Is.True);
        }

        [Test]
        public void RC05HostRejectsEveryTornPayloadKeyUntilCleanReset()
        {
            var root = new GameObject("RC05TornGateRoot");
            var values = new Dictionary<string, int>();
            SeedThroughM08Notice(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            string[] tornKeys =
            {
                RC05ThirdSquadFacts.PendingSignatureKey,
                RC05ThirdSquadFacts.TrialDeliveryKey,
                RC05ThirdSquadFacts.ResolutionKey,
                RC05ThirdSquadFacts.SkirmishKey,
                RC05ThirdSquadFacts.RealZhengYuanVerifiedKey,
                RC05ThirdSquadFacts.OldWomanStoneMentionedKey
            };

            try
            {
                foreach (string key in tornKeys)
                {
                    Assert.That(RC05ThirdSquadFacts.TryResetForNewVoyage(write), Is.True);
                    values[key] = 1;
                    using (var host = new RC05ThirdSquadHost(
                        root.transform, () => true, () => false, () => true,
                        () => true, () => true, () => false, read, write))
                    {
                        Assert.That(host.TryOpen(), Is.False, $"Expected torn key '{key}' to block open.");
                    }
                }

                Assert.That(RC05ThirdSquadFacts.TryResetForNewVoyage(write), Is.True);
                using (var cleanHost = new RC05ThirdSquadHost(
                    root.transform, () => true, () => false, () => true,
                    () => true, () => true, () => false, read, write))
                {
                    Assert.That(cleanHost.TryOpen(), Is.True);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
