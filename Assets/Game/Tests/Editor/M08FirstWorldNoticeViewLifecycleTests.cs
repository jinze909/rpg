using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M08FirstWorldNoticeViewLifecycleTests
    {
        [Test]
        public void DestroyingCoreChildControlDetachesLocaleAndBlocksAcknowledge()
        {
            var root = new GameObject("M08 notice partial teardown");
            string savedLanguage = SoundwellUiLocalization.Language;
            M08FirstWorldNoticeView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                int calls = 0;
                view = new M08FirstWorldNoticeView(root.transform, () =>
                {
                    calls++;
                    return true;
                });
                Assert.That(view.IsDisposed, Is.False);

                // Destroy a core child control while leaving canvas and panel alive.
                UnityEngine.Object.DestroyImmediate(view.CaseList.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => view.AcknowledgeButton.onClick.Invoke());
                Assert.That(calls, Is.Zero);
                Assert.That(view.IsDelivered, Is.False);
                Assert.DoesNotThrow(() =>
                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese));
                Assert.DoesNotThrow(() => view.Refresh());
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(16f / 9f));
                Assert.DoesNotThrow(() => view.Dispose());
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
            var root = new GameObject("M08 notice aspect guard");
            M08FirstWorldNoticeView view = null;
            try
            {
                view = new M08FirstWorldNoticeView(root.transform, () => true);

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
        public void ReentrantAcknowledgeClickIsBlockedWhileHostCallbackIsInFlight()
        {
            var root = new GameObject("M08 notice reentrant ack");
            M08FirstWorldNoticeView view = null;
            try
            {
                int calls = 0;
                view = new M08FirstWorldNoticeView(root.transform, () =>
                {
                    calls++;
                    Assert.That(view.AcknowledgeButton.interactable, Is.False);
                    view.AcknowledgeButton.onClick.Invoke();
                    return true;
                });

                view.AcknowledgeButton.onClick.Invoke();
                Assert.That(calls, Is.EqualTo(1));
                Assert.That(view.IsDelivered, Is.True);
                Assert.That(view.AcknowledgeButton.interactable, Is.False);
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
            var root = new GameObject("M08 notice throw handling");
            string savedLanguage = SoundwellUiLocalization.Language;
            M08FirstWorldNoticeView retryView = null;
            M08FirstWorldNoticeView disposedView = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);

                int attempts = 0;
                retryView = new M08FirstWorldNoticeView(root.transform, () =>
                {
                    attempts++;
                    if (attempts == 1)
                        throw new InvalidOperationException("Interrupted Makinom write");
                    return true;
                });
                Assert.Throws<InvalidOperationException>(() =>
                    retryView.AcknowledgeButton.onClick.Invoke());
                Assert.That(retryView.IsDelivered, Is.False);
                Assert.That(retryView.AcknowledgeButton.interactable, Is.True);

                retryView.AcknowledgeButton.onClick.Invoke();
                Assert.That(attempts, Is.EqualTo(2));
                Assert.That(retryView.IsDelivered, Is.True);

                disposedView = new M08FirstWorldNoticeView(root.transform, () =>
                {
                    disposedView.Dispose();
                    throw new InvalidOperationException("Host disposed view before throwing");
                });
                Assert.Throws<InvalidOperationException>(() =>
                    disposedView.AcknowledgeButton.onClick.Invoke());
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
