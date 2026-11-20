using System;
using System.Collections.Generic;
using GamingIsLove.Makinom;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class ST06TaoJiHomecomingTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedM07(Dictionary<string, int> values, bool taoJiRescued)
        {
            Action<string, int> write = (key, value) => values[key] = value;
            Func<string, int> read = Reader(values);

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
                    M06RescuePlan.OfficialHandover,
                    true,
                    M06LetterDeliveryOutcome.NotCarried,
                    true),
                read, write), Is.True);
            Assert.That(M07ExtractionFacts.TryRecord(
                new M07ExtractionRecord(
                    M07ExecutedRoute.OfficialHandover,
                    M07PumpShutdownState.KeptRunning,
                    M07RouteCostFlags.HarborSupplyDebt,
                    boardedFirstBatchCount: 300,
                    lowerLevelEvacuatedCount: 0,
                    M07RusFate.Arrested,
                    M07LedgerState.Preserved,
                    M07LiangChoice.StayOnIsland,
                    M07YidaChoice.HandToInspector,
                    taoJiRescued: taoJiRescued),
                read, write), Is.True);
        }

        [Test]
        public void HomecomingRequiresTaoJiRescuedInM07()
        {
            var values = new Dictionary<string, int>();
            SeedM07(values, taoJiRescued: false);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(ST06TaoJiHomecomingSession.TryCreateFromSavedFacts(read, out var session), Is.False);
            Assert.That(session, Is.Null);
            Assert.That(ST06TaoJiHomecomingFacts.TryRecord(
                new ST06HomecomingRecord(
                    ST06ChairResolution.RefundAndRebuild, false, true, false,
                    ST06LetterStatus.NotSent, false),
                read, write), Is.False);
        }

        [Test]
        public void CoverUpRetainsUnremediatedTrustLossAndInTransitLetterDoesNotAppearOnM16Desk()
        {
            var values = new Dictionary<string, int>();
            SeedM07(values, taoJiRescued: true);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(ST06TaoJiHomecomingSession.TryCreateFromSavedFacts(read, out var session), Is.True);
            session.ResolveBrokenChair(ST06ChairResolution.CoverUp);
            Assert.That(session.HasUnremediatedCoverUpTrustLoss, Is.True);

            // Ticket cannot be enclosed while letter is merely in transit.
            Assert.Throws<ArgumentException>(() =>
                session.SetPostReturnLetterProgress(true, ST06LetterStatus.InTransit, true));

            session.SetPostReturnLetterProgress(true, ST06LetterStatus.InTransit, false);
            Assert.That(session.TrySettle(read, write), Is.True);

            Assert.That(ST06TaoJiHomecomingFacts.TryRead(read, out ST06HomecomingRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.HasUnremediatedCoverUpTrustLoss, Is.True);
            Assert.That(saved.Value.ShouldPlaceLetterOnM16Desk, Is.False);
            Assert.That(saved.Value.WelcomeDinnerHeld, Is.False);
        }

        [Test]
        public void DeliveredLetterWithFarTravelEnclosesMarketTicketAndSupportsRemediationAndRetry()
        {
            var values = new Dictionary<string, int>();
            SeedM07(values, taoJiRescued: true);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(ST06TaoJiHomecomingSession.TryCreateFromSavedFacts(read, out var session), Is.True);
            session.ResolveBrokenChair(ST06ChairResolution.CoverUp);
            Assert.That(session.TryRemediateCoverUp(), Is.True);
            Assert.That(session.HasUnremediatedCoverUpTrustLoss, Is.False);
            session.RecordWelcomeDinner();
            session.SetPostReturnLetterProgress(true, ST06LetterStatus.Delivered, true);

            int failOnce = 0;
            Assert.Throws<InvalidOperationException>(() =>
                session.TrySettle(read, (k, v) =>
                {
                    if (k == ST06TaoJiHomecomingFacts.LetterStatusKey && failOnce++ == 0)
                        throw new InvalidOperationException("Interrupted ST06 write");
                    write(k, v);
                }));
            Assert.That(session.IsSettled, Is.False);

            Assert.That(session.TrySettle(read, write), Is.True);
            Assert.That(ST06TaoJiHomecomingFacts.TryRead(read, out ST06HomecomingRecord? saved), Is.True);
            Assert.That(saved.Value.ShouldPlaceLetterOnM16Desk, Is.True);
            Assert.That(saved.Value.MarketTicketEnclosed, Is.True);
            Assert.That(saved.Value.WelcomeDinnerHeld, Is.True);

            Assert.That(ST06TaoJiHomecomingFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(ST06TaoJiHomecomingFacts.TryRead(read, out ST06HomecomingRecord? reset), Is.True);
            Assert.That(reset, Is.Null);

            Assert.That(ST06TaoJiHomecomingFacts.TryRead(null, out _), Is.False);
            if (!Maki.Initialized || Maki.Game?.Variables == null)
                Assert.That(ST06TaoJiHomecomingFacts.TryRead(out _), Is.False);
        }
    }
}
