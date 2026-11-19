using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M05HarborWalkViewTests
    {
        [Test]
        public void RepairRumorAndMarketAccusationAdvanceInScriptOrderOnlyOnce()
        {
            var root = new GameObject("M05 harbor script test");
            M05HarborWalkView view = null;
            string language = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                int completed = 0;
                view = new M05HarborWalkView(root.transform, () => completed++);
                Assert.That(view.Beat, Is.EqualTo(M05HarborBeat.RepairStall));
                Assert.That(view.Dialogue.text, Does.Contain("Tax Collector"));
                Assert.That(view.Dialogue.text, Does.Contain("Shen Li"));
                view.AdvanceButton.onClick.Invoke();
                Assert.That(view.Beat, Is.EqualTo(M05HarborBeat.FisherRumor));
                Assert.That(view.Dialogue.text, Does.Contain("Three boats"));
                Assert.That(view.Dialogue.text, Does.Contain("rumor"));
                view.AdvanceButton.onClick.Invoke();
                Assert.That(view.Beat, Is.EqualTo(M05HarborBeat.MarketAccusation));
                Assert.That(view.Dialogue.text, Does.Contain("half a bowl"));
                Assert.That(completed, Is.Zero);
                view.AdvanceButton.onClick.Invoke();
                view.AdvanceButton.onClick.Invoke();
                Assert.That(view.Beat, Is.EqualTo(M05HarborBeat.Finished));
                Assert.That(completed, Is.EqualTo(1));
                Assert.That(view.AdvanceButton.gameObject.activeSelf, Is.False);
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(language);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LocaleSwitchAndAspectChangesPreserveTheWitnessedBeat()
        {
            var root = new GameObject("M05 language and viewport");
            M05HarborWalkView view = null;
            string language = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                view = new M05HarborWalkView(root.transform, () => { });
                view.AdvanceButton.onClick.Invoke();
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.Beat, Is.EqualTo(M05HarborBeat.FisherRumor));
                Assert.That(view.Dialogue.text, Does.Contain("三条船撞礁"));
                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(aspect));
                Assert.That(view.Beat, Is.EqualTo(M05HarborBeat.FisherRumor));
                view.AdvanceButton.onClick.Invoke();
                Assert.That(view.Dialogue.text, Does.Contain("我爹那个月只吃半碗"));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(language);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DialoguePanelIsBottomAnchoredInsteadOfAFullScreenBackdrop()
        {
            var root = new GameObject("M05 lower safe region");
            M05HarborWalkView view = null;
            try
            {
                view = new M05HarborWalkView(root.transform, () => { });
                Assert.That(view.Panel.anchorMin.y, Is.EqualTo(0f));
                Assert.That(view.Panel.anchorMax.y, Is.EqualTo(0f));
                Assert.That(view.Panel.sizeDelta.y, Is.LessThan(220f));
                Assert.That(view.Panel.sizeDelta.x,
                    Is.LessThan(SoundwellUiTheme.ReferenceWidth));
                Assert.That(view.AdvanceButton.name, Is.EqualTo("M05 harbor advance"));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InterruptedFinalHostTransitionRetainsTheLastBeatForRetry()
        {
            var root = new GameObject("M05 transition retry");
            M05HarborWalkView view = null;
            try
            {
                int attempts = 0;
                view = new M05HarborWalkView(root.transform, () =>
                {
                    attempts++;
                    view.AdvanceButton.onClick.Invoke();
                    if (attempts == 1)
                        throw new InvalidOperationException("Host unavailable");
                });
                view.AdvanceButton.onClick.Invoke();
                view.AdvanceButton.onClick.Invoke();
                Assert.Throws<InvalidOperationException>(() =>
                    view.AdvanceButton.onClick.Invoke());
                Assert.That(view.Beat, Is.EqualTo(M05HarborBeat.MarketAccusation));
                Assert.That(view.AdvanceButton.interactable, Is.True);
                Assert.That(attempts, Is.EqualTo(1));
                view.AdvanceButton.onClick.Invoke();
                Assert.That(attempts, Is.EqualTo(2));
                Assert.That(view.Beat, Is.EqualTo(M05HarborBeat.Finished));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DisposalRejectsLateInputAndInvalidStagesNeverInventDialogue()
        {
            var root = new GameObject("M05 teardown");
            M05HarborWalkView view = null;
            try
            {
                Assert.Throws<ArgumentNullException>(() =>
                    new M05HarborWalkView(null, () => { }));
                Assert.Throws<ArgumentNullException>(() =>
                    new M05HarborWalkView(root.transform, null));
                int completed = 0;
                view = new M05HarborWalkView(root.transform, () => completed++);
                view.AdvanceButton.onClick.Invoke();
                view.AdvanceButton.onClick.Invoke();
                ButtonClickAfterDispose(view);
                Assert.That(completed, Is.Zero);
                Assert.DoesNotThrow(() => view.Refresh());
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(0.5f));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    M05HarborWalkCopy.Title((M05HarborBeat)100, true));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    M05HarborWalkCopy.Describe((M05HarborBeat)(-1), false));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ButtonClickAfterDispose(M05HarborWalkView view)
        {
            var button = view.AdvanceButton;
            view.Dispose();
            button.onClick.Invoke();
        }
    }
}
