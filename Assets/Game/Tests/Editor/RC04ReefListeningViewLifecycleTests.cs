using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RC04ReefListeningViewLifecycleTests
    {
        [Test]
        public void PartialChildTeardownDisposesViewAndBlocksEchoClickLayoutAndLocaleRefresh()
        {
            var root = new GameObject("RC04 listening partial teardown echo");
            string previousLanguage = SoundwellUiLocalization.Language;
            RC04ReefListeningView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                var session = new RC04DingYueReefSession();
                view = new RC04ReefListeningView(root.transform, session, _ => true);

                var slowBtn = view.SlowEchoButton;
                UnityEngine.Object.DestroyImmediate(view.Heading.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => slowBtn.onClick.Invoke());
                Assert.That(session.CompletedListeningRounds, Is.EqualTo(0));
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
        public void PartialChildTeardownOnApproachClickDisposesViewCleanly()
        {
            var root = new GameObject("RC04 listening partial teardown approach");
            RC04ReefListeningView view = null;
            try
            {
                var session = new RC04DingYueReefSession();
                for (int i = 0; i < 3; i++)
                    Assert.That(session.SubmitEchoDiscrimination(RC04EchoChoice.SlowHumanEcho), Is.True);

                int calls = 0;
                view = new RC04ReefListeningView(root.transform, session, _ => { calls++; return true; });
                var approachBtn = view.ApproachButton;
                UnityEngine.Object.DestroyImmediate(view.Body.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => approachBtn.onClick.Invoke());
                Assert.That(calls, Is.EqualTo(0));
                Assert.That(view.ApproachDelivered, Is.False);
                Assert.That(view.Panel == null, Is.True);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InvalidViewportAspectsAreIgnoredWithoutDistortingPanelScale()
        {
            var root = new GameObject("RC04 listening invalid aspects");
            RC04ReefListeningView view = null;
            try
            {
                var session = new RC04DingYueReefSession();
                view = new RC04ReefListeningView(root.transform, session, _ => true);
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
        public void ReentrantApproachClickAndDisposeInsideThrowingCallbackAreGuarded()
        {
            var root = new GameObject("RC04 listening reentrant and dispose");
            RC04ReefListeningView view1 = null;
            RC04ReefListeningView view2 = null;
            try
            {
                var session1 = new RC04DingYueReefSession();
                for (int i = 0; i < 3; i++)
                    Assert.That(session1.SubmitEchoDiscrimination(RC04EchoChoice.SlowHumanEcho), Is.True);

                int invocations = 0;
                view1 = new RC04ReefListeningView(root.transform, session1, _ =>
                {
                    invocations++;
                    Assert.That(view1.ApproachButton.interactable, Is.False);
                    view1.ApproachButton.onClick.Invoke();
                    return true;
                });
                view1.ApproachButton.onClick.Invoke();
                Assert.That(invocations, Is.EqualTo(1));
                Assert.That(view1.ApproachDelivered, Is.True);

                var session2 = new RC04DingYueReefSession();
                for (int i = 0; i < 3; i++)
                    Assert.That(session2.SubmitEchoDiscrimination(RC04EchoChoice.SlowHumanEcho), Is.True);

                view2 = new RC04ReefListeningView(root.transform, session2, _ =>
                {
                    view2.Dispose();
                    throw new InvalidOperationException("Simulated RC04 listening teardown during approach");
                });
                Assert.Throws<InvalidOperationException>(() => view2.ApproachButton.onClick.Invoke());
                Assert.That(view2.IsDisposed, Is.True);
                Assert.That(view2.ApproachDelivered, Is.False);
                Assert.DoesNotThrow(() => view2.Refresh());
                Assert.DoesNotThrow(() => view2.LayoutForViewportAspect(16f / 9f));
            }
            finally
            {
                view1?.Dispose();
                view2?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
