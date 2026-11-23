using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ST06TaoJiHomecomingHostTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedM07(Dictionary<string, int> values, bool taoJiRescued)
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
                boardedFirstBatchCount: 300,
                lowerLevelEvacuatedCount: 0,
                M07RusFate.Arrested, M07LedgerState.Preserved,
                M07LiangChoice.StayOnIsland,
                M07YidaChoice.HandToInspector,
                taoJiRescued: taoJiRescued), read, write), Is.True);
        }

        [Test]
        public void HostCannotOpenWithoutM07TaoJiRescuedPresenceOrCleanConfirmedKey()
        {
            var root = new GameObject("ST06 host prerequisites");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            bool present = true;
            ST06TaoJiHomecomingHost host = null;
            try
            {
                host = new ST06TaoJiHomecomingHost(
                    root.transform, () => present, read, write);
                Assert.That(host.TryOpen(), Is.False);

                SeedM07(values, taoJiRescued: false);
                Assert.That(host.TryOpen(), Is.False);

                values.Clear();
                SeedM07(values, taoJiRescued: true);
                present = false;
                Assert.That(host.TryOpen(), Is.False);

                present = true;
                values[ST06TaoJiHomecomingFacts.ConfirmedKey] = 2;
                Assert.That(host.TryOpen(), Is.False);

                values[ST06TaoJiHomecomingFacts.ConfirmedKey] = 0;
                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.ActiveView, Is.Not.Null);
                Assert.That(host.IsSettled, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CoverUpRemediationAndWelcomeDinnerSettleOnceAndBlockReopen()
        {
            var root = new GameObject("ST06 host settle");
            var values = new Dictionary<string, int>();
            SeedM07(values, taoJiRescued: true);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            ST06TaoJiHomecomingHost host = null;
            ST06TaoJiHomecomingHost duplicate = null;
            try
            {
                host = new ST06TaoJiHomecomingHost(
                    root.transform, () => true, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                view.CoverUpButton.onClick.Invoke();
                view.RemediateCoverUpButton.onClick.Invoke();
                view.HoldWelcomeDinnerButton.onClick.Invoke();
                view.SettleButton.onClick.Invoke();

                Assert.That(host.IsSettled, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(ST06TaoJiHomecomingFacts.TryRead(
                    read, out ST06HomecomingRecord? saved), Is.True);
                Assert.That(saved.HasValue, Is.True);
                Assert.That(saved.Value.ChairResolution, Is.EqualTo(ST06ChairResolution.CoverUp));
                Assert.That(saved.Value.CoverUpRemediated, Is.True);
                Assert.That(saved.Value.WelcomeDinnerHeld, Is.True);

                duplicate = new ST06TaoJiHomecomingHost(
                    root.transform, () => true, read, write);
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
        public void InterruptedWriteAndCommitThenThrowRecoverWithoutDuplicateWrites()
        {
            var root = new GameObject("ST06 host retry");
            var values = new Dictionary<string, int>();
            SeedM07(values, taoJiRescued: true);
            Func<string, int> read = Reader(values);
            int preCommitFails = 0;
            int confirmCommits = 0;
            Action<string, int> unstable = (k, v) =>
            {
                if (k == ST06TaoJiHomecomingFacts.ChairResolutionKey && preCommitFails++ == 0)
                    throw new InvalidOperationException("Interrupted before confirm");
                values[k] = v;
                if (k == ST06TaoJiHomecomingFacts.ConfirmedKey && v == 1 && ++confirmCommits == 1)
                    throw new InvalidOperationException("Lost ack after confirm");
            };
            ST06TaoJiHomecomingHost host = null;
            try
            {
                host = new ST06TaoJiHomecomingHost(
                    root.transform, () => true, read, unstable);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                view.TeachJointButton.onClick.Invoke();

                Assert.Throws<InvalidOperationException>(() =>
                    view.SettleButton.onClick.Invoke());
                Assert.That(host.IsSettled, Is.False);
                Assert.That(host.ActiveView, Is.Not.Null);

                Assert.Throws<InvalidOperationException>(() =>
                    view.SettleButton.onClick.Invoke());
                Assert.That(host.IsSettled, Is.True);
                Assert.That(confirmCommits, Is.EqualTo(1));

                view.SettleButton.onClick.Invoke();
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
        public void ExternalSameChairButDifferentConsequencesNeverAcknowledgesThisSession()
        {
            var root = new GameObject("ST06 conflicting acknowledgement");
            var values = new Dictionary<string, int>();
            SeedM07(values, taoJiRescued: true);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            ST06TaoJiHomecomingHost host = null;
            try
            {
                host = new ST06TaoJiHomecomingHost(
                    root.transform, () => true, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                view.CoverUpButton.onClick.Invoke();
                view.RemediateCoverUpButton.onClick.Invoke();
                view.HoldWelcomeDinnerButton.onClick.Invoke();
                host.Session.SetPostReturnLetterProgress(
                    taoJiTraveledFar: true,
                    letterStatus: ST06LetterStatus.Delivered,
                    marketTicketEnclosed: true);

                // A different writer finishes a record sharing only the chair
                // choice. The open view must not acknowledge those other facts
                // as this session's decisions or close as if they were saved.
                var other = new ST06HomecomingRecord(
                    ST06ChairResolution.CoverUp,
                    coverUpRemediated: false,
                    welcomeDinnerHeld: false,
                    taoJiTraveledFar: false,
                    letterStatus: ST06LetterStatus.NotSent,
                    marketTicketEnclosed: false);
                Assert.That(ST06TaoJiHomecomingFacts.TryRecord(other, read, write),
                    Is.True);

                view.SettleButton.onClick.Invoke();
                Assert.That(host.ActiveView, Is.SameAs(view));
                Assert.That(host.Session.IsSettled, Is.False);
                Assert.That(host.IsSettled, Is.True);
                Assert.That(ST06TaoJiHomecomingFacts.TryRead(
                    read, out ST06HomecomingRecord? persisted), Is.True);
                Assert.That(persisted.HasValue, Is.True);
                Assert.That(persisted.Value.CoverUpRemediated, Is.False);
                Assert.That(persisted.Value.WelcomeDinnerHeld, Is.False);
                Assert.That(persisted.Value.LetterStatus,
                    Is.EqualTo(ST06LetterStatus.NotSent));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PresenceWithdrawalAtCommitTimeBlocksWriteUntilTaoJiReturns()
        {
            var root = new GameObject("ST06 host presence");
            var values = new Dictionary<string, int>();
            SeedM07(values, taoJiRescued: true);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            bool present = true;
            ST06TaoJiHomecomingHost host = null;
            try
            {
                host = new ST06TaoJiHomecomingHost(
                    root.transform, () => present, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                view.TaoJiExplainsButton.onClick.Invoke();

                present = false;
                view.SettleButton.onClick.Invoke();
                Assert.That(host.IsSettled, Is.False);
                Assert.That(host.ActiveView, Is.Not.Null);

                present = true;
                view.SettleButton.onClick.Invoke();
                Assert.That(host.IsSettled, Is.True);
                Assert.That(host.ActiveView, Is.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DisposalCleansUpViewAndIgnoresStaleButtonClicks()
        {
            var root = new GameObject("ST06 host dispose");
            var values = new Dictionary<string, int>();
            SeedM07(values, taoJiRescued: true);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            ST06TaoJiHomecomingHost host = null;
            try
            {
                host = new ST06TaoJiHomecomingHost(
                    root.transform, () => true, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                view.RefundRebuildButton.onClick.Invoke();
                var settleBtn = view.SettleButton;

                host.Dispose();
                settleBtn.onClick.Invoke();
                Assert.That(host.IsDisposed, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(host.IsSettled, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
