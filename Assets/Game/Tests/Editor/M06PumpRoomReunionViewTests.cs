using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M06PumpRoomReunionViewTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedVerifiedM05Arrival(
            Dictionary<string, int> values,
            int readyBerths = 10,
            bool tenderArrived = true,
            bool receiverMet = true,
            M05LetterDecision? letterDecision = null)
        {
            Action<string, int> write = (key, value) => values[key] = value;
            Func<string, int> read = Reader(values);
            var manifest = new M04DepartureManifest(M03VoyageChoice.QiQueMerchant,
                "Verified civilian hull", "A Yuan", 4, "Shallow tender",
                true, 16, 8, "Named receiver", false, false);
            Assert.That(manifest.ConfirmDeparture(), Is.True);
            Assert.That(M04DepartureFacts.TryRecord(manifest, write), Is.True);
            Assert.That(M04DepartureFacts.TryMarkSailed(read, write), Is.True);

            var fog = new FogListeningSession(
                new[] { new FogListeningCue(FogBearing.Ahead, FogCueKind.ClearWater, 80) },
                maxListenFocus: 1, maxHullIntegrity: 2);
            fog.ApplyHelmAction(FogHelmAction.HoldCourse);
            Assert.That(fog.IsComplete && fog.Succeeded, Is.True);
            Assert.That(FogListeningFacts.TryRecord(fog, write), Is.True);

            Assert.That(M05LandingFacts.TryRecord(
                new M05LandingReport(3, readyBerths, tenderArrived, receiverMet), read, write), Is.True);
            if (letterDecision.HasValue)
                Assert.That(M05LettersFacts.TryRecord(letterDecision.Value, read, write), Is.True);
        }

        [Test]
        public void FullReunionWithCarriedLettersReachesLetterStageAndRecordsChosenPlanOnce()
        {
            var root = new GameObject("M06 reunion view carried letters");
            var values = new Dictionary<string, int>();
            SeedVerifiedM05Arrival(values, letterDecision: M05LetterDecision.TakePacket);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            string previousLanguage = SoundwellUiLocalization.Language;
            M06PumpRoomReunionView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(M06PumpRoomReunionSession.TryCreateFromSavedFacts(read, out var session), Is.True);
                int saves = 0;
                view = new M06PumpRoomReunionView(root.transform, session, plan =>
                {
                    saves++;
                    Assert.That(session.TryConfirmRescuePlan(plan, read, write), Is.True);
                });

                Assert.That(view.Body.text, Does.Contain("10"));
                view.GreetingWrenchButton.onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(M06ReunionStage.PumpRoomWitnesses));
                Assert.That(view.Body.text, Does.Contain("小七"));

                view.AdvanceButton.onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(M06ReunionStage.LetterHandoff));
                Assert.That(view.Body.text, Does.Contain("陶霁"));

                view.AdvanceButton.onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(M06ReunionStage.OulinRetaliationTruth));
                Assert.That(view.Body.text, Does.Contain("老张"));

                view.AdvanceButton.onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(M06ReunionStage.RescuePlanDecision));
                Assert.That(view.MinerShutdownButton.gameObject.activeSelf, Is.True);

                view.MinerShutdownButton.onClick.Invoke();
                view.MinerShutdownButton.onClick.Invoke();
                Assert.That(saves, Is.EqualTo(1));
                Assert.That(session.Stage, Is.EqualTo(M06ReunionStage.Completed));
                Assert.That(view.MinerShutdownButton.gameObject.activeSelf, Is.False);
                Assert.That(M06RescuePlanFacts.TryRead(read, out M06ReunionRecord? saved), Is.True);
                Assert.That(saved.Value.Plan, Is.EqualTo(M06RescuePlan.MinerShutdown));
                Assert.That(saved.Value.LetterDelivery, Is.EqualTo(M06LetterDeliveryOutcome.DeliveredWithPending));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ZeroUsableBerthsAndUnopenedLettersSkipsDeliveryAndShowsChannelChoice()
        {
            var root = new GameObject("M06 reunion view zero berths");
            var values = new Dictionary<string, int>();
            SeedVerifiedM05Arrival(values, readyBerths: 0, tenderArrived: false, receiverMet: false, letterDecision: null);
            Func<string, int> read = Reader(values);
            string previousLanguage = SoundwellUiLocalization.Language;
            M06PumpRoomReunionView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(M06PumpRoomReunionSession.TryCreateFromSavedFacts(read, out var session), Is.True);
                view = new M06PumpRoomReunionView(root.transform, session, _ => { });

                Assert.That(view.Body.text, Does.Contain("领航船到了"));
                Assert.That(view.GreetingWithdrawalOrChannelButton.GetComponentInChildren<UnityEngine.UI.Text>().text,
                    Does.Contain("先把能走的水道核一下"));
                view.GreetingWithdrawalOrChannelButton.onClick.Invoke();
                view.AdvanceButton.onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(M06ReunionStage.OulinRetaliationTruth),
                    "Validation N76: unopened M05 letters must skip the letter handoff stage.");
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InterruptedPlanSaveKeepsAllFourPlanButtonsInteractableForRetry()
        {
            var root = new GameObject("M06 reunion view retry");
            var values = new Dictionary<string, int>();
            SeedVerifiedM05Arrival(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            M06PumpRoomReunionView view = null;
            try
            {
                Assert.That(M06PumpRoomReunionSession.TryCreateFromSavedFacts(read, out var session), Is.True);
                int attempts = 0;
                view = new M06PumpRoomReunionView(root.transform, session, plan =>
                {
                    attempts++;
                    if (attempts == 1)
                        throw new InvalidOperationException("Interrupted Makinom write");
                    Assert.That(session.TryConfirmRescuePlan(plan, read, write), Is.True);
                });

                view.GreetingBringBackButton.onClick.Invoke();
                view.AdvanceButton.onClick.Invoke();
                view.AdvanceButton.onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(M06ReunionStage.RescuePlanDecision));

                Assert.Throws<InvalidOperationException>(() => view.CargoCoverButton.onClick.Invoke());
                Assert.That(session.Stage, Is.EqualTo(M06ReunionStage.RescuePlanDecision));
                Assert.That(view.CargoCoverButton.interactable, Is.True);

                view.CargoCoverButton.onClick.Invoke();
                Assert.That(attempts, Is.EqualTo(2));
                Assert.That(session.Stage, Is.EqualTo(M06ReunionStage.Completed));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LocaleSwitchAndViewportAspectUpdatesStayInsideSafeRegion()
        {
            var root = new GameObject("M06 reunion view locale");
            var values = new Dictionary<string, int>();
            SeedVerifiedM05Arrival(values);
            Func<string, int> read = Reader(values);
            string previousLanguage = SoundwellUiLocalization.Language;
            M06PumpRoomReunionView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                Assert.That(M06PumpRoomReunionSession.TryCreateFromSavedFacts(read, out var session), Is.True);
                view = new M06PumpRoomReunionView(root.transform, session, _ => { });
                Assert.That(view.Panel.anchorMin.y, Is.EqualTo(0f));
                Assert.That(view.Panel.anchorMax.y, Is.EqualTo(0f));
                Assert.That(view.Panel.sizeDelta.x, Is.LessThan(SoundwellUiTheme.ReferenceWidth));
                Assert.That(view.Heading.text, Does.Contain("UNDERGROUND PUMP HALL"));

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.Heading.text, Does.Contain("地下主泵房"));
                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(aspect));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NullArgumentsRejectedAndDisposalBlocksLateClicks()
        {
            var root = new GameObject("M06 reunion view dispose");
            var values = new Dictionary<string, int>();
            SeedVerifiedM05Arrival(values);
            Func<string, int> read = Reader(values);
            M06PumpRoomReunionView view = null;
            try
            {
                Assert.That(M06PumpRoomReunionSession.TryCreateFromSavedFacts(read, out var session), Is.True);
                Assert.Throws<ArgumentNullException>(() => new M06PumpRoomReunionView(null, session, _ => { }));
                Assert.Throws<ArgumentNullException>(() => new M06PumpRoomReunionView(root.transform, null, _ => { }));
                Assert.Throws<ArgumentNullException>(() => new M06PumpRoomReunionView(root.transform, session, null));

                view = new M06PumpRoomReunionView(root.transform, session, _ => { });
                var btn = view.GreetingBringBackButton;
                view.Dispose();
                btn.onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(M06ReunionStage.VesselBriefing));
                Assert.DoesNotThrow(() => view.Refresh());
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(1f));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
