using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M08WorkshopOrderHostTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedVerifiedM07(Dictionary<string, int> values)
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
                new M06ReunionRecord(M06RescuePlan.OfficialHandover, true,
                    M06LetterDeliveryOutcome.NotCarried, true), read, write), Is.True);
            Assert.That(M07ExtractionFacts.TryRecord(
                new M07ExtractionRecord(
                    M07ExecutedRoute.OfficialHandover,
                    M07PumpShutdownState.KeptRunning,
                    M07RouteCostFlags.HarborSupplyDebt,
                    boardedFirstBatchCount: 10,
                    lowerLevelEvacuatedCount: 0,
                    M07RusFate.Arrested,
                    M07LedgerState.Preserved,
                    M07LiangChoice.StayOnIsland,
                    M07YidaChoice.HandToInspector,
                    taoJiRescued: false), read, write), Is.True);
        }

        private static void CompleteWinch(M08FirstWinchOrderView view)
        {
            view.PrimaryButton.onClick.Invoke(); // Scrap gear
            view.SecondaryButton.onClick.Invoke(); // Dock cable
            view.PrimaryButton.onClick.Invoke(); // Assemble
            view.PrimaryButton.onClick.Invoke(); // Unsafe first test
            Assert.That(view.Session.Stage, Is.EqualTo(M08WinchOrderStage.RepairLooseCable));
            view.PrimaryButton.onClick.Invoke(); // Repair
            view.PrimaryButton.onClick.Invoke(); // Successful retry
            Assert.That(view.Session.IsCompleted, Is.True);
        }

        [Test]
        public void WorkshopCannotPayBeforeM07OrWithoutCompletedWinch()
        {
            var root = new GameObject("M08 host gate test");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            M08WorkshopOrderHost host = null;
            try
            {
                host = new M08WorkshopOrderHost(root.transform, read, write);
                Assert.That(host.TryOpen(), Is.False);
                Assert.That(M08WorkshopDepositFacts.TryConfirm(new M08FirstWinchOrderSession(), read, write), Is.False);
                Assert.That(values, Is.Empty);
                SeedVerifiedM07(values);
                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.IsCompleted, Is.False);
                Assert.That(M08WorkshopDepositFacts.TryConfirm(host.ActiveView.Session, read, write), Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void IdenticalNewVoyageCannotClaimOldWinchCompletion()
        {
            var root = new GameObject("M08 stale winch commission");
            var values = new Dictionary<string, int>();
            SeedVerifiedM07(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M08WorkshopOrderHost host = null;
            try
            {
                host = new M08WorkshopOrderHost(root.transform, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var oldView = host.ActiveView;
                CompleteWinch(oldView);
                int oldSequence = read(M05LandingFacts.ArrivalSequenceKey);

                Assert.That(ReturnOpeningContinuation.TryResetPreviousOutboundFacts(write),
                    Is.True);
                SeedVerifiedM07(values);
                Assert.That(read(M05LandingFacts.ArrivalSequenceKey),
                    Is.GreaterThan(oldSequence));
                var oldButton = oldView.FinishButton;
                Assert.Throws<InvalidOperationException>(() =>
                    oldButton.onClick.Invoke());
                Assert.That(read(M08WorkshopDepositFacts.CompletedKey), Is.Zero);
                Assert.That(host.ActiveView, Is.SameAs(oldView));

                host.Dispose();
                host = new M08WorkshopOrderHost(root.transform, read, write);
                Assert.That(host.TryOpen(), Is.True);
                CompleteWinch(host.ActiveView);
                host.ActiveView.FinishButton.onClick.Invoke();
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
        public void CompletedWinchPersistsOnceAcrossNewHostInstances()
        {
            var root = new GameObject("M08 host persist test");
            var values = new Dictionary<string, int>();
            SeedVerifiedM07(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            M08WorkshopOrderHost first = null;
            M08WorkshopOrderHost again = null;
            try
            {
                first = new M08WorkshopOrderHost(root.transform, read, write);
                Assert.That(first.TryOpen(), Is.True);
                CompleteWinch(first.ActiveView);
                first.ActiveView.FinishButton.onClick.Invoke();
                Assert.That(first.ActiveView, Is.Null);
                Assert.That(first.IsCompleted, Is.True);
                Assert.That(values[M08WorkshopDepositFacts.CompletedKey], Is.EqualTo(1));

                again = new M08WorkshopOrderHost(root.transform, read, write);
                Assert.That(again.IsCompleted, Is.True);
                Assert.That(again.TryOpen(), Is.False);
                Assert.That(M08WorkshopDepositFacts.TryConfirm(new M08FirstWinchOrderSession(), read, write), Is.False);
            }
            finally
            {
                again?.Dispose();
                first?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InterruptedWriteAllowsSameViewRetryWithoutDoubleCompletion()
        {
            var root = new GameObject("M08 host retry test");
            var values = new Dictionary<string, int>();
            SeedVerifiedM07(values);
            Func<string, int> read = Reader(values);
            int attempts = 0;
            Action<string, int> unstable = (key, value) =>
            {
                if (key == M08WorkshopDepositFacts.CompletedKey && attempts++ == 0)
                    throw new InvalidOperationException("Interrupted deposit save");
                values[key] = value;
            };
            M08WorkshopOrderHost host = null;
            try
            {
                host = new M08WorkshopOrderHost(root.transform, read, unstable);
                Assert.That(host.TryOpen(), Is.True);
                CompleteWinch(host.ActiveView);
                var view = host.ActiveView;
                Assert.Throws<InvalidOperationException>(() => view.FinishButton.onClick.Invoke());
                Assert.That(host.IsCompleted, Is.False);
                Assert.That(view.FinishButton.interactable, Is.True);
                view.FinishButton.onClick.Invoke();
                Assert.That(host.IsCompleted, Is.True);
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
        public void AcknowledgesWriteThatCommittedBeforeThrowWithoutPayingTwice()
        {
            var root = new GameObject("M08 host write-then-throw test");
            var values = new Dictionary<string, int>();
            SeedVerifiedM07(values);
            Func<string, int> read = Reader(values);
            int writes = 0;
            Action<string, int> writeThenThrow = (key, value) =>
            {
                values[key] = value;
                if (key == M08WorkshopDepositFacts.CompletedKey && ++writes == 1)
                    throw new InvalidOperationException("Host lost acknowledgement after save");
            };
            M08WorkshopOrderHost host = null;
            try
            {
                host = new M08WorkshopOrderHost(root.transform, read, writeThenThrow);
                Assert.That(host.TryOpen(), Is.True);
                CompleteWinch(host.ActiveView);
                var view = host.ActiveView;
                Assert.Throws<InvalidOperationException>(() => view.FinishButton.onClick.Invoke());
                Assert.That(host.IsCompleted, Is.True);
                view.FinishButton.onClick.Invoke();
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
        public void PartialWorkshopResetCannotReplayDepositOverOldDockReplyOrDawnNotice()
        {
            var root = new GameObject("M08 orphaned downstream workshop reset");
            var values = new Dictionary<string, int>();
            SeedVerifiedM07(values);
            Func<string, int> read = Reader(values);
            int depositWrites = 0;
            Action<string, int> write = (key, value) =>
            {
                if (key == M08WorkshopDepositFacts.CompletedKey && value == 1)
                    depositWrites++;
                values[key] = value;
            };
            var completedWinch = new M08FirstWinchOrderSession();
            Assert.That(completedWinch.TryAcquirePart(M08WinchPart.ScrapGear), Is.True);
            Assert.That(completedWinch.TryAcquirePart(M08WinchPart.DockCable), Is.True);
            Assert.That(completedWinch.TryAssemble(), Is.True);
            Assert.That(completedWinch.TryTightenCable(), Is.True);
            Assert.That(completedWinch.TryTestLift(), Is.True);
            M08WorkshopOrderHost host = null;
            try
            {
                var orphanedCases = new (string Key, int Value)[]
                {
                    (M08DockTruthFacts.ConfirmedKey, 1),
                    (M08DockTruthFacts.ConfirmedKey, 2),
                    (M08DockTruthFacts.ShenRelationResponseKey, (int)M08DockTruthResponse.StaySilent),
                    (M08DockTruthFacts.ShenRelationResponseKey, 99),
                    (M08FirstWorldNoticeFacts.ConfirmedKey, 1),
                    (M08FirstWorldNoticeFacts.ConfirmedKey, 2)
                };
                foreach (var orphan in orphanedCases)
                {
                    Assert.That(M08WorkshopDepositFacts.TryResetForNewVoyage(write), Is.True);
                    Assert.That(M08DockTruthFacts.TryResetForNewVoyage(write), Is.True);
                    Assert.That(M08FirstWorldNoticeFacts.TryResetForNewVoyage(write), Is.True);
                    values[orphan.Key] = orphan.Value;
                    host = new M08WorkshopOrderHost(root.transform, read, write);
                    Assert.That(host.TryOpen(), Is.False,
                        $"An old downstream M08 fact '{orphan.Key}' must block a new workshop.");
                    Assert.That(host.ActiveView, Is.Null);
                    Assert.That(M08WorkshopDepositFacts.TryConfirm(
                        completedWinch, read, write), Is.False,
                        "The Makinom deposit authority must also reject the stale story state.");
                    Assert.That(depositWrites, Is.Zero);
                    Assert.That(read(M08WorkshopDepositFacts.CompletedKey), Is.Zero);
                    host.Dispose();
                    host = null;
                }

                Assert.That(M08DockTruthFacts.TryResetForNewVoyage(write), Is.True);
                Assert.That(M08FirstWorldNoticeFacts.TryResetForNewVoyage(write), Is.True);
                host = new M08WorkshopOrderHost(root.transform, read, write);
                Assert.That(host.TryOpen(), Is.True,
                    "Once the explicit reset clears all orphaned facts, a genuine new commission may open.");
                Assert.That(host.ActiveView, Is.Not.Null);
                Assert.That(M08WorkshopDepositFacts.TryConfirm(
                    completedWinch, read, write), Is.True);
                Assert.That(depositWrites, Is.EqualTo(1));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ResetOrInvalidUpstreamNeverTreatsStaleDepositAsPaid()
        {
            var values = new Dictionary<string, int>();
            SeedVerifiedM07(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            var session = new M08FirstWinchOrderSession();
            Assert.That(session.TryAcquirePart(M08WinchPart.ScrapGear), Is.True);
            Assert.That(session.TryAcquirePart(M08WinchPart.DockCable), Is.True);
            Assert.That(session.TryAssemble(), Is.True);
            Assert.That(session.TryTightenCable(), Is.True);
            Assert.That(session.TryTestLift(), Is.True);
            Assert.That(M08WorkshopDepositFacts.TryConfirm(session, read, write), Is.True);
            Assert.That(M08WorkshopDepositFacts.TryRead(read, out bool paid), Is.True);
            Assert.That(paid, Is.True);

            Assert.That(M07ExtractionFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(M08WorkshopDepositFacts.TryRead(read, out paid), Is.True);
            Assert.That(paid, Is.False);
            Assert.That(M08WorkshopDepositFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(values[M08WorkshopDepositFacts.CompletedKey], Is.Zero);
            values[M08WorkshopDepositFacts.CompletedKey] = 2;
            Assert.That(M08WorkshopDepositFacts.TryRead(read, out paid), Is.True);
            Assert.That(paid, Is.False);
            Assert.That(M08WorkshopDepositFacts.TryRead(null, out paid), Is.False);
            Assert.That(M08WorkshopDepositFacts.TryResetForNewVoyage(null), Is.False);
        }
    }
}
