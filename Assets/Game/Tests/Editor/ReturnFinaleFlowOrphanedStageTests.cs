using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ReturnFinaleFlowOrphanedStageTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static ReturnFinaleFlowContext ObservedScenes()
        {
            return new ReturnFinaleFlowContext
            {
                AtJingwanAssembly = () => true,
                ThreeRecordsMatched = () => true,
                CarriesM02BattleReport = () => true,
                AtHuilanPass = () => true,
                ShenLiAccompanying = () => true,
                OulinParticipating = () => true,
                AtBellTowerCamp = () => true,
                M03PriorConfession = () => true,
                M03SecrecyPromised = () => false,
                LinSuPresent = () => true,
                FleetBriefingReady = () => true,
                SafetyPrepComplete = () => true,
                CanDepartNow = () => true,
                AtStraitAction = () => true,
                ValidSupervisionCredentials = () => true,
                ValidPortGuarantee = () => true,
                ThreeOpenDoorBellsRung = () => true,
                FogRelayCompleted = () => true,
                FogMasteryBonusAvailable = () => false,
                ReadCivilianCounts = () => new M14CivilianCounts(12, 2, 1),
                CeremonyReady = () => true,
                ConsentedVerifiedNamesCount = () => 12,
                XiaoqiCustomName = () => null,
                AtLinchaoAfternoon = () => true,
                ShenLiWillingToMeet = () => true,
                ResolveClosingTone = () => M16RespectAgencyTone.RespectedOthersChoices
            };
        }

        private static void SeedThroughM09(Dictionary<string, int> values)
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
        public void EvaluateNextStageReturnsNoneWhenOrphanedDownstreamAct3FactsBlockCurrentChapter()
        {
            var root = new GameObject("FinaleFlowOrphanedStageRoot");
            var values = new Dictionary<string, int>();
            SeedThroughM09(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                using (var flow = new ReturnFinaleFlowHost(
                    root.transform, ObservedScenes(), read, write))
                {
                    // Orphaned M16 key must make M10 evaluate to None until cleared.
                    values[M16EpilogueFacts.ConfirmedKey] = 1;
                    Assert.That(flow.EvaluateNextStage(), Is.EqualTo(ReturnFinaleStage.None));
                    Assert.That(flow.TryOpenNextStage(), Is.False);
                    Assert.That(M16EpilogueFacts.TryResetForNewVoyage(write), Is.True);

                    Assert.That(flow.EvaluateNextStage(), Is.EqualTo(ReturnFinaleStage.M10Assembly));
                    Assert.That(M10AssemblyFacts.TryRecord(new M10AssemblyRecord(
                        true, M10RosterAccuracy.VerifiedFirstPass,
                        M10BattleReportSource.CarriedFromM02,
                        M10HanLedgerUse.KeepConfidential,
                        M10SupplyContractStatus.SignedFullCoverage,
                        false, false, true), read, write), Is.True);

                    // Orphaned M14 AyuanOutcomeKey must make M11 evaluate to None until cleared.
                    values[M13DepartureRiskFacts.AyuanOutcomeKey] = (int)M14AyuanOutcome.Safe;
                    Assert.That(flow.EvaluateNextStage(), Is.EqualTo(ReturnFinaleStage.None));
                    Assert.That(flow.TryOpenNextStage(), Is.False);
                    Assert.That(M13DepartureRiskFacts.TryResetForNewVoyage(write), Is.True);

                    Assert.That(flow.EvaluateNextStage(), Is.EqualTo(ReturnFinaleStage.M11ReverseRescue));
                    Assert.That(flow.TryOpenNextStage(), Is.True);
                    Assert.That(flow.CurrentStage, Is.EqualTo(ReturnFinaleStage.M11ReverseRescue));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
