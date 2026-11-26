using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RC02ZhengYuanViewLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static RC02ZhengYuanSession CreateSession(Dictionary<string, int> values)
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

            Assert.That(RC02ZhengYuanSession.TryCreateFromSavedFacts(
                read, realZhengYuanFound: false, out RC02ZhengYuanSession session), Is.True);
            return session;
        }

        private static void AdvanceToDeskDecision(RC02ZhengYuanView view)
        {
            view.ToothCheckButton.onClick.Invoke();
            view.FlankQuestionButton.onClick.Invoke();
            view.HandQuestionButton.onClick.Invoke();
            view.HearConfessionButton.onClick.Invoke();
        }

        [Test]
        public void PartialChildTeardownDisposesViewAndBlocksClickLayoutAndLocaleRefresh()
        {
            var root = new GameObject("RC02 view partial teardown");
            var values = new Dictionary<string, int>();
            string previousLanguage = SoundwellUiLocalization.Language;
            RC02ZhengYuanView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                RC02ZhengYuanSession session = CreateSession(values);
                int calls = 0;
                view = new RC02ZhengYuanView(root.transform, session, () => true, _ => calls++);

                var toothBtn = view.ToothCheckButton;
                UnityEngine.Object.DestroyImmediate(view.Heading.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => toothBtn.onClick.Invoke());
                Assert.That(calls, Is.EqualTo(0));
                Assert.That(session.ChecksCompleted, Is.EqualTo(RC02IntakeChecks.None));
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
            var root = new GameObject("RC02 view invalid aspects");
            var values = new Dictionary<string, int>();
            RC02ZhengYuanView view = null;
            try
            {
                RC02ZhengYuanSession session = CreateSession(values);
                view = new RC02ZhengYuanView(root.transform, session, () => true, _ => { });
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

        private static void AssertEffectiveInputLock(
            RC02ZhengYuanView view, CanvasGroup group, Dictionary<string, int> values,
            Func<int> willingnessCalls, Func<int> submissions)
        {
            var session = view.Session;
            var stage = session.Stage;
            var checks = session.ChecksCompleted;
            bool confession = session.RoofConfessionHeard;
            bool motherCalled = session.MotherCalledShiLeng;
            bool consented = session.ShiLengConsented;
            int beforeWillingness = willingnessCalls();
            int beforeSubmissions = submissions();
            var before = new Dictionary<string, int>(values);
            Action clickAll = () =>
            {
                view.ToothCheckButton.onClick.Invoke();
                view.FlankQuestionButton.onClick.Invoke();
                view.HandQuestionButton.onClick.Invoke();
            };
            Action assertUnchanged = () =>
            {
                Assert.That(session.Stage, Is.EqualTo(stage));
                Assert.That(session.ChecksCompleted, Is.EqualTo(checks));
                Assert.That(session.RoofConfessionHeard, Is.EqualTo(confession));
                Assert.That(session.MotherCalledShiLeng, Is.EqualTo(motherCalled));
                Assert.That(session.ShiLengConsented, Is.EqualTo(consented));
                Assert.That(willingnessCalls(), Is.EqualTo(beforeWillingness));
                Assert.That(submissions(), Is.EqualTo(beforeSubmissions));
                CollectionAssert.AreEquivalent(before, values);
            };
            group.interactable = false;
            clickAll();
            assertUnchanged();
            group.interactable = true;
            view.Panel.gameObject.SetActive(false);
            clickAll();
            assertUnchanged();
            view.Panel.gameObject.SetActive(true);
        }

        [Test]
        public void EffectiveInputLockPreservesConsentAndExplicitAdoptionRegistration()
        {
            var root = new GameObject("RC02 effective input lock");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            RC02ZhengYuanView view = null;
            try
            {
                RC02ZhengYuanSession session = CreateSession(values);
                bool willing = false;
                int willingnessCalls = 0;
                int submissions = 0;
                view = new RC02ZhengYuanView(root.transform, session,
                    () => { willingnessCalls++; return willing; },
                    resolution =>
                    {
                        submissions++;
                        Assert.That(session.TrySettleResolution(resolution, read, write), Is.True);
                    });
                var group = root.AddComponent<CanvasGroup>();
                Action assertLocked = () => AssertEffectiveInputLock(
                    view, group, values, () => willingnessCalls, () => submissions);

                assertLocked();
                view.ToothCheckButton.onClick.Invoke();
                assertLocked();
                view.FlankQuestionButton.onClick.Invoke();
                view.HandQuestionButton.onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(RC02Stage.RoofConfession));
                assertLocked();
                view.HearConfessionButton.onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(RC02Stage.DeskDecision));
                assertLocked();

                view.BeginAdoptionButton.onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(RC02Stage.AdoptionNameMoment));
                assertLocked();
                view.WaitQuietlyButton.onClick.Invoke();
                Assert.That(willingnessCalls, Is.EqualTo(1));
                Assert.That(session.Stage, Is.EqualTo(RC02Stage.DeskDecision));
                Assert.That(session.MotherCalledShiLeng, Is.False);
                Assert.That(session.ShiLengConsented, Is.False);
                Assert.That(read(RC02ZhengYuanFacts.ConfirmedKey), Is.Zero);

                willing = true;
                assertLocked();
                view.BeginAdoptionButton.onClick.Invoke();
                assertLocked();
                view.WaitQuietlyButton.onClick.Invoke();
                Assert.That(willingnessCalls, Is.EqualTo(2));
                Assert.That(session.MotherCalledShiLeng, Is.True);
                Assert.That(session.ShiLengConsented, Is.True);
                assertLocked();
                Assert.That(submissions, Is.Zero);
                Assert.That(read(RC02ZhengYuanFacts.ConfirmedKey), Is.Zero);

                view.ConfirmAdoptionButton.onClick.Invoke();
                view.ExposeButton.onClick.Invoke();
                Assert.That(submissions, Is.EqualTo(1));
                Assert.That(session.IsResolved, Is.True);
                Assert.That(RC02ZhengYuanFacts.TryRead(read, out RC02ZhengYuanRecord? saved), Is.True);
                Assert.That(saved.HasValue, Is.True);
                Assert.That(saved.Value.ChecksCompleted, Is.EqualTo(RC02IntakeChecks.All));
                Assert.That(saved.Value.RoofConfessionHeard, Is.True);
                Assert.That(saved.Value.Resolution, Is.EqualTo(RC02Resolution.AdoptAsShiLeng));
                Assert.That(saved.Value.MotherCalledShiLeng, Is.True);
                Assert.That(saved.Value.ShiLengConsented, Is.True);
                Assert.That(saved.Value.RealZhengYuanFound, Is.False);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ReentrantResolutionSubmitClickIsIgnoredDuringSubmissionCallback()
        {
            var root = new GameObject("RC02 view reentrant submit");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            RC02ZhengYuanView view = null;
            try
            {
                RC02ZhengYuanSession session = CreateSession(values);
                int invocations = 0;
                view = new RC02ZhengYuanView(root.transform, session, () => true, resolution =>
                {
                    invocations++;
                    Assert.That(view.ExceptionButton.interactable, Is.False);
                    view.ExceptionButton.onClick.Invoke();
                    Assert.That(session.TrySettleResolution(resolution, read, write), Is.True);
                });

                AdvanceToDeskDecision(view);
                view.ExceptionButton.onClick.Invoke();

                Assert.That(invocations, Is.EqualTo(1));
                Assert.That(session.IsResolved, Is.True);
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
            var root = new GameObject("RC02 view dispose in callback");
            var values = new Dictionary<string, int>();
            RC02ZhengYuanView view = null;
            try
            {
                RC02ZhengYuanSession session = CreateSession(values);
                view = new RC02ZhengYuanView(root.transform, session, () => true, _ =>
                {
                    view.Dispose();
                    throw new InvalidOperationException("Simulated RC02 teardown during submit");
                });

                AdvanceToDeskDecision(view);
                Assert.Throws<InvalidOperationException>(() => view.ExposeButton.onClick.Invoke());
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
