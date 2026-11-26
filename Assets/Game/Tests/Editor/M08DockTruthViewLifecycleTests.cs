using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M08DockTruthViewLifecycleTests
    {
        [Test]
        public void DestroyingCoreChildControlDetachesLocaleAndBlocksClicks()
        {
            var root = new GameObject("M08 dock truth partial teardown");
            string savedLanguage = SoundwellUiLocalization.Language;
            M08DockTruthView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var session = new M08DockTruthSession(true, true);
                int writes = 0;

                view = new M08DockTruthView(root.transform, session, _ =>
                {
                    writes++;
                    return true;
                });
                Assert.That(view.IsDisposed, Is.False);

                // Destroy a core child control while leaving canvas and panel alive.
                UnityEngine.Object.DestroyImmediate(view.FinishButton.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => view.HearButton.onClick.Invoke());
                Assert.That(session.Stage, Is.EqualTo(M08DockTruthStage.AwaitingTruth));
                Assert.DoesNotThrow(() =>
                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese));
                Assert.DoesNotThrow(() => view.Refresh());
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(16f / 9f));
                Assert.DoesNotThrow(() => view.Dispose());
                Assert.That(writes, Is.Zero);
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
            var root = new GameObject("M08 dock truth aspect guard");
            M08DockTruthView view = null;
            try
            {
                var session = new M08DockTruthSession(true, true);
                view = new M08DockTruthView(root.transform, session, _ => true);

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
        public void ReentrantFinishClickIsBlockedWhileHostAcknowledgeIsInFlight()
        {
            var root = new GameObject("M08 dock truth reentrant finish");
            M08DockTruthView view = null;
            try
            {
                var session = new M08DockTruthSession(true, true);
                int writes = 0;

                view = new M08DockTruthView(root.transform, session, _ =>
                {
                    writes++;
                    Assert.That(view.FinishButton.interactable, Is.False);
                    view.FinishButton.onClick.Invoke();
                    return true;
                });

                view.HearButton.onClick.Invoke();
                view.SecondResponseButton.onClick.Invoke();
                view.FinishButton.onClick.Invoke();

                Assert.That(writes, Is.EqualTo(1));
                Assert.That(session.IsCompleted, Is.True);
                Assert.That(view.FinishButton.interactable, Is.False);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ThrowingAcknowledgeAllowsRetryWhenLiveAndDoesNotResurrectWhenDisposed()
        {
            var root = new GameObject("M08 dock truth throw handling");
            string savedLanguage = SoundwellUiLocalization.Language;
            M08DockTruthView retryView = null;
            M08DockTruthView disposedView = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);

                // Case 1: Host throws without disposing -> button remains retryable.
                var retrySession = new M08DockTruthSession(true, true);
                int retryAttempts = 0;
                retryView = new M08DockTruthView(root.transform, retrySession, _ =>
                {
                    retryAttempts++;
                    if (retryAttempts == 1)
                        throw new InvalidOperationException("Interrupted Makinom write");
                    return true;
                });
                retryView.HearButton.onClick.Invoke();
                retryView.ThirdResponseButton.onClick.Invoke();
                Assert.Throws<InvalidOperationException>(() => retryView.FinishButton.onClick.Invoke());
                Assert.That(retrySession.IsCompleted, Is.False);
                Assert.That(retryView.FinishButton.interactable, Is.True);

                retryView.FinishButton.onClick.Invoke();
                Assert.That(retryAttempts, Is.EqualTo(2));
                Assert.That(retrySession.IsCompleted, Is.True);

                // Case 2: Host disposes view before throwing -> stays disposed cleanly.
                var disposedSession = new M08DockTruthSession(true, true);
                disposedView = new M08DockTruthView(root.transform, disposedSession, _ =>
                {
                    disposedView.Dispose();
                    throw new InvalidOperationException("Host disposed view before throwing");
                });
                disposedView.HearButton.onClick.Invoke();
                disposedView.FirstResponseButton.onClick.Invoke();
                Assert.Throws<InvalidOperationException>(() => disposedView.FinishButton.onClick.Invoke());
                Assert.That(disposedView.IsDisposed, Is.True);
                Assert.DoesNotThrow(() => disposedView.Refresh());
                Assert.DoesNotThrow(() =>
                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese));
            }
            finally
            {
                retryView?.Dispose();
                disposedView?.Dispose();
                SoundwellUiLocalization.SetLanguage(savedLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
