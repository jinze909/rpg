using System;
using System.Linq;
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
        private const string Folder = "Soundwell/Localization/SoundwellUI-";
        private static string language = English;
        public static string Language => language;
        public static event Action Changed;
        private static Font cjkFont;
        private static bool checkedOsFonts;

        // Unity's bundled LegacyRuntime Latin font may silently drop Chinese
        // glyphs. Prefer a CJK-capable installed font rather than claiming the
        // localized string is visible merely because label.text changed.
        // Do not ship or modify an ORK/Makinom font or create a parallel UI.
        public static Font PresentationFont(Font englishFont)
        {
            if (language != Chinese)
                return englishFont;
            if (!checkedOsFonts)
            {
                checkedOsFonts = true;
                string[] installed = Font.GetOSInstalledFontNames();
                if (installed != null && installed.Length > 0)
                {
                    var ranked = installed
                        .OrderBy(name => name.IndexOf("CJK", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("Droid Sans Fallback", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("YaHei", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("PingFang", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("WenQuan", StringComparison.OrdinalIgnoreCase) >= 0
                            ? 0 : 1)
                        .ToArray();
                    Font candidate = Font.CreateDynamicFontFromOSFont(ranked, 20);
                    if (candidate != null &&
                        candidate.HasCharacter('中') && candidate.HasCharacter('声') &&
                        candidate.HasCharacter('铃'))
                        cjkFont = candidate;
                    else if (candidate != null)
                        UnityEngine.Object.Destroy(candidate);
                }
            }
            return cjkFont != null ? cjkFont : englishFont;
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

        public static void Toggle() => SetLanguage(language == English ? Chinese : English);
    }
}
