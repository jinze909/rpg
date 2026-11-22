using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M07ExtractionNightViewLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static M07ExtractionNightSession CreateVerifiedSession(M06RescuePlan plan)
        {
            var values = new Dictionary<string, int>();
            Action<string, int> write = (key, value) => values[key] = value;
            Func<string, int> read = Reader(values);

            var manifest = new M04DepartureManifest(
                M03VoyageChoice.QiQueMerchant,
                "Confirmed hull", "A Yuan", 4, "Shallow tender",
                true, 16, 8, "Named receiver", false, false);
            Assert.That(manifest.ConfirmDeparture(), Is.True);
            Assert.That(M04DepartureFacts.TryRecord(manifest, write), Is.True);
            Assert.That(M04DepartureFacts.TryMarkSailed(read, write), Is.True);

            var fog = new FogListeningSession(
                new[] { new FogListeningCue(FogBearing.Ahead, FogCueKind.ClearWater, 80) },
                maxListenFocus: 1, maxHullIntegrity: 2);
            fog.ApplyHelmAction(FogHelmAction.HoldCourse);
            Assert.That(FogListeningFacts.TryRecord(fog, write), Is.True);

            Assert.That(M05LandingFacts.TryRecord(
                new M05LandingReport(3, 10, true, true), read, write), Is.True);
            Assert.That(M05LettersFacts.TryRecord(
                M05LetterDecision.TakePacket, read, write), Is.True);
            Assert.That(M06RescuePlanFacts.TryRecord(
                new M06ReunionRecord(plan, true, M06LetterDeliveryOutcome.DeliveredWithPending, true),
                read, write), Is.True);

            Assert.That(M07ExtractionNightSession.TryCreateFromSavedFacts(read, out var session), Is.True);
            return session;
        }

        [Test]
        public void DestroyingCoreChildControlDetachesLocaleAndBlocksClicks()
        {
            var root = new GameObject("M07 view partial teardown");
            string savedLanguage = SoundwellUiLocalization.Language;
            M07ExtractionNightView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var session = CreateVerifiedSession(M06RescuePlan.MinerShutdown);
                int rollCalls = 0;

                view = new M07ExtractionNightView(
                    root.transform, session, (_, __) => rollCalls++);
                Assert.That(view.IsDisposed, Is.False);

                // Destroy a core child control while leaving canvas and panel alive.
                UnityEngine.Object.DestroyImmediate(view.AdvanceButton.gameObject);
                Assert.That(view.IsDisposed, Is.True);

                Assert.DoesNotThrow(() => view.ExecutePlanButton.onClick.Invoke());
                Assert.That(session.Stage, Is.EqualTo(M07ExtractionStage.RouteExecution));
                Assert.DoesNotThrow(() =>
                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese));
                Assert.DoesNotThrow(() => view.Refresh());
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(16f / 9f));
                Assert.DoesNotThrow(() => view.Dispose());
                Assert.That(rollCalls, Is.Zero);
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
            var root = new GameObject("M07 view aspect guard");
            M07ExtractionNightView view = null;
            try
            {
                var session = CreateVerifiedSession(M06RescuePlan.OfficialHandover);
                view = new M07ExtractionNightView(root.transform, session, (_, __) => { });

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
        public void InactiveChoiceButtonsCannotTriggerRouteExecution()
        {
            var root = new GameObject("M07 view route index guard");
            M07ExtractionNightView view = null;
            try
            {
                var session = CreateVerifiedSession(M06RescuePlan.CargoCover);
                view = new M07ExtractionNightView(root.transform, session, (_, __) => { });

                Assert.That(session.Stage, Is.EqualTo(M07ExtractionStage.RouteExecution));
                Assert.That(view.RusLostContractButton.interactable, Is.False);
                Assert.That(view.RusShelteredButton.interactable, Is.False);

                view.RusLostContractButton.onClick.Invoke();
                view.RusShelteredButton.onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(M07ExtractionStage.RouteExecution));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ThrowingOnRollCallAfterDisposingViewDoesNotResurrectView()
        {
            var root = new GameObject("M07 view dispose-then-throw roll call");
            string savedLanguage = SoundwellUiLocalization.Language;
            M07ExtractionNightView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var session = CreateVerifiedSession(M06RescuePlan.ForceBreakout);
                view = new M07ExtractionNightView(root.transform, session, (_, __) =>
                {
                    view.Dispose();
                    throw new InvalidOperationException("Host disposed view before throwing");
                });

                view.ExecutePlanButton.onClick.Invoke();
                view.AdvanceButton.onClick.Invoke();
                view.ArrestRusButton.onClick.Invoke();
                view.AdvanceButton.onClick.Invoke();

                Assert.Throws<InvalidOperationException>(() =>
                    view.LiangStayButton.onClick.Invoke());
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
