using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M10AssemblyViewHostTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedThroughM09(
            Dictionary<string, int> values,
            bool seedM08Notice = false)
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

            if (seedM08Notice)
            {
                var winch = new M08FirstWinchOrderSession();
                Assert.That(winch.TryAcquirePart(M08WinchPart.ScrapGear), Is.True);
                Assert.That(winch.TryAcquirePart(M08WinchPart.DockCable), Is.True);
                Assert.That(winch.TryAssemble(), Is.True);
                Assert.That(winch.TryTightenCable(), Is.True);
                Assert.That(winch.TryTestLift(), Is.True);
                Assert.That(M08WorkshopDepositFacts.TryConfirm(winch, read, write), Is.True);
                Assert.That(M08FirstWorldNoticeFacts.TryRecord(true, read, write), Is.True);
            }

            Assert.That(M09BerthTableFacts.TryRecord(new M09BerthTableRecord(
                totalBerths: 20,
                pendingNames: 23,
                M09BerthExchangeFlags.HanQiaoCivilianVoyage,
                M09RuiyaOutcome.Rescued,
                qiDebtDisclosedHere: false,
                shenReconciled: false), read, write), Is.True);
        }

        [Test]
        public void HostCannotOpenWithoutM09BerthTablePresenceOrCleanConfirmedKey()
        {
            var root = new GameObject("M10 host prerequisites");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            bool atAssembly = false;
            M10AssemblyHost host = null;
            try
            {
                host = new M10AssemblyHost(
                    root.transform, () => atAssembly, () => true, () => false, read, write);
                Assert.That(host.TryOpen(), Is.False);

                SeedThroughM09(values);
                Assert.That(host.TryOpen(), Is.False);

                atAssembly = true;
                values[M10AssemblyFacts.ConfirmedKey] = 2;
                Assert.That(host.TryOpen(), Is.False);

                values[M10AssemblyFacts.ConfirmedKey] = 0;
                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.ActiveView, Is.Not.Null);
                Assert.That(host.IsCompleted, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FullFlowHandlesMismatchRequeueArchiveCopyRC02RC05ContextAndCommitOnce()
        {
            var root = new GameObject("M10 host full flow");
            var values = new Dictionary<string, int>();
            SeedThroughM09(values, seedM08Notice: true);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(RC02ZhengYuanFacts.TryRecord(new RC02ZhengYuanRecord(
                RC02IntakeChecks.All, true, RC02Resolution.AllowByException,
                motherCalledShiLeng: false, shiLengConsented: false,
                realZhengYuanFound: false), read, write), Is.True);
            Assert.That(RC05ThirdSquadFacts.TryRecord(new RC05ThirdSquadRecord(
                trialDeliveryVerified: true, RC05Resolution.CivilianPardon,
                skirmishOccurred: false, realZhengYuanVerified: true,
                oldWomanStoneMentioned: false), read, write), Is.True);

            bool matched = false;
            M10AssemblyHost host = null;
            M10AssemblyHost duplicate = null;
            try
            {
                host = new M10AssemblyHost(
                    root.transform, () => true, () => matched, () => false, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;

                view.ObserveNoticeButton.onClick.Invoke();
                Assert.That(host.Session.Phase, Is.EqualTo(M10AssemblyPhase.ThreeRecordReconciliation));

                view.ReconcileRecordsButton.onClick.Invoke();
                Assert.That(host.Session.HadReconciliationDelay, Is.True);
                Assert.That(host.Session.Phase, Is.EqualTo(M10AssemblyPhase.ThreeRecordReconciliation));

                matched = true;
                view.ReconcileRecordsButton.onClick.Invoke();
                Assert.That(host.Session.Phase, Is.EqualTo(M10AssemblyPhase.HanQiaoConfrontation));
                Assert.That(view.PresentM02CopyButton.gameObject.activeSelf, Is.False);
                Assert.That(view.RetrieveArchiveCopyButton.gameObject.activeSelf, Is.True);

                view.RetrieveArchiveCopyButton.onClick.Invoke();
                Assert.That(view.Body.text, Does.Contain("RC-02"));
                view.TradeForConcessionsButton.onClick.Invoke();

                Assert.That(host.Session.Phase, Is.EqualTo(M10AssemblyPhase.SupplyContractTable));
                Assert.That(view.Body.text, Does.Contain("RC-05"));
                view.SignFullCoverageButton.onClick.Invoke();

                view.HearBellTowerReportButton.onClick.Invoke();
                view.ConfirmAssemblyButton.onClick.Invoke();

                Assert.That(host.IsCompleted, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(M10AssemblyFacts.TryRead(read, out M10AssemblyRecord? saved), Is.True);
                Assert.That(saved.Value.RosterAccuracy, Is.EqualTo(M10RosterAccuracy.DelayedRequeued));
                Assert.That(saved.Value.BattleReportSource, Is.EqualTo(M10BattleReportSource.RetrievedFromHanArchive));
                Assert.That(saved.Value.LeverageWeakenedByRC02, Is.True);
                Assert.That(saved.Value.CoversRC05MountainFamilies, Is.True);

                duplicate = new M10AssemblyHost(
                    root.transform, () => true, () => true, () => true, read, write);
                Assert.That(duplicate.TryOpen(), Is.False);
            }
            finally
            {
                duplicate?.Dispose();
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LateThreeRecordMismatchBlocksFinalAssemblyUntilRecordsAgreeAgain()
        {
            var root = new GameObject("M10 late three-record mismatch");
            var values = new Dictionary<string, int>();
            SeedThroughM09(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            bool recordsMatch = true;
            M10AssemblyHost host = null;
            try
            {
                host = new M10AssemblyHost(
                    root.transform, () => true, () => recordsMatch,
                    () => true, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                view.ObserveNoticeButton.onClick.Invoke();
                view.ReconcileRecordsButton.onClick.Invoke();
                Assert.That(host.Session.Phase,
                    Is.EqualTo(M10AssemblyPhase.HanQiaoConfrontation));
                view.PresentM02CopyButton.onClick.Invoke();
                view.KeepConfidentialButton.onClick.Invoke();
                view.RecordPartialGapButton.onClick.Invoke();
                view.HearBellTowerReportButton.onClick.Invoke();

                // The three sources agreed at Yi Lei's table but a live
                // arrival/roster update made them disagree before saving.
                recordsMatch = false;
                view.ConfirmAssemblyButton.onClick.Invoke();
                Assert.That(host.Session.IsCompleted, Is.False);
                Assert.That(host.ActiveView, Is.SameAs(view));
                Assert.That(read(M10AssemblyFacts.ConfirmedKey), Is.Zero,
                    "A stale name reconciliation must never be persisted.");

                // After the records are genuinely consistent again the
                // same dialogue may finish, without a duplicate write.
                recordsMatch = true;
                view.ConfirmAssemblyButton.onClick.Invoke();
                Assert.That(host.IsCompleted, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(read(M10AssemblyFacts.ConfirmedKey), Is.EqualTo(1));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InterruptedSaveAndCommitThenThrowRecoverWithoutDuplicateWrites()
        {
            var root = new GameObject("M10 host retry");
            var values = new Dictionary<string, int>();
            SeedThroughM09(values);
            Func<string, int> read = Reader(values);
            int preCommitFails = 0;
            int confirmCommits = 0;
            Action<string, int> unstable = (k, v) =>
            {
                if (k == M10AssemblyFacts.SupplyContractKey && preCommitFails++ == 0)
                    throw new InvalidOperationException("Interrupted before confirm");
                values[k] = v;
                if (k == M10AssemblyFacts.ConfirmedKey && v == 1 && ++confirmCommits == 1)
                    throw new InvalidOperationException("Lost ack after confirm");
            };
            M10AssemblyHost host = null;
            try
            {
                host = new M10AssemblyHost(
                    root.transform, () => true, () => true, () => true, read, unstable);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                view.ObserveNoticeButton.onClick.Invoke();
                view.ReconcileRecordsButton.onClick.Invoke();
                view.PresentM02CopyButton.onClick.Invoke();
                view.PublishDraftButton.onClick.Invoke();
                view.RecordPartialGapButton.onClick.Invoke();
                view.HearBellTowerReportButton.onClick.Invoke();

                Assert.Throws<InvalidOperationException>(() =>
                    view.ConfirmAssemblyButton.onClick.Invoke());
                Assert.That(host.IsCompleted, Is.False);
                Assert.That(host.ActiveView, Is.Not.Null);

                Assert.Throws<InvalidOperationException>(() =>
                    view.ConfirmAssemblyButton.onClick.Invoke());
                Assert.That(host.IsCompleted, Is.True);
                Assert.That(confirmCommits, Is.EqualTo(1));

                view.ConfirmAssemblyButton.onClick.Invoke();
                Assert.That(confirmCommits, Is.EqualTo(1));
                Assert.That(host.ActiveView, Is.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LocaleAspectPresenceWithdrawalAndDisposalGuardsWorkCleanly()
        {
            var root = new GameObject("M10 host guards");
            var values = new Dictionary<string, int>();
            SeedThroughM09(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            string prevLang = SoundwellUiLocalization.Language;
            bool atAssembly = true;
            M10AssemblyHost host = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                host = new M10AssemblyHost(
                    root.transform, () => atAssembly, () => true, () => true, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                Assert.That(view.Heading.text, Does.Contain("THIRD WORLD NOTICE"));
                Assert.That(view.Panel.anchorMin.y, Is.EqualTo(0f));
                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(aspect));

                view.ObserveNoticeButton.onClick.Invoke();
                view.ReconcileRecordsButton.onClick.Invoke();
                view.PresentM02CopyButton.onClick.Invoke();
                view.KeepConfidentialButton.onClick.Invoke();
                view.SignFullCoverageButton.onClick.Invoke();
                view.HearBellTowerReportButton.onClick.Invoke();
                var confirmBtn = view.ConfirmAssemblyButton;

                atAssembly = false;
                confirmBtn.onClick.Invoke();
                Assert.That(host.IsCompleted, Is.False);

                atAssembly = true;
                host.Dispose();
                confirmBtn.onClick.Invoke();
                Assert.That(host.IsDisposed, Is.True);
                Assert.That(host.IsCompleted, Is.False);
            }
            finally
            {
                host?.Dispose();
                SoundwellUiLocalization.SetLanguage(prevLang);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
