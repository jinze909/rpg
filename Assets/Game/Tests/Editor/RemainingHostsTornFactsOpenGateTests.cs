using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    /// <summary>
    /// Verifies that M13, M15, RC-02, RC-03, and ST-06 hosts refuse to open a
    /// fresh encounter when torn/partial payload keys remain persisted with
    /// ConfirmedKey == 0, and open cleanly once explicitly reset.
    /// </summary>
    public sealed class RemainingHostsTornFactsOpenGateTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedThroughM08NoticeWithTaoJi(Dictionary<string, int> values)
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
                taoJiRescued: true), read, write), Is.True);

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
        public void M13M15RC02RC03AndST06HostsRejectTornPayloadKeysUntilReset()
        {
            var root = new GameObject("RemainingHostsTornGateRoot");
            var values = new Dictionary<string, int>();
            SeedThroughM08NoticeWithTaoJi(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                // M13 torn RouteKey blocks open until reset.
                values[M13DepartureRiskFacts.RouteKey] = (int)M13ApproachRoute.RouteASupervisedCivilian;
                using (var m13 = new M13DepartureRiskHost(
                    root.transform, () => true, () => true, () => true, read, write))
                {
                    Assert.That(m13.TryOpen(), Is.False);
                    Assert.That(M13DepartureRiskFacts.TryResetForNewVoyage(write), Is.True);
                    Assert.That(m13.TryOpen(), Is.True);
                }

                // M15 torn ReaderKey blocks open until reset.
                values[M15FinalRollCallFacts.ReaderKey] = (int)M15RollCallReader.Protagonist;
                using (var m15 = new M15FinalRollCallHost(
                    root.transform, () => true, () => false, () => 12, () => null, read, write))
                {
                    Assert.That(m15.TryOpen(), Is.False);
                    Assert.That(M15FinalRollCallFacts.TryResetForNewVoyage(write), Is.True);
                    Assert.That(m15.TryOpen(), Is.True);
                }

                // RC-02 torn ResolutionKey blocks open until reset.
                values[RC02ZhengYuanFacts.ResolutionKey] = (int)RC02Resolution.ExposeImpostor;
                using (var rc02 = new RC02ZhengYuanHost(
                    root.transform, () => true, () => false, () => true, read, write))
                {
                    Assert.That(rc02.TryOpen(), Is.False);
                    Assert.That(RC02ZhengYuanFacts.TryResetForNewVoyage(write), Is.True);
                    Assert.That(rc02.TryOpen(), Is.True);
                }

                // RC-03 torn ResolutionKey blocks open until reset.
                values[RC03BaijiMineFacts.ResolutionKey] = (int)RC03Resolution.SecretlyEvacuateLanRuo;
                using (var rc03 = new RC03BaijiMineHost(
                    root.transform, () => true, () => true, () => true, () => false, read, write))
                {
                    Assert.That(rc03.TryOpen(), Is.False);
                    Assert.That(RC03BaijiMineFacts.TryResetForNewVoyage(write), Is.True);
                    Assert.That(rc03.TryOpen(), Is.True);
                }

                // ST-06 torn ChairResolutionKey blocks open until reset.
                values[ST06TaoJiHomecomingFacts.ChairResolutionKey] =
                    (int)ST06ChairResolution.RefundAndRebuild;
                using (var st06 = new ST06TaoJiHomecomingHost(
                    root.transform, () => true, read, write))
                {
                    Assert.That(st06.TryOpen(), Is.False);
                    Assert.That(ST06TaoJiHomecomingFacts.TryResetForNewVoyage(write), Is.True);
                    Assert.That(st06.TryOpen(), Is.True);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
