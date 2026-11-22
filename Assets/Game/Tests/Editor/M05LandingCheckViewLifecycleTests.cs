using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M05LandingCheckViewLifecycleTests
    {
        [Test]
        public void DestroyedCanvasReportsDisposedImmediatelyAndBlocksConfirmAndLocaleRefresh()
        {
            var root = new GameObject("M05LandingCheckCanvasDestroyRoot");
            string previousLanguage = SoundwellUiLocalization.Language;
            M05LandingCheckView view = null;

            try
            {
                int confirmations = 0;
                view = new M05LandingCheckView(
                    root.transform,
                    new M05LandingReport(3, 8, true, true),
                    _ => confirmations++);

                Assert.That(view.IsDisposed, Is.False);
                var button = view.ConfirmButton;

                UnityEngine.Object.DestroyImmediate(view.Panel.parent.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => button.onClick.Invoke());
                Assert.That(confirmations, Is.Zero);

                string alternate = previousLanguage == SoundwellUiLocalization.Chinese
                    ? SoundwellUiLocalization.English
                    : SoundwellUiLocalization.Chinese;
                Assert.DoesNotThrow(() => SoundwellUiLocalization.SetLanguage(alternate));
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(16f / 9f));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NonFiniteAndNonPositiveAspectIsIgnoredWithoutCorruptingPanelScale()
        {
            var root = new GameObject("M05LandingCheckAspectRoot");
            M05LandingCheckView view = null;

            try
            {
                view = new M05LandingCheckView(
                    root.transform,
                    new M05LandingReport(3, 8, true, true),
                    _ => { });

                view.LayoutForViewportAspect(16f / 9f);
                Vector3 baselineScale = view.Panel.localScale;

                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(float.NaN));
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(float.PositiveInfinity));
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(float.NegativeInfinity));
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(0f));
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(-1f));
                Assert.That(view.Panel.localScale, Is.EqualTo(baselineScale));
                Assert.That(view.IsDisposed, Is.False);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
