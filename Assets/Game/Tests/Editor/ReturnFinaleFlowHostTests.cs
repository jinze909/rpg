using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ReturnFinaleFlowHostTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        // Positive finale tests explicitly supply observed Act-3 scene state;
        // production defaults remain unproven and fail closed.
        private static ReturnFinaleFlowContext ObservedFinaleScenes()
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
        public void UnprovenOrMissingSceneContextCannotOpenAnyFinaleStage()
        {
            var root = new GameObject("UnprovenFinaleContextRoot");
            var values = new Dictionary<string, int>();
            SeedThroughM09(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            var context = new ReturnFinaleFlowContext();

            try
            {
                Assert.That(context.ReadCivilianCounts(), Is.EqualTo(default(M14CivilianCounts)));
                Assert.That(context.ConsentedVerifiedNamesCount(), Is.Zero);

                using (var flow = new ReturnFinaleFlowHost(root.transform, context, read, write))
                {
                    Assert.That(flow.EvaluateNextStage(), Is.EqualTo(ReturnFinaleStage.M10Assembly));
                    Assert.That(flow.TryOpenNextStage(), Is.False);
                    Assert.That(flow.HasActiveHost, Is.False);
                    Assert.That(root.transform.childCount, Is.Zero);

                    // Null delegate must fail closed without throwing.
                    context.AtJingwanAssembly = null;
                    Assert.That(flow.TryOpenNextStage(), Is.False);
                    Assert.That(flow.HasActiveHost, Is.False);

                    // Open M10 once observed, settle M10, and verify a missing M11
                    // delegate fails closed without tearing down the prior active host.
                    context.AtJingwanAssembly = () => true;
                    Assert.That(flow.TryOpenNextStage(), Is.True);
                    Assert.That(flow.CurrentStage, Is.EqualTo(ReturnFinaleStage.M10Assembly));
                    var m10Host = flow.ActiveM10Host;
                    Assert.That(M10AssemblyFacts.TryRecord(new M10AssemblyRecord(
                        true, M10RosterAccuracy.VerifiedFirstPass, M10BattleReportSource.CarriedFromM02,
                        M10HanLedgerUse.KeepConfidential, M10SupplyContractStatus.SignedFullCoverage,
                        false, false, true), read, write), Is.True);

                    context.AtHuilanPass = null;
                    Assert.That(flow.EvaluateNextStage(), Is.EqualTo(ReturnFinaleStage.M11ReverseRescue));
                    Assert.That(flow.TryOpenNextStage(), Is.False);
                    Assert.That(flow.ActiveM10Host, Is.SameAs(m10Host));
                    Assert.That(m10Host.IsDisposed, Is.False);

                    // Unproven defaults also block M12 through M16 even when prior chapters exist.
                    flow.CloseActiveHost();
                    Assert.That(M11ReverseRescueFacts.TryRecord(new M11ReverseRescueRecord(
                        true, M11PassEntryMethod.LandGateNegotiation, true,
                        M11ThirdSquadConscriptionStance.NotInPass,
                        M11RescueProvider.FallbackPaidArtisans, 0, false,
                        M11ShenDivisionPact.NoneShenAbsent), read, write), Is.True);
                    Assert.That(flow.TryOpenNextStage(), Is.False);

                    Assert.That(M12BridgeTruthFacts.TryRecord(new M12BridgeTruthRecord(
                        true, true, M12LuConfrontationReply.YouSaidThatLastTime,
                        false, M12StatementFlags.RequiredBase, false, false,
                        M12TruthDisclosure.SealTruth), read, write), Is.True);
                    Assert.That(flow.TryOpenNextStage(), Is.False);

                    Assert.That(M13DepartureRiskFacts.TryRecordDeparture(new M13DepartureRecord(
                        M13ApproachRoute.RouteASupervisedCivilian, true, false, false, false),
                        read, write), Is.True);
                    Assert.That(flow.TryOpenNextStage(), Is.False);

                    Assert.That(M14FinalPassageFacts.TryRecord(new M14FinalPassageRecord(
                        M13ApproachRoute.RouteASupervisedCivilian,
                        M14PassageOutcome.RouteASupervisedCivilianEstablished,
                        true, 12, 2, 1, M14BorderForceAction.NotRouteB,
                        M14LuJunDisposition.RelievedOfCommand, M14AyuanOutcome.Safe, false),
                        read, write), Is.True);
                    Assert.That(flow.TryOpenNextStage(), Is.False);

                    Assert.That(M15FinalRollCallFacts.TryRecord(new M15FinalRollCallRecord(
                        M15RollCallReader.Protagonist,
                        M15FinalIdentityResponse.PublicTrueName,
                        M15PensionClosure.StoppedAndReconciled,
                        M15SteleInscription.CarveConsentedVerifiedNames,
                        12, false), read, write), Is.True);
                    Assert.That(flow.TryOpenNextStage(), Is.False);
                    Assert.That(flow.HasActiveHost, Is.False);
                    Assert.That(root.transform.childCount, Is.Zero);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FailsClosedBeforeM09AndResumesAtExactNextUncompletedStage()
        {
            var root = new GameObject("FinaleFlowResumeRoot");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                using (var flow = new ReturnFinaleFlowHost(
                    root.transform, ObservedFinaleScenes(), read, write))
                {
                    Assert.That(flow.EvaluateNextStage(), Is.EqualTo(ReturnFinaleStage.None));
                    Assert.That(flow.TryOpenNextStage(), Is.False);

                    SeedThroughM09(values);
                    Assert.That(flow.EvaluateNextStage(), Is.EqualTo(ReturnFinaleStage.M10Assembly));

                    // Seed M10, M11, and M12 (SealTruth) directly as if resuming a mid-Act-3 save.
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
                        m03PriorConfession: true,
                        m03SecrecyPromised: true,
                        luReply: M12LuConfrontationReply.YouSaidThatLastTime,
                        shenLiParticipating: false,
                        statementsHeard: M12StatementFlags.RequiredBase,
                        linSuEchoHeard: false,
                        m10HanExposedPublicly: false,
                        disclosure: M12TruthDisclosure.SealTruth), read, write), Is.True);

                    Assert.That(flow.EvaluateNextStage(), Is.EqualTo(ReturnFinaleStage.M13DepartureRisk));
                    Assert.That(flow.TryOpenNextStage(), Is.True);
                    Assert.That(flow.CurrentStage, Is.EqualTo(ReturnFinaleStage.M13DepartureRisk));
                    Assert.That(flow.ActiveM13Host, Is.Not.Null);
                    // Cannot re-open the same stage while already open.
                    Assert.That(flow.TryOpenNextStage(), Is.False);

                    // Seed M13 & M14 and verify M15 automatically derives BridgeTruthSealed == true from M12.
                    Assert.That(M13DepartureRiskFacts.TryRecordDeparture(new M13DepartureRecord(
                        M13ApproachRoute.RouteASupervisedCivilian, true, false, false, false),
                        read, write), Is.True);
                    Assert.That(M14FinalPassageFacts.TryRecord(new M14FinalPassageRecord(
                        M13ApproachRoute.RouteASupervisedCivilian,
                        M14PassageOutcome.RouteASupervisedCivilianEstablished,
                        true, 12, 2, 1, M14BorderForceAction.NotRouteB,
                        M14LuJunDisposition.RelievedOfCommand, M14AyuanOutcome.Safe, false),
                        read, write), Is.True);

                    Assert.That(flow.TryOpenNextStage(), Is.True);
                    Assert.That(flow.CurrentStage, Is.EqualTo(ReturnFinaleStage.M15FinalRollCall));
                    Assert.That(flow.ActiveM15Host, Is.Not.Null);
                    Assert.That(flow.ActiveM15Host.Session.BridgeTruthSealed, Is.True);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FullM10ThroughM16ChainAdvancesThroughEveryLiveHostToCompletion()
        {
            var root = new GameObject("FinaleFlowFullChainRoot");
            var values = new Dictionary<string, int>();
            SeedThroughM09(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                using (var flow = new ReturnFinaleFlowHost(
                    root.transform, ObservedFinaleScenes(), read, write))
                {
                    // M10
                    Assert.That(flow.TryOpenNextStage(), Is.True);
                    Assert.That(flow.CurrentStage, Is.EqualTo(ReturnFinaleStage.M10Assembly));
                    var m10 = flow.ActiveM10Host.Session;
                    Assert.That(m10.TryObserveWorldNoticeThree(), Is.True);
                    Assert.That(m10.TryReconcileThreeRecords(true), Is.True);
                    Assert.That(m10.TrySecureBattleReportCopy(M10BattleReportSource.CarriedFromM02), Is.True);
                    Assert.That(m10.TryDecideHanLedgerUse(M10HanLedgerUse.KeepConfidential), Is.True);
                    Assert.That(m10.TrySignSupplyContract(M10SupplyContractStatus.SignedFullCoverage), Is.True);
                    Assert.That(m10.TryReceiveBellTowerSeizureReport(), Is.True);
                    Assert.That(m10.TryCommit(read, write), Is.True);

                    // M11
                    Assert.That(flow.TryOpenNextStage(), Is.True);
                    Assert.That(flow.CurrentStage, Is.EqualTo(ReturnFinaleStage.M11ReverseRescue));
                    var m11 = flow.ActiveM11Host.Session;
                    Assert.That(m11.TryObserveThreeBellsAndEnter(M11PassEntryMethod.LandGateNegotiation), Is.True);
                    Assert.That(m11.TryObserveConscriptionAndCoverReturnees(), Is.True);
                    Assert.That(m11.TryHearShenLiFogCall(), Is.True);
                    Assert.That(m11.TryHearShenLiFogCall(), Is.True);
                    Assert.That(m11.TryHearShenLiFogCall(), Is.True);
                    Assert.That(m11.TryAnswerPresentForShenLiExtraction(), Is.True);
                    Assert.That(m11.TryChooseDivisionPact(M11ShenDivisionPact.MutualConsultAndDivision), Is.True);
                    Assert.That(m11.TryCommit(read, write), Is.True);

                    // M12
                    Assert.That(flow.TryOpenNextStage(), Is.True);
                    Assert.That(flow.CurrentStage, Is.EqualTo(ReturnFinaleStage.M12BridgeTruth));
                    var m12 = flow.ActiveM12Host.Session;
                    Assert.That(m12.TryConfrontLuJun(M12LuConfrontationReply.YouSaidThatLastTime), Is.True);
                    Assert.That(m12.TryHearStatement(M12StatementFlags.JiangShenHeard), Is.True);
                    Assert.That(m12.TryHearStatement(M12StatementFlags.HanAndYiLeiHeard), Is.True);
                    Assert.That(m12.TryHearStatement(M12StatementFlags.ShenLiHeard), Is.True);
                    Assert.That(m12.TryDecideTruthAndCommit(M12TruthDisclosure.PublishTruth, read, write), Is.True);

                    // M13
                    Assert.That(flow.TryOpenNextStage(), Is.True);
                    Assert.That(flow.CurrentStage, Is.EqualTo(ReturnFinaleStage.M13DepartureRisk));
                    Assert.That(flow.ActiveM13Host.Session.TryConfirmDeparture(
                        M13ApproachRoute.RouteASupervisedCivilian, read, write), Is.True);

                    // M14
                    Assert.That(flow.TryOpenNextStage(), Is.True);
                    Assert.That(flow.CurrentStage, Is.EqualTo(ReturnFinaleStage.M14FinalPassage));
                    var m14 = flow.ActiveM14Host.Session;
                    Assert.That(m14.TryResolveRouteASupervisedSquare(
                        validSupervisionCredentials: true,
                        validPortGuarantee: true,
                        luJunDisposition: M14LuJunDisposition.RelievedOfCommand), Is.True);
                    Assert.That(m14.TryVerifyCivilianExit(12, 2, 1), Is.True);
                    Assert.That(m14.TrySettleAyuanAndCommit(M14AyuanOutcome.Safe, read, write), Is.True);

                    // M15
                    Assert.That(flow.TryOpenNextStage(), Is.True);
                    Assert.That(flow.CurrentStage, Is.EqualTo(ReturnFinaleStage.M15FinalRollCall));
                    Assert.That(flow.ActiveM15Host.Session.BridgeTruthSealed, Is.False);
                    Assert.That(flow.ActiveM15Host.Session.TryCompleteRollCall(
                        M15RollCallReader.Protagonist,
                        M15FinalIdentityResponse.PublicTrueName,
                        M15SteleInscription.CarveConsentedVerifiedNames,
                        read, write), Is.True);

                    // M16
                    Assert.That(flow.TryOpenNextStage(), Is.True);
                    Assert.That(flow.CurrentStage, Is.EqualTo(ReturnFinaleStage.M16Epilogue));
                    var m16View = flow.ActiveM16Host.ActiveView;
                    m16View.ProceedToDeskButton.onClick.Invoke();
                    m16View.StepOutsideButton.onClick.Invoke();
                    m16View.PierButton.onClick.Invoke();
                    Assert.That(flow.ActiveM16Host.IsCompleted, Is.True);

                    // Completed
                    Assert.That(flow.TryOpenNextStage(), Is.False);
                    Assert.That(flow.CurrentStage, Is.EqualTo(ReturnFinaleStage.Completed));
                    Assert.That(flow.HasActiveHost, Is.False);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AssemblyCallbackClosingFlowCannotPublishLateStage()
        {
            AssertAssemblyCallbackCancellingFlowCannotPublishLateStage(false);
        }

        [Test]
        public void AssemblyCallbackDisposingFlowCannotPublishLateStage()
        {
            AssertAssemblyCallbackCancellingFlowCannotPublishLateStage(true);
        }

        private static void AssertAssemblyCallbackCancellingFlowCannotPublishLateStage(bool disposeFlow)
        {
            var root = new GameObject("Finale flow cancelled during assembly entry");
            var values = new Dictionary<string, int>();
            SeedThroughM09(values);
            var context = ObservedFinaleScenes();
            ReturnFinaleFlowHost flow = null;
            int writes = 0;
            try
            {
                context.AtJingwanAssembly = () =>
                {
                    if (disposeFlow) flow.Dispose();
                    else flow.CloseActiveHost();
                    return true;
                };
                flow = new ReturnFinaleFlowHost(root.transform, context, Reader(values),
                    (k, v) => { writes++; values[k] = v; });
                Assert.That(flow.TryOpenNextStage(), Is.False);
                Assert.That(flow.CurrentStage, Is.EqualTo(ReturnFinaleStage.None));
                Assert.That(flow.HasActiveHost, Is.False);
                Assert.That(flow.ActiveM10Host, Is.Null);
                Assert.That(root.transform.childCount, Is.Zero);
                Assert.That(writes, Is.Zero);
                context.AtJingwanAssembly = () => true;
                Assert.That(flow.TryOpenNextStage(), Is.EqualTo(!disposeFlow));
            }
            finally
            {
                flow?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AssemblyCallbackCannotRecursivelyDispatchNextStage()
        {
            var root = new GameObject("Finale flow nested stage opening");
            var values = new Dictionary<string, int>();
            SeedThroughM09(values);
            var context = ObservedFinaleScenes();
            ReturnFinaleFlowHost flow = null;
            bool attempted = false;
            try
            {
                context.AtJingwanAssembly = () =>
                {
                    if (!attempted)
                    {
                        attempted = true;
                        Assert.That(flow.TryOpenNextStage(), Is.False);
                    }
                    return true;
                };
                flow = new ReturnFinaleFlowHost(root.transform, context, Reader(values),
                    (k, v) => values[k] = v);
                Assert.That(flow.TryOpenNextStage(), Is.True);
                Assert.That(attempted, Is.True);
                Assert.That(flow.CurrentStage, Is.EqualTo(ReturnFinaleStage.M10Assembly));
                Assert.That(flow.ActiveM10Host.ActiveView, Is.Not.Null);
                flow.CloseActiveHost();
                Assert.That(root.transform.childCount, Is.Zero);
                Assert.That(flow.TryOpenNextStage(), Is.True);
            }
            finally
            {
                flow?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DisposalClosesActiveSubHostAndRejectsFurtherOpens()
        {
            var root = new GameObject("FinaleFlowDisposeRoot");
            var values = new Dictionary<string, int>();
            SeedThroughM09(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                var flow = new ReturnFinaleFlowHost(
                    root.transform, ObservedFinaleScenes(), read, write);
                Assert.That(flow.TryOpenNextStage(), Is.True);
                var m10Host = flow.ActiveM10Host;
                Assert.That(m10Host.IsDisposed, Is.False);

                flow.Dispose();
                Assert.That(flow.IsDisposed, Is.True);
                Assert.That(flow.HasActiveHost, Is.False);
                Assert.That(m10Host.IsDisposed, Is.True);
                Assert.That(flow.TryOpenNextStage(), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
