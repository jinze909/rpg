using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M08WorkshopOrphanedRosterCaseTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedVerifiedM07(Dictionary<string, int> values)
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
        }

        [Test]
        public void WorkshopHostAndDepositFactsRejectOrphanedRC01ThroughRC05FactsUntilReset()
        {
            var root = new GameObject("M08WorkshopOrphanedRosterCasesRoot");
            var values = new Dictionary<string, int>();
            SeedVerifiedM07(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var completedWinch = new M08FirstWinchOrderSession();
            Assert.That(completedWinch.TryAcquirePart(M08WinchPart.ScrapGear), Is.True);
            Assert.That(completedWinch.TryAcquirePart(M08WinchPart.DockCable), Is.True);
            Assert.That(completedWinch.TryAssemble(), Is.True);
            Assert.That(completedWinch.TryTightenCable(), Is.True);
            Assert.That(completedWinch.TryTestLift(), Is.True);

            string[] orphanedRosterKeys =
            {
                RC01ChenYanFacts.ConfirmedKey,
                RC02ZhengYuanFacts.PendingSignatureKey,
                RC03BaijiMineFacts.ConfirmedKey,
                RC04DingYueReefFacts.PendingSignatureKey,
                RC05ThirdSquadFacts.ConfirmedKey
            };

            try
            {
                foreach (string key in orphanedRosterKeys)
                {
                    values[key] = 1;
                    Assert.That(M08WorkshopDepositFacts.HasOrphanedDownstreamFacts(read), Is.True,
                        $"Orphaned roster key '{key}' must block M08 workshop deposit.");
                    Assert.That(M08WorkshopDepositFacts.TryConfirm(completedWinch, read, write), Is.False);
                    using (var host = new M08WorkshopOrderHost(root.transform, read, write))
                    {
                        Assert.That(host.TryOpen(), Is.False);
                        Assert.That(host.ActiveView, Is.Null);
                    }
                    values[key] = 0;
                }

                Assert.That(M08WorkshopDepositFacts.HasOrphanedDownstreamFacts(read), Is.False);
                using (var cleanHost = new M08WorkshopOrderHost(root.transform, read, write))
                {
                    Assert.That(cleanHost.TryOpen(), Is.True);
                    Assert.That(cleanHost.ActiveView, Is.Not.Null);
                    Assert.That(M08WorkshopDepositFacts.TryConfirm(completedWinch, read, write), Is.True);
                    Assert.That(cleanHost.IsCompleted, Is.True);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
