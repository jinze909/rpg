using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M02QiQueNightViewLifecycleTests
    {
        private GameObject root;
        private M02QiQueNightView view;
        private string previousLanguage;

        [SetUp]
        public void SetUp()
        {
            previousLanguage = SoundwellUiLocalization.Language;
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            root = new GameObject("M02 Qi Que night lifecycle root");
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
        public void IsDisposedTracksExplicitDisposeAndExternalCanvasDestruction()
        {
            view = new M02QiQueNightView(root.transform, true, () => { });
            Assert.That(view.IsDisposed, Is.False);

            UnityEngine.Object.DestroyImmediate(root);
            root = null;
            Assert.That(view.IsDisposed, Is.True);

            Assert.DoesNotThrow(() => view.Dispose());
            Assert.That(view.IsDisposed, Is.True);
        }

        [Test]
        public void DestroyedCanvasBlocksAdvanceAndLeaveWithoutMutatingNightOrThrowing()
        {
            int finishedCalls = 0;
            view = new M02QiQueNightView(root.transform, true, () => finishedCalls++);
            view.ProgressButton.onClick.Invoke();
            view.ProgressButton.onClick.Invoke();
            view.ProgressButton.onClick.Invoke();
            Assert.That(view.Night.Stage, Is.EqualTo(M02QiQueNightStage.ReviewingChart));

            UnityEngine.Object.DestroyImmediate(root);
            root = null;

            Assert.DoesNotThrow(() => view.ProgressButton.onClick.Invoke());
            Assert.That(view.IsDisposed, Is.True);
            Assert.That(view.Night.Stage, Is.EqualTo(M02QiQueNightStage.ReviewingChart));
            Assert.That(finishedCalls, Is.Zero);
        }

        [Test]
        public void FailedHostCallbackThatDisposesViewDoesNotReopenOrThrowSecondaryError()
        {
            view = new M02QiQueNightView(root.transform, false, () =>
            {
                view.Dispose();
                throw new InvalidOperationException("Host tore down view and failed");
            });
            view.ProgressButton.onClick.Invoke();
            view.ProgressButton.onClick.Invoke();
            Assert.That(view.Night.Stage, Is.EqualTo(M02QiQueNightStage.ReviewingChart));

            Assert.Throws<InvalidOperationException>(() => view.ProgressButton.onClick.Invoke());
            Assert.That(view.IsDisposed, Is.True);
            Assert.That(view.Night.Stage, Is.EqualTo(M02QiQueNightStage.ReviewingChart));
        }

        [Test]
        public void LayoutForViewportAspectIgnoresNonPositiveAndNonFiniteAspects()
        {
            view = new M02QiQueNightView(root.transform, true, () => { });

            view.LayoutForViewportAspect(16f / 9f);
            Vector3 expectedScale = view.ProgressButton.transform.parent.localScale;

            view.LayoutForViewportAspect(0f);
            view.LayoutForViewportAspect(-1f);
            view.LayoutForViewportAspect(float.NaN);
            view.LayoutForViewportAspect(float.PositiveInfinity);
            view.LayoutForViewportAspect(float.NegativeInfinity);

            Assert.That(view.ProgressButton.transform.parent.localScale, Is.EqualTo(expectedScale));
            Assert.That(view.IsDisposed, Is.False);
        }
    }
}
