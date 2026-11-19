using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M04DepartureFactsTests
    {
        private static M04DepartureManifest ReadyManifest(
            M03VoyageChoice route,
            int crew = 3,
            int berths = 18,
            int returnees = 13,
            bool officialStamp = true,
            bool escortFlag = true)
        {
            var manifest = new M04DepartureManifest(
                route,
                "Confirmed hull",
                "A Yuan",
                crew,
                "Assigned shallow tender",
                true,
                berths,
                returnees,
                "Named landing contact",
                officialStamp,
                escortFlag);
            Assert.That(manifest.ConfirmDeparture(), Is.True);
            return manifest;
        }

        [Test]
        public void UnconfirmedOrNullManifestIsRejectedWithoutWritingVariables()
        {
            var store = new Dictionary<string, int>();
            Action<string, int> setInt = (key, value) => store[key] = value;

            Assert.Throws<ArgumentNullException>(() =>
                M04DepartureFacts.TryRecord(null, setInt));

            var unconfirmed = new M04DepartureManifest(
                M03VoyageChoice.QiQueMerchant,
                "Confirmed hull",
                "A Yuan",
                3,
                "Assigned shallow tender",
                true,
                18,
                13,
                "Named landing contact",
                false,
                false);
            Assert.Throws<InvalidOperationException>(() =>
                M04DepartureFacts.TryRecord(unconfirmed, setInt));
            Assert.That(store, Is.Empty);

            var blocked = new M04DepartureManifest(
                M03VoyageChoice.HanQiaoOfficial,
                "",
                "A Yuan",
                0,
                "",
                false,
                0,
                13,
                "",
                false,
                false);
            Assert.Throws<InvalidOperationException>(() =>
                M04DepartureFacts.TryRecord(blocked, setInt));
            Assert.That(store, Is.Empty);
        }

        [Test]
        public void UninitializedMakinomGuardsReturnFalseWithoutThrowing()
        {
            var confirmed = ReadyManifest(M03VoyageChoice.QiQueMerchant);
            Assert.That(M04DepartureFacts.TryResetForNewVisit(), Is.False);
            Assert.That(M04DepartureFacts.TryRecord(confirmed), Is.False);
            Assert.That(M04DepartureFacts.TryRead(out M04DepartureRecord? record), Is.False);
            Assert.That(record, Is.Null);
        }

        [Test]
        public void ConfirmedDeparturePersistsAndReadsBackAcrossAllVoyageRoutes()
        {
            var routes = new[]
            {
                (M03VoyageChoice.QiQueMerchant, false, false),
                (M03VoyageChoice.HanQiaoOfficial, true, false),
                (M03VoyageChoice.MixedWithOfficial, true, false),
                (M03VoyageChoice.MixedWithCivilian, false, false),
                (M03VoyageChoice.LuJunEscort, false, true)
            };

            foreach (var (route, stamp, escortFlag) in routes)
            {
                var store = new Dictionary<string, int>();
                Action<string, int> setInt = (key, value) => store[key] = value;
                Func<string, int> getInt = key => store.TryGetValue(key, out int value) ? value : 0;

                var manifest = ReadyManifest(
                    route,
                    crew: 4,
                    berths: 20,
                    returnees: 15,
                    officialStamp: stamp,
                    escortFlag: escortFlag);
                Assert.That(M04DepartureFacts.TryRecord(manifest, setInt), Is.True);
                Assert.That(M04DepartureFacts.TryRead(getInt, out M04DepartureRecord? record), Is.True);
                Assert.That(record.HasValue, Is.True);
                Assert.That(record.Value.Route, Is.EqualTo(route));
                Assert.That(record.Value.AssignedCrew, Is.EqualTo(4));
                Assert.That(record.Value.AvailableBerths, Is.EqualTo(20));
                Assert.That(record.Value.ReturneesToBook, Is.EqualTo(15));
                Assert.That(record.Value.OfficialRegistrationEffective, Is.EqualTo(stamp));
                Assert.That(record.Value.EscortFlagAccepted, Is.EqualTo(escortFlag));
            }
        }

        [Test]
        public void ResetForNewVisitClearsPriorVoyageFacts()
        {
            var store = new Dictionary<string, int>();
            Action<string, int> setInt = (key, value) => store[key] = value;
            Func<string, int> getInt = key => store.TryGetValue(key, out int value) ? value : 0;

            var prior = ReadyManifest(M03VoyageChoice.LuJunEscort, officialStamp: false, escortFlag: true);
            Assert.That(M04DepartureFacts.TryRecord(prior, setInt), Is.True);
            Assert.That(M04DepartureFacts.TryRead(getInt, out M04DepartureRecord? beforeReset), Is.True);
            Assert.That(beforeReset.HasValue, Is.True);

            Assert.That(M04DepartureFacts.TryResetForNewVisit(setInt), Is.True);
            Assert.That(store[M04DepartureFacts.ConfirmedKey], Is.Zero);
            Assert.That(store[M04DepartureFacts.RouteKey], Is.Zero);
            Assert.That(store[M04DepartureFacts.AssignedCrewKey], Is.Zero);
            Assert.That(store[M04DepartureFacts.AvailableBerthsKey], Is.Zero);
            Assert.That(store[M04DepartureFacts.ReturneesToBookKey], Is.Zero);
            Assert.That(store[M04DepartureFacts.OfficialStampKey], Is.Zero);
            Assert.That(store[M04DepartureFacts.EscortFlagKey], Is.Zero);

            Assert.That(M04DepartureFacts.TryRead(getInt, out M04DepartureRecord? afterReset), Is.True);
            Assert.That(afterReset, Is.Null);
        }

        [Test]
        public void CorruptedOrIncompleteSavedVariablesNeverFabricateConfirmedDeparture()
        {
            var baseStore = new Dictionary<string, int>();
            var confirmed = ReadyManifest(M03VoyageChoice.HanQiaoOfficial, officialStamp: true, escortFlag: false);
            M04DepartureFacts.TryRecord(confirmed, (key, value) => baseStore[key] = value);

            void AssertRejected(Action<Dictionary<string, int>> corrupt)
            {
                var copy = new Dictionary<string, int>(baseStore);
                corrupt(copy);
                Func<string, int> getInt = key => copy.TryGetValue(key, out int value) ? value : 0;
                Assert.That(M04DepartureFacts.TryRead(getInt, out M04DepartureRecord? record), Is.True);
                Assert.That(record, Is.Null);
            }

            AssertRejected(s => s[M04DepartureFacts.ConfirmedKey] = 0);
            AssertRejected(s => s[M04DepartureFacts.RouteKey] = 0);
            AssertRejected(s => s[M04DepartureFacts.RouteKey] = 999);
            AssertRejected(s => s[M04DepartureFacts.AssignedCrewKey] = 0);
            AssertRejected(s => s[M04DepartureFacts.AvailableBerthsKey] = 0);
            AssertRejected(s => s[M04DepartureFacts.ReturneesToBookKey] = 0);
            AssertRejected(s => s[M04DepartureFacts.ReturneesToBookKey] = 19);
            AssertRejected(s => s[M04DepartureFacts.OfficialStampKey] = 0);
            AssertRejected(s =>
            {
                s[M04DepartureFacts.RouteKey] = (int)M03VoyageChoice.LuJunEscort + 1;
                s[M04DepartureFacts.EscortFlagKey] = 0;
            });
        }
    }
}
