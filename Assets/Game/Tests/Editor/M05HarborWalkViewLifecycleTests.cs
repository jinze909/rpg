using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M05HarborWalkViewLifecycleTests
    {
        [Test]
        public void DestroyedCanvasReportsDisposedImmediatelyAndBlocksBeatAdvanceAndLocaleRefresh()
        {
            var root = new GameObject("M05HarborWalkCanvasDestroyRoot");
            string previousLanguage = SoundwellUiLocalization.Language;
            M05HarborWalkView view = null;

            try
            {
                int observed = 0;
                int finished = 0;
                view = new M05HarborWalkView(
                    root.transform,
                    () => finished++,
                    _ => observed++);

                Assert.That(view.IsDisposed, Is.False);
                var button = view.AdvanceButton;

                UnityEngine.Object.DestroyImmediate(view.Panel.parent.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => button.onClick.Invoke());
                Assert.That(view.Beat, Is.EqualTo(M05HarborBeat.RepairStall));
                Assert.That(observed, Is.Zero);
                Assert.That(finished, Is.Zero);

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
            var root = new GameObject("M05HarborWalkAspectRoot");
            M05HarborWalkView view = null;

            try
            {
                view = new M05HarborWalkView(root.transform, () => { });
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
