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

        public static void Route(AudioSource bell)
        {
            if (bell == null)
                throw new ArgumentNullException(nameof(bell));
            AudioMixer mixer = Resources.Load<AudioMixer>(MixerResource);
            if (mixer == null)
                throw new InvalidOperationException("M6 Soundwell AudioMixer asset is missing.");
            var outputs = mixer.FindMatchingGroups("SFX").Where(g => g.name == "SFX").ToArray();
            if (outputs.Length != 1)
                throw new InvalidOperationException("Expected exactly one SFX mixer output.");
            bell.outputAudioMixerGroup = outputs[0];
            // Connecting a new scene must not overwrite the player's mute choice.
            // Only the explicit SetMuted command is allowed to update preferences.
            if (!mixer.SetFloat(VolumeParameter, IsMuted ? -80f : 0f))
                throw new InvalidOperationException("The SFX volume control is not exposed by the saved mixer.");
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
