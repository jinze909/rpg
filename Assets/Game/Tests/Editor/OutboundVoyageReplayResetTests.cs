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
            ST06TaoJiHomecomingFacts.ConfirmedKey,
            RC04DingYueReefFacts.ConfirmedKey,
            M13DepartureRiskFacts.DepartureConfirmedKey,
            M15FinalRollCallFacts.ConfirmedKey
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
            data[M13DepartureRiskFacts.AyuanOutcomeKey] = 1;
            data[M15FinalRollCallFacts.ConsentedNamesCountKey] = 6;

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
            Assert.That(Get(values, M13DepartureRiskFacts.AyuanOutcomeKey), Is.Zero);
            Assert.That(Get(values, M15FinalRollCallFacts.ConsentedNamesCountKey), Is.Zero);
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
