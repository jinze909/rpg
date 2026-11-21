using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M05OuterHarborFlowHostTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedCompletedCrossing(Dictionary<string, int> values,
            bool sailed = true, bool crossedFog = true)
        {
            Action<string, int> write = (key, value) => values[key] = value;
            Func<string, int> read = Reader(values);
            var manifest = new M04DepartureManifest(M03VoyageChoice.QiQueMerchant,
                "Verified civilian hull", "A Yuan", 4, "Shallow tender",
                true, 16, 8, "Named receiver", false, false);
            Assert.That(manifest.ConfirmDeparture(), Is.True);
            Assert.That(M04DepartureFacts.TryRecord(manifest, write), Is.True);
            if (sailed)
                Assert.That(M04DepartureFacts.TryMarkSailed(read, write), Is.True);
            if (crossedFog)
            {
                var fog = new FogListeningSession(
                    new[] { new FogListeningCue(FogBearing.Ahead,
                        FogCueKind.ClearWater, 80) },
                    maxListenFocus: 1, maxHullIntegrity: 2);
                fog.ApplyHelmAction(FogHelmAction.HoldCourse);
                Assert.That(fog.IsComplete && fog.Succeeded, Is.True);
                Assert.That(FogListeningFacts.TryRecord(fog, write), Is.True);
            }
        }

        [Test]
        public void LandingCheckFromOldSailingCannotConfirmAnIdenticalReplayedCrossing()
        {
            var root = new GameObject("M05 identical sailing landing protection");
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            var landing = new M05LandingReport(3, 8, true, true);
            M05OuterHarborFlowHost oldHost = null;
            M05OuterHarborFlowHost currentHost = null;
            try
            {
                int oldSailing = read(M04DepartureFacts.SailingSequenceKey);
                Assert.That(M05OuterHarborFlowHost.TryOpen(root.transform, landing,
                    read, write, _ => Assert.Fail("Old landing cannot complete"),
                    out oldHost), Is.True);

                // M03 begins a fresh trip while the old landing canvas remains.
                Assert.That(ReturnOpeningContinuation.TryResetPreviousOutboundFacts(write),
                    Is.True);
                Assert.Throws<InvalidOperationException>(() =>
                    oldHost.LandingCheckView.ConfirmButton.onClick.Invoke());
                Assert.That(read(M05LandingFacts.ConfirmedKey), Is.Zero);

                // The newly sailed hull and observed report deliberately match
                // every old value; only the monotonic cast-off identity differs.
                SeedCompletedCrossing(values);
                Assert.That(read(M04DepartureFacts.SailingSequenceKey),
                    Is.GreaterThan(oldSailing));
                Assert.That(M05LandingFacts.CanRecord(landing, read), Is.True);
                Assert.Throws<InvalidOperationException>(() =>
                    oldHost.LandingCheckView.ConfirmButton.onClick.Invoke());
                Assert.That(oldHost.Stage, Is.EqualTo(M05OuterHarborFlowStage.LandingCheck));
                Assert.That(oldHost.LandingCheckView.ConfirmButton.interactable, Is.True);
                Assert.That(read(M05LandingFacts.ConfirmedKey), Is.Zero);

                oldHost.Dispose();
                oldHost = null;
                Assert.That(M05OuterHarborFlowHost.TryOpen(root.transform, landing,
                    read, write, _ => { }, out currentHost), Is.True);
                currentHost.LandingCheckView.ConfirmButton.onClick.Invoke();
                Assert.That(read(M05LandingFacts.ConfirmedKey), Is.EqualTo(1));
                Assert.That(currentHost.Stage,
                    Is.EqualTo(M05OuterHarborFlowStage.ArrivalSession));
            }
            finally
            {
                oldHost?.Dispose();
                currentHost?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CorruptSeranDecisionRejectsFreshAndSavedArrivalBeforeShowingUnfinishableUi()
        {
            foreach (bool landingSaved in new[] { false, true })
            {
                var root = new GameObject("M05 corrupt letters entry");
                var values = new Dictionary<string, int>();
                SeedCompletedCrossing(values);
                var report = new M05LandingReport(3, 8, true, true);
                var read = Reader(values);
                Action<string, int> seed = (key, val) => values[key] = val;
                if (landingSaved)
                    Assert.That(M05LandingFacts.TryRecord(report, read, seed), Is.True);
                values[M05LettersFacts.DecisionKey] = 99;
                int writes = 0;
                try
                {
                    Assert.That(M05OuterHarborFlowHost.TryOpen(root.transform,
                        landingSaved ? (M05LandingReport?)null : report,
                        read, (key, val) => { writes++; values[key] = val; },
                        _ => { }, out var host), Is.False);
                    Assert.That(host, Is.Null);
                    Assert.That(root.transform.childCount, Is.Zero);
                    Assert.That(values[M05LettersFacts.DecisionKey], Is.EqualTo(99));
                    Assert.That(writes, Is.Zero);
                    if (landingSaved)
                    {
                        Assert.That(M05OuterHarborArrivalSession.TryOpen(
                            root.transform, read, seed, _ => { }, out var session),
                            Is.False);
                        Assert.That(session, Is.Null);
                        Assert.That(root.transform.childCount, Is.Zero);
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        [Test]
        public void TornOrOrphanedM05LandingFactsCannotOpenFreshArrival()
        {
            var root = new GameObject("M05 torn arrival entry");
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            var observed = new M05LandingReport(3, 8, true, true);
            string[] orphanKeys =
            {
                M05LandingFacts.CrewArrivedKey,
                M05LandingFacts.ReadyBerthsKey,
                M05LandingFacts.TenderArrivedKey,
                M05LandingFacts.ReceiverMetKey,
                M05LettersFacts.DecisionKey,
                M05HarborVisitFacts.VisitedMaskKey
            };

            try
            {
                foreach (string key in orphanKeys)
                {
                    Assert.That(M05LandingFacts.TryResetForNewVisit(write), Is.True);
                    Assert.That(M05LettersFacts.TryResetForNewVoyage(write), Is.True);
                    Assert.That(M05HarborVisitFacts.TryResetForNewVoyage(write), Is.True);
                    values[key] = 1;
                    int writes = 0;
                    Assert.That(read(M05LandingFacts.ConfirmedKey), Is.Zero);
                    Assert.That(M05OuterHarborFlowHost.TryOpen(
                        root.transform, observed, read,
                        (k, v) => { writes++; values[k] = v; }, _ => { },
                        out M05OuterHarborFlowHost rejected), Is.False,
                        $"Fresh M05 arrival must refuse orphaned key '{key}'.");
                    Assert.That(rejected, Is.Null);
                    Assert.That(root.transform.childCount, Is.Zero);
                    Assert.That(values[key], Is.EqualTo(1));
                    Assert.That(writes, Is.Zero);
                }

                Assert.That(M05LandingFacts.TryResetForNewVisit(write), Is.True);
                Assert.That(M05LettersFacts.TryResetForNewVoyage(write), Is.True);
                Assert.That(M05HarborVisitFacts.TryResetForNewVoyage(write), Is.True);
                Assert.That(M05OuterHarborFlowHost.TryOpen(root.transform,
                    observed, read, write, _ => { },
                    out M05OuterHarborFlowHost clean), Is.True);
                clean.Dispose();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PreviousVoyageSeranChoiceIsReaskedAfterANewVerifiedLanding()
        {
            var root = new GameObject("M05 stale Seran choice on new arrival");
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            var landing = new M05LandingReport(3, 8, true, true);
            M05OuterHarborFlowHost host = null;
            try
            {
                Assert.That(M05LandingFacts.TryRecord(landing, read, write), Is.True);
                Assert.That(M05LettersFacts.TryRecord(
                    M05LetterDecision.TakePacket, read, write), Is.True);
                int firstArrival = read(M05LandingFacts.ArrivalSequenceKey);
                Assert.That(read(M05LettersFacts.DecisionKey),
                    Is.EqualTo((int)M05LetterDecision.TakePacket));

                // Simulate a torn reset that invalidates the old crossing and
                // landing but leaves the previous Seran choice untouched.
                Assert.That(M05LandingFacts.TryResetForNewVisit(write), Is.True);
                Assert.That(M04DepartureFacts.TryResetForNewVisit(write), Is.True);
                Assert.That(FogListeningFacts.TryResetForNewLeg(write), Is.True);
                SeedCompletedCrossing(values);
                Assert.That(M05LandingFacts.TryRecord(landing, read, write), Is.True);
                int secondArrival = read(M05LandingFacts.ArrivalSequenceKey);
                Assert.That(secondArrival, Is.GreaterThan(firstArrival));
                Assert.That(read(M05LettersFacts.DecisionKey),
                    Is.EqualTo((int)M05LetterDecision.TakePacket),
                    "The test deliberately retains the old raw save value.");
                Assert.That(M05LettersFacts.TryRead(
                    read, out M05LetterDecision? stale), Is.True);
                Assert.That(stale, Is.Null);

                // Resuming a real, newly verified landing must show Seran's
                // choice again instead of silently carrying the old packet.
                Assert.That(M05OuterHarborFlowHost.TryOpen(
                    root.transform, null, read, write, _ => { }, out host), Is.True);
                Assert.That(host.ArrivalSession.Stage,
                    Is.EqualTo(M05OuterHarborStage.SeranLetters));
                Assert.That(host.ArrivalSession.LetterDecision, Is.Null);
                Assert.That(host.ArrivalSession.SeranView, Is.Not.Null);

                host.ArrivalSession.SeranView.LeaveButton.onClick.Invoke();
                host.ArrivalSession.SeranView.ContinueButton.onClick.Invoke();
                Assert.That(M05LettersFacts.TryRead(
                    read, out M05LetterDecision? current), Is.True);
                Assert.That(current, Is.EqualTo(M05LetterDecision.LeaveForNextBoat));
                Assert.That(M05LettersFacts.LettersCarried(current), Is.Zero);
                Assert.That(read(M05LettersFacts.DecisionArrivalIdentityKey),
                    Is.EqualTo(secondArrival + 1));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CorruptCommittedLandingDoesNotOpenAnUnfinishableLandingScreen()
        {
            var root = new GameObject("M05 corrupted confirmed arrival");
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            var observed = new M05LandingReport(3, 8, true, true);
            int writes = 0;
            Action<string, int> write = (key, value) =>
            {
                writes++;
                values[key] = value;
            };

            try
            {
                // Simulate an interrupted or corrupted saved report whose
                // confirmed flag was published despite an impossible crew.
                values[M05LandingFacts.ConfirmedKey] = 1;
                values[M05LandingFacts.CrewArrivedKey] = 99;
                values[M05LandingFacts.ReadyBerthsKey] = 8;
                values[M05LandingFacts.TenderArrivedKey] = 1;
                values[M05LandingFacts.ReceiverMetKey] = 1;
                Assert.That(M05LandingFacts.TryRead(read, out M05LandingReport? corrupt),
                    Is.True);
                Assert.That(corrupt.HasValue, Is.False);

                Assert.That(M05OuterHarborFlowHost.TryOpen(root.transform,
                    observed, read, write, _ => Assert.Fail("Corrupt arrival cannot complete"),
                    out M05OuterHarborFlowHost host), Is.False);
                Assert.That(host, Is.Null);
                Assert.That(root.transform.childCount, Is.Zero);
                Assert.That(writes, Is.Zero);
                Assert.That(values[M05LandingFacts.ConfirmedKey], Is.EqualTo(1));

                // Removing only the confirmation flag does not repair the
                // torn payload. Re-entering the harbor must still fail closed
                // without replacing the corrupted crew with this new visit.
                values[M05LandingFacts.ConfirmedKey] = 0;
                int writesBeforeTornReopen = writes;
                Assert.That(M05OuterHarborFlowHost.TryOpen(root.transform,
                    observed, read, write, _ => { }, out host), Is.False);
                Assert.That(host, Is.Null);
                Assert.That(root.transform.childCount, Is.Zero);
                Assert.That(values[M05LandingFacts.CrewArrivedKey], Is.EqualTo(99));
                Assert.That(writes, Is.EqualTo(writesBeforeTornReopen));

                // Only an explicit new-visit reset clears the corrupt payload.
                // A genuinely clean, unconfirmed arrival remains playable.
                Assert.That(M05LandingFacts.TryResetForNewVisit(write), Is.True);
                Assert.That(M05OuterHarborFlowHost.TryOpen(root.transform,
                    observed, read, write, _ => { }, out host), Is.True);
                Assert.That(host.LandingCheckView, Is.Not.Null);
                host.Dispose();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryOpenRefusesIncompleteCrossingOrInvalidObservedLanding()
        {
            var root = new GameObject("M05 flow gate test");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            var validLanding = new M05LandingReport(3, 10, true, true);
            try
            {
                Assert.That(M05OuterHarborFlowHost.TryOpen(
                    root.transform, validLanding, read, write, _ => { }, out _), Is.False);

                SeedCompletedCrossing(values, sailed: false, crossedFog: true);
                Assert.That(M05OuterHarborFlowHost.TryOpen(
                    root.transform, validLanding, read, write, _ => { }, out _), Is.False);

                Assert.That(M04DepartureFacts.TryMarkSailed(read, write), Is.True);
                Assert.That(M05OuterHarborFlowHost.TryOpen(
                    root.transform, observedLanding: null, read, write, _ => { }, out _), Is.False);
                Assert.That(M05OuterHarborFlowHost.TryOpen(
                    root.transform, new M05LandingReport(3, 5, false, true),
                    read, write, _ => { }, out _), Is.False);
                Assert.That(M05OuterHarborFlowHost.TryOpen(
                    root.transform, new M05LandingReport(5, 5, true, true),
                    read, write, _ => { }, out _), Is.False);
                Assert.That(root.transform.childCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ChangedCurrentRouteCannotOfferAnUnsaveableLandingConfirmation()
        {
            var root = new GameObject("M05 changed route landing gate");
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            int writes = 0;
            Action<string, int> write = (key, value) =>
            {
                writes++;
                values[key] = value;
            };
            var observed = new M05LandingReport(3, 8, true, true);
            try
            {
                // The actual M04 hull sailed the merchant route; a later M03
                // choice from another visit cannot confirm its M05 arrival.
                var newVisit = new M03LuJunEncounter(M02BridgeEvidence.None, true);
                newVisit.Begin();
                newVisit.ObserveRollCall();
                newVisit.Ask(M03LuJunOpening.NeedAShip);
                newVisit.ChooseVoyage(M03VoyageChoice.HanQiaoOfficial);
                Assert.That(M03LuJunFacts.TryRecord(newVisit, write), Is.True);
                writes = 0;

                Assert.That(M05LandingFacts.CanRecord(observed, read), Is.False);
                Assert.That(M05OuterHarborFlowHost.TryOpen(root.transform,
                    observed, read, write, _ => Assert.Fail("Wrong ship arrived"),
                    out M05OuterHarborFlowHost rejected), Is.False);
                Assert.That(rejected, Is.Null);
                Assert.That(root.transform.childCount, Is.Zero);
                Assert.That(writes, Is.Zero);
                Assert.That(values.ContainsKey(M05LandingFacts.ConfirmedKey), Is.False);

                // A valid current route matching the actually sailed ship is
                // still allowed; only the contradictory journey is blocked.
                values[M03LuJunFacts.VoyageKey] =
                    (int)M03VoyageChoice.QiQueMerchant + 1;
                Assert.That(M05LandingFacts.CanRecord(observed, read), Is.True);
                Assert.That(M05OuterHarborFlowHost.TryOpen(root.transform,
                    observed, read, write, _ => { },
                    out M05OuterHarborFlowHost valid), Is.True);
                valid.Dispose();
                Assert.That(values.ContainsKey(M05LandingFacts.ConfirmedKey), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FullOuterHarborFlowSequencesLandingCheckSeranAndHarborWalkWithSingleCanvas()
        {
            var root = new GameObject("M05 full flow test");
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            M05OuterHarborFlowHost host = null;
            try
            {
                int completions = 0;
                M05OuterHarborOutcome outcome = default;
                Assert.That(M05OuterHarborFlowHost.TryOpen(
                    root.transform, new M05LandingReport(3, 10, true, true),
                    read, write, result => { completions++; outcome = result; },
                    out host), Is.True);
                Assert.That(host.Stage, Is.EqualTo(M05OuterHarborFlowStage.LandingCheck));
                Assert.That(host.LandingCheckView, Is.Not.Null);
                Assert.That(host.ArrivalSession, Is.Null);
                Assert.That(root.transform.childCount, Is.EqualTo(1));

                host.LandingCheckView.ConfirmButton.onClick.Invoke();
                Assert.That(M05LandingFacts.TryRead(read, out M05LandingReport? persisted), Is.True);
                Assert.That(persisted.HasValue, Is.True);
                Assert.That(host.Stage, Is.EqualTo(M05OuterHarborFlowStage.ArrivalSession));
                Assert.That(host.LandingCheckView, Is.Null);
                Assert.That(host.ArrivalSession, Is.Not.Null);
                Assert.That(host.ArrivalSession.Stage, Is.EqualTo(M05OuterHarborStage.SeranLetters));
                Assert.That(root.transform.childCount, Is.EqualTo(1));

                host.ArrivalSession.SeranView.TakeButton.onClick.Invoke();
                host.ArrivalSession.SeranView.ContinueButton.onClick.Invoke();
                Assert.That(host.ArrivalSession.Stage, Is.EqualTo(M05OuterHarborStage.HarborWalk));
                Assert.That(root.transform.childCount, Is.EqualTo(1));

                host.ArrivalSession.HarborWalkView.AdvanceButton.onClick.Invoke();
                host.ArrivalSession.HarborWalkView.AdvanceButton.onClick.Invoke();
                host.ArrivalSession.HarborWalkView.AdvanceButton.onClick.Invoke();

                Assert.That(completions, Is.EqualTo(1));
                Assert.That(host.Stage, Is.EqualTo(M05OuterHarborFlowStage.Completed));
                Assert.That(host.ArrivalSession, Is.Null);
                Assert.That(outcome.Landing.CrewArrived, Is.EqualTo(3));
                Assert.That(outcome.Landing.ReadyRescueBerths, Is.EqualTo(10));
                Assert.That(outcome.LetterDecision, Is.EqualTo(M05LetterDecision.TakePacket));
                Assert.That(outcome.LettersCarried, Is.EqualTo(M05LettersFacts.PacketLetterCount));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SavedLandingResumesDirectlyAtArrivalSessionWithoutReconfirmingLanding()
        {
            var root = new GameObject("M05 flow resume test");
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            Assert.That(M05LandingFacts.TryRecord(
                new M05LandingReport(2, 0, false, false), read, write), Is.True);

            M05OuterHarborFlowHost host = null;
            try
            {
                Assert.That(M05OuterHarborFlowHost.TryOpen(
                    root.transform, observedLanding: null, read, write, _ => { },
                    out host), Is.True);
                Assert.That(host.Stage, Is.EqualTo(M05OuterHarborFlowStage.ArrivalSession));
                Assert.That(host.LandingCheckView, Is.Null);
                Assert.That(host.ArrivalSession, Is.Not.Null);
                Assert.That(host.ArrivalSession.Landing.HasUsableLandingCapacity, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InterruptedLandingWriteAllowsRetryWithoutAdvancingToArrivalSession()
        {
            var root = new GameObject("M05 flow retry test");
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            bool failNext = true;
            Action<string, int> flakyWrite = (key, value) =>
            {
                if (failNext && key == M05LandingFacts.ReadyBerthsKey)
                {
                    failNext = false;
                    throw new InvalidOperationException("Interrupted landing save");
                }
                values[key] = value;
            };

            M05OuterHarborFlowHost host = null;
            try
            {
                Assert.That(M05OuterHarborFlowHost.TryOpen(
                    root.transform, new M05LandingReport(4, 8, true, true),
                    read, flakyWrite, _ => { }, out host), Is.True);
                Assert.Throws<InvalidOperationException>(() =>
                    host.LandingCheckView.ConfirmButton.onClick.Invoke());
                Assert.That(host.Stage, Is.EqualTo(M05OuterHarborFlowStage.LandingCheck));
                Assert.That(host.LandingCheckView.ConfirmButton.interactable, Is.True);
                Assert.That(host.ArrivalSession, Is.Null);

                host.LandingCheckView.ConfirmButton.onClick.Invoke();
                Assert.That(host.Stage, Is.EqualTo(M05OuterHarborFlowStage.ArrivalSession));
                Assert.That(host.ArrivalSession, Is.Not.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SavedLandingSurvivesInterruptedScreenHandoffAndRetriesWithoutDuplicateWrite()
        {
            var root = new GameObject("M05 landing handoff retry test");
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> ordinaryRead = Reader(values);
            bool failFirstLetterRead = true;
            Func<string, int> flakyRead = key =>
            {
                // Simulate a temporary read error *after* the landing save
                // succeeded, while opening the following letter scene.
                if (key == M05LettersFacts.DecisionKey &&
                    ordinaryRead(M05LandingFacts.ConfirmedKey) == 1 &&
                    failFirstLetterRead)
                {
                    failFirstLetterRead = false;
                    throw new InvalidOperationException("Interrupted next-screen read");
                }
                return ordinaryRead(key);
            };
            int publishedLandings = 0;
            Action<string, int> write = (key, value) =>
            {
                values[key] = value;
                if (key == M05LandingFacts.ConfirmedKey && value == 1)
                    publishedLandings++;
            };

            M05OuterHarborFlowHost host = null;
            try
            {
                Assert.That(M05OuterHarborFlowHost.TryOpen(
                    root.transform, new M05LandingReport(3, 10, true, true),
                    flakyRead, write, _ => { }, out host), Is.True);

                Assert.Throws<InvalidOperationException>(() =>
                    host.LandingCheckView.ConfirmButton.onClick.Invoke());
                Assert.That(publishedLandings, Is.EqualTo(1));
                Assert.That(M05LandingFacts.TryRead(ordinaryRead,
                    out M05LandingReport? saved), Is.True);
                Assert.That(saved.HasValue, Is.True);
                Assert.That(saved.Value.CrewArrived, Is.EqualTo(3));
                Assert.That(host.Stage, Is.EqualTo(M05OuterHarborFlowStage.LandingCheck));
                Assert.That(host.ArrivalSession, Is.Null);
                Assert.That(host.LandingCheckView.ConfirmButton.interactable, Is.True);
                Assert.That(root.transform.childCount, Is.EqualTo(1));

                host.LandingCheckView.ConfirmButton.onClick.Invoke();
                Assert.That(publishedLandings, Is.EqualTo(1),
                    "The retry must reuse the committed landing, not publish another.");
                Assert.That(host.Stage, Is.EqualTo(M05OuterHarborFlowStage.ArrivalSession));
                Assert.That(host.LandingCheckView, Is.Null);
                Assert.That(host.ArrivalSession, Is.Not.Null);
                Assert.That(host.ArrivalSession.Stage, Is.EqualTo(M05OuterHarborStage.SeranLetters));
                Assert.That(root.transform.childCount, Is.EqualTo(1),
                    "The landing canvas must be retired once the next scene is ready.");
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AspectPropagationAndParentTeardownDisposeCleanly()
        {
            var root = new GameObject("M05 flow teardown test");
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            M05OuterHarborFlowHost host = null;
            try
            {
                Assert.Throws<ArgumentNullException>(() =>
                    M05OuterHarborFlowHost.TryOpen(
                        null, new M05LandingReport(3, 6, true, true),
                        read, write, _ => { }, out _));
                Assert.Throws<ArgumentNullException>(() =>
                    M05OuterHarborFlowHost.TryOpen(
                        root.transform, new M05LandingReport(3, 6, true, true),
                        read, write, null, out _));

                Assert.That(M05OuterHarborFlowHost.TryOpen(
                    root.transform, new M05LandingReport(3, 6, true, true),
                    read, write, _ => { }, out host), Is.True);
                Assert.DoesNotThrow(() => host.LayoutForViewportAspect(9f / 16f));
                host.LandingCheckView.ConfirmButton.onClick.Invoke();
                Assert.DoesNotThrow(() => host.LayoutForViewportAspect(16f / 9f));

                UnityEngine.Object.DestroyImmediate(root);
                root = null;
                Assert.DoesNotThrow(() => host.LayoutForViewportAspect(32f / 9f));
                Assert.That(host.IsDisposed, Is.True);
            }
            finally
            {
                host?.Dispose();
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
