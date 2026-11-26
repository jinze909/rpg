using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M04CastOffViewLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> saved)
        {
            return key => saved.TryGetValue(key, out int value) ? value : 0;
        }

        private static void ConfirmReady(Dictionary<string, int> saved)
        {
            saved[M03LuJunFacts.ConfessionKey] = (int)M03LuConfession.NotAsked + 1;
            saved[M03LuJunFacts.SecrecyKey] = (int)M03SecrecyAnswer.NotTriggered + 1;
            saved[M03LuJunFacts.VoyageKey] = (int)M03VoyageChoice.QiQueMerchant + 1;
            var manifest = new M04DepartureManifest(
                M03VoyageChoice.QiQueMerchant, "Verified hull", "A Yuan", 4,
                "Actual shallow tender", true, 15, 9, "Named landing receiver",
                false, false);
            Assert.That(manifest.ConfirmDeparture(), Is.True);
            Assert.That(M04DepartureFacts.TryRecord(manifest,
                (key, value) => saved[key] = value), Is.True);
        }

        [Test]
        public void DestroyedCanvasReportsDisposedImmediatelyAndBlocksCastOffAndLocaleRefresh()
        {
            var root = new GameObject("M04CastOffCanvasDestroyRoot");
            var saved = new Dictionary<string, int>();
            ConfirmReady(saved);
            string previousLanguage = SoundwellUiLocalization.Language;
            M04CastOffView view = null;

            try
            {
                int sailedCallbacks = 0;
                view = new M04CastOffView(
                    root.transform,
                    Reader(saved),
                    (key, value) => saved[key] = value,
                    () => sailedCallbacks++);

                Assert.That(view.IsDisposed, Is.False);
                var button = view.CastOffButton;

                UnityEngine.Object.DestroyImmediate(view.Panel.parent.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => button.onClick.Invoke());
                Assert.That(sailedCallbacks, Is.Zero);
                Assert.That(saved[M04DepartureFacts.SailedKey], Is.Zero);

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
            var root = new GameObject("M04CastOffAspectRoot");
            var saved = new Dictionary<string, int>();
            ConfirmReady(saved);
            M04CastOffView view = null;

            try
            {
                view = new M04CastOffView(
                    root.transform,
                    Reader(saved),
                    (key, value) => saved[key] = value,
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
