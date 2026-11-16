using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace Rpg.Editor
{
    /// <summary>
    /// M6. Let the pinned Unity Editor author its own .mixer YAML; never
    /// hand-serialize native mixer data. Verify only reads saved content.
    /// The original ORK/Makinom assets and settings are unaffected.
    /// </summary>
    public static class SoundwellAudioMixerSetup
    {
        private const string Folder = "Assets/Game/Resources/Soundwell/Audio";
        private const string MixerPath = Folder + "/SoundwellMixer.mixer";
        private const string Parameter = "SoundwellSFXVolume";

        // A saved native .mixer is the source of truth. Never manufacture
        // its internal controller, parameter GUIDs, or exposed-parameter
        // records through UnityEditor private reflection.
        public static void Prepare()
        {
            if (AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath) == null)
                throw new InvalidOperationException(
                    "The authored SoundwellMixer.mixer asset is missing. Restore " +
                    MixerPath + " from version control; do not regenerate it through private APIs.");
            Validate();
        }

        public static void Validate()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null)
                throw new InvalidOperationException("The saved M6 native AudioMixer is missing.");
            var groups = mixer.FindMatchingGroups("");
            if (groups.Length != 4 ||
                groups.Count(g => g.name == "Master") != 1 ||
                groups.Count(g => g.name == "Music") != 1 ||
                groups.Count(g => g.name == "Ambient") != 1 ||
                groups.Count(g => g.name == "SFX") != 1)
                throw new InvalidOperationException("M6 must have only Master/Music/Ambient/SFX buses.");
            if (mixer.FindSnapshot("Snapshot") == null)
                throw new InvalidOperationException("The native mixer must have a saved starting snapshot.");

            // Runtime also reads this public exposed parameter. Validation
            // must not depend on the editor's undocumented mixer internals.
            if (!mixer.GetFloat(Parameter, out _))
                throw new InvalidOperationException("The SFX attenuation must expose the saved volume parameter.");

            Debug.Log("RPG_SOUNDWELL_AUDIO_MIXER_VALIDATED");
        }

    }
}
