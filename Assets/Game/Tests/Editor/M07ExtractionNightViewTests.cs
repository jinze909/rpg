using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M07ExtractionNightViewTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedVerifiedM06(
            Dictionary<string, int> values,
            M06RescuePlan plan,
            M05LetterDecision? letterDecision = M05LetterDecision.TakePacket)
        {
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

            var letterOutcome = M06LetterDeliveryOutcome.NotCarried;
            if (letterDecision.HasValue)
            {
                Assert.That(M05LettersFacts.TryRecord(letterDecision.Value, read, write), Is.True);
                if (letterDecision.Value == M05LetterDecision.TakePacket)
                    letterOutcome = M06LetterDeliveryOutcome.DeliveredWithPending;
            }

            Assert.That(M06RescuePlanFacts.TryRecord(
                new M06ReunionRecord(plan, true, letterOutcome, true), read, write), Is.True);
        }

        [Test]
        public void MinerShutdownFlowShowsOldZhangFarewellReturnLettersAndPersistsRollCall()
        {
            var root = new GameObject("M07 view miner shutdown");
            var values = new Dictionary<string, int>();
            SeedVerifiedM06(values, M06RescuePlan.MinerShutdown, M05LetterDecision.TakePacket);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            string previousLanguage = SoundwellUiLocalization.Language;
            M07ExtractionNightView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(M07ExtractionNightSession.TryCreateFromSavedFacts(read, out var session), Is.True);
                int saves = 0;
                view = new M07ExtractionNightView(root.transform, session, (liang, yida) =>
                {
                    saves++;
                    Assert.That(session.TryCompleteDockRollCall(liang, yida, true, read, write), Is.True);
                });

                view.ExecutePlanButton.onClick.Invoke();
                Assert.That(view.Body.text, Does.Contain("对不住了，老张"));
                view.AdvanceButton.onClick.Invoke();

                view.RusFledButton.onClick.Invoke();
                Assert.That(view.Body.text, Does.Contain("写了一整页"));
                view.AdvanceButton.onClick.Invoke();

                Assert.That(view.Body.text, Does.Contain("伊蕾监察副本"));
                Assert.That(view.Body.text, Does.Contain("七十二"));
                view.LiangStayButton.onClick.Invoke();
                view.LiangStayButton.onClick.Invoke();

                Assert.That(saves, Is.EqualTo(1));
                Assert.That(session.Stage, Is.EqualTo(M07ExtractionStage.Completed));
                Assert.That(M07ExtractionFacts.TryRead(read, out M07ExtractionRecord? saved), Is.True);
                Assert.That(saved.HasValue, Is.True);
                Assert.That(saved.Value.LowerLevelEvacuatedCount, Is.EqualTo(500));
                Assert.That(saved.Value.BoardedFirstBatchCount, Is.EqualTo(300));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BlockedRouteFallbackShowsNoPumpStopAndNoFabricatedReturnLetters()
        {
            var root = new GameObject("M07 view fallback");
            var values = new Dictionary<string, int>();
            SeedVerifiedM06(values, M06RescuePlan.ForceBreakout, letterDecision: null);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            string previousLanguage = SoundwellUiLocalization.Language;
            M07ExtractionNightView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(M07ExtractionNightSession.TryCreateFromSavedFacts(read, out var session), Is.True);
                view = new M07ExtractionNightView(root.transform, session, (liang, yida) =>
                {
                    Assert.That(session.TryCompleteDockRollCall(liang, yida, true, read, write), Is.True);
                });

                view.PivotFallbackButton.onClick.Invoke();
                Assert.That(view.Body.text, Does.Not.Contain("对不住了，老张"));
                Assert.That(view.Body.text, Does.Contain("主泵由留守工匠维持运转"));
                view.AdvanceButton.onClick.Invoke();

                view.ArrestRusButton.onClick.Invoke();
                Assert.That(view.Body.text, Does.Contain("不生成回信"));
                view.AdvanceButton.onClick.Invoke();

                view.LiangForceButton.onClick.Invoke();
                Assert.That(M07ExtractionFacts.TryRead(read, out M07ExtractionRecord? saved), Is.True);
                Assert.That(saved.Value.ExecutedRoute, Is.EqualTo(M07ExecutedRoute.FallbackOfficialHandover));
                Assert.That(saved.Value.StrainsShenRelation, Is.True);
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InterruptedRollCallSaveRetainsLiangButtonsForCleanRetry()
        {
            var root = new GameObject("M07 view retry");
            var values = new Dictionary<string, int>();
            SeedVerifiedM06(values, M06RescuePlan.OfficialHandover);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M07ExtractionNightView view = null;
            try
            {
                Assert.That(M07ExtractionNightSession.TryCreateFromSavedFacts(read, out var session), Is.True);
                int attempts = 0;
                view = new M07ExtractionNightView(root.transform, session, (liang, yida) =>
                {
                    attempts++;
                    if (attempts == 1)
                        throw new InvalidOperationException("Interrupted roll call save");
                    Assert.That(session.TryCompleteDockRollCall(liang, yida, true, read, write), Is.True);
                });

                view.ExecutePlanButton.onClick.Invoke();
                view.AdvanceButton.onClick.Invoke();
                view.ArrestRusButton.onClick.Invoke();
                view.AdvanceButton.onClick.Invoke();

                Assert.Throws<InvalidOperationException>(() => view.LiangPersuadeButton.onClick.Invoke());
                Assert.That(session.Stage, Is.EqualTo(M07ExtractionStage.DockRollCall));
                Assert.That(view.LiangPersuadeButton.interactable, Is.True);

                view.LiangPersuadeButton.onClick.Invoke();
                Assert.That(attempts, Is.EqualTo(2));
                Assert.That(session.Stage, Is.EqualTo(M07ExtractionStage.Completed));
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
            var root = new GameObject("M07 view locale");
            var values = new Dictionary<string, int>();
            SeedVerifiedM06(values, M06RescuePlan.CargoCover);
            Func<string, int> read = Reader(values);
            string previousLanguage = SoundwellUiLocalization.Language;
            M07ExtractionNightView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                Assert.That(M07ExtractionNightSession.TryCreateFromSavedFacts(read, out var session), Is.True);
                view = new M07ExtractionNightView(root.transform, session, (_, __) => { });
                Assert.That(view.Heading.text, Does.Contain("RED SALT EXTRACTION NIGHT"));
                Assert.That(view.Panel.anchorMin.y, Is.EqualTo(0f));

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.Heading.text, Does.Contain("赤盐营救之夜"));
                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(aspect));

                var btn = view.ExecutePlanButton;
                view.Dispose();
                btn.onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(M07ExtractionStage.RouteExecution));
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
