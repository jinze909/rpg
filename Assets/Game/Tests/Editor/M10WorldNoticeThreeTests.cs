using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M10WorldNoticeThreeTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        internal static void SeedThroughM09BerthTable(Dictionary<string, int> values)
        {
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;

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
            Assert.That(M06RescuePlanFacts.TryRecord(
                new M06ReunionRecord(
                    M06RescuePlan.OfficialHandover, true,
                    M06LetterDeliveryOutcome.NotCarried, true), read, write), Is.True);
            Assert.That(M07ExtractionFacts.TryRecord(new M07ExtractionRecord(
                M07ExecutedRoute.OfficialHandover,
                M07PumpShutdownState.KeptRunning,
                M07RouteCostFlags.HarborSupplyDebt,
                boardedFirstBatchCount: 10,
                lowerLevelEvacuatedCount: 0,
                M07RusFate.Arrested, M07LedgerState.Preserved,
                M07LiangChoice.StayOnIsland,
                M07YidaChoice.HandToInspector,
                taoJiRescued: false), read, write), Is.True);
            Assert.That(M09BerthTableFacts.TryRecord(new M09BerthTableRecord(
                totalBerths: 20,
                pendingNames: 23,
                M09BerthExchangeFlags.HanQiaoCivilianVoyage,
                M09RuiyaOutcome.Rescued,
                qiDebtDisclosedHere: false,
                shenReconciled: false), read, write), Is.True);
        }

        [Test]
        public void LinchaoNoticeIsRecordedBeforeAssemblyAndAssemblyResumesPastNotice()
        {
            var values = new Dictionary<string, int>();
            SeedThroughM09BerthTable(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            var root = new GameObject("M10 third notice flow test");
            string previousLanguage = SoundwellUiLocalization.Language;
            bool atLinchao = true;
            bool atJingwan = false;
            var context = new ReturnFinaleFlowContext
            {
                AtLinchaoFerry = () => atLinchao,
                AtJingwanAssembly = () => atJingwan,
                ThreeRecordsMatched = () => true,
                CarriesM02BattleReport = () => false
            };
            var flow = new ReturnFinaleFlowHost(root.transform, context, read, write);

            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(flow.EvaluateNextStage(), Is.EqualTo(ReturnFinaleStage.M10Assembly));
                Assert.That(flow.TryOpenNextStage(), Is.True);
                Assert.That(flow.ActiveM10Host, Is.Not.Null);
                var notice = flow.ActiveM10Host.ActiveWorldNoticeView;
                Assert.That(notice, Is.Not.Null);
                StringAssert.Contains("陆钧", notice.Notice.text);

                // A hidden or interaction-locked notice must not persist acknowledgement.
                var group = notice.Panel.gameObject.AddComponent<CanvasGroup>();
                group.interactable = false;
                Assert.That(notice.AcknowledgeButton.IsInteractable(), Is.False);
                notice.AcknowledgeButton.onClick.Invoke();
                Assert.That(M10WorldNoticeThreeFacts.TryReadForCurrentArrival(
                    read, out bool lockedObserved), Is.True);
                Assert.That(lockedObserved, Is.False);
                Assert.That(flow.HasActiveHost, Is.True);
                Assert.That(notice.IsDelivered, Is.False);

                group.interactable = true;
                notice.Panel.gameObject.SetActive(false);
                notice.AcknowledgeButton.onClick.Invoke();
                Assert.That(M10WorldNoticeThreeFacts.TryReadForCurrentArrival(
                    read, out bool hiddenObserved), Is.True);
                Assert.That(hiddenObserved, Is.False);
                Assert.That(flow.HasActiveHost, Is.True);
                Assert.That(notice.IsDelivered, Is.False);

                notice.Panel.gameObject.SetActive(true);
                notice.AcknowledgeButton.onClick.Invoke();
                Assert.That(M10WorldNoticeThreeFacts.TryReadForCurrentArrival(
                    read, out bool observed), Is.True);
                Assert.That(observed, Is.True);
                Assert.That(flow.HasActiveHost, Is.False);

                atLinchao = false;
                atJingwan = true;
                Assert.That(flow.TryOpenNextStage(), Is.True);
                Assert.That(flow.ActiveM10Host.Session.Phase,
                    Is.EqualTo(M10AssemblyPhase.ThreeRecordReconciliation));
            }
            finally
            {
                flow.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void StaleArrivalCannotAcknowledgeTheOpenNotice()
        {
            var values = new Dictionary<string, int>();
            SeedThroughM09BerthTable(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            var root = new GameObject("M10 stale notice test");
            bool atLinchao = true;
            var host = new M10AssemblyHost(
                root.transform, () => false, () => true, () => false,
                read, write, () => atLinchao);

            try
            {
                Assert.That(host.TryOpen(), Is.True);
                var notice = host.ActiveWorldNoticeView;
                Assert.That(notice, Is.Not.Null);
                values[M05LandingFacts.ArrivalSequenceKey] =
                    values[M05LandingFacts.ArrivalSequenceKey] + 1;

                notice.AcknowledgeButton.onClick.Invoke();
                Assert.That(M10WorldNoticeThreeFacts.TryReadForCurrentArrival(
                    read, out bool observed), Is.True);
                Assert.That(observed, Is.False);
                Assert.That(host.ActiveWorldNoticeView, Is.Not.Null);
            }
            finally
            {
                host.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ConfirmedWriteLostAcknowledgementIsIdempotentlyRecovered()
        {
            var values = new Dictionary<string, int>();
            SeedThroughM09BerthTable(values);
            Func<string, int> read = Reader(values);
            int lostAcknowledgements = 0;
            Action<string, int> unstable = (key, value) =>
            {
                values[key] = value;
                if (key == M10WorldNoticeThreeFacts.ConfirmedKey &&
                    value == 1 && lostAcknowledgements++ == 0)
                    throw new InvalidOperationException("Lost acknowledgement after notice commit");
            };

            Assert.Throws<InvalidOperationException>(() =>
                M10WorldNoticeThreeFacts.TryRecord(read, unstable));
            Assert.That(M10WorldNoticeThreeFacts.TryReadForCurrentArrival(
                read, out bool observed), Is.True);
            Assert.That(observed, Is.True);
            Assert.That(M10WorldNoticeThreeFacts.TryRecord(
                read, unstable), Is.True);
        }

        [Test]
        public void ResetInvalidatesNoticeBeforeClearingItsVoyageIdentity()
        {
            var values = new Dictionary<string, int>();
            SeedThroughM09BerthTable(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            Assert.That(M10WorldNoticeThreeFacts.TryRecord(read, write), Is.True);

            Assert.That(M10WorldNoticeThreeFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(M10WorldNoticeThreeFacts.TryReadForCurrentArrival(
                read, out bool observed), Is.True);
            Assert.That(observed, Is.False);
            Assert.That(values[M10WorldNoticeThreeFacts.ConfirmedKey], Is.Zero);
            Assert.That(values[M10WorldNoticeThreeFacts.ArrivalIdentityKey], Is.Zero);
        }
    }
}