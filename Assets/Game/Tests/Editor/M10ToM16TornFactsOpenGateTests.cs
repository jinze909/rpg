using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    /// <summary>
    /// Verifies that M10, M11, M12, M14, and M16 hosts refuse to open a fresh
    /// encounter when partially persisted (torn) payload keys remain with
    /// ConfirmedKey == 0, and open cleanly once explicitly reset.
    /// </summary>
    public sealed class M10ToM16TornFactsOpenGateTests
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
        public void AllFiveAct3HostsRefuseToOpenOnTornPayloadKeysUntilReset()
        {
            var root = new GameObject("Act3TornFactsGateRoot");
            var values = new Dictionary<string, int>();
            SeedThroughM09(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                // M10 torn HanLedgerUseKey blocks open until reset.
                values[M10AssemblyFacts.HanLedgerUseKey] = (int)M10HanLedgerUse.PublishDraft;
                using (var m10Host = new M10AssemblyHost(
                    root.transform, () => true, () => true, () => true, read, write))
                {
                    Assert.That(m10Host.TryOpen(), Is.False);
                    Assert.That(M10AssemblyFacts.TryResetForNewVoyage(write), Is.True);
                    Assert.That(m10Host.TryOpen(), Is.True);
                }

                Assert.That(M10AssemblyFacts.TryRecord(new M10AssemblyRecord(
                    true, M10RosterAccuracy.VerifiedFirstPass, M10BattleReportSource.CarriedFromM02,
                    M10HanLedgerUse.KeepConfidential, M10SupplyContractStatus.SignedFullCoverage,
                    false, false, true), read, write), Is.True);

                // M11 torn RescueProviderKey blocks open until reset.
                values[M11ReverseRescueFacts.RescueProviderKey] = (int)M11RescueProvider.ShenLiShallowBoat;
                using (var m11Host = new M11ReverseRescueHost(
                    root.transform, () => true, () => false, () => false, read, write))
                {
                    Assert.That(m11Host.TryOpen(), Is.False);
                    Assert.That(M11ReverseRescueFacts.TryResetForNewVoyage(write), Is.True);
                    Assert.That(m11Host.TryOpen(), Is.True);
                }

                Assert.That(M11ReverseRescueFacts.TryRecord(new M11ReverseRescueRecord(
                    true, M11PassEntryMethod.LandGateNegotiation, true,
                    M11ThirdSquadConscriptionStance.NotInPass,
                    M11RescueProvider.FallbackPaidArtisans, 0, false,
                    M11ShenDivisionPact.NoneShenAbsent), read, write), Is.True);

                // M12 torn DisclosureKey blocks open until reset.
                values[M12BridgeTruthFacts.DisclosureKey] = (int)M12TruthDisclosure.PublishTruth;
                using (var m12Host = new M12BridgeTruthHost(
                    root.transform, () => true, () => true, () => false,
                    () => false, () => false, read, write))
                {
                    Assert.That(m12Host.TryOpen(), Is.False);
                    Assert.That(M12BridgeTruthFacts.TryResetForNewVoyage(write), Is.True);
                    Assert.That(m12Host.TryOpen(), Is.True);
                }

                Assert.That(M13DepartureRiskFacts.TryRecordDeparture(new M13DepartureRecord(
                    M13ApproachRoute.RouteASupervisedCivilian, true, false, false, false),
                    read, write), Is.True);

                // M14 torn PassageOutcomeKey blocks open until reset.
                values[M14FinalPassageFacts.PassageOutcomeKey] =
                    (int)M14PassageOutcome.RouteASupervisedCivilianEstablished;
                using (var m14Host = new M14FinalPassageHost(
                    root.transform, () => true, () => true, () => true, () => true,
                    () => true, () => false, () => new M14CivilianCounts(12, 2, 1), read, write))
                {
                    Assert.That(m14Host.TryOpen(), Is.False);
                    Assert.That(M14FinalPassageFacts.TryResetForNewVoyage(write), Is.True);
                    Assert.That(m14Host.TryOpen(), Is.True);
                }

                Assert.That(M15FinalRollCallFacts.TryRecord(new M15FinalRollCallRecord(
                    M15RollCallReader.Protagonist, M15FinalIdentityResponse.PublicTrueName,
                    M15PensionClosure.StoppedAndReconciled, M15SteleInscription.KeepOriginal,
                    0, false), read, write), Is.True);

                // M16 torn StandingSpotKey blocks open until reset.
                values[M16EpilogueFacts.StandingSpotKey] = (int)M16StandingSpot.PierSeaBreeze;
                using (var m16Host = new M16EpilogueHost(
                    root.transform, () => true, () => true,
                    () => M16RespectAgencyTone.RespectedOthersChoices, read, write))
                {
                    Assert.That(m16Host.TryOpen(), Is.False);
                    Assert.That(M16EpilogueFacts.TryResetForNewVoyage(write), Is.True);
                    Assert.That(m16Host.TryOpen(), Is.True);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
