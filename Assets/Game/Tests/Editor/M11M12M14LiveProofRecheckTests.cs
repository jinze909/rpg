using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M11M12M14LiveProofRecheckTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedThroughM10(Dictionary<string, int> values)
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
                20, 23, M09BerthExchangeFlags.HanQiaoCivilianVoyage,
                M09RuiyaOutcome.Rescued, false, false), read, write), Is.True);
            Assert.That(M10AssemblyFacts.TryRecord(new M10AssemblyRecord(
                true, M10RosterAccuracy.VerifiedFirstPass, M10BattleReportSource.CarriedFromM02,
                M10HanLedgerUse.PublishDraft, M10SupplyContractStatus.SignedFullCoverage,
                false, false, true), read, write), Is.True);
        }

        [Test]
        public void M11HostBlocksCommitWhenShenLiOrOulinPresenceChangesMidEncounter()
        {
            var root = new GameObject("M11 live proof recheck");
            var values = new Dictionary<string, int>();
            SeedThroughM10(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            bool shenLi = true;
            bool oulin = true;

            try
            {
                using (var host = new M11ReverseRescueHost(
                    root.transform, () => true, () => shenLi, () => oulin, read, write))
                {
                    Assert.That(host.TryOpen(), Is.True);
                    var view = host.ActiveView;
                    view.LandGateButton.onClick.Invoke();
                    view.CoverReturneesButton.onClick.Invoke();
                    view.HearFogCallButton.onClick.Invoke();
                    view.HearFogCallButton.onClick.Invoke();
                    view.HearFogCallButton.onClick.Invoke();
                    view.AnswerPresentButton.onClick.Invoke();
                    view.MutualDivisionButton.onClick.Invoke();

                    shenLi = false;
                    view.ConfirmRescueButton.onClick.Invoke();
                    Assert.That(host.IsCompleted, Is.False);

                    shenLi = true;
                    oulin = false;
                    view.ConfirmRescueButton.onClick.Invoke();
                    Assert.That(host.IsCompleted, Is.False);

                    oulin = true;
                    view.ConfirmRescueButton.onClick.Invoke();
                    Assert.That(host.IsCompleted, Is.True);
                    Assert.That(host.ActiveView, Is.Null);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void M12HostBlocksCommitWhenLiveParticipantsOrM03ContextChangeMidEncounter()
        {
            var root = new GameObject("M12 live proof recheck");
            var values = new Dictionary<string, int>();
            SeedThroughM10(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            Assert.That(M11ReverseRescueFacts.TryRecord(new M11ReverseRescueRecord(
                true, M11PassEntryMethod.LandGateNegotiation, true,
                M11ThirdSquadConscriptionStance.NotInPass,
                M11RescueProvider.ShenLiShallowBoat, 3, true,
                M11ShenDivisionPact.MutualConsultAndDivision), read, write), Is.True);

            bool priorConfession = true;
            bool secrecyPromised = false;
            bool shenLi = true;
            bool linSu = true;

            try
            {
                using (var host = new M12BridgeTruthHost(
                    root.transform, () => true,
                    () => priorConfession, () => secrecyPromised,
                    () => shenLi, () => linSu, read, write))
                {
                    Assert.That(host.TryOpen(), Is.True);
                    var view = host.ActiveView;
                    view.YouSaidThatLastTimeButton.onClick.Invoke();
                    view.HearHanAndYiLeiButton.onClick.Invoke();
                    view.HearJiangShenButton.onClick.Invoke();
                    view.HearShenLiButton.onClick.Invoke();
                    Assert.That(host.Session.Phase, Is.EqualTo(M12Phase.TruthDisclosureChoice));

                    shenLi = false;
                    view.PublishTruthButton.onClick.Invoke();
                    Assert.That(host.IsCompleted, Is.False);

                    shenLi = true;
                    linSu = false;
                    view.PublishTruthButton.onClick.Invoke();
                    Assert.That(host.IsCompleted, Is.False);

                    linSu = true;
                    priorConfession = false;
                    view.PublishTruthButton.onClick.Invoke();
                    Assert.That(host.IsCompleted, Is.False);

                    priorConfession = true;
                    secrecyPromised = true;
                    view.PublishTruthButton.onClick.Invoke();
                    Assert.That(host.IsCompleted, Is.False);

                    secrecyPromised = false;
                    view.PublishTruthButton.onClick.Invoke();
                    Assert.That(host.IsCompleted, Is.True);
                    Assert.That(host.ActiveView, Is.Null);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void M14HostBlocksCommitWhenRouteABOrCLiveProofIsRevokedBeforeSettlement()
        {
            var root = new GameObject("M14 live proof recheck");
            try
            {
                // Route A: revoke credentials or port guarantee before final commit.
                {
                    var values = new Dictionary<string, int>();
                    Func<string, int> read = Reader(values);
                    Action<string, int> write = (k, v) => values[k] = v;
                    Assert.That(M13DepartureRiskFacts.TryRecordDeparture(new M13DepartureRecord(
                        M13ApproachRoute.RouteASupervisedCivilian, true, false, false, false),
                        read, write), Is.True);
                    bool credentials = true;
                    bool portGuarantee = true;
                    using (var host = new M14FinalPassageHost(
                        root.transform, () => true, () => credentials, () => portGuarantee,
                        () => false, () => false, () => false,
                        () => new M14CivilianCounts(16, 4, 2), read, write))
                    {
                        Assert.That(host.TryOpen(), Is.True);
                        var view = host.ActiveView;
                        view.DetainLuJunButton.onClick.Invoke();
                        view.VerifyCivilianExitButton.onClick.Invoke();

                        credentials = false;
                        view.SettleAyuanSafeButton.onClick.Invoke();
                        Assert.That(host.IsCompleted, Is.False);

                        credentials = true;
                        portGuarantee = false;
                        view.SettleAyuanSafeButton.onClick.Invoke();
                        Assert.That(host.IsCompleted, Is.False);

                        portGuarantee = true;
                        view.SettleAyuanSafeButton.onClick.Invoke();
                        Assert.That(host.IsCompleted, Is.True);
                    }
                }

                // Route B: revoke three open-door bells before final commit.
                {
                    var values = new Dictionary<string, int>();
                    Func<string, int> read = Reader(values);
                    Action<string, int> write = (k, v) => values[k] = v;
                    Assert.That(M13DepartureRiskFacts.TryRecordDeparture(new M13DepartureRecord(
                        M13ApproachRoute.RouteBBellTowerAssault, true, false, false, true),
                        read, write), Is.True);
                    bool threeBells = true;
                    using (var host = new M14FinalPassageHost(
                        root.transform, () => true, () => false, () => false,
                        () => threeBells, () => false, () => false,
                        () => new M14CivilianCounts(14, 2, 0), read, write))
                    {
                        Assert.That(host.TryOpen(), Is.True);
                        var view = host.ActiveView;
                        view.PursueWarehousesButton.onClick.Invoke();
                        view.VerifyCivilianExitButton.onClick.Invoke();

                        threeBells = false;
                        view.SettleAyuanSafeButton.onClick.Invoke();
                        Assert.That(host.IsCompleted, Is.False);

                        threeBells = true;
                        view.SettleAyuanSafeButton.onClick.Invoke();
                        Assert.That(host.IsCompleted, Is.True);
                    }
                }

                // Route C: revoke fog relay or fog mastery bonus before final commit.
                {
                    var values = new Dictionary<string, int>();
                    Func<string, int> read = Reader(values);
                    Action<string, int> write = (k, v) => values[k] = v;
                    Assert.That(M13DepartureRiskFacts.TryRecordDeparture(new M13DepartureRecord(
                        M13ApproachRoute.RouteCOldCanalNightRun, true, false, false, false),
                        read, write), Is.True);
                    bool fogRelay = true;
                    bool fogMastery = true;
                    using (var host = new M14FinalPassageHost(
                        root.transform, () => true, () => false, () => false,
                        () => false, () => fogRelay, () => fogMastery,
                        () => new M14CivilianCounts(20, 0, 1), read, write))
                    {
                        Assert.That(host.TryOpen(), Is.True);
                        var view = host.ActiveView;
                        view.CompleteFogRelayButton.onClick.Invoke();
                        view.VerifyCivilianExitButton.onClick.Invoke();

                        fogRelay = false;
                        view.SettleAyuanSafeButton.onClick.Invoke();
                        Assert.That(host.IsCompleted, Is.False);

                        fogRelay = true;
                        fogMastery = false;
                        view.SettleAyuanSafeButton.onClick.Invoke();
                        Assert.That(host.IsCompleted, Is.False);

                        fogMastery = true;
                        view.SettleAyuanSafeButton.onClick.Invoke();
                        Assert.That(host.IsCompleted, Is.True);
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
