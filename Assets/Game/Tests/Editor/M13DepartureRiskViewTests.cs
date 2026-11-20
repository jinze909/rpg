using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M13DepartureRiskViewTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void UnpreparedRouteBAndCStayDisabledUntilWarningAndConsentOrPrepCompleted()
        {
            var root = new GameObject("M13 view risk gate");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            string previousLanguage = SoundwellUiLocalization.Language;
            M13DepartureRiskView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                var session = new M13DepartureRiskSession(safetyPreparationComplete: false);
                view = new M13DepartureRiskView(root.transform, session, route =>
                {
                    Assert.That(session.TryConfirmDeparture(route, read, write), Is.True);
                });

                Assert.That(view.RouteAButton.interactable, Is.True);
                Assert.That(view.RouteBButton.interactable, Is.False);
                Assert.That(view.RouteCButton.interactable, Is.False);
                Assert.That(view.AcceptRiskConsentButton.gameObject.activeSelf, Is.False);

                session.DeliverQiQueWarning();
                view.Refresh();
                Assert.That(view.AcceptRiskConsentButton.gameObject.activeSelf, Is.True);
                Assert.That(view.RouteCButton.interactable, Is.False);

                view.AcceptRiskConsentButton.onClick.Invoke();
                Assert.That(view.RouteCButton.interactable, Is.True);
                Assert.That(view.RouteBButton.interactable, Is.False,
                    "Route B still requires 3-bell reverse signal agreement.");

                view.RouteCButton.onClick.Invoke();
                Assert.That(session.IsDeparted, Is.True);
                Assert.That(session.ConfirmedRoute, Is.EqualTo(M13ApproachRoute.RouteCOldCanalNightRun));
                Assert.That(M13DepartureRiskFacts.TryReadDeparture(read, out M13DepartureRecord? saved), Is.True);
                Assert.That(saved.Value.CanAyuanSufferHighRiskOutcome, Is.True);
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RouteBUnlocksAfterSafetyPrepAndThreeBellAgreementAndSettlesOnce()
        {
            var root = new GameObject("M13 view route B");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M13DepartureRiskView view = null;
            try
            {
                var session = new M13DepartureRiskSession(safetyPreparationComplete: false);
                int saves = 0;
                view = new M13DepartureRiskView(root.transform, session, route =>
                {
                    saves++;
                    Assert.That(session.TryConfirmDeparture(route, read, write), Is.True);
                });

                view.CompletePrepButton.onClick.Invoke();
                Assert.That(view.RouteCButton.interactable, Is.True);
                Assert.That(view.RouteBButton.interactable, Is.False);

                view.AgreeThreeBellsButton.onClick.Invoke();
                Assert.That(view.RouteBButton.interactable, Is.True);

                view.RouteBButton.onClick.Invoke();
                view.RouteBButton.onClick.Invoke();
                Assert.That(saves, Is.EqualTo(1));
                Assert.That(view.RouteBButton.gameObject.activeSelf, Is.False);
                Assert.That(M13DepartureRiskFacts.TryReadDeparture(read, out M13DepartureRecord? saved), Is.True);
                Assert.That(saved.Value.CanAyuanSufferHighRiskOutcome, Is.False);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InterruptedDepartureSaveRetainsRouteButtonsForCleanRetry()
        {
            var root = new GameObject("M13 view retry");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M13DepartureRiskView view = null;
            try
            {
                var session = new M13DepartureRiskSession(safetyPreparationComplete: true);
                int attempts = 0;
                view = new M13DepartureRiskView(root.transform, session, route =>
                {
                    attempts++;
                    if (attempts == 1)
                        throw new InvalidOperationException("Interrupted M13 write");
                    Assert.That(session.TryConfirmDeparture(route, read, write), Is.True);
                });

                Assert.Throws<InvalidOperationException>(() => view.RouteAButton.onClick.Invoke());
                Assert.That(session.IsDeparted, Is.False);
                Assert.That(view.RouteAButton.interactable, Is.True);

                view.RouteAButton.onClick.Invoke();
                Assert.That(attempts, Is.EqualTo(2));
                Assert.That(session.IsDeparted, Is.True);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LocaleSwitchAspectScalingAndDisposalGuardsWorkCleanly()
        {
            var root = new GameObject("M13 view locale");
            string previousLanguage = SoundwellUiLocalization.Language;
            M13DepartureRiskView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var session = new M13DepartureRiskSession(safetyPreparationComplete: false);
                view = new M13DepartureRiskView(root.transform, session, _ => { });
                Assert.That(view.Heading.text, Does.Contain("APPROACH ROUTE"));
                Assert.That(view.Panel.anchorMin.y, Is.EqualTo(0f));

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.Heading.text, Does.Contain("进场路线与风险确认"));
                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(aspect));

                var btn = view.CompletePrepButton;
                view.Dispose();
                btn.onClick.Invoke();
                Assert.That(session.SafetyPreparationComplete, Is.False);
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
