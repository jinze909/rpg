using System;
using System.Collections.Generic;
using GamingIsLove.Makinom;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M09BerthTableTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedVerifiedM07(Dictionary<string, int> values)
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
                    taoJiRescued: true),
                read, write), Is.True);
        }

        [Test]
        public void BerthTableCannotPersistWithoutVerifiedM07Extraction()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var session = new M09BerthTableSession();
            Assert.That(session.CurrentShortfall, Is.EqualTo(5));
            Assert.That(session.TrySettleBerthTable(M09RuiyaOutcome.Deferred, read, write), Is.False);
            Assert.That(values, Is.Empty);
        }

        [Test]
        public void DefaultSecondBatchShortfallCombinesExchangesDisclosesQiDebtAndPersists()
        {
            var values = new Dictionary<string, int>();
            SeedVerifiedM07(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var session = new M09BerthTableSession(
                baseBerths: 18, pendingNames: 23,
                shenLiPresent: true, qiDebtPreviouslyKnown: false);
            Assert.That(session.CurrentShortfall, Is.EqualTo(5));
            Assert.That(session.QiDebtDisclosedHere, Is.False);

            Assert.That(session.TryNegotiateExchange(M09BerthExchangeFlags.QiQueCargoVoyage, 3), Is.True);
            Assert.That(session.TryNegotiateExchange(M09BerthExchangeFlags.QiQueCargoVoyage, 3), Is.False,
                "Same exchange cannot be claimed twice.");
            Assert.That(session.QiDebtDisclosedHere, Is.True);
            Assert.That(session.CurrentShortfall, Is.EqualTo(2));

            Assert.That(session.TryNegotiateExchange(M09BerthExchangeFlags.ShipwrightExtraBerths, 2), Is.True);
            Assert.That(session.ShenLiTwoDayApprenticeLeave, Is.True);
            Assert.That(session.TotalBerths, Is.EqualTo(23));
            Assert.That(session.CurrentShortfall, Is.Zero);

            Assert.That(session.TryPlayShenReconciliation(), Is.True);
            Assert.That(session.TrySettleBerthTable(M09RuiyaOutcome.Rescued, read, write), Is.True);

            Assert.That(M09BerthTableFacts.TryRead(read, out M09BerthTableRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.TotalBerths, Is.EqualTo(23));
            Assert.That(saved.Value.RemainingUnassigned, Is.Zero);
            Assert.That(saved.Value.RuiyaOutcome, Is.EqualTo(M09RuiyaOutcome.Rescued));
            Assert.That(saved.Value.QiDebtDisclosedHere, Is.True);
            Assert.That(saved.Value.ShenReconciled, Is.True);
        }

        [Test]
        public void AbsentShenLiBlocksReconciliationAndShipwrightExchangeWhileUnassignedRemainPending()
        {
            var values = new Dictionary<string, int>();
            SeedVerifiedM07(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var session = new M09BerthTableSession(
                baseBerths: 18, pendingNames: 23,
                shenLiPresent: false, qiDebtPreviouslyKnown: true);

            Assert.That(session.TryNegotiateExchange(M09BerthExchangeFlags.ShipwrightExtraBerths, 3), Is.False);
            Assert.That(session.TryPlayShenReconciliation(), Is.False);
            Assert.That(session.TryNegotiateExchange(M09BerthExchangeFlags.HanQiaoCivilianVoyage, 2), Is.True);
            Assert.That(session.OfficialLivingRegistrationRequired, Is.True);
            Assert.That(session.CurrentShortfall, Is.EqualTo(3));

            Assert.That(session.TrySettleBerthTable(M09RuiyaOutcome.Deferred, read, write), Is.True);
            Assert.That(M09BerthTableFacts.TryRead(read, out M09BerthTableRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.AssignedCount, Is.EqualTo(20));
            Assert.That(saved.Value.RemainingUnassigned, Is.EqualTo(3));
            Assert.That(saved.Value.ShenReconciled, Is.False);
        }

        [Test]
        public void AbsentShenLiCanPayShipwrightWithoutInventingHerTwoDayTeaching()
        {
            var values = new Dictionary<string, int>();
            SeedVerifiedM07(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            var session = new M09BerthTableSession(
                baseBerths: 18, pendingNames: 23,
                shenLiPresent: false, qiDebtPreviouslyKnown: true);

            Assert.That(session.TryNegotiateExchange(
                M09BerthExchangeFlags.ShipwrightExtraBerths, 5), Is.False,
                "An absent Shen Li cannot be volunteered to teach apprentices.");
            Assert.That(session.TryNegotiateExchange(
                M09BerthExchangeFlags.HanQiaoCivilianVoyage, 1,
                shipwrightFeePaid: true), Is.False,
                "Paying a shipyard is not a substitute for official registration.");
            Assert.That(session.TryNegotiateExchange(
                M09BerthExchangeFlags.ShipwrightExtraBerths, 5,
                shipwrightFeePaid: true), Is.True);
            Assert.That(session.ShipwrightFeePaid, Is.True);
            Assert.That(session.ShenLiTwoDayApprenticeLeave, Is.False);
            Assert.That(session.TryPlayShenReconciliation(), Is.False);
            Assert.That(session.CurrentShortfall, Is.Zero);

            Assert.That(session.TrySettleBerthTable(
                M09RuiyaOutcome.Rescued, read, write), Is.True);
            Assert.That(M09BerthTableFacts.TryRead(
                read, out M09BerthTableRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.ShipwrightFeePaid, Is.True);
            Assert.That(saved.Value.Exchanges, Is.EqualTo(
                M09BerthExchangeFlags.ShipwrightExtraBerths));
            Assert.That(saved.Value.ShenReconciled, Is.False);

            // A corrupted fee flag cannot silently become a free exchange.
            values[M09BerthTableFacts.ShipwrightFeePaidKey] = 2;
            Assert.That(M09BerthTableFacts.TryRead(
                read, out M09BerthTableRecord? corrupted), Is.True);
            Assert.That(corrupted, Is.Null);
            values[M09BerthTableFacts.ShipwrightFeePaidKey] = 1;
            Assert.That(M09BerthTableFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(values[M09BerthTableFacts.ShipwrightFeePaidKey], Is.Zero);
        }

        [Test]
        public void ForgedShipyardPaymentAndOverflowCannotMintBerths()
        {
            var values = new Dictionary<string, int>();
            SeedVerifiedM07(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            var invalid = new M09BerthTableRecord(
                23, 23, M09BerthExchangeFlags.None,
                M09RuiyaOutcome.Deferred, false, false,
                shipwrightFeePaid: true);

            Assert.That(M09BerthTableFacts.TryRecord(invalid, read, write), Is.False);
            Assert.That(values.ContainsKey(M09BerthTableFacts.ConfirmedKey), Is.False);

            var nearMax = new M09BerthTableSession(
                baseBerths: int.MaxValue - 1, pendingNames: 23);
            Assert.That(nearMax.TryNegotiateExchange(
                M09BerthExchangeFlags.HanQiaoCivilianVoyage, 2), Is.False);
            Assert.That(nearMax.TotalBerths, Is.EqualTo(int.MaxValue - 1));
            Assert.That(nearMax.NegotiatedExchanges, Is.EqualTo(
                M09BerthExchangeFlags.None));
            Assert.That(nearMax.TryNegotiateExchange(
                M09BerthExchangeFlags.HanQiaoCivilianVoyage, 1), Is.True);
            Assert.That(nearMax.TotalBerths, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void InterruptedSaveRetriesCleanlyAndUpstreamM07ResetInvalidatesReadback()
        {
            var values = new Dictionary<string, int>();
            SeedVerifiedM07(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var session = new M09BerthTableSession();
            int failOnce = 0;
            Assert.Throws<InvalidOperationException>(() =>
                session.TrySettleBerthTable(M09RuiyaOutcome.NotRescued, read, (k, v) =>
                {
                    if (k == M09BerthTableFacts.RuiyaOutcomeKey && failOnce++ == 0)
                        throw new InvalidOperationException("Interrupted M09 write");
                    write(k, v);
                }));
            Assert.That(session.IsSettled, Is.False);
            Assert.That(M09BerthTableFacts.TryRead(read, out M09BerthTableRecord? partial), Is.True);
            Assert.That(partial, Is.Null);

            Assert.That(session.TrySettleBerthTable(M09RuiyaOutcome.NotRescued, read, write), Is.True);
            Assert.That(M09BerthTableFacts.TryRead(read, out M09BerthTableRecord? confirmed), Is.True);
            Assert.That(confirmed.HasValue, Is.True);

            Assert.That(M07ExtractionFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(M09BerthTableFacts.TryRead(read, out M09BerthTableRecord? stale), Is.True);
            Assert.That(stale, Is.Null);

            Assert.That(M09BerthTableFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(values[M09BerthTableFacts.ConfirmedKey], Is.Zero);
        }

        [Test]
        public void TornWriteCannotReplaceRuiyaRescueCapacityOrShipwrightCost()
        {
            var values = new Dictionary<string, int>();
            SeedVerifiedM07(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            var original = new M09BerthTableRecord(
                23, 23,
                M09BerthExchangeFlags.QiQueCargoVoyage |
                M09BerthExchangeFlags.ShipwrightExtraBerths,
                M09RuiyaOutcome.Rescued, true, true,
                shipwrightFeePaid: false);
            var changedRescue = new M09BerthTableRecord(
                23, 23, original.Exchanges, M09RuiyaOutcome.NotRescued,
                true, true);
            var changedBerths = new M09BerthTableRecord(
                24, 23, original.Exchanges, original.RuiyaOutcome,
                true, true);
            var changedCost = new M09BerthTableRecord(
                23, 23, original.Exchanges, original.RuiyaOutcome,
                true, true, shipwrightFeePaid: true);

            Assert.Throws<InvalidOperationException>(() =>
                M09BerthTableFacts.TryRecord(original, read, (key, value) =>
                {
                    write(key, value);
                    if (key == M09BerthTableFacts.PendingSignatureKey)
                        throw new InvalidOperationException("Torn just after M09 intent");
                }));
            int intent = read(M09BerthTableFacts.PendingSignatureKey);
            Assert.That(intent, Is.Not.Zero);
            Assert.That(M09BerthTableFacts.TryRecord(changedRescue, read, write), Is.False);
            Assert.That(M09BerthTableFacts.TryRecord(changedBerths, read, write), Is.False);
            Assert.That(M09BerthTableFacts.TryRecord(changedCost, read, write), Is.False);
            Assert.That(M09BerthTableFacts.TryRecord(original, read, write), Is.True);
            Assert.That(M09BerthTableFacts.TryRead(
                read, out M09BerthTableRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.RuiyaOutcome, Is.EqualTo(M09RuiyaOutcome.Rescued));
            Assert.That(saved.Value.TotalBerths, Is.EqualTo(23));
            Assert.That(saved.Value.ShipwrightFeePaid, Is.False);

            // A confirmed legacy berth table has no intent key.
            values[M09BerthTableFacts.PendingSignatureKey] = 0;
            Assert.That(M09BerthTableFacts.TryRead(
                read, out M09BerthTableRecord? legacy), Is.True);
            Assert.That(legacy.HasValue, Is.True);

            Assert.That(M09BerthTableFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(read(M09BerthTableFacts.PendingSignatureKey), Is.Zero);
            values[M09BerthTableFacts.RuiyaOutcomeKey] = (int)M09RuiyaOutcome.Rescued;
            Assert.That(M09BerthTableFacts.TryRecord(original, read, write), Is.False,
                "Orphaned legacy outcome without intent cannot silently be overwritten.");
        }

        [Test]
        public void PendingM09IntentCannotBeReusedAcrossDifferentArrivals()
        {
            var values = new Dictionary<string, int>();
            SeedVerifiedM07(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            var record = new M09BerthTableRecord(
                18, 23, M09BerthExchangeFlags.None,
                M09RuiyaOutcome.Deferred, false, false);
            Assert.Throws<InvalidOperationException>(() =>
                M09BerthTableFacts.TryRecord(record, read, (key, value) =>
                {
                    write(key, value);
                    if (key == M09BerthTableFacts.PendingSignatureKey)
                        throw new InvalidOperationException("Torn berth intent");
                }));
            values[M05LandingFacts.ArrivalSequenceKey]++;
            Assert.That(M09BerthTableFacts.TryRecord(record, read, write), Is.False,
                "An old arrival cannot finalize its berth table on a new voyage.");
        }

        [Test]
        public void MissingSaveAuthorityOrNullDelegatesNeverFabricateM09Facts()
        {
            var record = new M09BerthTableRecord(
                18, 23, M09BerthExchangeFlags.None, M09RuiyaOutcome.Deferred, false, false);
            Assert.That(M09BerthTableFacts.TryRead(null, out M09BerthTableRecord? missing), Is.False);
            Assert.That(missing, Is.Null);
            Assert.That(M09BerthTableFacts.TryRecord(record, null, (_, __) => { }), Is.False);
            Assert.That(M09BerthTableFacts.TryRecord(record, _ => 0, null), Is.False);
            Assert.That(M09BerthTableFacts.TryResetForNewVoyage(null), Is.False);

            if (!Maki.Initialized || Maki.Game?.Variables == null)
            {
                Assert.That(M09BerthTableFacts.TryRead(out M09BerthTableRecord? unbound), Is.False);
                Assert.That(unbound, Is.Null);
                Assert.That(M09BerthTableFacts.TryRecord(record), Is.False);
                Assert.That(M09BerthTableFacts.TryResetForNewVoyage(), Is.False);
            }
        }
    }
}
