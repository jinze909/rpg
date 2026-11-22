using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M04DepartureReadinessViewLifecycleTests
    {
        private GameObject root;
        private M04DepartureReadinessView view;
        private string previousLanguage;

        [SetUp]
        public void SetUp()
        {
            previousLanguage = SoundwellUiLocalization.Language;
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            root = new GameObject("M04 departure readiness lifecycle root");
        }

        [TearDown]
        public void TearDown()
        {
            view?.Dispose();
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
            SoundwellUiLocalization.SetLanguage(previousLanguage);
        }

        private static M04DepartureManifest ReadyManifest()
        {
            return new M04DepartureManifest(M03VoyageChoice.HanQiaoOfficial,
                "Inspected hull", "Confirmed pilot", 3, "Inspected tender",
                true, 18, 13, "Named landing receiver", true, false);
        }

        [Test]
        public void IsDisposedTracksExplicitDisposeAndExternalCanvasDestruction()
        {
            view = new M04DepartureReadinessView(root.transform, ReadyManifest(),
                _ => { }, () => { });
            Assert.That(view.IsDisposed, Is.False);

            UnityEngine.Object.DestroyImmediate(root);
            root = null;
            Assert.That(view.IsDisposed, Is.True);

            Assert.DoesNotThrow(() => view.Dispose());
            Assert.That(view.IsDisposed, Is.True);
        }

        [Test]
        public void FailedConfirmCallbackThatDisposesViewDoesNotReopenOrThrowSecondaryError()
        {
            M04DepartureManifest manifest = ReadyManifest();
            view = new M04DepartureReadinessView(root.transform, manifest,
                _ =>
                {
                    view.Dispose();
                    throw new InvalidOperationException("Host tore down readiness view during confirm");
                }, () => { });

            Assert.Throws<InvalidOperationException>(() => view.ConfirmButton.onClick.Invoke());
            Assert.That(view.IsDisposed, Is.True);
            Assert.That(manifest.DepartureConfirmed, Is.False);
        }

        [Test]
        public void FailedBackCallbackThatDisposesViewDoesNotReopenOrThrowSecondaryError()
        {
            view = new M04DepartureReadinessView(root.transform, ReadyManifest(),
                _ => { }, () =>
                {
                    view.Dispose();
                    throw new InvalidOperationException("Host tore down readiness view during back");
                });

            Assert.Throws<InvalidOperationException>(() => view.BackButton.onClick.Invoke());
            Assert.That(view.IsDisposed, Is.True);
        }

        [Test]
        public void LayoutForViewportAspectIgnoresNonPositiveAndNonFiniteAspects()
        {
            view = new M04DepartureReadinessView(root.transform, ReadyManifest(),
                _ => { }, () => { });

            view.LayoutForViewportAspect(16f / 9f);
            Vector3 expectedScale = view.ConfirmButton.transform.parent.localScale;

            view.LayoutForViewportAspect(0f);
            view.LayoutForViewportAspect(-1f);
            view.LayoutForViewportAspect(float.NaN);
            view.LayoutForViewportAspect(float.PositiveInfinity);
            view.LayoutForViewportAspect(float.NegativeInfinity);

            Assert.That(view.ConfirmButton.transform.parent.localScale, Is.EqualTo(expectedScale));
            Assert.That(view.IsDisposed, Is.False);
        }
    }
}
