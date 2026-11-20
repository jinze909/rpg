using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class M01WestLaneViewTests
    {
        private GameObject root;
        private M01WestLaneView view;
        private string previousLanguage;

        [SetUp]
        public void SetUp()
        {
            previousLanguage = SoundwellUiLocalization.Language;
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            root = new GameObject("M01 West Lane view tests");
        }

        [TearDown]
        public void TearDown()
        {
            view?.Dispose();
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
            SoundwellUiLocalization.SetLanguage(previousLanguage);
        }

        [Test]
        public void StageIconTracksDoorIronBoxDecisionAndHidesAfterHandoff()
        {
            RpgPixelArtManifest manifest = RpgPixelArtManifest.LoadDefault();
            Assert.That(manifest, Is.Not.Null);

            view = NewView(M00RollCallOutcome.Silent, false, _ => { });
            AssertStageIcon(view.StageIcon, manifest.FogSignalLanternOn);

            view.ProgressButton.onClick.Invoke();
            Assert.That(view.Stage, Is.EqualTo(M01WestLaneStage.AtIronBox));
            AssertStageIcon(view.StageIcon, manifest.M07CargoCoverCrateClosed);

            view.ProgressButton.onClick.Invoke();
            Assert.That(view.Stage, Is.EqualTo(M01WestLaneStage.ChoosingPension));
            AssertStageIcon(view.StageIcon, manifest.M07CargoCoverCrateOpen);

            view.GetChoiceButton(2).onClick.Invoke();
            Assert.That(view.Stage, Is.EqualTo(M01WestLaneStage.Concluded));
            AssertStageIcon(view.StageIcon, manifest.M07InspectionPendingPass);

            view.ProgressButton.onClick.Invoke();
            Assert.That(view.StageIcon.sprite, Is.Null);
            Assert.That(view.StageIcon.enabled, Is.False);
            Assert.That(view.StageIcon.gameObject.activeSelf, Is.False);
        }

        private static void AssertStageIcon(Image icon, Sprite expected)
        {
            Assert.That(icon, Is.Not.Null);
            Assert.That(expected, Is.Not.Null);
            Assert.That(icon.sprite, Is.EqualTo(expected));
            Assert.That(icon.preserveAspect, Is.True);
            Assert.That(icon.raycastTarget, Is.False);
            Assert.That(icon.enabled, Is.True);
            Assert.That(icon.gameObject.activeSelf, Is.True);
            Assert.That(icon.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(icon.sprite.pixelsPerUnit, Is.EqualTo(RpgWorldMapScale.PixelsPerUnit));
        }

        [Test]
        public void DoorAndIronBoxMustBeOpenedBeforePensionChoices()
        {
            view = NewView(M00RollCallOutcome.Silent, false, _ => { });
            Assert.That(view.Stage, Is.EqualTo(M01WestLaneStage.AtDoor));
            Assert.That(view.GetChoiceButton(0).gameObject.activeSelf, Is.False);
            view.GetChoiceButton(0).onClick.Invoke();
            Assert.That(view.Decision.HasValue, Is.False);

            view.ProgressButton.onClick.Invoke();
            Assert.That(view.Stage, Is.EqualTo(M01WestLaneStage.AtIronBox));
            Assert.That(view.DialogueText.text, Does.Contain("really here"));
            view.GetChoiceButton(1).onClick.Invoke();
            Assert.That(view.Decision.HasValue, Is.False);

            view.ProgressButton.onClick.Invoke();
            Assert.That(view.Stage, Is.EqualTo(M01WestLaneStage.ChoosingPension));
            Assert.That(view.DialogueText.text, Does.Contain("twelve years"));
            Assert.That(view.GetChoiceButton(0).gameObject.activeSelf, Is.True);
            Assert.That(view.ProgressButton.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void PublicRollCallAnswerOutweighsPromptAndMakesConcealmentRiskVisible()
        {
            int delivered = 0;
            M01PensionChoice selected = M01PensionChoice.LeaveWithoutDecision;
            view = NewView(M00RollCallOutcome.AnsweredThird, true, x =>
            {
                delivered++;
                selected = x;
            });
            view.ProgressButton.onClick.Invoke();
            Assert.That(view.DialogueText.text, Does.Contain("answered the roll call"));
            Assert.That(view.DialogueText.text, Does.Not.Contain("prompted my lines"));
            view.ProgressButton.onClick.Invoke();
            view.GetChoiceButton(1).onClick.Invoke();

            Assert.That(view.Stage, Is.EqualTo(M01WestLaneStage.Concluded));
            Assert.That(view.Decision.Value, Is.EqualTo(M01PensionChoice.KeepIdentityUnregistered));
            Assert.That(view.DialogueText.text, Does.Contain("people already witnessed"));
            Assert.That(M01HomecomingResponse.PensionPathFor(view.Decision.Value),
                Is.EqualTo("隐瞒"));
            Assert.That(delivered, Is.Zero);
            view.ProgressButton.onClick.Invoke();
            view.ProgressButton.onClick.Invoke();
            Assert.That(delivered, Is.EqualTo(1));
            Assert.That(selected, Is.EqualTo(M01PensionChoice.KeepIdentityUnregistered));
        }

        [Test]
        public void MissingReturnReviewRemainsUnapprovedAndPreservesPromptRecognition()
        {
            M01PensionChoice? selected = null;
            view = NewView(M00RollCallOutcome.Silent, true, x => selected = x);
            view.ProgressButton.onClick.Invoke();
            Assert.That(view.DialogueText.text, Does.Contain("prompted my lines"));
            view.ProgressButton.onClick.Invoke();
            view.GetChoiceButton(2).onClick.Invoke();
            Assert.That(view.DialogueText.text, Does.Contain("nothing has been approved"));
            Assert.That(M01HomecomingResponse.PensionPathFor(view.Decision.Value),
                Is.EqualTo("未处理"));
            view.ProgressButton.onClick.Invoke();
            Assert.That(selected, Is.EqualTo(M01PensionChoice.RequestMissingReturnReview));
        }

        [Test]
        public void LeavingWithoutDecisionIsUnresolved()
        {
            view = NewView(M00RollCallOutcome.LeftCrowd, false, _ => { });
            view.ProgressButton.onClick.Invoke();
            view.ProgressButton.onClick.Invoke();
            view.GetChoiceButton(3).onClick.Invoke();
            Assert.That(view.Decision.Value, Is.EqualTo(M01PensionChoice.LeaveWithoutDecision));
            Assert.That(M01HomecomingResponse.PensionPathFor(view.Decision.Value),
                Is.EqualTo("未处理"));
        }

        [Test]
        public void LocaleAndViewportChangesNeverOverwriteAChosenPath()
        {
            view = NewView(M00RollCallOutcome.AnsweredFirst, true, _ => { });
            view.ProgressButton.onClick.Invoke();
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            Assert.That(view.DialogueText.text, Does.Contain("广场上应声"));
            view.ProgressButton.onClick.Invoke();
            Assert.That(view.GetChoiceButton(2).GetComponentInChildren<UnityEngine.UI.Text>(true).text,
                Does.Contain("尚未获准"));
            view.GetChoiceButton(0).onClick.Invoke();
            view.LayoutForViewportAspect(9f / 16f);
            view.LayoutForViewportAspect(5f);
            Assert.That(view.Stage, Is.EqualTo(M01WestLaneStage.Concluded));
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            Assert.That(view.Decision.Value, Is.EqualTo(M01PensionChoice.RegisterLivingIdentity));
            Assert.That(view.DialogueText.text, Does.Contain("real name"));
        }

        [Test]
        public void FailedPensionHandoffKeepsChosenPathRetryableWithoutDoubleSubmit()
        {
            int calls = 0;
            int saved = 0;
            view = NewView(M00RollCallOutcome.AnsweredThird, true, choice =>
            {
                calls++;
                Assert.That(choice, Is.EqualTo(M01PensionChoice.KeepIdentityUnregistered));
                Assert.That(view.Stage, Is.EqualTo(M01WestLaneStage.Concluded));
                Assert.That(view.ProgressButton.gameObject.activeSelf, Is.False);
                // Reentrant direct UGUI invocations must not submit again.
                view.ProgressButton.onClick.Invoke();
                if (calls == 1)
                    throw new InvalidOperationException("Simulated interrupted pension save");
                saved++;
            });
            view.ProgressButton.onClick.Invoke();
            view.ProgressButton.onClick.Invoke();
            view.GetChoiceButton(1).onClick.Invoke();
            Assert.That(view.DialogueText.text, Does.Contain("people already witnessed"));

            Assert.Throws<InvalidOperationException>(() =>
                view.ProgressButton.onClick.Invoke());
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(saved, Is.Zero);
            Assert.That(view.Decision, Is.EqualTo(M01PensionChoice.KeepIdentityUnregistered));
            Assert.That(view.Stage, Is.EqualTo(M01WestLaneStage.Concluded));
            Assert.That(view.ProgressButton.gameObject.activeSelf, Is.True);

            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            Assert.That(view.DialogueText.text, Does.Contain("目击者和流言"));
            view.ProgressButton.onClick.Invoke();
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(saved, Is.EqualTo(1));
            Assert.That(view.ProgressButton.gameObject.activeSelf, Is.False);
            view.ProgressButton.onClick.Invoke();
            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public void DestroyedWestLaneCanvasCannotAdvancePensionOrReceiveLayoutAndLocaleCallbacks()
        {
            // Scene teardown and localization may race the external
            // viewport callback in either order.
            foreach (bool layoutFirst in new[] { true, false })
            {
                var scene = new GameObject("Destroyed M01 West Lane");
                try
                {
                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                    int saved = 0;
                    view = new M01WestLaneView(scene.transform,
                        M00RollCallOutcome.AnsweredThird, true, _ => saved++);
                    Assert.That(view.Stage, Is.EqualTo(M01WestLaneStage.AtDoor));

                    UnityEngine.Object.DestroyImmediate(scene);
                    scene = null;
                    if (layoutFirst)
                        Assert.DoesNotThrow(() => view.LayoutForViewportAspect(9f / 16f));
                    Assert.DoesNotThrow(() => SoundwellUiLocalization.SetLanguage(
                        SoundwellUiLocalization.Chinese));
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(32f / 9f));
                    Assert.DoesNotThrow(() => view.Refresh());
                    Assert.DoesNotThrow(() => view.Dispose());
                    Assert.DoesNotThrow(() => view.Dispose());
                    Assert.DoesNotThrow(() => SoundwellUiLocalization.SetLanguage(
                        SoundwellUiLocalization.English));
                    Assert.That(view.Stage, Is.EqualTo(M01WestLaneStage.AtDoor));
                    Assert.That(view.Decision, Is.Null,
                        "Unloading West Lane must not choose a pension path.");
                    Assert.That(saved, Is.Zero);
                }
                finally
                {
                    view?.Dispose();
                    if (scene != null)
                        UnityEngine.Object.DestroyImmediate(scene);
                }
            }
        }

        [Test]
        public void SceneUnloadDetachesLocaleListenerAndRejectsInvalidIdentity()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                NewView((M00RollCallOutcome)88, false, _ => { }));
            Assert.Throws<ArgumentNullException>(() =>
                NewView(M00RollCallOutcome.Silent, false, null));
            view = NewView(M00RollCallOutcome.Silent, false, _ => { });
            UnityEngine.Object.DestroyImmediate(root);
            root = null;
            Assert.DoesNotThrow(() =>
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese));
            Assert.DoesNotThrow(() => view.Dispose());
        }

        private M01WestLaneView NewView(
            M00RollCallOutcome identity, bool prompted, Action<M01PensionChoice> done)
        {
            return new M01WestLaneView(root.transform, identity, prompted, done);
        }
    }
}
