using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M13ToM16TornCommitLockTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void M13InterruptedDepartureLocksOutCompetingRouteAndAllowsIdenticalRetry()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var chosen = new M13DepartureRecord(
                M13ApproachRoute.RouteASupervisedCivilian, true, false, false, false);
            var competing = new M13DepartureRecord(
                M13ApproachRoute.RouteCOldCanalNightRun, true, false, false, false);

            Assert.Throws<InvalidOperationException>(() => M13DepartureRiskFacts.TryRecordDeparture(
                chosen, read, (k, v) =>
                {
                    if (k == M13DepartureRiskFacts.DepartureConfirmedKey)
                        throw new InvalidOperationException("Interrupted before M13 confirm");
                    values[k] = v;
                }));

            Assert.That(M13DepartureRiskFacts.CanRecordDeparture(competing, read), Is.False);
            Assert.That(M13DepartureRiskFacts.TryRecordDeparture(competing, read, write), Is.False);

            Assert.That(M13DepartureRiskFacts.TryRecordDeparture(chosen, read, write), Is.True);
            Assert.That(M13DepartureRiskFacts.TryReadDeparture(read, out M13DepartureRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.Route, Is.EqualTo(M13ApproachRoute.RouteASupervisedCivilian));
        }

        [Test]
        public void M14InterruptedPassageLocksOutCompetingDispositionAndAllowsIdenticalRetry()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(M13DepartureRiskFacts.TryRecordDeparture(new M13DepartureRecord(
                M13ApproachRoute.RouteASupervisedCivilian, true, false, false, false),
                read, write), Is.True);

            var chosen = new M14FinalPassageRecord(
                M13ApproachRoute.RouteASupervisedCivilian,
                M14PassageOutcome.RouteASupervisedCivilianEstablished,
                true, 12, 2, 1, M14BorderForceAction.NotRouteB,
                M14LuJunDisposition.RelievedOfCommand, M14AyuanOutcome.Safe, false);
            var competing = new M14FinalPassageRecord(
                M13ApproachRoute.RouteASupervisedCivilian,
                M14PassageOutcome.RouteASupervisedCivilianEstablished,
                true, 12, 2, 1, M14BorderForceAction.NotRouteB,
                M14LuJunDisposition.Detained, M14AyuanOutcome.Safe, false);

            Assert.Throws<InvalidOperationException>(() => M14FinalPassageFacts.TryRecord(
                chosen, read, (k, v) =>
                {
                    if (k == M14FinalPassageFacts.ConfirmedKey)
                        throw new InvalidOperationException("Interrupted before M14 confirm");
                    values[k] = v;
                }));

            Assert.That(M14FinalPassageFacts.CanRecord(competing, read), Is.False);
            Assert.That(M14FinalPassageFacts.TryRecord(competing, read, write), Is.False);

            Assert.That(M14FinalPassageFacts.TryRecord(chosen, read, write), Is.True);
            Assert.That(M14FinalPassageFacts.TryRead(read, out M14FinalPassageRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.LuJunDisposition,
                Is.EqualTo(M14LuJunDisposition.RelievedOfCommand));
        }

        [Test]
        public void M15InterruptedRollCallLocksOutCompetingIdentityAndSteleAndAllowsIdenticalRetry()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var chosen = new M15FinalRollCallRecord(
                M15RollCallReader.Protagonist,
                M15FinalIdentityResponse.PublicTrueName,
                M15PensionClosure.StoppedAndReconciled,
                M15SteleInscription.CarveConsentedVerifiedNames,
                12, false);
            var competing = new M15FinalRollCallRecord(
                M15RollCallReader.HanQiao,
                M15FinalIdentityResponse.PrivateCustodianResponse,
                M15PensionClosure.UnchangedOrPrivate,
                M15SteleInscription.LeaveBlank,
                0, true);

            Assert.Throws<InvalidOperationException>(() => M15FinalRollCallFacts.TryRecord(
                chosen, read, (k, v) =>
                {
                    if (k == M15FinalRollCallFacts.ConfirmedKey)
                        throw new InvalidOperationException("Interrupted before M15 confirm");
                    values[k] = v;
                }));

            Assert.That(M15FinalRollCallFacts.CanRecord(competing, read), Is.False);
            Assert.That(M15FinalRollCallFacts.TryRecord(competing, read, write), Is.False);

            Assert.That(M15FinalRollCallFacts.TryRecord(chosen, read, write), Is.True);
            Assert.That(M15FinalRollCallFacts.TryRead(read, out M15FinalRollCallRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.IdentityResponse,
                Is.EqualTo(M15FinalIdentityResponse.PublicTrueName));
            Assert.That(saved.Value.SteleInscription,
                Is.EqualTo(M15SteleInscription.CarveConsentedVerifiedNames));
        }

        [Test]
        public void M16InterruptedEpilogueLocksOutCompetingSpotAndAllowsIdenticalRetry()
        {
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            Assert.That(M15FinalRollCallFacts.TryRecord(new M15FinalRollCallRecord(
                M15RollCallReader.Protagonist,
                M15FinalIdentityResponse.PublicTrueName,
                M15PensionClosure.StoppedAndReconciled,
                M15SteleInscription.CarveConsentedVerifiedNames,
                12, false), read, write), Is.True);

            var chosen = new M16EpilogueRecord(
                M16WalkStops.WorkshopStop,
                M16DeliveredLettersFlags.None,
                true,
                M16StandingSpot.PierSeaBreeze,
                true,
                M16RespectAgencyTone.RespectedOthersChoices);
            var competing = new M16EpilogueRecord(
                M16WalkStops.WorkshopStop,
                M16DeliveredLettersFlags.None,
                true,
                M16StandingSpot.MemorialSteleSunset,
                true,
                M16RespectAgencyTone.PartedWaysWithClosure);

            Assert.Throws<InvalidOperationException>(() => M16EpilogueFacts.TryRecord(
                chosen, read, (k, v) =>
                {
                    if (k == M16EpilogueFacts.ConfirmedKey)
                        throw new InvalidOperationException("Interrupted before M16 confirm");
                    values[k] = v;
                }));

            Assert.That(M16EpilogueFacts.CanRecord(competing, read), Is.False);
            Assert.That(M16EpilogueFacts.TryRecord(competing, read, write), Is.False);

            Assert.That(M16EpilogueFacts.TryRecord(chosen, read, write), Is.True);
            Assert.That(M16EpilogueFacts.TryRead(read, out M16EpilogueRecord? saved), Is.True);
            Assert.That(saved.HasValue, Is.True);
            Assert.That(saved.Value.StandingSpot, Is.EqualTo(M16StandingSpot.PierSeaBreeze));
            Assert.That(saved.Value.ClosingTone,
                Is.EqualTo(M16RespectAgencyTone.RespectedOthersChoices));
        }
    }
}
