using System;
using System.Collections.Generic;
using GamingIsLove.Makinom;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RC05ThirdSquadTests
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
        public void CaseRequiresM08NoticeRollCallObservationAndBoatTrialBeforeMillMeeting()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(RC05ThirdSquadSession.TryCreateFromSavedFacts(
                read, false, out var blocked), Is.False);
            Assert.That(blocked, Is.Null);

            SeedM08RosterCasesAvailable(values);
            Assert.That(RC05ThirdSquadSession.TryCreateFromSavedFacts(
                read, oldWomanStoneTriggered: false, out var session), Is.True);
            Assert.That(session.TryVerifySaltGrainBoatDelivery(true), Is.False);
            Assert.That(session.TryObserveMountainPassRollCall(), Is.True);
            Assert.That(session.TryVerifySaltGrainBoatDelivery(false), Is.False);
            Assert.That(session.TryVerifySaltGrainBoatDelivery(true), Is.True);
            Assert.That(session.Stage, Is.EqualTo(RC05Stage.MillMeeting));

            // Cannot mention M00 stone callback when it was not triggered.
            Assert.That(session.TryMentionOldWomanAtStele(), Is.False);
            Assert.That(session.TrySettleResolution(RC05Resolution.CivilianPardon, read, write), Is.False);
        }

        [Test]
        public void RealZhengYuanRequiresRosterAndTestimonyVerificationAndPardonFlagsM10Supply()
        {
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(RC05ThirdSquadSession.TryCreateFromSavedFacts(
                read, oldWomanStoneTriggered: true, out var session), Is.True);
            Assert.That(session.TryObserveMountainPassRollCall(), Is.True);
            Assert.That(session.TryVerifySaltGrainBoatDelivery(true), Is.True);
            Assert.That(session.TryMentionOldWomanAtStele(), Is.True);
            Assert.That(session.TryMentionOldWomanAtStele(), Is.False);

            Assert.That(session.TryVerifyRealZhengYuan(true, false), Is.False);
            Assert.That(session.TryVerifyRealZhengYuan(false, true), Is.False);
            Assert.That(session.TryVerifyRealZhengYuan(true, true), Is.True);

            Assert.That(session.TryProceedToThreePartyTalks(skirmishTriggered: false), Is.True);
            Assert.That(session.TrySettleResolution(RC05Resolution.CivilianPardon, read, write), Is.True);

            Assert.That(RC05ThirdSquadFacts.TryRead(read, out RC05ThirdSquadRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.NeedsM10SupplyContract, Is.True);
            Assert.That(saved.Value.RealZhengYuanVerified, Is.True);
            Assert.That(saved.Value.OldWomanStoneMentioned, Is.True);
            Assert.That(saved.Value.SkirmishOccurred, Is.False);
        }

        [Test]
        public void InterruptedWriteAndCommitThenThrowRecoverWithoutDuplicateWrites()
        {
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values);
            Func<string, int> read = Reader(values);
            int preCommitFails = 0;
            int confirmCommits = 0;
            Action<string, int> unstable = (k, v) =>
            {
                if (k == RC05ThirdSquadFacts.ResolutionKey && preCommitFails++ == 0)
                    throw new InvalidOperationException("Interrupted before confirm");
                values[k] = v;
                if (k == RC05ThirdSquadFacts.ConfirmedKey && v == 1 && ++confirmCommits == 1)
                    throw new InvalidOperationException("Lost ack after confirm");
            };

            Assert.That(RC05ThirdSquadSession.TryCreateFromSavedFacts(read, false, out var session), Is.True);
            Assert.That(session.TryObserveMountainPassRollCall(), Is.True);
            Assert.That(session.TryVerifySaltGrainBoatDelivery(true), Is.True);
            Assert.That(session.TryProceedToThreePartyTalks(skirmishTriggered: true), Is.True);

            Assert.Throws<InvalidOperationException>(() =>
                session.TrySettleResolution(RC05Resolution.ContinueSmuggling, read, unstable));
            Assert.That(session.IsResolved, Is.False);

            Assert.Throws<InvalidOperationException>(() =>
                session.TrySettleResolution(RC05Resolution.ContinueSmuggling, read, unstable));
            Assert.That(confirmCommits, Is.EqualTo(1));

            Assert.That(session.TrySettleResolution(RC05Resolution.EnlistUnderLuJun, read, unstable), Is.False);
            Assert.That(session.TrySettleResolution(RC05Resolution.ContinueSmuggling, read, unstable), Is.True);
            Assert.That(confirmCommits, Is.EqualTo(1));
            Assert.That(session.CompletedRecord.Value.HarmsHanQiaoTaxRoute, Is.True);
            Assert.That(session.CompletedRecord.Value.SquadRemainsOnMountain, Is.True);
            Assert.That(session.CompletedRecord.Value.SkirmishOccurred, Is.True);
        }

        [Test]
        public void CorruptOrResetUpstreamFactsInvalidateReadbackAndNullDelegatesFailClosed()
        {
            var values = new Dictionary<string, int>();
            SeedM08RosterCasesAvailable(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(RC05ThirdSquadFacts.TryRecord(
                new RC05ThirdSquadRecord(
                    trialDeliveryVerified: false,
                    RC05Resolution.EnlistUnderLuJun,
                    skirmishOccurred: false,
                    realZhengYuanVerified: false,
                    oldWomanStoneMentioned: false),
                read, write), Is.False);

            Assert.That(RC05ThirdSquadFacts.TryRecord(
                new RC05ThirdSquadRecord(
                    trialDeliveryVerified: true,
                    RC05Resolution.EnlistUnderLuJun,
                    skirmishOccurred: false,
                    realZhengYuanVerified: false,
                    oldWomanStoneMentioned: false),
                read, write), Is.True);

            values[RC05ThirdSquadFacts.ResolutionKey] = 99;
            Assert.That(RC05ThirdSquadFacts.TryRead(read, out RC05ThirdSquadRecord? corrupt), Is.True);
            Assert.That(corrupt, Is.Null);

            values[RC05ThirdSquadFacts.ResolutionKey] = (int)RC05Resolution.EnlistUnderLuJun;
            Assert.That(M08FirstWorldNoticeFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(RC05ThirdSquadFacts.TryRead(read, out RC05ThirdSquadRecord? stale), Is.True);
            Assert.That(stale, Is.Null);

            Assert.That(RC05ThirdSquadFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(values[RC05ThirdSquadFacts.ConfirmedKey], Is.Zero);
            Assert.That(RC05ThirdSquadFacts.TryRead(null, out _), Is.False);
            Assert.That(RC05ThirdSquadFacts.TryResetForNewVoyage(null), Is.False);
            if (!Maki.Initialized || Maki.Game?.Variables == null)
                Assert.That(RC05ThirdSquadFacts.TryRead(out _), Is.False);
        }
    }
}
