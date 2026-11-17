using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization.Tables;

namespace Rpg.Tests.Editor
{
    // Story 003 presents its choices, danger cues, surveyor replies and
    // earned reward in both supported languages. A newly authored StringTable
    // key may never silently fall back to an English/raw key in the trial.
    public sealed class SoundwellLocalizedRescueCopyTests
    {
        [Test]
        public void S00BridgeheadPlayerCopyNeverShowsPrototypeOrEngineNames()
        {
            // A v1.2 character must not suddenly speak about ORK or an S00
            // prototype in the battle title, waiting cue, retry or outcome.
            // Both languages use these actual runtime constants; framework
            // faults remain available in logs but not player-facing dialogue.
            var fields = typeof(WhitewaterDefenseEncounter)
                .GetFields(System.Reflection.BindingFlags.NonPublic |
                           System.Reflection.BindingFlags.Static)
                .Where(field => field.Name.StartsWith("PlayerCopy",
                    StringComparison.Ordinal)).ToArray();
            Assert.That(fields.Length, Is.EqualTo(16),
                "Test all eight authored English/Chinese readout and title pairs.");
            foreach (var field in fields)
            {
                string value = field.GetRawConstantValue() as string;
                Assert.That(value, Is.Not.Null.And.Not.Empty, field.Name);
                Assert.That(Regex.IsMatch(value,
                    @"\b(?:ORK|Makinom|S00)\b|原型|调试", RegexOptions.IgnoreCase),
                    Is.False, field.Name + " exposes engine/prototype copy.");
            }
        }

        [Test]
        public void AdaptiveLocalizedTitleSamplingClipsToActualCaptureRect()
        {
            var pixels = new Color32[120 * 80];
            var bright = new Color32(235, 235, 235, 255);
            pixels[62 * 120 + 20] = bright;
            pixels[62 * 120 + 28] = bright;
            pixels[62 * 120 + 60] = bright; // outside title
            var title = new RectInt(10, 50, 30, 25);
            int count = SoundwellSmokeHost.CountLocalizedGlyphPixels(
                pixels, 120, 80, title);
            Assert.That(count, Is.EqualTo(2));
            Assert.That(SoundwellSmokeHost.CountLocalizedGlyphPixels(
                pixels, 120, 80, new RectInt(130, 100, 15, 20)), Is.Zero);
            Assert.That(SoundwellSmokeHost.CountLocalizedGlyphPixels(
                pixels, 120, 80, new RectInt(0, 0, 0, 20)), Is.Zero);
        }

        [Test]
        public void S00DialogueChoicesAndNavigationRemainVisibleAtExtremeAspects()
        {
            var layout = typeof(WhitewaterPrelude).GetMethod("StageLayoutSlots",
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic);
            Assert.That(layout, Is.Not.Null);
            foreach (float aspect in new[] { 9f / 16f, 0.95f, 32f / 9f, 5f })
            {
                var slots = (Vector2[])layout.Invoke(null, new object[] { aspect });
                Assert.That(slots.Length, Is.EqualTo(9));
                float halfWidth = 0.5f * Mathf.Sqrt(960f * 540f * aspect);
                float halfHeight = 0.5f * Mathf.Sqrt(960f * 540f / aspect);
                for (int choice = 4; choice <= 6; choice++)
                {
                    Assert.That(Mathf.Abs(slots[choice].x) + 116f,
                        Is.LessThanOrEqualTo(halfWidth - 6f),
                        "The complete 232px choice must remain clickable.");
                    Assert.That(Mathf.Abs(slots[choice].y) + 25f,
                        Is.LessThanOrEqualTo(halfHeight - 5f),
                        "The complete 50px choice must stay inside the canvas.");
                }
                foreach (int button in new[] { 7, 8 })
                {
                    Assert.That(Mathf.Abs(slots[button].x) + 114f,
                        Is.LessThanOrEqualTo(halfWidth - 6f));
                    Assert.That(Mathf.Abs(slots[button].y) + 14.5f,
                        Is.LessThanOrEqualTo(halfHeight - 6f),
                        "Continue and return are needed to leave the prelude.");
                }
                // Portrait stacks decisions; ultrawide keeps the intended
                // left-to-right choice order while compressing vertical gaps.
                if (aspect <= 0.95f)
                    Assert.That(slots[4].y, Is.GreaterThan(slots[5].y));
                else
                    Assert.That(slots[4].x, Is.LessThan(slots[5].x));
            }
            Vector2[] regular = (Vector2[])layout.Invoke(null,
                new object[] { 16f / 9f });
            Assert.That(regular[4], Is.EqualTo(new Vector2(-250f, -192f)));
            Assert.That(regular[8], Is.EqualTo(new Vector2(-145f, -245f)));
        }

