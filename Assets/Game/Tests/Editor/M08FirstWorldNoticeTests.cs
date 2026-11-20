using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M08FirstWorldNoticeTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int v) ? v : 0;
        }

        private static void SeedVerifiedM08Winch(Dictionary<string, int> values)
        {
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
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

            var winch = new M08FirstWinchOrderSession();
            Assert.That(winch.TryAcquirePart(M08WinchPart.ScrapGear), Is.True);
            Assert.That(winch.TryAcquirePart(M08WinchPart.DockCable), Is.True);
            Assert.That(winch.TryAssemble(), Is.True);
            Assert.That(winch.TryTightenCable(), Is.True);
            Assert.That(winch.TryTestLift(), Is.True);
            Assert.That(M08WorkshopDepositFacts.TryConfirm(winch, read, write), Is.True);
        }

        [Test]
        public void FirstNoticeCannotAppearBeforePaidWinchAndObservedRealDawn()
        {
            var root = new GameObject("M08 first notice prerequisites");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            bool dawn = true;
            M08FirstWorldNoticeHost host = null;
            try
            {
                host = new M08FirstWorldNoticeHost(
                    root.transform, () => dawn, read, write);
                Assert.That(host.TryOpen(), Is.False);
                Assert.That(M08FirstWorldNoticeFacts.TryRecord(true, read, write), Is.False);
                Assert.That(values, Is.Empty);
                SeedVerifiedM08Winch(values);
                dawn = false;
                Assert.That(host.TryOpen(), Is.False);
                Assert.That(M08FirstWorldNoticeFacts.TryRecord(false, read, write), Is.False);
                dawn = true;
                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.NoticeRead, Is.False);
                for (int i = 1; i <= 5; i++)
                    Assert.That(host.IsCaseAvailable(i), Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ReadingRealDawnNoticeOpensFiveCasesWithoutInventingOutcomes()
        {
            var root = new GameObject("M08 first notice cases");
            var values = new Dictionary<string, int>();
            SeedVerifiedM08Winch(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            string language = SoundwellUiLocalization.Language;
            M08FirstWorldNoticeHost host = null;
            M08FirstWorldNoticeHost duplicate = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                host = new M08FirstWorldNoticeHost(root.transform, () => true, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                Assert.That(view.Notice.text, Does.Contain("回澜关加收通行税"));
                Assert.That(view.CaseList.text, Does.Contain("RC-01"));
                Assert.That(view.CaseList.text, Does.Contain("RC-05"));
                Assert.That(view.CaseList.text, Does.Contain("开放≠获救"));
                Assert.That(view.Panel.anchorMin.y, Is.EqualTo(0f));
                Assert.That(view.AcknowledgeButton.interactable, Is.True);
                view.AcknowledgeButton.onClick.Invoke();

                Assert.That(host.NoticeRead, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(values[M08FirstWorldNoticeFacts.ConfirmedKey], Is.EqualTo(1));
                for (int i = 1; i <= 5; i++)
                    Assert.That(host.IsCaseAvailable(i), Is.True,
                        "Availability is not a case resolution.");
                Assert.That(host.IsCaseAvailable(0), Is.False);
                Assert.That(host.IsCaseAvailable(6), Is.False);
                Assert.That(M08FirstWorldNoticeFacts.TryCaseAvailable(
                    6, read, out bool invalid), Is.False);
                Assert.That(invalid, Is.False);

                Assert.That(values.ContainsKey(RC04DingYueReefFacts.ConfirmedKey), Is.False);
                Assert.That(values.ContainsKey(M09BerthTableFacts.ConfirmedKey), Is.False);
                duplicate = new M08FirstWorldNoticeHost(
                    root.transform, () => true, read, write);
                Assert.That(duplicate.TryOpen(), Is.False);
                Assert.That(M08FirstWorldNoticeFacts.TryRecord(true, read, write), Is.False);
            }
            finally
            {
                duplicate?.Dispose();
                host?.Dispose();
                SoundwellUiLocalization.SetLanguage(language);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RealTimeDaybreakWithdrawalAndInterruptedSaveBothRetry()
        {
            var root = new GameObject("M08 first notice interrupted");
            var values = new Dictionary<string, int>();
            SeedVerifiedM08Winch(values);
            Func<string, int> read = Reader(values);
            bool dawn = true;
            int attempts = 0;
            Action<string, int> unstable = (k, v) =>
            {
                if (k == M08FirstWorldNoticeFacts.ConfirmedKey && attempts++ == 0)
                    throw new InvalidOperationException("Interrupted notice save");
                values[k] = v;
            };
            M08FirstWorldNoticeHost host = null;
            try
            {
                host = new M08FirstWorldNoticeHost(
                    root.transform, () => dawn, read, unstable);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                dawn = false;
                view.AcknowledgeButton.onClick.Invoke();
                Assert.That(host.NoticeRead, Is.False);
                Assert.That(attempts, Is.Zero);
                dawn = true;
                Assert.Throws<InvalidOperationException>(() =>
                    view.AcknowledgeButton.onClick.Invoke());
                Assert.That(view.AcknowledgeButton.interactable, Is.True);
                Assert.That(host.NoticeRead, Is.False);
                view.AcknowledgeButton.onClick.Invoke();
                Assert.That(host.NoticeRead, Is.True);
                Assert.That(attempts, Is.EqualTo(2));
                Assert.That(host.ActiveView, Is.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CommittedThenThrewIsAcknowledgedWithoutDuplicateNotices()
        {
            var root = new GameObject("M08 first notice lost ack");
            var values = new Dictionary<string, int>();
            SeedVerifiedM08Winch(values);
            Func<string, int> read = Reader(values);
            int writes = 0;
            Action<string, int> lostAck = (k, v) =>
            {
                values[k] = v;
                if (k == M08FirstWorldNoticeFacts.ConfirmedKey && ++writes == 1)
                    throw new InvalidOperationException("Acknowledgement lost");
            };
            M08FirstWorldNoticeHost host = null;
            try
            {
                host = new M08FirstWorldNoticeHost(
                    root.transform, () => true, read, lostAck);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                Assert.Throws<InvalidOperationException>(() =>
                    view.AcknowledgeButton.onClick.Invoke());
                Assert.That(host.NoticeRead, Is.True);
                view.AcknowledgeButton.onClick.Invoke();
                Assert.That(writes, Is.EqualTo(1));
                Assert.That(host.ActiveView, Is.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InvalidOrPreviousVoyageFactNeverOpensRosterCases()
        {
            var values = new Dictionary<string, int>();
            SeedVerifiedM08Winch(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            Assert.That(M08FirstWorldNoticeFacts.TryRecord(true, read, write), Is.True);
            values[M08FirstWorldNoticeFacts.ConfirmedKey] = 2;
            Assert.That(M08FirstWorldNoticeFacts.TryRead(read, out bool readBack), Is.True);
            Assert.That(readBack, Is.False);
            Assert.That(M08FirstWorldNoticeFacts.TryCaseAvailable(
                1, read, out bool available), Is.True);
            Assert.That(available, Is.False);

            values[M08FirstWorldNoticeFacts.ConfirmedKey] = 1;
            Assert.That(M07ExtractionFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(M08FirstWorldNoticeFacts.TryRead(read, out readBack), Is.True);
            Assert.That(readBack, Is.False);
            Assert.That(M08FirstWorldNoticeFacts.TryCaseAvailable(
                1, read, out available), Is.True);
            Assert.That(available, Is.False);

            Assert.That(M08FirstWorldNoticeFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(values[M08FirstWorldNoticeFacts.ConfirmedKey], Is.Zero);
            Assert.That(M08FirstWorldNoticeFacts.TryResetForNewVoyage(null), Is.False);
            Assert.That(M08FirstWorldNoticeFacts.TryRead(null, out readBack), Is.False);
        }

        [Test]
        public void EnglishNoticeCanSwitchToChineseAndDisposeWithoutAwardingAnything()
        {
            var root = new GameObject("M08 first notice localization");
            var values = new Dictionary<string, int>();
            SeedVerifiedM08Winch(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            string language = SoundwellUiLocalization.Language;
            M08FirstWorldNoticeHost host = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                host = new M08FirstWorldNoticeHost(
                    root.transform, () => true, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                Assert.That(view.Notice.text, Does.Contain("Huilan Pass"));
                Assert.That(view.CaseList.text, Does.Contain("RC-04"));
                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(aspect));
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.Notice.text, Does.Contain("祁雀"));
                var button = view.AcknowledgeButton;
                host.Dispose();
                button.onClick.Invoke();
                Assert.That(M08FirstWorldNoticeFacts.TryRead(read, out bool seen), Is.True);
                Assert.That(seen, Is.False);
            }
            finally
            {
                host?.Dispose();
                SoundwellUiLocalization.SetLanguage(language);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
