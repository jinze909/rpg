using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ST06TaoJiHomecomingViewTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedM07TaoJiRescued(Dictionary<string, int> values)
        {
            Action<string, int> write = (key, value) => values[key] = value;
            Func<string, int> read = Reader(values);

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
                    M06RescuePlan.OfficialHandover,
                    true,
                    M06LetterDeliveryOutcome.NotCarried,
                    true),
                read, write), Is.True);
            Assert.That(M07ExtractionFacts.TryRecord(
                new M07ExtractionRecord(
                    M07ExecutedRoute.OfficialHandover,
                    M07PumpShutdownState.KeptRunning,
                    M07RouteCostFlags.HarborSupplyDebt,
                    boardedFirstBatchCount: 300,
                    lowerLevelEvacuatedCount: 0,
                    M07RusFate.Arrested,
                    M07LedgerState.Preserved,
                    M07LiangChoice.StayOnIsland,
                    M07YidaChoice.HandToInspector,
                    taoJiRescued: true),
                read, write), Is.True);
        }

        [Test]
        public void CoverUpShowsRemediateOptionAndSettlesRemediatedWithWelcomeDinnerOnce()
        {
            var root = new GameObject("ST06 view coverup");
            var values = new Dictionary<string, int>();
            SeedM07TaoJiRescued(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            string previousLanguage = SoundwellUiLocalization.Language;
            ST06TaoJiHomecomingView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(ST06TaoJiHomecomingSession.TryCreateFromSavedFacts(read, out var session), Is.True);
                int saves = 0;
                view = new ST06TaoJiHomecomingView(root.transform, session, s =>
                {
                    saves++;
                    Assert.That(s.TrySettle(read, write), Is.True);
                });

                view.CoverUpButton.onClick.Invoke();
                Assert.That(view.Step, Is.EqualTo(ST06HomecomingViewStep.PostResolution));
                Assert.That(view.RemediateCoverUpButton.gameObject.activeSelf, Is.True);
                Assert.That(view.Body.text, Does.Contain("信誉损失尚未弥补"));

                view.RemediateCoverUpButton.onClick.Invoke();
                Assert.That(view.RemediateCoverUpButton.gameObject.activeSelf, Is.False);
                view.HoldWelcomeDinnerButton.onClick.Invoke();
                Assert.That(view.HoldWelcomeDinnerButton.interactable, Is.False);

                view.SettleButton.onClick.Invoke();
                view.SettleButton.onClick.Invoke();
                Assert.That(saves, Is.EqualTo(1));
                Assert.That(view.Step, Is.EqualTo(ST06HomecomingViewStep.Completed));
                Assert.That(ST06TaoJiHomecomingFacts.TryRead(read, out ST06HomecomingRecord? saved), Is.True);
                Assert.That(saved.Value.ChairResolution, Is.EqualTo(ST06ChairResolution.CoverUp));
                Assert.That(saved.Value.CoverUpRemediated, Is.True);
                Assert.That(saved.Value.WelcomeDinnerHeld, Is.True);
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NonCoverUpResolutionHidesRemediateButtonAndSettlesCleanly()
        {
            var root = new GameObject("ST06 view teach");
            var values = new Dictionary<string, int>();
            SeedM07TaoJiRescued(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            ST06TaoJiHomecomingView view = null;
            try
            {
                Assert.That(ST06TaoJiHomecomingSession.TryCreateFromSavedFacts(read, out var session), Is.True);
                view = new ST06TaoJiHomecomingView(root.transform, session, s =>
                {
                    Assert.That(s.TrySettle(read, write), Is.True);
                });

                view.TeachJointButton.onClick.Invoke();
                Assert.That(view.RemediateCoverUpButton.gameObject.activeSelf, Is.False);
                view.SettleButton.onClick.Invoke();
                Assert.That(ST06TaoJiHomecomingFacts.TryRead(read, out ST06HomecomingRecord? saved), Is.True);
                Assert.That(saved.Value.ChairResolution, Is.EqualTo(ST06ChairResolution.TeachJointTogether));
                Assert.That(saved.Value.HasUnremediatedCoverUpTrustLoss, Is.False);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InterruptedSettleSaveRetainsSettleButtonForCleanRetry()
        {
            var root = new GameObject("ST06 view retry");
            var values = new Dictionary<string, int>();
            SeedM07TaoJiRescued(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            ST06TaoJiHomecomingView view = null;
            try
            {
                Assert.That(ST06TaoJiHomecomingSession.TryCreateFromSavedFacts(read, out var session), Is.True);
                int attempts = 0;
                view = new ST06TaoJiHomecomingView(root.transform, session, s =>
                {
                    attempts++;
                    if (attempts == 1)
                        throw new InvalidOperationException("Interrupted ST06 write");
                    Assert.That(s.TrySettle(read, write), Is.True);
                });

                view.RefundRebuildButton.onClick.Invoke();
                Assert.Throws<InvalidOperationException>(() => view.SettleButton.onClick.Invoke());
                Assert.That(view.Step, Is.EqualTo(ST06HomecomingViewStep.PostResolution));
                Assert.That(view.SettleButton.interactable, Is.True);

                view.SettleButton.onClick.Invoke();
                Assert.That(attempts, Is.EqualTo(2));
                Assert.That(view.Step, Is.EqualTo(ST06HomecomingViewStep.Completed));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LocaleSwitchAspectScalingAndDisposalGuardsWorkCleanly()
        {
            var root = new GameObject("ST06 view locale");
            var values = new Dictionary<string, int>();
            SeedM07TaoJiRescued(values);
            Func<string, int> read = Reader(values);
            string previousLanguage = SoundwellUiLocalization.Language;
            ST06TaoJiHomecomingView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                Assert.That(ST06TaoJiHomecomingSession.TryCreateFromSavedFacts(read, out var session), Is.True);
                view = new ST06TaoJiHomecomingView(root.transform, session, _ => { });
                Assert.That(view.Heading.text, Does.Contain("BROKEN CHAIR JOINT"));
                Assert.That(view.Panel.anchorMin.y, Is.EqualTo(0f));

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.Heading.text, Does.Contain("修错的椅子"));
                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(aspect));

                var btn = view.RefundRebuildButton;
                view.Dispose();
                btn.onClick.Invoke();
                Assert.That(view.Step, Is.EqualTo(ST06HomecomingViewStep.ResolveChair));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
