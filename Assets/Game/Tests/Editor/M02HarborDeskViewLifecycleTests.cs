using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M02HarborDeskViewLifecycleTests
    {
        private GameObject root;
        private M02HarborDeskView view;
        private string previousLanguage;

        [SetUp]
        public void SetUp()
        {
            previousLanguage = SoundwellUiLocalization.Language;
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            root = new GameObject("M02 harbor desk lifecycle root");
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
            view = new M02HarborDeskView(root.transform,
                M01PensionChoice.RegisterLivingIdentity, _ => { });
            Assert.That(view.IsDisposed, Is.False);

            UnityEngine.Object.DestroyImmediate(root);
            root = null;
            Assert.That(view.IsDisposed, Is.True);
            Assert.That(view.PreferredFocusTarget, Is.Null);

            Assert.DoesNotThrow(() => view.Dispose());
            Assert.That(view.IsDisposed, Is.True);
        }

        [Test]
        public void DestroyedCanvasBlocksChooseAndLeaveWithoutMutatingDeskOrThrowing()
        {
            int finishedCalls = 0;
            view = new M02HarborDeskView(root.transform,
                M01PensionChoice.RegisterLivingIdentity, _ => finishedCalls++);

            UnityEngine.Object.DestroyImmediate(root);
            root = null;

            Assert.DoesNotThrow(() => view.GetActionButton(0).onClick.Invoke());
            Assert.DoesNotThrow(() => view.GetActionButton(1).onClick.Invoke());
            Assert.DoesNotThrow(() => view.LeaveButton.onClick.Invoke());
            Assert.That(view.IsDisposed, Is.True);
            Assert.That(view.Desk.LivingRegistryStamped, Is.False);
            Assert.That(view.Desk.QiQueDebtDiscovered, Is.False);
            Assert.That(view.Desk.Stage, Is.EqualTo(M02HarborDeskStage.Discussing));
            Assert.That(finishedCalls, Is.Zero);
        }

        [Test]
        public void FailedHostCallbackThatDisposesViewDoesNotReopenOrThrowSecondaryError()
        {
            view = new M02HarborDeskView(root.transform,
                M01PensionChoice.RegisterLivingIdentity, _ =>
                {
                    view.Dispose();
                    throw new InvalidOperationException("Host tore down harbor desk view and failed");
                });

            Assert.Throws<InvalidOperationException>(() => view.LeaveButton.onClick.Invoke());
            Assert.That(view.IsDisposed, Is.True);
            Assert.That(view.Desk.Stage, Is.EqualTo(M02HarborDeskStage.Discussing));
        }

        [Test]
        public void LayoutForViewportAspectIgnoresNonPositiveAndNonFiniteAspects()
        {
            view = new M02HarborDeskView(root.transform,
                M01PensionChoice.RegisterLivingIdentity, _ => { });

            view.LayoutForViewportAspect(16f / 9f);
            Vector3 expectedScale = view.LeaveButton.transform.parent.localScale;

            view.LayoutForViewportAspect(0f);
            view.LayoutForViewportAspect(-1f);
            view.LayoutForViewportAspect(float.NaN);
            view.LayoutForViewportAspect(float.PositiveInfinity);
            view.LayoutForViewportAspect(float.NegativeInfinity);

            Assert.That(view.LeaveButton.transform.parent.localScale, Is.EqualTo(expectedScale));
            Assert.That(view.IsDisposed, Is.False);
        }
    }
}
