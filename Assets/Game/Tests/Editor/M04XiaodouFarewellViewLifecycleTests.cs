using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M04XiaodouFarewellViewLifecycleTests
    {
        [Test]
        public void DestroyedCanvasReportsDisposedImmediatelyAndBlocksEncounterMutation()
        {
            var root = new GameObject("M04XiaodouCanvasDestroyRoot");
            string previousLanguage = SoundwellUiLocalization.Language;
            M04XiaodouFarewellView view = null;

            try
            {
                int completions = 0;
                view = new M04XiaodouFarewellView(root.transform, true, _ => completions++);
                Assert.That(view.IsDisposed, Is.False);
                Assert.That(root.transform.childCount, Is.EqualTo(1));

                var advance = view.AdvanceButton;
                var choice0 = view.GetChoiceButton(0);

                UnityEngine.Object.DestroyImmediate(root.transform.GetChild(0).gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => advance.onClick.Invoke());
                Assert.DoesNotThrow(() => choice0.onClick.Invoke());
                Assert.That(view.Encounter.Stage, Is.EqualTo(M04FarewellStage.Greeting));
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
        public void NonFiniteAndNonPositiveAspectIsIgnoredWithoutThrowingOrDisposing()
        {
            var root = new GameObject("M04XiaodouAspectRoot");
            M04XiaodouFarewellView view = null;

            try
            {
                view = new M04XiaodouFarewellView(root.transform, false, _ => { });
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(float.NaN));
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(float.PositiveInfinity));
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(float.NegativeInfinity));
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(0f));
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(-1f));
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
