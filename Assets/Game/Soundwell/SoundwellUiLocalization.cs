using System;
using UnityEngine;
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
            var en = Resources.Load<StringTable>(Folder + English);
            if (en == null)
                throw new InvalidOperationException("Saved English Soundwell StringTable is missing.");
            var selected = language == Chinese
                ? Resources.Load<StringTable>(Folder + Chinese) : en;
            if (selected == null)
                throw new InvalidOperationException("Saved Chinese Soundwell StringTable is missing.");
            var translated = selected.GetEntry(key)?.LocalizedValue;
            return !string.IsNullOrEmpty(translated) ? translated :
                en.GetEntry(key)?.LocalizedValue ?? key;
        }

        public static void SetLanguage(string code)
        {
            if (code != English && code != Chinese)
                throw new ArgumentOutOfRangeException(nameof(code), "Only authored EN/zh UI is supported.");
            if (Resources.Load<StringTable>(Folder + English) == null ||
                Resources.Load<StringTable>(Folder + Chinese) == null)
                throw new InvalidOperationException("Cannot select a missing Soundwell StringTable.");
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
