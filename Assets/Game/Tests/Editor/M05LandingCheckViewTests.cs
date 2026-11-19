using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M05LandingCheckViewTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedCompletedCrossing(Dictionary<string, int> values)
        {
            Action<string, int> write = (key, value) => values[key] = value;
            Func<string, int> read = Reader(values);
            var manifest = new M04DepartureManifest(M03VoyageChoice.QiQueMerchant,
                "Verified civilian hull", "A Yuan", 4, "Shallow tender",
                true, 16, 8, "Named receiver", false, false);
            Assert.That(manifest.ConfirmDeparture(), Is.True);
            Assert.That(M04DepartureFacts.TryRecord(manifest, write), Is.True);
            Assert.That(M04DepartureFacts.TryMarkSailed(read, write), Is.True);

            var fog = new FogListeningSession(
                new[] { new FogListeningCue(FogBearing.Ahead,
                    FogCueKind.ClearWater, 80) },
                maxListenFocus: 1, maxHullIntegrity: 2);
            fog.ApplyHelmAction(FogHelmAction.HoldCourse);
            Assert.That(fog.IsComplete && fog.Succeeded, Is.True);
            Assert.That(FogListeningFacts.TryRecord(fog, write), Is.True);
        }

        [Test]
        public void ConfirmingVerifiedLandingRecordsFactsExactlyOnce()
        {
            var root = new GameObject("M05 landing check confirm");
            var values = new Dictionary<string, int>();
            SeedCompletedCrossing(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            string previousLanguage = SoundwellUiLocalization.Language;
            M05LandingCheckView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                int confirmations = 0;
                var report = new M05LandingReport(3, 10, true, true);
                view = new M05LandingCheckView(root.transform, report, confirmed =>
                {
                    confirmations++;
                    Assert.That(M05LandingFacts.TryRecord(confirmed, read, write), Is.True);
                });

                Assert.That(view.Summary.text, Does.Contain("Crew arrived: 3"));
                Assert.That(view.Summary.text, Does.Contain("Usable rescue berths now: 10"));
                Assert.That(M05LandingFacts.TryRead(read, out M05LandingReport? before), Is.True);
                Assert.That(before, Is.Null);

                view.ConfirmButton.onClick.Invoke();
                view.ConfirmButton.onClick.Invoke();
                Assert.That(confirmations, Is.EqualTo(1));
                Assert.That(view.ConfirmButton.gameObject.activeSelf, Is.False);
                Assert.That(M05LandingFacts.TryRead(read, out M05LandingReport? after), Is.True);
                Assert.That(after.HasValue, Is.True);
                Assert.That(after.Value.ReadyRescueBerths, Is.EqualTo(10));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DelayedTenderDisplaysZeroUsableBerthsAndRefusesInflatedReports()
        {
            var root = new GameObject("M05 landing check zero capacity");
            string previousLanguage = SoundwellUiLocalization.Language;
            M05LandingCheckView view = null;
            try
            {
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    new M05LandingCheckView(root.transform,
                        new M05LandingReport(3, 5, false, true), _ => { }));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    new M05LandingCheckView(root.transform,
                        new M05LandingReport(3, 5, true, false), _ => { }));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    new M05LandingCheckView(root.transform,
                        new M05LandingReport(0, 0, false, false), _ => { }));

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                view = new M05LandingCheckView(root.transform,
                    new M05LandingReport(2, 0, false, false), _ => { });
                Assert.That(view.Summary.text, Does.Contain("实到船员：2"));
                Assert.That(view.Summary.text, Does.Contain("当前可用接应舱位：0"));
                Assert.That(view.Summary.text, Does.Contain("尚未到达"));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BottomPanelStaysInSafeRegionAndSupportsLocaleAndAspectSwitches()
        {
            var root = new GameObject("M05 landing check layout");
            string previousLanguage = SoundwellUiLocalization.Language;
            M05LandingCheckView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                view = new M05LandingCheckView(root.transform,
                    new M05LandingReport(4, 8, true, true), _ => { });
                Assert.That(view.Panel.anchorMin.y, Is.EqualTo(0f));
                Assert.That(view.Panel.anchorMax.y, Is.EqualTo(0f));
                Assert.That(view.Panel.sizeDelta.y, Is.LessThan(220f));
                Assert.That(view.Panel.sizeDelta.x,
                    Is.LessThan(SoundwellUiTheme.ReferenceWidth));
                Assert.That(view.ConfirmButton.name, Is.EqualTo("M05 landing confirm"));

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.Summary.text, Does.Contain("当前可用接应舱位：8"));
                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(aspect));
                Assert.That(view.ConfirmButton.interactable, Is.True);
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InterruptedHostSaveRetainsConfirmButtonForExactlyOneRetry()
        {
            var root = new GameObject("M05 landing check retry");
            M05LandingCheckView view = null;
            try
            {
                int attempts = 0;
                int successes = 0;
                view = new M05LandingCheckView(root.transform,
                    new M05LandingReport(3, 9, true, true), _ =>
                    {
                        attempts++;
                        view.ConfirmButton.onClick.Invoke();
                        if (attempts == 1)
                            throw new InvalidOperationException("Interrupted Makinom write");
                        successes++;
                    });

                Assert.Throws<InvalidOperationException>(() =>
                    view.ConfirmButton.onClick.Invoke());
                Assert.That(view.ConfirmButton.interactable, Is.True);
                Assert.That(attempts, Is.EqualTo(1));

                view.ConfirmButton.onClick.Invoke();
                view.ConfirmButton.onClick.Invoke();
                Assert.That(attempts, Is.EqualTo(2));
                Assert.That(successes, Is.EqualTo(1));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NullArgumentsRejectedAndDisposalBlocksLateCallbacks()
        {
            var root = new GameObject("M05 landing check dispose");
            M05LandingCheckView view = null;
            try
            {
                var report = new M05LandingReport(2, 4, true, true);
                Assert.Throws<ArgumentNullException>(() =>
                    new M05LandingCheckView(null, report, _ => { }));
                Assert.Throws<ArgumentNullException>(() =>
                    new M05LandingCheckView(root.transform, report, null));

                int calls = 0;
                view = new M05LandingCheckView(root.transform, report, _ => calls++);
                var button = view.ConfirmButton;
                view.Dispose();
                button.onClick.Invoke();
                Assert.That(calls, Is.Zero);
                Assert.DoesNotThrow(() => view.Refresh());
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(1f));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
