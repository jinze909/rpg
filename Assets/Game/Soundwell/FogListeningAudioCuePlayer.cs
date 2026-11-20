using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Lightweight authored-state audio feedback for the M04 listening game.
    /// This produces original, deterministic short PCM cues locally: it never
    /// plays external recordings or writes to Makinom/ORK. Every source is
    /// routed through the already saved Soundwell SFX mixer/mute preference.
    /// </summary>
    public sealed class FogListeningAudioCuePlayer : IDisposable
    {
        private const int SampleRate = 22050;
        private const int SampleCount = 17640; // 0.8 second one-shot
        private const float TwoPi = Mathf.PI * 2f;

        private readonly GameObject gameObject;
        private readonly AudioSource source;
        private readonly AudioLowPassFilter lowPass;
        private AudioClip currentClip;
        private int lastStep = -1;
        private int lastClarity = -1;
        private bool disposed;

        public AudioSource Source => source;
        public AudioLowPassFilter LowPass => lowPass;
        public bool IsDisposed => disposed;

        public FogListeningAudioCuePlayer(Transform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            gameObject = new GameObject("M04 fog auditory cue",
                typeof(AudioSource), typeof(AudioLowPassFilter));
            gameObject.transform.SetParent(parent, false);
            source = gameObject.GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = 0.26f;
            lowPass = gameObject.GetComponent<AudioLowPassFilter>();
            lowPass.lowpassResonanceQ = 1f;
            // AudioMixer.SetFloat is not available during Unity EditMode
            // construction even when the mixer asset is present; routing it
            // there logs an error on every view and masks real UI test results.
            // No cue can play until PlayMode, so route only when the audio
            // engine is live. Still respect the saved mute on dormant sources.
            if (Application.isPlaying)
                SoundwellAudioBus.Route(source);
            else
                source.mute = SoundwellAudioBus.IsMuted;
        }

        public void Sync(FogListeningSession session)
        {
            if (disposed || gameObject == null)
                return;
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            if (session.IsComplete)
            {
                ClearClip();
                lastStep = -1;
                lastClarity = -1;
                return;
            }

            int clarity = session.EffectiveClarity();
            if (session.CurrentStep == lastStep && clarity == lastClarity)
                return;

            FogListeningCue cue = session.CurrentCue;
            lastStep = session.CurrentStep;
            lastClarity = clarity;

            // A faint cue is intentionally directionless and muffled. Even
            // with headphones it cannot leak port/starboard before the same
            // identification threshold used by the UGUI text and sprite.
            bool recognized = clarity >= FogListeningView.ClearCueThreshold;
            source.panStereo = recognized
                ? cue.Bearing == FogBearing.Port ? -0.76f
                : cue.Bearing == FogBearing.Starboard ? 0.76f : 0f
                : 0f;
            lowPass.cutoffFrequency = recognized ? 6500f : 420f;

            ClearClip();
            currentClip = GenerateCue(cue.Kind);
            source.clip = currentClip;
            if (Application.isPlaying && source.enabled && source.gameObject.activeInHierarchy)
                source.Play();
        }

        private static AudioClip GenerateCue(FogCueKind kind)
        {
            var pcm = new float[SampleCount];
            uint noiseState = 0xA511E9B3u + (uint)kind * 1013904223u;
            for (int i = 0; i < pcm.Length; i++)
            {
                float t = (float)i / SampleRate;
                float end = (float)(SampleCount - i) / SampleRate;
                float fade = Mathf.Min(1f, t * 26f) * Mathf.Min(1f, end * 12f);
                noiseState = noiseState * 1664525u + 1013904223u;
                float noise = ((noiseState >> 8) / 8388608f) - 1f;
                float waveform;
                switch (kind)
                {
                    case FogCueKind.ReefSurf:
                        float swell = Mathf.Sin(Mathf.PI * t / 0.8f);
                        waveform = 0.22f * noise * swell * swell +
                            0.07f * Mathf.Sin(TwoPi * 73f * t);
                        break;
                    case FogCueKind.FogBell:
                        float decay = Mathf.Exp(-3.3f * t);
                        waveform = decay * (0.24f * Mathf.Sin(TwoPi * 330f * t) +
                            0.10f * Mathf.Sin(TwoPi * 495f * t) +
                            0.05f * Mathf.Sin(TwoPi * 770f * t));
                        break;
                    case FogCueKind.SignalResponse:
                        bool pulse = t < 0.17f || (t > 0.32f && t < 0.49f);
                        waveform = pulse
                            ? 0.19f * Mathf.Sin(TwoPi * 440f * t) : 0f;
                        break;
                    case FogCueKind.FalseEcho:
                        float flutter = 0.5f + 0.5f * Mathf.Sin(TwoPi * 7f * t);
                        waveform = 0.10f * flutter *
                            Mathf.Sin(TwoPi * (255f + 70f * t) * t) +
                            0.07f * noise;
                        break;
                    case FogCueKind.ClearWater:
                        waveform = 0.07f * noise +
                            0.04f * Mathf.Sin(TwoPi * 85f * t);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(kind));
                }
                pcm[i] = Mathf.Clamp(waveform * fade, -0.9f, 0.9f);
            }

            AudioClip clip = AudioClip.Create("M04 fog cue " + kind,
                SampleCount, 1, SampleRate, false);
            if (!clip.SetData(pcm, 0))
            {
                Release(clip);
                throw new InvalidOperationException("Unable to generate the current fog audio cue.");
            }
            return clip;
        }

        private void ClearClip()
        {
            if (source != null)
            {
                source.Stop();
                source.clip = null;
            }
            if (currentClip != null)
            {
                Release(currentClip);
                currentClip = null;
            }
        }

        private static void Release(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(obj);
            else
                UnityEngine.Object.DestroyImmediate(obj);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            ClearClip();
            Release(gameObject);
        }
    }
}
