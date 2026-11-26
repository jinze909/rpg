using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RC01ChenYanViewLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static RC01ChenYanSession CreateSession(Dictionary<string, int> values)
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

            Assert.That(RC01ChenYanSession.TryCreateFromSavedFacts(read, out RC01ChenYanSession session), Is.True);
            return session;
        }

        private static void AdvanceToRosterDecision(RC01ChenYanView view)
        {
            view.WristScarButton.onClick.Invoke();
            view.DoughTuneButton.onClick.Invoke();
            view.ShrineTabletButton.onClick.Invoke();
            view.ConfrontButton.onClick.Invoke();
            view.TalkLinSuButton.onClick.Invoke();
            view.TalkQiuniangButton.onClick.Invoke();
        }

        [Test]
        public void PartialChildTeardownDisposesViewAndBlocksClickLayoutAndLocaleRefresh()
        {
            var root = new GameObject("RC01 view partial teardown");
            var values = new Dictionary<string, int>();
            string previousLanguage = SoundwellUiLocalization.Language;
            RC01ChenYanView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                RC01ChenYanSession session = CreateSession(values);
                int calls = 0;
                view = new RC01ChenYanView(root.transform, session, _ => calls++);

                var scarBtn = view.WristScarButton;
                UnityEngine.Object.DestroyImmediate(view.Heading.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => scarBtn.onClick.Invoke());
                Assert.That(calls, Is.EqualTo(0));
                Assert.That(session.CluesFound, Is.EqualTo(RC01FerryClues.None));
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
            var root = new GameObject("RC01 view invalid aspects");
            var values = new Dictionary<string, int>();
            RC01ChenYanView view = null;
            try
            {
                RC01ChenYanSession session = CreateSession(values);
                view = new RC01ChenYanView(root.transform, session, _ => { });
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
            RC01ChenYanView view, CanvasGroup group, Dictionary<string, int> values)
        {
            var session = view.Session;
            var stage = session.Stage;
            var clues = session.CluesFound;
            var order = session.TalkOrder;
            bool admitted = session.IdentityAdmitted;
            bool linSu = session.LinSuTalked;
            bool qiuniang = session.QiuniangTalked;
            var before = new Dictionary<string, int>(values);
            Action clickAll = () =>
            {
                view.WristScarButton.onClick.Invoke();
                view.DoughTuneButton.onClick.Invoke();
                view.ShrineTabletButton.onClick.Invoke();
            };
            Action assertUnchanged = () =>
            {
                Assert.That(session.Stage, Is.EqualTo(stage));
                Assert.That(session.CluesFound, Is.EqualTo(clues));
                Assert.That(session.TalkOrder, Is.EqualTo(order));
                Assert.That(session.IdentityAdmitted, Is.EqualTo(admitted));
                Assert.That(session.LinSuTalked, Is.EqualTo(linSu));
                Assert.That(session.QiuniangTalked, Is.EqualTo(qiuniang));
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
        public void EffectiveInputLockPreservesQiuniangFirstAndExplicitRosterDecision()
        {
            AssertEffectiveInputLockPreservesInterviewOrderAndExplicitRosterDecision(true);
        }

        [Test]
        public void EffectiveInputLockPreservesLinSuFirstAndExplicitRosterDecision()
        {
            AssertEffectiveInputLockPreservesInterviewOrderAndExplicitRosterDecision(false);
        }

        private static void AssertEffectiveInputLockPreservesInterviewOrderAndExplicitRosterDecision(bool qiuniangFirst)
        {
            var root = new GameObject("RC01 effective input lock");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            RC01ChenYanView view = null;
            try
            {
                RC01ChenYanSession session = CreateSession(values);
                int submissions = 0;
                view = new RC01ChenYanView(root.transform, session, outcome =>
                {
                    submissions++;
                    Assert.That(session.TrySettleRosterEntry(outcome, read, write), Is.True);
                });
                var group = root.AddComponent<CanvasGroup>();

                AssertEffectiveInputLock(view, group, values);
                view.WristScarButton.onClick.Invoke();
                AssertEffectiveInputLock(view, group, values);
                view.DoughTuneButton.onClick.Invoke();
                view.ShrineTabletButton.onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(RC01InvestigationStage.IdentityConfrontation));
                AssertEffectiveInputLock(view, group, values);
                view.ConfrontButton.onClick.Invoke();

                Assert.That(session.Stage, Is.EqualTo(RC01InvestigationStage.TwoHomesTalk));
                AssertEffectiveInputLock(view, group, values);
                if (qiuniangFirst)
                    view.TalkQiuniangButton.onClick.Invoke();
                else
                    view.TalkLinSuButton.onClick.Invoke();
                AssertEffectiveInputLock(view, group, values);
                if (qiuniangFirst)
                    view.TalkLinSuButton.onClick.Invoke();
                else
                    view.TalkQiuniangButton.onClick.Invoke();

                Assert.That(session.Stage, Is.EqualTo(RC01InvestigationStage.RosterDecision));
                AssertEffectiveInputLock(view, group, values);
                Assert.That(submissions, Is.Zero);
                Assert.That(read(RC01ChenYanFacts.ConfirmedKey), Is.Zero);
                Assert.That(view.NewReturneeNameButton.IsInteractable(), Is.True);
                view.NewReturneeNameButton.onClick.Invoke();
                view.RestoreLivingButton.onClick.Invoke();

                Assert.That(submissions, Is.EqualTo(1));
                Assert.That(session.IsResolved, Is.True);
                Assert.That(RC01ChenYanFacts.TryRead(read, out RC01ChenYanRecord? saved), Is.True);
                Assert.That(saved.HasValue, Is.True);
                Assert.That(saved.Value.CluesFound, Is.EqualTo(RC01FerryClues.All));
                Assert.That(saved.Value.TalkOrder, Is.EqualTo(qiuniangFirst
                    ? RC01TalkOrder.QiuniangFirst : RC01TalkOrder.LinSuFirst));
                Assert.That(saved.Value.Atmosphere, Is.EqualTo(qiuniangFirst
                    ? RC01TalkAtmosphere.CalmWithLinSu : RC01TalkAtmosphere.GuardedWithQiuniang));
                Assert.That(saved.Value.Outcome, Is.EqualTo(RC01ChenYanOutcome.IssueNewReturneeName));
                Assert.That(saved.Value.LinSuRegisterNoteRecorded, Is.True);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ReentrantOutcomeSubmitClickIsIgnoredDuringSubmissionCallback()
        {
            var root = new GameObject("RC01 view reentrant submit");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            RC01ChenYanView view = null;
            try
            {
                RC01ChenYanSession session = CreateSession(values);
                int invocations = 0;
                view = new RC01ChenYanView(root.transform, session, outcome =>
                {
                    invocations++;
                    Assert.That(view.RestoreLivingButton.interactable, Is.False);
                    view.RestoreLivingButton.onClick.Invoke();
                    Assert.That(session.TrySettleRosterEntry(outcome, read, write), Is.True);
                });

                AdvanceToRosterDecision(view);
                view.RestoreLivingButton.onClick.Invoke();

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
            var root = new GameObject("RC01 view dispose in callback");
            var values = new Dictionary<string, int>();
            RC01ChenYanView view = null;
            try
            {
                RC01ChenYanSession session = CreateSession(values);
                view = new RC01ChenYanView(root.transform, session, _ =>
                {
                    view.Dispose();
                    throw new InvalidOperationException("Simulated RC01 teardown during submit");
                });

                AdvanceToRosterDecision(view);
                Assert.Throws<InvalidOperationException>(() => view.KeepDeadButton.onClick.Invoke());
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
