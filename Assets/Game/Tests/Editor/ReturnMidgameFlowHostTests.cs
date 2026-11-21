using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ReturnMidgameFlowHostTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
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
        public void FailsClosedBeforeM07AndAdvancesM08ThroughM09WithoutRequiringDockTruth()
        {
            var root = new GameObject("MidgameFlowChainRoot");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                var ctx = new ReturnMidgameFlowContext
                {
                    ShenLiPresentAtDock = () => false
                };
                using (var flow = new ReturnMidgameFlowHost(root.transform, ctx, read, write))
                {
                    Assert.That(flow.EvaluateNextRequiredStage(), Is.EqualTo(ReturnMidgameStage.None));
                    Assert.That(flow.TryOpenNextRequiredStage(), Is.False);

                    SeedM07(values);
                    Assert.That(flow.TryOpenNextRequiredStage(), Is.True);
                    Assert.That(flow.CurrentStage, Is.EqualTo(ReturnMidgameStage.M08WorkshopOrder));
                    Assert.That(flow.ActiveWorkshopHost, Is.Not.Null);

                    // Complete workshop deposit.
                    var winch = new M08FirstWinchOrderSession();
                    Assert.That(winch.TryAcquirePart(M08WinchPart.ScrapGear), Is.True);
                    Assert.That(winch.TryAcquirePart(M08WinchPart.DockCable), Is.True);
                    Assert.That(winch.TryAssemble(), Is.True);
                    Assert.That(winch.TryTightenCable(), Is.True);
                    Assert.That(winch.TryTestLift(), Is.True);
                    Assert.That(M08WorkshopDepositFacts.TryConfirm(winch, read, write), Is.True);

                    // Optional dock truth is unavailable when Shen Li is absent, yet M08 First World Notice proceeds cleanly.
                    Assert.That(flow.CanOpenOptionalDockTruth(), Is.False);
                    Assert.That(flow.TryOpenNextRequiredStage(), Is.True);
                    Assert.That(flow.CurrentStage, Is.EqualTo(ReturnMidgameStage.M08FirstWorldNotice));
                    Assert.That(flow.ActiveFirstNoticeHost, Is.Not.Null);

                    Assert.That(M08FirstWorldNoticeFacts.TryRecord(true, read, write), Is.True);

                    // Advance to M09 Berth Table.
                    Assert.That(flow.TryOpenNextRequiredStage(), Is.True);
                    Assert.That(flow.CurrentStage, Is.EqualTo(ReturnMidgameStage.M09BerthTable));
                    Assert.That(flow.ActiveBerthTableHost, Is.Not.Null);

                    Assert.That(M09BerthTableFacts.TryRecord(new M09BerthTableRecord(
                        20, 23, M09BerthExchangeFlags.HanQiaoCivilianVoyage,
                        M09RuiyaOutcome.Rescued, false, false), read, write), Is.True);

                    Assert.That(flow.TryOpenNextRequiredStage(), Is.False);
                    Assert.That(flow.CurrentStage, Is.EqualTo(ReturnMidgameStage.ReadyForActThree));
                    Assert.That(flow.HasActiveHost, Is.False);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void OptionalDockTruthOpensAfterWorkshopDepositWhenShenLiPresentAndDisposesCleanly()
        {
            var root = new GameObject("MidgameFlowDockRoot");
            var values = new Dictionary<string, int>();
            SeedM07(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var winch = new M08FirstWinchOrderSession();
            Assert.That(winch.TryAcquirePart(M08WinchPart.ScrapGear), Is.True);
            Assert.That(winch.TryAcquirePart(M08WinchPart.DockCable), Is.True);
            Assert.That(winch.TryAssemble(), Is.True);
            Assert.That(winch.TryTightenCable(), Is.True);
            Assert.That(winch.TryTestLift(), Is.True);
            Assert.That(M08WorkshopDepositFacts.TryConfirm(winch, read, write), Is.True);

            try
            {
                var flow = new ReturnMidgameFlowHost(
                    root.transform, new ReturnMidgameFlowContext(), read, write);
                Assert.That(flow.CanOpenOptionalDockTruth(), Is.True);
                Assert.That(flow.TryOpenOptionalDockTruth(), Is.True);
                Assert.That(flow.CurrentStage, Is.EqualTo(ReturnMidgameStage.M08OptionalDockTruth));
                var dockHost = flow.ActiveDockTruthHost;
                Assert.That(dockHost, Is.Not.Null);
                Assert.That(dockHost.IsDisposed, Is.False);

                flow.Dispose();
                Assert.That(flow.IsDisposed, Is.True);
                Assert.That(dockHost.IsDisposed, Is.True);
                Assert.That(flow.TryOpenNextRequiredStage(), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
