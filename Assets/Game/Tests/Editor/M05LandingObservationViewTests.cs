using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M05LandingObservationViewTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static Action<string, int> Writer(Dictionary<string, int> values)
        {
            return (key, value) => values[key] = value;
        }

        private static M04DepartureRecord SeedCompletedCrossing(
            Dictionary<string, int> values)
        {
            Func<string, int> read = Reader(values);
            Action<string, int> write = Writer(values);
            var encounter = new M03LuJunEncounter(M02BridgeEvidence.None, true);
            encounter.Begin();
            encounter.ObserveRollCall();
            encounter.Ask(M03LuJunOpening.NeedAShip);
            encounter.ChooseVoyage(M03VoyageChoice.QiQueMerchant);
            Assert.That(M03LuJunFacts.TryRecord(encounter, write), Is.True);

            var manifest = new M04DepartureManifest(
                M03VoyageChoice.QiQueMerchant, "Verified civilian hull",
                "A Yuan", 4, "Shallow tender", true,
                16, 8, "Named receiver", false, false);
            Assert.That(manifest.ConfirmDeparture(), Is.True);
            Assert.That(M04DepartureFacts.TryRecord(manifest, write), Is.True);
            Assert.That(M04DepartureFacts.TryMarkSailed(read, write), Is.True);

            var fog = new FogListeningSession(
                new[] { new FogListeningCue(
                    FogBearing.Ahead, FogCueKind.ClearWater, 80) },
                maxListenFocus: 1, maxHullIntegrity: 2);
            fog.ApplyHelmAction(FogHelmAction.HoldCourse);
            Assert.That(fog.IsComplete && fog.Succeeded, Is.True);
            Assert.That(FogListeningFacts.TryRecord(fog, write), Is.True);
            Assert.That(M04DepartureFacts.TryRead(read,
                out M04DepartureRecord? departure), Is.True);
            Assert.That(departure.HasValue, Is.True);
            return departure.Value;
        }

        [Test]
        public void StartsUnverifiedAndDoesNotCopyDeparturePlanIntoArrivalFacts()
        {
            var root = new GameObject("M05 actual arrival observation defaults");
            var values = new Dictionary<string, int>();
            M04DepartureRecord departure = SeedCompletedCrossing(values);
            M05LandingObservationView view = null;
            int callbacks = 0;
            try
            {
                view = new M05LandingObservationView(root.transform, departure,
                    Reader(values), _ => callbacks++);

                Assert.That(view.CrewArrived, Is.Zero);
                Assert.That(view.ReadyRescueBerths, Is.Zero);
                Assert.That(view.TenderArrived, Is.False);
                Assert.That(view.ReceiverMet, Is.False);
                Assert.That(view.CanConfirm, Is.False);
                Assert.That(view.ConfirmButton.interactable, Is.False);
                Assert.That(M05LandingFacts.TryRead(Reader(values),
                    out M05LandingReport? saved), Is.True);
                Assert.That(saved, Is.Null);

                view.ConfirmButton.onClick.Invoke();
                Assert.That(callbacks, Is.Zero);
                Assert.That(values.ContainsKey(M05LandingFacts.ConfirmedKey), Is.False);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ConfirmsOnlyTheCrewTenderReceiverAndBerthsThePlayerActuallySelected()
        {
            var root = new GameObject("M05 actual arrival observation confirm");
            var values = new Dictionary<string, int>();
            M04DepartureRecord departure = SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = Writer(values);
            M05LandingObservationView view = null;
            int callbacks = 0;
            try
            {
                var expected = new M05LandingReport(2, 3, true, true);
                view = new M05LandingObservationView(root.transform, departure,
                    read, report =>
                    {
                        callbacks++;
                        Assert.That(report.CrewArrived, Is.EqualTo(expected.CrewArrived));
                        Assert.That(report.ReadyRescueBerths, Is.EqualTo(expected.ReadyRescueBerths));
                        Assert.That(report.TenderArrived, Is.EqualTo(expected.TenderArrived));
                        Assert.That(report.ReceiverMet, Is.EqualTo(expected.ReceiverMet));
                        Assert.That(M05LandingFacts.TryRecord(report, read, write), Is.True);
                    });

                view.CrewPlusButton.onClick.Invoke();
                view.CrewPlusButton.onClick.Invoke();
                view.TenderButton.onClick.Invoke();
                view.ReceiverButton.onClick.Invoke();
                view.BerthPlusButton.onClick.Invoke();
                view.BerthPlusButton.onClick.Invoke();
                view.BerthPlusButton.onClick.Invoke();

                Assert.That(view.CanConfirm, Is.True);
                Assert.That(view.Report.CrewArrived, Is.EqualTo(2));
                Assert.That(view.Report.ReadyRescueBerths, Is.EqualTo(3));
                view.ConfirmButton.onClick.Invoke();
                view.ConfirmButton.onClick.Invoke();

                Assert.That(callbacks, Is.EqualTo(1));
                Assert.That(M05LandingFacts.TryRead(read,
                    out M05LandingReport? saved), Is.True);
                Assert.That(saved.HasValue, Is.True);
                Assert.That(saved.Value.CrewArrived, Is.EqualTo(2));
                Assert.That(saved.Value.ReadyRescueBerths, Is.EqualTo(3));
                Assert.That(saved.Value.TenderArrived, Is.True);
                Assert.That(saved.Value.ReceiverMet, Is.True);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MissingTenderOrReceiverLocksUsableBerthsAtZeroButStillAllowsAccurateLanding()
        {
            var root = new GameObject("M05 delayed tender arrival observation");
            var values = new Dictionary<string, int>();
            M04DepartureRecord departure = SeedCompletedCrossing(values);
            M05LandingObservationView view = null;
            M05LandingReport received = default;
            try
            {
                view = new M05LandingObservationView(root.transform, departure,
                    Reader(values), report => received = report);
                view.CrewPlusButton.onClick.Invoke();
                view.TenderButton.onClick.Invoke();
                view.ReceiverButton.onClick.Invoke();
                for (int i = 0; i < 4; i++)
                    view.BerthPlusButton.onClick.Invoke();
                Assert.That(view.ReadyRescueBerths, Is.EqualTo(4));

                view.TenderButton.onClick.Invoke();
                Assert.That(view.ReadyRescueBerths, Is.Zero);
                Assert.That(view.BerthPlusButton.interactable, Is.False);
                view.ConfirmButton.onClick.Invoke();

                Assert.That(received.CrewArrived, Is.EqualTo(1));
                Assert.That(received.ReadyRescueBerths, Is.Zero);
                Assert.That(received.TenderArrived, Is.False);
                Assert.That(received.ReceiverMet, Is.True);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InterruptedLandingHandoffCanRetryTheSameAlreadySavedObservation()
        {
            var root = new GameObject("M05 saved landing handoff retry");
            var values = new Dictionary<string, int>();
            M04DepartureRecord departure = SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = Writer(values);
            M05LandingObservationView view = null;
            int attempts = 0;
            try
            {
                view = new M05LandingObservationView(root.transform, departure,
                    read, report =>
                    {
                        attempts++;
                        if (attempts == 1)
                        {
                            Assert.That(M05LandingFacts.TryRecord(report, read, write), Is.True);
                            throw new InvalidOperationException("M05 arrival session unavailable");
                        }
                        Assert.That(M05LandingFacts.TryRead(read,
                            out M05LandingReport? saved), Is.True);
                        Assert.That(saved.HasValue, Is.True);
                        Assert.That(saved.Value.CrewArrived, Is.EqualTo(report.CrewArrived));
                    });

                view.CrewPlusButton.onClick.Invoke();
                view.TenderButton.onClick.Invoke();
                view.ReceiverButton.onClick.Invoke();
                view.BerthPlusButton.onClick.Invoke();

                Assert.Throws<InvalidOperationException>(() =>
                    view.ConfirmButton.onClick.Invoke());
                Assert.That(view.ConfirmButton.interactable, Is.True,
                    "The same confirmed observation must remain retryable after a failed scene handoff.");
                view.ConfirmButton.onClick.Invoke();
                Assert.That(attempts, Is.EqualTo(2));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CountsStayWithinVerifiedDepartureLimitsAndLocaleAndAspectCanChange()
        {
            var root = new GameObject("M05 actual arrival observation layout");
            var values = new Dictionary<string, int>();
            M04DepartureRecord departure = SeedCompletedCrossing(values);
            string previousLanguage = SoundwellUiLocalization.Language;
            M05LandingObservationView view = null;
            try
            {
                view = new M05LandingObservationView(root.transform, departure,
                    Reader(values), _ => { });
                for (int i = 0; i < 8; i++)
                    view.CrewPlusButton.onClick.Invoke();
                Assert.That(view.CrewArrived, Is.EqualTo(departure.AssignedCrew));
                view.TenderButton.onClick.Invoke();
                view.ReceiverButton.onClick.Invoke();
                for (int i = 0; i < 20; i++)
                    view.BerthPlusButton.onClick.Invoke();
                Assert.That(view.ReadyRescueBerths, Is.EqualTo(departure.AvailableBerths));
                Assert.That(view.CrewPlusButton.interactable, Is.False);
                Assert.That(view.BerthPlusButton.interactable, Is.False);
                Assert.That(view.CrewValue.text, Is.EqualTo("4 / 4"));
                Assert.That(view.BerthValue.text, Is.EqualTo("16 / 16"));

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.Instructions.text, Does.Contain("启程时的计划不是抵港事实"));
                Assert.That(view.Status.text, Does.Contain("当前可用救援舱位 16"));
                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(aspect));
                Assert.That(view.CanConfirm, Is.True);
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CorruptOrChangedVoyageCannotConfirmAndDisposalBlocksLateCallbacks()
        {
            var root = new GameObject("M05 stale arrival observation");
            var values = new Dictionary<string, int>();
            M04DepartureRecord departure = SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            M05LandingObservationView view = null;
            int callbacks = 0;
            try
            {
                view = new M05LandingObservationView(root.transform, departure,
                    read, _ => callbacks++);
                view.CrewPlusButton.onClick.Invoke();
                view.TenderButton.onClick.Invoke();
                view.ReceiverButton.onClick.Invoke();
                view.BerthPlusButton.onClick.Invoke();
                Assert.That(view.CanConfirm, Is.True);

                values[M03LuJunFacts.VoyageKey] = 999;
                view.Refresh();
                Assert.That(view.CanConfirm, Is.False);
                Assert.That(view.ConfirmButton.interactable, Is.False);
                view.ConfirmButton.onClick.Invoke();
                Assert.That(callbacks, Is.Zero);

                var staleButton = view.ConfirmButton;
                view.Dispose();
                staleButton.onClick.Invoke();
                Assert.That(callbacks, Is.Zero);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}