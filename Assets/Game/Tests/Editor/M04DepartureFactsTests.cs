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
        public void CastOffRejectsEveryStaleFogFieldWithoutAdvancingSailingSequence()
        {
            var store = new Dictionary<string, int>();
            Func<string, int> read = key =>
                store.TryGetValue(key, out int value) ? value : 0;
            Action<string, int> write = (key, value) => store[key] = value;
            Assert.That(M04DepartureFacts.TryRecord(
                ReadyManifest(M03VoyageChoice.QiQueMerchant,
                    officialStamp: false, escortFlag: false), write), Is.True);

            int sailingWrites = 0;
            Action<string, int> observedWrite = (key, value) =>
            {
                sailingWrites++;
                write(key, value);
            };

            string[] staleKeys =
            {
                FogListeningFacts.CompletedKey,
                FogListeningFacts.PendingSignatureKey,
                FogListeningFacts.SucceededKey,
                FogListeningFacts.HullIntegrityKey,
                FogListeningFacts.AvoidedHazardsKey,
                FogListeningFacts.MatchedSignalsKey,
                FogListeningFacts.RemainingFocusKey
            };

            foreach (string staleKey in staleKeys)
            {
                store[staleKey] = 1;
                Assert.That(M04DepartureFacts.CanBeginFreshFogLeg(read),
                    Is.False, staleKey);
                Assert.That(M04DepartureFacts.TryMarkSailed(read, observedWrite),
                    Is.False, staleKey);
                Assert.That(sailingWrites, Is.Zero, staleKey);
                Assert.That(read(M04DepartureFacts.SailedKey), Is.Zero, staleKey);
                Assert.That(read(M04DepartureFacts.SailingSequenceKey), Is.Zero,
                    staleKey);
                store[staleKey] = 0;
            }

            Assert.That(M04DepartureFacts.CanBeginFreshFogLeg(read), Is.True);
            Assert.That(M04DepartureFacts.TryMarkSailed(read, observedWrite),
                Is.True);
            Assert.That(read(M04DepartureFacts.SailingSequenceKey), Is.EqualTo(1));
            Assert.That(read(M04DepartureFacts.SailedKey), Is.EqualTo(1));
        }

        [Test]
        public void CastOffSequenceSurvivesResetAndChangesOnIdenticalReplayedVoyage()
        {
            var store = new Dictionary<string, int>();
            Func<string, int> read = key =>
                store.TryGetValue(key, out int value) ? value : 0;
            Action<string, int> write = (key, value) => store[key] = value;
            var manifest = ReadyManifest(M03VoyageChoice.QiQueMerchant,
                officialStamp: false, escortFlag: false);

            Assert.That(M04DepartureFacts.TryRecord(manifest, write), Is.True);
            Assert.That(read(M04DepartureFacts.SailingSequenceKey), Is.Zero,
                "Legacy saves begin without a cast-off identity.");
            Assert.That(M04DepartureFacts.TryMarkSailed(read, write), Is.True);
            Assert.That(read(M04DepartureFacts.SailingSequenceKey), Is.EqualTo(1));
            Assert.That(M04DepartureFacts.TryMarkSailed(read, write), Is.False);
            Assert.That(read(M04DepartureFacts.SailingSequenceKey), Is.EqualTo(1));

            Assert.That(M04DepartureFacts.TryResetForNewVisit(write), Is.True);
            Assert.That(read(M04DepartureFacts.SailingSequenceKey), Is.EqualTo(1),
                "Replay invalidates departure facts, never the monotonically increasing identity.");
            Assert.That(M04DepartureFacts.TryRecord(manifest, write), Is.True);
            Assert.That(M04DepartureFacts.TryMarkSailed(read, write), Is.True);
            Assert.That(read(M04DepartureFacts.SailingSequenceKey), Is.EqualTo(2));

            Assert.That(M04DepartureFacts.TryResetForNewVisit(write), Is.True);
            Assert.That(M04DepartureFacts.TryRecord(manifest, write), Is.True);
            write(M04DepartureFacts.SailingSequenceKey, int.MaxValue);
            Assert.That(M04DepartureFacts.TryMarkSailed(read, write), Is.False);
            Assert.That(read(M04DepartureFacts.SailedKey), Is.Zero,
                "Sequence overflow must not turn a prepared ship into a sailed ship.");
            write(M04DepartureFacts.SailingSequenceKey, -1);
            Assert.That(M04DepartureFacts.TryMarkSailed(read, write), Is.False);
            Assert.That(read(M04DepartureFacts.SailedKey), Is.Zero);
        }

        [Test]
        public void UninitializedMakinomGuardsReturnFalseWithoutThrowing()
        {
            var confirmed = ReadyManifest(M03VoyageChoice.QiQueMerchant);
            Assert.That(M04DepartureFacts.TryResetForNewVisit(), Is.False);
            Assert.That(M04DepartureFacts.TryRecord(confirmed), Is.False);
            Assert.That(M04DepartureFacts.TryRead(out M04DepartureRecord? record), Is.False);
            Assert.That(record, Is.Null);
            Assert.That(M04DepartureFacts.TryMarkSailed(), Is.False);
            Assert.That(M04DepartureFacts.TryReadSailed(out bool sailed), Is.False);
            Assert.That(sailed, Is.False);
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
        public void StartingAnotherM03VoyageClearsEveryEarlierOutboundFact()
        {
            var store = new Dictionary<string, int>();
            Action<string, int> write = (key, value) => store[key] = value;
            Func<string, int> read = key =>
                store.TryGetValue(key, out int value) ? value : 0;

            var previous = ReadyManifest(M03VoyageChoice.LuJunEscort,
                officialStamp: false, escortFlag: true);
            Assert.That(M04DepartureFacts.TryRecord(previous, write), Is.True);
            var ledger = new M04RosterLedger();
            ledger.AddEntry("dock-01", "Actually confirmed",
                M04RosterStatus.ConfirmedAtDock);
            Assert.That(M04RosterFacts.TryRecord(ledger, write), Is.True);
            var fog = new FogListeningSession(
                new[] { new FogListeningCue(FogBearing.Ahead,
                    FogCueKind.ClearWater, 80) },
                maxListenFocus: 1, maxHullIntegrity: 2);
            fog.ApplyHelmAction(FogHelmAction.HoldCourse);
            Assert.That(FogListeningFacts.TryRecord(fog, write), Is.True);
            write(M04QiQueCupFacts.CupCarriedKey, 1);
            write(M04XiaodouFarewellFacts.RealLineKey, 2);

            Assert.That(M04DepartureFacts.TryRead(read,
                out M04DepartureRecord? oldDeparture), Is.True);
            Assert.That(oldDeparture.HasValue, Is.True);
            Assert.That(M04RosterFacts.TryRead(read,
                out M04RosterSummaryRecord? oldRoster), Is.True);
            Assert.That(oldRoster.HasValue, Is.True);
            Assert.That(FogListeningFacts.TryRead(read,
                out FogListeningRecord? oldFog), Is.True);
            Assert.That(oldFog.HasValue, Is.True);

            Assert.That(ReturnOpeningContinuation.TryResetPreviousOutboundFacts(null),
                Is.False);
            Assert.That(ReturnOpeningContinuation.TryResetPreviousOutboundFacts(write),
                Is.True);
            Assert.That(M04DepartureFacts.TryRead(read,
                out M04DepartureRecord? departure), Is.True);
            Assert.That(departure, Is.Null);
            Assert.That(M04RosterFacts.TryRead(read,
                out M04RosterSummaryRecord? roster), Is.True);
            Assert.That(roster, Is.Null);
            Assert.That(FogListeningFacts.TryRead(read,
                out FogListeningRecord? fogLeg), Is.True);
            Assert.That(fogLeg, Is.Null);
            Assert.That(read(M04QiQueCupFacts.CupCarriedKey), Is.Zero);
            Assert.That(read(M04XiaodouFarewellFacts.RealLineKey), Is.Zero);
            Assert.That(read(M04DepartureFacts.RouteKey), Is.Zero);
            Assert.That(read(M04RosterFacts.TotalEntriesKey), Is.Zero);
            Assert.That(read(FogListeningFacts.SucceededKey), Is.Zero);
        }

        [Test]
        public void InterruptedNewVoyageResetCannotPublishOldDepartureRosterOrFogLeg()
        {
            var store = new Dictionary<string, int>();
            Action<string, int> write = (key, value) => store[key] = value;
            Func<string, int> read = key =>
                store.TryGetValue(key, out int value) ? value : 0;

            Assert.That(M04DepartureFacts.TryRecord(
                ReadyManifest(M03VoyageChoice.QiQueMerchant), write), Is.True);
            var ledger = new M04RosterLedger();
            ledger.AddEntry("dock-01", "Confirmed", M04RosterStatus.ConfirmedAtDock);
            Assert.That(M04RosterFacts.TryRecord(ledger, write), Is.True);
            var fog = new FogListeningSession(
                new[] { new FogListeningCue(FogBearing.Ahead, FogCueKind.ClearWater, 90) },
                maxListenFocus: 1, maxHullIntegrity: 2);
            fog.ApplyHelmAction(FogHelmAction.HoldCourse);
            Assert.That(FogListeningFacts.TryRecord(fog, write), Is.True);

            Assert.Throws<InvalidOperationException>(() =>
                ReturnOpeningContinuation.TryResetPreviousOutboundFacts((key, value) =>
                {
                    if (key == M04DepartureFacts.AssignedCrewKey)
                        throw new InvalidOperationException("Interrupted voyage reset");
                    write(key, value);
                }));
            Assert.That(read(M04DepartureFacts.ConfirmedKey), Is.Zero);
            Assert.That(read(M04RosterFacts.ReconciledKey), Is.Zero);
            Assert.That(read(FogListeningFacts.CompletedKey), Is.Zero);
            Assert.That(M04DepartureFacts.TryRead(read,
                out M04DepartureRecord? departure), Is.True);
            Assert.That(departure, Is.Null);
            Assert.That(M04RosterFacts.TryRead(read,
                out M04RosterSummaryRecord? roster), Is.True);
            Assert.That(roster, Is.Null);
            Assert.That(FogListeningFacts.TryRead(read,
                out FogListeningRecord? fogLeg), Is.True);
            Assert.That(fogLeg, Is.Null);

            Assert.That(ReturnOpeningContinuation.TryResetPreviousOutboundFacts(write),
                Is.True, "The exact same M03 visit can retry the invalidation.");
            Assert.That(read(M04DepartureFacts.AssignedCrewKey), Is.Zero);
            Assert.That(read(M04RosterFacts.ConfirmedAtDockKey), Is.Zero);
            Assert.That(read(FogListeningFacts.HullIntegrityKey), Is.Zero);
        }

        [Test]
        public void InterruptedDepartureWriteNeverLeavesAnOldConfirmationActive()
        {
            var store = new Dictionary<string, int>();
            Action<string, int> setInt = (key, value) => store[key] = value;
            Func<string, int> getInt = key => store.TryGetValue(key, out int value) ? value : 0;

            var previous = ReadyManifest(M03VoyageChoice.QiQueMerchant);
            Assert.That(M04DepartureFacts.TryRecord(previous, setInt), Is.True);
            Assert.That(M04DepartureFacts.TryRead(getInt, out M04DepartureRecord? before), Is.True);
            Assert.That(before.HasValue, Is.True);

            var replacement = ReadyManifest(M03VoyageChoice.LuJunEscort,
                crew: 4, berths: 20, returnees: 16,
                officialStamp: false, escortFlag: true);
            Assert.Throws<InvalidOperationException>(() =>
                M04DepartureFacts.TryRecord(replacement, (key, value) =>
                {
                    if (key == M04DepartureFacts.ReturneesToBookKey)
                        throw new InvalidOperationException("Simulated interrupted write");
                    store[key] = value;
                }));

            Assert.That(store[M04DepartureFacts.ConfirmedKey], Is.Zero);
            Assert.That(M04DepartureFacts.TryRead(getInt, out M04DepartureRecord? interrupted),
                Is.True);
            Assert.That(interrupted, Is.Null,
                "A partial voyage update must not reuse the old confirmation flag.");

            var writes = new List<string>();
            Assert.That(M04DepartureFacts.TryRecord(replacement, (key, value) =>
            {
                writes.Add(key);
                store[key] = value;
            }), Is.True);
            Assert.That(writes[0], Is.EqualTo(M04DepartureFacts.ConfirmedKey));
            Assert.That(writes[writes.Count - 1], Is.EqualTo(M04DepartureFacts.ConfirmedKey));
            Assert.That(M04DepartureFacts.TryRead(getInt, out M04DepartureRecord? committed),
                Is.True);
            Assert.That(committed.HasValue, Is.True);
            Assert.That(committed.Value.Route, Is.EqualTo(M03VoyageChoice.LuJunEscort));
            Assert.That(committed.Value.ReturneesToBook, Is.EqualTo(16));
        }

        [Test]
        public void InvalidSavedFlagValuesCannotBecomeAConfirmedMerchantDeparture()
        {
            var valid = new Dictionary<string, int>();
            var manifest = ReadyManifest(M03VoyageChoice.QiQueMerchant,
                officialStamp: false, escortFlag: false);
            Assert.That(M04DepartureFacts.TryRecord(
                manifest, (key, value) => valid[key] = value), Is.True);

            foreach (string key in new[]
            {
                M04DepartureFacts.OfficialStampKey,
                M04DepartureFacts.EscortFlagKey
            })
            {
                foreach (int invalid in new[] { -1, 2, int.MaxValue })
                {
                    var corrupted = new Dictionary<string, int>(valid);
                    corrupted[key] = invalid;
                    Func<string, int> read = candidate =>
                        corrupted.TryGetValue(candidate, out int value) ? value : 0;

                    Assert.That(M04DepartureFacts.TryRead(read,
                        out M04DepartureRecord? record), Is.True);
                    Assert.That(record, Is.Null,
                        "Saved flag " + key + " cannot accept value " + invalid + ".");
                }
            }

            Func<string, int> original = key =>
                valid.TryGetValue(key, out int value) ? value : 0;
            Assert.That(M04DepartureFacts.TryRead(original,
                out M04DepartureRecord? restored), Is.True);
            Assert.That(restored.HasValue, Is.True);
            Assert.That(restored.Value.Route, Is.EqualTo(M03VoyageChoice.QiQueMerchant));
        }


        [Test]
        public void AChangedOrCorruptM03RouteCannotSailAnOldConfirmedHull()
        {
            var store = new Dictionary<string, int>();
            Action<string, int> write = (key, value) => store[key] = value;
            Func<string, int> read = key =>
                store.TryGetValue(key, out int value) ? value : 0;

            Assert.That(M04DepartureFacts.TryRecord(
                ReadyManifest(M03VoyageChoice.QiQueMerchant,
                    officialStamp: false, escortFlag: false), write), Is.True);

            // The player changed the actual M03 route after this manifest
            // was confirmed. A direct host call must not sail that old ship.
            var newVisit = new M03LuJunEncounter(M02BridgeEvidence.None, true);
            newVisit.Begin();
            newVisit.ObserveRollCall();
            newVisit.Ask(M03LuJunOpening.NeedAShip);
            newVisit.ChooseVoyage(M03VoyageChoice.LuJunEscort);
            Assert.That(M03LuJunFacts.TryRecord(newVisit, write), Is.True);

            int sailingWrites = 0;
            Action<string, int> observedWrite = (key, value) =>
            {
                if (key == M04DepartureFacts.SailedKey)
                    sailingWrites++;
                write(key, value);
            };
            Assert.That(M04DepartureFacts.TryMarkSailed(read, observedWrite), Is.False);
            Assert.That(read(M04DepartureFacts.SailedKey), Is.Zero);
            Assert.That(sailingWrites, Is.Zero);

            // Invalid M03 route or an interrupted dialogue cannot authorize
            // the same voyage either, even if its route integer looks valid.
            write(M03LuJunFacts.VoyageKey, 999);
            Assert.That(M04DepartureFacts.TryMarkSailed(read, observedWrite), Is.False);
            write(M03LuJunFacts.VoyageKey,
                (int)M03VoyageChoice.QiQueMerchant + 1);
            int savedSecrecy = read(M03LuJunFacts.SecrecyKey);
            write(M03LuJunFacts.SecrecyKey, 999);
            Assert.That(M04DepartureFacts.TryMarkSailed(read, observedWrite), Is.False);

            // Restoring the complete current itinerary permits exactly one
            // real cast-off, without rewriting the confirmed manifest.
            write(M03LuJunFacts.SecrecyKey, savedSecrecy);
            Assert.That(M04DepartureFacts.TryMarkSailed(read, observedWrite), Is.True);
            Assert.That(read(M04DepartureFacts.SailedKey), Is.EqualTo(1));
            Assert.That(sailingWrites, Is.EqualTo(1));
            Assert.That(M04DepartureFacts.TryMarkSailed(read, observedWrite), Is.False);
        }

        [Test]
        public void ActualSailingRequiresConfirmedReadinessAndNeverFollowsItAutomatically()
        {
            var store = new Dictionary<string, int>();
            Action<string, int> setInt = (key, value) => store[key] = value;
            Func<string, int> getInt = key =>
                store.TryGetValue(key, out int value) ? value : 0;

            Assert.That(M04DepartureFacts.TryMarkSailed(getInt, setInt), Is.False);
            Assert.That(M04DepartureFacts.TryReadSailed(getInt, out bool sailed), Is.True);
            Assert.That(sailed, Is.False);

            var confirmed = ReadyManifest(M03VoyageChoice.QiQueMerchant,
                officialStamp: false, escortFlag: false);
            Assert.That(M04DepartureFacts.TryRecord(confirmed, setInt), Is.True);
            Assert.That(M04DepartureFacts.TryReadSailed(getInt, out sailed), Is.True);
            Assert.That(sailed, Is.False,
                "Approving a vessel and berths must not claim the ship cast off.");

            Assert.That(M04DepartureFacts.TryMarkSailed(getInt, setInt), Is.True);
            Assert.That(M04DepartureFacts.TryReadSailed(getInt, out sailed), Is.True);
            Assert.That(sailed, Is.True);
            Assert.That(M04DepartureFacts.TryMarkSailed(getInt, setInt), Is.False,
                "The same actual departure cannot be reported twice.");

            // Changing the next itinerary invalidates the old sailing fact.
            Assert.That(M04DepartureFacts.TryRecord(confirmed, setInt), Is.True);
            Assert.That(M04DepartureFacts.TryReadSailed(getInt, out sailed), Is.True);
            Assert.That(sailed, Is.False);
            Assert.That(M04DepartureFacts.TryMarkSailed(getInt, setInt), Is.True);
            Assert.That(ReturnOpeningContinuation.TryResetPreviousOutboundFacts(setInt),
                Is.True);
            Assert.That(M04DepartureFacts.TryReadSailed(getInt, out sailed), Is.True);
            Assert.That(sailed, Is.False);
            Assert.That(store[M04DepartureFacts.SailedKey], Is.Zero);
        }

        [Test]
        public void CorruptOrInterruptedReadinessNeverMakesAStaleCastOffValid()
        {
            var store = new Dictionary<string, int>();
            Action<string, int> setInt = (key, value) => store[key] = value;
            Func<string, int> getInt = key =>
                store.TryGetValue(key, out int value) ? value : 0;

            Assert.That(M04DepartureFacts.TryRecord(
                ReadyManifest(M03VoyageChoice.HanQiaoOfficial), setInt), Is.True);
            store[M04DepartureFacts.SailedKey] = 2;
            Assert.That(M04DepartureFacts.TryMarkSailed(getInt, setInt), Is.False);
            Assert.That(M04DepartureFacts.TryReadSailed(getInt, out bool sailed), Is.True);
            Assert.That(sailed, Is.False);

            store[M04DepartureFacts.SailedKey] = 0;
            store[M04DepartureFacts.AvailableBerthsKey] = 0;
            Assert.That(M04DepartureFacts.TryMarkSailed(getInt, setInt), Is.False);
            store[M04DepartureFacts.SailedKey] = 1;
            Assert.That(M04DepartureFacts.TryReadSailed(getInt, out sailed), Is.True);
            Assert.That(sailed, Is.False,
                "A stale flag cannot bypass a malformed confirmed manifest.");

            // Interrupted replacement clears both the readiness and cast-off
            // markers before it can expose any partially written next trip.
            store[M04DepartureFacts.AvailableBerthsKey] = 18;
            Assert.Throws<InvalidOperationException>(() =>
                M04DepartureFacts.TryRecord(
                    ReadyManifest(M03VoyageChoice.LuJunEscort,
                        officialStamp: false, escortFlag: true),
                    (key, value) =>
                    {
                        if (key == M04DepartureFacts.RouteKey)
                            throw new InvalidOperationException("Interrupted route write");
                        setInt(key, value);
                    }));
            Assert.That(store[M04DepartureFacts.ConfirmedKey], Is.Zero);
            Assert.That(store[M04DepartureFacts.SailedKey], Is.Zero);
            Assert.That(M04DepartureFacts.TryReadSailed(getInt, out sailed), Is.True);
            Assert.That(sailed, Is.False);
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
