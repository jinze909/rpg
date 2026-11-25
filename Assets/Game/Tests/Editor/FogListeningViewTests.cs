using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class FogListeningViewTests
    {
        [Test]
        public void M04AuditoryCluesRespectFogClarityPanMixerAndSessionCompletion()
        {
            var root = new GameObject("M04 fog audible direction test");
            FogListeningView view = null;
            string oldLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var leg = new FogListeningSession(new[]
                {
                    new FogListeningCue(FogBearing.Port, FogCueKind.ReefSurf, 30),
                    new FogListeningCue(FogBearing.Starboard, FogCueKind.SignalResponse, 80),
                    new FogListeningCue(FogBearing.Ahead, FogCueKind.FalseEcho, 80)
                }, maxListenFocus: 1, maxHullIntegrity: 2);
                view = new FogListeningView(root.transform, leg, _ => { });
                AudioSource source = view.CueAudioSource;

                Assert.That(source, Is.Not.Null);
                Assert.That(source.playOnAwake, Is.False);
                Assert.That(source.loop, Is.False);
                Assert.That(source.spatialBlend, Is.Zero);
                Assert.That(source.clip, Is.Not.Null);
                Assert.That(source.panStereo, Is.Zero,
                    "Faint sound cannot leak the hidden port bearing via stereo pan.");
                Assert.That(view.CueAudioLowPass.cutoffFrequency, Is.LessThan(1000f));
                Assert.That(leg.RemainingFocus, Is.EqualTo(1));

                view.FocusButton.onClick.Invoke();
                Assert.That(leg.RemainingFocus, Is.Zero);
                Assert.That(source.panStereo, Is.LessThan(-0.5f),
                    "Only an identified port sound can be panned left.");
                Assert.That(view.CueAudioLowPass.cutoffFrequency, Is.GreaterThan(4000f));
                AudioClip identified = source.clip;
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(source.clip, Is.SameAs(identified),
                    "Switching language must not replay/regenerate the same sound.");

                view.SlowButton.onClick.Invoke();
                Assert.That(leg.CurrentStep, Is.EqualTo(1));
                Assert.That(source.panStereo, Is.GreaterThan(0.5f),
                    "The next identified starboard signal is a new right-side cue.");
                Assert.That(source.clip.name, Does.Contain("SignalResponse"));
                view.SignalButton.onClick.Invoke();
                Assert.That(leg.CurrentStep, Is.EqualTo(2));
                Assert.That(source.panStereo, Is.Zero);
                Assert.That(source.clip.name, Does.Contain("FalseEcho"));
                Assert.That(leg.MatchedSignals, Is.EqualTo(1));
                Assert.That(leg.HullIntegrity, Is.EqualTo(2));

                view.HoldButton.onClick.Invoke();
                Assert.That(leg.Succeeded, Is.True);
                Assert.That(source.clip, Is.Null,
                    "Completed voyages cannot keep playing the previous fog clue.");
                Assert.That(leg.HullIntegrity, Is.EqualTo(2),
                    "Procedural audio changes neither ORK nor Makinom voyage facts.");
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(oldLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PixelCueOnlyAppearsWhenListeningIdentifiesARealReefOrSignal()
        {
            var root = new GameObject("M04 clarity gated fog pixel icon");
            FogListeningView view = null;
            string savedLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                RpgPixelArtManifest manifest = RpgPixelArtManifest.LoadDefault();
                Assert.That(manifest, Is.Not.Null);
                Assert.That(manifest.FogReefHintSubmerged, Is.Not.Null);
                Assert.That(manifest.FogSignalLanternOn, Is.Not.Null);

                var session = new FogListeningSession(new[]
                {
                    new FogListeningCue(FogBearing.Starboard, FogCueKind.ReefSurf, 30),
                    new FogListeningCue(FogBearing.Port, FogCueKind.SignalResponse, 80,
                        requiresReceiverLamp: true),
                    new FogListeningCue(FogBearing.Ahead, FogCueKind.FalseEcho, 85)
                }, maxListenFocus: 1, maxHullIntegrity: 2);
                view = new FogListeningView(root.transform, session, _ => { });
                Assert.That(view.CueIcon, Is.Not.Null);
                Assert.That(view.CueIcon.gameObject.activeSelf, Is.False,
                    "Faint unidentified sounds cannot reveal a hidden reef.");
                Assert.That(view.CueText.text, Does.Not.Contain("Starboard"));
                Assert.That(view.CueIcon.raycastTarget, Is.False);

                view.FocusButton.onClick.Invoke();
                Assert.That(view.CueText.text, Does.Contain("Starboard"));
                Assert.That(view.CueIcon.gameObject.activeSelf, Is.True);
                Assert.That(view.CueIcon.sprite, Is.EqualTo(manifest.FogReefHintSubmerged));
                Assert.That(view.CueIcon.preserveAspect, Is.True);
                Assert.That(view.CueIcon.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(view.CueIcon.sprite.pixelsPerUnit,
                    Is.EqualTo(RpgWorldMapScale.PixelsPerUnit));
                Assert.That(session.HullIntegrity, Is.EqualTo(2),
                    "Presenting a pixel clue must not resolve or damage the voyage.");

                view.SlowButton.onClick.Invoke();
                Assert.That(session.CurrentStep, Is.EqualTo(1));
                Assert.That(view.CueIcon.sprite, Is.EqualTo(manifest.FogSignalLanternOn));
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.CueIcon.sprite, Is.EqualTo(manifest.FogSignalLanternOn));
                Assert.That(view.CueIcon.raycastTarget, Is.False);

                view.SignalButton.onClick.Invoke();
                Assert.That(session.MatchedSignals, Is.EqualTo(1));
                Assert.That(session.CurrentStep, Is.EqualTo(2));
                Assert.That(view.CueIcon.gameObject.activeSelf, Is.False,
                    "A false echo must not reuse the preceding real signal image.");
                view.HoldButton.onClick.Invoke();
                Assert.That(session.Succeeded, Is.True);
                Assert.That(view.CueIcon.gameObject.activeSelf, Is.False,
                    "Completing the fog leg must clear the last clue image.");
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(savedLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void GamepadFocusRecoveryCapturesRealBilingualFogCanvasStates()
        {
            var root = new GameObject("M04 fog gamepad visual evidence root");
            FogListeningView view = null;
            string previousLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                var session = new FogListeningSession(new[]
                {
                    new FogListeningCue(FogBearing.Ahead, FogCueKind.ReefSurf, 35)
                }, maxListenFocus: 1, maxHullIntegrity: 2);
                view = new FogListeningView(root.transform, session, _ => { });
                Canvas canvas = root.GetComponentInChildren<Canvas>(true);
                Assert.That(canvas, Is.Not.Null);
                Assert.That(canvas.gameObject.name, Is.EqualTo("Fog listening canvas"));
                Assert.That(view.PreferredFocusButton, Is.SameAs(view.FocusButton));
                SoundwellSmokeHost.CaptureStoryCanvas(
                    canvas, "m04-fog-gamepad-focus-start-zh");

                view.FocusButton.onClick.Invoke();
                Assert.That(session.RemainingFocus, Is.Zero);
                Assert.That(view.FocusButton.IsInteractable(), Is.False);
                Assert.That(view.PreferredFocusButton, Is.SameAs(view.SlowButton));
                Assert.That(view.SlowButton.IsInteractable(), Is.True);
                SoundwellSmokeHost.CaptureStoryCanvas(
                    canvas, "m04-fog-gamepad-focus-exhausted-zh");

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                view.SlowButton.onClick.Invoke();
                Assert.That(session.IsComplete, Is.True);
                Assert.That(view.PreferredFocusButton, Is.SameAs(view.ContinueButton));
                Assert.That(view.ContinueButton.IsInteractable(), Is.True);
                SoundwellSmokeHost.CaptureStoryCanvas(
                    canvas, "m04-fog-gamepad-continue-en");
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

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
                Assert.That(view.PreferredFocusButton, Is.SameAs(view.FocusButton));
                Assert.That(view.CueText.text, Does.Contain("Sound is faint in the fog"));

                view.FocusButton.onClick.Invoke();
                Assert.That(session.RemainingFocus, Is.Zero);
                Assert.That(view.FocusButton.interactable, Is.False);
                Assert.That(view.PreferredFocusButton, Is.SameAs(view.SlowButton),
                    "After focus is exhausted, gamepad focus must return to an enabled helm action.");
                Assert.That(view.PreferredFocusButton.IsInteractable(), Is.True);
                Assert.That(view.CueText.text, Does.Contain("Low reef surf"));

                view.SlowButton.onClick.Invoke();
                Assert.That(session.IsComplete, Is.True);
                Assert.That(view.PreferredFocusButton, Is.SameAs(view.ContinueButton),
                    "After the last helm action, focus must advance to the enabled result button.");
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
        public void WrongSignalCannotCompleteFogLegAndSlowSoundRecoversSameCue()
        {
            var root = new GameObject("Fog wrong-heading recovery");
            FogListeningView view = null;
            string priorLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var session = new FogListeningSession(
                    new[] { new FogListeningCue(FogBearing.Ahead,
                        FogCueKind.ClearWater, 80) },
                    maxListenFocus: 1, maxHullIntegrity: 2);
                int saved = 0;
                view = new FogListeningView(root.transform, session, _ => saved++);

                view.SignalButton.onClick.Invoke();
                Assert.That(session.CurrentStep, Is.Zero);
                Assert.That(session.IsComplete, Is.False);
                Assert.That(view.ContinueButton.interactable, Is.False);
                Assert.That(view.CueText.text,
                    Does.Contain("Lost bearing; listen again or slow and sound"));
                view.ContinueButton.onClick.Invoke();
                Assert.That(saved, Is.Zero);

                view.SlowButton.onClick.Invoke();
                Assert.That(session.CurrentStep, Is.EqualTo(1));
                Assert.That(session.Succeeded, Is.True);
                Assert.That(view.ContinueButton.interactable, Is.True);
                view.ContinueButton.onClick.Invoke();
                Assert.That(saved, Is.EqualTo(1));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(priorLanguage);
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
