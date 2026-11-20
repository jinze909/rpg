using System;
using System.Collections.Generic;
using GamingIsLove.Makinom;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M13DepartureRiskTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void UnpreparedRouteBOrCIsBlockedWithoutQiWarningAndSeparateConsent()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var session = new M13DepartureRiskSession(safetyPreparationComplete: false);
            session.AgreeThreeBellReverseSignal();

            Assert.That(session.CanConfirmDeparture(M13ApproachRoute.RouteASupervisedCivilian), Is.True);
            Assert.That(session.CanConfirmDeparture(M13ApproachRoute.RouteBBellTowerAssault), Is.False,
                "Picking Route B without safety prep or explicit risk consent must be blocked.");
            Assert.That(session.CanConfirmDeparture(M13ApproachRoute.RouteCOldCanalNightRun), Is.False);
            Assert.That(session.TryAcceptHighRiskConsent(), Is.False,
                "Consent cannot be accepted before Qi Que delivers the explicit warning.");

            session.DeliverQiQueWarning();
            Assert.That(session.CanConfirmDeparture(M13ApproachRoute.RouteBBellTowerAssault), Is.False,
                "Warning alone does not authorize unprepared high-risk departure.");

            Assert.That(session.TryAcceptHighRiskConsent(), Is.True);
            Assert.That(session.CanConfirmDeparture(M13ApproachRoute.RouteBBellTowerAssault), Is.True);
            Assert.That(session.TryConfirmDeparture(M13ApproachRoute.RouteBBellTowerAssault, read, write), Is.True);

            Assert.That(M13DepartureRiskFacts.TryReadDeparture(read, out M13DepartureRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.CanAyuanSufferHighRiskOutcome, Is.True);

            Assert.That(M13DepartureRiskFacts.TryRecordAyuanOutcome(
                M14AyuanOutcome.SeverelyInjuredRetired, read, write), Is.True);
            Assert.That(M13DepartureRiskFacts.TryReadAyuanOutcome(read, out M14AyuanOutcome? outcome), Is.True);
            Assert.That(outcome, Is.EqualTo(M14AyuanOutcome.SeverelyInjuredRetired));

            // Subsequent combat retry cannot overwrite the settled outcome.
            Assert.That(M13DepartureRiskFacts.TryRecordAyuanOutcome(
                M14AyuanOutcome.Lost, read, write), Is.False);
        }

        [Test]
        public void RouteBRequiresThreeBellReverseSignalAgreementAndSafePrepGuaranteesAyuanSafe()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var session = new M13DepartureRiskSession(safetyPreparationComplete: false);
            session.CompleteSafetyPreparation();
            Assert.That(session.CanConfirmDeparture(M13ApproachRoute.RouteBBellTowerAssault), Is.False,
                "Route B requires the 3-bell reverse-signal agreement with the fleet.");

            session.AgreeThreeBellReverseSignal();
            Assert.That(session.TryConfirmDeparture(M13ApproachRoute.RouteBBellTowerAssault, read, write), Is.True);

            // With complete safety preparation, A Yuan cannot suffer injury or loss.
            Assert.That(M13DepartureRiskFacts.TryRecordAyuanOutcome(
                M14AyuanOutcome.Lost, read, write), Is.False);
            Assert.That(M13DepartureRiskFacts.TryRecordAyuanOutcome(
                M14AyuanOutcome.SeverelyInjuredRetired, read, write), Is.False);
            Assert.That(M13DepartureRiskFacts.TryRecordAyuanOutcome(
                M14AyuanOutcome.Safe, read, write), Is.True);
        }

        [Test]
        public void RouteANeverSendsAyuanToHighRiskPostAndAlwaysRejectsNonSafeOutcome()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var session = new M13DepartureRiskSession(safetyPreparationComplete: false);
            session.DeliverQiQueWarning();
            session.TryAcceptHighRiskConsent();
            Assert.That(session.TryConfirmDeparture(M13ApproachRoute.RouteASupervisedCivilian, read, write), Is.True);

            Assert.That(M13DepartureRiskFacts.TryRecordAyuanOutcome(
                M14AyuanOutcome.Lost, read, write), Is.False);
            Assert.That(M13DepartureRiskFacts.TryRecordAyuanOutcome(
                M14AyuanOutcome.Safe, read, write), Is.True);
        }

        [Test]
        public void InterruptedSaveRetriesCleanlyAndCorruptOrResetSavesNeverFabricateDeparture()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var session = new M13DepartureRiskSession(safetyPreparationComplete: true);
            int failOnce = 0;
            Assert.Throws<InvalidOperationException>(() =>
                session.TryConfirmDeparture(M13ApproachRoute.RouteCOldCanalNightRun, read, (k, v) =>
                {
                    if (k == M13DepartureRiskFacts.RouteKey && failOnce++ == 0)
                        throw new InvalidOperationException("Interrupted M13 save");
                    write(k, v);
                }));
            Assert.That(session.IsDeparted, Is.False);
            Assert.That(M13DepartureRiskFacts.TryReadDeparture(read, out M13DepartureRecord? partial), Is.True);
            Assert.That(partial, Is.Null);

            Assert.That(session.TryConfirmDeparture(M13ApproachRoute.RouteCOldCanalNightRun, read, write), Is.True);
            Assert.That(M13DepartureRiskFacts.TryResetForNewVoyage(write), Is.True);
            Assert.That(M13DepartureRiskFacts.TryReadDeparture(read, out M13DepartureRecord? reset), Is.True);
            Assert.That(reset, Is.Null);

            Assert.That(M13DepartureRiskFacts.TryReadDeparture(null, out _), Is.False);
            if (!Maki.Initialized || Maki.Game?.Variables == null)
                Assert.That(M13DepartureRiskFacts.TryReadDeparture(out _), Is.False);
        }
    }
}
