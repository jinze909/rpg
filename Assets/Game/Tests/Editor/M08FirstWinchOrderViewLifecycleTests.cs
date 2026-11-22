using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M08FirstWinchOrderViewLifecycleTests
    {
        private static M08FirstWinchOrderSession CompletedSession()
        {
            var session = new M08FirstWinchOrderSession();
            Assert.That(session.TryAcquirePart(M08WinchPart.ScrapGear), Is.True);
            Assert.That(session.TryAcquirePart(M08WinchPart.DockCable), Is.True);
            Assert.That(session.TryAssemble(), Is.True);
            Assert.That(session.TryTightenCable(), Is.True);
            Assert.That(session.TryTestLift(), Is.True);
            Assert.That(session.IsCompleted, Is.True);
            return session;
        }

        [Test]
        public void DestroyingCoreChildControlDetachesLocaleAndBlocksClicks()
        {
            var root = new GameObject("M08 winch partial teardown");
            string savedLanguage = SoundwellUiLocalization.Language;
            M08FirstWinchOrderView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var session = new M08FirstWinchOrderSession();
                int deliveries = 0;

                view = new M08FirstWinchOrderView(root.transform, session, _ => deliveries++);
                Assert.That(view.IsDisposed, Is.False);

                // Destroy a core child control while leaving canvas and panel alive.
                UnityEngine.Object.DestroyImmediate(view.FinishButton.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => view.PrimaryButton.onClick.Invoke());
                Assert.That(session.HasScrapGear, Is.False);
                Assert.DoesNotThrow(() =>
                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese));
                Assert.DoesNotThrow(() => view.Refresh());
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(16f / 9f));
                Assert.DoesNotThrow(() => view.Dispose());
                Assert.That(deliveries, Is.Zero);
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(savedLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InvalidViewportAspectsAreIgnoredWithoutThrowing()
        {
            var root = new GameObject("M08 winch aspect guard");
            M08FirstWinchOrderView view = null;
            try
            {
                var session = new M08FirstWinchOrderSession();
                view = new M08FirstWinchOrderView(root.transform, session, _ => { });

                view.LayoutForViewportAspect(16f / 9f);
                Vector3 expectedScale = view.Panel.localScale;

                foreach (float badAspect in new[]
                {
                    0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity
                })
                {
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(badAspect));
                    Assert.That(view.Panel.localScale, Is.EqualTo(expectedScale));
                }

                Assert.That(view.IsDisposed, Is.False);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ReentrantFinishClickIsBlockedWhileHostCallbackIsInFlight()
        {
            var root = new GameObject("M08 winch reentrant finish");
            M08FirstWinchOrderView view = null;
            try
            {
                var session = CompletedSession();
                int deliveries = 0;

                view = new M08FirstWinchOrderView(root.transform, session, _ =>
                {
                    deliveries++;
                    Assert.That(view.FinishButton.interactable, Is.False);
                    view.FinishButton.onClick.Invoke();
                });

                view.FinishButton.onClick.Invoke();
                Assert.That(deliveries, Is.EqualTo(1));
                Assert.That(view.FinishButton.interactable, Is.False);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ThrowingOnCompletedAfterDisposingViewDoesNotResurrectView()
        {
            var root = new GameObject("M08 winch dispose-then-throw");
            string savedLanguage = SoundwellUiLocalization.Language;
            M08FirstWinchOrderView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var session = CompletedSession();

                view = new M08FirstWinchOrderView(root.transform, session, _ =>
                {
                    view.Dispose();
                    throw new InvalidOperationException("Host disposed view before throwing");
                });

                Assert.Throws<InvalidOperationException>(() =>
                    view.FinishButton.onClick.Invoke());
                Assert.That(view.IsDisposed, Is.True);
                Assert.DoesNotThrow(() => view.Refresh());
                Assert.DoesNotThrow(() =>
                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(savedLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
