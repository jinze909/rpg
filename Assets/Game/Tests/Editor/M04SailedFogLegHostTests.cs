using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M04SailedFogLegHostTests
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

        private static void SeedReady(Dictionary<string, int> values)
        {
            SeedChosenRoute(Writer(values), M03VoyageChoice.QiQueMerchant);
            var manifest = new M04DepartureManifest(
                M03VoyageChoice.QiQueMerchant, "Seen civilian hull",
                "Named pilot", 3, "Verified tender", true,
                6, 2, "Actual harbor receiver", false, false);
            Assert.That(manifest.ConfirmDeparture(), Is.True);
            Assert.That(M04DepartureFacts.TryRecord(manifest, Writer(values)), Is.True);
        }

        private static FogListeningSession ClearLeg()
        {
            return new FogListeningSession(new[]
            {
                new FogListeningCue(FogBearing.Ahead, FogCueKind.ClearWater, 80)
            }, 1, 2);
        }

        private static FogListeningSession HullFailureLeg()
        {
            return new FogListeningSession(new[]
            {
                new FogListeningCue(FogBearing.Ahead, FogCueKind.ReefSurf, 40)
            }, 1, 1);
        }

        [Test]
        public void FogCannotOpenFromChosenRouteOrReadinessWithoutActualCastOff()
        {
            var scene = new GameObject("M04 fog sailing prerequisites");
            var values = new Dictionary<string, int>();
            try
            {
                Assert.That(M04SailedFogLegHost.TryOpen(scene.transform, ClearLeg(),
                    Reader(values), Writer(values), _ => { }, out var missing), Is.False);
                Assert.That(missing, Is.Null);

                SeedReady(values);
                Assert.That(M04SailedFogLegHost.TryOpen(scene.transform, ClearLeg(),
                    Reader(values), Writer(values), _ => { }, out var unsailed), Is.False);
                Assert.That(unsailed, Is.Null);

                Assert.That(M04DepartureFacts.TryMarkSailed(
                    Reader(values), Writer(values)), Is.True);
                Assert.That(M04SailedFogLegHost.TryOpen(scene.transform, ClearLeg(),
                    Reader(values), Writer(values), _ => { }, out var actual), Is.True);
                Assert.That(actual.Departure.Route, Is.EqualTo(M03VoyageChoice.QiQueMerchant));
                Assert.That(actual.View, Is.Not.Null);
                Assert.That(values.ContainsKey(FogListeningFacts.CompletedKey), Is.False,
                    "Opening the UI does not mean the fog was successfully crossed.");
                actual.Dispose();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(scene);
            }
        }

        [Test]
        public void OldSailedHullCannotStartOrCompleteAReplacedRoute()
        {
            var scene = new GameObject("M04 stale sailed-route protection");
            var values = new Dictionary<string, int>();
            M04SailedFogLegHost host = null;
            try
            {
                SeedReady(values);
                Assert.That(M04DepartureFacts.TryMarkSailed(
                    Reader(values), Writer(values)), Is.True);
                SeedChosenRoute(Writer(values), M03VoyageChoice.LuJunEscort);

                Assert.That(M04SailedFogLegHost.TryOpen(scene.transform, ClearLeg(),
                    Reader(values), Writer(values), _ => { }, out var refused), Is.False,
                    "A previously sailed hull must not open a different chosen route.");
                Assert.That(refused, Is.Null);

                SeedChosenRoute(Writer(values), M03VoyageChoice.QiQueMerchant);
                Assert.That(M04SailedFogLegHost.TryOpen(scene.transform, ClearLeg(),
                    Reader(values), Writer(values), _ => { }, out host), Is.True);
                host.View.HoldButton.onClick.Invoke();
                Assert.That(host.Leg.IsComplete, Is.True);

                // The voyage can change while the UI is open. Its stale
                // completion must not write the next route's fog outcome.
                SeedChosenRoute(Writer(values), M03VoyageChoice.HanQiaoOfficial);
                Assert.Throws<InvalidOperationException>(() =>
                    host.View.ContinueButton.onClick.Invoke());
                Assert.That(values.ContainsKey(FogListeningFacts.CompletedKey), Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(scene);
            }
        }

        [Test]
        public void NavigatedFogLegPersistsOnlyAfterContinueAndNotAsArrival()
        {
            var scene = new GameObject("M04 real fog passage");
            var values = new Dictionary<string, int>();
            M04SailedFogLegHost host = null;
            try
            {
                SeedReady(values);
                Assert.That(M04DepartureFacts.TryMarkSailed(
                    Reader(values), Writer(values)), Is.True);
                var leg = ClearLeg();
                int completed = 0;
                FogListeningRecord final = default;
                Assert.That(M04SailedFogLegHost.TryOpen(scene.transform, leg,
                    Reader(values), Writer(values), record =>
                    {
                        completed++;
                        final = record;
                    }, out host), Is.True);
                Assert.That(host.View.ContinueButton.interactable, Is.False);
                host.View.HoldButton.onClick.Invoke();
                Assert.That(leg.IsComplete, Is.True);
                Assert.That(leg.Succeeded, Is.True);
                Assert.That(values.ContainsKey(FogListeningFacts.CompletedKey), Is.False);
                host.View.ContinueButton.onClick.Invoke();
                Assert.That(completed, Is.EqualTo(1));
                Assert.That(values[FogListeningFacts.CompletedKey], Is.EqualTo(1));
                Assert.That(values[FogListeningFacts.SucceededKey], Is.EqualTo(1));
                Assert.That(final.Succeeded, Is.True);
                Assert.That(final.HullIntegrity, Is.EqualTo(2));
                Assert.That(values.ContainsKey(M05LandingFacts.ConfirmedKey), Is.False,
                    "Successfully crossing fog never invents a landed vessel.");
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(scene);
            }
        }

        [Test]
        public void ReefDamageRecordsFailedLegRatherThanASuccessfulCrossing()
        {
            var scene = new GameObject("M04 damaged hull");
            var values = new Dictionary<string, int>();
            M04SailedFogLegHost host = null;
            try
            {
                SeedReady(values);
                M04DepartureFacts.TryMarkSailed(Reader(values), Writer(values));
                var leg = HullFailureLeg();
                FogListeningRecord? completed = null;
                Assert.That(M04SailedFogLegHost.TryOpen(scene.transform, leg,
                    Reader(values), Writer(values), r => completed = r, out host), Is.True);
                host.View.HoldButton.onClick.Invoke();
                Assert.That(leg.IsComplete, Is.True);
                Assert.That(leg.Succeeded, Is.False);
                Assert.That(host.View.ContinueButton.interactable, Is.True);
                host.View.ContinueButton.onClick.Invoke();
                Assert.That(completed.HasValue, Is.True);
                Assert.That(completed.Value.Succeeded, Is.False);
                Assert.That(values[FogListeningFacts.SucceededKey], Is.Zero);
                Assert.That(values.ContainsKey(M05LandingFacts.ConfirmedKey), Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(scene);
            }
        }

        [Test]
        public void SavedFogResultRetriesFailedHostNavigationWithoutSecondWrite()
        {
            var scene = new GameObject("M04 fog retry");
            var values = new Dictionary<string, int>();
            M04SailedFogLegHost host = null;
            try
            {
                SeedReady(values);
                M04DepartureFacts.TryMarkSailed(Reader(values), Writer(values));
                int committed = 0, attempts = 0;
                Assert.That(M04SailedFogLegHost.TryOpen(scene.transform, ClearLeg(),
                    Reader(values),
                    (key, value) =>
                    {
                        values[key] = value;
                        if (key == FogListeningFacts.CompletedKey && value == 1)
                            committed++;
                    },
                    _ =>
                    {
                        attempts++;
                        host.View.ContinueButton.onClick.Invoke();
                        if (attempts == 1)
                            throw new InvalidOperationException("Next scene unavailable");
                    }, out host), Is.True);
                host.View.HoldButton.onClick.Invoke();
                Assert.Throws<InvalidOperationException>(() =>
                    host.View.ContinueButton.onClick.Invoke());
                Assert.That(committed, Is.EqualTo(1));
                Assert.That(values[FogListeningFacts.CompletedKey], Is.EqualTo(1));
                Assert.That(host.View.ContinueButton.interactable, Is.True);
                host.View.ContinueButton.onClick.Invoke();
                Assert.That(attempts, Is.EqualTo(2));
                Assert.That(committed, Is.EqualTo(1));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(scene);
            }
        }

        [Test]
        public void NewVisitDuringFogInvalidatesItsOldCallbackBeforeAnySave()
        {
            var scene = new GameObject("M04 replay safety");
            var values = new Dictionary<string, int>();
            M04SailedFogLegHost host = null;
            try
            {
                SeedReady(values);
                M04DepartureFacts.TryMarkSailed(Reader(values), Writer(values));
                int called = 0;
                Assert.That(M04SailedFogLegHost.TryOpen(scene.transform, ClearLeg(),
                    Reader(values), Writer(values), _ => called++, out host), Is.True);
                host.View.HoldButton.onClick.Invoke();
                Assert.That(M04DepartureFacts.TryResetForNewVisit(Writer(values)), Is.True);
                Assert.Throws<InvalidOperationException>(() =>
                    host.View.ContinueButton.onClick.Invoke());
                Assert.That(called, Is.Zero);
                Assert.That(values.ContainsKey(FogListeningFacts.CompletedKey), Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(scene);
            }
        }

        [Test]
        public void CompletedOrMalformedSavedFogLegCannotBeReplaced()
        {
            var scene = new GameObject("M04 cannot override saved fog");
            var values = new Dictionary<string, int>();
            try
            {
                SeedReady(values);
                M04DepartureFacts.TryMarkSailed(Reader(values), Writer(values));
                values[FogListeningFacts.CompletedKey] = 2;
                Assert.That(M04SailedFogLegHost.TryOpen(scene.transform, ClearLeg(),
                    Reader(values), Writer(values), _ => { }, out var invalid), Is.False);
                Assert.That(invalid, Is.Null);

                values[FogListeningFacts.CompletedKey] = 1;
                values[FogListeningFacts.SucceededKey] = 1;
                values[FogListeningFacts.HullIntegrityKey] = 2;
                Assert.That(M04SailedFogLegHost.TryOpen(scene.transform, ClearLeg(),
                    Reader(values), Writer(values), _ => { }, out var prior), Is.False);
                Assert.That(prior, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(scene);
            }
        }

        [Test]
        public void MissingDependenciesAndDisposedViewDoNotCreateAnOutcome()
        {
            var scene = new GameObject("M04 fog safe disposal");
            var values = new Dictionary<string, int>();
            M04SailedFogLegHost host = null;
            try
            {
                SeedReady(values);
                M04DepartureFacts.TryMarkSailed(Reader(values), Writer(values));
                Assert.Throws<ArgumentNullException>(() =>
                    M04SailedFogLegHost.TryOpen(null, ClearLeg(),
                        Reader(values), Writer(values), _ => { }, out _));
                Assert.Throws<ArgumentNullException>(() =>
                    M04SailedFogLegHost.TryOpen(scene.transform, null,
                        Reader(values), Writer(values), _ => { }, out _));
                Assert.Throws<ArgumentNullException>(() =>
                    M04SailedFogLegHost.TryOpen(scene.transform, ClearLeg(),
                        Reader(values), Writer(values), null, out _));
                Assert.That(M04SailedFogLegHost.TryOpen(scene.transform, ClearLeg(),
                    Reader(values), Writer(values), _ => { }, out host), Is.True);
                Assert.DoesNotThrow(() => host.LayoutForViewportAspect(9f / 16f));
                var hold = host.View.HoldButton;
                host.Dispose();
                hold.onClick.Invoke();
                Assert.That(values.ContainsKey(FogListeningFacts.CompletedKey), Is.False);
                Assert.DoesNotThrow(() => host.LayoutForViewportAspect(16f / 9f));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(scene);
            }
        }
    }
}
