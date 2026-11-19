using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M04DockPreflightViewTests
    {
        [Test]
        public void CupHandoffShowsDawnRouteAndRealResourceChecksWithoutClaimingDeparture()
        {
            var root = new GameObject("M04 dock preflight");
            M04DockPreflightView view = null;
            string previous = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                int completions = 0;
                view = new M04DockPreflightView(root.transform,
                    M03VoyageChoice.QiQueMerchant, () => completions++);

                Assert.That(view.Stage, Is.EqualTo(M04DockPreflightStage.DawnSupply));
                Assert.That(view.Dialogue.text, Does.Contain("delivers supplies"));
                Assert.That(view.Dialogue.text, Does.Contain("not"));
                view.AdvanceButton.onClick.Invoke();
                Assert.That(view.Stage, Is.EqualTo(M04DockPreflightStage.VerifyVoyageRoute));
                Assert.That(view.Dialogue.text, Does.Contain("Qi Que"));
                Assert.That(view.Dialogue.text, Does.Contain("does not put Qi Que aboard"));
                view.AdvanceButton.onClick.Invoke();
                Assert.That(view.Stage, Is.EqualTo(M04DockPreflightStage.VerifyPhysicalReadiness));
                Assert.That(view.Dialogue.text, Does.Contain("reserve at least one"));
                Assert.That(view.Dialogue.text, Does.Contain("not a cast-off"));
                Assert.That(completions, Is.Zero);
                view.AdvanceButton.onClick.Invoke();
                view.AdvanceButton.onClick.Invoke();
                Assert.That(view.Stage, Is.EqualTo(M04DockPreflightStage.Finished));
                Assert.That(completions, Is.EqualTo(1));
                Assert.That(view.AdvanceButton.gameObject.activeSelf, Is.False);
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(previous);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EveryCommittedRouteHasItsOwnPhysicalVerificationRequirements()
        {
            foreach (M03VoyageChoice route in Enum.GetValues(typeof(M03VoyageChoice)))
            {
                string copy = M04DockPreflightCopy.Describe(
                    M04DockPreflightStage.VerifyVoyageRoute, route, false);
                Assert.That(copy.Length, Is.GreaterThan(80), route.ToString());
                Assert.That(copy, Does.Contain("ship").Or.Contain("hull"),
                    route.ToString());
            }
            string official = M04DockPreflightCopy.Describe(
                M04DockPreflightStage.VerifyVoyageRoute,
                M03VoyageChoice.HanQiaoOfficial, false);
            Assert.That(official, Does.Contain("effective registration"));
            string escort = M04DockPreflightCopy.Describe(
                M04DockPreflightStage.VerifyVoyageRoute,
                M03VoyageChoice.LuJunEscort, false);
            Assert.That(escort, Does.Contain("accepted flag"));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                M04DockPreflightCopy.Describe(
                    M04DockPreflightStage.VerifyVoyageRoute,
                    (M03VoyageChoice)99, false));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                M04DockPreflightCopy.Title((M04DockPreflightStage)99, false));
        }

        [Test]
        public void LocaleAndAspectReflowKeepPreviouslyChosenRouteAndStage()
        {
            var root = new GameObject("M04 dock locale test");
            M04DockPreflightView view = null;
            string previous = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                view = new M04DockPreflightView(root.transform,
                    M03VoyageChoice.MixedWithOfficial, () => { });
                view.AdvanceButton.onClick.Invoke();
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.Stage, Is.EqualTo(M04DockPreflightStage.VerifyVoyageRoute));
                Assert.That(view.Route, Is.EqualTo(M03VoyageChoice.MixedWithOfficial));
                Assert.That(view.Dialogue.text, Does.Contain("混合航路"));
                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                    Assert.DoesNotThrow(() => view.LayoutForViewportAspect(aspect));
                Assert.That(view.Stage, Is.EqualTo(M04DockPreflightStage.VerifyVoyageRoute));
                Assert.That(view.Panel.anchorMin.y, Is.EqualTo(0f));
                Assert.That(view.Panel.anchorMax.y, Is.EqualTo(0f));
                Assert.That(view.Panel.sizeDelta.y, Is.LessThan(245f),
                    "Only a small bottom panel should cover the dock map.");
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(previous);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FailedHostTransitionCanRetryWithoutReplayingTheEntireFarewell()
        {
            var root = new GameObject("M04 preflight retry");
            M04DockPreflightView view = null;
            try
            {
                int attempts = 0;
                view = new M04DockPreflightView(root.transform,
                    M03VoyageChoice.HanQiaoOfficial, () =>
                    {
                        attempts++;
                        // A reentrant direct invocation must not consume a
                        // second transition or a second scene completion.
                        view.AdvanceButton.onClick.Invoke();
                        if (attempts == 1)
                            throw new InvalidOperationException("Temporary host error");
                    });
                view.AdvanceButton.onClick.Invoke();
                view.AdvanceButton.onClick.Invoke();
                Assert.Throws<InvalidOperationException>(() =>
                    view.AdvanceButton.onClick.Invoke());
                Assert.That(view.Stage, Is.EqualTo(M04DockPreflightStage.VerifyPhysicalReadiness));
                Assert.That(view.AdvanceButton.interactable, Is.True);
                view.AdvanceButton.onClick.Invoke();
                Assert.That(attempts, Is.EqualTo(2));
                Assert.That(view.Stage, Is.EqualTo(M04DockPreflightStage.Finished));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InvalidConstructorAndDisposalCannotConsumeCallbacks()
        {
            var root = new GameObject("M04 teardown test");
            M04DockPreflightView view = null;
            try
            {
                Assert.Throws<ArgumentNullException>(() =>
                    new M04DockPreflightView(null, M03VoyageChoice.QiQueMerchant, () => { }));
                Assert.Throws<ArgumentNullException>(() =>
                    new M04DockPreflightView(root.transform,
                        M03VoyageChoice.QiQueMerchant, null));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    new M04DockPreflightView(root.transform,
                        (M03VoyageChoice)(-1), () => { }));
                int complete = 0;
                view = new M04DockPreflightView(root.transform,
                    M03VoyageChoice.LuJunEscort, () => complete++);
                var button = view.AdvanceButton;
                view.Dispose();
                button.onClick.Invoke();
                Assert.That(complete, Is.Zero);
                Assert.DoesNotThrow(() => view.Refresh());
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(0.7f));
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