        [Test]
        public void WhitewaterPreludeTitleMatchesSelectedSceneLanguage()
        {
            string previous = Rpg.Soundwell.SoundwellUiLocalization.Language;
            try
            {
                Rpg.Soundwell.SoundwellUiLocalization.SetLanguage(
                    Rpg.Soundwell.SoundwellUiLocalization.English);
                Assert.That(Rpg.Soundwell.WhitewaterPrelude.SceneTitle,
                    Is.EqualTo("Whitewater Crossing"),
                    "The v1.2 English scene title must not expose the internal S00 identifier.");
                Rpg.Soundwell.SoundwellUiLocalization.SetLanguage(
                    Rpg.Soundwell.SoundwellUiLocalization.Chinese);
                Assert.That(Rpg.Soundwell.WhitewaterPrelude.SceneTitle,
                    Is.EqualTo("白水渡"),
                    "The v1.2 Chinese title must preserve the authored name without S00.");
            }
            finally
            {
                Rpg.Soundwell.SoundwellUiLocalization.SetLanguage(previous);
            }
        }

        [Test]
        public void WhitewaterReplayLabelUsesOnlyEarnedProgressAndBothSavedLanguages()
        {
            Assert.That(Rpg.Soundwell.SoundwellEntrance.PreludeProgressKey(false, false, false),
                Is.EqualTo("entry.whitewater_prologue"));
            Assert.That(Rpg.Soundwell.SoundwellEntrance.PreludeProgressKey(true, false, false),
                Is.EqualTo("entry.whitewater_replay_evacuee"));
            Assert.That(Rpg.Soundwell.SoundwellEntrance.PreludeProgressKey(true, true, false),
                Is.EqualTo("entry.whitewater_replay_bell"));
            Assert.That(Rpg.Soundwell.SoundwellEntrance.PreludeProgressKey(true, true, true),
                Is.EqualTo("entry.whitewater_replay_cable"),
                "Bracing the cable must not claim ORK defence or a completed third bell.");
            Assert.That(Rpg.Soundwell.SoundwellEntrance.PreludeProgressKey(true, true, true, true),
                Is.EqualTo("entry.whitewater_replay_third_bell"),
                "Only the earned third-bell Makinom flag can advance the replay label.");
            Assert.That(Rpg.Soundwell.SoundwellEntrance.PreludeProgressKey(true, true, true, false),
                Is.EqualTo("entry.whitewater_replay_cable"),
                "A replay which reaches the clamp must not invent the third bell.");
            Assert.That(Rpg.Soundwell.SoundwellEntrance.PreludeProgressKey(false, false, false, true),
                Is.EqualTo("entry.whitewater_replay_third_bell"),
                "The real third-bell flag is authoritative even if older markers were cleared.");

            var en = Resources.Load<StringTable>("Soundwell/Localization/SoundwellUI-en");
            var zh = Resources.Load<StringTable>("Soundwell/Localization/SoundwellUI-zh-Hans");
            Assert.That(en, Is.Not.Null);
            Assert.That(zh, Is.Not.Null);
            foreach (string key in new[]
            {
                "entry.whitewater_replay_evacuee",
                "entry.whitewater_replay_bell",
                "entry.whitewater_replay_cable",
                "entry.whitewater_replay_third_bell"
            })
            {
                Assert.That(en.GetEntry(key)?.LocalizedValue, Does.Contain("Replay"),
                    "A repeated S00 visit must not promise loading an unavailable checkpoint.");
                Assert.That(zh.GetEntry(key)?.LocalizedValue, Does.Contain("重玩"),
                    "The same replay warning must remain readable after switching language.");
            }
        }

