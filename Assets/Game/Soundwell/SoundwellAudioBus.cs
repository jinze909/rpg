using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;

namespace Rpg.Soundwell
{
    /// <summary>
    /// A single routed bell source. Runtime only reads the Unity-authored mixer;
    /// ORK remains battle authority, and there is no parallel audio playback.
    /// </summary>
    public static class SoundwellAudioBus
    {
        public const string MixerResource = "Soundwell/Audio/SoundwellMixer";
        public const string VolumeParameter = "SoundwellSFXVolume";
        public const string MutePreferenceKey = "rpg.soundwell.audio.sfx-muted.v1";
        public static bool IsMuted => PlayerPrefs.GetInt(MutePreferenceKey, 0) != 0;

        // A Soundwell stage can create more than one bell source. Keep the
        // authored mixer and SFX group across scene-local source replacements.
        private static AudioMixer cachedMixer;
        private static AudioMixerGroup cachedSfxGroup;

        private static AudioMixer CurrentMixer()
        {
            if (cachedMixer == null)
                cachedMixer = Resources.Load<AudioMixer>(MixerResource);
            return cachedMixer;
        }

        public static void Route(AudioSource bell)
        {
            if (bell == null)
                throw new ArgumentNullException(nameof(bell));
            RouteWithMixer(bell, CurrentMixer());
        }

        // Every direct-source fallback must detach an earlier SFX output.
        // Otherwise an audible bell can remain routed through a stale muted mixer.
        // Missing/edited resources should not crash the scene while the
        // player is walking or rescuing someone. Keep a muted source muted
        // even if the authored SFX bus cannot be loaded or controlled.
        private static bool RouteWithMixer(AudioSource bell, AudioMixer mixer)
        {
            if (mixer == null)
            {
                bell.outputAudioMixerGroup = null;
                bell.mute = IsMuted;
                Debug.LogError("M6 Soundwell AudioMixer asset is missing; using direct AudioSource mute fallback.");
                return false;
            }

            AudioMixerGroup output = cachedSfxGroup;
            if (output == null || output.audioMixer != mixer)
            {
                var outputs = mixer.FindMatchingGroups("SFX")
                    .Where(group => group.name == "SFX").ToArray();
                if (outputs.Length != 1)
                {
                    bell.outputAudioMixerGroup = null;
                bell.mute = IsMuted;
                    Debug.LogError("M6 Soundwell mixer must have exactly one SFX output; using direct AudioSource mute fallback.");
                    return false;
                }
                cachedSfxGroup = output = outputs[0];
            }

            bell.outputAudioMixerGroup = output;
            if (!mixer.SetFloat(VolumeParameter, IsMuted ? -80f : 0f))
            {
                bell.outputAudioMixerGroup = null;
                bell.mute = IsMuted;
                Debug.LogError("M6 Soundwell SFX volume parameter is unavailable; using direct AudioSource mute fallback.");
                return false;
            }

            // The saved mixer is authoritative again; do not retain a stale
            // per-source mute from an earlier fallback.
            bell.mute = false;
            return true;
        }

        public static bool ToggleMuted()
        {
            if (SetMuted(CurrentMixer(), !IsMuted))
                return true;
            Debug.LogError("M6 Soundwell SFX toggle unavailable; keeping the player's saved preference.");
            return false;
        }

        public static bool SetMuted(AudioMixer mixer, bool muted)
        {
            if (mixer == null || !mixer.SetFloat(VolumeParameter, muted ? -80f : 0f))
                return false;
            PlayerPrefs.SetInt(MutePreferenceKey, muted ? 1 : 0);
            PlayerPrefs.Save();
            return true;
        }

        public static bool TryGetVolume(AudioMixer mixer, out float decibels)
        {
            decibels = 0f;
            return mixer != null && mixer.GetFloat(VolumeParameter, out decibels);
        }
    }
}
