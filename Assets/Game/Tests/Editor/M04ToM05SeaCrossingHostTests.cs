using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M04ToM05SeaCrossingHostTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static Action<string, int> Writer(Dictionary<string, int> values)
        {
            return (key, value) => values[key] = value;
        }

        private static void SeedChosenRoute(Action<string, int> setInt,
            M03VoyageChoice route)
        {
            var encounter = new M03LuJunEncounter(M02BridgeEvidence.None, true);
            encounter.Begin();
            encounter.ObserveRollCall();
            encounter.Ask(M03LuJunOpening.NeedAShip);
            encounter.ChooseVoyage(route);
            Assert.That(M03LuJunFacts.TryRecord(encounter, setInt), Is.True);
        }

        private static void SeedVerifiedReadyManifest(Dictionary<string, int> values,
            M03VoyageChoice route = M03VoyageChoice.QiQueMerchant)
        {
            Action<string, int> write = Writer(values);
            SeedChosenRoute(write, route);
            var manifest = new M04DepartureManifest(
                route, "Verified civilian hull", "A Yuan", 4,
                "Shallow tender", true, 16, 8, "Named receiver",
                false, false);
            Assert.That(manifest.ConfirmDeparture(), Is.True);
            Assert.That(M04DepartureFacts.TryRecord(manifest, write), Is.True);
        }

        private static FogListeningSession ClearFogLeg()
        {
            return new FogListeningSession(new[]
            {
                new FogListeningCue(FogBearing.Ahead, FogCueKind.ClearWater, 80)
            }, maxListenFocus: 1, maxHullIntegrity: 2);
        }

        private static FogListeningSession FailingFogLeg()
        {
            return new FogListeningSession(new[]
            {
                new FogListeningCue(FogBearing.Ahead, FogCueKind.ReefSurf, 40)
            }, maxListenFocus: 1, maxHullIntegrity: 1);
        }

        [Test]
        public void FullSeaCrossingFlowsFromCastOffThroughFogToM05OuterHarborWithSingleCanvas()
        {
            var root = new GameObject("M04 to M05 full crossing");
            var values = new Dictionary<string, int>();
            SeedVerifiedReadyManifest(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = Writer(values);
            M04ToM05SeaCrossingHost host = null;
            try
            {
                int completions = 0;
                int fogFailures = 0;
                M05OuterHarborOutcome outcome = default;
                var landing = new M05LandingReport(3, 10, true, true);

                Assert.That(M04ToM05SeaCrossingHost.TryOpen(
                    root.transform, ClearFogLeg, landing, read, write,
                    result => { completions++; outcome = result; },
                    _ => fogFailures++,
                    out host), Is.True);

                Assert.That(host.Stage, Is.EqualTo(M04ToM05SeaCrossingStage.CastOff));
                Assert.That(host.CastOffView, Is.Not.Null);
                Assert.That(host.FogHost, Is.Null);
                Assert.That(host.OuterHarborHost, Is.Null);
                Assert.That(root.transform.childCount, Is.EqualTo(1));

                // Cast off -> opens sailed fog passage with a single active canvas.
                host.CastOffView.CastOffButton.onClick.Invoke();
                Assert.That(values[M04DepartureFacts.SailedKey], Is.EqualTo(1));
                Assert.That(host.Stage, Is.EqualTo(M04ToM05SeaCrossingStage.FogPassage));
                Assert.That(host.CastOffView, Is.Null);
                Assert.That(host.FogHost, Is.Not.Null);
                Assert.That(root.transform.childCount, Is.EqualTo(1));

                // Navigate fog leg -> opens M05 outer-harbor landing check.
                host.FogHost.View.HoldButton.onClick.Invoke();
                host.FogHost.View.ContinueButton.onClick.Invoke();
                Assert.That(values[FogListeningFacts.CompletedKey], Is.EqualTo(1));
                Assert.That(values[FogListeningFacts.SucceededKey], Is.EqualTo(1));
                Assert.That(host.Stage, Is.EqualTo(M04ToM05SeaCrossingStage.OuterHarbor));
                Assert.That(host.FogHost, Is.Null);
                Assert.That(host.OuterHarborHost, Is.Not.Null);
                Assert.That(host.OuterHarborHost.Stage, Is.EqualTo(M05OuterHarborFlowStage.LandingCheck));
                Assert.That(root.transform.childCount, Is.EqualTo(1));

                // Confirm landing -> Seran letters (take packet) -> Harbor walk.
                host.OuterHarborHost.LandingCheckView.ConfirmButton.onClick.Invoke();
                host.OuterHarborHost.ArrivalSession.SeranView.TakeButton.onClick.Invoke();
                host.OuterHarborHost.ArrivalSession.SeranView.ContinueButton.onClick.Invoke();
                host.OuterHarborHost.ArrivalSession.HarborWalkView.AdvanceButton.onClick.Invoke();
                host.OuterHarborHost.ArrivalSession.HarborWalkView.AdvanceButton.onClick.Invoke();
                host.OuterHarborHost.ArrivalSession.HarborWalkView.AdvanceButton.onClick.Invoke();

                Assert.That(fogFailures, Is.Zero);
                Assert.That(completions, Is.EqualTo(1));
                Assert.That(host.Stage, Is.EqualTo(M04ToM05SeaCrossingStage.Completed));
                Assert.That(outcome.Landing.CrewArrived, Is.EqualTo(3));
                Assert.That(outcome.Landing.ReadyRescueBerths, Is.EqualTo(10));
                Assert.That(outcome.Landing.TenderArrived, Is.True);
                Assert.That(outcome.Landing.ReceiverMet, Is.True);
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
        public void DestroyedFogCanvasReopensSameCompletedLegOnNextViewportLayout()
        {
            var root = new GameObject("M04 to M05 fog canvas recovery");
            var values = new Dictionary<string, int>();
            SeedVerifiedReadyManifest(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = Writer(values);
            FogListeningSession leg = ClearFogLeg();
            M04ToM05SeaCrossingHost host = null;
            try
            {
                Assert.That(M04ToM05SeaCrossingHost.TryOpen(
                    root.transform, () => leg, new M05LandingReport(3, 10, true, true),
                    read, write, _ => { },
                    _ => Assert.Fail("A clear fog leg cannot fail."),
                    out host), Is.True);

                host.CastOffView.CastOffButton.onClick.Invoke();
                Assert.That(host.Stage, Is.EqualTo(M04ToM05SeaCrossingStage.FogPassage));
                M04SailedFogLegHost fogHost = host.FogHost;
                FogListeningView originalView = fogHost.View;
                Assert.That(originalView, Is.Not.Null);

                originalView.HoldButton.onClick.Invoke();
                Assert.That(leg.IsComplete, Is.True);
                Assert.That(originalView.ContinueButton.interactable, Is.True);
                Assert.That(values.ContainsKey(FogListeningFacts.CompletedKey), Is.False,
                    "The finished leg is not persisted until the player continues.");

                Canvas canvas = originalView.HoldButton.GetComponentInParent<Canvas>();
                Assert.That(canvas, Is.Not.Null);
                UnityEngine.Object.DestroyImmediate(canvas.gameObject);
                Assert.That(originalView.IsDisposed, Is.True);
                Assert.That(fogHost.HasLiveView, Is.False);

                // The orchestrator already forwards viewport layout to its
                // active fog host. That call must rebuild the destroyed view
                // around the same completed session, not replay the voyage.
                host.LayoutForViewportAspect(16f / 9f);
                Assert.That(host.Stage, Is.EqualTo(M04ToM05SeaCrossingStage.FogPassage));
                Assert.That(host.FogHost, Is.SameAs(fogHost));
                FogListeningView reopenedView = fogHost.View;
                Assert.That(reopenedView, Is.Not.Null);
                Assert.That(reopenedView, Is.Not.SameAs(originalView));
                Assert.That(reopenedView.Session, Is.SameAs(leg));
                Assert.That(reopenedView.ContinueButton.interactable, Is.True);
                Assert.That(leg.IsComplete, Is.True);

                reopenedView.ContinueButton.onClick.Invoke();
                Assert.That(values[FogListeningFacts.CompletedKey], Is.EqualTo(1));
                Assert.That(values[FogListeningFacts.SucceededKey], Is.EqualTo(1));
                Assert.That(host.Stage, Is.EqualTo(M04ToM05SeaCrossingStage.OuterHarbor));
                Assert.That(host.FogHost, Is.Null);
                Assert.That(host.OuterHarborHost, Is.Not.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LiveLandingIsObservedOnlyAfterSailedFogAndMissingEvidenceRetainsRetry()
        {
            var root = new GameObject("M04 to M05 deferred real landing");
            var values = new Dictionary<string, int>();
            SeedVerifiedReadyManifest(values);
            Func<string, int> read = Reader(values);
            int fogWrites = 0, landingWrites = 0;
            Action<string, int> write = (key, value) =>
            {
                if (key == FogListeningFacts.CompletedKey && value == 1)
                    fogWrites++;
                if (key == M05LandingFacts.ConfirmedKey && value == 1)
                    landingWrites++;
                values[key] = value;
            };
            int observations = 0;
            M05LandingReport? actualObservation = null;
            M04ToM05SeaCrossingHost host = null;
            try
            {
                Assert.That(M04ToM05SeaCrossingHost.TryOpenWithLandingObservation(
                    root.transform, ClearFogLeg,
                    () => { observations++; return actualObservation; },
                    read, write, _ => { },
                    _ => Assert.Fail("A clear fog leg cannot fail."),
                    out host), Is.True);
                Assert.That(observations, Is.Zero,
                    "A real M05 arrival cannot be observed before departure.");
                host.CastOffView.CastOffButton.onClick.Invoke();
                Assert.That(host.Stage, Is.EqualTo(M04ToM05SeaCrossingStage.FogPassage));
                Assert.That(observations, Is.Zero);
                host.FogHost.View.HoldButton.onClick.Invoke();
                Assert.That(observations, Is.Zero);
                Assert.Throws<InvalidOperationException>(() =>
                    host.FogHost.View.ContinueButton.onClick.Invoke());
                Assert.That(observations, Is.EqualTo(1));
                Assert.That(fogWrites, Is.EqualTo(1));
                Assert.That(landingWrites, Is.Zero);
                Assert.That(host.FogHost.View.ContinueButton.interactable, Is.True);
                Assert.That(host.OuterHarborHost, Is.Null);
                Assert.That(root.transform.childCount, Is.EqualTo(1));

                // Only a later real dock observation can open landing checks.
                actualObservation = new M05LandingReport(2, 0, false, false);
                host.FogHost.View.ContinueButton.onClick.Invoke();
                Assert.That(observations, Is.EqualTo(2));
                Assert.That(fogWrites, Is.EqualTo(1));
                Assert.That(host.Stage, Is.EqualTo(M04ToM05SeaCrossingStage.OuterHarbor));
                Assert.That(host.OuterHarborHost.LandingCheckView, Is.Not.Null);
                Assert.That(landingWrites, Is.Zero,
                    "Opening the harbor view must not auto-confirm a report.");
                host.OuterHarborHost.LandingCheckView.ConfirmButton.onClick.Invoke();
                Assert.That(landingWrites, Is.EqualTo(1));
                Assert.That(M05LandingFacts.TryRead(read,
                    out M05LandingReport? persisted), Is.True);
                Assert.That(persisted.HasValue, Is.True);
                Assert.That(persisted.Value.ReadyRescueBerths, Is.Zero);
                Assert.That(persisted.Value.TenderArrived, Is.False);
                Assert.That(persisted.Value.ReceiverMet, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InteractiveArrivalObservationOpensOnlyAfterFogAndRecordsPlayerSelectedDockFacts()
        {
            var root = new GameObject("M04 to M05 interactive arrival observation");
            var values = new Dictionary<string, int>();
            SeedVerifiedReadyManifest(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = Writer(values);
            M04ToM05SeaCrossingHost host = null;
            int completions = 0;
            int fogFailures = 0;
            try
            {
                Assert.That(M04ToM05SeaCrossingHost.TryOpenWithInteractiveLandingObservation(
                    root.transform, ClearFogLeg, read, write,
                    _ => completions++, _ => fogFailures++, out host), Is.True);
                Assert.That(host.Stage, Is.EqualTo(M04ToM05SeaCrossingStage.CastOff));
                Assert.That(host.LandingObservationView, Is.Null);
                Assert.That(values.ContainsKey(M05LandingFacts.ConfirmedKey), Is.False);

                host.CastOffView.CastOffButton.onClick.Invoke();
                Assert.That(host.Stage, Is.EqualTo(M04ToM05SeaCrossingStage.FogPassage));
                Assert.That(host.LandingObservationView, Is.Null);
                host.FogHost.View.HoldButton.onClick.Invoke();
                Assert.That(host.LandingObservationView, Is.Null);

                host.FogHost.View.ContinueButton.onClick.Invoke();
                Assert.That(host.Stage, Is.EqualTo(M04ToM05SeaCrossingStage.LandingObservation));
                Assert.That(host.LandingObservationView, Is.Not.Null);
                Assert.That(host.FogHost, Is.Null);
                Assert.That(root.transform.childCount, Is.EqualTo(1));
                Assert.That(values.ContainsKey(M05LandingFacts.ConfirmedKey), Is.False);

                M05LandingObservationView observation = host.LandingObservationView;
                Assert.That(observation.CrewArrived, Is.Zero,
                    "The departure manifest must not pre-fill the number who actually arrived.");
                Assert.That(observation.ReadyRescueBerths, Is.Zero);
                observation.CrewPlusButton.onClick.Invoke();
                observation.CrewPlusButton.onClick.Invoke();
                observation.TenderButton.onClick.Invoke();
                observation.ReceiverButton.onClick.Invoke();
                observation.BerthPlusButton.onClick.Invoke();
                observation.BerthPlusButton.onClick.Invoke();
                observation.BerthPlusButton.onClick.Invoke();
                Assert.That(observation.CanConfirm, Is.True);

                observation.ConfirmButton.onClick.Invoke();
                Assert.That(host.Stage, Is.EqualTo(M04ToM05SeaCrossingStage.OuterHarbor));
                Assert.That(host.LandingObservationView, Is.Null);
                Assert.That(host.OuterHarborHost, Is.Not.Null);
                Assert.That(host.OuterHarborHost.Stage,
                    Is.EqualTo(M05OuterHarborFlowStage.ArrivalSession));
                Assert.That(root.transform.childCount, Is.EqualTo(1));
                Assert.That(M05LandingFacts.TryRead(read,
                    out M05LandingReport? saved), Is.True);
                Assert.That(saved.HasValue, Is.True);
                Assert.That(saved.Value.CrewArrived, Is.EqualTo(2));
                Assert.That(saved.Value.ReadyRescueBerths, Is.EqualTo(3));
                Assert.That(saved.Value.TenderArrived, Is.True);
                Assert.That(saved.Value.ReceiverMet, Is.True);
                Assert.That(completions, Is.Zero);
                Assert.That(fogFailures, Is.Zero);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DelayedTenderAndLeftLettersPassThroughVerbatimWithoutInflatingCapacity()
        {
            var root = new GameObject("M04 to M05 delayed tender");
            var values = new Dictionary<string, int>();
            SeedVerifiedReadyManifest(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = Writer(values);
            M04ToM05SeaCrossingHost host = null;
            try
            {
                M05OuterHarborOutcome outcome = default;
                int completions = 0;
                var delayedLanding = new M05LandingReport(2, 0, false, false);

                Assert.That(M04ToM05SeaCrossingHost.TryOpen(
                    root.transform, ClearFogLeg, delayedLanding, read, write,
                    result => { completions++; outcome = result; },
                    _ => Assert.Fail("Fog leg succeeded and must not invoke failure callback."),
                    out host), Is.True);

                host.CastOffView.CastOffButton.onClick.Invoke();
                host.FogHost.View.HoldButton.onClick.Invoke();
                host.FogHost.View.ContinueButton.onClick.Invoke();
                host.OuterHarborHost.LandingCheckView.ConfirmButton.onClick.Invoke();
                host.OuterHarborHost.ArrivalSession.SeranView.LeaveButton.onClick.Invoke();
                host.OuterHarborHost.ArrivalSession.SeranView.ContinueButton.onClick.Invoke();
                host.OuterHarborHost.ArrivalSession.HarborWalkView.AdvanceButton.onClick.Invoke();
                host.OuterHarborHost.ArrivalSession.HarborWalkView.AdvanceButton.onClick.Invoke();
                host.OuterHarborHost.ArrivalSession.HarborWalkView.AdvanceButton.onClick.Invoke();

                Assert.That(completions, Is.EqualTo(1));
                Assert.That(outcome.Landing.CrewArrived, Is.EqualTo(2));
                Assert.That(outcome.Landing.ReadyRescueBerths, Is.Zero);
                Assert.That(outcome.Landing.TenderArrived, Is.False);
                Assert.That(outcome.Landing.ReceiverMet, Is.False);
                Assert.That(outcome.Landing.HasUsableLandingCapacity, Is.False);
                Assert.That(outcome.LetterDecision, Is.EqualTo(M05LetterDecision.LeaveForNextBoat));
                Assert.That(outcome.LettersCarried, Is.Zero);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FailedFinalHarborNavigationKeepsLastBeatRetryableWithoutReplayingOrRewriting()
        {
            var root = new GameObject("M05 final scene handoff retry");
            var values = new Dictionary<string, int>();
            SeedVerifiedReadyManifest(values);
            Func<string, int> read = Reader(values);
            int sailedWrites = 0, fogWrites = 0, landingWrites = 0, letterWrites = 0;
            Action<string, int> write = (key, value) =>
            {
                if (key == M04DepartureFacts.SailedKey && value == 1) sailedWrites++;
                if (key == FogListeningFacts.CompletedKey && value == 1) fogWrites++;
                if (key == M05LandingFacts.ConfirmedKey && value == 1) landingWrites++;
                if (key == M05LettersFacts.DecisionKey) letterWrites++;
                values[key] = value;
            };
            M04ToM05SeaCrossingHost host = null;
            try
            {
                int attempts = 0;
                M05OuterHarborOutcome accepted = default;
                Assert.That(M04ToM05SeaCrossingHost.TryOpen(
                    root.transform, ClearFogLeg,
                    new M05LandingReport(3, 8, true, true), read, write,
                    outcome =>
                    {
                        attempts++;
                        if (attempts == 1)
                            throw new InvalidOperationException("Next scene temporarily unavailable");
                        accepted = outcome;
                    },
                    _ => Assert.Fail("Successful fog passage must not fail"),
                    out host), Is.True);

                host.CastOffView.CastOffButton.onClick.Invoke();
                host.FogHost.View.HoldButton.onClick.Invoke();
                host.FogHost.View.ContinueButton.onClick.Invoke();
                host.OuterHarborHost.LandingCheckView.ConfirmButton.onClick.Invoke();
                host.OuterHarborHost.ArrivalSession.SeranView.TakeButton.onClick.Invoke();
                host.OuterHarborHost.ArrivalSession.SeranView.ContinueButton.onClick.Invoke();
                var lastBeat = host.OuterHarborHost.ArrivalSession.HarborWalkView;
                lastBeat.AdvanceButton.onClick.Invoke();
                lastBeat.AdvanceButton.onClick.Invoke();
                Assert.That(lastBeat.Beat, Is.EqualTo(M05HarborBeat.MarketAccusation));

                Assert.Throws<InvalidOperationException>(() =>
                    lastBeat.AdvanceButton.onClick.Invoke());
                Assert.That(attempts, Is.EqualTo(1));
                Assert.That(host.Stage, Is.EqualTo(M04ToM05SeaCrossingStage.OuterHarbor));
                Assert.That(host.OuterHarborHost.Stage, Is.EqualTo(M05OuterHarborFlowStage.ArrivalSession));
                Assert.That(lastBeat.AdvanceButton.interactable, Is.True);
                Assert.That(lastBeat.Beat, Is.EqualTo(M05HarborBeat.MarketAccusation));
                Assert.That(root.transform.childCount, Is.EqualTo(1));
                Assert.That(sailedWrites, Is.EqualTo(1));
                Assert.That(fogWrites, Is.EqualTo(1));
                Assert.That(landingWrites, Is.EqualTo(1));
                Assert.That(letterWrites, Is.EqualTo(1));

                // Re-deliver the same saved arrival and letter choice; no
                // duplicate voyage, landing, or packet decision is permitted.
                lastBeat.AdvanceButton.onClick.Invoke();
                Assert.That(attempts, Is.EqualTo(2));
                Assert.That(host.Stage, Is.EqualTo(M04ToM05SeaCrossingStage.Completed));
                Assert.That(host.OuterHarborHost, Is.Null);
                Assert.That(accepted.Landing.ReadyRescueBerths, Is.EqualTo(8));
                Assert.That(accepted.LetterDecision, Is.EqualTo(M05LetterDecision.TakePacket));
                Assert.That(sailedWrites, Is.EqualTo(1));
                Assert.That(fogWrites, Is.EqualTo(1));
                Assert.That(landingWrites, Is.EqualTo(1));
                Assert.That(letterWrites, Is.EqualTo(1));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FailedFogHandoffKeepsCastOffArmedForRetryWithoutDoubleSailing()
        {
            var root = new GameObject("M04 to M05 fog handoff retry");
            var values = new Dictionary<string, int>();
            SeedVerifiedReadyManifest(values);
            Func<string, int> read = Reader(values);
            int sailedWrites = 0;
            Action<string, int> write = (key, value) =>
            {
                if (key == M04DepartureFacts.SailedKey && value == 1)
                    sailedWrites++;
                values[key] = value;
            };
            int fogFactoryCalls = 0;
            M04ToM05SeaCrossingHost host = null;
            try
            {
                Assert.That(M04ToM05SeaCrossingHost.TryOpen(
                    root.transform,
                    () => ++fogFactoryCalls == 1 ? null : ClearFogLeg(),
                    new M05LandingReport(3, 8, true, true),
                    read, write, _ => { }, _ => { }, out host), Is.True);

                Assert.Throws<InvalidOperationException>(() =>
                    host.CastOffView.CastOffButton.onClick.Invoke());
                Assert.That(sailedWrites, Is.EqualTo(1));
                Assert.That(host.Stage, Is.EqualTo(M04ToM05SeaCrossingStage.CastOff));
                Assert.That(host.CastOffView, Is.Not.Null);
                Assert.That(host.CastOffView.CastOffButton.interactable, Is.True);
                Assert.That(host.FogHost, Is.Null);
                Assert.That(root.transform.childCount, Is.EqualTo(1));

                host.CastOffView.CastOffButton.onClick.Invoke();
                Assert.That(sailedWrites, Is.EqualTo(1));
                Assert.That(host.Stage, Is.EqualTo(M04ToM05SeaCrossingStage.FogPassage));
                Assert.That(host.CastOffView, Is.Null);
                Assert.That(host.FogHost, Is.Not.Null);
                Assert.That(root.transform.childCount, Is.EqualTo(1));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FailedOuterHarborHandoffKeepsFogContinueArmedForRetryWithoutDoubleWrite()
        {
            var root = new GameObject("M04 to M05 harbor handoff retry");
            var values = new Dictionary<string, int>();
            SeedVerifiedReadyManifest(values);
            Func<string, int> read = Reader(values);
            int fogWrites = 0;
            Action<string, int> write = (key, value) =>
            {
                if (key == FogListeningFacts.CompletedKey && value == 1)
                    fogWrites++;
                values[key] = value;
            };
            M04ToM05SeaCrossingHost host = null;
            try
            {
                // Null observed landing forces M05OuterHarborFlowHost.TryOpen to return false
                // until a verified landing report exists in the save store.
                Assert.That(M04ToM05SeaCrossingHost.TryOpen(
                    root.transform, ClearFogLeg, observedLanding: null,
                    read, write, _ => { }, _ => { }, out host), Is.True);

                host.CastOffView.CastOffButton.onClick.Invoke();
                host.FogHost.View.HoldButton.onClick.Invoke();
                Assert.Throws<InvalidOperationException>(() =>
                    host.FogHost.View.ContinueButton.onClick.Invoke());
                Assert.That(fogWrites, Is.EqualTo(1));
                Assert.That(host.Stage, Is.EqualTo(M04ToM05SeaCrossingStage.FogPassage));
                Assert.That(host.FogHost, Is.Not.Null);
                Assert.That(host.FogHost.View.ContinueButton.interactable, Is.True);
                Assert.That(host.OuterHarborHost, Is.Null);
                Assert.That(root.transform.childCount, Is.EqualTo(1));

                Assert.That(M05LandingFacts.TryRecord(
                    new M05LandingReport(3, 8, true, true), read, write), Is.True);
                host.FogHost.View.ContinueButton.onClick.Invoke();
                Assert.That(fogWrites, Is.EqualTo(1));
                Assert.That(host.Stage, Is.EqualTo(M04ToM05SeaCrossingStage.OuterHarbor));
                Assert.That(host.FogHost, Is.Null);
                Assert.That(host.OuterHarborHost, Is.Not.Null);
                Assert.That(root.transform.childCount, Is.EqualTo(1));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FailedFogPassageStopsBeforeM05AndNeverRecordsArrival()
        {
            var root = new GameObject("M04 to M05 fog failure");
            var values = new Dictionary<string, int>();
            SeedVerifiedReadyManifest(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = Writer(values);
            M04ToM05SeaCrossingHost host = null;
            try
            {
                int completions = 0;
                int fogFailures = 0;
                FogListeningRecord failedRecord = default;

                Assert.That(M04ToM05SeaCrossingHost.TryOpen(
                    root.transform, FailingFogLeg, new M05LandingReport(3, 8, true, true),
                    read, write,
                    _ => completions++,
                    record => { fogFailures++; failedRecord = record; },
                    out host), Is.True);

                host.CastOffView.CastOffButton.onClick.Invoke();
                host.FogHost.View.HoldButton.onClick.Invoke();
                host.FogHost.View.ContinueButton.onClick.Invoke();

                Assert.That(completions, Is.Zero);
                Assert.That(fogFailures, Is.EqualTo(1));
                Assert.That(failedRecord.Succeeded, Is.False);
                Assert.That(host.Stage, Is.EqualTo(M04ToM05SeaCrossingStage.FogFailed));
                Assert.That(host.OuterHarborHost, Is.Null);
                Assert.That(values.ContainsKey(M05LandingFacts.ConfirmedKey), Is.False,
                    "A failed fog leg must never fabricate or confirm M05 arrival.");
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FailedFogSceneCallbackCanRetrySavedFailureWithoutReplayingOrArriving()
        {
            var root = new GameObject("M04 failed fog callback retry");
            var values = new Dictionary<string, int>();
            SeedVerifiedReadyManifest(values);
            Func<string, int> read = Reader(values);
            int savedFogCompletions = 0;
            Action<string, int> write = (key, value) =>
            {
                if (key == FogListeningFacts.CompletedKey && value == 1)
                    savedFogCompletions++;
                values[key] = value;
            };
            M04ToM05SeaCrossingHost host = null;
            try
            {
                int failures = 0;
                int arrivals = 0;
                Assert.That(M04ToM05SeaCrossingHost.TryOpen(
                    root.transform, FailingFogLeg,
                    new M05LandingReport(3, 8, true, true), read, write,
                    _ => arrivals++, record =>
                    {
                        failures++;
                        Assert.That(record.Succeeded, Is.False);
                        if (failures == 1)
                            throw new InvalidOperationException("Failure scene unavailable");
                    }, out host), Is.True);

                host.CastOffView.CastOffButton.onClick.Invoke();
                host.FogHost.View.HoldButton.onClick.Invoke();
                Assert.Throws<InvalidOperationException>(() =>
                    host.FogHost.View.ContinueButton.onClick.Invoke());
                Assert.That(values[FogListeningFacts.CompletedKey], Is.EqualTo(1));
                Assert.That(values[FogListeningFacts.SucceededKey], Is.Zero);
                Assert.That(savedFogCompletions, Is.EqualTo(1));
                Assert.That(host.Stage, Is.EqualTo(M04ToM05SeaCrossingStage.FogPassage));
                Assert.That(host.FogHost, Is.Not.Null);
                Assert.That(host.FogHost.View.ContinueButton.interactable, Is.True);
                Assert.That(root.transform.childCount, Is.EqualTo(1));
                Assert.That(arrivals, Is.Zero);
                Assert.That(values.ContainsKey(M05LandingFacts.ConfirmedKey), Is.False);

                host.FogHost.View.ContinueButton.onClick.Invoke();
                Assert.That(failures, Is.EqualTo(2));
                Assert.That(savedFogCompletions, Is.EqualTo(1));
                Assert.That(host.Stage, Is.EqualTo(M04ToM05SeaCrossingStage.FogFailed));
                Assert.That(host.FogHost, Is.Null);
                Assert.That(host.OuterHarborHost, Is.Null);
                Assert.That(arrivals, Is.Zero);
                Assert.That(values.ContainsKey(M05LandingFacts.ConfirmedKey), Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryOpenGatesUnverifiedOrMismatchedRouteAndResumesFromSavedMilestones()
        {
            var root = new GameObject("M04 to M05 resume gates");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = Writer(values);
            var landing = new M05LandingReport(3, 8, true, true);
            M04ToM05SeaCrossingHost fogResume = null;
            M04ToM05SeaCrossingHost harborResume = null;
            try
            {
                // No verified manifest -> refused.
                Assert.That(M04ToM05SeaCrossingHost.TryOpen(
                    root.transform, ClearFogLeg, landing, read, write,
                    _ => { }, _ => { }, out var unready), Is.False);
                Assert.That(unready, Is.Null);

                // Verified manifest mismatched with current M03 route -> refused.
                SeedVerifiedReadyManifest(values, M03VoyageChoice.QiQueMerchant);
                SeedChosenRoute(write, M03VoyageChoice.LuJunEscort);
                Assert.That(M04ToM05SeaCrossingHost.TryOpen(
                    root.transform, ClearFogLeg, landing, read, write,
                    _ => { }, _ => { }, out var mismatched), Is.False);
                Assert.That(mismatched, Is.Null);

                // Restore matching route + mark sailed -> resumes directly at FogPassage.
                SeedChosenRoute(write, M03VoyageChoice.QiQueMerchant);
                Assert.That(M04DepartureFacts.TryMarkSailed(read, write), Is.True);
                Assert.That(M04ToM05SeaCrossingHost.TryOpen(
                    root.transform, ClearFogLeg, landing, read, write,
                    _ => { }, _ => { }, out fogResume), Is.True);
                Assert.That(fogResume.Stage, Is.EqualTo(M04ToM05SeaCrossingStage.FogPassage));
                Assert.That(fogResume.CastOffView, Is.Null);
                Assert.That(fogResume.FogHost, Is.Not.Null);
                fogResume.Dispose();
                fogResume = null;

                // Record succeeded fog leg -> resumes directly at OuterHarbor.
                var fog = ClearFogLeg();
                fog.ApplyHelmAction(FogHelmAction.HoldCourse);
                Assert.That(FogListeningFacts.TryRecord(fog, write), Is.True);
                Assert.That(M04ToM05SeaCrossingHost.TryOpen(
                    root.transform, ClearFogLeg, landing, read, write,
                    _ => { }, _ => { }, out harborResume), Is.True);
                Assert.That(harborResume.Stage, Is.EqualTo(M04ToM05SeaCrossingStage.OuterHarbor));
                Assert.That(harborResume.OuterHarborHost, Is.Not.Null);
            }
            finally
            {
                fogResume?.Dispose();
                harborResume?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TornFogSaveBlocksBothCastOffAndSavedSailingResume()
        {
            var root = new GameObject("M04 to M05 torn fog gate");
            var values = new Dictionary<string, int>();
            SeedVerifiedReadyManifest(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = Writer(values);
            M04ToM05SeaCrossingHost resumed = null;
            try
            {
                values[FogListeningFacts.HullIntegrityKey] = 2;
                Assert.That(M04ToM05SeaCrossingHost.TryOpen(
                    root.transform, ClearFogLeg, new M05LandingReport(3, 8, true, true),
                    read, write, _ => { }, _ => { }, out var beforeCastOff), Is.False);
                Assert.That(beforeCastOff, Is.Null);
                Assert.That(read(M04DepartureFacts.SailedKey), Is.Zero);
                Assert.That(root.transform.childCount, Is.Zero);

                Assert.That(M04DepartureFacts.TryMarkSailed(read, write), Is.True);
                Assert.That(M04ToM05SeaCrossingHost.TryOpen(
                    root.transform, ClearFogLeg, new M05LandingReport(3, 8, true, true),
                    read, write, _ => { }, _ => { }, out var afterCastOff), Is.False);
                Assert.That(afterCastOff, Is.Null);
                Assert.That(values[FogListeningFacts.HullIntegrityKey], Is.EqualTo(2));
                Assert.That(root.transform.childCount, Is.Zero);

                Assert.That(FogListeningFacts.TryResetForNewLeg(write), Is.True);
                Assert.That(M04ToM05SeaCrossingHost.TryOpen(
                    root.transform, ClearFogLeg, new M05LandingReport(3, 8, true, true),
                    read, write, _ => { }, _ => { }, out resumed), Is.True);
                Assert.That(resumed.Stage, Is.EqualTo(M04ToM05SeaCrossingStage.FogPassage));
            }
            finally
            {
                resumed?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NullGuardsAspectPropagationAndTeardownDisposeCleanly()
        {
            var root = new GameObject("M04 to M05 teardown");
            var values = new Dictionary<string, int>();
            SeedVerifiedReadyManifest(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = Writer(values);
            var landing = new M05LandingReport(3, 6, true, true);
            M04ToM05SeaCrossingHost host = null;
            try
            {
                Assert.Throws<ArgumentNullException>(() =>
                    M04ToM05SeaCrossingHost.TryOpen(
                        null, ClearFogLeg, landing, read, write, _ => { }, _ => { }, out _));
                Assert.Throws<ArgumentNullException>(() =>
                    M04ToM05SeaCrossingHost.TryOpen(
                        root.transform, null, landing, read, write, _ => { }, _ => { }, out _));
                Assert.Throws<ArgumentNullException>(() =>
                    M04ToM05SeaCrossingHost.TryOpen(
                        root.transform, ClearFogLeg, landing, read, write, null, _ => { }, out _));
                Assert.Throws<ArgumentNullException>(() =>
                    M04ToM05SeaCrossingHost.TryOpen(
                        root.transform, ClearFogLeg, landing, read, write, _ => { }, null, out _));

                Assert.That(M04ToM05SeaCrossingHost.TryOpen(
                    root.transform, ClearFogLeg, landing, read, write,
                    _ => { }, _ => { }, out host), Is.True);
                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                    Assert.DoesNotThrow(() => host.LayoutForViewportAspect(aspect));

                UnityEngine.Object.DestroyImmediate(root);
                root = null;
                Assert.DoesNotThrow(() => host.LayoutForViewportAspect(16f / 9f));
                Assert.That(host.IsDisposed, Is.True);
            }
            finally
            {
                host?.Dispose();
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
            }
        }
        [Test]
        public void DestroyedFogActionControlReopensSameLegWithoutAdvancingOrPersisting()
        {
            var root = new GameObject("M04 to M05 fog action-control recovery");
            var values = new Dictionary<string, int>();
            SeedVerifiedReadyManifest(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = Writer(values);
            FogListeningSession leg = ClearFogLeg();
            M04ToM05SeaCrossingHost host = null;

            try
            {
                Assert.That(M04ToM05SeaCrossingHost.TryOpen(
                    root.transform, () => leg, new M05LandingReport(3, 10, true, true),
                    read, write, _ => { },
                    _ => Assert.Fail("A clear fog leg cannot fail."),
                    out host), Is.True);

                host.CastOffView.CastOffButton.onClick.Invoke();
                M04SailedFogLegHost fogHost = host.FogHost;
                FogListeningView originalView = fogHost.View;
                Assert.That(originalView, Is.Not.Null);

                UnityEngine.Object.DestroyImmediate(originalView.HoldButton.gameObject);
                Assert.That(originalView.IsDisposed, Is.True);
                Assert.That(fogHost.HasLiveView, Is.False);

                host.LayoutForViewportAspect(16f / 9f);
                FogListeningView reopenedView = fogHost.View;
                Assert.That(reopenedView, Is.Not.Null);
                Assert.That(reopenedView, Is.Not.SameAs(originalView));
                Assert.That(reopenedView.Session, Is.SameAs(leg));
                Assert.That(leg.IsComplete, Is.False);
                Assert.That(read(FogListeningFacts.CompletedKey), Is.Zero);

                reopenedView.HoldButton.onClick.Invoke();
                reopenedView.ContinueButton.onClick.Invoke();

                Assert.That(read(FogListeningFacts.CompletedKey), Is.EqualTo(1));
                Assert.That(host.Stage, Is.EqualTo(M04ToM05SeaCrossingStage.OuterHarbor));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

    }
}
