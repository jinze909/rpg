using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    /// <summary>
    /// Verifies that ST05TwoDoorsHost, RC02ZhengYuanHost, and RC03BaijiMineHost
    /// revalidate live scene proofs at commit time so withdrawn consent or
    /// invalidated proofs cannot slip through an already-open view.
    /// </summary>
    public sealed class LiveProofRecheckHostTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
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
        public void ST05HostRechecksLiveProofsBeforeHomeAndAssignmentCommit()
        {
            var root = new GameObject("ST05LiveProofRoot");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            bool partnersHeard = true;
            bool jobTermsReady = true;

            try
            {
                using (var host = new ST05TwoDoorsHost(
                    root.transform, () => true, () => true, () => true, () => true,
                    () => partnersHeard, () => false, () => jobTermsReady, read, write))
                {
                    Assert.That(host.TryOpen(), Is.True);
                    var view = host.ActiveView;
                    view.ConvertToBenchesButton.onClick.Invoke();
                    view.TestMarketGiftsButton.onClick.Invoke();
                    view.ResolveCourtyardButton.onClick.Invoke();
                    view.HearBothPartnersButton.onClick.Invoke();

                    // Withdraw partnersHeard right before home commit -> rejected without writing.
                    partnersHeard = false;
                    view.KeepTwoDoorsButton.onClick.Invoke();
                    Assert.That(ST05TwoDoorsFacts.TryRead(read, out ST05TwoDoorsOutcome? none), Is.True);
                    Assert.That(none, Is.Null);

                    // Restore partnersHeard -> home commit succeeds.
                    partnersHeard = true;
                    view.KeepTwoDoorsButton.onClick.Invoke();
                    Assert.That(host.Session.Phase, Is.EqualTo(ST05TwoDoorsPhase.AwaitChengOwnJobDecision));

                    // Withdraw jobTermsReady right before assignment commit -> rejected without finishing.
                    jobTermsReady = false;
                    view.ChengStayLocalButton.onClick.Invoke();
                    Assert.That(host.IsCompleted, Is.False);

                    // Restore jobTermsReady -> assignment commit succeeds cleanly.
                    jobTermsReady = true;
                    view.ChengStayLocalButton.onClick.Invoke();
                    Assert.That(host.IsCompleted, Is.True);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RC02AndRC03HostsRejectStaleCommitWhenLiveConsentOrTrainingProofIsWithdrawn()
        {
            var root = new GameObject("RC02RC03LiveProofRoot");
            var values = new Dictionary<string, int>();
            SeedThroughM08Notice(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            bool shiLengConsent = true;
            bool trainingSecured = true;

            try
            {
                using (var rc02 = new RC02ZhengYuanHost(
                    root.transform, () => true, () => false, () => shiLengConsent, read, write))
                {
                    Assert.That(rc02.TryOpen(), Is.True);
                    var view = rc02.ActiveView;
                    view.ToothCheckButton.onClick.Invoke();
                    view.FlankQuestionButton.onClick.Invoke();
                    view.HandQuestionButton.onClick.Invoke();
                    view.HearConfessionButton.onClick.Invoke();
                    view.BeginAdoptionButton.onClick.Invoke();
                    Assert.That(rc02.Session.Stage, Is.EqualTo(RC02Stage.AdoptionNameMoment));
                    view.WaitQuietlyButton.onClick.Invoke();
                    Assert.That(rc02.Session.MotherCalledShiLeng, Is.True);
                    Assert.That(rc02.Session.ShiLengConsented, Is.True);

                    shiLengConsent = false;
                    view.ConfirmAdoptionButton.onClick.Invoke();
                    Assert.That(rc02.IsResolved, Is.False);

                    shiLengConsent = true;
                    view.ConfirmAdoptionButton.onClick.Invoke();
                    Assert.That(rc02.IsResolved, Is.True);
                }

                using (var rc03 = new RC03BaijiMineHost(
                    root.transform, () => true, () => true, () => trainingSecured, () => false, read, write))
                {
                    Assert.That(rc03.TryOpen(), Is.True);
                    var view = rc03.ActiveView;
                    view.CanteenVisitButton.onClick.Invoke();
                    view.PumpRoomVisitButton.onClick.Invoke();
                    view.CharterTalksButton.onClick.Invoke();
                    view.VerifyDiagramButton.onClick.Invoke();
                    view.SecureTrainingButton.onClick.Invoke();
                    Assert.That(rc03.Session.PumpDiagramVerified, Is.True);
                    Assert.That(rc03.Session.SuccessorTrainingSecured, Is.True);

                    trainingSecured = false;
                    view.AmendCharterButton.onClick.Invoke();
                    Assert.That(rc03.IsResolved, Is.False);

                    trainingSecured = true;
                    view.AmendCharterButton.onClick.Invoke();
                    Assert.That(rc03.IsResolved, Is.True);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
