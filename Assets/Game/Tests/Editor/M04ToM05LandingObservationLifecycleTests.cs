using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M04ToM05LandingObservationLifecycleTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static Action<string, int> Writer(Dictionary<string, int> values)
        {
            return (key, value) => values[key] = value;
        }

        private static void SeedVerifiedReadyManifest(Dictionary<string, int> values)
        {
            Action<string, int> write = Writer(values);
            var encounter = new M03LuJunEncounter(M02BridgeEvidence.None, true);
            encounter.Begin();
            encounter.ObserveRollCall();
            encounter.Ask(M03LuJunOpening.NeedAShip);
            encounter.ChooseVoyage(M03VoyageChoice.QiQueMerchant);
            Assert.That(M03LuJunFacts.TryRecord(encounter, write), Is.True);

            var manifest = new M04DepartureManifest(
                M03VoyageChoice.QiQueMerchant, "Verified civilian hull", "A Yuan", 4,
                "Shallow tender", true, 16, 8, "Named receiver", false, false);
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

        [Test]
        public void DestroyedObservationCanvasReportsDisposedAndBlocksStateMutation()
        {
            var root = new GameObject("M05LandingObsCanvasDestroyRoot");
            var values = new Dictionary<string, int>();
            SeedVerifiedReadyManifest(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = Writer(values);
            M04ToM05SeaCrossingHost host = null;

            try
            {
                Assert.That(M04ToM05SeaCrossingHost.TryOpenWithInteractiveLandingObservation(
                    root.transform, ClearFogLeg, read, write,
                    _ => { }, _ => { }, out host), Is.True);

                host.CastOffView.CastOffButton.onClick.Invoke();
                host.FogHost.View.HoldButton.onClick.Invoke();
                host.FogHost.View.ContinueButton.onClick.Invoke();
                Assert.That(host.Stage, Is.EqualTo(M04ToM05SeaCrossingStage.LandingObservation));

                var view = host.LandingObservationView;
                Assert.That(view, Is.Not.Null);
                Assert.That(view.IsDisposed, Is.False);
                var crewPlus = view.CrewPlusButton;
                var tenderButton = view.TenderButton;
                var receiverButton = view.ReceiverButton;
                var berthPlus = view.BerthPlusButton;
                var confirmButton = view.ConfirmButton;

                UnityEngine.Object.DestroyImmediate(view.AuthoredPanel.parent.gameObject);
                Assert.That(view.IsDisposed, Is.True);
                Assert.That(host.LandingObservationView, Is.Null);

                Assert.DoesNotThrow(() => crewPlus.onClick.Invoke());
                Assert.DoesNotThrow(() => tenderButton.onClick.Invoke());
                Assert.DoesNotThrow(() => receiverButton.onClick.Invoke());
                Assert.DoesNotThrow(() => berthPlus.onClick.Invoke());
                Assert.DoesNotThrow(() => confirmButton.onClick.Invoke());
                Assert.That(view.CrewArrived, Is.Zero);
                Assert.That(view.TenderArrived, Is.False);
                Assert.That(view.ReceiverMet, Is.False);
                Assert.That(view.ReadyRescueBerths, Is.Zero);
                Assert.That(read(M05LandingFacts.ConfirmedKey), Is.Zero);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ExternalObservationDisposalClearsHostReferenceAndIgnoresNonFiniteAspect()
        {
            var root = new GameObject("M05LandingObsExternalDisposeRoot");
            var values = new Dictionary<string, int>();
            SeedVerifiedReadyManifest(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = Writer(values);
            M04ToM05SeaCrossingHost host = null;

            try
            {
                Assert.That(M04ToM05SeaCrossingHost.TryOpenWithInteractiveLandingObservation(
                    root.transform, ClearFogLeg, read, write,
                    _ => { }, _ => { }, out host), Is.True);

                host.CastOffView.CastOffButton.onClick.Invoke();
                host.FogHost.View.HoldButton.onClick.Invoke();
                host.FogHost.View.ContinueButton.onClick.Invoke();

                Assert.DoesNotThrow(() => host.LayoutForViewportAspect(float.NaN));
                Assert.DoesNotThrow(() => host.LayoutForViewportAspect(float.PositiveInfinity));
                Assert.DoesNotThrow(() => host.LayoutForViewportAspect(-1f));

                var view = host.LandingObservationView;
                Assert.That(view, Is.Not.Null);
                view.Dispose();
                Assert.That(view.IsDisposed, Is.True);
                Assert.That(host.LandingObservationView, Is.Null);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
