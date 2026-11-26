using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M04DockPreflightViewLifecycleTests
    {
        [Test]
        public void DestroyedCanvasReportsDisposedImmediatelyAndBlocksAdvanceAndLocaleRefresh()
        {
            var root = new GameObject("M04DockPreflightCanvasDestroyRoot");
            string previousLanguage = SoundwellUiLocalization.Language;
            M04DockPreflightView view = null;

            try
            {
                int completions = 0;
                view = new M04DockPreflightView(
                    root.transform,
                    M03VoyageChoice.QiQueMerchant,
                    () => completions++);

                Assert.That(view.IsDisposed, Is.False);
                var button = view.AdvanceButton;

                UnityEngine.Object.DestroyImmediate(view.Panel.parent.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => button.onClick.Invoke());
                Assert.That(view.Stage, Is.EqualTo(M04DockPreflightStage.DawnSupply));
                Assert.That(completions, Is.Zero);

                string alternate = previousLanguage == SoundwellUiLocalization.Chinese
                    ? SoundwellUiLocalization.English
                    : SoundwellUiLocalization.Chinese;
                Assert.DoesNotThrow(() => SoundwellUiLocalization.SetLanguage(alternate));
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
            var root = new GameObject("M04DockPreflightAspectRoot");
            M04DockPreflightView view = null;

            try
            {
                view = new M04DockPreflightView(
                    root.transform,
                    M03VoyageChoice.LuJunEscort,
                    () => { });

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
