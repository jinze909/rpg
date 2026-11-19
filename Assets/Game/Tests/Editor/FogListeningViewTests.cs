using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class FogListeningViewTests
    {
        [Test]
        public void FocusListenButtonClarifiesLowClarityCueAndDisablesWhenExhausted()
        {
            var root = new GameObject("Fog view focus");
            FogListeningView view = null;
            string savedLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var session = new FogListeningSession(
                    new[] { new FogListeningCue(FogBearing.Ahead, FogCueKind.ReefSurf, 35) },
                    maxListenFocus: 1,
                    maxHullIntegrity: 2);

                view = new FogListeningView(root.transform, session, _ => { });
                Assert.That(view.FocusButton.interactable, Is.True);
                Assert.That(view.CueText.text, Does.Contain("Sound is faint in the fog"));

                view.FocusButton.onClick.Invoke();
                Assert.That(session.RemainingFocus, Is.Zero);
                Assert.That(view.FocusButton.interactable, Is.False);
                Assert.That(view.CueText.text, Does.Contain("Low reef surf"));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(savedLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FaintFogCueHidesPreciseBearingUntilFocusMakesItIdentifiable()
        {
            var root = new GameObject("Fog bearing uncertainty");
            FogListeningView view = null;
            string savedLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var session = new FogListeningSession(
                    new[] { new FogListeningCue(FogBearing.Starboard, FogCueKind.ReefSurf, 30) },
                    maxListenFocus: 1,
                    maxHullIntegrity: 2);
                view = new FogListeningView(root.transform, session, _ => { });

                Assert.That(view.CueText.text, Does.Contain("Bearing: indistinct"));
                Assert.That(view.CueText.text, Does.Not.Contain("Starboard"));
                Assert.That(view.CueText.text, Does.Not.Contain("Low reef surf"));

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.CueText.text, Does.Contain("方位：难辨"));
                Assert.That(view.CueText.text, Does.Not.Contain("右舷"));

                view.FocusButton.onClick.Invoke();
                Assert.That(session.EffectiveClarity(), Is.EqualTo(65));
                Assert.That(view.CueText.text, Does.Contain("方位：右舷"));
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                Assert.That(view.CueText.text, Does.Contain("Bearing: Starboard"));
                Assert.That(view.CueText.text, Does.Contain("Low reef surf"));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(savedLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ReefSurfDescriptionDoesNotContradictTheResolvedBearing()
        {
            var root = new GameObject("Fog reef bearing copy");
            FogListeningView view = null;
            string originalLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var session = new FogListeningSession(
                    new[]
                    {
                        new FogListeningCue(FogBearing.Port, FogCueKind.ReefSurf, 85),
                        new FogListeningCue(FogBearing.Starboard, FogCueKind.ReefSurf, 85),
                        new FogListeningCue(FogBearing.Ahead, FogCueKind.ReefSurf, 85)
                    }, maxListenFocus: 1, maxHullIntegrity: 3);
                view = new FogListeningView(root.transform, session, _ => { });
                foreach (string bearing in new[] { "Port", "Starboard", "Ahead" })
                {
                    Assert.That(view.CueText.text, Does.Contain("Bearing: " + bearing));
                    Assert.That(view.CueText.text, Does.Contain("Low reef surf"));
                    Assert.That(view.CueText.text, Does.Contain("check the bearing"));
                    Assert.That(view.CueText.text, Does.Not.Contain("reef surf ahead"),
                        "A reef on either side cannot be announced as straight ahead.");
                    if (bearing != "Ahead")
                        view.SlowButton.onClick.Invoke();
                }

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.CueText.text, Does.Contain("方位：正前"));
                Assert.That(view.CueText.text, Does.Contain("低沉礁浪拍击声"));
                Assert.That(session.AvoidedHazards, Is.EqualTo(2));
                Assert.That(session.HullIntegrity, Is.EqualTo(3));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(originalLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ContinueButtonStaysBlockedUntilSessionCompletesEvenOnDirectClick()
        {
            var root = new GameObject("Fog view continue guard");
            FogListeningView view = null;
            try
            {
                var session = new FogListeningSession(
                    new[] { new FogListeningCue(FogBearing.Ahead, FogCueKind.ClearWater, 80) },
                    maxListenFocus: 1,
                    maxHullIntegrity: 2);

                int finished = 0;
                view = new FogListeningView(root.transform, session, _ => finished++);
                Assert.That(view.ContinueButton.interactable, Is.False);

                view.ContinueButton.onClick.Invoke();
                Assert.That(finished, Is.Zero);
                Assert.That(session.IsComplete, Is.False);
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HelmButtonsResolveHazardSignalAndDeliverCompletedSessionOnce()
        {
            var root = new GameObject("Fog view helm");
            FogListeningView view = null;
            string savedLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var session = new FogListeningSession(
                    new[]
                    {
                        new FogListeningCue(FogBearing.Ahead, FogCueKind.ReefSurf, 70),
                        new FogListeningCue(FogBearing.Starboard, FogCueKind.SignalResponse, 80, requiresReceiverLamp: true)
                    },
                    maxListenFocus: 2,
                    maxHullIntegrity: 2);

                int delivered = 0;
                FogListeningSession result = null;
                view = new FogListeningView(root.transform, session, s => { delivered++; result = s; });

                view.PortButton.onClick.Invoke();
                Assert.That(session.AvoidedHazards, Is.EqualTo(1));
                Assert.That(view.CueText.text, Does.Contain("signal lamp"));

                view.SignalButton.onClick.Invoke();
                Assert.That(session.MatchedSignals, Is.EqualTo(1));
                Assert.That(session.Succeeded, Is.True);
                Assert.That(view.ContinueButton.interactable, Is.True);
                Assert.That(view.ContinueButton.GetComponentInChildren<UnityEngine.UI.Text>().text,
                    Is.EqualTo("Continue voyage"));
                Assert.That(view.HoldButton.interactable, Is.False);

                view.ContinueButton.onClick.Invoke();
                Assert.That(delivered, Is.EqualTo(1));
                Assert.That(result, Is.SameAs(session));
                view.ContinueButton.onClick.Invoke();
                Assert.That(delivered, Is.EqualTo(1));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(savedLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InterruptedSaveAllowsRetryOfExactCompletedLegWithoutReplayingHelm()
        {
            foreach (bool safePassage in new[] { true, false })
            {
                var root = new GameObject("Fog host retry " + safePassage);
                FogListeningView view = null;
                try
                {
                    var session = new FogListeningSession(
                        new[]
                        {
                            new FogListeningCue(FogBearing.Ahead, FogCueKind.ReefSurf, 70),
                            new FogListeningCue(FogBearing.Ahead, FogCueKind.ClearWater, 95)
                        }, maxListenFocus: 1, maxHullIntegrity: 1);

                    int attempts = 0;
                    int recorded = 0;
                    bool interrupt = true;
                    view = new FogListeningView(root.transform, session, completed =>
                    {
                        attempts++;
                        Assert.That(completed, Is.SameAs(session));
                        Assert.That(completed.IsComplete, Is.True);
                        Assert.That(completed.Succeeded, Is.EqualTo(safePassage));
                        Assert.That(view.ContinueButton.interactable, Is.False);
                        // Direct onClick bypasses the disabled button state.
                        view.ContinueButton.onClick.Invoke();
                        view.HoldButton.onClick.Invoke();
                        if (interrupt)
                            throw new InvalidOperationException("Interrupted fog outcome save");
                        recorded++;
                    });

                    if (safePassage)
                    {
                        view.PortButton.onClick.Invoke();
                        view.HoldButton.onClick.Invoke();
                    }
                    else
                        view.HoldButton.onClick.Invoke();

                    Assert.That(session.IsComplete, Is.True);
                    Assert.That(session.Succeeded, Is.EqualTo(safePassage));
                    int completedSteps = session.CurrentStep;
                    int hull = session.HullIntegrity;
                    Assert.Throws<InvalidOperationException>(() =>
                        view.ContinueButton.onClick.Invoke());
                    Assert.That(attempts, Is.EqualTo(1));
                    Assert.That(recorded, Is.Zero);
                    Assert.That(view.ContinueButton.interactable, Is.True);
                    Assert.That(session.CurrentStep, Is.EqualTo(completedSteps));
                    Assert.That(session.HullIntegrity, Is.EqualTo(hull));

                    interrupt = false;
                    view.ContinueButton.onClick.Invoke();
                    Assert.That(attempts, Is.EqualTo(2));
                    Assert.That(recorded, Is.EqualTo(1));
                    Assert.That(view.ContinueButton.interactable, Is.False);
                    view.ContinueButton.onClick.Invoke();
                    Assert.That(attempts, Is.EqualTo(2));
                    Assert.That(session.CurrentStep, Is.EqualTo(completedSteps));
                    Assert.That(session.HullIntegrity, Is.EqualTo(hull));
                }
                finally
                {
                    view?.Dispose();
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        [Test]
        public void ChangingLocaleUpdatesHeadingsAndCueReadoutWithoutMutatingSession()
        {
            var root = new GameObject("Fog view locale");
            FogListeningView view = null;
            string savedLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var session = new FogListeningSession(
                    new[] { new FogListeningCue(FogBearing.Port, FogCueKind.FogBell, 75) },
                    maxListenFocus: 2,
                    maxHullIntegrity: 2);

                view = new FogListeningView(root.transform, session, _ => { });
                Assert.That(view.CueText.text, Does.Contain("Distinct fog bell"));

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.StatusText.text, Does.Contain("听雾专注：2/2"));
                Assert.That(view.CueText.text, Does.Contain("雾钟节律清晰"));
                Assert.That(session.CurrentStep, Is.Zero);
                Assert.That(session.RemainingFocus, Is.EqualTo(2));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(savedLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HullFailureEndsSessionAndViewportAspectScalingStaysSafe()
        {
            var root = new GameObject("Fog view hull failure");
            FogListeningView view = null;
            string savedLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                var session = new FogListeningSession(
                    new[]
                    {
                        new FogListeningCue(FogBearing.Ahead, FogCueKind.ReefSurf, 70),
                        new FogListeningCue(FogBearing.Ahead, FogCueKind.ClearWater, 90)
                    },
                    maxListenFocus: 1,
                    maxHullIntegrity: 1);

                view = new FogListeningView(root.transform, session, _ => { });
                foreach (float ratio in new[] { 9f / 16f, 32f / 9f, 16f / 9f })
                    view.LayoutForViewportAspect(ratio);

                view.HoldButton.onClick.Invoke();
                Assert.That(session.IsComplete, Is.True);
                Assert.That(session.Succeeded, Is.False);
                Assert.That(view.CueText.text, Does.Contain("船体受损过重"));
                Assert.That(view.ContinueButton.interactable, Is.True);
                Assert.That(view.ContinueButton.GetComponentInChildren<UnityEngine.UI.Text>().text,
                    Is.EqualTo("确认本段雾航失败"));
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                Assert.That(view.ContinueButton.GetComponentInChildren<UnityEngine.UI.Text>().text,
                    Is.EqualTo("Acknowledge failed leg"));
                Assert.That(session.Succeeded, Is.False,
                    "Changing the acknowledgement text cannot turn damage into success.");
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(savedLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyedViewCannotReceiveStaleLocaleChangesOrFailOnDispose()
        {
            var root = new GameObject("Fog view destroyed");
            FogListeningView view = null;
            string savedLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var session = new FogListeningSession(
                    new[] { new FogListeningCue(FogBearing.Port, FogCueKind.FogBell, 75) },
                    maxListenFocus: 1,
                    maxHullIntegrity: 2);
                view = new FogListeningView(root.transform, session, _ => { });

                UnityEngine.Object.DestroyImmediate(root);
                root = null;

                Assert.DoesNotThrow(() =>
                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese));
                Assert.DoesNotThrow(() => view.LayoutForViewportAspect(9f / 16f));
                Assert.DoesNotThrow(() => view.Dispose());
                Assert.DoesNotThrow(() =>
                    SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(savedLanguage);
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyedViewRejectsStaleFocusHelmAndCompletionCallbacks()
        {
            var activeRoot = new GameObject("Fog view destroyed active");
            var completedRoot = new GameObject("Fog view destroyed completed");
            FogListeningView activeView = null;
            FogListeningView completedView = null;
            try
            {
                var activeSession = new FogListeningSession(
                    new[] { new FogListeningCue(FogBearing.Ahead, FogCueKind.ReefSurf, 35) },
                    maxListenFocus: 1,
                    maxHullIntegrity: 2);
                activeView = new FogListeningView(activeRoot.transform, activeSession, _ => { });
                UnityEngine.Object.DestroyImmediate(activeRoot);
                activeRoot = null;

                Assert.DoesNotThrow(() => activeView.FocusButton.onClick.Invoke());
                Assert.DoesNotThrow(() => activeView.HoldButton.onClick.Invoke());
                Assert.That(activeSession.RemainingFocus, Is.EqualTo(1));
                Assert.That(activeSession.CurrentStep, Is.Zero);
                Assert.That(activeSession.HullIntegrity, Is.EqualTo(2));

                int delivered = 0;
                var completedSession = new FogListeningSession(
                    new[] { new FogListeningCue(FogBearing.Ahead, FogCueKind.ClearWater, 80) },
                    maxListenFocus: 1,
                    maxHullIntegrity: 2);
                completedView = new FogListeningView(
                    completedRoot.transform, completedSession, _ => delivered++);
                completedView.HoldButton.onClick.Invoke();
                Assert.That(completedSession.IsComplete, Is.True);

                UnityEngine.Object.DestroyImmediate(completedRoot);
                completedRoot = null;

                Assert.DoesNotThrow(() => completedView.ContinueButton.onClick.Invoke());
                Assert.That(delivered, Is.Zero);
            }
            finally
            {
                activeView?.Dispose();
                completedView?.Dispose();
                if (activeRoot != null)
                    UnityEngine.Object.DestroyImmediate(activeRoot);
                if (completedRoot != null)
                    UnityEngine.Object.DestroyImmediate(completedRoot);
            }
        }
    }
}
