using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace Rpg.Tests.Editor
{
    // Story 003 presents its choices, danger cues, surveyor replies and
    // earned reward in both supported languages. A newly authored StringTable
    // key may never silently fall back to an English/raw key in the trial.
    public sealed class SoundwellLocalizedRescueCopyTests
    {
        [Test]
        public void WhitewaterPreludeTitleMatchesSelectedSceneLanguage()
        {
            string previous = Rpg.Soundwell.SoundwellUiLocalization.Language;
            try
            {
                Rpg.Soundwell.SoundwellUiLocalization.SetLanguage(
                    Rpg.Soundwell.SoundwellUiLocalization.English);
                Assert.That(Rpg.Soundwell.WhitewaterPrelude.SceneTitle,
                    Is.EqualTo("S00 · Whitewater Crossing"),
                    "English S00 must not show a permanently Chinese-only stage title.");
                Rpg.Soundwell.SoundwellUiLocalization.SetLanguage(
                    Rpg.Soundwell.SoundwellUiLocalization.Chinese);
                Assert.That(Rpg.Soundwell.WhitewaterPrelude.SceneTitle,
                    Is.EqualTo("S00 · 白水渡"),
                    "The same stage title must preserve its authored Chinese name.");
            }
            finally
            {
                Rpg.Soundwell.SoundwellUiLocalization.SetLanguage(previous);
            }
        }

        [Test]
        public void EveryPlayableSoundwellLabelAndRewardHasMatchingEnAndChineseCopy()
        {
            var english = Resources.Load<StringTable>("Soundwell/Localization/SoundwellUI-en");
            var chinese = Resources.Load<StringTable>("Soundwell/Localization/SoundwellUI-zh-Hans");
            Assert.That(english, Is.Not.Null);
            Assert.That(chinese, Is.Not.Null);
            Assert.That(english.SharedData, Is.Not.Null);
            Assert.That(chinese.SharedData, Is.SameAs(english.SharedData),
                "Both locales must use the one saved Soundwell key registry.");

            int verified = 0;
            foreach (var shared in english.SharedData.Entries)
            {
                string key = shared.Key;
                // The deliberately English-only diagnostic fallback is not a
                // game-facing action, rescue clue or localized player label.
                if (key == "diagnostic.fallback")
                    continue;

                var en = english.GetEntry(key)?.LocalizedValue;
                var zh = chinese.GetEntry(key)?.LocalizedValue;
                Assert.That(en, Is.Not.Null.And.Not.Empty, "Missing English copy: " + key);
                Assert.That(zh, Is.Not.Null.And.Not.Empty, "Missing Chinese copy: " + key);

                // Dynamic rescue stats and encounter counts use string.Format.
                // Dropping {0} in one language silently loses a player's earned
                // progress, even though the interface otherwise appears valid.
                var enArgs = Regex.Matches(en, @"\{[0-9]+\}")
                    .Cast<Match>().Select(m => m.Value)
                    .OrderBy(value => value, StringComparer.Ordinal).ToArray();
                var zhArgs = Regex.Matches(zh, @"\{[0-9]+\}")
                    .Cast<Match>().Select(m => m.Value)
                    .OrderBy(value => value, StringComparer.Ordinal).ToArray();
                Assert.That(zhArgs, Is.EqualTo(enArgs),
                    "Localized dynamic placeholders differ: " + key);
                verified++;
            }
            Assert.That(verified, Is.GreaterThan(100),
                "The full shipped Soundwell UI, not a tiny hand-picked subset, must be checked.");
        }
    }
}
