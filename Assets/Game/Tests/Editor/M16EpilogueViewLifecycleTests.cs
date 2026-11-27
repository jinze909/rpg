using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M16EpilogueViewLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static M16EpilogueSession CreateSession(Dictionary<string, int> values)
        {
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(M15FinalRollCallFacts.TryRecord(new M15FinalRollCallRecord(
                M15RollCallReader.Protagonist,
                M15FinalIdentityResponse.PublicTrueName,
                M15PensionClosure.StoppedAndReconciled,
                M15SteleInscription.AddReturnedAliveLine,
                consentedVerifiedNamesCount: 0,
                xiaoqiCalledByNumberSeventyTwo: false), read, write), Is.True);

            Assert.That(M16EpilogueSession.TryCreateFromSavedFacts(
                read, shenLiWillingToMeet: true,
                M16RespectAgencyTone.RespectedOthersChoices,
                out M16EpilogueSession session), Is.True);
            return session;
        }

        [Test]
        public void PartialChildTeardownDisposesViewAndBlocksAdvanceLayoutAndLocaleRefresh()
        {
            var root = new GameObject("M16 view partial teardown advance");
            var values = new Dictionary<string, int>();
            string previousLanguage = SoundwellUiLocalization.Language;
            M16EpilogueView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                M16EpilogueSession session = CreateSession(values);
                int calls = 0;
                view = new M16EpilogueView(root.transform, session, _ => calls++);

                var advanceBtn = view.ProceedToDeskButton;
                UnityEngine.Object.DestroyImmediate(view.Heading.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => advanceBtn.onClick.Invoke());
                Assert.That(calls, Is.EqualTo(0));
                Assert.That(session.Phase, Is.EqualTo(M16Phase.AfternoonWalk));
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
        public void PartialChildTeardownOnChoiceClickDisposesViewCleanly()
        {
            var root = new GameObject("M16 view partial teardown choice");
            var values = new Dictionary<string, int>();
            M16EpilogueView view = null;
            try
            {
                M16EpilogueSession session = CreateSession(values);
                view = new M16EpilogueView(root.transform, session, _ => { });

                var workshopBtn = view.VisitWorkshopButton;
                UnityEngine.Object.DestroyImmediate(view.Body.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => workshopBtn.onClick.Invoke());
                Assert.That(session.StopsVisited, Is.EqualTo(M16WalkStops.None));
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
            var root = new GameObject("M16 view invalid aspects");
            var values = new Dictionary<string, int>();
            M16EpilogueView view = null;
            try
            {
                M16EpilogueSession session = CreateSession(values);
                view = new M16EpilogueView(root.transform, session, _ => { });
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
        public void EffectiveInputLockBlocksWalkDeskAndFinalStandingUntilRestored()
        {
            var root = new GameObject("M16 epilogue effective input lock");
            var values = new Dictionary<string, int>();
            M16EpilogueView view = null;
            try
            {
                M16EpilogueLettersViewTests.SeedM15WithCompletedRC01AndST02(values);
                Func<string, int> read = Reader(values);
                Assert.That(M16EpilogueSession.TryCreateFromSavedFacts(
                    read, true, M16RespectAgencyTone.RespectedOthersChoices,
                    out M16EpilogueSession session), Is.True);
                int saves = 0;
                view = new M16EpilogueView(root.transform, session, spot =>
                {
                    saves++;
                    Assert.That(session.TryChooseStandingSpotAndCommit(
                        spot, read, (k, v) => values[k] = v), Is.True);
                });
                var group = root.AddComponent<CanvasGroup>();
                group.interactable = false;
                Assert.That(view.ProceedToDeskButton.IsInteractable(), Is.False);
                view.VisitWorkshopButton.onClick.Invoke();
                view.ProceedToDeskButton.onClick.Invoke();
                Assert.That(session.StopsVisited, Is.EqualTo(M16WalkStops.None));
                Assert.That(session.Phase, Is.EqualTo(M16Phase.AfternoonWalk));
                group.interactable = true;
                view.Panel.gameObject.SetActive(false);
                view.VisitQiQueButton.onClick.Invoke();
                view.ProceedToDeskButton.onClick.Invoke();
                Assert.That(session.StopsVisited, Is.EqualTo(M16WalkStops.None));
                Assert.That(session.Phase, Is.EqualTo(M16Phase.AfternoonWalk));
                view.Panel.gameObject.SetActive(true);
                view.VisitWorkshopButton.onClick.Invoke();
                Assert.That(session.StopsVisited, Is.EqualTo(M16WalkStops.WorkshopStop));
                view.ProceedToDeskButton.onClick.Invoke();

                Assert.That(session.Phase, Is.EqualTo(M16Phase.DeskLettersAndRegister));
                Assert.That(view.ReadNextLetterButton.IsInteractable(), Is.True);
                group.interactable = false;
                view.ReadNextLetterButton.onClick.Invoke();
                view.ReviewRegisterButton.onClick.Invoke();
                view.StepOutsideButton.onClick.Invoke();
                Assert.That(session.LettersRead, Is.EqualTo(M16DeliveredLettersFlags.None));
                Assert.That(session.DeskRegisterReviewed, Is.False);
                Assert.That(session.Phase, Is.EqualTo(M16Phase.DeskLettersAndRegister));
                group.interactable = true;
                view.Panel.gameObject.SetActive(false);
                view.ReadNextLetterButton.onClick.Invoke();
                view.StepOutsideButton.onClick.Invoke();
                Assert.That(session.LettersRead, Is.EqualTo(M16DeliveredLettersFlags.None));
                Assert.That(session.Phase, Is.EqualTo(M16Phase.DeskLettersAndRegister));
                view.Panel.gameObject.SetActive(true);
                view.ReadNextLetterButton.onClick.Invoke();
                Assert.That(session.LettersRead, Is.EqualTo(M16DeliveredLettersFlags.RC01ChenYan));
                view.ReviewRegisterButton.onClick.Invoke();
                Assert.That(session.DeskRegisterReviewed, Is.True);
                view.StepOutsideButton.onClick.Invoke();

                Assert.That(session.Phase, Is.EqualTo(M16Phase.FinalStandingAndFadeOut));
                group.interactable = false;
                view.PierButton.onClick.Invoke();
                group.interactable = true;
                view.Panel.gameObject.SetActive(false);
                view.WorkshopDoorButton.onClick.Invoke();
                Assert.That(saves, Is.Zero);
                Assert.That(read(M16EpilogueFacts.ConfirmedKey), Is.Zero);
                view.Panel.gameObject.SetActive(true);
                view.SteleButton.onClick.Invoke();
                Assert.That(saves, Is.EqualTo(1));
                Assert.That(session.IsCompleted, Is.True);
                Assert.That(M16EpilogueFacts.TryRead(read, out M16EpilogueRecord? saved), Is.True);
                Assert.That(saved.Value.StopsVisited, Is.EqualTo(M16WalkStops.WorkshopStop));
                Assert.That(saved.Value.LettersReadMask, Is.EqualTo(M16DeliveredLettersFlags.RC01ChenYan));
                Assert.That(saved.Value.DeskRegisterReviewed, Is.True);
                Assert.That(saved.Value.StandingSpot, Is.EqualTo((M16StandingSpot)2));
                view.PierButton.onClick.Invoke();
                Assert.That(saves, Is.EqualTo(1));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DisabledCanvasRejectsInvisibleWalkDeskAndStandingActionsUntilRestored()
        {
            var root = new GameObject("M16 disabled canvas input");
            var values = new Dictionary<string, int>();
            M16EpilogueView view = null;
            try
            {
                M16EpilogueLettersViewTests.SeedM15WithCompletedRC01AndST02(values);
                Func<string, int> read = Reader(values);
                Assert.That(M16EpilogueSession.TryCreateFromSavedFacts(
                    read, true, M16RespectAgencyTone.RespectedOthersChoices,
                    out M16EpilogueSession session), Is.True);
                int saves = 0;
                view = new M16EpilogueView(root.transform, session, spot =>
                {
                    saves++;
                    Assert.That(session.TryChooseStandingSpotAndCommit(
                        spot, read, (k, v) => values[k] = v), Is.True);
                });
                var canvas = view.Panel.GetComponentInParent<Canvas>();
                Assert.That(canvas, Is.Not.Null);

                canvas.enabled = false;
                view.VisitWorkshopButton.onClick.Invoke();
                view.ProceedToDeskButton.onClick.Invoke();
                Assert.That(session.StopsVisited, Is.EqualTo(M16WalkStops.None));
                Assert.That(session.Phase, Is.EqualTo(M16Phase.AfternoonWalk));
                canvas.enabled = true;
                view.VisitWorkshopButton.onClick.Invoke();
                view.ProceedToDeskButton.onClick.Invoke();
                Assert.That(session.StopsVisited, Is.EqualTo(M16WalkStops.WorkshopStop));
                Assert.That(session.Phase, Is.EqualTo(M16Phase.DeskLettersAndRegister));

                canvas.enabled = false;
                view.ReadNextLetterButton.onClick.Invoke();
                view.ReviewRegisterButton.onClick.Invoke();
                view.StepOutsideButton.onClick.Invoke();
                Assert.That(session.LettersRead, Is.EqualTo(M16DeliveredLettersFlags.None));
                Assert.That(session.DeskRegisterReviewed, Is.False);
                Assert.That(session.Phase, Is.EqualTo(M16Phase.DeskLettersAndRegister));
                canvas.enabled = true;
                view.ReadNextLetterButton.onClick.Invoke();
                view.ReviewRegisterButton.onClick.Invoke();
                view.StepOutsideButton.onClick.Invoke();
                Assert.That(session.LettersRead, Is.EqualTo(M16DeliveredLettersFlags.RC01ChenYan));
                Assert.That(session.DeskRegisterReviewed, Is.True);
                Assert.That(session.Phase, Is.EqualTo(M16Phase.FinalStandingAndFadeOut));

                canvas.enabled = false;
                view.PierButton.onClick.Invoke();
                Assert.That(saves, Is.Zero);
                Assert.That(read(M16EpilogueFacts.ConfirmedKey), Is.Zero);
                Assert.That(session.IsCompleted, Is.False);
                canvas.enabled = true;
                view.SteleButton.onClick.Invoke();
                Assert.That(saves, Is.EqualTo(1));
                Assert.That(session.IsCompleted, Is.True);
                Assert.That(M16EpilogueFacts.TryRead(read, out M16EpilogueRecord? saved), Is.True);
                Assert.That(saved.Value.StandingSpot, Is.EqualTo(M16StandingSpot.MemorialSteleSunset));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ReentrantStandingSpotClickAndDisposeInsideThrowingCallbackAreGuarded()
        {
            var root = new GameObject("M16 view reentrant and dispose");
            var values1 = new Dictionary<string, int>();
            var values2 = new Dictionary<string, int>();
            M16EpilogueView view1 = null;
            M16EpilogueView view2 = null;
            try
            {
                M16EpilogueSession session1 = CreateSession(values1);
                Func<string, int> read1 = Reader(values1);
                Action<string, int> write1 = (k, v) => values1[k] = v;
                int invocations = 0;
                view1 = new M16EpilogueView(root.transform, session1, spot =>
                {
                    invocations++;
                    Assert.That(view1.PierButton.interactable, Is.False);
                    view1.PierButton.onClick.Invoke();
                    Assert.That(session1.TryChooseStandingSpotAndCommit(spot, read1, write1), Is.True);
                });

                view1.ProceedToDeskButton.onClick.Invoke();
                view1.StepOutsideButton.onClick.Invoke();
                view1.PierButton.onClick.Invoke();
                Assert.That(invocations, Is.EqualTo(1));
                Assert.That(session1.IsCompleted, Is.True);

                M16EpilogueSession session2 = CreateSession(values2);
                view2 = new M16EpilogueView(root.transform, session2, _ =>
                {
                    view2.Dispose();
                    throw new InvalidOperationException("Simulated M16 teardown during submit");
                });

                view2.ProceedToDeskButton.onClick.Invoke();
                view2.StepOutsideButton.onClick.Invoke();
                Assert.Throws<InvalidOperationException>(() => view2.SteleButton.onClick.Invoke());
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
