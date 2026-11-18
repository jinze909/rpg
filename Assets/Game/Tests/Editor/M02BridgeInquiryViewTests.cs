using System.Collections;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Rpg.Tests.Editor
{
    public sealed class M02BridgeInquiryViewTests
    {
        private GameObject root;
        private M02BridgeInquiryView view;
        private string priorLanguage;

        [SetUp]
        public void SetUp()
        {
            priorLanguage = SoundwellUiLocalization.Language;
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            root = new GameObject("M02 bridge investigation test");
        }

        [TearDown]
        public void TearDown()
        {
            view?.Dispose();
            if (root != null)
                Object.DestroyImmediate(root);
            SoundwellUiLocalization.SetLanguage(priorLanguage);
        }

        [Test]
        public void ActualButtonsRevealDistinctCluesAndFinishOnlyOnce()
        {
            var inquiry = new M02BridgeInquiry();
            inquiry.Begin();
            int completed = 0;
            M02BridgeEvidence received = M02BridgeEvidence.None;
            view = new M02BridgeInquiryView(root.transform, inquiry, clues =>
            {
                completed++;
                received = clues;
            });

            view.GetEvidenceButton(0).onClick.Invoke();
            view.GetEvidenceButton(2).onClick.Invoke();
            Assert.That(view.Narrative.text, Does.Contain("third bell"));
            view.ContinueButton.onClick.Invoke();
            Assert.That(inquiry.Location, Is.EqualTo(M02EvidenceLocation.HarborArchive));
            Assert.That(view.GetEvidenceButton(1).gameObject.activeSelf, Is.False);
            view.GetEvidenceButton(0).onClick.Invoke();
            Assert.That(view.Narrative.text, Does.Contain("Han Qiao"));
            view.ContinueButton.onClick.Invoke();
            view.ContinueButton.onClick.Invoke();

            Assert.That(completed, Is.EqualTo(1));
            Assert.That(received, Is.EqualTo(M02BridgeEvidence.CleanCableCut |
                M02BridgeEvidence.ThirdBellSignal | M02BridgeEvidence.WarReportDraft));
            Assert.That(inquiry.Location, Is.EqualTo(M02EvidenceLocation.Finished));
        }

        [Test]
        public void GamepadFocusPrefersUnreadCluesThenContinueWithoutRevealingAnyForFree()
        {
            var inquiry = new M02BridgeInquiry();
            inquiry.Begin();
            view = new M02BridgeInquiryView(root.transform, inquiry, _ => { });

            Assert.That(view.PreferredFocusTarget, Is.SameAs(view.GetEvidenceButton(0)));
            Assert.That(inquiry.Found, Is.EqualTo(M02BridgeEvidence.None));
            view.GetEvidenceButton(0).onClick.Invoke();
            Assert.That(view.PreferredFocusTarget, Is.SameAs(view.GetEvidenceButton(1)));
            view.GetEvidenceButton(1).onClick.Invoke();
            Assert.That(view.PreferredFocusTarget, Is.SameAs(view.GetEvidenceButton(2)));
            view.GetEvidenceButton(2).onClick.Invoke();
            Assert.That(view.PreferredFocusTarget, Is.SameAs(view.ContinueButton));

            view.ContinueButton.onClick.Invoke();
            Assert.That(inquiry.Location, Is.EqualTo(M02EvidenceLocation.HarborArchive));
            Assert.That(view.GetEvidenceButton(1).gameObject.activeSelf, Is.False);
            Assert.That(view.PreferredFocusTarget, Is.SameAs(view.GetEvidenceButton(0)));
            view.GetEvidenceButton(0).onClick.Invoke();
            Assert.That(view.PreferredFocusTarget, Is.SameAs(view.ContinueButton));
            view.ContinueButton.onClick.Invoke();
            Assert.That(inquiry.Location, Is.EqualTo(M02EvidenceLocation.Finished));
            Assert.That(view.PreferredFocusTarget, Is.Null);
            Assert.That(inquiry.Found, Is.EqualTo(
                M02BridgeEvidence.CleanCableCut |
                M02BridgeEvidence.FerrymanLog |
                M02BridgeEvidence.ThirdBellSignal |
                M02BridgeEvidence.WarReportDraft));
        }

        [Test]
        public void SkippingWarehouseKeepsArchiveFocusOnUnseenDraft()
        {
            var inquiry = new M02BridgeInquiry();
            inquiry.Begin();
            view = new M02BridgeInquiryView(root.transform, inquiry, _ => { });
            view.ContinueButton.onClick.Invoke();
            Assert.That(view.PreferredFocusTarget, Is.SameAs(view.GetEvidenceButton(0)));
            Assert.That(inquiry.Found, Is.EqualTo(M02BridgeEvidence.None));
            view.ContinueButton.onClick.Invoke();
            Assert.That(view.PreferredFocusTarget, Is.Null);
            Assert.That(inquiry.Found, Is.EqualTo(M02BridgeEvidence.None));
        }

        [UnityTest]
        public IEnumerator GamepadEnteringArchiveFocusesUnreadDraftBeforeFinish()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);
            yield return null;

            var input = new InputTestFixture();
            input.Setup();
            var owner = new GameObject("M02 live archive focus");
            M02BridgeInquiryView runtimeView = null;
            try
            {
                Gamepad pad = InputSystem.AddDevice<Gamepad>();
                EventSystem events = SoundwellInputAdapter.EnsureEventSystem(owner.transform);
                SoundwellInputAdapter.SetUiContext();
                var inquiry = new M02BridgeInquiry();
                inquiry.Begin();
                int completions = 0;
                runtimeView = new M02BridgeInquiryView(owner.transform, inquiry,
                    _ => completions++);

                events.SetSelectedGameObject(runtimeView.ContinueButton.gameObject);
                input.Press(pad.buttonSouth);
                yield return null;
                input.Release(pad.buttonSouth);
                yield return null;

                Assert.That(inquiry.Location, Is.EqualTo(M02EvidenceLocation.HarborArchive));
                Assert.That(inquiry.Found, Is.EqualTo(M02BridgeEvidence.None));
                Assert.That(events.currentSelectedGameObject,
                    Is.SameAs(runtimeView.GetEvidenceButton(0).gameObject),
                    "Entering the archive must not leave Submit on Finish.");
                Assert.That(completions, Is.Zero);

                input.Press(pad.buttonSouth);
                yield return null;
                input.Release(pad.buttonSouth);
                yield return null;
                Assert.That(inquiry.Location, Is.EqualTo(M02EvidenceLocation.HarborArchive));
                Assert.That(inquiry.Found, Is.EqualTo(M02BridgeEvidence.WarReportDraft));
                Assert.That(completions, Is.Zero);
            }
            finally
            {
                runtimeView?.Dispose();
                Object.Destroy(owner);
                input.TearDown();
            }
            yield return new ExitPlayMode();
        }

        [Test]
        public void PlayerMaySkipEveryClueAndSwitchLocaleWithoutChangingFacts()
        {
            var inquiry = new M02BridgeInquiry();
            inquiry.Begin();
            M02BridgeEvidence received = (M02BridgeEvidence)(-1);
            view = new M02BridgeInquiryView(root.transform, inquiry, clues => received = clues);
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            Assert.That(view.Narrative.text, Does.Contain("守桥老人"));
            view.LayoutForViewportAspect(9f / 16f);
            view.LayoutForViewportAspect(32f / 9f);
            view.ContinueButton.onClick.Invoke();
            view.ContinueButton.onClick.Invoke();
            Assert.That(received, Is.EqualTo(M02BridgeEvidence.None));
        }


        [Test]
        public void NewCluesNotifyHostImmediatelyAndRevisitingDoesNotDuplicateThem()
        {
            var inquiry = new M02BridgeInquiry();
            inquiry.Begin();
            M02BridgeEvidence saved = M02BridgeEvidence.None;
            int observations = 0;
            int completed = 0;
            view = new M02BridgeInquiryView(root.transform, inquiry,
                clues =>
                {
                    completed++;
                    Assert.That(clues, Is.EqualTo(saved));
                },
                clue =>
                {
                    observations++;
                    saved = M02BridgeEvidenceFacts.MergeObserved(saved, clue);
                });

            view.GetEvidenceButton(0).onClick.Invoke();
            view.GetEvidenceButton(0).onClick.Invoke();
            Assert.That(observations, Is.EqualTo(1));
            Assert.That(saved, Is.EqualTo(M02BridgeEvidence.CleanCableCut));
            view.ContinueButton.onClick.Invoke();
            view.GetEvidenceButton(0).onClick.Invoke();
            Assert.That(saved, Is.EqualTo(M02BridgeEvidence.CleanCableCut |
                M02BridgeEvidence.WarReportDraft));
            Assert.That(observations, Is.EqualTo(2));
            view.ContinueButton.onClick.Invoke();
            Assert.That(completed, Is.EqualTo(1));
        }

        [Test]
        public void DestroyingTheViewDoesNotEraseAnAlreadyObservedClue()
        {
            var inquiry = new M02BridgeInquiry();
            inquiry.Begin();
            M02BridgeEvidence saved = M02BridgeEvidence.None;
            int completed = 0;
            view = new M02BridgeInquiryView(root.transform, inquiry,
                _ => completed++, clue => saved = M02BridgeEvidenceFacts.MergeObserved(saved, clue));
            view.GetEvidenceButton(1).onClick.Invoke();
            view.Dispose();
            view.GetEvidenceButton(2).onClick.Invoke();
            Assert.That(saved, Is.EqualTo(M02BridgeEvidence.FerrymanLog));
            Assert.That(completed, Is.Zero);
        }

        [Test]
        public void SceneTeardownUnsubscribesLocaleAndDisposesSafely()
        {
            var inquiry = new M02BridgeInquiry();
            inquiry.Begin();
            view = new M02BridgeInquiryView(root.transform, inquiry, _ => { });
            view.Dispose();
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            Assert.That(inquiry.Found, Is.EqualTo(M02BridgeEvidence.None));
        }
    }
}
