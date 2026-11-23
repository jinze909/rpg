using System;
using System.Collections.Generic;
using GamingIsLove.Makinom;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M12BridgeTruthTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedThroughM11(
            Dictionary<string, int> values,
            M10HanLedgerUse m10HanUse = M10HanLedgerUse.KeepConfidential)
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
        public void SessionRequiresM11GatesM03HistoryAndAllowsPublishWithoutAbsentShenLi()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(M12BridgeTruthSession.TryCreateFromSavedFacts(
                read, false, false, false, false, out var blocked), Is.False);
            Assert.That(blocked, Is.Null);

            SeedThroughM11(values, M10HanLedgerUse.PublishDraft);
            // Cannot promise secrecy if M03 had no prior confession.
            Assert.That(M12BridgeTruthSession.TryCreateFromSavedFacts(
                read, m03PriorConfession: false, m03SecrecyPromised: true,
                shenLiParticipating: false, linSuPresent: false, out _), Is.False);

            Assert.That(M12BridgeTruthSession.TryCreateFromSavedFacts(
                read, m03PriorConfession: false, m03SecrecyPromised: false,
                shenLiParticipating: false, linSuPresent: true, out var session), Is.True);

            // "You said that last time" is blocked when M03 did not ask about the bridge.
            Assert.That(session.TryConfrontLuJun(M12LuConfrontationReply.YouSaidThatLastTime), Is.False);
            Assert.That(session.TryConfrontLuJun(M12LuConfrontationReply.SomeWantNoMoreWar), Is.True);

            // Absent Shen Li cannot be summoned; RC-01 not completed so Lin Su echo blocked.
            Assert.That(session.TryHearStatement(M12StatementFlags.ShenLiHeard), Is.False);
            Assert.That(session.TryHearLinSuEcho(), Is.False);
            Assert.That(session.TryHearStatement(M12StatementFlags.JiangShenHeard), Is.True);
            Assert.That(session.TryHearStatement(M12StatementFlags.HanAndYiLeiHeard), Is.True);
            Assert.That(session.Phase, Is.EqualTo(M12Phase.TruthDisclosureChoice));

            // PublishTruth remains open even without Shen Li.
            Assert.That(session.TryDecideTruthAndCommit(
                M12TruthDisclosure.PublishTruth, read, write), Is.True);
            Assert.That(session.IsCompleted, Is.True);
            Assert.That(session.CompletedRecord.Value.FirstConfessionHeardHere, Is.True);
            Assert.That(session.CompletedRecord.Value.HanQiaoAlreadyOutOfOffice, Is.True);
            Assert.That(session.CompletedRecord.Value.M10PublishedDraftRemainsIrrevocable, Is.True);
        }

        [Test]
        public void PriorM03ConfessionWithSecrecyPromiseTracksBreakageAndWillingKnowersHandover()
        {
            var values = new Dictionary<string, int>();
            SeedThroughM11(values, M10HanLedgerUse.TradeForConcessions);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(M12BridgeTruthSession.TryCreateFromSavedFacts(
                read, m03PriorConfession: true, m03SecrecyPromised: true,
                shenLiParticipating: true, linSuPresent: false, out var session), Is.True);
            Assert.That(session.TryConfrontLuJun(M12LuConfrontationReply.YouSaidThatLastTime), Is.True);

            Assert.That(session.TryHearStatement(M12StatementFlags.HanAndYiLeiHeard), Is.True);
            Assert.That(session.TryHearStatement(M12StatementFlags.JiangShenHeard), Is.True);
            Assert.That(session.Phase, Is.EqualTo(M12Phase.StakeholderStatements));
            Assert.That(session.TryHearStatement(M12StatementFlags.ShenLiHeard), Is.True);
            Assert.That(session.Phase, Is.EqualTo(M12Phase.TruthDisclosureChoice));

            Assert.That(session.TryDecideTruthAndCommit(
                M12TruthDisclosure.WillingKnowersLedger, read, write), Is.True);
            Assert.That(M12BridgeTruthFacts.TryRead(read, out M12BridgeTruthRecord? saved), Is.True);
            Assert.That(saved.Value.FirstConfessionHeardHere, Is.False);
            Assert.That(saved.Value.RequiresDraftHandoverToYiLei, Is.True);
            Assert.That(saved.Value.BreaksLuSecrecyPromise, Is.True);
            Assert.That(saved.Value.HanQiaoAlreadyOutOfOffice, Is.False);
        }

        [Test]
        public void CommitRecoversFromInterruptedWriteAndLostAckAfterConfirm()
        {
            var values = new Dictionary<string, int>();
            SeedThroughM11(values, M10HanLedgerUse.PublishDraft);
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

            Assert.That(M12BridgeTruthSession.TryCreateFromSavedFacts(
                read, true, true, false, false, out var session), Is.True);
            Assert.That(session.TryConfrontLuJun(M12LuConfrontationReply.YouTookTheirRoad), Is.True);
            Assert.That(session.TryHearStatement(M12StatementFlags.JiangShenHeard), Is.True);
            Assert.That(session.TryHearStatement(M12StatementFlags.HanAndYiLeiHeard), Is.True);

            Assert.Throws<InvalidOperationException>(() =>
                session.TryDecideTruthAndCommit(M12TruthDisclosure.SealTruth, read, unstable));
            Assert.That(session.IsCompleted, Is.False);

            Assert.Throws<InvalidOperationException>(() =>
                session.TryDecideTruthAndCommit(M12TruthDisclosure.SealTruth, read, unstable));
            Assert.That(confirmCommits, Is.EqualTo(1));

            Assert.That(session.TryDecideTruthAndCommit(
                M12TruthDisclosure.PublishTruth, read, unstable), Is.False);
            Assert.That(session.TryDecideTruthAndCommit(
                M12TruthDisclosure.SealTruth, read, unstable), Is.True);
            Assert.That(confirmCommits, Is.EqualTo(1));
            Assert.That(session.CompletedRecord.Value.BreaksLuSecrecyPromise, Is.False);
            Assert.That(session.CompletedRecord.Value.M10PublishedDraftRemainsIrrevocable, Is.True);
        }

        [Test]
        public void ForgedM10HanStatusCorruptDataAndUpstreamResetFailClosed()
        {
            var values = new Dictionary<string, int>();
            SeedThroughM11(values, M10HanLedgerUse.PublishDraft);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            // Forging M10HanExposedPublicly = false when M10 published the draft must be rejected.
            Assert.That(M12BridgeTruthFacts.TryRecord(new M12BridgeTruthRecord(
                false, false, M12LuConfrontationReply.SomeWantNoMoreWar,
                false, M12StatementFlags.RequiredBase, false,
                m10HanExposedPublicly: false, M12TruthDisclosure.SealTruth), read, write), Is.False);

            Assert.That(M12BridgeTruthFacts.TryRecord(new M12BridgeTruthRecord(
                false, false, M12LuConfrontationReply.SomeWantNoMoreWar,
                false, M12StatementFlags.RequiredBase, false,
                m10HanExposedPublicly: true, M12TruthDisclosure.SealTruth), read, write), Is.True);

            values[M12BridgeTruthFacts.DisclosureKey] = 99;
            Assert.That(M12BridgeTruthFacts.TryRead(read, out M12BridgeTruthRecord? corrupt), Is.True);
            Assert.That(corrupt, Is.Null);

            values[M12BridgeTruthFacts.DisclosureKey] = (int)M12TruthDisclosure.SealTruth;
            Assert.That(M11ReverseRescueFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(M12BridgeTruthFacts.TryRead(read, out M12BridgeTruthRecord? stale), Is.True);
            Assert.That(stale, Is.Null);

            Assert.That(M12BridgeTruthFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(values[M12BridgeTruthFacts.ConfirmedKey], Is.Zero);
            Assert.That(M12BridgeTruthFacts.TryRead(null, out _), Is.False);
            Assert.That(M12BridgeTruthFacts.TryResetForNewVoyage(null), Is.False);
            if (!Maki.Initialized || Maki.Game?.Variables == null)
                Assert.That(M12BridgeTruthFacts.TryRead(out _), Is.False);
        }

        [Test]
        public void TornRC01CannotBeTreatedAsUnplayedOrGrantLinSuEcho()
        {
            var values = new Dictionary<string, int>();
            SeedThroughM11(values);
            Func<string, int> read = Reader(values);
            int writes = 0;
            Action<string, int> write = (key, value) =>
            {
                values[key] = value;
                if (key == M12BridgeTruthFacts.ConfirmedKey)
                    writes++;
            };

            // An optional RC-01 case that has genuinely never been played is safe.
            Assert.That(M12BridgeTruthSession.TryCreateFromSavedFacts(
                read, false, false, false, false, out var clean), Is.True);
            Assert.That(clean.LinSuEligible, Is.False);

            var ordinary = new M12BridgeTruthRecord(
                false, false, M12LuConfrontationReply.SomeWantNoMoreWar,
                false, M12StatementFlags.RequiredBase, false,
                m10HanExposedPublicly: false, M12TruthDisclosure.SealTruth);
            var fabricatedEcho = new M12BridgeTruthRecord(
                false, false, M12LuConfrontationReply.SomeWantNoMoreWar,
                false, M12StatementFlags.RequiredBase, true,
                m10HanExposedPublicly: false, M12TruthDisclosure.SealTruth);
            Assert.That(M12BridgeTruthFacts.TryRecord(fabricatedEcho, read, write),
                Is.False);

            // RC-01 claims completion, but the saved choice is corrupt.
            // M12 may not erase its outcome by assuming the case was skipped.
            values[RC01ChenYanFacts.ConfirmedKey] = 1;
            values[RC01ChenYanFacts.OutcomeKey] = 99;
            Assert.That(RC01ChenYanFacts.TryRead(read, out _), Is.True);
            Assert.That(M12BridgeTruthSession.TryCreateFromSavedFacts(
                read, false, false, false, false, out _), Is.False);
            Assert.That(M12BridgeTruthFacts.TryRecord(ordinary, read, write),
                Is.False);

            // A mid-write intent without confirmation is not a clean absence.
            values[RC01ChenYanFacts.ConfirmedKey] = 0;
            values[RC01ChenYanFacts.OutcomeKey] = 0;
            values[RC01ChenYanFacts.PendingSignatureKey] = 1011;
            Assert.That(RC01ChenYanFacts.TryRead(read, out _), Is.False);
            Assert.That(M12BridgeTruthSession.TryCreateFromSavedFacts(
                read, false, false, false, false, out _), Is.False);
            Assert.That(M12BridgeTruthFacts.TryRecord(ordinary, read, write),
                Is.False);
            Assert.That(writes, Is.Zero);

            // Only the clean repaired RC-01 state can resume M12 and persist
            // the ordinary truth choice without claiming Lin Su's echo.
            values[RC01ChenYanFacts.PendingSignatureKey] = 0;
            Assert.That(M12BridgeTruthSession.TryCreateFromSavedFacts(
                read, false, false, false, false, out _), Is.True);
            Assert.That(M12BridgeTruthFacts.TryRecord(ordinary, read, write),
                Is.True);
            Assert.That(writes, Is.EqualTo(1));
        }
    }
}
