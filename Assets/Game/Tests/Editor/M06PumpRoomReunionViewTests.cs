using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M06PumpRoomReunionViewTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> store)
        {
            return key => store.TryGetValue(key, out int value) ? value : 0;
        }

        private static M06PumpRoomReunionSession CreateVerifiedSession(
            Dictionary<string, int> store,
            int readyBerths = 10,
            M05LetterDecision? letters = null)
        {
            Action<string, int> write = (key, value) => store[key] = value;
            Func<string, int> read = Reader(store);
            var manifest = new M04DepartureManifest(
                M03VoyageChoice.QiQueMerchant,
                "Verified merchant hull", "A Yuan", 4, "Shallow tender",
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
                new M05LandingReport(3, readyBerths, readyBerths > 0, readyBerths > 0),
                read, write), Is.True);
            if (letters.HasValue)
                Assert.That(M05LettersFacts.TryRecord(letters.Value, read, write), Is.True);
            Assert.That(M06PumpRoomReunionSession.TryCreateFromSavedFacts(
                read, out M06PumpRoomReunionSession session), Is.True);
            return session;
        }

        [Test]
        public void CarriedLettersAndPumpRoomStagesLeadToOnePersistedRescuePlan()
        {
            var root = new GameObject("M06 pump room carried letters");
            var store = new Dictionary<string, int>();
            string language = SoundwellUiLocalization.Language;
            M06PumpRoomReunionView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                var session = CreateVerifiedSession(store,
                    letters: M05LetterDecision.TakePacket);
                Func<string, int> read = Reader(store);
                Action<string, int> write = (key, value) => store[key] = value;
                int callbacks = 0;
                view = new M06PumpRoomReunionView(root.transform, session, plan =>
                {
                    callbacks++;
                    return session.TryConfirmRescuePlan(plan, read, write);
                });

                Assert.That(view.Narrative.text, Does.Contain("可用舱位 10"));
                view.ChoiceButtons[3].onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(M06ReunionStage.VesselBriefing),
                    "Hidden plan button must not skip the briefing.");
                view.ChoiceButtons[2].onClick.Invoke();
                Assert.That(session.SelectedGreeting, Is.EqualTo(
                    M06ReunionGreeting.DiscussWithdrawalOrChannel));
                Assert.That(session.Stage, Is.EqualTo(M06ReunionStage.PumpRoomWitnesses));
                Assert.That(view.Narrative.text, Does.Contain("主泵仍在排水"));

                view.ChoiceButtons[0].onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(M06ReunionStage.LetterHandoff));
                Assert.That(session.LettersHandled, Is.False);
                Assert.That(view.Narrative.text, Does.Contain("仍需待投"));
                view.ChoiceButtons[0].onClick.Invoke();
                Assert.That(session.LettersHandled, Is.True);
                Assert.That(session.PendingReturnLetterNoted, Is.True);

                view.ChoiceButtons[0].onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(M06ReunionStage.RescuePlanDecision));
                Assert.That(view.Narrative.text, Does.Contain("五百人"));
                Assert.That(view.ChoiceButtons[3].gameObject.activeSelf, Is.True);
                view.ChoiceButtons[2].onClick.Invoke();
                view.ChoiceButtons[2].onClick.Invoke();
                Assert.That(callbacks, Is.EqualTo(1));
                Assert.That(session.Stage, Is.EqualTo(M06ReunionStage.Completed));
                Assert.That(session.ConfirmedPlan, Is.EqualTo(M06RescuePlan.MinerShutdown));
                Assert.That(view.Narrative.text, Does.Contain("尚待 M07"));
                Assert.That(view.ChoiceButtons[0].gameObject.activeSelf, Is.False);
                Assert.That(M06RescuePlanFacts.TryRead(read, out M06ReunionRecord? record), Is.True);
                Assert.That(record.HasValue, Is.True);
                Assert.That(record.Value.Plan, Is.EqualTo(M06RescuePlan.MinerShutdown));
                Assert.That(record.Value.LetterDelivery,
                    Is.EqualTo(M06LetterDeliveryOutcome.DeliveredWithPending));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(language);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NoCarriedLettersSkipHandoffAndZeroBerthsDoNotPromiseFirstBatch()
        {
            var root = new GameObject("M06 pump room no letters");
            var store = new Dictionary<string, int>();
            string previousLanguage = SoundwellUiLocalization.Language;
            M06PumpRoomReunionView view = null;
            try
            {
                var session = CreateVerifiedSession(store, readyBerths: 0,
                    letters: M05LetterDecision.LeaveForNextBoat);
                Func<string, int> read = Reader(store);
                Action<string, int> write = (key, value) => store[key] = value;
                int callbacks = 0;
                view = new M06PumpRoomReunionView(root.transform, session, plan =>
                {
                    callbacks++;
                    return session.TryConfirmRescuePlan(plan, read, write);
                });
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.Narrative.text, Does.Contain("舱位 0"));
                Assert.That(view.Narrative.text, Does.Not.Contain("已核实可用舱位 10"));
                Assert.That(view.ChoiceButtons[2].GetComponentInChildren<UnityEngine.UI.Text>().text,
                    Does.Contain("先把能走的水道核一下"));

                view.ChoiceButtons[0].onClick.Invoke();
                view.ChoiceButtons[0].onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(M06ReunionStage.OulinRetaliationTruth),
                    "M05 did not carry the packet: M06 must not fabricate letter delivery.");
                Assert.That(session.LettersHandled, Is.False);
                view.ChoiceButtons[0].onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(M06ReunionStage.RescuePlanDecision));
                view.ChoiceButtons[0].onClick.Invoke();
                Assert.That(callbacks, Is.EqualTo(1));
                Assert.That(session.ConfirmedPlan, Is.EqualTo(M06RescuePlan.OfficialHandover));
                Assert.That(M06RescuePlanFacts.TryRead(read, out M06ReunionRecord? record), Is.True);
                Assert.That(record.Value.LetterDelivery,
                    Is.EqualTo(M06LetterDeliveryOutcome.NotCarried));
                Assert.That(record.Value.ReportedUsableCapacity, Is.False);
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InterruptedSaveRetainsStageAndChoiceForExactlyOneRetry()
        {
            var root = new GameObject("M06 interrupted rescue plan");
            var store = new Dictionary<string, int>();
            M06PumpRoomReunionView view = null;
            try
            {
                var session = CreateVerifiedSession(store);
                Func<string, int> read = Reader(store);
                Action<string, int> write = (key, value) => store[key] = value;
                int attempts = 0;
                view = new M06PumpRoomReunionView(root.transform, session, plan =>
                {
                    attempts++;
                    view.ChoiceButtons[1].onClick.Invoke();
                    if (attempts == 1)
                        throw new InvalidOperationException("Interrupted Makinom write");
                    return session.TryConfirmRescuePlan(plan, read, write);
                });

                view.ChoiceButtons[0].onClick.Invoke();
                view.ChoiceButtons[0].onClick.Invoke();
                view.ChoiceButtons[0].onClick.Invoke();
                Assert.That(session.Stage, Is.EqualTo(M06ReunionStage.RescuePlanDecision));
                Assert.Throws<InvalidOperationException>(() =>
                    view.ChoiceButtons[1].onClick.Invoke());
                Assert.That(session.Stage, Is.EqualTo(M06ReunionStage.RescuePlanDecision));
                Assert.That(view.ChoiceButtons[1].interactable, Is.True);
                Assert.That(attempts, Is.EqualTo(1));
                view.ChoiceButtons[1].onClick.Invoke();
                view.ChoiceButtons[1].onClick.Invoke();
                Assert.That(attempts, Is.EqualTo(2));
                Assert.That(session.ConfirmedPlan, Is.EqualTo(M06RescuePlan.CargoCover));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LocaleViewportAndDisposeCannotChangeTheSavedStage()
        {
            var root = new GameObject("M06 pump room layout");
            var store = new Dictionary<string, int>();
            var session = CreateVerifiedSession(store);
            var language = SoundwellUiLocalization.Language;
            M06PumpRoomReunionView view = null;
            try
            {
                Assert.Throws<ArgumentNullException>(() =>
                    new M06PumpRoomReunionView(null, session, _ => true));
                Assert.Throws<ArgumentNullException>(() =>
                    new M06PumpRoomReunionView(root.transform, null, _ => true));
                Assert.Throws<ArgumentNullException>(() =>
                    new M06PumpRoomReunionView(root.transform, session, null));
                view = new M06PumpRoomReunionView(root.transform, session, _ => true);
                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(aspect));
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.Narrative.text, Does.Contain("沈砾"));
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                Assert.That(view.Narrative.text, Does.Contain("Shen Li"));
                Assert.That(session.Stage, Is.EqualTo(M06ReunionStage.VesselBriefing));
                view.Dispose();
                Assert.DoesNotThrow(() => view.Refresh());
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(1f));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(language);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
