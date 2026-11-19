using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization.Tables;

namespace Rpg.Soundwell
{
    // Owns only Soundwell UGUI copy. ORK/Makinom texts retain their authority.
    // Translations exist solely in the saved Unity Localization StringTables.
    public static class SoundwellUiLocalization
    {
        public const string English = "en";
        public const string Chinese = "zh-Hans";
        public const string LanguagePreferenceKey = "rpg.soundwell.ui.language.v1";
        private const string Folder = "Soundwell/Localization/SoundwellUI-";
        private static string language = English;
        public static string Language => language;
        public static event Action Changed;
        private const string CjkFontPath = "Soundwell/Fonts/SoundwellChinese";
        private static Font cjkFont;
        // Keep references to authored tables instead of loading on every HUD
        // refresh. Unity-null also allows reloading destroyed editor assets.
        private static StringTable englishTable;
        private static StringTable chineseTable;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            language = English;
            Changed = null;
            cjkFont = null;
            englishTable = null;
            chineseTable = null;
        }

        private static StringTable RequireSavedTable(string code)
        {
            bool chinese = code == Chinese;
            StringTable table = chinese ? chineseTable : englishTable;
            if (table == null)
            {
                table = Resources.Load<StringTable>(Folder + code);
                if (table == null)
                    throw new InvalidOperationException(chinese
                        ? "Saved Chinese Soundwell StringTable is missing."
                        : "Saved English Soundwell StringTable is missing.");
                if (chinese)
                    chineseTable = table;
                else
                    englishTable = table;
            }
            return table;
        }

        // Apply the last player-selected locale before the first scene Awake,
        // so the entry screen and trial HUD never flash the wrong language.
        // Only the two shipped StringTables are accepted; a stale/unknown
        // preference gracefully falls back to English.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RestoreSavedLanguage()
        {
            string saved = PlayerPrefs.GetString(LanguagePreferenceKey, English);
            language = saved == Chinese ? Chinese : English;
        }

        // M9: use one pinned, game-owned OFL typeface imported by Unity.
        // The previous OS-font fallback advertised HasCharacter(true), yet
        // rendered blank CJK glyphs on the actual CI GameView screenshots.
        // Keep the original Latin font for English and do not touch ORK UI.
        public static Font PresentationFont(Font englishFont)
        {
            if (language != Chinese)
                return englishFont;
            if (cjkFont == null)
                cjkFont = Resources.Load<Font>(CjkFontPath);
            if (cjkFont == null || !cjkFont.HasCharacter('中') ||
                !cjkFont.HasCharacter('声') || !cjkFont.HasCharacter('铃'))
                throw new InvalidOperationException(
                    "The pinned Chinese font is missing or lacks required glyphs: " + CjkFontPath);
            return cjkFont;
        }

        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Localization key cannot be empty.", nameof(key));
            var en = RequireSavedTable(English);
            var selected = language == Chinese ? RequireSavedTable(Chinese) : en;
            var translated = selected.GetEntry(key)?.LocalizedValue;
            string copy = !string.IsNullOrEmpty(translated) ? translated :
                en.GetEntry(key)?.LocalizedValue ?? key;
            // The trial's Retry input is rebindable. Preserve the authored
            // localized instruction but replace its default shortcut with
            // the effective Gameplay/Retry bindings actually listened for.
            return key == "hint.reset_here" ? WithRetryControls(copy) : copy;
        }

        private static string WithRetryControls(string copy)
        {
            if (string.IsNullOrEmpty(copy) || copy[0] != '[')
                return copy;
            int end = copy.IndexOf(']');
            if (end < 0)
                return copy;

            var asset = Resources.Load<InputActionAsset>("Input/SoundwellControls");
            var action = asset?.FindActionMap("Gameplay", false)?.FindAction("Retry", false);
            if (action == null)
                return copy;
            string keyboard = string.Empty;
            string gamepad = string.Empty;
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (binding.isComposite || binding.isPartOfComposite ||
                    string.IsNullOrEmpty(binding.effectivePath))
                    continue;
                if (string.IsNullOrEmpty(keyboard) &&
                    binding.effectivePath.StartsWith("<Keyboard>/",
                        StringComparison.OrdinalIgnoreCase))
                    keyboard = action.GetBindingDisplayString(i);
                if (string.IsNullOrEmpty(gamepad) &&
                    binding.effectivePath.StartsWith("<Gamepad>/",
                        StringComparison.OrdinalIgnoreCase))
                    gamepad = action.GetBindingDisplayString(i);
            }
            string padLabel = string.IsNullOrEmpty(gamepad) ? string.Empty :
                (language == Chinese ? "手柄 " : "Pad ") + gamepad;
            string label = !string.IsNullOrEmpty(keyboard) && !string.IsNullOrEmpty(padLabel)
                ? keyboard + (language == Chinese ? "／" : " / ") + padLabel
                : !string.IsNullOrEmpty(keyboard) ? keyboard
                : !string.IsNullOrEmpty(padLabel) ? padLabel
                : language == Chinese ? "重试" : "Retry";
            return "[" + label + "]" + copy.Substring(end + 1);
        }

        public static void SetLanguage(string code)
        {
            if (code != English && code != Chinese)
                throw new ArgumentOutOfRangeException(nameof(code), "Only authored EN/zh UI is supported.");
            RequireSavedTable(English);
            RequireSavedTable(Chinese);
            if (language == code)
                return;
            language = code;
            Changed?.Invoke();
        }

        // Persist only an intentional player UI toggle, not programmatic
        // language changes used for diagnostics or isolated Unity tests.
        public static void Toggle()
        {
            SetLanguage(language == English ? Chinese : English);
            PlayerPrefs.SetString(LanguagePreferenceKey, language);
            PlayerPrefs.Save();
        }
    }
}
