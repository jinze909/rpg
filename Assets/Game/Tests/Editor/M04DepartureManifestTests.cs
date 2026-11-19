using System;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M04DepartureManifestTests
    {
        private static M04DepartureManifest Ready(M03VoyageChoice route,
            int berths = 18, int returnees = 13, bool stamp = true,
            bool escortFlag = true, string pilot = "A Yuan")
        {
            return new M04DepartureManifest(route, "Confirmed hull", pilot, 3,
                "Assigned shallow tender", true, berths, returnees,
                "Named landing contact", stamp, escortFlag);
        }

        [Test]
        public void MerchantDepartureRequiresRealResourcesNotQiQueHerself()
        {
            // Qi Que can remain in port if an actual other pilot is assigned.
            var sailing = Ready(M03VoyageChoice.QiQueMerchant);
            Assert.That(sailing.Check(), Is.Empty);
            Assert.That(sailing.ConfirmDeparture(), Is.True);
            Assert.That(sailing.DepartureConfirmed, Is.True);
            Assert.That(sailing.ConfirmDeparture(), Is.False,
                "An accepted sailing cannot be recorded a second time.");
        }

        [Test]
        public void AChosenRouteDoesNotInventHullCrewTenderOrReceiver()
        {
            var missing = new M04DepartureManifest(M03VoyageChoice.QiQueMerchant,
                "", " ", 0, null, false, 0, 3, "", false, false);
            Assert.That(missing.Check(), Is.EquivalentTo(new[]
            {
                M04DepartureBlocker.NoConfirmedHull,
                M04DepartureBlocker.NoConfirmedPilot,
                M04DepartureBlocker.NoCrewAssigned,
                M04DepartureBlocker.UncheckedDraft,
                M04DepartureBlocker.NoShallowTender,
                M04DepartureBlocker.NoLandingReceiver,
                M04DepartureBlocker.InsufficientBerths
            }));
            Assert.That(missing.ConfirmDeparture(), Is.False);
            Assert.That(missing.DepartureConfirmed, Is.False);
        }

        [Test]
        public void PlannedReturneesCannotExceedActualBerths()
        {
            // A plan larger than the available berths needs another vessel
            // or a later trip before reserving the larger number.
            var overbooked = Ready(M03VoyageChoice.QiQueMerchant, berths: 18,
                returnees: 23);
            Assert.That(overbooked.Check(), Does.Contain(
                M04DepartureBlocker.InsufficientBerths));
            Assert.That(overbooked.ConfirmDeparture(), Is.False);

            var splitIntoRealTrips = Ready(M03VoyageChoice.QiQueMerchant,
                berths: 18, returnees: 18);
            Assert.That(splitIntoRealTrips.ConfirmDeparture(), Is.True);
        }

        [Test]
        public void OfficialHullAndMixedOfficialNeedAStampedRegistration()
        {
            foreach (var route in new[]
                     { M03VoyageChoice.HanQiaoOfficial,
                       M03VoyageChoice.MixedWithOfficial })
            {
                var unstamped = Ready(route, stamp: false);
                Assert.That(unstamped.Check(), Does.Contain(
                    M04DepartureBlocker.MissingOfficialStamp));
                Assert.That(unstamped.ConfirmDeparture(), Is.False);
                Assert.That(Ready(route, stamp: true).ConfirmDeparture(), Is.True);
            }
            // A civilian mixed route does not borrow a fictional official stamp.
            Assert.That(Ready(M03VoyageChoice.MixedWithCivilian,
                stamp: false).ConfirmDeparture(), Is.True);
        }

        [Test]
        public void LuJunEscortRequiresAcknowledgedPublicFlag()
        {
            var noFlag = Ready(M03VoyageChoice.LuJunEscort, escortFlag: false);
            Assert.That(noFlag.Check(), Does.Contain(
                M04DepartureBlocker.UnacceptedEscortFlag));
            Assert.That(noFlag.ConfirmDeparture(), Is.False);
            Assert.That(Ready(M03VoyageChoice.LuJunEscort,
                escortFlag: true).ConfirmDeparture(), Is.True);
        }

        [Test]
        public void HostRecordExceptionDoesNotConsumeDepartureConfirmation()
        {
            var departure = Ready(M03VoyageChoice.QiQueMerchant);
            int attempts = 0;
            Assert.Throws<InvalidOperationException>(() =>
                departure.ConfirmDeparture(record =>
                {
                    attempts++;
                    Assert.That(record.DepartureConfirmed, Is.True);
                    throw new InvalidOperationException("Interrupted Makinom write");
                }));
            Assert.That(departure.DepartureConfirmed, Is.False);
            Assert.That(attempts, Is.EqualTo(1));

            Assert.That(departure.ConfirmDeparture(record =>
            {
                attempts++;
                Assert.That(record.DepartureConfirmed, Is.True);
            }), Is.True);
            Assert.That(attempts, Is.EqualTo(2));
            Assert.That(departure.DepartureConfirmed, Is.True);
            Assert.That(departure.ConfirmDeparture(_ => attempts++), Is.False);
            Assert.That(attempts, Is.EqualTo(2));
            Assert.Throws<ArgumentNullException>(() =>
                departure.ConfirmDeparture((Action<M04DepartureManifest>)null));
        }

        [Test]
        public void EmptyOrInvalidDemandCannotClaimCompleteDeparture()
        {
            var noReserve = Ready(M03VoyageChoice.QiQueMerchant, returnees: 0);
            Assert.That(noReserve.Check(), Does.Contain(
                M04DepartureBlocker.NoRescueBerthPlan));
            Assert.That(noReserve.ConfirmDeparture(), Is.False);

            // This is the outbound trip. A berth can be reserved for the rescue
            // before the protagonist has spoken to any returnee in the mines.
            var oneReservedBerth = Ready(M03VoyageChoice.QiQueMerchant,
                berths: 1, returnees: 1);
            Assert.That(oneReservedBerth.ConfirmDeparture(), Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                Ready((M03VoyageChoice)999));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                Ready(M03VoyageChoice.QiQueMerchant, berths: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                Ready(M03VoyageChoice.QiQueMerchant, returnees: -1));
        }
    }
}
