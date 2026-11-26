using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ST01TheaterChoiceViewLifecycleTests
    {
        [Test]
        public void PartialChildTeardownDisposesViewAndBlocksClickLayoutAndLocaleRefresh()
        {
            var root = new GameObject("ST01 view partial teardown");
            string previousLanguage = SoundwellUiLocalization.Language;
            ST01TheaterChoiceView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                int calls = 0;
                view = new ST01TheaterChoiceView(
                    root.transform, null, null, _ => true, _ => calls++);

                var boycottBtn = view.BoycottButton;
                UnityEngine.Object.DestroyImmediate(view.Heading.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => boycottBtn.onClick.Invoke());
                Assert.That(calls, Is.EqualTo(0));
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
            var root = new GameObject("ST01 view invalid aspects");
            ST01TheaterChoiceView view = null;
            try
            {
                view = new ST01TheaterChoiceView(
                    root.transform, null, null, _ => true, _ => { });
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
        public void ReentrantChoiceSubmitClickIsIgnoredDuringSubmissionCallback()
        {
            var root = new GameObject("ST01 view reentrant submit");
            ST01TheaterChoiceView view = null;
            try
            {
                int invocations = 0;
                view = new ST01TheaterChoiceView(
                    root.transform, null, null, _ => true, _ =>
                    {
                        invocations++;
                        Assert.That(view.RewriteButton.interactable, Is.False);
                        Assert.That(view.ContractButton.interactable, Is.False);
                        view.RewriteButton.onClick.Invoke();
                        view.ContractButton.onClick.Invoke();
                    });

                view.RewriteButton.onClick.Invoke();
                Assert.That(invocations, Is.EqualTo(1));
                Assert.That(view.RewriteButton.interactable, Is.True);
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
            var root = new GameObject("ST01 view dispose in callback");
            ST01TheaterChoiceView view = null;
            try
            {
                view = new ST01TheaterChoiceView(
                    root.transform, null, null, _ => true, _ =>
                    {
                        view.Dispose();
                        throw new InvalidOperationException("Simulated ST01 teardown during submit");
                    });

                Assert.Throws<InvalidOperationException>(() => view.BoycottButton.onClick.Invoke());
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
