using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ReturnRosterCasesHubHostTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        // These tests explicitly simulate observed world locations and proof.
        // Production's unconfigured context is intentionally not permissive.
        private static ReturnRosterCasesHubContext ObservedCaseScenes()
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
        public void MissingOrUnobservedWorldEvidenceCannotOpenAnyRosterCase()
        {
            var root = new GameObject("Unobserved roster cases");
            var values = new Dictionary<string, int>();
            SeedThroughM08Notice(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            var context = new ReturnRosterCasesHubContext();

            try
            {
                using (var hub = new ReturnRosterCasesHubHost(
                    root.transform, context, read, write))
                {
                    for (int i = 1; i <= 5; i++)
                    {
                        var caseId = (ReturnRosterCaseId)i;
                        Assert.That(hub.IsCaseAvailable(caseId), Is.True);
                        Assert.That(hub.TryOpenCase(caseId), Is.False,
                            "An M08 case notice is not evidence of reaching its real scene.");
                    }

                    context.AtWhiteReedFerry = null;
                    context.AtReefListeningPoint = null;
                    context.VerifiedReefSceneConditions = null;
                    Assert.That(hub.TryOpenCase(ReturnRosterCaseId.RC01ChenYan),
                        Is.False);
                    Assert.That(hub.TryOpenCase(ReturnRosterCaseId.RC04DingYueReef),
                        Is.False);
                    Assert.That(hub.HasActiveCase, Is.False);
                    Assert.That(root.transform.childCount, Is.Zero);
                    Assert.That(hub.CountSettledCases(), Is.Zero);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HubGatesAllCasesOnM08NoticeAndEnforcesSingleActiveCaseExclusion()
        {
            var root = new GameObject("RosterHubGateRoot");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                using (var hub = new ReturnRosterCasesHubHost(
                    root.transform, ObservedCaseScenes(), read, write))
                {
                    Assert.That(hub.IsCaseAvailable(ReturnRosterCaseId.RC01ChenYan), Is.False);
                    Assert.That(hub.TryOpenCase(ReturnRosterCaseId.RC01ChenYan), Is.False);

                    SeedThroughM08Notice(values);
                    Assert.That(hub.IsCaseAvailable(ReturnRosterCaseId.RC01ChenYan), Is.True);
                    Assert.That(hub.TryOpenCase(ReturnRosterCaseId.RC01ChenYan), Is.True);
                    Assert.That(hub.ActiveCase, Is.EqualTo(ReturnRosterCaseId.RC01ChenYan));
                    Assert.That(hub.ActiveRC01Host, Is.Not.Null);

                    // Cannot open another case while RC-01 is still active and unsettled.
                    Assert.That(hub.TryOpenCase(ReturnRosterCaseId.RC03BaijiMine), Is.False);
                    Assert.That(hub.ActiveCase, Is.EqualTo(ReturnRosterCaseId.RC01ChenYan));

                    // Closing RC-01 allows opening RC-04 listening immediately.
                    hub.CloseActiveCase();
                    Assert.That(hub.TryOpenCase(ReturnRosterCaseId.RC04DingYueReef), Is.True);
                    Assert.That(hub.ActiveRC04Host, Is.Not.Null);
                    Assert.That(hub.ActiveRC04Host.ActiveListeningView, Is.Not.Null);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SettlingRC05PropagatesRealZhengYuanVerificationIntoRC02ViaHub()
        {
            var root = new GameObject("RosterHubCrossCaseRoot");
            var values = new Dictionary<string, int>();
            SeedThroughM08Notice(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                using (var hub = new ReturnRosterCasesHubHost(
                    root.transform, ObservedCaseScenes(), read, write))
                {
                    Assert.That(hub.TryOpenCase(ReturnRosterCaseId.RC05ThirdSquad), Is.True);
                    var rc05 = hub.ActiveRC05Host.Session;
                    Assert.That(rc05.TryObserveMountainPassRollCall(), Is.True);
                    Assert.That(rc05.TryVerifySaltGrainBoatDelivery(true), Is.True);
                    Assert.That(rc05.TryVerifyRealZhengYuan(true, true), Is.True);
                    Assert.That(rc05.TryProceedToThreePartyTalks(), Is.True);
                    Assert.That(rc05.TrySettleResolution(
                        RC05Resolution.CivilianPardon, read, write), Is.True);
                    Assert.That(hub.IsCaseSettled(ReturnRosterCaseId.RC05ThirdSquad), Is.True);
                    Assert.That(hub.CountSettledCases(), Is.EqualTo(1));

                    // Once RC-05 is settled, hub can transition cleanly to RC-02 and auto-propagate RealZhengYuanFound.
                    Assert.That(hub.TryOpenCase(ReturnRosterCaseId.RC02ZhengYuan), Is.True);
                    Assert.That(hub.ActiveCase, Is.EqualTo(ReturnRosterCaseId.RC02ZhengYuan));
                    var rc02 = hub.ActiveRC02Host.Session;
                    Assert.That(rc02.RealZhengYuanFound, Is.True);

                    // Cannot reopen already-settled RC-05.
                    Assert.That(hub.IsCaseAvailable(ReturnRosterCaseId.RC05ThirdSquad), Is.False);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void SceneCallbackCancellingCaseOpenLeavesNoViewOrFact(
            bool reef, bool disposeHub)
        {
            var root = new GameObject("Roster case cancelled during scene proof");
            var values = new Dictionary<string, int>();
            SeedThroughM08Notice(values);
            var context = ObservedCaseScenes();
            ReturnRosterCasesHubHost hub = null;
            int writes = 0;
            try
            {
                Func<bool> cancel = () =>
                {
                    if (disposeHub) hub.Dispose();
                    else hub.CloseActiveCase();
                    return true;
                };
                if (reef) context.AtReefListeningPoint = cancel;
                else context.AtWhiteReedFerry = cancel;

                hub = new ReturnRosterCasesHubHost(root.transform, context,
                    Reader(values), (k, v) => { writes++; values[k] = v; });
                var id = reef ? ReturnRosterCaseId.RC04DingYueReef :
                    ReturnRosterCaseId.RC01ChenYan;

                Assert.That(hub.TryOpenCase(id), Is.False);
                Assert.That(hub.ActiveCase, Is.EqualTo(ReturnRosterCaseId.None));
                Assert.That(hub.HasActiveCase, Is.False);
                Assert.That(root.transform.childCount, Is.Zero);
                Assert.That(writes, Is.Zero);

                context.AtReefListeningPoint = () => true;
                context.AtWhiteReedFerry = () => true;
                Assert.That(hub.TryOpenCase(id), Is.EqualTo(!disposeHub));
            }
            finally
            {
                hub?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NestedCaseOpenDuringReefProofCannotReplaceListeningView()
        {
            var root = new GameObject("Roster hub nested opening");
            var values = new Dictionary<string, int>();
            SeedThroughM08Notice(values);
            var context = ObservedCaseScenes();
            ReturnRosterCasesHubHost hub = null;
            bool attempted = false;
            try
            {
                context.AtReefListeningPoint = () =>
                {
                    attempted = true;
                    Assert.That(hub.TryOpenCase(ReturnRosterCaseId.RC01ChenYan), Is.False);
                    return true;
                };
                hub = new ReturnRosterCasesHubHost(root.transform, context,
                    Reader(values), (k, v) => values[k] = v);
                Assert.That(hub.TryOpenCase(ReturnRosterCaseId.RC04DingYueReef), Is.True);
                Assert.That(attempted, Is.True);
                Assert.That(hub.ActiveCase, Is.EqualTo(ReturnRosterCaseId.RC04DingYueReef));
                Assert.That(hub.ActiveRC01Host, Is.Null);
                Assert.That(hub.ActiveRC04Host.ActiveListeningView, Is.Not.Null);
                hub.CloseActiveCase();
                Assert.That(root.transform.childCount, Is.Zero);
                Assert.That(hub.TryOpenCase(ReturnRosterCaseId.RC01ChenYan), Is.True);
            }
            finally
            {
                hub?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DisposalDisposesActiveCaseHostAndBlocksFurtherDispatches()
        {
            var root = new GameObject("RosterHubDisposeRoot");
            var values = new Dictionary<string, int>();
            SeedThroughM08Notice(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                var hub = new ReturnRosterCasesHubHost(
                    root.transform, ObservedCaseScenes(), read, write);
                Assert.That(hub.TryOpenCase(ReturnRosterCaseId.RC03BaijiMine), Is.True);
                var rc03Host = hub.ActiveRC03Host;
                Assert.That(rc03Host.IsDisposed, Is.False);

                hub.Dispose();
                Assert.That(hub.IsDisposed, Is.True);
                Assert.That(hub.HasActiveCase, Is.False);
                Assert.That(rc03Host.IsDisposed, Is.True);
                Assert.That(hub.TryOpenCase(ReturnRosterCaseId.RC01ChenYan), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
