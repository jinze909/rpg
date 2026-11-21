using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    /// <summary>
    /// Starting a new M03 departure must not inherit completed, voyage-owned
    /// later-story decisions from the previous crossing.
    /// </summary>
    public sealed class OutboundVoyageReplayResetTests
    {
        private static readonly string[] PublishedKeys =
        {
            M04DepartureFacts.ConfirmedKey,
            M04RosterFacts.ReconciledKey,
            FogListeningFacts.CompletedKey,
            M05LandingFacts.ConfirmedKey,
            M05LettersFacts.DecisionKey,
            M05HarborVisitFacts.VisitedMaskKey,
            M05HarborPositionFacts.PositionKey,
            M06MineCampReconFacts.ObservedMaskKey,
            M06RescuePlanFacts.ConfirmedKey,
            M07ExtractionFacts.ConfirmedKey,
            M08DockTruthFacts.ConfirmedKey,
            M08FirstWorldNoticeFacts.ConfirmedKey,
            M08WorkshopDepositFacts.CompletedKey,
            M09BerthTableFacts.ConfirmedKey,
            M10AssemblyFacts.ConfirmedKey,
            M11ReverseRescueFacts.ConfirmedKey,
            M12BridgeTruthFacts.ConfirmedKey,
            M13DepartureRiskFacts.DepartureConfirmedKey,
            M14FinalPassageFacts.ConfirmedKey,
            M15FinalRollCallFacts.ConfirmedKey,
            M16EpilogueFacts.ConfirmedKey,
            RC01ChenYanFacts.ConfirmedKey,
            RC02ZhengYuanFacts.ConfirmedKey,
            RC03BaijiMineFacts.ConfirmedKey,
            RC04DingYueReefFacts.ConfirmedKey,
            RC05ThirdSquadFacts.ConfirmedKey,
            ST06TaoJiHomecomingFacts.ConfirmedKey
        };

        private static Dictionary<string, int> SeedOldVoyage()
        {
            var data = new Dictionary<string, int>();
            foreach (string key in PublishedKeys)
                data[key] = 1;

            data[M06RescuePlanFacts.PlanKey] = (int)M06RescuePlan.ForceBreakout;
            data[M07ExtractionFacts.TaoJiRescuedKey] = 1;
            data[M07ExtractionFacts.BoardedFirstBatchKey] = 8;
            data[M09BerthTableFacts.ShipwrightFeePaidKey] = 1;
            data[M10AssemblyFacts.HanLedgerUseKey] = (int)M10HanLedgerUse.KeepConfidential;
            data[M11ReverseRescueFacts.ThreeBellsKey] = 1;
            data[M12BridgeTruthFacts.DisclosureKey] = (int)M12TruthDisclosure.SealTruth;
            data[M13DepartureRiskFacts.AyuanOutcomeKey] = 1;
            data[M14FinalPassageFacts.DepartingCountKey] = 12;
            data[M15FinalRollCallFacts.ConsentedNamesCountKey] = 6;
            data[M16EpilogueFacts.LettersReadKey] = 3;
            data[RC01ChenYanFacts.OutcomeKey] = (int)RC01ChenYanOutcome.RestoreLivingName;
            data[RC02ZhengYuanFacts.ChecksMaskKey] = 3;
            data[RC03BaijiMineFacts.VisitsMaskKey] = 3;
            data[RC05ThirdSquadFacts.ResolutionKey] = (int)RC05Resolution.CivilianPardon;
            data[M05LandingFacts.ArrivalSequenceKey] = 7;
            data[ST02RiverLanternsFacts.ConfirmedKey] = 1;
            data[ST03BathhouseFacts.ConfirmedKey] = 1;
            data[ST05TwoDoorsFacts.HomeConfirmedKey] = 1;

            // Earlier choices are historical context, not post-departure
            // results: this helper must not rewrite them.
            data[M02QiQueDebtFacts.DebtKnownKey] = 1;
            data[M03LuJunFacts.VoyageKey] = (int)M03VoyageChoice.QiQueMerchant;
            return data;
        }

        private static int Get(Dictionary<string, int> data, string key)
        {
            return data.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void RestartingOutboundVoyageClearsAllLaterChapterMarkersAndOutcomes()
        {
            var values = SeedOldVoyage();
            Assert.That(ReturnOpeningContinuation.TryResetPreviousOutboundFacts(
                (key, value) => values[key] = value), Is.True);

            foreach (string key in PublishedKeys)
                Assert.That(Get(values, key), Is.Zero, "Stale voyage marker: " + key);
            Assert.That(Get(values, M06RescuePlanFacts.PlanKey), Is.Zero);
            Assert.That(Get(values, M07ExtractionFacts.TaoJiRescuedKey), Is.Zero);
            Assert.That(Get(values, M07ExtractionFacts.BoardedFirstBatchKey), Is.Zero);
            Assert.That(Get(values, M09BerthTableFacts.ShipwrightFeePaidKey), Is.Zero);
            Assert.That(Get(values, M10AssemblyFacts.HanLedgerUseKey), Is.Zero);
            Assert.That(Get(values, M11ReverseRescueFacts.ThreeBellsKey), Is.Zero);
            Assert.That(Get(values, M12BridgeTruthFacts.DisclosureKey), Is.Zero);
            Assert.That(Get(values, M13DepartureRiskFacts.AyuanOutcomeKey), Is.Zero);
            Assert.That(Get(values, M14FinalPassageFacts.DepartingCountKey), Is.Zero);
            Assert.That(Get(values, M15FinalRollCallFacts.ConsentedNamesCountKey), Is.Zero);
            Assert.That(Get(values, M16EpilogueFacts.LettersReadKey), Is.Zero);
            Assert.That(Get(values, RC01ChenYanFacts.OutcomeKey), Is.Zero);
            Assert.That(Get(values, RC02ZhengYuanFacts.ChecksMaskKey), Is.Zero);
            Assert.That(Get(values, RC03BaijiMineFacts.VisitsMaskKey), Is.Zero);
            Assert.That(Get(values, RC05ThirdSquadFacts.ResolutionKey), Is.Zero);
            Assert.That(Get(values, M05LandingFacts.ArrivalSequenceKey), Is.EqualTo(7),
                "Landing identity must not be reused by the next voyage.");
            Assert.That(Get(values, ST02RiverLanternsFacts.ConfirmedKey), Is.EqualTo(1));
            Assert.That(Get(values, ST03BathhouseFacts.ConfirmedKey), Is.EqualTo(1));
            Assert.That(Get(values, ST05TwoDoorsFacts.HomeConfirmedKey), Is.EqualTo(1));
            Assert.That(Get(values, M02QiQueDebtFacts.DebtKnownKey), Is.EqualTo(1));
            Assert.That(Get(values, M03LuJunFacts.VoyageKey),
                Is.EqualTo((int)M03VoyageChoice.QiQueMerchant));

            // Replay reset is idempotent and must not create new facts.
            Assert.That(ReturnOpeningContinuation.TryResetPreviousOutboundFacts(
                (key, value) => values[key] = value), Is.True);
            foreach (string key in PublishedKeys)
                Assert.That(Get(values, key), Is.Zero, "Repeat reset: " + key);
        }

        [Test]
        public void InterruptedFieldResetCannotRepublishAnyDownstreamOutcome()
        {
            var values = SeedOldVoyage();
            Assert.Throws<InvalidOperationException>(() =>
                ReturnOpeningContinuation.TryResetPreviousOutboundFacts((key, value) =>
                {
                    if (key == M06RescuePlanFacts.PlanKey)
                        throw new InvalidOperationException("Simulated interrupted downstream reset");
                    values[key] = value;
                }));

            foreach (string key in PublishedKeys)
                Assert.That(Get(values, key), Is.Zero,
                    "All completion flags must be invalidated before field reset: " + key);
            Assert.That(Get(values, M02QiQueDebtFacts.DebtKnownKey), Is.EqualTo(1));
        }
    }
}
