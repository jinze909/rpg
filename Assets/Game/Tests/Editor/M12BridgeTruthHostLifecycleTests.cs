using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M12BridgeTruthHostLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedThroughM11(Dictionary<string, int> values)
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
            Assert.That(M09BerthTableFacts.TryRecord(new M09BerthTableRecord(
                totalBerths: 20,
                pendingNames: 23,
                M09BerthExchangeFlags.HanQiaoCivilianVoyage,
                M09RuiyaOutcome.Rescued,
                qiDebtDisclosedHere: false,
                shenReconciled: false), read, write), Is.True);
            Assert.That(M10AssemblyFacts.TryRecord(new M10AssemblyRecord(
                worldNoticeThreeObserved: true,
                M10RosterAccuracy.VerifiedFirstPass,
                M10BattleReportSource.CarriedFromM02,
                M10HanLedgerUse.KeepConfidential,
                M10SupplyContractStatus.SignedFullCoverage,
                leverageWeakenedByRC02: false,
                coversRC05MountainFamilies: false,
                bellTowerSeizureHeard: true), read, write), Is.True);
            Assert.That(M11ReverseRescueFacts.TryRecord(new M11ReverseRescueRecord(
                threeBellsObserved: true,
                M11PassEntryMethod.LandGateNegotiation,
                conscriptionObserved: true,
                M11ThirdSquadConscriptionStance.NotInPass,
                M11RescueProvider.ShenLiShallowBoat,
                echoCallsHeard: 3,
                oulinJoinedExtraction: true,
                M11ShenDivisionPact.MutualConsultAndDivision), read, write), Is.True);
        }


        [Test]
        public void ReentrantSceneOpeningKeepsOneTruthPanel()
        {
            ReentrantOpeningKeepsOneTruthPanel(false);
        }

        [Test]
        public void ReentrantParticipantOpeningKeepsOneTruthPanel()
        {
            ReentrantOpeningKeepsOneTruthPanel(true);
        }

        private static void ReentrantOpeningKeepsOneTruthPanel(bool duringParticipantRead)
        {
            var root = new GameObject("M12 nested opening");
            var values = new Dictionary<string, int>();
            SeedThroughM11(values);
            M12BridgeTruthHost host = null;
            bool attempted = false, nestedOpened = true;
            Func<bool> callback = () =>
            {
                if (!attempted)
                {
                    attempted = true;
                    nestedOpened = host.TryOpen();
                }
                return !duringParticipantRead;
            };
            try
            {
                host = new M12BridgeTruthHost(root.transform,
                    duringParticipantRead ? (Func<bool>)(() => true) : callback,
                    () => false, () => false,
                    duringParticipantRead ? callback : (Func<bool>)(() => false),
                    () => false, Reader(values), (k, v) => values[k] = v);
                Assert.That(host.TryOpen(), Is.True);
                Assert.That(attempted, Is.True);
                Assert.That(nestedOpened, Is.False);
                Assert.That(host.TryOpen(), Is.False);
                FinishStatements(host.ActiveView);
                host.ActiveView.PublishTruthButton.onClick.Invoke();
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
        public void OpeningExceptionReleasesTruthReservation()
        {
            var root = new GameObject("M12 interrupted opening");
            var values = new Dictionary<string, int>();
            SeedThroughM11(values);
            M12BridgeTruthHost host = null;
            bool fail = true;
            try
            {
                host = new M12BridgeTruthHost(root.transform,
                    () => true, () =>
                    {
                        if (fail) throw new InvalidOperationException("Confession read interrupted");
                        return false;
                    }, () => false, () => false, () => false,
                    Reader(values), (k, v) => values[k] = v);
                Assert.Throws<InvalidOperationException>(() => host.TryOpen());
                Assert.That(host.ActiveView, Is.Null);
                fail = false;
                Assert.That(host.TryOpen(), Is.True);
                FinishStatements(host.ActiveView);
                host.ActiveView.SealTruthButton.onClick.Invoke();
                Assert.That(host.IsCompleted, Is.True);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RetirementDuringParticipantReadCannotPublishTruthPanel()
        {
            var root = new GameObject("M12 retired opening");
            var values = new Dictionary<string, int>();
            SeedThroughM11(values);
            M12BridgeTruthHost host = null;
            try
            {
                host = new M12BridgeTruthHost(root.transform,
                    () => true, () => false, () => false,
                    () => { host.Dispose(); return false; }, () => false,
                    Reader(values), (k, v) => values[k] = v);
                Assert.That(host.TryOpen(), Is.False);
                Assert.That(host.IsDisposed, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(host.Session, Is.Null);
                Assert.That(host.IsCompleted, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RetirementDuringFinalParticipantProofCannotSaveTruthDecision()
        {
            var root = new GameObject("M12 retired during final proof");
            var values = new Dictionary<string, int>();
            SeedThroughM11(values);
            M12BridgeTruthHost host = null;
            bool retire = false;
            int confirmWrites = 0;
            try
            {
                host = new M12BridgeTruthHost(root.transform,
                    () => true, () => false, () => false, () => false,
                    () =>
                    {
                        if (retire) host.Dispose();
                        return false;
                    }, Reader(values), (k, v) =>
                    {
                        values[k] = v;
                        if (k == M12BridgeTruthFacts.ConfirmedKey && v == 1)
                            confirmWrites++;
                    });
                Assert.That(host.TryOpen(), Is.True);
                var oldView = host.ActiveView;
                FinishStatements(oldView);
                retire = true;
                Assert.DoesNotThrow(() => oldView.PublishTruthButton.onClick.Invoke());
                Assert.That(host.IsDisposed, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(host.IsCompleted, Is.False);
                Assert.That(confirmWrites, Is.Zero);
                Assert.That(host.Session.IsCompleted, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void FinishStatements(M12BridgeTruthView view)
        {
            Assert.That(view, Is.Not.Null);
            view.SomeWantNoMoreWarButton.onClick.Invoke();
            view.HearJiangShenButton.onClick.Invoke();
            view.HearHanAndYiLeiButton.onClick.Invoke();
            Assert.That(view.Session.Phase, Is.EqualTo(M12Phase.TruthDisclosureChoice));
        }

        [Test]
        public void ExternalViewDisposalClearsActiveViewAndAllowsCleanReopen()
        {
            var root = new GameObject("M12 lifecycle external dispose");
            var values = new Dictionary<string, int>();
            SeedThroughM11(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M12BridgeTruthHost host = null;
            try
            {
                host = new M12BridgeTruthHost(
                    root.transform, () => true, () => false, () => false,
                    () => false, () => false, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var firstView = host.ActiveView;
                Assert.That(firstView, Is.Not.Null);
                Assert.That(firstView.IsDisposed, Is.False);

                firstView.Dispose();
                Assert.That(firstView.IsDisposed, Is.True);
                Assert.That(host.ActiveView, Is.Null);

                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.ActiveView, Is.Not.Null);
                Assert.That(host.ActiveView, Is.Not.SameAs(firstView));
                Assert.That(host.ActiveView.IsDisposed, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyedCanvasMarksViewDisposedAndAllowsHostReopen()
        {
            var root = new GameObject("M12 lifecycle destroyed canvas");
            var values = new Dictionary<string, int>();
            SeedThroughM11(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M12BridgeTruthHost host = null;
            try
            {
                host = new M12BridgeTruthHost(
                    root.transform, () => true, () => false, () => false,
                    () => false, () => false, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var firstView = host.ActiveView;
                Assert.That(firstView, Is.Not.Null);

                UnityEngine.Object.DestroyImmediate(firstView.Panel.parent.gameObject);
                Assert.That(firstView.IsDisposed, Is.True);
                Assert.That(host.ActiveView, Is.Null);

                Assert.That(host.TryOpen(), Is.True);
                Assert.That(host.ActiveView, Is.Not.Null);
                Assert.That(host.ActiveView, Is.Not.SameAs(firstView));
                Assert.That(host.ActiveView.IsDisposed, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DisposingHostDuringSuccessfulTruthSavePreservesDecisionWithoutThrowing()
        {
            var root = new GameObject("M12 dispose during truth save");
            var values = new Dictionary<string, int>();
            SeedThroughM11(values);
            Func<string, int> read = Reader(values);
            int confirmWrites = 0;
            M12BridgeTruthHost host = null;
            Action<string, int> write = (key, value) =>
            {
                values[key] = value;
                if (key == M12BridgeTruthFacts.ConfirmedKey && value == 1)
                {
                    confirmWrites++;
                    host.Dispose();
                }
            };
            try
            {
                host = new M12BridgeTruthHost(
                    root.transform, () => true, () => false, () => false,
                    () => false, () => false, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                view.SomeWantNoMoreWarButton.onClick.Invoke();
                view.HearJiangShenButton.onClick.Invoke();
                view.HearHanAndYiLeiButton.onClick.Invoke();
                Assert.That(host.Session.Phase, Is.EqualTo(M12Phase.TruthDisclosureChoice));

                var publish = view.PublishTruthButton;
                var seal = view.SealTruthButton;
                Assert.DoesNotThrow(() => publish.onClick.Invoke());
                Assert.That(host.IsDisposed, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(host.IsCompleted, Is.True);
                Assert.That(host.Session.IsCompleted, Is.True);
                Assert.That(host.Session.Disclosure, Is.EqualTo(M12TruthDisclosure.PublishTruth));
                Assert.That(confirmWrites, Is.EqualTo(1));
                Assert.That(M12BridgeTruthFacts.TryRead(
                    read, out M12BridgeTruthRecord? saved), Is.True);
                Assert.That(saved.Value.Disclosure, Is.EqualTo(M12TruthDisclosure.PublishTruth));
                Assert.That(saved.Value.FirstConfessionHeardHere, Is.True);
                Assert.DoesNotThrow(() => seal.onClick.Invoke());
                Assert.DoesNotThrow(() => publish.onClick.Invoke());
                Assert.That(confirmWrites, Is.EqualTo(1));
                Assert.That(M12BridgeTruthFacts.TryRead(read, out saved), Is.True);
                Assert.That(saved.Value.Disclosure, Is.EqualTo(M12TruthDisclosure.PublishTruth));
                Assert.That(host.TryOpen(), Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyedParentFailsClosedWithoutThrowingOnTryOpen()
        {
            var root = new GameObject("M12 lifecycle destroyed parent");
            var values = new Dictionary<string, int>();
            SeedThroughM11(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M12BridgeTruthHost host = null;
            try
            {
                host = new M12BridgeTruthHost(
                    root.transform, () => true, () => false, () => false,
                    () => false, () => false, read, write);
                UnityEngine.Object.DestroyImmediate(root);
                root = null;

                bool opened = false;
                Assert.DoesNotThrow(() => opened = host.TryOpen());
                Assert.That(opened, Is.False);
                Assert.That(host.ActiveView, Is.Null);
            }
            finally
            {
                host?.Dispose();
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
