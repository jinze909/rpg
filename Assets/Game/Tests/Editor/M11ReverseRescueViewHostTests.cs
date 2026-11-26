using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M11ReverseRescueViewHostTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedThroughM10(
            Dictionary<string, int> values,
            bool seedRC05Enlisted = false)
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

            if (seedRC05Enlisted)
            {
                var winch = new M08FirstWinchOrderSession();
                Assert.That(winch.TryAcquirePart(M08WinchPart.ScrapGear), Is.True);
                Assert.That(winch.TryAcquirePart(M08WinchPart.DockCable), Is.True);
                Assert.That(winch.TryAssemble(), Is.True);
                Assert.That(winch.TryTightenCable(), Is.True);
                Assert.That(winch.TryTestLift(), Is.True);
                Assert.That(M08WorkshopDepositFacts.TryConfirm(winch, read, write), Is.True);
                Assert.That(M08FirstWorldNoticeFacts.TryRecord(true, read, write), Is.True);
                Assert.That(RC05ThirdSquadFacts.TryRecord(new RC05ThirdSquadRecord(
                    trialDeliveryVerified: true, RC05Resolution.EnlistUnderLuJun,
                    skirmishOccurred: false, realZhengYuanVerified: false,
                    oldWomanStoneMentioned: false), read, write), Is.True);
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
                M10HanLedgerUse.PublishDraft,
                M10SupplyContractStatus.SignedFullCoverage,
                leverageWeakenedByRC02: false,
                coversRC05MountainFamilies: false,
                bellTowerSeizureHeard: true), read, write), Is.True);
        }

        [Test]
        public void HostCannotOpenWithoutM10AssemblyPresenceOrCleanConfirmedKey()
        {
            var root = new GameObject("M11 host prerequisites");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            bool atPass = false;
            M11ReverseRescueHost host = null;
            try
            {
                host = new M11ReverseRescueHost(
                    root.transform, () => atPass, () => true, () => true, read, write);
                Assert.That(host.TryOpen(), Is.False);

                SeedThroughM10(values);
                Assert.That(host.TryOpen(), Is.False);

                atPass = true;
                values[M11ReverseRescueFacts.ConfirmedKey] = 2;
                Assert.That(host.TryOpen(), Is.False);

                values[M11ReverseRescueFacts.ConfirmedKey] = 0;
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
        public void FullFlowWithShenLiAbsentOulinAndRC05EnlistedCommitsShoreBoatmanOnce()
        {
            var root = new GameObject("M11 host shen li flow");
            var values = new Dictionary<string, int>();
            SeedThroughM10(values, seedRC05Enlisted: true);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            M11ReverseRescueHost host = null;
            M11ReverseRescueHost duplicate = null;
            try
            {
                host = new M11ReverseRescueHost(
                    root.transform, () => true, () => true, () => false, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;

                view.LandGateButton.onClick.Invoke();
                Assert.That(host.Session.Phase, Is.EqualTo(M11Phase.ConscriptionAndTrap));
                Assert.That(view.HaoLieEnforceButton.gameObject.activeSelf, Is.True);

                view.HaoLieRefuseButton.onClick.Invoke();
                Assert.That(host.Session.Phase, Is.EqualTo(M11Phase.FogExtraction));

                view.HearFogCallButton.onClick.Invoke();
                view.HearFogCallButton.onClick.Invoke();
                view.HearFogCallButton.onClick.Invoke();
                Assert.That(host.Session.EchoCallsHeard, Is.EqualTo(3));

                view.AnswerPresentButton.onClick.Invoke();
                Assert.That(host.Session.Phase, Is.EqualTo(M11Phase.PostRescuePact));

                view.MutualDivisionButton.onClick.Invoke();
                view.ConfirmRescueButton.onClick.Invoke();

                Assert.That(host.IsCompleted, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(M11ReverseRescueFacts.TryRead(read, out M11ReverseRescueRecord? saved), Is.True);
                Assert.That(saved.Value.ThirdSquadStance, Is.EqualTo(M11ThirdSquadConscriptionStance.RefusedForcedConscription));
                Assert.That(saved.Value.ShoreBoatmanTookHelm, Is.True);
                Assert.That(saved.Value.ImprovesShenRelation, Is.True);

                duplicate = new M11ReverseRescueHost(
                    root.transform, () => true, () => true, () => false, read, write);
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
        public void FallbackArtisansWhenShenLiAbsentAndInterruptedSaveRecoverCleanly()
        {
            var root = new GameObject("M11 host fallback retry");
            var values = new Dictionary<string, int>();
            SeedThroughM10(values);
            Func<string, int> read = Reader(values);
            int preCommitFails = 0;
            int confirmCommits = 0;
            Action<string, int> unstable = (k, v) =>
            {
                if (k == M11ReverseRescueFacts.RescueProviderKey && preCommitFails++ == 0)
                    throw new InvalidOperationException("Interrupted before confirm");
                values[k] = v;
                if (k == M11ReverseRescueFacts.ConfirmedKey && v == 1 && ++confirmCommits == 1)
                    throw new InvalidOperationException("Lost ack after confirm");
            };
            M11ReverseRescueHost host = null;
            try
            {
                host = new M11ReverseRescueHost(
                    root.transform, () => true, () => false, () => true, read, unstable);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;

                view.ReliefPassButton.onClick.Invoke();
                view.CoverReturneesButton.onClick.Invoke();
                view.FallbackArtisansButton.onClick.Invoke();
                Assert.That(host.Session.Phase, Is.EqualTo(M11Phase.PostRescuePact));

                Assert.Throws<InvalidOperationException>(() =>
                    view.ConfirmRescueButton.onClick.Invoke());
                Assert.That(host.IsCompleted, Is.False);

                Assert.Throws<InvalidOperationException>(() =>
                    view.ConfirmRescueButton.onClick.Invoke());
                Assert.That(host.IsCompleted, Is.True);
                Assert.That(confirmCommits, Is.EqualTo(1));

                view.ConfirmRescueButton.onClick.Invoke();
                Assert.That(confirmCommits, Is.EqualTo(1));
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(M11ReverseRescueFacts.TryRead(read, out M11ReverseRescueRecord? saved), Is.True);
                Assert.That(saved.Value.UsedHigherCostFallback, Is.True);
                Assert.That(saved.Value.EchoCallsHeard, Is.Zero);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DisposingHostDuringSuccessfulConfirmationDoesNotThrowOrRepeatSave()
        {
            var root = new GameObject("M11 dispose during confirmation");
            var values = new Dictionary<string, int>();
            SeedThroughM10(values);
            Func<string, int> read = Reader(values);
            int confirmWrites = 0;
            M11ReverseRescueHost host = null;
            Action<string, int> write = (key, value) =>
            {
                values[key] = value;
                if (key == M11ReverseRescueFacts.ConfirmedKey && value == 1)
                {
                    confirmWrites++;
                    host.Dispose();
                }
            };

            try
            {
                host = new M11ReverseRescueHost(
                    root.transform, () => true, () => false, () => false, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                view.ReliefPassButton.onClick.Invoke();
                view.CoverReturneesButton.onClick.Invoke();
                view.FallbackArtisansButton.onClick.Invoke();
                Assert.That(host.Session.Phase, Is.EqualTo(M11Phase.PostRescuePact));

                var confirm = view.ConfirmRescueButton;
                Assert.DoesNotThrow(() => confirm.onClick.Invoke());
                Assert.That(host.IsDisposed, Is.True);
                Assert.That(host.ActiveView, Is.Null);
                Assert.That(host.IsCompleted, Is.True);
                Assert.That(host.Session.IsCompleted, Is.True);
                Assert.That(confirmWrites, Is.EqualTo(1));
                Assert.That(M11ReverseRescueFacts.TryRead(
                    read, out M11ReverseRescueRecord? saved), Is.True);
                Assert.That(saved.Value.UsedHigherCostFallback, Is.True);
                Assert.That(saved.Value.EchoCallsHeard, Is.Zero);
                Assert.DoesNotThrow(() => confirm.onClick.Invoke());
                Assert.That(confirmWrites, Is.EqualTo(1));
                Assert.That(host.TryOpen(), Is.False);
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
            var root = new GameObject("M11 host guards");
            var values = new Dictionary<string, int>();
            SeedThroughM10(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            string prevLang = SoundwellUiLocalization.Language;
            bool atPass = true;
            M11ReverseRescueHost host = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                host = new M11ReverseRescueHost(
                    root.transform, () => atPass, () => false, () => false, read, write);
                Assert.That(host.TryOpen(), Is.True);
                var view = host.ActiveView;
                Assert.That(view.Heading.text, Does.Contain("THREE BELLS AT HUILAN PASS"));
                Assert.That(view.Panel.anchorMin.y, Is.EqualTo(0f));
                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(aspect));

                view.MerchantCaravanButton.onClick.Invoke();
                view.CoverReturneesButton.onClick.Invoke();
                view.FallbackArtisansButton.onClick.Invoke();
                var confirmBtn = view.ConfirmRescueButton;

                atPass = false;
                confirmBtn.onClick.Invoke();
                Assert.That(host.IsCompleted, Is.False);

                atPass = true;
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
