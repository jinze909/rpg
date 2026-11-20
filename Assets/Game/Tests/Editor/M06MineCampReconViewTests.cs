using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M06MineCampReconViewTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedVerifiedM05Arrival(Dictionary<string, int> values)
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
                new[] { new FogListeningCue(FogBearing.Ahead, FogCueKind.ClearWater, 80) },
                maxListenFocus: 1, maxHullIntegrity: 2);
            fog.ApplyHelmAction(FogHelmAction.HoldCourse);
            Assert.That(fog.IsComplete && fog.Succeeded, Is.True);
            Assert.That(FogListeningFacts.TryRecord(fog, write), Is.True);

            Assert.That(M05LandingFacts.TryRecord(
                new M05LandingReport(3, 10, true, true), read, write), Is.True);
        }

        [Test]
        public void RecordingReconLeadPersistsOnceAndHidesConfirmButton()
        {
            var root = new GameObject("M06 recon view confirm");
            var values = new Dictionary<string, int>();
            SeedVerifiedM05Arrival(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (key, value) => values[key] = value;
            string previousLanguage = SoundwellUiLocalization.Language;
            M06MineCampReconView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(M06MineCampReconSession.TryLoad(read, out var session), Is.True);
                int callbacks = 0;
                view = new M06MineCampReconView(root.transform, M06MineCampLead.MineTunnelAndPump, lead =>
                {
                    callbacks++;
                    Assert.That(session.TryInspectLead(lead, read, write), Is.True);
                });

                Assert.That(view.Heading.text, Does.Contain("矿道与主排水泵"));
                Assert.That(view.Summary.text, Does.Contain("五百人"));
                Assert.That(session.IsLeadObserved(M06MineCampLead.MineTunnelAndPump), Is.False);

                view.ConfirmButton.onClick.Invoke();
                view.ConfirmButton.onClick.Invoke();
                Assert.That(callbacks, Is.EqualTo(1));
                Assert.That(view.ConfirmButton.gameObject.activeSelf, Is.False);
                Assert.That(session.IsLeadObserved(M06MineCampLead.MineTunnelAndPump), Is.True);
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
            var root = new GameObject("M06 recon view layout");
            string previousLanguage = SoundwellUiLocalization.Language;
            M06MineCampReconView view = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                view = new M06MineCampReconView(root.transform, M06MineCampLead.GuardPatrol, _ => { });
                Assert.That(view.Panel.anchorMin.y, Is.EqualTo(0f));
                Assert.That(view.Panel.anchorMax.y, Is.EqualTo(0f));
                Assert.That(view.Panel.sizeDelta.y, Is.LessThan(220f));
                Assert.That(view.Panel.sizeDelta.x, Is.LessThan(SoundwellUiTheme.ReferenceWidth));
                Assert.That(view.Heading.text, Does.Contain("RED SALT MINE CAMP"));
                Assert.That(view.Summary.text, Does.Contain("20 minutes"));

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.Heading.text, Does.Contain("赤盐矿营侦察"));
                Assert.That(view.Summary.text, Does.Contain("二十分钟"));

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
            var root = new GameObject("M06 recon view retry");
            M06MineCampReconView view = null;
            try
            {
                int attempts = 0;
                int successes = 0;
                view = new M06MineCampReconView(root.transform, M06MineCampLead.EscortSchedule, _ =>
                {
                    attempts++;
                    view.ConfirmButton.onClick.Invoke();
                    if (attempts == 1)
                        throw new InvalidOperationException("Interrupted Makinom recon write");
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
        public void InvalidLeadOrNullArgumentsAreRejectedAndDisposalBlocksLateCallbacks()
        {
            var root = new GameObject("M06 recon view guards");
            M06MineCampReconView view = null;
            try
            {
                Assert.Throws<ArgumentNullException>(() =>
                    new M06MineCampReconView(null, M06MineCampLead.SupplyLedger, _ => { }));
                Assert.Throws<ArgumentNullException>(() =>
                    new M06MineCampReconView(root.transform, M06MineCampLead.SupplyLedger, null));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    new M06MineCampReconView(root.transform, (M06MineCampLead)99, _ => { }));

                int calls = 0;
                view = new M06MineCampReconView(root.transform, M06MineCampLead.SupplyLedger, _ => calls++);
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
