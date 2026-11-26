using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M04RosterReconciliationViewLifecycleTests
    {
        [Test]
        public void DestroyingCoreChildControlMarksDisposedDetachesLocaleAndBlocksActions()
        {
            var root = new GameObject("M04 roster partial teardown");
            M04RosterReconciliationView view = null;
            string savedLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var ledger = new M04RosterLedger();
                ledger.AddEntry("dock-1", "Confirmed returnee", M04RosterStatus.ConfirmedAtDock);
                int finished = 0;
                int back = 0;

                view = new M04RosterReconciliationView(
                    root.transform, ledger, 1, _ => finished++, () => back++);
                Assert.That(view.IsDisposed, Is.False);

                // Destroy a core child control while leaving canvasObject and heading alive.
                UnityEngine.Object.DestroyImmediate(view.FinishButton.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() =>
                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese));
                Assert.DoesNotThrow(() => view.Refresh());
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(16f / 9f));
                Assert.DoesNotThrow(() => view.BookButton.onClick.Invoke());
                Assert.DoesNotThrow(() => view.BackButton.onClick.Invoke());
                Assert.Throws<ObjectDisposedException>(() => view.SetVerifiedBerths(2));

                Assert.That(ledger.BookedCount, Is.Zero);
                Assert.That(finished, Is.Zero);
                Assert.That(back, Is.Zero);
                Assert.DoesNotThrow(() => view.Dispose());
                Assert.That(view.IsDisposed, Is.True);
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
            var root = new GameObject("M04 roster aspect guard");
            M04RosterReconciliationView view = null;
            try
            {
                var ledger = new M04RosterLedger();
                view = new M04RosterReconciliationView(
                    root.transform, ledger, 1, _ => { }, () => { });

                view.LayoutForViewportAspect(16f / 9f);
                Vector3 expectedScale = view.Summary.transform.parent.localScale;

                foreach (float badAspect in new[]
                {
                    0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity
                })
                {
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(badAspect));
                    Assert.That(view.Summary.transform.parent.localScale, Is.EqualTo(expectedScale));
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
        public void ThrowingOnFinishedAfterDisposingViewDoesNotResurrectView()
        {
            var root = new GameObject("M04 roster dispose-then-throw finish");
            M04RosterReconciliationView view = null;
            string savedLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var ledger = new M04RosterLedger();
                ledger.AddEntry("dock-1", "Confirmed returnee", M04RosterStatus.ConfirmedAtDock);

                view = new M04RosterReconciliationView(
                    root.transform, ledger, 1, _ =>
                    {
                        view.Dispose();
                        throw new InvalidOperationException("Host disposed view before throwing");
                    }, () => { });

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

        [Test]
        public void ThrowingOnBackAfterDisposingViewDoesNotResurrectView()
        {
            var root = new GameObject("M04 roster dispose-then-throw back");
            M04RosterReconciliationView view = null;
            string savedLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var ledger = new M04RosterLedger();
                ledger.AddEntry("dock-1", "Confirmed returnee", M04RosterStatus.ConfirmedAtDock);

                view = new M04RosterReconciliationView(
                    root.transform, ledger, 1, _ => { }, () =>
                    {
                        view.Dispose();
                        throw new InvalidOperationException("Host disposed view before throwing on back");
                    });

                Assert.Throws<InvalidOperationException>(() =>
                    view.BackButton.onClick.Invoke());
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
