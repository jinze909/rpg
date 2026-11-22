using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ReturnRosterCasesHubTornFactsTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static ReturnRosterCasesHubContext ObservedScenes()
        {
            return new ReturnRosterCasesHubContext
            {
                AtWhiteReedFerry = () => true,
                AtIntakeDesk = () => true,
                ShiLengWillingForAdoption = () => true,
                OnBaijiIsland = () => true,
                DiagramMatchedAtPumpTwo = () => true,
                SuccessorTrainingConfirmed = () => true,
                LanRuoWillingToStayOneYear = () => true,
                AtReefListeningPoint = () => true,
                CanApproachReef = () => true,
                VerifiedReefSceneConditions = _ => true,
                AtAbandonedMill = () => true,
                BoatmanCountMatched = () => true,
                SquadRosterChecked = () => true,
                ZhengYuanTestimonyConfirmed = () => true
            };
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
        public void HubMarksTornOrCorruptCasesUnavailableWithoutClosingActiveSettledCase()
        {
            var root = new GameObject("RosterHubTornFactsRoot");
            var values = new Dictionary<string, int>();
            SeedThroughM08Notice(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var tornCases = new (ReturnRosterCaseId CaseId, string Key)[]
            {
                (ReturnRosterCaseId.RC01ChenYan, RC01ChenYanFacts.PendingSignatureKey),
                (ReturnRosterCaseId.RC02ZhengYuan, RC02ZhengYuanFacts.PendingSignatureKey),
                (ReturnRosterCaseId.RC03BaijiMine, RC03BaijiMineFacts.PendingSignatureKey),
                (ReturnRosterCaseId.RC04DingYueReef, RC04DingYueReefFacts.PendingSignatureKey),
                (ReturnRosterCaseId.RC05ThirdSquad, RC05ThirdSquadFacts.PendingSignatureKey)
            };

            try
            {
                using (var hub = new ReturnRosterCasesHubHost(
                    root.transform, ObservedScenes(), read, write))
                {
                    foreach (var torn in tornCases)
                    {
                        values[torn.Key] = 1;
                        Assert.That(hub.IsCaseSettled(torn.CaseId), Is.False, torn.Key);
                        Assert.That(hub.IsCaseAvailable(torn.CaseId), Is.False, torn.Key);
                        Assert.That(hub.TryOpenCase(torn.CaseId), Is.False, torn.Key);
                        values[torn.Key] = 0;
                        Assert.That(hub.IsCaseAvailable(torn.CaseId), Is.True, torn.Key);
                    }

                    // Open and settle RC-01, then attempt opening a torn RC-03:
                    // hub must fail closed without prematurely disposing active RC-01.
                    Assert.That(hub.TryOpenCase(ReturnRosterCaseId.RC01ChenYan), Is.True);
                    var rc01Host = hub.ActiveRC01Host;
                    Assert.That(RC01ChenYanFacts.TryRecord(new RC01ChenYanRecord(
                        RC01FerryClues.All, RC01TalkOrder.LinSuFirst,
                        RC01ChenYanOutcome.KeepMarkedDead, true), read, write), Is.True);

                    values[RC03BaijiMineFacts.ResolutionKey] = (int)RC03Resolution.KeepOldCharter;
                    Assert.That(hub.IsCaseAvailable(ReturnRosterCaseId.RC03BaijiMine), Is.False);
                    Assert.That(hub.TryOpenCase(ReturnRosterCaseId.RC03BaijiMine), Is.False);
                    Assert.That(hub.ActiveRC01Host, Is.SameAs(rc01Host));
                    Assert.That(rc01Host.IsDisposed, Is.False);

                    Assert.That(RC03BaijiMineFacts.TryResetForNewVoyage(write), Is.True);
                    Assert.That(hub.IsCaseAvailable(ReturnRosterCaseId.RC03BaijiMine), Is.True);
                    Assert.That(hub.TryOpenCase(ReturnRosterCaseId.RC03BaijiMine), Is.True);
                    Assert.That(hub.ActiveCase, Is.EqualTo(ReturnRosterCaseId.RC03BaijiMine));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
