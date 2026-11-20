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
    }
}
