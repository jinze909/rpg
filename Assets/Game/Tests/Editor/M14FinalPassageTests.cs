using System;
using System.Collections.Generic;
using GamingIsLove.Makinom;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M14FinalPassageTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedM13Departure(
            Dictionary<string, int> values,
            M13ApproachRoute route,
            bool safetyPrepComplete,
            bool qiWarning = false,
            bool highRiskConsent = false,
            bool threeBellSignal = false)
        {
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            Assert.That(M13DepartureRiskFacts.TryRecordDeparture(new M13DepartureRecord(
                route, safetyPrepComplete, qiWarning, highRiskConsent, threeBellSignal),
                read, write), Is.True);
        }

        [Test]
        public void RouteARequiresM13CredentialsSingleLuDispositionAndGuaranteesAyuanSafe()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(M14FinalPassageSession.TryCreateFromSavedFacts(read, out var blocked), Is.False);
            Assert.That(blocked, Is.Null);

            SeedM13Departure(values, M13ApproachRoute.RouteASupervisedCivilian, safetyPrepComplete: true);
            Assert.That(M14FinalPassageSession.TryCreateFromSavedFacts(read, out var session), Is.True);

            // Wrong route action or missing credentials must fail.
            Assert.That(session.TryResolveRouteBBellTowerAssault(true, M14BorderForceAction.StopAtEscort), Is.False);
            Assert.That(session.TryResolveRouteASupervisedSquare(
                validSupervisionCredentials: false, validPortGuarantee: true,
                M14LuJunDisposition.RelievedOfCommand), Is.False);

            Assert.That(session.TryResolveRouteASupervisedSquare(
                validSupervisionCredentials: true, validPortGuarantee: true,
                M14LuJunDisposition.RelievedOfCommand), Is.True);
            Assert.That(session.Phase, Is.EqualTo(M14Phase.CivilianExitVerification));

            // Zero total civilians cannot pass verification.
            Assert.That(session.TryVerifyCivilianExit(0, 0, 0), Is.False);
            Assert.That(session.TryVerifyCivilianExit(18, 4, 3), Is.True);

            // Route A guarantees A Yuan Safe.
            Assert.That(session.TrySettleAyuanAndCommit(M14AyuanOutcome.Lost, read, write), Is.False);
            Assert.That(session.TrySettleAyuanAndCommit(M14AyuanOutcome.Safe, read, write), Is.True);

            Assert.That(session.IsCompleted, Is.True);
            Assert.That(session.CompletedRecord.Value.UnlocksMainEnding, Is.True);
            Assert.That(session.CompletedRecord.Value.PassageOutcome,
                Is.EqualTo(M14PassageOutcome.RouteASupervisedCivilianEstablished));
            Assert.That(session.CompletedRecord.Value.TotalAccountedCivilians, Is.EqualTo(25));
        }

        [Test]
        public void RouteBHighRiskConsentAllowsNonSafeAyuanAndRecordsBorderForceAction()
        {
            var values = new Dictionary<string, int>();
            SeedM13Departure(values, M13ApproachRoute.RouteBBellTowerAssault,
                safetyPrepComplete: false, qiWarning: true, highRiskConsent: true, threeBellSignal: true);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(M14FinalPassageSession.TryCreateFromSavedFacts(read, out var session), Is.True);
            Assert.That(session.TryResolveRouteBBellTowerAssault(
                threeOpenDoorBellsRung: false, M14BorderForceAction.StopAtEscort), Is.False);
            Assert.That(session.TryResolveRouteBBellTowerAssault(
                threeOpenDoorBellsRung: true, M14BorderForceAction.PursueAndSeizeWarehouses), Is.True);
            Assert.That(session.TryVerifyCivilianExit(14, 2, 0), Is.True);
            Assert.That(session.TrySettleAyuanAndCommit(
                M14AyuanOutcome.SeverelyInjuredRetired, read, write), Is.True);

            Assert.That(M14FinalPassageFacts.TryRead(read, out M14FinalPassageRecord? saved), Is.True);
            Assert.That(saved.Value.BorderForceAction, Is.EqualTo(M14BorderForceAction.PursueAndSeizeWarehouses));
            Assert.That(saved.Value.AyuanOutcome, Is.EqualTo(M14AyuanOutcome.SeverelyInjuredRetired));
            Assert.That(M13DepartureRiskFacts.TryReadAyuanOutcome(read, out M14AyuanOutcome? m13Ayuan), Is.True);
            Assert.That(m13Ayuan, Is.EqualTo(M14AyuanOutcome.SeverelyInjuredRetired));
        }

        [Test]
        public void RouteCCommitRecoversFromInterruptedWriteAndLostAckAfterConfirm()
        {
            var values = new Dictionary<string, int>();
            SeedM13Departure(values, M13ApproachRoute.RouteCOldCanalNightRun, safetyPrepComplete: true);
            Func<string, int> read = Reader(values);
            int preCommitFails = 0;
            int confirmCommits = 0;
            Action<string, int> unstable = (k, v) =>
            {
                if (k == M14FinalPassageFacts.PassageOutcomeKey && preCommitFails++ == 0)
                    throw new InvalidOperationException("Interrupted before confirm");
                values[k] = v;
                if (k == M14FinalPassageFacts.ConfirmedKey && v == 1 && ++confirmCommits == 1)
                    throw new InvalidOperationException("Lost ack after confirm");
            };

            Assert.That(M14FinalPassageSession.TryCreateFromSavedFacts(read, out var session), Is.True);
            Assert.That(session.TryResolveRouteCOldCanalRun(
                fogRelayCompleted: true, fogMasteryBonusAvailable: true), Is.True);
            Assert.That(session.TryVerifyCivilianExit(20, 0, 2), Is.True);

            Assert.Throws<InvalidOperationException>(() =>
                session.TrySettleAyuanAndCommit(M14AyuanOutcome.Safe, read, unstable));
            Assert.That(session.IsCompleted, Is.False);

            Assert.Throws<InvalidOperationException>(() =>
                session.TrySettleAyuanAndCommit(M14AyuanOutcome.Safe, read, unstable));
            Assert.That(confirmCommits, Is.EqualTo(1));

            Assert.That(session.TrySettleAyuanAndCommit(M14AyuanOutcome.Safe, read, unstable), Is.True);
            Assert.That(confirmCommits, Is.EqualTo(1));
            Assert.That(session.CompletedRecord.Value.FogMasteryBonusApplied, Is.True);
        }

        [Test]
        public void MismatchedRouteOutcomeCorruptDataAndUpstreamResetFailClosed()
        {
            var values = new Dictionary<string, int>();
            SeedM13Departure(values, M13ApproachRoute.RouteCOldCanalNightRun, safetyPrepComplete: true);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            // Cannot claim Route A ending while M13 departed on Route C.
            Assert.That(M14FinalPassageFacts.TryRecord(new M14FinalPassageRecord(
                M13ApproachRoute.RouteCOldCanalNightRun,
                M14PassageOutcome.RouteASupervisedCivilianEstablished,
                true, 10, 0, 0,
                M14BorderForceAction.NotRouteB, M14LuJunDisposition.NotSettledInRouteA,
                M14AyuanOutcome.Safe, false), read, write), Is.False);

            Assert.That(M14FinalPassageFacts.TryRecord(new M14FinalPassageRecord(
                M13ApproachRoute.RouteCOldCanalNightRun,
                M14PassageOutcome.RouteCNeutralBypassEstablished,
                true, 10, 0, 0,
                M14BorderForceAction.NotRouteB, M14LuJunDisposition.NotSettledInRouteA,
                M14AyuanOutcome.Safe, false), read, write), Is.True);

            values[M14FinalPassageFacts.PassageOutcomeKey] = 99;
            Assert.That(M14FinalPassageFacts.TryRead(read, out M14FinalPassageRecord? corrupt), Is.True);
            Assert.That(corrupt, Is.Null);

            values[M14FinalPassageFacts.PassageOutcomeKey] =
                (int)M14PassageOutcome.RouteCNeutralBypassEstablished;
            Assert.That(M13DepartureRiskFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(M14FinalPassageFacts.TryRead(read, out M14FinalPassageRecord? stale), Is.True);
            Assert.That(stale, Is.Null);

            Assert.That(M14FinalPassageFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(values[M14FinalPassageFacts.ConfirmedKey], Is.Zero);
            Assert.That(M14FinalPassageFacts.TryRead(null, out _), Is.False);
            Assert.That(M14FinalPassageFacts.TryResetForNewVoyage(null), Is.False);
            if (!Maki.Initialized || Maki.Game?.Variables == null)
                Assert.That(M14FinalPassageFacts.TryRead(out _), Is.False);
        }
    }
}
