using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M09EastShoreNightCrossingViewLifecycleTests
    {
        private static M09EastShoreNightCrossingSession CompletedRescuedSession()
        {
            var session = new M09EastShoreNightCrossingSession(3);
            Assert.That(session.TryListenForOars(), Is.True);
            Assert.That(session.TryHoldSilent(), Is.True);
            Assert.That(session.TryRowToJetty(), Is.True);
            Assert.That(session.TryRecordRuiyaConsent(true, 2), Is.True);
            Assert.That(session.TryConfirmActualBoarding(2), Is.True);
            Assert.That(session.IsComplete, Is.True);
            return session;
        }

        [Test]
        public void DestroyingCoreChildControlDetachesLocaleAndBlocksClicks()
        {
            var root = new GameObject("M09 night ferry partial teardown");
            string savedLanguage = SoundwellUiLocalization.Language;
            M09EastShoreNightCrossingView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var session = new M09EastShoreNightCrossingSession(3);
                int deliveries = 0;

                view = new M09EastShoreNightCrossingView(
                    root.transform, session, 2, _ => true, _ => deliveries++);
                Assert.That(view.IsDisposed, Is.False);

                // Destroy a core child control while leaving canvas and panel alive.
                UnityEngine.Object.DestroyImmediate(view.FinishButton.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => view.ListenOrProceedButton.onClick.Invoke());
                Assert.That(session.Stage, Is.EqualTo(M09NightCrossingStage.FogApproach));
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
            var root = new GameObject("M09 night ferry aspect guard");
            M09EastShoreNightCrossingView view = null;
            try
            {
                var session = new M09EastShoreNightCrossingSession(3);
                view = new M09EastShoreNightCrossingView(
                    root.transform, session, 2, _ => true, _ => { });

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
            var root = new GameObject("M09 night ferry reentrant finish");
            M09EastShoreNightCrossingView view = null;
            try
            {
                var session = CompletedRescuedSession();
                int deliveries = 0;

                view = new M09EastShoreNightCrossingView(
                    root.transform, session, 2, _ => true, _ =>
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
            var root = new GameObject("M09 night ferry dispose-then-throw");
            string savedLanguage = SoundwellUiLocalization.Language;
            M09EastShoreNightCrossingView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var session = CompletedRescuedSession();

                view = new M09EastShoreNightCrossingView(
                    root.transform, session, 2, _ => true, _ =>
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
