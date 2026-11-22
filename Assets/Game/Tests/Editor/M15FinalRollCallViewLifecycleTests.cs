using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M15FinalRollCallViewLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void PartialChildTeardownDisposesViewAndBlocksClickLayoutAndLocaleRefresh()
        {
            var root = new GameObject("M15 view partial teardown");
            string previousLanguage = SoundwellUiLocalization.Language;
            M15FinalRollCallView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                var session = new M15FinalRollCallSession(false, 3);
                int calls = 0;
                view = new M15FinalRollCallView(root.transform, session, (_, __, ___) => calls++);

                var readerButton = view.ProtagonistReaderButton;
                UnityEngine.Object.DestroyImmediate(view.Heading.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => readerButton.onClick.Invoke());
                Assert.That(calls, Is.EqualTo(0));
                Assert.That(view.Step, Is.EqualTo(M15RollCallViewStep.SelectReader));
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
            var root = new GameObject("M15 view invalid aspects");
            M15FinalRollCallView view = null;
            try
            {
                var session = new M15FinalRollCallSession(false, 2);
                view = new M15FinalRollCallView(root.transform, session, (_, __, ___) => { });
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
        public void ReentrantSteleSubmitClickIsIgnoredDuringSubmissionCallback()
        {
            var root = new GameObject("M15 view reentrant submit");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M15FinalRollCallView view = null;
            try
            {
                var session = new M15FinalRollCallSession(false, 2);
                int invocations = 0;
                view = new M15FinalRollCallView(root.transform, session, (reader, identity, stele) =>
                {
                    invocations++;
                    Assert.That(view.KeepOriginalSteleButton.interactable, Is.False);
                    view.KeepOriginalSteleButton.onClick.Invoke();
                    Assert.That(session.TryCompleteRollCall(reader, identity, stele, read, write), Is.True);
                });

                view.ProtagonistReaderButton.onClick.Invoke();
                view.PublicTrueNameButton.onClick.Invoke();
                view.KeepOriginalSteleButton.onClick.Invoke();

                Assert.That(invocations, Is.EqualTo(1));
                Assert.That(view.Step, Is.EqualTo(M15RollCallViewStep.Completed));
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
            var root = new GameObject("M15 view dispose in callback");
            M15FinalRollCallView view = null;
            try
            {
                var session = new M15FinalRollCallSession(false, 1);
                view = new M15FinalRollCallView(root.transform, session, (_, __, ___) =>
                {
                    view.Dispose();
                    throw new InvalidOperationException("Simulated M15 teardown during submit");
                });

                view.ProtagonistReaderButton.onClick.Invoke();
                view.PublicTrueNameButton.onClick.Invoke();

                Assert.Throws<InvalidOperationException>(() => view.KeepOriginalSteleButton.onClick.Invoke());
                Assert.That(view.IsDisposed, Is.True);
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
