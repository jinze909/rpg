using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M08DockTruthHostTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedConfirmedWorkshop(Dictionary<string, int> values)
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
        public void PreviousDockReplyCannotBeSavedAfterAnIdenticalNewArrival()
        {
            var root = new GameObject("M08 dock reply voyage replay");
            var values = new Dictionary<string, int>();
            SeedConfirmedWorkshop(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M08DockTruthHost oldHost = null;
            M08DockTruthHost currentHost = null;
            try
            {
                int firstArrival = read(M05LandingFacts.ArrivalSequenceKey);
                oldHost = new M08DockTruthHost(root.transform, () => true, read, write);
                Assert.That(oldHost.TryOpen(), Is.True);
                var oldView = oldHost.ActiveView;
                oldView.HearButton.onClick.Invoke();
                oldView.SecondResponseButton.onClick.Invoke();

                Assert.That(ReturnOpeningContinuation.TryResetPreviousOutboundFacts(write),
                    Is.True);
                oldView.FinishButton.onClick.Invoke();
                Assert.That(read(M08DockTruthFacts.ConfirmedKey), Is.Zero);

                SeedConfirmedWorkshop(values);
                Assert.That(read(M05LandingFacts.ArrivalSequenceKey),
                    Is.GreaterThan(firstArrival));
                oldView.FinishButton.onClick.Invoke();
                Assert.That(read(M08DockTruthFacts.ConfirmedKey), Is.Zero);
                Assert.That(oldHost.ActiveView, Is.SameAs(oldView));

                oldHost.Dispose();
                oldHost = null;
                currentHost = new M08DockTruthHost(root.transform, () => true, read, write);
                Assert.That(currentHost.TryOpen(), Is.True);
                currentHost.ActiveView.HearButton.onClick.Invoke();
                currentHost.ActiveView.SecondResponseButton.onClick.Invoke();
                currentHost.ActiveView.FinishButton.onClick.Invoke();
                Assert.That(currentHost.HasRecordedResponse, Is.True);
                Assert.That(currentHost.ActiveView, Is.Null);
            }
            finally
            {
                oldHost?.Dispose();
                currentHost?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DockNeverOpensWithoutVerifiedWinchOrPresentShenLi()
        {
            var root = new GameObject("M08 dock gating");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            bool present = true;
            M08DockTruthHost host = null;
            try
            {
                host = new M08DockTruthHost(root.transform, () => present, read, write);
                Assert.That(host.TryOpen(), Is.False);
                Assert.That(M08DockTruthFacts.TryRecord(
                    M08DockTruthResponse.StaySilent, true, read, write), Is.False);
                Assert.That(values, Is.Empty);

                SeedConfirmedWorkshop(values);
                present = false;
                Assert.That(host.TryOpen(), Is.False);
                Assert.That(M08DockTruthFacts.TryRecord(
                    M08DockTruthResponse.StaySilent, false, read, write), Is.False);

                present = true;
                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.HasRecordedResponse, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TornUnconfirmedDockResponseCannotOpenFreshDialogue()
        {
            var root = new GameObject("M08 torn dock response");
            var values = new Dictionary<string, int>();
            SeedConfirmedWorkshop(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            M08DockTruthHost host = null;
            try
            {
                host = new M08DockTruthHost(root.transform, () => true, read, write);
                values[M08DockTruthFacts.ShenRelationResponseKey] =
                    (int)M08DockTruthResponse.StaySilent;
                Assert.That(read(M08DockTruthFacts.ConfirmedKey), Is.Zero);
                Assert.That(host.TryOpen(), Is.False,
                    "A partially committed Shen Li reply cannot be overwritten by a fresh conversation.");
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(read(M08DockTruthFacts.ShenRelationResponseKey),
                    Is.EqualTo((int)M08DockTruthResponse.StaySilent));

                Assert.That(M08DockTruthFacts.TryResetForNewVoyage(write), Is.True);
                Assert.That(host.TryOpen(), Is.True,
                    "An explicitly reset voyage still allows the dock conversation.");
                Assert.That(host.ActiveView, Is.Not.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AllThreeDockRepliesSaveAndReplayTheActualPlayerChoiceOnce()
        {
            foreach (M08DockTruthResponse choice in new[] {
                M08DockTruthResponse.AngerAtBeingShielded,
                M08DockTruthResponse.StaySilent,
                M08DockTruthResponse.AdmitSharedFear })
            {
                var root = new GameObject("M08 dock record " + choice);
                var values = new Dictionary<string, int>();
                SeedConfirmedWorkshop(values);
                Func<string, int> read = Reader(values);
                Action<string, int> write = (k, v) => values[k] = v;
                M08DockTruthHost host = null;
                M08DockTruthHost reopened = null;
                try
                {
                    host = new M08DockTruthHost(root.transform, () => true, read, write);
                    Assert.That(host.TryOpen(), Is.True);
                    var view = host.ActiveView;
                    view.HearButton.onClick.Invoke();
                    switch (choice)
                    {
                        case M08DockTruthResponse.AngerAtBeingShielded:
                            view.FirstResponseButton.onClick.Invoke(); break;
                        case M08DockTruthResponse.StaySilent:
                            view.SecondResponseButton.onClick.Invoke(); break;
                        case M08DockTruthResponse.AdmitSharedFear:
                            view.ThirdResponseButton.onClick.Invoke(); break;
                    }
                    view.FinishButton.onClick.Invoke();
                    Assert.That(host.HasRecordedResponse, Is.True);
                    Assert.That(host.ActiveView, Is.Null);
                    Assert.That(M08DockTruthFacts.TryRead(
                        read, out M08DockTruthResponse? persisted), Is.True);
                    Assert.That(persisted, Is.EqualTo(choice));
                    reopened = new M08DockTruthHost(root.transform, () => true, read, write);
                    Assert.That(reopened.TryOpen(), Is.False);
                    Assert.That(M08DockTruthFacts.TryRecord(
                        choice, true, read, write), Is.False);
                }
                finally
                {
                    reopened?.Dispose();
                    host?.Dispose();
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        [Test]
        public void LockedDockDialogueCannotHearChooseOrPersistResponseUntilUnlocked()
        {
            var root = new GameObject("M08 dock input lock");
            var values = new Dictionary<string, int>();
            SeedConfirmedWorkshop(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            M08DockTruthHost host = null;
            try
            {
                host = new M08DockTruthHost(root.transform, () => true, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                var group = view.Panel.gameObject.AddComponent<CanvasGroup>();
                group.interactable = false;
                Assert.That(view.HearButton.IsInteractable(), Is.False);
                view.HearButton.onClick.Invoke();
                Assert.That(view.Session.Stage, Is.EqualTo(M08DockTruthStage.AwaitingTruth));
                group.interactable = true;
                view.HearButton.onClick.Invoke();

                group.interactable = false;
                view.FirstResponseButton.onClick.Invoke();
                view.SecondResponseButton.onClick.Invoke();
                view.ThirdResponseButton.onClick.Invoke();
                Assert.That(view.Session.Stage, Is.EqualTo(M08DockTruthStage.AwaitingResponse));
                Assert.That(view.Session.SelectedResponse, Is.Null);
                group.interactable = true;
                view.SecondResponseButton.onClick.Invoke();
                Assert.That(view.Session.Stage, Is.EqualTo(M08DockTruthStage.AwaitingHost));

                group.interactable = false;
                view.FinishButton.onClick.Invoke();
                Assert.That(host.HasRecordedResponse, Is.False);
                Assert.That(read(M08DockTruthFacts.ShenRelationResponseKey), Is.Zero);
                Assert.That(read(M08DockTruthFacts.ConfirmedKey), Is.Zero);
                group.interactable = true;
                view.Panel.gameObject.SetActive(false);
                view.FinishButton.onClick.Invoke();
                Assert.That(host.HasRecordedResponse, Is.False);
                view.Panel.gameObject.SetActive(true);
                view.FinishButton.onClick.Invoke();
                Assert.That(host.HasRecordedResponse, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(M08DockTruthFacts.TryRead(read, out M08DockTruthResponse? saved), Is.True);
                Assert.That(saved, Is.EqualTo(M08DockTruthResponse.StaySilent));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InterruptedWriteKeepsSelectedResponseAndHostCanRetry()
        {
            var root = new GameObject("M08 dock interrupted write");
            var values = new Dictionary<string, int>();
            SeedConfirmedWorkshop(values);
            Func<string, int> read = Reader(values);
            int writes = 0;
            Action<string, int> unstable = (k, v) =>
            {
                if (k == M08DockTruthFacts.ShenRelationResponseKey && writes++ == 0)
                    throw new InvalidOperationException("Interrupted Makinom write");
                values[k] = v;
            };
            M08DockTruthHost host = null;
            try
            {
                host = new M08DockTruthHost(root.transform, () => true, read, unstable);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                view.HearButton.onClick.Invoke();
                view.ThirdResponseButton.onClick.Invoke();
                Assert.Throws<InvalidOperationException>(() =>
                    view.FinishButton.onClick.Invoke());
                Assert.That(host.HasRecordedResponse, Is.False);
                Assert.That(view.FinishButton.interactable, Is.True);
                view.FinishButton.onClick.Invoke();
                Assert.That(host.HasRecordedResponse, Is.True);
                Assert.That(writes, Is.EqualTo(2));
                Assert.That(host.ActiveView, Is.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CommittedThenThrewIsAcknowledgedWithoutDuplicateWriting()
        {
            var root = new GameObject("M08 dock acknowledgement lost");
            var values = new Dictionary<string, int>();
            SeedConfirmedWorkshop(values);
            Func<string, int> read = Reader(values);
            int commitCount = 0;
            Action<string, int> unstable = (k, v) =>
            {
                values[k] = v;
                if (k == M08DockTruthFacts.ConfirmedKey && v == 1 &&
                    ++commitCount == 1)
                    throw new InvalidOperationException("Acknowledgement lost");
            };
            M08DockTruthHost host = null;
            try
            {
                host = new M08DockTruthHost(root.transform, () => true, read, unstable);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                view.HearButton.onClick.Invoke();
                view.SecondResponseButton.onClick.Invoke();
                Assert.Throws<InvalidOperationException>(() =>
                    view.FinishButton.onClick.Invoke());
                Assert.That(host.HasRecordedResponse, Is.True);
                view.FinishButton.onClick.Invoke();
                Assert.That(commitCount, Is.EqualTo(1));
                Assert.That(host.ActiveView, Is.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PartySplitBeforeSaveCannotWriteAndCorruptOrStaleFactsDoNotRead()
        {
            var root = new GameObject("M08 dock late party split");
            var values = new Dictionary<string, int>();
            SeedConfirmedWorkshop(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            bool shenPresent = true;
            M08DockTruthHost host = null;
            try
            {
                host = new M08DockTruthHost(root.transform, () => shenPresent, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                view.HearButton.onClick.Invoke();
                view.FirstResponseButton.onClick.Invoke();
                shenPresent = false;
                view.FinishButton.onClick.Invoke();
                Assert.That(host.HasRecordedResponse, Is.False);
                Assert.That(values.ContainsKey(M08DockTruthFacts.ConfirmedKey), Is.False);
                shenPresent = true;
                view.FinishButton.onClick.Invoke();
                Assert.That(host.HasRecordedResponse, Is.True);

                values[M08DockTruthFacts.ShenRelationResponseKey] = 99;
                Assert.That(M08DockTruthFacts.TryRead(
                    read, out M08DockTruthResponse? corrupt), Is.True);
                Assert.That(corrupt, Is.Null);
                values[M08DockTruthFacts.ShenRelationResponseKey] =
                    (int)M08DockTruthResponse.AngerAtBeingShielded;
                Assert.That(M08WorkshopDepositFacts.TryResetForNewVoyage(write), Is.True);
                Assert.That(M08DockTruthFacts.TryRead(
                    read, out M08DockTruthResponse? stale), Is.True);
                Assert.That(stale, Is.Null);
                Assert.That(M08DockTruthFacts.TryResetForNewVoyage(write), Is.True);
                Assert.That(values[M08DockTruthFacts.ConfirmedKey], Is.Zero);
                Assert.That(values[M08DockTruthFacts.ShenRelationResponseKey], Is.Zero);
                Assert.That(M08DockTruthFacts.TryRead(
                    null, out M08DockTruthResponse? missing), Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
