using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RC04ReefResolutionViewLifecycleTests
    {
        private static RC04DingYueReefSession AfterListening()
        {
            var session = new RC04DingYueReefSession();
            for (int i = 0; i < 3; i++)
                Assert.That(session.SubmitEchoDiscrimination(
                    RC04EchoChoice.SlowHumanEcho), Is.True);
            return session;
        }

        [Test]
        public void PartialChildTeardownDisposesViewAndBlocksChoiceLayoutAndLocaleRefresh()
        {
            var root = new GameObject("RC04 resolution partial teardown");
            string previousLanguage = SoundwellUiLocalization.Language;
            RC04ReefResolutionView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                RC04DingYueReefSession session = AfterListening();
                int calls = 0;
                view = new RC04ReefResolutionView(
                    root.transform, session, _ => true, _ => { calls++; return true; });

                var bringBtn = view.BringDingYueButton;
                UnityEngine.Object.DestroyImmediate(view.Heading.gameObject);
                Assert.That(view.IsDisposed, Is.True);
                Assert.That(view.CanChoose(RC04Resolution.BringDingYueBack), Is.False);

                Assert.DoesNotThrow(() => bringBtn.onClick.Invoke());
                Assert.That(calls, Is.EqualTo(0));
                Assert.That(view.ResolutionDelivered, Is.False);
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
            var root = new GameObject("RC04 resolution invalid aspects");
            RC04ReefResolutionView view = null;
            try
            {
                RC04DingYueReefSession session = AfterListening();
                view = new RC04ReefResolutionView(
                    root.transform, session, _ => true, _ => true);
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
        public void ReentrantResolutionClickIsIgnoredDuringSubmissionCallback()
        {
            var root = new GameObject("RC04 resolution reentrant submit");
            RC04ReefResolutionView view = null;
            try
            {
                RC04DingYueReefSession session = AfterListening();
                int invocations = 0;
                view = new RC04ReefResolutionView(
                    root.transform, session, _ => true, _ =>
                    {
                        invocations++;
                        Assert.That(view.BringDingYueButton.interactable, Is.False);
                        Assert.That(view.BeaconButton.interactable, Is.False);
                        view.BringDingYueButton.onClick.Invoke();
                        view.BeaconButton.onClick.Invoke();
                        return true;
                    });

                view.BringDingYueButton.onClick.Invoke();
                Assert.That(invocations, Is.EqualTo(1));
                Assert.That(view.ResolutionDelivered, Is.True);
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
            var root = new GameObject("RC04 resolution dispose in callback");
            RC04ReefResolutionView view = null;
            try
            {
                RC04DingYueReefSession session = AfterListening();
                view = new RC04ReefResolutionView(
                    root.transform, session, _ => true, _ =>
                    {
                        view.Dispose();
                        throw new InvalidOperationException("Simulated RC04 resolution teardown during submit");
                    });

                Assert.Throws<InvalidOperationException>(() => view.BeaconButton.onClick.Invoke());
                Assert.That(view.IsDisposed, Is.True);
                Assert.That(view.ResolutionDelivered, Is.False);
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
