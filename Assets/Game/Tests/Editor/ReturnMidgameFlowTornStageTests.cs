using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ReturnMidgameFlowTornStageTests
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
        public void EvaluateNextRequiredStageReturnsNoneWhenCurrentMidgameStageHasCorruptOrTornFacts()
        {
            var root = new GameObject("MidgameFlowTornCurrentStageRoot");
            var values = new Dictionary<string, int>();
            SeedM07(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                using (var flow = new ReturnMidgameFlowHost(
                    root.transform, ObservedScenes(), read, write))
                {
                    // Corrupt M08 Workshop CompletedKey must evaluate to None.
                    values[M08WorkshopDepositFacts.CompletedKey] = 2;
                    Assert.That(flow.EvaluateNextRequiredStage(), Is.EqualTo(ReturnMidgameStage.None));
                    Assert.That(flow.TryOpenNextRequiredStage(), Is.False);
                    Assert.That(M08WorkshopDepositFacts.TryResetForNewVoyage(write), Is.True);
                    Assert.That(flow.EvaluateNextRequiredStage(),
                        Is.EqualTo(ReturnMidgameStage.M08WorkshopOrder));

                    var winch = new M08FirstWinchOrderSession();
                    Assert.That(winch.TryAcquirePart(M08WinchPart.ScrapGear), Is.True);
                    Assert.That(winch.TryAcquirePart(M08WinchPart.DockCable), Is.True);
                    Assert.That(winch.TryAssemble(), Is.True);
                    Assert.That(winch.TryTightenCable(), Is.True);
                    Assert.That(winch.TryTestLift(), Is.True);
                    Assert.That(M08WorkshopDepositFacts.TryConfirm(winch, read, write), Is.True);

                    // Corrupt M08 First World Notice ConfirmedKey must evaluate to None.
                    values[M08FirstWorldNoticeFacts.ConfirmedKey] = 2;
                    Assert.That(flow.EvaluateNextRequiredStage(), Is.EqualTo(ReturnMidgameStage.None));
                    Assert.That(flow.TryOpenNextRequiredStage(), Is.False);
                    Assert.That(M08FirstWorldNoticeFacts.TryResetForNewVoyage(write), Is.True);
                    Assert.That(flow.EvaluateNextRequiredStage(),
                        Is.EqualTo(ReturnMidgameStage.M08FirstWorldNotice));
                    Assert.That(M08FirstWorldNoticeFacts.TryRecord(true, read, write), Is.True);

                    // Torn M09 Berth Table payload without confirmation must evaluate to None.
                    values[M09BerthTableFacts.TotalBerthsKey] = 20;
                    Assert.That(flow.EvaluateNextRequiredStage(), Is.EqualTo(ReturnMidgameStage.None));
                    Assert.That(flow.TryOpenNextRequiredStage(), Is.False);
                    Assert.That(M09BerthTableFacts.TryResetForNewVoyage(write), Is.True);
                    Assert.That(flow.EvaluateNextRequiredStage(),
                        Is.EqualTo(ReturnMidgameStage.M09BerthTable));
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
