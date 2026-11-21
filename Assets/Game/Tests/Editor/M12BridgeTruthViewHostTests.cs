using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M12BridgeTruthViewHostTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedThroughM11(
            Dictionary<string, int> values,
            M10HanLedgerUse m10HanUse = M10HanLedgerUse.KeepConfidential,
            bool seedRC01Completed = false)
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

            if (seedRC01Completed)
            {
                var winch = new M08FirstWinchOrderSession();
                Assert.That(winch.TryAcquirePart(M08WinchPart.ScrapGear), Is.True);
                Assert.That(winch.TryAcquirePart(M08WinchPart.DockCable), Is.True);
                Assert.That(winch.TryAssemble(), Is.True);
                Assert.That(winch.TryTightenCable(), Is.True);
                Assert.That(winch.TryTestLift(), Is.True);
                Assert.That(M08WorkshopDepositFacts.TryConfirm(winch, read, write), Is.True);
                Assert.That(M08FirstWorldNoticeFacts.TryRecord(true, read, write), Is.True);
                Assert.That(RC01ChenYanFacts.TryRecord(new RC01ChenYanRecord(
                    RC01FerryClues.All,
                    RC01TalkOrder.LinSuFirst,
                    RC01ChenYanOutcome.KeepMarkedDead,
                    linSuRegisterNoteRecorded: true), read, write), Is.True);
            }

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
                m10HanUse,
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
        public void HostCannotOpenWithoutM11RescuePresenceOrCleanConfirmedKey()
        {
            var root = new GameObject("M12 host prerequisites");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            bool atCamp = false;
            M12BridgeTruthHost host = null;
            try
            {
                host = new M12BridgeTruthHost(
                    root.transform, () => atCamp, () => false, () => false,
                    () => false, () => false, read, write);
                Assert.That(host.TryOpen(), Is.False);

                SeedThroughM11(values);
                Assert.That(host.TryOpen(), Is.False);

                atCamp = true;
                values[M12BridgeTruthFacts.ConfirmedKey] = 2;
                Assert.That(host.TryOpen(), Is.False);

                values[M12BridgeTruthFacts.ConfirmedKey] = 0;
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
        public void FullFlowHandlesAbsentShenLiPublishTruthAndIrrevocableM10DraftOnce()
        {
            var root = new GameObject("M12 host absent shen li");
            var values = new Dictionary<string, int>();
            SeedThroughM11(values, M10HanLedgerUse.PublishDraft);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M12BridgeTruthHost host = null;
            M12BridgeTruthHost duplicate = null;
            try
            {
                host = new M12BridgeTruthHost(
                    root.transform, () => true, () => false, () => false,
                    () => false, () => false, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                Assert.That(view.YouSaidThatLastTimeButton.gameObject.activeSelf, Is.False);

                view.SomeWantNoMoreWarButton.onClick.Invoke();
                Assert.That(host.Session.Phase, Is.EqualTo(M12Phase.StakeholderStatements));
                Assert.That(view.HearShenLiButton.gameObject.activeSelf, Is.False);

                view.HearJiangShenButton.onClick.Invoke();
                view.HearHanAndYiLeiButton.onClick.Invoke();
                Assert.That(host.Session.Phase, Is.EqualTo(M12Phase.TruthDisclosureChoice));
                Assert.That(view.PublishTruthButton.gameObject.activeSelf, Is.True);

                view.PublishTruthButton.onClick.Invoke();
                Assert.That(host.IsCompleted, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(M12BridgeTruthFacts.TryRead(read, out M12BridgeTruthRecord? saved), Is.True);
                Assert.That(saved.Value.FirstConfessionHeardHere, Is.True);
                Assert.That(saved.Value.HanQiaoAlreadyOutOfOffice, Is.True);
                Assert.That(saved.Value.Disclosure, Is.EqualTo(M12TruthDisclosure.PublishTruth));

                duplicate = new M12BridgeTruthHost(
                    root.transform, () => true, () => false, () => false,
                    () => false, () => false, read, write);
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
        public void PriorM03ConfessionWithShenLiLinSuEchoAndInterruptedSaveRecoverCleanly()
        {
            var root = new GameObject("M12 host retry");
            var values = new Dictionary<string, int>();
            SeedThroughM11(values, M10HanLedgerUse.TradeForConcessions, seedRC01Completed: true);
            Func<string, int> read = Reader(values);
            int preCommitFails = 0;
            int confirmCommits = 0;
            Action<string, int> unstable = (k, v) =>
            {
                if (k == M12BridgeTruthFacts.DisclosureKey && preCommitFails++ == 0)
                    throw new InvalidOperationException("Interrupted before confirm");
                values[k] = v;
                if (k == M12BridgeTruthFacts.ConfirmedKey && v == 1 && ++confirmCommits == 1)
                    throw new InvalidOperationException("Lost ack after confirm");
            };
            M12BridgeTruthHost host = null;
            try
            {
                host = new M12BridgeTruthHost(
                    root.transform, () => true, () => true, () => true,
                    () => true, () => true, read, unstable);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                Assert.That(view.YouSaidThatLastTimeButton.gameObject.activeSelf, Is.True);

                view.YouSaidThatLastTimeButton.onClick.Invoke();
                Assert.That(view.LinSuEchoButton.gameObject.activeSelf, Is.True);
                view.LinSuEchoButton.onClick.Invoke();
                Assert.That(host.Session.LinSuEchoHeard, Is.True);

                view.HearHanAndYiLeiButton.onClick.Invoke();
                view.HearJiangShenButton.onClick.Invoke();
                view.HearShenLiButton.onClick.Invoke();
                Assert.That(host.Session.Phase, Is.EqualTo(M12Phase.TruthDisclosureChoice));

                Assert.Throws<InvalidOperationException>(() =>
                    view.WillingKnowersButton.onClick.Invoke());
                Assert.That(host.IsCompleted, Is.False);

                Assert.Throws<InvalidOperationException>(() =>
                    view.WillingKnowersButton.onClick.Invoke());
                Assert.That(host.IsCompleted, Is.True);
                Assert.That(confirmCommits, Is.EqualTo(1));

                view.WillingKnowersButton.onClick.Invoke();
                Assert.That(confirmCommits, Is.EqualTo(1));
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(M12BridgeTruthFacts.TryRead(read, out M12BridgeTruthRecord? saved), Is.True);
                Assert.That(saved.Value.LinSuEchoHeard, Is.True);
                Assert.That(saved.Value.BreaksLuSecrecyPromise, Is.True);
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
            var root = new GameObject("M12 host guards");
            var values = new Dictionary<string, int>();
            SeedThroughM11(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            string prevLang = SoundwellUiLocalization.Language;
            bool atCamp = true;
            M12BridgeTruthHost host = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                host = new M12BridgeTruthHost(
                    root.transform, () => atCamp, () => false, () => false,
                    () => false, () => false, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                Assert.That(view.Heading.text, Does.Contain("BELL TOWER CONFRONTATION"));
                Assert.That(view.Panel.anchorMin.y, Is.EqualTo(0f));
                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(aspect));

                view.YouTookTheirRoadButton.onClick.Invoke();
                view.HearJiangShenButton.onClick.Invoke();
                view.HearHanAndYiLeiButton.onClick.Invoke();
                var sealBtn = view.SealTruthButton;

                atCamp = false;
                sealBtn.onClick.Invoke();
                Assert.That(host.IsCompleted, Is.False);

                atCamp = true;
                host.Dispose();
                sealBtn.onClick.Invoke();
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
