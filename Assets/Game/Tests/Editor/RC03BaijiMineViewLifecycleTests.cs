using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class RC03BaijiMineViewLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static RC03BaijiMineSession CreateSession(Dictionary<string, int> values)
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

            Assert.That(RC03BaijiMineSession.TryCreateFromSavedFacts(
                read, out RC03BaijiMineSession session), Is.True);
            return session;
        }

        private static void CompleteIslandVisits(RC03BaijiMineView view)
        {
            view.CanteenVisitButton.onClick.Invoke();
            view.PumpRoomVisitButton.onClick.Invoke();
            view.CharterTalksButton.onClick.Invoke();
        }

        [Test]
        public void EffectiveInputLockProtectsIslandEvidenceConsentAndExplicitResolution()
        {
            var root = new GameObject("RC03 effective input lock");
            var group = root.AddComponent<CanvasGroup>();
            var values = new Dictionary<string, int>();
            RC03BaijiMineView view = null;
            try
            {
                RC03BaijiMineSession session = CreateSession(values);
                int diagramCalls = 0, trainingCalls = 0, consentCalls = 0, submits = 0;
                bool matched = false, trained = false, agreed = false;
                view = new RC03BaijiMineView(root.transform, session,
                    () => { diagramCalls++; return matched; },
                    () => { trainingCalls++; return trained; },
                    () => { consentCalls++; return agreed; },
                    resolution =>
                    {
                        submits++;
                        Assert.That(session.TrySettleResolution(
                            resolution, Reader(values), (key, value) => values[key] = value), Is.True);
                    });

                void AssertBlockedClicks()
                {
                    var stage = session.Stage;
                    var visits = session.VisitsCompleted;
                    bool diagram = session.PumpDiagramVerified;
                    bool training = session.SuccessorTrainingSecured;
                    bool consent = session.LanRuoConsentedToStay;
                    int[] counts = { diagramCalls, trainingCalls, consentCalls, submits };
                    var before = new Dictionary<string, int>(values);
                    Button[] buttons = { view.CanteenVisitButton, view.PumpRoomVisitButton,
                        view.CharterTalksButton, view.AmendCharterButton,
                        view.SecretEvacuateButton, view.StayOneYearButton };

                    group.interactable = false;
                    foreach (Button button in buttons) button.onClick.Invoke();
                    group.interactable = true;
                    view.Panel.gameObject.SetActive(false);
                    foreach (Button button in buttons) button.onClick.Invoke();
                    view.Panel.gameObject.SetActive(true);

                    Assert.That(session.Stage, Is.EqualTo(stage));
                    Assert.That(session.VisitsCompleted, Is.EqualTo(visits));
                    Assert.That(session.PumpDiagramVerified, Is.EqualTo(diagram));
                    Assert.That(session.SuccessorTrainingSecured, Is.EqualTo(training));
                    Assert.That(session.LanRuoConsentedToStay, Is.EqualTo(consent));
                    Assert.That(new[] { diagramCalls, trainingCalls, consentCalls, submits },
                        Is.EqualTo(counts));
                    CollectionAssert.AreEquivalent(before, values);
                }

                AssertBlockedClicks();
                view.CanteenVisitButton.onClick.Invoke();
                AssertBlockedClicks();
                view.PumpRoomVisitButton.onClick.Invoke();
                view.CharterTalksButton.onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(RC03Stage.ResolutionDecision));

                AssertBlockedClicks();
                view.SecureTrainingButton.onClick.Invoke();
                Assert.That(trainingCalls, Is.Zero);
                view.VerifyDiagramButton.onClick.Invoke();
                Assert.That(session.PumpDiagramVerified, Is.False);
                matched = true;
                AssertBlockedClicks();
                view.VerifyDiagramButton.onClick.Invoke();
                Assert.That(session.PumpDiagramVerified, Is.True);
                view.SecureTrainingButton.onClick.Invoke();
                Assert.That(session.SuccessorTrainingSecured, Is.False);
                trained = true;
                AssertBlockedClicks();
                view.SecureTrainingButton.onClick.Invoke();
                Assert.That(session.SuccessorTrainingSecured, Is.True);
                Assert.That(view.AmendCharterButton.IsInteractable(), Is.True);

                view.AskLanRuoStayButton.onClick.Invoke();
                Assert.That(session.LanRuoConsentedToStay, Is.False);
                view.StayOneYearButton.onClick.Invoke();
                Assert.That(submits, Is.Zero);
                agreed = true;
                AssertBlockedClicks();
                view.AskLanRuoStayButton.onClick.Invoke();
                Assert.That(session.LanRuoConsentedToStay, Is.True);
                Assert.That(view.StayOneYearButton.IsInteractable(), Is.True);
                AssertBlockedClicks();
                Assert.That(values.ContainsKey(RC03BaijiMineFacts.ConfirmedKey), Is.False);

                view.StayOneYearButton.onClick.Invoke();
                Assert.That(submits, Is.EqualTo(1));
                Assert.That(session.IsResolved, Is.True);
                Assert.That(RC03BaijiMineFacts.TryRead(Reader(values), out RC03BaijiMineRecord? record), Is.True);
                Assert.That(record.HasValue, Is.True);
                Assert.That(record.Value.Resolution, Is.EqualTo(RC03Resolution.StayOneYearByConsent));
                Assert.That(record.Value.LanRuoConsentedToStay, Is.True);
                Assert.That(record.Value.PumpDiagramVerified, Is.True);
                Assert.That(record.Value.SuccessorTrainingSecured, Is.True);
                Assert.That(record.Value.ChairmanLuoRegisterNoteRecorded, Is.True);
                Assert.That(record.Value.IslandWaterDisrupted, Is.False);
                Assert.That(record.Value.LanRuoQueuedForNextBatch, Is.False);
                AssertBlockedClicks();
                view.StayOneYearButton.onClick.Invoke();
                Assert.That(submits, Is.EqualTo(1));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PartialChildTeardownDisposesViewAndBlocksTopClickLayoutAndLocaleRefresh()
        {
            var root = new GameObject("RC03 view partial teardown top");
            var values = new Dictionary<string, int>();
            string previousLanguage = SoundwellUiLocalization.Language;
            RC03BaijiMineView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                RC03BaijiMineSession session = CreateSession(values);
                int calls = 0;
                view = new RC03BaijiMineView(
                    root.transform, session, () => true, () => true, () => true, _ => calls++);

                var canteenBtn = view.CanteenVisitButton;
                UnityEngine.Object.DestroyImmediate(view.Heading.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => canteenBtn.onClick.Invoke());
                Assert.That(calls, Is.EqualTo(0));
                Assert.That(session.VisitsCompleted, Is.EqualTo(RC03IslandVisits.None));
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
        public void PartialChildTeardownOnBottomClickDisposesViewCleanly()
        {
            var root = new GameObject("RC03 view partial teardown bottom");
            var values = new Dictionary<string, int>();
            RC03BaijiMineView view = null;
            try
            {
                RC03BaijiMineSession session = CreateSession(values);
                int calls = 0;
                view = new RC03BaijiMineView(
                    root.transform, session, () => true, () => true, () => true, _ => calls++);
                CompleteIslandVisits(view);

                var evacBtn = view.SecretEvacuateButton;
                UnityEngine.Object.DestroyImmediate(view.Body.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => evacBtn.onClick.Invoke());
                Assert.That(calls, Is.EqualTo(0));
                Assert.That(view.Panel == null, Is.True);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InvalidViewportAspectsAreIgnoredWithoutDistortingPanelScale()
        {
            var root = new GameObject("RC03 view invalid aspects");
            var values = new Dictionary<string, int>();
            RC03BaijiMineView view = null;
            try
            {
                RC03BaijiMineSession session = CreateSession(values);
                view = new RC03BaijiMineView(
                    root.transform, session, () => true, () => true, () => true, _ => { });
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
        public void ReentrantResolutionSubmitAndDisposeInsideThrowingCallbackAreGuarded()
        {
            var root = new GameObject("RC03 view reentrant and dispose");
            var values1 = new Dictionary<string, int>();
            var values2 = new Dictionary<string, int>();
            RC03BaijiMineView view1 = null;
            RC03BaijiMineView view2 = null;
            try
            {
                RC03BaijiMineSession session1 = CreateSession(values1);
                Func<string, int> read1 = Reader(values1);
                Action<string, int> write1 = (k, v) => values1[k] = v;
                int invocations = 0;
                view1 = new RC03BaijiMineView(
                    root.transform, session1, () => true, () => true, () => true, resolution =>
                    {
                        invocations++;
                        Assert.That(view1.SecretEvacuateButton.interactable, Is.False);
                        view1.SecretEvacuateButton.onClick.Invoke();
                        Assert.That(session1.TrySettleResolution(resolution, read1, write1), Is.True);
                    });

                CompleteIslandVisits(view1);
                view1.SecretEvacuateButton.onClick.Invoke();
                Assert.That(invocations, Is.EqualTo(1));
                Assert.That(session1.IsResolved, Is.True);

                RC03BaijiMineSession session2 = CreateSession(values2);
                view2 = new RC03BaijiMineView(
                    root.transform, session2, () => true, () => true, () => true, _ =>
                    {
                        view2.Dispose();
                        throw new InvalidOperationException("Simulated RC03 teardown during submit");
                    });

                CompleteIslandVisits(view2);
                Assert.Throws<InvalidOperationException>(() => view2.SecretEvacuateButton.onClick.Invoke());
                Assert.That(view2.IsDisposed, Is.True);
                Assert.DoesNotThrow(() => view2.Refresh());
                Assert.DoesNotThrow(() => view2.LayoutForViewportAspect(16f / 9f));
            }
            finally
            {
                view1?.Dispose();
                view2?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
