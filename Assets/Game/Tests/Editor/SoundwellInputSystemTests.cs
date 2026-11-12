using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace Rpg.Tests.Editor
{
    public sealed class SoundwellInputSystemTests
    {
        [Test]
        public void InputActionsHaveSeparateContextsAndKeepBothBackendsEnabled()
        {
            SoundwellInputAdapter.ValidateActionAsset();
            string playerSettings = File.ReadAllText("ProjectSettings/ProjectSettings.asset");
            Assert.That(playerSettings, Does.Match(@"(?m)^\s*activeInputHandler:\s*2\s*$"),
                "Both keeps ORK's legacy backend available while game-owned actions move to the New Input System.");
            Assert.That(File.Exists("Assets/Game/Resources/Input/SoundwellControls.inputactions"), Is.True);
            AssertSavedMenuBindingsCannotBeDisabled();
        }

        // Existing exact-test manifest keeps its count. Exercise both the
        // broken persisted-json case and a legitimate rebound key against a
        // cloned SAVED InputActionAsset, leaving the running adapter alone.
        private static void AssertSavedMenuBindingsCannotBeDisabled()
        {
            const string key = "rpg.soundwell.input.binding-overrides.v1";
            bool hadPreference = PlayerPrefs.HasKey(key);
            string priorPreference = PlayerPrefs.GetString(key, string.Empty);
            var savedAsset = Resources.Load<InputActionAsset>("Input/SoundwellControls");
            Assert.That(savedAsset, Is.Not.Null);
            var isolatedAsset = Object.Instantiate(savedAsset);
            try
            {
                var menu = isolatedAsset.FindActionMap("Gameplay", true)
                    .FindAction("ToggleMenu", true);
                Assert.That(menu.bindings.Count, Is.GreaterThanOrEqualTo(2));
                var restore = typeof(SoundwellInputAdapter).GetMethod(
                    "RestoreSavedBindingOverrides",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Static);
                Assert.That(restore, Is.Not.Null);
                // Test each mission-critical input in isolation. All disable
                // cases use valid Input System JSON, not malformed text, and
                // are restored through the same production PlayerPrefs path.
                foreach (var required in new[]
                {
                    (Name: "ToggleMenu", DefaultPath: "<Keyboard>/tab"),
                    (Name: "Ring", DefaultPath: "<Keyboard>/e"),
                    (Name: "Wrap", DefaultPath: "<Keyboard>/q"),
                    (Name: "Carry", DefaultPath: "<Keyboard>/f"),
                    (Name: "Gate", DefaultPath: "<Keyboard>/g"),
                    (Name: "Retry", DefaultPath: "<Keyboard>/r")
                })
                {
                    var action = isolatedAsset.FindActionMap("Gameplay", true)
                        .FindAction(required.Name, true);
                    Assert.That(action.bindings.Count, Is.GreaterThanOrEqualTo(1));
                    for (int i = 0; i < action.bindings.Count; i++)
                        action.ApplyBindingOverride(i, string.Empty);
                    Assert.That(action.bindings.All(binding =>
                        string.IsNullOrEmpty(binding.effectivePath)), Is.True,
                        required.Name + " must really lose every effective binding.");

                    string disabledJson = isolatedAsset.SaveBindingOverridesAsJson();
                    Assert.That(disabledJson, Is.Not.Empty);
                    isolatedAsset.RemoveAllBindingOverrides();
                    PlayerPrefs.SetString(key, disabledJson);
                    LogAssert.Expect(LogType.Warning,
                        "Soundwell binding overrides are invalid; restored default controls (InvalidOperationException).");
                    restore.Invoke(null, new object[] { isolatedAsset });
                    Assert.That(PlayerPrefs.HasKey(key), Is.False,
                        required.Name + " must reject a valid-JSON but unplayable preference.");
                    Assert.That(action.bindings.Any(binding =>
                        binding.effectivePath == required.DefaultPath), Is.True,
                        "Restore the saved default input for " + required.Name);
                }
                Assert.That(menu.bindings.Any(binding =>
                    binding.effectivePath == "<Gamepad>/start"), Is.True);

                // A 2DVector's W/A/S/D child entries retain effective paths
                // even when their composite root is overridden to empty. With
                // both gamepad paths also disabled, no movement remains. This
                // is valid Input System JSON, so ensure the restore rejects it
                // rather than accepting orphaned keyboard composite parts.
                var move = isolatedAsset.FindActionMap("Gameplay", true)
                    .FindAction("Move", true);
                Assert.That(move.bindings.Any(binding => binding.isComposite), Is.True);
                Assert.That(move.bindings.Any(binding =>
                    binding.isPartOfComposite), Is.True);
                for (int i = 0; i < move.bindings.Count; i++)
                {
                    if (!move.bindings[i].isPartOfComposite)
                        move.ApplyBindingOverride(i, string.Empty);
                }
                Assert.That(move.bindings.Where(binding =>
                    !binding.isPartOfComposite).All(binding =>
                    string.IsNullOrEmpty(binding.effectivePath)), Is.True);
                Assert.That(move.bindings.Any(binding =>
                    binding.isPartOfComposite && !string.IsNullOrEmpty(binding.effectivePath)),
                    Is.True, "Orphaned direction parts reproduce the ignored composite-root failure.");
                string noMoveJson = isolatedAsset.SaveBindingOverridesAsJson();
                isolatedAsset.RemoveAllBindingOverrides();
                PlayerPrefs.SetString(key, noMoveJson);
                LogAssert.Expect(LogType.Warning,
                    "Soundwell binding overrides are invalid; restored default controls (InvalidOperationException).");
                restore.Invoke(null, new object[] { isolatedAsset });
                Assert.That(PlayerPrefs.HasKey(key), Is.False,
                    "A saved composite-root override must not strand all movement.");
                Assert.That(move.bindings.Any(binding =>
                    binding.isComposite && binding.effectivePath == "2DVector"), Is.True);
                Assert.That(move.bindings.Any(binding =>
                    binding.effectivePath == "<Gamepad>/leftStick"), Is.True);

                // Do not erase a usable preference merely because it differs
                // from the authored default keyboard key.
                menu.ApplyBindingOverride(0, "<Keyboard>/f10");
                string validJson = isolatedAsset.SaveBindingOverridesAsJson();
                isolatedAsset.RemoveAllBindingOverrides();
                PlayerPrefs.SetString(key, validJson);
                restore.Invoke(null, new object[] { isolatedAsset });
                Assert.That(menu.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/f10"));
                Assert.That(PlayerPrefs.GetString(key), Is.EqualTo(validJson),
                    "Legitimate remapping must remain persisted.");
            }
            finally
            {
                if (hadPreference)
                    PlayerPrefs.SetString(key, priorPreference);
                else
                    PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
                Object.DestroyImmediate(isolatedAsset);
            }
        }

        [UnityTest]
        public IEnumerator EntryAndGameplayContextsRouteLiveKeyboardAndGamepadEvents()
        {
            // Bootstrap's SoundwellEntrance initializes the production action asset in Awake.
            // Keep this input-runtime test isolated so InputTestFixture owns the runtime first.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);

            // InputTestFixture is designed for Play Mode tests. It isolates the
            // input runtime and drives real InputAction bindings without host hardware.
            var input = new InputTestFixture();
            input.Setup();
            try
            {
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                Gamepad gamepad = InputSystem.AddDevice<Gamepad>();

                SoundwellInputAdapter.SetEntryContext();
                Assert.That(SoundwellInputAdapter.IsEntryContextActive, Is.True);

                input.Press(keyboard.f2Key);
                yield return null;
                Assert.That(SoundwellInputAdapter.InteractPressed(), Is.True,
                    "F2 must activate the real Entry action.");
                Assert.That(SoundwellInputAdapter.EntryKeyboardShortcutPressed(), Is.True,
                    "Only the real F2 binding bypasses a focused UGUI settings button.");
                input.Release(keyboard.f2Key);
                yield return null;

                input.Press(gamepad.buttonSouth);
                yield return null;
                Assert.That(SoundwellInputAdapter.InteractPressed(), Is.True,
                    "Gamepad South must activate the real Entry action.");
                Assert.That(SoundwellInputAdapter.EntryKeyboardShortcutPressed(), Is.False,
                    "Gamepad South is also UGUI Submit and must respect focused buttons.");
                Assert.That(SoundwellInputAdapter.ReadGameplay().Ring, Is.False,
                    "Entry input must not also ring the gameplay bell.");
                input.Release(gamepad.buttonSouth);
                yield return null;

                SoundwellInputAdapter.SetGameplayContext();
                Assert.That(SoundwellInputAdapter.IsGameplayContextActive, Is.True);

                input.Press(gamepad.buttonSouth);
                yield return null;
                Assert.That(SoundwellInputAdapter.ReadGameplay().Ring, Is.True,
                    "Gamepad South must ring only after the Gameplay map is active.");
                Assert.That(SoundwellInputAdapter.InteractPressed(), Is.False,
                    "Gameplay input must not also activate the Entry action.");
                input.Release(gamepad.buttonSouth);
                yield return null;

                input.Press(keyboard.wKey);
                // InputTestFixture queues UnityTest events for the next player-loop update.
                // Force one deterministic update here so the composite value is observable
                // before the test runner advances the coroutine again.
                InputSystem.Update();
                Assert.That(keyboard.wKey.isPressed, Is.True,
                    "The test runtime must apply the queued W key state.");
                Assert.That(SoundwellInputAdapter.ReadGameplay().Movement.y, Is.GreaterThan(0.9f),
                    "W must drive the real Gameplay Move action.");

                SoundwellInputAdapter.SetUiContext();
                Assert.That(SoundwellInputAdapter.IsUiContextActive, Is.True,
                    "Focus mode may enable only the menu toggle, not world movement.");
                Assert.That(SoundwellInputAdapter.IsGameplayContextActive, Is.False);
                input.Release(keyboard.wKey);
                yield return null;
                input.Press(keyboard.sKey);
                yield return null;
                Assert.That(SoundwellInputAdapter.ReadGameplay().Movement, Is.EqualTo(Vector2.zero),
                    "UI focus must disable gameplay movement rather than moving behind a focused menu.");
                input.Release(keyboard.sKey);
                yield return null;

                // The same physical key/button must be able to close the menu
                // after gameplay actions are suspended. Previously ToggleMenu
                // was disabled with the rest of Gameplay: the menu trapped focus.
                input.Press(keyboard.tabKey);
                // WasPressedThisFrame is scoped to an Input System update. In
                // Editor UnityTests a coroutine yield can advance past that
                // update before the assertion; process the queued event and
                // inspect the action in the SAME update, as with Move above.
                InputSystem.Update();
                Assert.That(keyboard.tabKey.isPressed, Is.True,
                    "The test runtime must receive the Tab key-down event.");
                Assert.That(SoundwellInputAdapter.ToggleMenuPressed(), Is.True,
                    "Tab must remain available to close the currently focused UGUI menu.");
                Assert.That(SoundwellInputAdapter.IsUiContextActive, Is.True);
                Assert.That(SoundwellInputAdapter.ReadGameplay().Ring, Is.False);
                input.Release(keyboard.tabKey);
                InputSystem.Update();

                input.Press(gamepad.startButton);
                InputSystem.Update();
                Assert.That(gamepad.startButton.isPressed, Is.True,
                    "The test runtime must receive Gamepad Start.");
                Assert.That(SoundwellInputAdapter.ToggleMenuPressed(), Is.True,
                    "Gamepad Start must close the UGUI menu without enabling world controls.");
                Assert.That(SoundwellInputAdapter.IsUiContextActive, Is.True);
                input.Release(gamepad.startButton);
                InputSystem.Update();

                SoundwellInputAdapter.SetGameplayContext();
                Assert.That(SoundwellInputAdapter.IsGameplayContextActive, Is.True,
                    "Closing focus mode must restore Gameplay input.");
                Assert.That(SoundwellInputAdapter.IsUiContextActive, Is.False);

                // Gamepad South is both Entry/UGUI Submit and the gameplay
                // Ring action. Holding the physical button across a context
                // handoff must not manufacture an additional bell action.
                SoundwellInputAdapter.SetEntryContext();
                input.Press(gamepad.buttonSouth);
                InputSystem.Update();
                Assert.That(SoundwellInputAdapter.InteractPressed(), Is.True);
                SoundwellInputAdapter.SetGameplayContext();
                InputSystem.Update();
                Assert.That(SoundwellInputAdapter.ReadGameplay().Ring, Is.False,
                    "Holding Entry Submit across the handoff must not ring the bell.");
                input.Release(gamepad.buttonSouth);
                InputSystem.Update();
                input.Press(gamepad.buttonSouth);
                InputSystem.Update();
                Assert.That(SoundwellInputAdapter.ReadGameplay().Ring, Is.True,
                    "Releasing and pressing again must still ring the bell.");
                input.Release(gamepad.buttonSouth);
                InputSystem.Update();

                SoundwellInputAdapter.SetUiContext();
                input.Press(gamepad.buttonSouth);
                InputSystem.Update();
                Assert.That(SoundwellInputAdapter.ReadGameplay().Ring, Is.False,
                    "Focused UGUI Submit cannot trigger a gameplay action.");
                SoundwellInputAdapter.SetGameplayContext();
                InputSystem.Update();
                Assert.That(SoundwellInputAdapter.ReadGameplay().Ring, Is.False,
                    "Returning from focused UGUI with South held cannot auto-ring.");
                input.Release(gamepad.buttonSouth);
                InputSystem.Update();
            }
            finally
            {
                SoundwellInputAdapter.SetUiContext();
                input.TearDown();
            }

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator GamepadSubmitOnFocusedEntrySfxButtonDoesNotStartTheTrial()
        {
            // Exercise the actual EventSystem and real InputAction events
            // without ORK bootstrapping a parallel trial scene.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);
            var input = new InputTestFixture();
            input.Setup();
            bool hadMutePreference = PlayerPrefs.HasKey(SoundwellAudioBus.MutePreferenceKey);
            int previousMutePreference = PlayerPrefs.GetInt(SoundwellAudioBus.MutePreferenceKey, 0);
            bool hadLanguagePreference = PlayerPrefs.HasKey(SoundwellUiLocalization.LanguagePreferenceKey);
            string previousLanguagePreference = PlayerPrefs.GetString(
                SoundwellUiLocalization.LanguagePreferenceKey, string.Empty);
            string previousLanguage = SoundwellUiLocalization.Language;
            try
            {
                Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
                var entryRoot = new GameObject("Soundwell Entry UI input regression");
                var entry = entryRoot.AddComponent<SoundwellEntrance>();
                yield return null; // Start selects Enter first.

                // An inactive ORK/Makinom EventSystem must be retired
                // before scene handoff can reactivate it as a second UI owner.
                // Keep this regression inside the existing tracked real-input
                // UnityTest so exact-test evidence collection remains strict.
                var parkedUi = new GameObject("Parked ORK input owner");
                parkedUi.SetActive(false);
                parkedUi.AddComponent<EventSystem>();
                parkedUi.AddComponent<StandaloneInputModule>();
                EventSystem ownedUi = SoundwellInputAdapter.EnsureEventSystem(entryRoot.transform);
                Assert.That(ownedUi, Is.Not.Null);
                Assert.That(ownedUi.gameObject.activeInHierarchy, Is.True);
                yield return null; // Complete deferred removal.
                parkedUi.SetActive(true);
                yield return null;
                var liveUiOwners = UnityEngine.Object.FindObjectsByType<EventSystem>(
                    FindObjectsSortMode.None);
                Assert.That(liveUiOwners, Has.Length.EqualTo(1),
                    "Reactivated ORK UI must not add a second EventSystem.");
                Assert.That(liveUiOwners[0], Is.SameAs(ownedUi));
                Assert.That(parkedUi.GetComponent<EventSystem>(), Is.Null,
                    "Inactive foreign input owner must be removed before reactivation.");
                UnityEngine.Object.Destroy(parkedUi);

                var sfxButton = entry.GetComponentsInChildren<Button>(true)
                    .Single(button => button.gameObject.name == "Soundwell SFX toggle");
                // The visible retry/result summary must never overlap either
                // the entry footer or the SFX control at 4:3/narrow and 16:9.
                // Verify the actual UGUI RectTransforms, not just hardcoded text.
                foreach (float aspect in new[] { 1f, 16f / 9f })
                {
                    entry.LayoutForViewportAspect(aspect);
                    var labels = entry.GetComponentsInChildren<Text>(true);
                    var footer = labels.Single(t =>
                        t.text == SoundwellUiLocalization.Get("entry.footer")).rectTransform;
                    var result = labels.Single(t =>
                        t.text == entry.ReturnSummaryText &&
                        t.rectTransform.sizeDelta.y >= 100f).rectTransform;
                    var sfxRect = (RectTransform)sfxButton.transform;
                    float footerBottom = footer.anchoredPosition.y - footer.sizeDelta.y * 0.5f;
                    float resultTop = result.anchoredPosition.y + result.sizeDelta.y * 0.5f;
                    float resultBottom = result.anchoredPosition.y - result.sizeDelta.y * 0.5f;
                    float sfxTop = sfxRect.anchoredPosition.y + sfxRect.sizeDelta.y * 0.5f;
                    Assert.That(footerBottom - resultTop, Is.GreaterThanOrEqualTo(4f),
                        "The return result must not cover the entry instructions at aspect " + aspect);
                    Assert.That(resultBottom - sfxTop, Is.GreaterThanOrEqualTo(4f),
                        "The return result must not cover the SFX button at aspect " + aspect);
                }
                entry.LayoutForViewportAspect((float)Screen.width / Mathf.Max(Screen.height, 1));
                EventSystem events = EventSystem.current;
                Assert.That(events, Is.Not.Null);
                events.SetSelectedGameObject(sfxButton.gameObject);
                Assert.That(events.currentSelectedGameObject, Is.SameAs(sfxButton.gameObject));
                string initialScene = SceneManager.GetActiveScene().name;
                bool mutedBefore = SoundwellAudioBus.IsMuted;

                // UI Submit and the Entry/Interact action receive the SAME pad A.
                // The UI must toggle mute, not also transition into SoundwellTrial.
                input.Press(gamepad.buttonSouth);
                yield return null;
                yield return null;
                Assert.That(SoundwellAudioBus.IsMuted, Is.EqualTo(!mutedBefore),
                    "Gamepad A must actually activate the focused SFX UGUI Button.");
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(initialScene),
                    "Gamepad Submit on a settings button cannot also enter the trial.");
                Assert.That(entry, Is.Not.Null,
                    "The Entry UI must survive a gamepad settings action.");
                input.Release(gamepad.buttonSouth);
                yield return null;

                // This is a second REAL focused UGUI setting, rather than a
                // synthetic locale setter. Gamepad A must toggle the saved
                // language without simultaneously activating Entry/Interact.
                var languageButton = entry.GetComponentsInChildren<Button>(true)
                    .Single(button => button.gameObject.name == "Soundwell entry Language button");
                // The actual UGUI navigation graph must keep every setting
                // reachable with D-pad arrows and show the same selection tint.
                var enterButton = entry.GetComponentsInChildren<Button>(true)
                    .Single(button => button.gameObject.name == "Enter Soundwell button");
                Assert.That(enterButton.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
                Assert.That(languageButton.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
                Assert.That(sfxButton.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
                Assert.That(enterButton.navigation.selectOnUp, Is.SameAs(languageButton));
                Assert.That(enterButton.navigation.selectOnDown, Is.SameAs(sfxButton));
                Assert.That(languageButton.navigation.selectOnDown, Is.SameAs(enterButton));
                Assert.That(sfxButton.navigation.selectOnUp, Is.SameAs(enterButton));
                Assert.That(languageButton.navigation.selectOnUp, Is.SameAs(sfxButton));
                Assert.That(sfxButton.navigation.selectOnDown, Is.SameAs(languageButton));
                Assert.That(languageButton.transition, Is.EqualTo(Selectable.Transition.ColorTint));
                Assert.That(sfxButton.transition, Is.EqualTo(Selectable.Transition.ColorTint));
                Assert.That(enterButton.colors.selectedColor,
                    Is.Not.EqualTo(enterButton.colors.normalColor));
                Assert.That(sfxButton.colors.selectedColor,
                    Is.EqualTo(enterButton.colors.selectedColor));
                Assert.That(languageButton.colors.selectedColor,
                    Is.EqualTo(enterButton.colors.selectedColor));
                events.SetSelectedGameObject(languageButton.gameObject);
                Assert.That(events.currentSelectedGameObject,
                    Is.SameAs(languageButton.gameObject));
                string languageBefore = SoundwellUiLocalization.Language;
                input.Press(gamepad.buttonSouth);
                yield return null;
                yield return null;
                string languageAfter = languageBefore == SoundwellUiLocalization.English
                    ? SoundwellUiLocalization.Chinese : SoundwellUiLocalization.English;
                Assert.That(SoundwellUiLocalization.Language, Is.EqualTo(languageAfter),
                    "Gamepad Submit must toggle the actual entry-screen locale button.");
                Assert.That(PlayerPrefs.GetString(SoundwellUiLocalization.LanguagePreferenceKey),
                    Is.EqualTo(languageAfter), "The player choice must persist.");
                Assert.That(languageButton.GetComponentInChildren<Text>().text,
                    Is.EqualTo(SoundwellUiLocalization.Get("action.language")));
                Assert.That(entry.GetComponentsInChildren<Text>(true).Any(label =>
                    label.text == SoundwellUiLocalization.Get("entry.title")), Is.True,
                    "The entry heading must refresh without reloading the scene.");
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(initialScene),
                    "Gamepad Submit on Language must not also start the trial.");
                Assert.That(events.currentSelectedGameObject,
                    Is.SameAs(languageButton.gameObject),
                    "Changing language must keep focus on its setting button.");
                input.Release(gamepad.buttonSouth);
                yield return null;
            }
            finally
            {
                SoundwellUiLocalization.SetLanguage(previousLanguage);
                if (hadLanguagePreference)
                    PlayerPrefs.SetString(SoundwellUiLocalization.LanguagePreferenceKey,
                        previousLanguagePreference);
                else
                    PlayerPrefs.DeleteKey(SoundwellUiLocalization.LanguagePreferenceKey);
                var mixer = Resources.Load<UnityEngine.Audio.AudioMixer>(SoundwellAudioBus.MixerResource);
                if (mixer != null)
                    SoundwellAudioBus.SetMuted(mixer, previousMutePreference != 0);
                if (!hadMutePreference)
                    PlayerPrefs.DeleteKey(SoundwellAudioBus.MutePreferenceKey);
                PlayerPrefs.Save();
                SoundwellInputAdapter.SetUiContext();
                input.TearDown();
            }

            yield return new ExitPlayMode();
        }
    }
}
