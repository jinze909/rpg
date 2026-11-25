using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ST06TaoJiHomecomingViewLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static ST06TaoJiHomecomingSession CreateSession(Dictionary<string, int> values)
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
                boardedFirstBatchCount: 300,
                lowerLevelEvacuatedCount: 0,
                M07RusFate.Arrested, M07LedgerState.Preserved,
                M07LiangChoice.StayOnIsland,
                M07YidaChoice.HandToInspector,
                taoJiRescued: true), read, write), Is.True);

            Assert.That(ST06TaoJiHomecomingSession.TryCreateFromSavedFacts(
                read, out ST06TaoJiHomecomingSession session), Is.True);
            return session;
        }

        [Test]
        public void PartialChildTeardownDisposesViewAndBlocksClickLayoutAndLocaleRefresh()
        {
            var root = new GameObject("ST06 view partial teardown");
            var values = new Dictionary<string, int>();
            string previousLanguage = SoundwellUiLocalization.Language;
            ST06TaoJiHomecomingView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                ST06TaoJiHomecomingSession session = CreateSession(values);
                int calls = 0;
                view = new ST06TaoJiHomecomingView(root.transform, session, _ => calls++);

                var refundBtn = view.RefundRebuildButton;
                UnityEngine.Object.DestroyImmediate(view.Heading.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => refundBtn.onClick.Invoke());
                Assert.That(calls, Is.EqualTo(0));
                Assert.That(view.Step, Is.EqualTo(ST06HomecomingViewStep.ResolveChair));
                Assert.That(view.Panel == null, Is.True);

                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(16f / 9f));
                Assert.DoesNotThrow(() => SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InvalidViewportAspectsAreIgnoredWithoutDistortingPanelScale()
        {
            var root = new GameObject("ST06 view invalid aspects");
            var values = new Dictionary<string, int>();
            ST06TaoJiHomecomingView view = null;
            try
            {
                ST06TaoJiHomecomingSession session = CreateSession(values);
                view = new ST06TaoJiHomecomingView(root.transform, session, _ => { });
                view.LayoutForViewportAspect(16f / 9f);
                Vector3 baselineScale = view.Panel.localScale;

                foreach (float invalidAspect in new[]
                    { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                {
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(invalidAspect));
                    Assert.That(view.Panel.localScale, Is.EqualTo(baselineScale));
                }
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EffectiveInputLockPreservesUnremediatedCoverUpWithoutDinner()
        {
            AssertEffectiveInputLockPreservesCoverUpAndDinnerFacts(false);
        }

        [Test]
        public void EffectiveInputLockAllowsExplicitRemediationAndDinnerAfterUnlock()
        {
            AssertEffectiveInputLockPreservesCoverUpAndDinnerFacts(true);
        }

        private static void AssertEffectiveInputLockPreservesCoverUpAndDinnerFacts(bool remediate)
        {
            var root = new GameObject("ST06 effective input lock");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            ST06TaoJiHomecomingView view = null;
            try
            {
                ST06TaoJiHomecomingSession session = CreateSession(values);
                int saves = 0;
                view = new ST06TaoJiHomecomingView(root.transform, session, s =>
                {
                    saves++;
                    Assert.That(s.TrySettle(read, (k, v) => values[k] = v), Is.True);
                });
                var group = root.AddComponent<CanvasGroup>();
                group.interactable = false;
                Assert.That(view.RefundRebuildButton.IsInteractable(), Is.False);
                view.RefundRebuildButton.onClick.Invoke();
                Assert.That(session.ChairResolution.HasValue, Is.False);
                group.interactable = true;
                view.Panel.gameObject.SetActive(false);
                view.TeachJointButton.onClick.Invoke();
                Assert.That(view.Step, Is.EqualTo(ST06HomecomingViewStep.ResolveChair));
                Assert.That(session.ChairResolution.HasValue, Is.False);

                view.Panel.gameObject.SetActive(true);
                view.CoverUpButton.onClick.Invoke();
                Assert.That(view.Step, Is.EqualTo(ST06HomecomingViewStep.PostResolution));
                Assert.That(session.HasUnremediatedCoverUpTrustLoss, Is.True);

                group.interactable = false;
                view.RemediateCoverUpButton.onClick.Invoke();
                view.HoldWelcomeDinnerButton.onClick.Invoke();
                view.SettleButton.onClick.Invoke();
                group.interactable = true;
                view.Panel.gameObject.SetActive(false);
                view.RemediateCoverUpButton.onClick.Invoke();
                view.HoldWelcomeDinnerButton.onClick.Invoke();
                view.SettleButton.onClick.Invoke();
                Assert.That(session.HasUnremediatedCoverUpTrustLoss, Is.True);
                Assert.That(session.WelcomeDinnerHeld, Is.False);
                Assert.That(session.IsSettled, Is.False);
                Assert.That(saves, Is.Zero);
                Assert.That(read(ST06TaoJiHomecomingFacts.ConfirmedKey), Is.Zero);

                view.Panel.gameObject.SetActive(true);
                if (remediate)
                {
                    view.RemediateCoverUpButton.onClick.Invoke();
                    view.HoldWelcomeDinnerButton.onClick.Invoke();
                }
                view.SettleButton.onClick.Invoke();
                view.SettleButton.onClick.Invoke();
                Assert.That(saves, Is.EqualTo(1));
                Assert.That(view.Step, Is.EqualTo(ST06HomecomingViewStep.Completed));
                Assert.That(ST06TaoJiHomecomingFacts.TryRead(read, out ST06HomecomingRecord? saved), Is.True);
                Assert.That(saved.HasValue, Is.True);
                Assert.That(saved.Value.ChairResolution, Is.EqualTo(ST06ChairResolution.CoverUp));
                Assert.That(saved.Value.CoverUpRemediated, Is.EqualTo(remediate));
                Assert.That(saved.Value.WelcomeDinnerHeld, Is.EqualTo(remediate));
                Assert.That(saved.Value.HasUnremediatedCoverUpTrustLoss, Is.EqualTo(!remediate));
                Assert.That(saved.Value.LetterStatus, Is.EqualTo(ST06LetterStatus.NotSent));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ReentrantSettleClickIsIgnoredDuringSettlementCallback()
        {
            var root = new GameObject("ST06 view reentrant settle");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            ST06TaoJiHomecomingView view = null;
            try
            {
                ST06TaoJiHomecomingSession session = CreateSession(values);
                int invocations = 0;
                view = new ST06TaoJiHomecomingView(root.transform, session, activeSession =>
                {
                    invocations++;
                    Assert.That(view.SettleButton.interactable, Is.False);
                    view.SettleButton.onClick.Invoke();
                    Assert.That(activeSession.TrySettle(read, write), Is.True);
                });

                view.TeachJointButton.onClick.Invoke();
                view.SettleButton.onClick.Invoke();

                Assert.That(invocations, Is.EqualTo(1));
                Assert.That(view.Step, Is.EqualTo(ST06HomecomingViewStep.Completed));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DisposingViewInsideThrowingCallbackDoesNotThrowOnFinallyRefresh()
        {
            var root = new GameObject("ST06 view dispose in callback");
            var values = new Dictionary<string, int>();
            ST06TaoJiHomecomingView view = null;
            try
            {
                ST06TaoJiHomecomingSession session = CreateSession(values);
                view = new ST06TaoJiHomecomingView(root.transform, session, _ =>
                {
                    view.Dispose();
                    throw new InvalidOperationException("Simulated ST06 teardown during settle");
                });

                view.RefundRebuildButton.onClick.Invoke();
                Assert.Throws<InvalidOperationException>(() => view.SettleButton.onClick.Invoke());
                Assert.That(view.IsDisposed, Is.True);
                Assert.DoesNotThrow(() => view.Refresh());
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(16f / 9f));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
