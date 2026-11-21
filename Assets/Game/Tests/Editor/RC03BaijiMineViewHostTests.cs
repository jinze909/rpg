using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RC03BaijiMineViewHostTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedM08RosterCasesAvailable(
            Dictionary<string, int> values,
            M06RescuePlan plan = M06RescuePlan.OfficialHandover)
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
                    plan, true, M06LetterDeliveryOutcome.NotCarried, true), read, write), Is.True);

            bool shutdown = plan == M06RescuePlan.MinerShutdown;
            Assert.That(M07ExtractionFacts.TryRecord(new M07ExtractionRecord(
                shutdown ? M07ExecutedRoute.MinerShutdown : M07ExecutedRoute.OfficialHandover,
                shutdown ? M07PumpShutdownState.SafelyStoppedThreeHours : M07PumpShutdownState.KeptRunning,
                M07ExtractionFacts.RequiredCostForPlan(plan),
                boardedFirstBatchCount: 10,
                lowerLevelEvacuatedCount: shutdown ? M07ExtractionFacts.LowerLevelMinerCount : 0,
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

        private static void CompleteIslandVisits(RC03BaijiMineView view)
        {
            view.CanteenVisitButton.onClick.Invoke();
            view.PumpRoomVisitButton.onClick.Invoke();
            view.CharterTalksButton.onClick.Invoke();
        }

        [Test]
        public void HostCannotOpenWithoutM08NoticePresenceOrCleanConfirmedKey()
        {
            var root = new GameObject("RC03 host prerequisites");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            bool onIsland = false;
            RC03BaijiMineHost host = null;
            try
            {
                host = new RC03BaijiMineHost(
                    root.transform, () => onIsland, () => true, () => true, () => true, read, write);
                Assert.That(host.TryOpen(), Is.False);

                SeedM08RosterCasesAvailable(values);
                Assert.That(host.TryOpen(), Is.False);

                onIsland = true;
                values[RC03BaijiMineFacts.ConfirmedKey] = 2;
                Assert.That(host.TryOpen(), Is.False);

                values[RC03BaijiMineFacts.ConfirmedKey] = 0;
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
        public void IncompleteCharterIntentBlocksFreshIslandEncounter()
        {
            var root = new GameObject("RC03 torn charter intent");
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            RC03BaijiMineHost host = null;
            try
            {
                host = new RC03BaijiMineHost(root.transform,
                    () => true, () => true, () => true, () => true, read, write);
                values[RC03BaijiMineFacts.PendingSignatureKey] = 13000;
                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.ActiveView, Is.Null);
                values[RC03BaijiMineFacts.PendingSignatureKey] = 0;
                Assert.That(host.TryOpen(), Is.True);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AmendCharterUnlocksOnlyAfterDiagramAndTrainingAndPersistsOnce()
        {
            var root = new GameObject("RC03 host amend");
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values, M06RescuePlan.MinerShutdown);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            RC03BaijiMineHost host = null;
            RC03BaijiMineHost duplicate = null;
            try
            {
                host = new RC03BaijiMineHost(
                    root.transform, () => true, () => true, () => true, () => false, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                CompleteIslandVisits(view);

                Assert.That(view.AmendCharterButton.interactable, Is.False);
                Assert.That(view.SecureTrainingButton.interactable, Is.False);
                Assert.That(view.StayOneYearButton.interactable, Is.False);

                view.VerifyDiagramButton.onClick.Invoke();
                Assert.That(view.SecureTrainingButton.interactable, Is.True);
                Assert.That(view.AmendCharterButton.interactable, Is.False);

                view.SecureTrainingButton.onClick.Invoke();
                Assert.That(view.AmendCharterButton.interactable, Is.True);

                view.AmendCharterButton.onClick.Invoke();
                Assert.That(host.IsResolved, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(RC03BaijiMineFacts.TryRead(read, out RC03BaijiMineRecord? saved), Is.True);
                Assert.That(saved.Value.Resolution, Is.EqualTo(RC03Resolution.AmendCharterWithSuccessors));
                Assert.That(saved.Value.LanRuoQueuedForNextBatch, Is.True);

                duplicate = new RC03BaijiMineHost(
                    root.transform, () => true, () => true, () => true, () => true, read, write);
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
        public void StayOneYearRequiresExplicitLanRuoConsentInView()
        {
            var root = new GameObject("RC03 host stay");
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            bool lanRuoAgrees = false;
            RC03BaijiMineHost host = null;
            try
            {
                host = new RC03BaijiMineHost(
                    root.transform, () => true, () => false, () => false, () => lanRuoAgrees, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                CompleteIslandVisits(view);

                view.AskLanRuoStayButton.onClick.Invoke();
                Assert.That(view.StayOneYearButton.interactable, Is.False);

                lanRuoAgrees = true;
                view.AskLanRuoStayButton.onClick.Invoke();
                Assert.That(view.StayOneYearButton.interactable, Is.True);

                view.StayOneYearButton.onClick.Invoke();
                Assert.That(host.IsResolved, Is.True);
                Assert.That(RC03BaijiMineFacts.TryRead(read, out RC03BaijiMineRecord? saved), Is.True);
                Assert.That(saved.Value.Resolution, Is.EqualTo(RC03Resolution.StayOneYearByConsent));
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
            var root = new GameObject("RC03 host retry");
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values);
            Func<string, int> read = Reader(values);
            int preCommitFails = 0;
            int confirmCommits = 0;
            Action<string, int> unstable = (k, v) =>
            {
                if (k == RC03BaijiMineFacts.ResolutionKey && preCommitFails++ == 0)
                    throw new InvalidOperationException("Interrupted before confirm");
                values[k] = v;
                if (k == RC03BaijiMineFacts.ConfirmedKey && v == 1 && ++confirmCommits == 1)
                    throw new InvalidOperationException("Lost ack after confirm");
            };
            RC03BaijiMineHost host = null;
            try
            {
                host = new RC03BaijiMineHost(
                    root.transform, () => true, () => false, () => false, () => false, read, unstable);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                CompleteIslandVisits(view);

                Assert.Throws<InvalidOperationException>(() =>
                    view.SecretEvacuateButton.onClick.Invoke());
                Assert.That(host.IsResolved, Is.False);
                Assert.That(host.ActiveView, Is.Not.Null);

                Assert.Throws<InvalidOperationException>(() =>
                    view.SecretEvacuateButton.onClick.Invoke());
                Assert.That(host.IsResolved, Is.True);
                Assert.That(confirmCommits, Is.EqualTo(1));

                view.SecretEvacuateButton.onClick.Invoke();
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
            var root = new GameObject("RC03 host guards");
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            string prevLang = SoundwellUiLocalization.Language;
            bool onIsland = true;
            RC03BaijiMineHost host = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                host = new RC03BaijiMineHost(
                    root.transform, () => onIsland, () => false, () => false, () => false, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                Assert.That(view.Heading.text, Does.Contain("BAIJI ISLAND VISITS"));
                Assert.That(view.Panel.anchorMin.y, Is.EqualTo(0f));
                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(aspect));

                CompleteIslandVisits(view);
                var evacBtn = view.SecretEvacuateButton;

                onIsland = false;
                evacBtn.onClick.Invoke();
                Assert.That(host.IsResolved, Is.False);

                onIsland = true;
                host.Dispose();
                evacBtn.onClick.Invoke();
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
