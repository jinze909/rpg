using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RC01ChenYanViewHostTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedM08RosterCasesAvailable(Dictionary<string, int> values)
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
            Assert.That(M08FirstWorldNoticeFacts.TryRecord(true, read, write), Is.True);
        }

        private static void ReachTwoHomesTalk(RC01ChenYanView view)
        {
            view.WristScarButton.onClick.Invoke();
            view.DoughTuneButton.onClick.Invoke();
            view.ShrineTabletButton.onClick.Invoke();
            view.ConfrontButton.onClick.Invoke();
        }

        [Test]
        public void HostCannotOpenWithoutM08NoticePresenceOrCleanConfirmedKey()
        {
            var root = new GameObject("RC01 host prerequisites");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            bool atFerry = false;
            RC01ChenYanHost host = null;
            try
            {
                host = new RC01ChenYanHost(root.transform, () => atFerry, read, write);
                Assert.That(host.TryOpen(), Is.False);

                SeedM08RosterCasesAvailable(values);
                Assert.That(host.TryOpen(), Is.False);

                atFerry = true;
                values[RC01ChenYanFacts.ConfirmedKey] = 2;
                Assert.That(host.TryOpen(), Is.False);

                values[RC01ChenYanFacts.ConfirmedKey] = 0;
                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.ActiveView, Is.Not.Null);
                Assert.That(host.IsResolved, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TornUnconfirmedRosterFactsCannotOpenFreshChenYanCase()
        {
            var root = new GameObject("RC01 torn roster outcome");
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            RC01ChenYanHost host = null;
            try
            {
                host = new RC01ChenYanHost(root.transform, () => true, read, write);
                foreach (string orphanedKey in new[]
                {
                    RC01ChenYanFacts.CluesMaskKey,
                    RC01ChenYanFacts.TalkOrderKey,
                    RC01ChenYanFacts.OutcomeKey,
                    RC01ChenYanFacts.LinSuNoteKey
                })
                {
                    values[orphanedKey] = 1;
                    Assert.That(host.TryOpen(), Is.False,
                        "A partially recorded roster choice must not reopen: " + orphanedKey);
                    Assert.That(host.ActiveView, Is.Null);
                    values[orphanedKey] = 0;
                }

                Assert.That(host.TryOpen(), Is.True,
                    "A new voyage with clean RC01 keys remains playable.");
                Assert.That(host.ActiveView, Is.Not.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LinSuFirstFlowShowsGuardedAtmosphereAndSettlesRestoreLivingOnce()
        {
            var root = new GameObject("RC01 host lin su first");
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            string prevLang = SoundwellUiLocalization.Language;
            RC01ChenYanHost host = null;
            RC01ChenYanHost duplicate = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                host = new RC01ChenYanHost(root.transform, () => true, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                ReachTwoHomesTalk(view);

                view.TalkLinSuButton.onClick.Invoke();
                Assert.That(view.Body.text, Does.Contain("紧绷防备"));
                view.TalkQiuniangButton.onClick.Invoke();
                view.RestoreLivingButton.onClick.Invoke();

                Assert.That(host.IsResolved, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(RC01ChenYanFacts.TryRead(read, out RC01ChenYanRecord? saved), Is.True);
                Assert.That(saved.Value.Atmosphere, Is.EqualTo(RC01TalkAtmosphere.GuardedWithQiuniang));
                Assert.That(saved.Value.Outcome, Is.EqualTo(RC01ChenYanOutcome.RestoreLivingName));

                duplicate = new RC01ChenYanHost(root.transform, () => true, read, write);
                Assert.That(duplicate.TryOpen(), Is.False);
            }
            finally
            {
                duplicate?.Dispose();
                host?.Dispose();
                SoundwellUiLocalization.SetLanguage(prevLang);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void QiuniangFirstFlowShowsCalmAtmosphereAndSettlesNewName()
        {
            var root = new GameObject("RC01 host qiuniang first");
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            string prevLang = SoundwellUiLocalization.Language;
            RC01ChenYanHost host = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                host = new RC01ChenYanHost(root.transform, () => true, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                ReachTwoHomesTalk(view);

                view.TalkQiuniangButton.onClick.Invoke();
                Assert.That(view.Body.text, Does.Contain("平静释然"));
                view.TalkLinSuButton.onClick.Invoke();
                view.NewReturneeNameButton.onClick.Invoke();

                Assert.That(host.IsResolved, Is.True);
                Assert.That(RC01ChenYanFacts.TryRead(read, out RC01ChenYanRecord? saved), Is.True);
                Assert.That(saved.Value.Atmosphere, Is.EqualTo(RC01TalkAtmosphere.CalmWithLinSu));
                Assert.That(saved.Value.Outcome, Is.EqualTo(RC01ChenYanOutcome.IssueNewReturneeName));
            }
            finally
            {
                host?.Dispose();
                SoundwellUiLocalization.SetLanguage(prevLang);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InterruptedSaveAndCommitThenThrowRecoverWithoutDuplicateWrites()
        {
            var root = new GameObject("RC01 host retry");
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values);
            Func<string, int> read = Reader(values);
            int preCommitFails = 0;
            int confirmCommits = 0;
            Action<string, int> unstable = (k, v) =>
            {
                if (k == RC01ChenYanFacts.OutcomeKey && preCommitFails++ == 0)
                    throw new InvalidOperationException("Interrupted before confirm");
                values[k] = v;
                if (k == RC01ChenYanFacts.ConfirmedKey && v == 1 && ++confirmCommits == 1)
                    throw new InvalidOperationException("Lost ack after confirm");
            };
            RC01ChenYanHost host = null;
            try
            {
                host = new RC01ChenYanHost(root.transform, () => true, read, unstable);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                ReachTwoHomesTalk(view);
                view.TalkLinSuButton.onClick.Invoke();
                view.TalkQiuniangButton.onClick.Invoke();

                Assert.Throws<InvalidOperationException>(() =>
                    view.KeepDeadButton.onClick.Invoke());
                Assert.That(host.IsResolved, Is.False);
                Assert.That(host.ActiveView, Is.Not.Null);

                Assert.Throws<InvalidOperationException>(() =>
                    view.KeepDeadButton.onClick.Invoke());
                Assert.That(host.IsResolved, Is.True);
                Assert.That(confirmCommits, Is.EqualTo(1));

                view.KeepDeadButton.onClick.Invoke();
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
            var root = new GameObject("RC01 host guards");
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            string prevLang = SoundwellUiLocalization.Language;
            bool atFerry = true;
            RC01ChenYanHost host = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                host = new RC01ChenYanHost(root.transform, () => atFerry, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                Assert.That(view.Heading.text, Does.Contain("WHITE REED FERRY"));
                Assert.That(view.Panel.anchorMin.y, Is.EqualTo(0f));
                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(aspect));

                ReachTwoHomesTalk(view);
                view.TalkLinSuButton.onClick.Invoke();
                view.TalkQiuniangButton.onClick.Invoke();
                var keepBtn = view.KeepDeadButton;

                atFerry = false;
                keepBtn.onClick.Invoke();
                Assert.That(host.IsResolved, Is.False);

                atFerry = true;
                host.Dispose();
                keepBtn.onClick.Invoke();
                Assert.That(host.IsDisposed, Is.True);
                Assert.That(host.IsResolved, Is.False);
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
