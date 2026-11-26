using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ReturnMidgameFlowHostLifecycleTests
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
                RealDaybreakObserved = () => true
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

        private static void SeedM08WorkshopDeposit(Dictionary<string, int> values)
        {
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var winch = new M08FirstWinchOrderSession();
            Assert.That(winch.TryAcquirePart(M08WinchPart.ScrapGear), Is.True);
            Assert.That(winch.TryAcquirePart(M08WinchPart.DockCable), Is.True);
            Assert.That(winch.TryAssemble(), Is.True);
            Assert.That(winch.TryTightenCable(), Is.True);
            Assert.That(winch.TryTestLift(), Is.True);
            Assert.That(M08WorkshopDepositFacts.TryConfirm(winch, read, write), Is.True);
        }

        [Test]
        public void ExternalChildViewDisposalClearsHasActiveHostAndAllowsCleanReopen()
        {
            var root = new GameObject("MidgameFlowExternalDisposeRoot");
            var values = new Dictionary<string, int>();
            SeedM07(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                using (var flow = new ReturnMidgameFlowHost(
                    root.transform, ObservedScenes(), read, write))
                {
                    Assert.That(flow.TryOpenNextRequiredStage(), Is.True);
                    var firstHost = flow.ActiveWorkshopHost;
                    Assert.That(flow.HasActiveHost, Is.True);
                    Assert.That(firstHost.ActiveView, Is.Not.Null);

                    firstHost.ActiveView.Dispose();
                    Assert.That(flow.HasActiveHost, Is.False);

                    Assert.That(flow.TryOpenNextRequiredStage(), Is.True);
                    Assert.That(flow.HasActiveHost, Is.True);
                    Assert.That(flow.ActiveWorkshopHost, Is.Not.SameAs(firstHost));
                    Assert.That(flow.ActiveWorkshopHost.ActiveView, Is.Not.Null);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyedChildCanvasClearsHasActiveHostAndAllowsOptionalDockTruthReopen()
        {
            var root = new GameObject("MidgameFlowCanvasDestroyRoot");
            var values = new Dictionary<string, int>();
            SeedM07(values);
            SeedM08WorkshopDeposit(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                using (var flow = new ReturnMidgameFlowHost(
                    root.transform, ObservedScenes(), read, write))
                {
                    Assert.That(flow.TryOpenOptionalDockTruth(), Is.True);
                    Assert.That(flow.HasActiveHost, Is.True);
                    Assert.That(flow.ActiveDockTruthHost.ActiveView, Is.Not.Null);
                    Assert.That(root.transform.childCount, Is.EqualTo(1));

                    UnityEngine.Object.DestroyImmediate(root.transform.GetChild(0).gameObject);
                    Assert.That(flow.HasActiveHost, Is.False);

                    Assert.That(flow.TryOpenOptionalDockTruth(), Is.True);
                    Assert.That(flow.HasActiveHost, Is.True);
                    Assert.That(flow.ActiveDockTruthHost.ActiveView, Is.Not.Null);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyedUiParentFailsClosedOnRequiredAndOptionalStageOpensWithoutThrowing()
        {
            var root = new GameObject("MidgameFlowParentDestroyRoot");
            var values = new Dictionary<string, int>();
            SeedM07(values);
            SeedM08WorkshopDeposit(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            ReturnMidgameFlowHost flow = null;
            try
            {
                flow = new ReturnMidgameFlowHost(
                    root.transform, ObservedScenes(), read, write);
                UnityEngine.Object.DestroyImmediate(root);
                root = null;

                Assert.That(flow.TryOpenNextRequiredStage(), Is.False);
                Assert.That(flow.TryOpenOptionalDockTruth(), Is.False);
                Assert.That(flow.HasActiveHost, Is.False);
            }
            finally
            {
                flow?.Dispose();
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
