using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class M04DepartureOrphanedDownstreamTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedReadyDeparture(Dictionary<string, int> values)
        {
            Action<string, int> write = (k, v) => values[k] = v;
            var encounter = new M03LuJunEncounter(M02BridgeEvidence.None, true);
            encounter.Begin();
            encounter.ObserveRollCall();
            encounter.Ask(M03LuJunOpening.NeedAShip);
            encounter.ChooseVoyage(M03VoyageChoice.QiQueMerchant);
            Assert.That(M03LuJunFacts.TryRecord(encounter, write), Is.True);

            var manifest = new M04DepartureManifest(
                M03VoyageChoice.QiQueMerchant,
                "Confirmed hull", "A Yuan", 4, "Shallow tender",
                true, 16, 8, "Named receiver", false, false);
            Assert.That(manifest.ConfirmDeparture(), Is.True);
            Assert.That(M04DepartureFacts.TryRecord(manifest, write), Is.True);
        }

        [Test]
        public void CastOffGateAndTryMarkSailedFailClosedWhenOrphanedDownstreamFactsExist()
        {
            var values = new Dictionary<string, int>();
            SeedReadyDeparture(values);
            Func<string, int> read = Reader(values);
            int sailWrites = 0;
            Action<string, int> write = (k, v) =>
            {
                if (k == M04DepartureFacts.SailedKey ||
                    k == M04DepartureFacts.SailingSequenceKey)
                    sailWrites++;
                values[k] = v;
            };

            string[] orphanKeys =
            {
                M07ExtractionFacts.ExecutedRouteKey,
                M08FirstWorldNoticeFacts.ConfirmedKey,
                M09BerthTableFacts.ConfirmedKey,
                RC05ThirdSquadFacts.ConfirmedKey,
                M10AssemblyFacts.ConfirmedKey,
                M16EpilogueFacts.ConfirmedKey
            };

            foreach (string orphanKey in orphanKeys)
            {
                values[orphanKey] = 1;
                Assert.That(M04DepartureFacts.CanBeginFreshFogLeg(read), Is.False, orphanKey);
                Assert.That(ReturnOpeningContinuation.TryReadCurrentCastOff(
                    read, out M04DepartureRecord? blocked), Is.False, orphanKey);
                Assert.That(blocked.HasValue, Is.False, orphanKey);
                Assert.That(M04DepartureFacts.TryMarkSailed(read, write), Is.False, orphanKey);
                Assert.That(sailWrites, Is.Zero, orphanKey);
                values[orphanKey] = 0;
            }

            Assert.That(M04DepartureFacts.CanBeginFreshFogLeg(read), Is.True);
            Assert.That(ReturnOpeningContinuation.TryReadCurrentCastOff(read, out _), Is.True);
            Assert.That(M04DepartureFacts.TryMarkSailed(read, write), Is.True);
            Assert.That(M04DepartureFacts.TryReadSailed(read, out bool sailed), Is.True);
            Assert.That(sailed, Is.True);
        }
    }
}
