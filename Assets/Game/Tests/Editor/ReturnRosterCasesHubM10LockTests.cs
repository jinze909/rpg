using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ReturnRosterCasesHubM10LockTests
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

        private static void SeedThroughM09WithM08Notice(Dictionary<string, int> values)
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
            Assert.That(M09BerthTableFacts.TryRecord(new M09BerthTableRecord(
                20, 23, M09BerthExchangeFlags.HanQiaoCivilianVoyage,
                M09RuiyaOutcome.Rescued, false, false), read, write), Is.True);
        }

        [Test]
        public void HubMarksRC02AndRC05UnavailableAfterM10OrOrphanedAct3WhileKeepingOtherCasesAvailable()
        {
            var root = new GameObject("RosterHubM10LockRoot");
            var values = new Dictionary<string, int>();
            SeedThroughM09WithM08Notice(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                using (var hub = new ReturnRosterCasesHubHost(
                    root.transform, ObservedScenes(), read, write))
                {
                    Assert.That(hub.IsCaseAvailable(ReturnRosterCaseId.RC02ZhengYuan), Is.True);
                    Assert.That(hub.IsCaseAvailable(ReturnRosterCaseId.RC05ThirdSquad), Is.True);

                    Assert.That(M10AssemblyFacts.TryRecord(new M10AssemblyRecord(
                        true, M10RosterAccuracy.VerifiedFirstPass,
                        M10BattleReportSource.CarriedFromM02,
                        M10HanLedgerUse.PublishDraft,
                        M10SupplyContractStatus.SignedFullCoverage,
                        false, false, true), read, write), Is.True);

                    Assert.That(hub.IsCaseAvailable(ReturnRosterCaseId.RC02ZhengYuan), Is.False);
                    Assert.That(hub.IsCaseAvailable(ReturnRosterCaseId.RC05ThirdSquad), Is.False);
                    Assert.That(hub.TryOpenCase(ReturnRosterCaseId.RC02ZhengYuan), Is.False);
                    Assert.That(hub.TryOpenCase(ReturnRosterCaseId.RC05ThirdSquad), Is.False);
                    Assert.That(hub.IsCaseAvailable(ReturnRosterCaseId.RC01ChenYan), Is.True);

                    Assert.That(M10AssemblyFacts.TryResetForNewVoyage(write), Is.True);
                    values[M16EpilogueFacts.ConfirmedKey] = 1;
                    Assert.That(hub.IsCaseAvailable(ReturnRosterCaseId.RC02ZhengYuan), Is.False);
                    Assert.That(hub.IsCaseAvailable(ReturnRosterCaseId.RC05ThirdSquad), Is.False);

                    Assert.That(M16EpilogueFacts.TryResetForNewVoyage(write), Is.True);
                    Assert.That(hub.IsCaseAvailable(ReturnRosterCaseId.RC02ZhengYuan), Is.True);
                    Assert.That(hub.IsCaseAvailable(ReturnRosterCaseId.RC05ThirdSquad), Is.True);
                    Assert.That(hub.TryOpenCase(ReturnRosterCaseId.RC02ZhengYuan), Is.True);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