        // A formation choice changes an actual ORK battle rule. Players must
        // see the stepping tradeoff before committing to the encounter in
        // either shipped language, rather than only a mode name.
        [Test]
        public void FormationToggleExplainsSteppingInBothLanguages()
        {
            var en = Resources.Load<StringTable>("Soundwell/Localization/SoundwellUI-en");
            var zh = Resources.Load<StringTable>("Soundwell/Localization/SoundwellUI-zh-Hans");
            Assert.That(en, Is.Not.Null);
            Assert.That(zh, Is.Not.Null);
            Assert.That(en.GetEntry("entry.formation_fixed")?.LocalizedValue,
                Does.Contain("no steps"));
            Assert.That(en.GetEntry("entry.formation_mobile")?.LocalizedValue,
                Does.Contain("step + act"));
            Assert.That(zh.GetEntry("entry.formation_fixed")?.LocalizedValue,
                Does.Contain("不走位"));
            Assert.That(zh.GetEntry("entry.formation_mobile")?.LocalizedValue,
                Does.Contain("走位＋行动"));
        }

        // The strict P0 collector must require this independent editor test
        // for both saved languages and both rebindable Retry controls.
        [Test]
        public void RetryHintShowsEffectiveControlsAfterSavedBindingRemap()
        {
            var action = Resources.Load<InputActionAsset>("Input/SoundwellControls")
                .FindActionMap("Gameplay", true).FindAction("Retry", true);
            int keyboardIndex = Enumerable.Range(0, action.bindings.Count)
                .Single(i => action.bindings[i].path == "<Keyboard>/r");
            int gamepadIndex = Enumerable.Range(0, action.bindings.Count)
                .Single(i => action.bindings[i].path == "<Gamepad>/buttonNorth");
            string oldKeyboard = action.bindings[keyboardIndex].overridePath;
            string oldGamepad = action.bindings[gamepadIndex].overridePath;
            string oldLanguage = Rpg.Soundwell.SoundwellUiLocalization.Language;
            // Initialize saved bindings before the isolated remap under test.
            Rpg.Soundwell.SoundwellInputAdapter.EntryKeyboardShortcutLabel();
            var entry = Resources.Load<InputActionAsset>("Input/SoundwellControls")
                .FindActionMap("Entry", true).FindAction("Interact", true);
            int entryPadIndex = Enumerable.Range(0, entry.bindings.Count)
                .Single(i => entry.bindings[i].path == "<Gamepad>/buttonSouth");
            string oldEntryPad = entry.bindings[entryPadIndex].overridePath;
            try
            {
                action.ApplyBindingOverride(keyboardIndex, "<Keyboard>/t");
                entry.ApplyBindingOverride(entryPadIndex, "<Gamepad>/buttonWest");
                action.ApplyBindingOverride(gamepadIndex, "<Gamepad>/buttonWest");
                string key = action.GetBindingDisplayString(keyboardIndex);
                string pad = action.GetBindingDisplayString(gamepadIndex);
                foreach (string locale in new[]
                {
                    Rpg.Soundwell.SoundwellUiLocalization.English,
                    Rpg.Soundwell.SoundwellUiLocalization.Chinese
                })
                {
                    Rpg.Soundwell.SoundwellUiLocalization.SetLanguage(locale);
                    string expected = locale == Rpg.Soundwell.SoundwellUiLocalization.Chinese
                        ? "[" + key + "／手柄 " + pad + "]"
                        : "[" + key + " / Pad " + pad + "]";
                    string hint = Rpg.Soundwell.SoundwellUiLocalization.Get("hint.reset_here");
                    Assert.That(hint, Does.StartWith(expected),
                        "The real retry control must match the displayed action in both locales.");
                    Assert.That(hint, Does.Not.StartWith("[R /"),
                        "A saved remap must not leave the default key in the trial HUD.");

                    string actualPad = entry.GetBindingDisplayString(entryPadIndex);
                    string entryKey = Rpg.Soundwell.SoundwellInputAdapter.EntryKeyboardShortcutLabel();
                    string expectedEntry = locale == Rpg.Soundwell.SoundwellUiLocalization.Chinese
                        ? "[" + entryKey + "／手柄 " + actualPad + "]"
                        : "[" + entryKey + " / Pad " + actualPad + "]";
                    string entryPrompt = Rpg.Soundwell.SoundwellEntrance.EntryActionPromptForCurrentBindings();
                    Assert.That(entryPrompt, Does.StartWith(expectedEntry),
                        "Entry hints must reflect the real remapped gamepad shortcut.");
                    Assert.That(entryPrompt, Does.Not.Contain("Pad A"));
                    Assert.That(entryPrompt, Does.Not.Contain("手柄 A"));
                }

                // A disabled Entry gamepad action must not advertise Pad A.
                // The independent focused UGUI Submit remains available.
                entry.ApplyBindingOverride(entryPadIndex, "");
                foreach (string locale in new[]
                {
                    Rpg.Soundwell.SoundwellUiLocalization.English,
                    Rpg.Soundwell.SoundwellUiLocalization.Chinese
                })
                {
                    Rpg.Soundwell.SoundwellUiLocalization.SetLanguage(locale);
                    string prompt = Rpg.Soundwell.SoundwellEntrance.EntryActionPromptForCurrentBindings();
                    string shortcut = Rpg.Soundwell.SoundwellInputAdapter.EntryKeyboardShortcutLabel();
                    Assert.That(prompt, Does.StartWith("[" + shortcut + "]"));
                    Assert.That(prompt, Does.Not.Contain("Pad A"));
                    Assert.That(prompt, Does.Not.Contain("手柄 A"));
                }
            }
            finally
            {
                if (oldEntryPad == null)
                    entry.RemoveBindingOverride(entryPadIndex);
                else
                    entry.ApplyBindingOverride(entryPadIndex, oldEntryPad);
                if (oldKeyboard == null)
                    action.RemoveBindingOverride(keyboardIndex);
                else
                    action.ApplyBindingOverride(keyboardIndex, oldKeyboard);
                if (oldGamepad == null)
                    action.RemoveBindingOverride(gamepadIndex);
                else
                    action.ApplyBindingOverride(gamepadIndex, oldGamepad);
                Rpg.Soundwell.SoundwellUiLocalization.SetLanguage(oldLanguage);
            }
        }

