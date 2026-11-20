using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RC04DingYueReefHostTests
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

        [Test]
        public void HostCannotOpenBeforeM08FirstNoticeOrWhenConfirmedKeyNonZero()
        {
            var root = new GameObject("RC04 host prerequisites");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            RC04DingYueReefHost host = null;
            try
            {
                host = new RC04DingYueReefHost(
                    root.transform, () => true, _ => true, read, write);
                Assert.That(host.TryOpenListening(), Is.False);
                Assert.That(host.ActiveListeningView, Is.Null);

                SeedM08RosterCasesAvailable(values);
                values[RC04DingYueReefFacts.ConfirmedKey] = 2;
                Assert.That(host.TryOpenListening(), Is.False);

                values[RC04DingYueReefFacts.ConfirmedKey] = 0;
                Assert.That(host.TryOpenListening(), Is.True);
                Assert.That(host.TryOpenListening(), Is.False);
                Assert.That(host.ActiveListeningView, Is.Not.Null);
                Assert.That(host.ActiveResolutionView, Is.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ListeningHandoffRequiresVerifiedApproachAndTransitionsToResolutionView()
        {
            var root = new GameObject("RC04 host handoff");
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            bool boatReady = false;
            RC04DingYueReefHost host = null;
            try
            {
                host = new RC04DingYueReefHost(
                    root.transform, () => boatReady, _ => true, read, write);
                Assert.That(host.TryOpenListening(), Is.True);
                var listening = host.ActiveListeningView;
                for (int i = 0; i < 3; i++)
                    listening.SlowEchoButton.onClick.Invoke();

                listening.ApproachButton.onClick.Invoke();
                Assert.That(host.ApproachConfirmed, Is.False);
                Assert.That(host.ActiveListeningView, Is.Not.Null);
                Assert.That(host.ActiveResolutionView, Is.Null);

                boatReady = true;
                listening.ApproachButton.onClick.Invoke();
                Assert.That(host.ApproachConfirmed, Is.True);
                Assert.That(host.ActiveListeningView, Is.Null);
                Assert.That(host.ActiveResolutionView, Is.Not.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EndToEndDecoyAmbushPersistsOnceAndBlocksReopen()
        {
            var root = new GameObject("RC04 host decoy");
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            RC04DingYueReefHost host = null;
            RC04DingYueReefHost duplicate = null;
            try
            {
                host = new RC04DingYueReefHost(
                    root.transform,
                    () => true,
                    res => res == RC04Resolution.KillMimicBeastWithPreparedDecoy,
                    read,
                    write);
                Assert.That(host.TryOpenListening(), Is.True);
                for (int i = 0; i < 3; i++)
                    host.ActiveListeningView.SlowEchoButton.onClick.Invoke();
                host.ActiveListeningView.ApproachButton.onClick.Invoke();

                Assert.That(host.Session.TryPrepareConsentedDecoyAmbush(
                    true, true, true, true), Is.True);
                Assert.That(host.Session.TryConfirmDecoyBeastDefeat(true), Is.True);
                host.ActiveResolutionView.Refresh();
                Assert.That(host.ActiveResolutionView.DecoyKillButton.interactable, Is.True);

                host.ActiveResolutionView.DecoyKillButton.onClick.Invoke();
                Assert.That(host.IsResolved, Is.True);
                Assert.That(host.ActiveResolutionView, Is.Null);
                Assert.That(RC04DingYueReefFacts.TryRead(read, out RC04ReefRecord? saved), Is.True);
                Assert.That(saved.HasValue, Is.True);
                Assert.That(saved.Value.Resolution,
                    Is.EqualTo(RC04Resolution.KillMimicBeastWithPreparedDecoy));

                duplicate = new RC04DingYueReefHost(
                    root.transform, () => true, _ => true, read, write);
                Assert.That(duplicate.TryOpenListening(), Is.False);
            }
            finally
            {
                duplicate?.Dispose();
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InterruptedSaveAndCommitThenThrowRecoverWithoutDuplicateWrites()
        {
            var root = new GameObject("RC04 host retry");
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values);
            Func<string, int> read = Reader(values);
            int preCommitFails = 0;
            int confirmCommits = 0;
            Action<string, int> unstable = (k, v) =>
            {
                if (k == RC04DingYueReefFacts.ResolutionKey && preCommitFails++ == 0)
                    throw new InvalidOperationException("Interrupted before confirm");
                values[k] = v;
                if (k == RC04DingYueReefFacts.ConfirmedKey && v == 1 && ++confirmCommits == 1)
                    throw new InvalidOperationException("Lost ack after confirm");
            };
            RC04DingYueReefHost host = null;
            try
            {
                host = new RC04DingYueReefHost(
                    root.transform, () => true, _ => true, read, unstable);
                Assert.That(host.TryOpenListening(), Is.True);
                for (int i = 0; i < 3; i++)
                    host.ActiveListeningView.SlowEchoButton.onClick.Invoke();
                host.ActiveListeningView.ApproachButton.onClick.Invoke();

                var resolutionView = host.ActiveResolutionView;
                Assert.Throws<InvalidOperationException>(() =>
                    resolutionView.BringDingYueButton.onClick.Invoke());
                Assert.That(host.IsResolved, Is.False);
                Assert.That(host.ActiveResolutionView, Is.Not.Null);

                Assert.Throws<InvalidOperationException>(() =>
                    resolutionView.BringDingYueButton.onClick.Invoke());
                Assert.That(host.IsResolved, Is.True);
                Assert.That(confirmCommits, Is.EqualTo(1));

                resolutionView.BringDingYueButton.onClick.Invoke();
                Assert.That(confirmCommits, Is.EqualTo(1));
                Assert.That(host.ActiveResolutionView, Is.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MidFlowConditionRevocationAndDisposePreventUnverifiedCommits()
        {
            var root = new GameObject("RC04 host guards");
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            bool patrolReady = true;
            RC04DingYueReefHost host = null;
            try
            {
                host = new RC04DingYueReefHost(
                    root.transform,
                    () => true,
                    res => res == RC04Resolution.LeaveWithSignalBeacon && patrolReady,
                    read,
                    write);
                Assert.That(host.TryOpenListening(), Is.True);
                for (int i = 0; i < 3; i++)
                    host.ActiveListeningView.SlowEchoButton.onClick.Invoke();
                host.ActiveListeningView.ApproachButton.onClick.Invoke();

                var beaconButton = host.ActiveResolutionView.BeaconButton;
                patrolReady = false;
                beaconButton.onClick.Invoke();
                Assert.That(host.IsResolved, Is.False);

                patrolReady = true;
                host.ActiveResolutionView.Refresh();
                host.Dispose();
                beaconButton.onClick.Invoke();
                Assert.That(host.IsDisposed, Is.True);
                Assert.That(host.IsResolved, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
