using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M05SeranLettersViewLifecycleTests
    {
        [Test]
        public void DestroyedCanvasReportsDisposedImmediatelyAndBlocksChoiceAndCompletion()
        {
            var root = new GameObject("M05SeranCanvasDestroyRoot");
            string previousLanguage = SoundwellUiLocalization.Language;
            M05SeranLettersView view = null;

            try
            {
                int completions = 0;
                view = new M05SeranLettersView(root.transform, _ => completions++);
                Assert.That(view.IsDisposed, Is.False);
                Assert.That(root.transform.childCount, Is.EqualTo(1));

                var take = view.TakeButton;
                var readPacket = view.ReadPacketButton;
                var cont = view.ContinueButton;

                UnityEngine.Object.DestroyImmediate(root.transform.GetChild(0).gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => take.onClick.Invoke());
                Assert.DoesNotThrow(() => readPacket.onClick.Invoke());
                Assert.DoesNotThrow(() => cont.onClick.Invoke());
                Assert.That(view.SelectedDecision, Is.Null);
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
            var root = new GameObject("M05SeranAspectRoot");
            M05SeranLettersView view = null;

            try
            {
                view = new M05SeranLettersView(root.transform, _ => { });
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
