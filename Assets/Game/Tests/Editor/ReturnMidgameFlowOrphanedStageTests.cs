using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ReturnMidgameFlowOrphanedStageTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static ReturnMidgameFlowContext ObservedScenes()
        {
            return new ReturnMidgameFlowContext
            {
                ShenLiPresentAtDock = () => true,
                RealDaybreakObserved = () => true,
                AtSecondBatchTable = () => true,
                ShenLiPresentAtTable = () => true,
                BaseBerths = () => 18,
                PendingNames = () => 23,
                RuiyaOutcome = () => M09RuiyaOutcome.Rescued
            };
        }

        private static void SeedM07(Dictionary<string, int> values)
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
        public void MidgameFlowReturnsNoneForOrphanedDownstreamStagesAndBlocksTornOptionalDockTruth()
        {
            var root = new GameObject("MidgameFlowOrphanedStageRoot");
            var values = new Dictionary<string, int>();
            SeedM07(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                using (var flow = new ReturnMidgameFlowHost(
                    root.transform, ObservedScenes(), read, write))
                {
                    // Orphaned RC-01 key blocks M08 Workshop stage evaluation.
                    values[RC01ChenYanFacts.ConfirmedKey] = 1;
                    Assert.That(flow.EvaluateNextRequiredStage(), Is.EqualTo(ReturnMidgameStage.None));
                    Assert.That(flow.TryOpenNextRequiredStage(), Is.False);
                    values[RC01ChenYanFacts.ConfirmedKey] = 0;

                    var winch = new M08FirstWinchOrderSession();
                    Assert.That(winch.TryAcquirePart(M08WinchPart.ScrapGear), Is.True);
                    Assert.That(winch.TryAcquirePart(M08WinchPart.DockCable), Is.True);
                    Assert.That(winch.TryAssemble(), Is.True);
                    Assert.That(winch.TryTightenCable(), Is.True);
                    Assert.That(winch.TryTestLift(), Is.True);
                    Assert.That(M08WorkshopDepositFacts.TryConfirm(winch, read, write), Is.True);

                    // Torn unconfirmed dock response must make CanOpenOptionalDockTruth false.
                    values[M08DockTruthFacts.ShenRelationResponseKey] =
                        (int)M08DockTruthResponse.StaySilent;
                    Assert.That(flow.CanOpenOptionalDockTruth(), Is.False);
                    Assert.That(flow.TryOpenOptionalDockTruth(), Is.False);
                    Assert.That(M08DockTruthFacts.TryResetForNewVoyage(write), Is.True);
                    Assert.That(flow.CanOpenOptionalDockTruth(), Is.True);

                    // Orphaned RC-05 key blocks M08 First World Notice evaluation.
                    values[RC05ThirdSquadFacts.ConfirmedKey] = 1;
                    Assert.That(flow.EvaluateNextRequiredStage(), Is.EqualTo(ReturnMidgameStage.None));
                    values[RC05ThirdSquadFacts.ConfirmedKey] = 0;
                    Assert.That(M08FirstWorldNoticeFacts.TryRecord(true, read, write), Is.True);

                    // Orphaned M16 key blocks M09 Berth Table evaluation until cleared.
                    values[M16EpilogueFacts.ConfirmedKey] = 1;
                    Assert.That(flow.EvaluateNextRequiredStage(), Is.EqualTo(ReturnMidgameStage.None));
                    Assert.That(flow.TryOpenNextRequiredStage(), Is.False);
                    Assert.That(M16EpilogueFacts.TryResetForNewVoyage(write), Is.True);

                    Assert.That(flow.EvaluateNextRequiredStage(), Is.EqualTo(ReturnMidgameStage.M09BerthTable));
                    Assert.That(flow.TryOpenNextRequiredStage(), Is.True);
                    Assert.That(flow.CurrentStage, Is.EqualTo(ReturnMidgameStage.M09BerthTable));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
