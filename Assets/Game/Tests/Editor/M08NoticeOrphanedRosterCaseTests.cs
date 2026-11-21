using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M08NoticeOrphanedRosterCaseTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedVerifiedM08Winch(Dictionary<string, int> values)
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
        }

        [Test]
        public void M08NoticeHostAndFactsRejectOrphanedRC01ThroughRC05FactsUntilReset()
        {
            var root = new GameObject("M08NoticeOrphanedRosterCasesRoot");
            var values = new Dictionary<string, int>();
            SeedVerifiedM08Winch(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            string[] orphanedRosterKeys =
            {
                RC01ChenYanFacts.ConfirmedKey,
                RC01ChenYanFacts.PendingSignatureKey,
                RC02ZhengYuanFacts.ConfirmedKey,
                RC02ZhengYuanFacts.PendingSignatureKey,
                RC03BaijiMineFacts.ConfirmedKey,
                RC03BaijiMineFacts.PendingSignatureKey,
                RC04DingYueReefFacts.ConfirmedKey,
                RC04DingYueReefFacts.PendingSignatureKey,
                RC05ThirdSquadFacts.ConfirmedKey,
                RC05ThirdSquadFacts.PendingSignatureKey
            };

            try
            {
                foreach (string orphanKey in orphanedRosterKeys)
                {
                    values[orphanKey] = 1;
                    Assert.That(M08FirstWorldNoticeFacts.HasOrphanedRosterCaseFacts(read), Is.True,
                        $"Orphaned key '{orphanKey}' must be detected.");
                    Assert.That(M08FirstWorldNoticeFacts.TryRecord(true, read, write), Is.False);
                    using (var host = new M08FirstWorldNoticeHost(root.transform, () => true, read, write))
                    {
                        Assert.That(host.TryOpen(), Is.False);
                        Assert.That(host.ActiveView, Is.Null);
                    }
                    values[orphanKey] = 0;
                }

                Assert.That(M08FirstWorldNoticeFacts.HasOrphanedRosterCaseFacts(read), Is.False);
                using (var cleanHost = new M08FirstWorldNoticeHost(root.transform, () => true, read, write))
                {
                    Assert.That(cleanHost.TryOpen(), Is.True);
                    cleanHost.ActiveView.AcknowledgeButton.onClick.Invoke();
                    Assert.That(cleanHost.NoticeRead, Is.True);
                    Assert.That(cleanHost.ActiveView, Is.Null);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
