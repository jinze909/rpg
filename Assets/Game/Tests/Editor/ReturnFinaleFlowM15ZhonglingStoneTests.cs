using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ReturnFinaleFlowM15ZhonglingStoneTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedThroughM14(Dictionary<string, int> values)
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
            Assert.That(M09BerthTableFacts.TryRecord(new M09BerthTableRecord(
                20, 23, M09BerthExchangeFlags.HanQiaoCivilianVoyage,
                M09RuiyaOutcome.Rescued, false, false), read, write), Is.True);
            Assert.That(M10AssemblyFacts.TryRecord(new M10AssemblyRecord(
                true, M10RosterAccuracy.VerifiedFirstPass, M10BattleReportSource.CarriedFromM02,
                M10HanLedgerUse.KeepConfidential, M10SupplyContractStatus.SignedFullCoverage,
                false, false, true), read, write), Is.True);
            Assert.That(M11ReverseRescueFacts.TryRecord(new M11ReverseRescueRecord(
                true, M11PassEntryMethod.LandGateNegotiation, true,
                M11ThirdSquadConscriptionStance.NotInPass,
                M11RescueProvider.FallbackPaidArtisans, 0, false,
                M11ShenDivisionPact.NoneShenAbsent), read, write), Is.True);
            Assert.That(M12BridgeTruthFacts.TryRecord(new M12BridgeTruthRecord(
                false, false, M12LuConfrontationReply.SomeWantNoMoreWar,
                false, M12StatementFlags.RequiredBase, false, false,
                M12TruthDisclosure.SealTruth), read, write), Is.True);
            Assert.That(M13DepartureRiskFacts.TryRecordDeparture(new M13DepartureRecord(
                M13ApproachRoute.RouteASupervisedCivilian, true, false, false, false),
                read, write), Is.True);
            Assert.That(M14FinalPassageFacts.TryRecord(new M14FinalPassageRecord(
                M13ApproachRoute.RouteASupervisedCivilian,
                M14PassageOutcome.RouteASupervisedCivilianEstablished,
                true, 12, 2, 1, M14BorderForceAction.NotRouteB,
                M14LuJunDisposition.RelievedOfCommand, M14AyuanOutcome.Safe, false),
                read, write), Is.True);
        }

        [Test]
        public void FinaleFlowSurfacesVerifiedST04ZhonglingStoneOnM15SteleAndRejectsSpoofedShipment()
        {
            var root = new GameObject("FinaleFlowM15ZhonglingStoneRoot");
            string previousLanguage = SoundwellUiLocalization.Language;
            try
            {
                var values = new Dictionary<string, int>();
                SeedThroughM14(values);
                Func<string, int> read = Reader(values);
                Action<string, int> write = (k, v) => values[k] = v;

                var context = new ReturnFinaleFlowContext
                {
                    CeremonyReady = () => true,
                    ConsentedVerifiedNamesCount = () => 6
                };

                // Record ST-04 long-road resolution with spoofed delivery enum -> must stay hidden on M15.
                var st04 = new ST04EchoValleyRecord(
                    ST04ValleyEntry.SurveyEchoesFromRidge,
                    ST04ChamberEntry.LureWithBell,
                    ST04BeastHandling.LedBackToOldHabitat,
                    ST04RoadResolution.ReturnBeastsAndUseCostlyLongRoad,
                    rescuedSurveyors: 2,
                    echoRulesVerified: true,
                    companionsMarked: true,
                    drainOpened: true,
                    longRoadFunded: true,
                    alternativeWorksiteReady: false);
                Assert.That(ST04EchoValleyFacts.TryRecord(st04, read, write), Is.True);
                values[ST04EchoValleyFacts.StoneShipmentKey] = (int)ST04StoneShipment.DeliveredToLinchao;

                using (var finale = new ReturnFinaleFlowHost(root.transform, context, read, write))
                {
                    Assert.That(finale.EvaluateNextStage(), Is.EqualTo(ReturnFinaleStage.M15FinalRollCall));
                    Assert.That(finale.TryOpenNextStage(), Is.True);
                    Assert.That(finale.ActiveM15Host.ShowZhonglingStoneOnMonument, Is.False);
                    Assert.That(finale.ActiveM15Host.ActiveView.ShowZhonglingStone, Is.False);
                    finale.CloseActiveHost();

                    // Restore clean state, record genuine order + delivery, and reopen M15 via FinaleFlow.
                    values[ST04EchoValleyFacts.StoneShipmentKey] = 0;
                    Assert.That(ST04EchoValleyFacts.TryMarkStoneOrderAccepted(read, write), Is.True);
                    Assert.That(ST04EchoValleyFacts.TryMarkStoneDelivered(read, write), Is.True);

                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                    Assert.That(finale.TryOpenNextStage(), Is.True);
                    Assert.That(finale.ActiveM15Host.ShowZhonglingStoneOnMonument, Is.True);
                    var view = finale.ActiveM15Host.ActiveView;
                    Assert.That(view.ShowZhonglingStone, Is.True);

                    view.ProtagonistReaderButton.onClick.Invoke();
                    view.PublicTrueNameButton.onClick.Invoke();
                    Assert.That(view.Body.text, Does.Contain("钟岭采石场运抵临潮的新碑石已立妥"));

                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                    Assert.That(view.Body.text, Does.Contain("delivered Zhongling quarry stone"));

                    view.CarveNamesSteleButton.onClick.Invoke();
                    Assert.That(finale.EvaluateNextStage(), Is.EqualTo(ReturnFinaleStage.M16Epilogue));
                }
            }
            finally
            {
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
