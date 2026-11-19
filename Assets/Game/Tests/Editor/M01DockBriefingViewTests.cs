using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M01DockBriefingViewTests
    {
        private GameObject root;
        private M01DockBriefingView view;
        private string previousLanguage;

        [SetUp]
        public void SetUp()
        {
            previousLanguage = SoundwellUiLocalization.Language;
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            root = new GameObject("M01 dock view test root");
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
        public void ActualButtonsRequireOpeningReplyAndCakeBeforeRouteComparison()
        {
            int calls = 0;
            view = new M01DockBriefingView(root.transform, () => calls++);
            Assert.That(view.Stage, Is.EqualTo(M01DockStage.AwaitingOpeningReply));
            Assert.That(view.AdvanceButton.gameObject.activeSelf, Is.False);
            view.AdvanceButton.onClick.Invoke();
            Assert.That(view.Stage, Is.EqualTo(M01DockStage.AwaitingOpeningReply));
            view.GetChoiceButton(2).onClick.Invoke();
            Assert.That(view.Stage, Is.EqualTo(M01DockStage.OfferingCake));
            Assert.That(view.Dialogue.text, Does.Contain("osmanthus cake"));
            view.AdvanceButton.onClick.Invoke();
            Assert.That(view.Stage, Is.EqualTo(M01DockStage.ReviewingRoutes));
            Assert.That(view.InspectedLead, Is.Null);
            view.GetChoiceButton(0).onClick.Invoke();
            Assert.That(view.Dialogue.text, Does.Contain("living registration"));
            view.GetChoiceButton(1).onClick.Invoke();
            Assert.That(view.Dialogue.text, Does.Contain("Third Brigade flag"));
            view.GetChoiceButton(2).onClick.Invoke();
            Assert.That(view.Dialogue.text, Does.Contain("small ship"));
            view.AdvanceButton.onClick.Invoke();
            view.AdvanceButton.onClick.Invoke();
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(view.Stage, Is.EqualTo(M01DockStage.Complete));
        }

        [Test]
        public void RouteChartFocusStartsAtFirstLeadWithoutBookingPassage()
        {
            int completed = 0;
            view = new M01DockBriefingView(root.transform, () => completed++);
            Assert.That(view.PreferredFocusTarget, Is.SameAs(view.GetChoiceButton(0)));
            view.GetChoiceButton(1).onClick.Invoke();
            Assert.That(view.Stage, Is.EqualTo(M01DockStage.OfferingCake));
            Assert.That(view.PreferredFocusTarget, Is.SameAs(view.AdvanceButton));

            view.AdvanceButton.onClick.Invoke();
            Assert.That(view.Stage, Is.EqualTo(M01DockStage.ReviewingRoutes));
            Assert.That(view.PreferredFocusTarget, Is.SameAs(view.GetChoiceButton(0)),
                "The first route should be available to keyboard/gamepad after accepting cake.");
            Assert.That(view.AdvanceButton.gameObject.activeSelf, Is.True,
                "Skipping optional route inspection remains a deliberate choice.");
            Assert.That(view.InspectedLead, Is.Null);
            Assert.That(completed, Is.Zero);

            view.AdvanceButton.onClick.Invoke();
            Assert.That(view.Stage, Is.EqualTo(M01DockStage.Complete));
            Assert.That(view.PreferredFocusTarget, Is.Null);
            Assert.That(completed, Is.EqualTo(1));
        }

        [Test]
        public void ChineseCopyAndScreenAspectChangesDoNotChoosePassage()
        {
            view = new M01DockBriefingView(root.transform, () => { });
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            Assert.That(view.Dialogue.text, Does.Contain("祁雀"));
            view.GetChoiceButton(0).onClick.Invoke();
            Assert.That(view.Dialogue.text, Does.Contain("桂花糕"));
            view.AdvanceButton.onClick.Invoke();
            view.GetChoiceButton(2).onClick.Invoke();
            Assert.That(view.Dialogue.text, Does.Contain("十几个人"));
            view.LayoutForViewportAspect(9f / 16f);
            view.LayoutForViewportAspect(32f / 9f);
            Assert.That(view.InspectedLead, Is.EqualTo(M01VoyageLead.QiQueMerchantRoute));
            Assert.That(view.Stage, Is.EqualTo(M01DockStage.ReviewingRoutes));
        }

        [Test]
        public void FailedHostCallbackKeepsBriefingCompletionRetryableAndBlocksReentrancy()
        {
            int attempts = 0;
            view = new M01DockBriefingView(root.transform, () =>
            {
                attempts++;
                if (attempts == 1)
                {
                    view.AdvanceButton.onClick.Invoke();
                    throw new InvalidOperationException("Simulated host handoff failure");
                }
            });
            view.GetChoiceButton(2).onClick.Invoke();
            view.AdvanceButton.onClick.Invoke();
            view.GetChoiceButton(1).onClick.Invoke();
            Assert.That(view.Stage, Is.EqualTo(M01DockStage.ReviewingRoutes));
            Assert.That(view.InspectedLead, Is.EqualTo(M01VoyageLead.LuJunOldGuard));

            Assert.Throws<InvalidOperationException>(() => view.AdvanceButton.onClick.Invoke());
            Assert.That(attempts, Is.EqualTo(1));
            Assert.That(view.Stage, Is.EqualTo(M01DockStage.ReviewingRoutes));
            Assert.That(view.InspectedLead, Is.EqualTo(M01VoyageLead.LuJunOldGuard));
            Assert.That(view.AdvanceButton.gameObject.activeSelf, Is.True);

            view.AdvanceButton.onClick.Invoke();
            Assert.That(attempts, Is.EqualTo(2));
            Assert.That(view.Stage, Is.EqualTo(M01DockStage.Complete));
            Assert.That(view.AdvanceButton.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void DestroyedDockCanvasCannotRetainFocusOrCompletePassageAfterSceneUnload()
        {
            foreach (bool layoutFirst in new[] { true, false })
            {
                var scene = new GameObject("Destroyed M01 dock briefing");
                try
                {
                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                    int completed = 0;
                    view = new M01DockBriefingView(scene.transform, () => completed++);
                    Assert.That(view.Stage, Is.EqualTo(M01DockStage.AwaitingOpeningReply));

                    UnityEngine.Object.DestroyImmediate(scene);
                    scene = null;

                    // A host focus query can arrive before its resize callback.
                    Assert.That(view.PreferredFocusTarget, Is.Null);
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

                    Assert.That(view.PreferredFocusTarget, Is.Null);
                    Assert.That(view.Stage, Is.EqualTo(M01DockStage.AwaitingOpeningReply));
                    Assert.That(view.InspectedLead, Is.Null,
                        "Unloading dock briefing must not inspect or book a route.");
                    Assert.That(completed, Is.Zero);
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
        public void DisposingViewDoesNotFinishStoryOrRetainListener()
        {
            int calls = 0;
            view = new M01DockBriefingView(root.transform, () => calls++);
            view.GetChoiceButton(1).onClick.Invoke();
            view.Dispose();
            view.AdvanceButton.onClick.Invoke();
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            Assert.That(calls, Is.Zero);
        }
    }
}
