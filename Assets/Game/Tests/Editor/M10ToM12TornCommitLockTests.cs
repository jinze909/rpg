using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M10ToM12TornCommitLockTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedThroughM09(Dictionary<string, int> values)
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
            Assert.That(M09BerthTableFacts.TryRecord(new M09BerthTableRecord(
                20, 23, M09BerthExchangeFlags.HanQiaoCivilianVoyage,
                M09RuiyaOutcome.Rescued, false, false), read, write), Is.True);
        }

        [Test]
        public void M10InterruptedCommitLocksOutCompetingChoicesAndAllowsIdenticalRetry()
        {
            var values = new Dictionary<string, int>();
            SeedThroughM09(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var chosen = new M10AssemblyRecord(
                true, M10RosterAccuracy.VerifiedFirstPass,
                M10BattleReportSource.CarriedFromM02,
                M10HanLedgerUse.PublishDraft,
                M10SupplyContractStatus.SignedFullCoverage,
                false, false, true);
            var competing = new M10AssemblyRecord(
                true, M10RosterAccuracy.VerifiedFirstPass,
                M10BattleReportSource.CarriedFromM02,
                M10HanLedgerUse.KeepConfidential,
                M10SupplyContractStatus.PartialGapToM13,
                false, false, true);

            Assert.Throws<InvalidOperationException>(() => M10AssemblyFacts.TryRecord(
                chosen, read, (k, v) =>
                {
                    if (k == M10AssemblyFacts.ConfirmedKey)
                        throw new InvalidOperationException("Interrupted before M10 confirm");
                    values[k] = v;
                }));

            Assert.That(M10AssemblyFacts.TryRead(read, out M10AssemblyRecord? pending), Is.True);
            Assert.That(pending.HasValue, Is.False);
            Assert.That(M10AssemblyFacts.CanRecord(competing, read), Is.False);
            Assert.That(M10AssemblyFacts.TryRecord(competing, read, write), Is.False);

            Assert.That(M10AssemblyFacts.TryRecord(chosen, read, write), Is.True);
            Assert.That(M10AssemblyFacts.TryRead(read, out M10AssemblyRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.HanLedgerUse, Is.EqualTo(M10HanLedgerUse.PublishDraft));
            Assert.That(saved.Value.SupplyContract,
                Is.EqualTo(M10SupplyContractStatus.SignedFullCoverage));
        }

        [Test]
        public void M11InterruptedCommitLocksOutCompetingRescueOutcomeAndAllowsIdenticalRetry()
        {
            var values = new Dictionary<string, int>();
            SeedThroughM09(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(M10AssemblyFacts.TryRecord(new M10AssemblyRecord(
                true, M10RosterAccuracy.VerifiedFirstPass,
                M10BattleReportSource.CarriedFromM02,
                M10HanLedgerUse.KeepConfidential,
                M10SupplyContractStatus.SignedFullCoverage,
                false, false, true), read, write), Is.True);

            var chosen = new M11ReverseRescueRecord(
                true, M11PassEntryMethod.LandGateNegotiation, true,
                M11ThirdSquadConscriptionStance.NotInPass,
                M11RescueProvider.ShenLiShallowBoat, 3, true,
                M11ShenDivisionPact.MutualConsultAndDivision);
            var competing = new M11ReverseRescueRecord(
                true, M11PassEntryMethod.MerchantCaravanSurvey, true,
                M11ThirdSquadConscriptionStance.NotInPass,
                M11RescueProvider.FallbackPaidArtisans, 0, false,
                M11ShenDivisionPact.NoneShenAbsent);

            Assert.Throws<InvalidOperationException>(() => M11ReverseRescueFacts.TryRecord(
                chosen, read, (k, v) =>
                {
                    if (k == M11ReverseRescueFacts.ConfirmedKey)
                        throw new InvalidOperationException("Interrupted before M11 confirm");
                    values[k] = v;
                }));

            Assert.That(M11ReverseRescueFacts.CanRecord(competing, read), Is.False);
            Assert.That(M11ReverseRescueFacts.TryRecord(competing, read, write), Is.False);

            Assert.That(M11ReverseRescueFacts.TryRecord(chosen, read, write), Is.True);
            Assert.That(M11ReverseRescueFacts.TryRead(read, out M11ReverseRescueRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.RescueProvider, Is.EqualTo(M11RescueProvider.ShenLiShallowBoat));
            Assert.That(saved.Value.DivisionPact,
                Is.EqualTo(M11ShenDivisionPact.MutualConsultAndDivision));
        }

        [Test]
        public void M12InterruptedCommitLocksOutCompetingDisclosureAndAllowsIdenticalRetry()
        {
            var values = new Dictionary<string, int>();
            SeedThroughM09(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(M10AssemblyFacts.TryRecord(new M10AssemblyRecord(
                true, M10RosterAccuracy.VerifiedFirstPass,
                M10BattleReportSource.CarriedFromM02,
                M10HanLedgerUse.KeepConfidential,
                M10SupplyContractStatus.SignedFullCoverage,
                false, false, true), read, write), Is.True);
            Assert.That(M11ReverseRescueFacts.TryRecord(new M11ReverseRescueRecord(
                true, M11PassEntryMethod.LandGateNegotiation, true,
                M11ThirdSquadConscriptionStance.NotInPass,
                M11RescueProvider.FallbackPaidArtisans, 0, false,
                M11ShenDivisionPact.NoneShenAbsent), read, write), Is.True);

            var chosen = new M12BridgeTruthRecord(
                m03PriorConfession: true,
                m03SecrecyPromised: true,
                luReply: M12LuConfrontationReply.YouSaidThatLastTime,
                shenLiParticipating: false,
                statementsHeard: M12StatementFlags.RequiredBase,
                linSuEchoHeard: false,
                m10HanExposedPublicly: false,
                disclosure: M12TruthDisclosure.PublishTruth);
            var competing = new M12BridgeTruthRecord(
                m03PriorConfession: true,
                m03SecrecyPromised: true,
                luReply: M12LuConfrontationReply.YouSaidThatLastTime,
                shenLiParticipating: false,
                statementsHeard: M12StatementFlags.RequiredBase,
                linSuEchoHeard: false,
                m10HanExposedPublicly: false,
                disclosure: M12TruthDisclosure.SealTruth);

            Assert.Throws<InvalidOperationException>(() => M12BridgeTruthFacts.TryRecord(
                chosen, read, (k, v) =>
                {
                    if (k == M12BridgeTruthFacts.ConfirmedKey)
                        throw new InvalidOperationException("Interrupted before M12 confirm");
                    values[k] = v;
                }));

            Assert.That(M12BridgeTruthFacts.CanRecord(competing, read), Is.False);
            Assert.That(M12BridgeTruthFacts.TryRecord(competing, read, write), Is.False);

            Assert.That(M12BridgeTruthFacts.TryRecord(chosen, read, write), Is.True);
            Assert.That(M12BridgeTruthFacts.TryRead(read, out M12BridgeTruthRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.Disclosure, Is.EqualTo(M12TruthDisclosure.PublishTruth));
        }
    }
}
