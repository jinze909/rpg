using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class M13DepartureRiskViewLifecycleTests
    {
        [Test]
        public void DestroyingButtonLabelMarksDisposedDetachesLocaleAndBlocksClicks()
        {
            var root = new GameObject("M13 view partial teardown");
            string savedLanguage = SoundwellUiLocalization.Language;
            M13DepartureRiskView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var session = new M13DepartureRiskSession(safetyPreparationComplete: false);
                int departures = 0;

                view = new M13DepartureRiskView(root.transform, session, _ => departures++);
                Assert.That(view.IsDisposed, Is.False);

                // Destroy a button's Text label while leaving buttons, canvas, and panel alive.
                var routeALabel = view.RouteAButton.GetComponentInChildren<Text>();
                Assert.That(routeALabel, Is.Not.Null);
                UnityEngine.Object.DestroyImmediate(routeALabel);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => view.CompletePrepButton.onClick.Invoke());
                Assert.DoesNotThrow(() => view.RouteAButton.onClick.Invoke());
                Assert.That(session.SafetyPreparationComplete, Is.False);
                Assert.That(departures, Is.Zero);
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
            var root = new GameObject("M13 view aspect guard");
            M13DepartureRiskView view = null;
            try
            {
                var session = new M13DepartureRiskSession(safetyPreparationComplete: true);
                view = new M13DepartureRiskView(root.transform, session, _ => { });

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
        public void ReentrantRouteConfirmClickIsBlockedWhileHostCallbackIsInFlight()
        {
            var root = new GameObject("M13 view reentrant route");
            M13DepartureRiskView view = null;
            try
            {
                var session = new M13DepartureRiskSession(safetyPreparationComplete: true);
                int departures = 0;

                view = new M13DepartureRiskView(root.transform, session, _ =>
                {
                    departures++;
                    Assert.That(view.RouteAButton.interactable, Is.False);
                    view.RouteAButton.onClick.Invoke();
                });

                view.RouteAButton.onClick.Invoke();
                Assert.That(departures, Is.EqualTo(1));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ThrowingOnDepartureAfterDisposingViewDoesNotResurrectView()
        {
            var root = new GameObject("M13 view dispose-then-throw");
            string savedLanguage = SoundwellUiLocalization.Language;
            M13DepartureRiskView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var session = new M13DepartureRiskSession(safetyPreparationComplete: true);

                view = new M13DepartureRiskView(root.transform, session, _ =>
                {
                    view.Dispose();
                    throw new InvalidOperationException("Host disposed view before throwing");
                });

                Assert.Throws<InvalidOperationException>(() =>
                    view.RouteAButton.onClick.Invoke());
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
