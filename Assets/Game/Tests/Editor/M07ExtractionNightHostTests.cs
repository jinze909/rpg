using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M07ExtractionNightHostTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedVerifiedM06(
            Dictionary<string, int> values,
            M06RescuePlan plan)
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
            Assert.That(M05LettersFacts.TryRecord(
                M05LetterDecision.TakePacket, read, write), Is.True);
            Assert.That(M06RescuePlanFacts.TryRecord(
                new M06ReunionRecord(
                    plan, true, M06LetterDeliveryOutcome.DeliveredWithPending, true),
                read, write), Is.True);
        }

        private static void AdvanceToDockRollCall(M07ExtractionNightView view)
        {
            view.ExecutePlanButton.onClick.Invoke();
            view.AdvanceButton.onClick.Invoke();
            view.ArrestRusButton.onClick.Invoke();
            view.AdvanceButton.onClick.Invoke();
        }

        [Test]
        public void HostCannotOpenWithoutVerifiedM06ReadinessOrCleanConfirmedKey()
        {
            var root = new GameObject("M07 host prerequisites");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            bool ready = false;
            M07ExtractionNightHost host = null;
            try
            {
                host = new M07ExtractionNightHost(
                    root.transform, () => ready, () => true, read, write);
                Assert.That(host.TryOpen(), Is.False);

                SeedVerifiedM06(values, M06RescuePlan.MinerShutdown);
                Assert.That(host.TryOpen(), Is.False);

                ready = true;
                values[M07ExtractionFacts.ConfirmedKey] = 2;
                Assert.That(host.TryOpen(), Is.False);

                values[M07ExtractionFacts.ConfirmedKey] = 0;
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
        public void OldExtractionViewCannotCommitAfterIdenticalNewLanding()
        {
            var root = new GameObject("M07 stale roll-call view");
            var values = new Dictionary<string, int>();
            SeedVerifiedM06(values, M06RescuePlan.CargoCover);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M07ExtractionNightHost host = null;
            try
            {
                host = new M07ExtractionNightHost(
                    root.transform, () => true, () => true, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var oldView = host.ActiveView;
                AdvanceToDockRollCall(oldView);
                int originalSequence = read(M05LandingFacts.ArrivalSequenceKey);

                Assert.That(ReturnOpeningContinuation.TryResetPreviousOutboundFacts(write),
                    Is.True);
                SeedVerifiedM06(values, M06RescuePlan.CargoCover);
                Assert.That(read(M05LandingFacts.ArrivalSequenceKey),
                    Is.GreaterThan(originalSequence));

                oldView.LiangStayButton.onClick.Invoke();
                Assert.That(read(M07ExtractionFacts.ConfirmedKey), Is.Zero);
                Assert.That(host.ActiveView, Is.SameAs(oldView));

                host.Dispose();
                host = new M07ExtractionNightHost(
                    root.transform, () => true, () => true, read, write);
                Assert.That(host.TryOpen(), Is.True);
                AdvanceToDockRollCall(host.ActiveView);
                host.ActiveView.LiangStayButton.onClick.Invoke();
                Assert.That(host.IsCompleted, Is.True);
                Assert.That(host.ActiveView, Is.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MinerShutdownEndToEndPersistsRollCallOnceAndBlocksReopen()
        {
            var root = new GameObject("M07 host miner shutdown");
            var values = new Dictionary<string, int>();
            SeedVerifiedM06(values, M06RescuePlan.MinerShutdown);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M07ExtractionNightHost host = null;
            M07ExtractionNightHost duplicate = null;
            try
            {
                host = new M07ExtractionNightHost(
                    root.transform, () => true, () => true, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                AdvanceToDockRollCall(view);
                view.LiangStayButton.onClick.Invoke();

                Assert.That(host.IsCompleted, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(M07ExtractionFacts.TryRead(
                    read, out M07ExtractionRecord? saved), Is.True);
                Assert.That(saved.HasValue, Is.True);
                Assert.That(saved.Value.ExecutedRoute, Is.EqualTo(M07ExecutedRoute.MinerShutdown));
                Assert.That(saved.Value.LowerLevelEvacuatedCount, Is.EqualTo(500));
                Assert.That(saved.Value.BoardedFirstBatchCount, Is.EqualTo(300));
                Assert.That(saved.Value.TaoJiRescued, Is.True);

                duplicate = new M07ExtractionNightHost(
                    root.transform, () => true, () => true, read, write);
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
            var root = new GameObject("M07 host retry");
            var values = new Dictionary<string, int>();
            SeedVerifiedM06(values, M06RescuePlan.OfficialHandover);
            Func<string, int> read = Reader(values);
            int preCommitFails = 0;
            int confirmCommits = 0;
            Action<string, int> unstable = (k, v) =>
            {
                if (k == M07ExtractionFacts.LiangChoiceKey && preCommitFails++ == 0)
                    throw new InvalidOperationException("Interrupted before confirm");
                values[k] = v;
                if (k == M07ExtractionFacts.ConfirmedKey && v == 1 && ++confirmCommits == 1)
                    throw new InvalidOperationException("Lost ack after confirm");
            };
            M07ExtractionNightHost host = null;
            try
            {
                host = new M07ExtractionNightHost(
                    root.transform, () => true, () => false, read, unstable);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                AdvanceToDockRollCall(view);

                Assert.Throws<InvalidOperationException>(() =>
                    view.LiangPersuadeButton.onClick.Invoke());
                Assert.That(host.IsCompleted, Is.False);
                Assert.That(host.ActiveView, Is.Not.Null);

                Assert.Throws<InvalidOperationException>(() =>
                    view.LiangPersuadeButton.onClick.Invoke());
                Assert.That(host.IsCompleted, Is.True);
                Assert.That(confirmCommits, Is.EqualTo(1));

                view.LiangPersuadeButton.onClick.Invoke();
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
        public void CompetingValidRollCallWithSameChoicesButDifferentBoardingCannotCloseScene()
        {
            var root = new GameObject("M07 competing roll call");
            var values = new Dictionary<string, int>();
            SeedVerifiedM06(values, M06RescuePlan.OfficialHandover);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M07ExtractionNightHost host = null;
            try
            {
                host = new M07ExtractionNightHost(
                    root.transform, () => true, () => true, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                AdvanceToDockRollCall(view);
                Assert.That(host.Session.BoardedFirstBatchCount, Is.EqualTo(300));

                // A separate scene has already committed a *valid* M07
                // record with the same Liang/Yida answers but only 200
                // people boarded. The old host checked only those answers
                // and silently retired this 300-person roll call.
                Assert.That(M07ExtractionNightSession.TryCreateFromSavedFacts(
                    read, out M07ExtractionNightSession other), Is.True);
                other.ExecuteRoute(blockedInField: false, boardedFirstBatchCount: 200);
                other.AdvanceTunnelOpening();
                other.ResolveRusConfrontation(
                    M07RusFate.Arrested, M07LedgerState.Preserved);
                other.AdvanceReturnLettersCheck();
                Assert.That(other.TryCompleteDockRollCall(
                    M07LiangChoice.StayOnIsland,
                    M07YidaChoice.HandToInspector,
                    taoJiRescued: true, read, write), Is.True);
                Assert.That(host.IsCompleted, Is.True);

                view.LiangStayButton.onClick.Invoke();
                Assert.That(host.ActiveView, Is.SameAs(view),
                    "Different verified boarding counts must not be acknowledged as one event.");
                Assert.That(host.Session.Stage, Is.EqualTo(M07ExtractionStage.DockRollCall));
                Assert.That(host.Session.CompletedRecord.HasValue, Is.False);
                Assert.That(M07ExtractionFacts.TryRead(
                    read, out M07ExtractionRecord? persisted), Is.True);
                Assert.That(persisted.HasValue, Is.True);
                Assert.That(persisted.Value.BoardedFirstBatchCount, Is.EqualTo(200));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ReadinessWithdrawalAtRollCallBlocksWriteUntilReadyAgain()
        {
            var root = new GameObject("M07 host readiness");
            var values = new Dictionary<string, int>();
            SeedVerifiedM06(values, M06RescuePlan.CargoCover);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            bool ready = true;
            M07ExtractionNightHost host = null;
            try
            {
                host = new M07ExtractionNightHost(
                    root.transform, () => ready, () => true, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                AdvanceToDockRollCall(view);

                ready = false;
                view.LiangForceButton.onClick.Invoke();
                Assert.That(host.IsCompleted, Is.False);
                Assert.That(host.ActiveView, Is.Not.Null);

                ready = true;
                view.LiangForceButton.onClick.Invoke();
                Assert.That(host.IsCompleted, Is.True);
                Assert.That(host.ActiveView, Is.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DisposalCleansUpViewAndIgnoresStaleDockRollCallClicks()
        {
            var root = new GameObject("M07 host dispose");
            var values = new Dictionary<string, int>();
            SeedVerifiedM06(values, M06RescuePlan.OfficialHandover);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M07ExtractionNightHost host = null;
            try
            {
                host = new M07ExtractionNightHost(
                    root.transform, () => true, () => true, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                AdvanceToDockRollCall(view);
                var stayBtn = view.LiangStayButton;

                host.Dispose();
                stayBtn.onClick.Invoke();
                Assert.That(host.IsDisposed, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(host.IsCompleted, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
