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
            if (!SetMuted(mixer, false))
                throw new InvalidOperationException("The SFX volume control is not exposed by the saved mixer.");
        }

        public static bool SetMuted(AudioMixer mixer, bool muted) =>
            mixer != null && mixer.SetFloat(VolumeParameter, muted ? -80f : 0f);

        public static bool TryGetVolume(AudioMixer mixer, out float decibels)
        {
            decibels = 0f;
            return mixer != null && mixer.GetFloat(VolumeParameter, out decibels);
        }
    }
}