        [Test]
        public void SavedLocalizationTablesAreCachedWhileLiveLocaleChanges()
        {
            // HUD refreshes should reuse both shipped table instances.
            var flags = System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static;
            var owner = typeof(Rpg.Soundwell.SoundwellUiLocalization);
            var enField = owner.GetField("englishTable", flags);
            var zhField = owner.GetField("chineseTable", flags);
            Assert.That(enField, Is.Not.Null);
            Assert.That(zhField, Is.Not.Null);
            string previous = Rpg.Soundwell.SoundwellUiLocalization.Language;
            var en = Resources.Load<StringTable>("Soundwell/Localization/SoundwellUI-en");
            var zh = Resources.Load<StringTable>("Soundwell/Localization/SoundwellUI-zh-Hans");
            Assert.That(en, Is.Not.Null);
            Assert.That(zh, Is.Not.Null);
            try
            {
                Rpg.Soundwell.SoundwellUiLocalization.SetLanguage(
                    Rpg.Soundwell.SoundwellUiLocalization.English);
                for (int i = 0; i < 3; i++)
                    Assert.That(Rpg.Soundwell.SoundwellUiLocalization.Get("entry.title"),
                        Is.EqualTo(en.GetEntry("entry.title").LocalizedValue));
                Rpg.Soundwell.SoundwellUiLocalization.SetLanguage(
                    Rpg.Soundwell.SoundwellUiLocalization.Chinese);
                for (int i = 0; i < 3; i++)
                    Assert.That(Rpg.Soundwell.SoundwellUiLocalization.Get("entry.title"),
                        Is.EqualTo(zh.GetEntry("entry.title").LocalizedValue));
                Assert.That(enField.GetValue(null), Is.SameAs(en));
                Assert.That(zhField.GetValue(null), Is.SameAs(zh));
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
