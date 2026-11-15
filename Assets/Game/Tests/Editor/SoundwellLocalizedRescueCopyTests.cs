using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
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

            var en = Resources.Load<StringTable>("Soundwell/Localization/SoundwellUI-en");
            var zh = Resources.Load<StringTable>("Soundwell/Localization/SoundwellUI-zh-Hans");
            Assert.That(en, Is.Not.Null);
            Assert.That(zh, Is.Not.Null);
            foreach (string key in new[]
            {
                "entry.whitewater_replay_evacuee",
                "entry.whitewater_replay_bell",
                "entry.whitewater_replay_cable"
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
