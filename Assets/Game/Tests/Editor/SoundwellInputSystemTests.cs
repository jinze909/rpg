using System.Collections;
using System.IO;
using System.Linq;
using GamingIsLove.Makinom;
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
        // S00 v1.2 deliberately keeps UGUI ownership after roll call until
        // the player asks one of Shen Li's two authored wrench questions.
        // Every live-input regression must earn movement through both choices.
        private static void FinishRollCallAndWrenchQuestion(WhitewaterPrelude stage)
        {
            Assert.That(stage.Phase, Is.Zero);
            Button firstChoice = stage.GetComponentsInChildren<Button>(true)
                .Single(b => b.gameObject.name == "S00 choice 0");
            firstChoice.onClick.Invoke();
            Assert.That(stage.Phase, Is.EqualTo(1));
            Assert.That(SoundwellInputAdapter.IsUiContextActive, Is.True,
                "The wrench question must remain a real dialogue choice.");
            Assert.That(firstChoice.GetComponentInChildren<Text>(true).text,
                Is.EqualTo(SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese
                    ? "为什么叫这个？" : "Why call it that?"));
            firstChoice.onClick.Invoke();
            Assert.That(stage.Phase, Is.EqualTo(1));
            Assert.That(SoundwellInputAdapter.IsGameplayContextActive, Is.True,
                "Gameplay input begins only after choosing a wrench question.");
        }
        [Test]
        public void S00EndingReturnControlRemainsClickableOnUltrawideWindows()
        {
            var position = typeof(WhitewaterPrelude).GetMethod(
                "EpilogueExitPosition",
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic);
            Assert.That(position, Is.Not.Null);
            Vector2 standard = (Vector2)position.Invoke(null,
                new object[] { 16f / 9f });
            Assert.That(standard, Is.EqualTo(new Vector2(0f, -190f)),
                "The authored 16:9 ending layout should not shift.");

            foreach (float aspect in new[]
            {
                320f / 720f, 32f / 9f, 5f
            })
            {
                Vector2 slot = (Vector2)position.Invoke(null,
                    new object[] { aspect });
                float halfHeight = 0.5f *
                    Mathf.Sqrt(960f * 540f / aspect);
                Assert.That(slot.x, Is.EqualTo(0f));
                Assert.That(slot.y - 14.5f,
                    Is.GreaterThanOrEqualTo(-halfHeight + 12f - 0.01f),
                    "The full 29px return button must remain inside the bottom of the screen.");
                Assert.That(slot.y + 14.5f,
                    Is.LessThanOrEqualTo(halfHeight - 12f));
            }
            Assert.That((Vector2)position.Invoke(null,
                new object[] { float.NaN }),
                Is.EqualTo(standard),
                "Invalid aspect values must not hide the only route out.");
        }

        [UnityTest]
        public IEnumerator S00DirectAudioFallbackTracksMuteAndRejoinsRealMixer()
        {
            // AudioMixer.SetFloat has no active mixer during EditMode import;
            // exercise the real mute pathway after audio runtime startup.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);
            var mixer = Resources.Load<UnityEngine.Audio.AudioMixer>(SoundwellAudioBus.MixerResource);
            Assert.That(mixer, Is.Not.Null);
            var root = new GameObject("S00 direct SFX fallback mute");
            var source = root.AddComponent<AudioSource>();
            bool hadPreference = PlayerPrefs.HasKey(SoundwellAudioBus.MutePreferenceKey);
            bool originalMute = SoundwellAudioBus.IsMuted;
            var route = typeof(SoundwellAudioBus).GetMethod("RouteWithMixer",
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic);
            Assert.That(route, Is.Not.Null);
            try
            {
                LogAssert.Expect(LogType.Error,
                    "M6 Soundwell AudioMixer asset is missing; using direct AudioSource mute fallback.");
                Assert.That(route.Invoke(null, new object[] { source, null }), Is.EqualTo(false));
                Assert.That(source.outputAudioMixerGroup, Is.Null);
                Assert.That(source.mute, Is.EqualTo(originalMute));

                // A valid mixer still accepts the player's SFX preference,
                // but the already-fallback direct source must change too.
                Assert.That(SoundwellAudioBus.SetMuted(mixer, !originalMute), Is.True);
                Assert.That(source.mute, Is.EqualTo(!originalMute));
                Assert.That(SoundwellAudioBus.SetMuted(mixer, originalMute), Is.True);
                Assert.That(source.mute, Is.EqualTo(originalMute));

                // Rejoining the actual SFX group removes fallback tracking.
                Assert.That(route.Invoke(null, new object[] { source, mixer }), Is.EqualTo(true));
                Assert.That(source.outputAudioMixerGroup?.name, Is.EqualTo("SFX"));
                Assert.That(source.mute, Is.False);
                Assert.That(SoundwellAudioBus.SetMuted(mixer, !originalMute), Is.True);
                Assert.That(source.mute, Is.False,
                    "A mixer-routed bell cannot retain a duplicate per-source mute.");
            }
            finally
            {
                SoundwellAudioBus.SetMuted(mixer, originalMute);
                if (!hadPreference)
                {
                    PlayerPrefs.DeleteKey(SoundwellAudioBus.MutePreferenceKey);
                    PlayerPrefs.Save();
                }
                Object.Destroy(root);
            }
            yield return new ExitPlayMode();
        }

        [Test]
        public void EntryHintRefreshRespondsOnlyToEffectiveBindingChanges()
        {
            var filter = typeof(SoundwellEntrance).GetMethod(
                "ShouldRefreshBindingPrompt",
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic);
            Assert.That(filter, Is.Not.Null);
            Assert.That(filter.Invoke(null, new object[] {
                InputActionChange.BoundControlsChanged }), Is.EqualTo(true));
            Assert.That(filter.Invoke(null, new object[] {
                InputActionChange.ActionStarted }), Is.EqualTo(false));
            Assert.That(filter.Invoke(null, new object[] {
                InputActionChange.ActionCanceled }), Is.EqualTo(false));
        }

        [Test]
        public void UnifiedHeldInputLatchConsumesReleaseBeforeAllowingNewPress()
        {
            // The adapter suppresses the opening press and its release, not
            // the first intentional later press. Exercise the exact shared
            // latch code without keyboard/gamepad dependence.
            var owner = typeof(SoundwellInputAdapter);
            var flags = System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static;
            var kinds = owner.GetNestedType("HeldControl",
                System.Reflection.BindingFlags.NonPublic);
            var states = owner.GetField("heldUntilRelease", flags);
            var consume = owner.GetMethod("ConsumeHeldLatch", flags);
            Assert.That(kinds, Is.Not.Null);
            Assert.That(states, Is.Not.Null);
            Assert.That(consume, Is.Not.Null);
            var array = (bool[])states.GetValue(null);
            var previous = (bool[])array.Clone();
            try
            {
                var toggle = System.Enum.Parse(kinds, "Toggle");
                int index = (int)System.Convert.ChangeType(toggle, typeof(int));
                array[index] = true;
                Assert.That(consume.Invoke(null, new object[] { toggle, true }), Is.EqualTo(true));
                Assert.That(consume.Invoke(null, new object[] { toggle, false }), Is.EqualTo(true),
                    "The release frame must remain consumed.");
                Assert.That(array[index], Is.False);
                Assert.That(consume.Invoke(null, new object[] { toggle, true }), Is.EqualTo(false),
                    "A later, intentional fresh press must be delivered.");
            }
            finally
            {
                System.Array.Copy(previous, array, array.Length);
            }
        }

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
                    (Map: "Entry", Name: "Interact", DefaultPath: "<Keyboard>/f2"),
                    (Map: "Gameplay", Name: "Back", DefaultPath: "<Keyboard>/escape"),
                    (Map: "Gameplay", Name: "ToggleMenu", DefaultPath: "<Keyboard>/tab"),
                    (Map: "Gameplay", Name: "Ring", DefaultPath: "<Keyboard>/e"),
                    (Map: "Gameplay", Name: "Wrap", DefaultPath: "<Keyboard>/q"),
                    (Map: "Gameplay", Name: "Carry", DefaultPath: "<Keyboard>/f"),
                    (Map: "Gameplay", Name: "Gate", DefaultPath: "<Keyboard>/g"),
                    (Map: "Gameplay", Name: "Retry", DefaultPath: "<Keyboard>/r")
                })
                {
                    var action = isolatedAsset.FindActionMap(required.Map, true)
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

                // S00 uses the same gamepad A for UGUI Submit. Holding it
                // while the prologue returns to Entry cannot trigger a new
                // Soundwell load. A fresh intentional press must still work.
                input.Press(gamepad.buttonSouth);
                InputSystem.Update();
                Assert.That(SoundwellInputAdapter.InteractPressed(), Is.True);
                SoundwellInputAdapter.SetUiContext();
                SoundwellInputAdapter.SetEntryContext();
                InputSystem.Update();
                Assert.That(SoundwellInputAdapter.InteractPressed(), Is.False,
                    "The held S00 Submit cannot also enter the trial on return.");
                input.Release(gamepad.buttonSouth);
                InputSystem.Update();
                Assert.That(SoundwellInputAdapter.InteractPressed(), Is.False);
                input.Press(gamepad.buttonSouth);
                InputSystem.Update();
                Assert.That(SoundwellInputAdapter.InteractPressed(), Is.True,
                    "Releasing A must rearm the next real Entry Submit.");
                input.Release(gamepad.buttonSouth);
                InputSystem.Update();

                // A re-enabled keyboard shortcut follows the same rule.
                input.Press(keyboard.f2Key);
                InputSystem.Update();
                Assert.That(SoundwellInputAdapter.EntryKeyboardShortcutPressed(), Is.True);
                SoundwellInputAdapter.SetUiContext();
                SoundwellInputAdapter.SetEntryContext();
                InputSystem.Update();
                Assert.That(SoundwellInputAdapter.EntryKeyboardShortcutPressed(), Is.False,
                    "One held F2 press must not be replayed by a context handoff.");
                input.Release(keyboard.f2Key);
                InputSystem.Update();
                Assert.That(SoundwellInputAdapter.EntryKeyboardShortcutPressed(), Is.False);
                input.Press(keyboard.f2Key);
                InputSystem.Update();
                Assert.That(SoundwellInputAdapter.EntryKeyboardShortcutPressed(), Is.True);
                input.Release(keyboard.f2Key);
                InputSystem.Update();

                // Entry's user-remapped keyboard shortcut is still a keyboard
                // action, not a hardcoded F2. Verify the actual Input System
                // event after changing the saved action asset's effective path;
                // Gamepad South must never bypass focused UGUI settings.
                var entryAction = Resources.Load<InputActionAsset>("Input/SoundwellControls")
                    .FindActionMap("Entry", true).FindAction("Interact", true);
                int entryKeyboardBinding = Enumerable.Range(0, entryAction.bindings.Count)
                    .Single(i => entryAction.bindings[i].path == "<Keyboard>/f2");
                string priorEntryOverride = entryAction.bindings[entryKeyboardBinding].overridePath;
                SoundwellInputAdapter.SetUiContext();
                entryAction.ApplyBindingOverride(entryKeyboardBinding, "<Keyboard>/f3");
                SoundwellInputAdapter.SetEntryContext();
                try
                {
                    input.Press(keyboard.f2Key);
                    InputSystem.Update();
                    Assert.That(SoundwellInputAdapter.EntryKeyboardShortcutPressed(), Is.False,
                        "The superseded F2 key cannot enter after remapping.");
                    input.Release(keyboard.f2Key);
                    InputSystem.Update();

                    input.Press(keyboard.f3Key);
                    InputSystem.Update();
                    Assert.That(SoundwellInputAdapter.InteractPressed(), Is.True);
                    Assert.That(SoundwellInputAdapter.EntryKeyboardShortcutPressed(), Is.True,
                        "The saved F3 keyboard shortcut must enter without requiring a pointer.");
                    Assert.That(SoundwellInputAdapter.EntryKeyboardShortcutLabel(),
                        Is.EqualTo("F3"),
                        "Entry instructions must follow the effective keyboard remap.");
                    input.Release(keyboard.f3Key);
                    InputSystem.Update();

                    input.Press(gamepad.buttonSouth);
                    InputSystem.Update();
                    Assert.That(SoundwellInputAdapter.EntryKeyboardShortcutPressed(), Is.False,
                        "Remapping the keyboard must never turn Gamepad Submit into an entry bypass.");
                    input.Release(gamepad.buttonSouth);
                    InputSystem.Update();
                }
                finally
                {
                    SoundwellInputAdapter.SetUiContext();
                    if (priorEntryOverride == null)
                        entryAction.RemoveBindingOverride(entryKeyboardBinding);
                    else
                        entryAction.ApplyBindingOverride(entryKeyboardBinding, priorEntryOverride);
                    SoundwellInputAdapter.SetEntryContext();
                }

                // Rebound interaction instructions in the *playable S00*
                // must show the actual InputAction, not the legacy E prompt.
                // Keep this regression in the existing live-input test, so
                // the strict collector's expected test list is unchanged.
                var ringAction = Resources.Load<InputActionAsset>("Input/SoundwellControls")
                    .FindActionMap("Gameplay", true).FindAction("Ring", true);
                int ringKeyboardBinding = Enumerable.Range(0, ringAction.bindings.Count)
                    .Single(i => ringAction.bindings[i].path == "<Keyboard>/e");
                int ringGamepadBinding = Enumerable.Range(0, ringAction.bindings.Count)
                    .Single(i => ringAction.bindings[i].path == "<Gamepad>/buttonSouth");
                string priorRingOverride = ringAction.bindings[ringKeyboardBinding].overridePath;
                string priorGamepadOverride = ringAction.bindings[ringGamepadBinding].overridePath;
                string originalLocale = SoundwellUiLocalization.Language;
                SoundwellInputAdapter.SetUiContext();
                ringAction.ApplyBindingOverride(ringKeyboardBinding, "<Keyboard>/r");
                ringAction.ApplyBindingOverride(ringGamepadBinding, "<Gamepad>/buttonWest");
                try
                {
                    Assert.That(SoundwellInputAdapter.GameplayKeyboardInteractionLabel(),
                        Is.EqualTo("R"));
                    string padBinding = ringAction.GetBindingDisplayString(ringGamepadBinding);
                    Assert.That(SoundwellInputAdapter.GameplayGamepadInteractionLabel(),
                        Is.EqualTo(padBinding));
                    Assert.That(ringAction.bindings[ringGamepadBinding].effectivePath,
                        Is.EqualTo("<Gamepad>/buttonWest"));
                    foreach (string locale in new[]
                    {
                        SoundwellUiLocalization.English, SoundwellUiLocalization.Chinese
                    })
                    {
                        SoundwellUiLocalization.SetLanguage(locale);
                        var stageRoot = new GameObject("S00 rebound interaction hint regression");
                        var stage = stageRoot.AddComponent<WhitewaterPrelude>();
                        stage.Begin(null);
                        FinishRollCallAndWrenchQuestion(stage);
                        string expected = locale == SoundwellUiLocalization.Chinese
                            ? "R／手柄 " + padBinding : "R / Pad " + padBinding;
                        string outdatedKey = locale == SoundwellUiLocalization.Chinese
                            ? "E／手柄 A" : "E / Pad A";
                        Assert.That(stage.CurrentInstructions, Does.Contain(expected),
                            "S00 must teach both effective Gameplay/Ring bindings.");
                        Assert.That(stage.CurrentInstructions, Does.Not.Contain(outdatedKey),
                            "Do not advertise default E or Pad A after remapping.");
                        stage.Close();
                        yield return null;
                    }
                }
                finally
                {
                    SoundwellInputAdapter.SetUiContext();
                    if (priorRingOverride == null)
                        ringAction.RemoveBindingOverride(ringKeyboardBinding);
                    else
                        ringAction.ApplyBindingOverride(ringKeyboardBinding, priorRingOverride);
                    if (priorGamepadOverride == null)
                        ringAction.RemoveBindingOverride(ringGamepadBinding);
                    else
                        ringAction.ApplyBindingOverride(ringGamepadBinding, priorGamepadOverride);
                    SoundwellUiLocalization.SetLanguage(originalLocale);
                }

                // The same S00 movement prompt must use the remapped
                // Gameplay/Move composite, not advertise default WASD.
                var moveAction = Resources.Load<InputActionAsset>("Input/SoundwellControls")
                    .FindActionMap("Gameplay", true).FindAction("Move", true);
                var paths = new[] { "<Keyboard>/w", "<Keyboard>/s",
                    "<Keyboard>/a", "<Keyboard>/d" };
                var remaps = new[] { "<Keyboard>/i", "<Keyboard>/k",
                    "<Keyboard>/j", "<Keyboard>/l" };
                var moveIndices = paths.Select(path => Enumerable.Range(0, moveAction.bindings.Count)
                    .Single(i => moveAction.bindings[i].path == path)).ToArray();
                var previousMoves = moveIndices.Select(i => moveAction.bindings[i].overridePath)
                    .ToArray();
                string previousMoveLocale = SoundwellUiLocalization.Language;
                SoundwellInputAdapter.SetUiContext();
                for (int i = 0; i < moveIndices.Length; i++)
                    moveAction.ApplyBindingOverride(moveIndices[i], remaps[i]);
                try
                {
                    Assert.That(SoundwellInputAdapter.GameplayKeyboardMovementLabel(),
                        Is.EqualTo("IJKL"));
                    foreach (string locale in new[]
                    {
                        SoundwellUiLocalization.English, SoundwellUiLocalization.Chinese
                    })
                    {
                        SoundwellUiLocalization.SetLanguage(locale);
                        var stageRoot = new GameObject("S00 rebound movement hint regression");
                        var stage = stageRoot.AddComponent<WhitewaterPrelude>();
                        stage.Begin(null);
                        FinishRollCallAndWrenchQuestion(stage);
                        Assert.That(stage.CurrentInstructions, Does.Contain("IJKL"),
                            "S00 walking instructions must follow physical Move overrides.");
                        Assert.That(stage.CurrentInstructions, Does.Not.Contain("WASD"),
                            "Do not advertise the default movement keys after remapping.");
                        stage.Close();
                        yield return null;
                    }

                    // Some valid saved remaps leave a subset of keyboard
                    // directions unbound in BOTH keyboard composites. The
                    // remaining keys still move the actor; the prompt must
                    // show them, not pretend only a gamepad can move.
                    var arrowPaths = new[] { "<Keyboard>/upArrow", "<Keyboard>/downArrow",
                        "<Keyboard>/leftArrow", "<Keyboard>/rightArrow" };
                    var arrowIndices = arrowPaths.Select(path =>
                        Enumerable.Range(0, moveAction.bindings.Count)
                            .Single(i => moveAction.bindings[i].path == path)).ToArray();
                    var previousArrows = arrowIndices.Select(i =>
                        moveAction.bindings[i].overridePath).ToArray();
                    try
                    {
                        moveAction.ApplyBindingOverride(moveIndices[0], "");
                        foreach (int index in arrowIndices)
                            moveAction.ApplyBindingOverride(index, "");
                        Assert.That(SoundwellInputAdapter.GameplayKeyboardMovementLabel(),
                            Is.EqualTo("-/J/K/L"),
                            "A partial keyboard remap should show actual usable keys and an unbound Up.");
                        SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                        var partialRoot = new GameObject("S00 partial keyboard movement cue regression");
                        var partialStage = partialRoot.AddComponent<WhitewaterPrelude>();
                        partialStage.Begin(null);
                        FinishRollCallAndWrenchQuestion(partialStage);
                        Assert.That(partialStage.CurrentInstructions, Does.Contain("-/J/K/L"));
                        Assert.That(partialStage.CurrentInstructions, Does.Not.Contain("IJKL"),
                            "The removed Up binding cannot be advertised as playable.");
                        partialStage.Close();
                        yield return null;
                    }
                    finally
                    {
                        moveAction.ApplyBindingOverride(moveIndices[0], remaps[0]);
                        for (int i = 0; i < arrowIndices.Length; i++)
                        {
                            if (previousArrows[i] == null)
                                moveAction.RemoveBindingOverride(arrowIndices[i]);
                            else
                                moveAction.ApplyBindingOverride(arrowIndices[i], previousArrows[i]);
                        }
                    }

                    // A player can retain gamepad Move while disabling all
                    // keyboard directions. Override the actual composite
                    // PARTS, not their roots: Input System 1.x can throw while
                    // resolving live child bindings when a root is disabled.
                    // This must exercise a valid playable gamepad-only setup.
                    var keyboardParts = Enumerable.Range(0, moveAction.bindings.Count)
                        .Where(index => moveAction.bindings[index].isPartOfComposite &&
                            moveAction.bindings[index].effectivePath.StartsWith(
                                "<Keyboard>/", System.StringComparison.OrdinalIgnoreCase))
                        .ToArray();
                    Assert.That(keyboardParts, Is.Not.Empty);
                    var savedParts = keyboardParts.Select(index =>
                        moveAction.bindings[index].overridePath).ToArray();
                    try
                    {
                        foreach (int part in keyboardParts)
                            moveAction.ApplyBindingOverride(part, "");
                        Assert.That(SoundwellInputAdapter.GameplayKeyboardMovementLabel(),
                            Is.Empty, "A gamepad-only binding must not show disabled keyboard directions.");
                        SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                        var joystickStageRoot = new GameObject("S00 gamepad-only movement cue regression");
                        var joystickStage = joystickStageRoot.AddComponent<WhitewaterPrelude>();
                        joystickStage.Begin(null);
                        FinishRollCallAndWrenchQuestion(joystickStage);
                        Assert.That(joystickStage.CurrentInstructions, Does.Contain("left stick"));
                        Assert.That(joystickStage.CurrentInstructions, Does.Not.Contain("IJKL"),
                            "S00 must not advertise disabled keyboard directions.");
                        Assert.That(joystickStage.CurrentInstructions, Does.Not.Contain("WASD"));
                        joystickStage.Close();
                        yield return null;
                    }
                    finally
                    {
                        for (int i = 0; i < keyboardParts.Length; i++)
                        {
                            if (savedParts[i] == null)
                                moveAction.RemoveBindingOverride(keyboardParts[i]);
                            else
                                moveAction.ApplyBindingOverride(keyboardParts[i], savedParts[i]);
                        }
                    }
                }
                finally
                {
                    SoundwellInputAdapter.SetUiContext();
                    for (int i = 0; i < moveIndices.Length; i++)
                    {
                        if (previousMoves[i] == null)
                            moveAction.RemoveBindingOverride(moveIndices[i]);
                        else
                            moveAction.ApplyBindingOverride(moveIndices[i], previousMoves[i]);
                    }
                    SoundwellUiLocalization.SetLanguage(previousMoveLocale);
                }

                // A remapped Gameplay/Back must update the actual S00
                // walking instruction, and the same physical buttons must
                // still cancel a walking phase. UI Cancel is separate.
                var backAction = Resources.Load<InputActionAsset>("Input/SoundwellControls")
                    .FindActionMap("Gameplay", true).FindAction("Back", true);
                int backKeyIndex = Enumerable.Range(0, backAction.bindings.Count)
                    .Single(i => backAction.bindings[i].path == "<Keyboard>/escape");
                int backPadIndex = Enumerable.Range(0, backAction.bindings.Count)
                    .Single(i => backAction.bindings[i].path == "<Gamepad>/buttonEast");
                string priorBackKey = backAction.bindings[backKeyIndex].overridePath;
                string priorBackPad = backAction.bindings[backPadIndex].overridePath;
                string priorBackLocale = SoundwellUiLocalization.Language;
                SoundwellInputAdapter.SetUiContext();
                backAction.ApplyBindingOverride(backKeyIndex, "<Keyboard>/backspace");
                backAction.ApplyBindingOverride(backPadIndex, "<Gamepad>/buttonWest");
                try
                {
                    string keyBack = backAction.GetBindingDisplayString(backKeyIndex);
                    string padBack = backAction.GetBindingDisplayString(backPadIndex);
                    Assert.That(SoundwellInputAdapter.GameplayKeyboardBackLabel(), Is.EqualTo(keyBack));
                    Assert.That(SoundwellInputAdapter.GameplayGamepadBackLabel(), Is.EqualTo(padBack));
                    foreach (string locale in new[]
                    {
                        SoundwellUiLocalization.English, SoundwellUiLocalization.Chinese
                    })
                    {
                        SoundwellUiLocalization.SetLanguage(locale);
                        var stageRoot = new GameObject("S00 rebound Back hint regression");
                        var stage = stageRoot.AddComponent<WhitewaterPrelude>();
                        stage.Begin(null);
                        FinishRollCallAndWrenchQuestion(stage);
                        string expected = locale == SoundwellUiLocalization.Chinese
                            ? keyBack + "／手柄 " + padBack
                            : keyBack + " / Pad " + padBack;
                        Assert.That(stage.CurrentInstructions, Does.Contain(expected),
                            "Gameplay Back prompt must display both effective bindings.");
                        Assert.That(stage.CurrentInstructions, Does.Not.Contain(
                            locale == SoundwellUiLocalization.Chinese ? "Esc／手柄 B" : "Esc / Pad B"));
                        stage.Close();
                        yield return null;
                    }

                    SoundwellInputAdapter.SetGameplayContext();
                    input.Press(keyboard.backspaceKey);
                    InputSystem.Update();
                    Assert.That(SoundwellInputAdapter.ReadGameplay().Back, Is.True,
                        "The displayed rebound Backspace key must cancel Gameplay.");
                    input.Release(keyboard.backspaceKey);
                    InputSystem.Update();
                    input.Press(gamepad.buttonWest);
                    InputSystem.Update();
                    Assert.That(SoundwellInputAdapter.ReadGameplay().Back, Is.True,
                        "The displayed rebound gamepad West button must cancel Gameplay.");
                    input.Release(gamepad.buttonWest);
                    InputSystem.Update();
                }
                finally
                {
                    SoundwellInputAdapter.SetUiContext();
                    if (priorBackKey == null)
                        backAction.RemoveBindingOverride(backKeyIndex);
                    else
                        backAction.ApplyBindingOverride(backKeyIndex, priorBackKey);
                    if (priorBackPad == null)
                        backAction.RemoveBindingOverride(backPadIndex);
                    else
                        backAction.ApplyBindingOverride(backPadIndex, priorBackPad);
                    SoundwellUiLocalization.SetLanguage(priorBackLocale);
                }

                SoundwellInputAdapter.SetGameplayContext();
                Assert.That(SoundwellInputAdapter.IsGameplayContextActive, Is.True);

                input.Press(gamepad.buttonSouth);
                // The expanded remap test drove manual InputSystem updates
                // before this assertion. Read WasPressedThisFrame in the
                // same update as the queued press, not after a coroutine yield.
                InputSystem.Update();
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

                // A UI Cancel is the same Escape / Pad B physical control as
                // Gameplay Back. Merely re-enabling the gameplay map while
                // that press is held must not trigger a second, scene-wide exit.
                SoundwellInputAdapter.SetUiContext();
                input.Press(gamepad.buttonEast);
                InputSystem.Update();
                SoundwellInputAdapter.SetGameplayContext();
                InputSystem.Update();
                Assert.That(SoundwellInputAdapter.ReadGameplay().Back, Is.False,
                    "Holding Pad B across UGUI to Gameplay cannot exit the scene.");
                input.Release(gamepad.buttonEast);
                InputSystem.Update();
                Assert.That(SoundwellInputAdapter.ReadGameplay().Back, Is.False);
                input.Press(gamepad.buttonEast);
                InputSystem.Update();
                Assert.That(SoundwellInputAdapter.ReadGameplay().Back, Is.True,
                    "A fresh Pad B after release must still exit normally.");
                input.Release(gamepad.buttonEast);
                InputSystem.Update();

                SoundwellInputAdapter.SetUiContext();
                input.Press(keyboard.escapeKey);
                InputSystem.Update();
                SoundwellInputAdapter.SetGameplayContext();
                InputSystem.Update();
                Assert.That(SoundwellInputAdapter.ReadGameplay().Back, Is.False,
                    "Holding Escape across UGUI to Gameplay cannot exit the scene.");
                input.Release(keyboard.escapeKey);
                InputSystem.Update();
                Assert.That(SoundwellInputAdapter.ReadGameplay().Back, Is.False);
                input.Press(keyboard.escapeKey);
                InputSystem.Update();
                Assert.That(SoundwellInputAdapter.ReadGameplay().Back, Is.True,
                    "A fresh Escape press after release must still exit normally.");
                input.Release(keyboard.escapeKey);
                InputSystem.Update();

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

                // Tab/Start is the SAME action in both Gameplay and focused
                // UGUI. Do not open and immediately close a menu when the
                // player holds the physical key across the map re-enable.
                input.Press(gamepad.startButton);
                InputSystem.Update();
                Assert.That(SoundwellInputAdapter.ToggleMenuPressed(), Is.True);
                SoundwellInputAdapter.SetUiContext();
                InputSystem.Update();
                Assert.That(SoundwellInputAdapter.ToggleMenuPressed(), Is.False,
                    "Holding Start across Gameplay→UGUI cannot toggle again.");
                input.Release(gamepad.startButton);
                InputSystem.Update();
                Assert.That(SoundwellInputAdapter.ToggleMenuPressed(), Is.False);
                input.Press(gamepad.startButton);
                InputSystem.Update();
                Assert.That(SoundwellInputAdapter.ToggleMenuPressed(), Is.True,
                    "Releasing and pressing Start again must still close UGUI.");
                SoundwellInputAdapter.SetGameplayContext();
                InputSystem.Update();
                Assert.That(SoundwellInputAdapter.ToggleMenuPressed(), Is.False,
                    "Holding Start across UGUI→Gameplay cannot reopen the menu.");
                input.Release(gamepad.startButton);
                InputSystem.Update();
                Assert.That(SoundwellInputAdapter.ToggleMenuPressed(), Is.False);
                input.Press(gamepad.startButton);
                InputSystem.Update();
                Assert.That(SoundwellInputAdapter.ToggleMenuPressed(), Is.True,
                    "A fresh Start press must reopen the menu after release.");
                input.Release(gamepad.startButton);
                InputSystem.Update();

                SoundwellInputAdapter.SetGameplayContext();
                input.Press(keyboard.tabKey);
                InputSystem.Update();
                Assert.That(SoundwellInputAdapter.ToggleMenuPressed(), Is.True);
                SoundwellInputAdapter.SetUiContext();
                InputSystem.Update();
                Assert.That(SoundwellInputAdapter.ToggleMenuPressed(), Is.False,
                    "Held Tab cannot double-toggle on entry into UGUI.");
                input.Release(keyboard.tabKey);
                InputSystem.Update();
                Assert.That(SoundwellInputAdapter.ToggleMenuPressed(), Is.False);
                input.Press(keyboard.tabKey);
                InputSystem.Update();
                Assert.That(SoundwellInputAdapter.ToggleMenuPressed(), Is.True,
                    "Tab must work again on its next intentional press.");
                input.Release(keyboard.tabKey);
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
        public IEnumerator WhitewaterCableRepairRequiresRealGamepadStickAndFreshSouthPress()
        {
            // The S00 narrative uses the shared gameplay map. Verify its actual
            // pad route, not a direct phase setter or a keyboard-only surrogate.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);

            var input = new InputTestFixture();
            input.Setup();
            WhitewaterPrelude prelude = null;
            try
            {
                Gamepad pad = InputSystem.AddDevice<Gamepad>();
                bool exited = false;
                var root = new GameObject("S00 real gamepad repair regression");
                prelude = root.AddComponent<WhitewaterPrelude>();
                prelude.Begin(() =>
                {
                    exited = true;
                    SoundwellInputAdapter.SetEntryContext();
                });
                yield return null;

                Assert.That(prelude.Phase, Is.EqualTo(0));
                FinishRollCallAndWrenchQuestion(prelude);
                yield return null;
                Assert.That(prelude.Phase, Is.EqualTo(1));
                Assert.That(SoundwellInputAdapter.IsGameplayContextActive, Is.True);
                Assert.That(prelude.CanRepair, Is.False);

                // Pressing the correct action from the wrong position cannot
                // secretly brace the clamp, award repair, or leave gameplay.
                input.Press(pad.buttonSouth);
                yield return null;
                input.Release(pad.buttonSouth);
                yield return null;
                Assert.That(prelude.Phase, Is.EqualTo(1));
                Assert.That(prelude.CanRepair, Is.False);

                input.Set(pad.leftStick, Vector2.right);
                float reachDeadline = Time.realtimeSinceStartup + 6f;
                while (!prelude.CanRepair && Time.realtimeSinceStartup < reachDeadline)
                    yield return null;
                input.Set(pad.leftStick, Vector2.zero);
                yield return null;
                Assert.That(prelude.CanRepair, Is.True,
                    "The real left stick must move the player to Shen Li's clamp.");
                Assert.That(prelude.Phase, Is.EqualTo(1),
                    "Simply arriving with the stick must never auto-complete the repair.");

                input.Press(pad.buttonSouth);
                yield return null;
                input.Release(pad.buttonSouth);
                yield return null;
                Assert.That(prelude.Phase, Is.EqualTo(1),
                    "The first real wrench knock cannot finish the repair or open choices.");
                Assert.That(prelude.CurrentNarration, Does.Contain("Tax Collector"));
                Assert.That(prelude.CurrentInstructions, Does.Contain("second time"));
                Assert.That(SoundwellInputAdapter.IsGameplayContextActive, Is.True);
                input.Press(pad.buttonSouth);
                yield return null;
                input.Release(pad.buttonSouth);
                yield return null;
                Assert.That(prelude.Phase, Is.EqualTo(2),
                    "Only the second fresh pad A at the actual clamp may open repair choices.");
                Assert.That(prelude.CurrentNarration, Does.Contain(
                    SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese
                        ? "监察员" : "Inspector"),
                    "The earned second knock must introduce Shen Li's other tool nickname before M05.");
                Assert.That(SoundwellInputAdapter.IsUiContextActive, Is.True);
                Assert.That(EventSystem.current?.currentSelectedGameObject?.name,
                    Is.EqualTo("S00 choice 0"),
                    "After using the gamepad in-world, the repair choices must regain UGUI focus.");

                // B must cancel from a focused choice and return to the saved
                // entry input context, without granting any repair choice.
                input.Press(pad.buttonEast);
                yield return null;
                input.Release(pad.buttonEast);
                yield return null;
                Assert.That(exited, Is.True);
                Assert.That(prelude == null, Is.True);
                Assert.That(SoundwellInputAdapter.IsEntryContextActive, Is.True);
            }
            finally
            {
                if (prelude != null)
                    prelude.Close();
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
            bool hadFormationPreference = PlayerPrefs.HasKey(OrkPartyBridge.FixedFormationPreferenceKey);
            int previousFormationPreference = PlayerPrefs.GetInt(
                OrkPartyBridge.FixedFormationPreferenceKey, 0);
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

                // Verify the player-visible localized Entry button follows a
                // live keyboard remap, rather than falsely advertising F2.
                var entryAction = Resources.Load<InputActionAsset>("Input/SoundwellControls")
                    .FindActionMap("Entry", true).FindAction("Interact", true);
                int entryBinding = Enumerable.Range(0, entryAction.bindings.Count)
                    .Single(i => entryAction.bindings[i].path == "<Keyboard>/f2");
                string originalEntryOverride = entryAction.bindings[entryBinding].overridePath;
                try
                {
                    entryAction.ApplyBindingOverride(entryBinding, "<Keyboard>/f3");
                    yield return null;
                    var enterHint = entry.GetComponentsInChildren<Text>(true)
                        .Single(label => label.transform.parent != null &&
                            label.transform.parent.name == "Enter Soundwell button");
                    Assert.That(enterHint.text, Does.Contain("[F3 /"),
                        "The real entry screen must show the player's active shortcut.");
                    Assert.That(enterHint.text, Does.Not.Contain("[F2 /"),
                        "Never leave the default F2 instruction after rebinding.");
                }
                finally
                {
                    if (originalEntryOverride == null)
                        entryAction.RemoveBindingOverride(entryBinding);
                    else
                        entryAction.ApplyBindingOverride(entryBinding, originalEntryOverride);
                }
                yield return null;

                var sfxButton = entry.GetComponentsInChildren<Button>(true)
                    .Single(button => button.gameObject.name == "Soundwell SFX toggle");
                var formationButton = entry.GetComponentsInChildren<Button>(true)
                    .Single(button => button.gameObject.name == "Soundwell formation toggle");
                var saveCheckpoint = entry.GetComponentsInChildren<Button>(true)
                    .Single(button => button.gameObject.name == "Save ORK checkpoint button");
                var loadCheckpoint = entry.GetComponentsInChildren<Button>(true)
                    .Single(button => button.gameObject.name == "Load ORK checkpoint button");
                Assert.That(saveCheckpoint.interactable, Is.False,
                    "Without a real ORK bootstrap the UI cannot overwrite a save.");
                Assert.That(loadCheckpoint.interactable, Is.False,
                    "Without a real ORK bootstrap the UI cannot load a save.");
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
                    var formationRect = (RectTransform)formationButton.transform;
                    var prologueButtonForLayout = entry.GetComponentsInChildren<Button>(true)
                        .Single(b => b.gameObject.name == "S00 Whitewater prologue button");
                    var prologueButtonRect = (RectTransform)prologueButtonForLayout.transform;
                    var prologueLabel = prologueButtonForLayout.GetComponentInChildren<Text>();
                    Assert.That(prologueLabel, Is.Not.Null);
                    Assert.That(prologueLabel.resizeTextForBestFit, Is.True,
                        "The S00 entry must fit translated copy without shrinking its click target.");
                    Assert.That(prologueLabel.rectTransform.sizeDelta.x,
                        Is.LessThanOrEqualTo(prologueButtonRect.sizeDelta.x - 8f),
                        "S00 text must remain inside the button horizontally at aspect " + aspect);
                    Assert.That(prologueLabel.rectTransform.sizeDelta.y,
                        Is.LessThanOrEqualTo(prologueButtonRect.sizeDelta.y - 3f),
                        "S00 text must remain inside the button vertically at aspect " + aspect);
                    float settingsGap = formationRect.anchoredPosition.x -
                        formationRect.sizeDelta.x * 0.5f -
                        (sfxRect.anchoredPosition.x + sfxRect.sizeDelta.x * 0.5f);
                    Assert.That(settingsGap, Is.GreaterThanOrEqualTo(4f),
                        "The two real entry settings must not overlap at aspect " + aspect);
                    float footerBottom = footer.anchoredPosition.y - footer.sizeDelta.y * 0.5f;
                    float resultTop = result.anchoredPosition.y + result.sizeDelta.y * 0.5f;
                    float resultBottom = result.anchoredPosition.y - result.sizeDelta.y * 0.5f;
                    float sfxTop = sfxRect.anchoredPosition.y + sfxRect.sizeDelta.y * 0.5f;
                    Assert.That(footerBottom - resultTop, Is.GreaterThanOrEqualTo(4f),
                        "The return result must not cover the entry instructions at aspect " + aspect);
                    Assert.That(resultBottom - sfxTop, Is.GreaterThanOrEqualTo(4f),
                        "The return result must not cover the SFX button at aspect " + aspect);
                }
                // On ultrawide CanvasScaler screens, the original y=-230
                // settings row vanishes below the visible viewport. Check the
                // real entry RectTransforms at both 32:9 and 5:1, including
                // return text and each keyboard/gamepad-focusable button.
                var entryControls = entry.GetComponentsInChildren<Button>(true);
                var enterForLayout = entryControls.Single(button =>
                    button.gameObject.name == "Enter Soundwell button");
                var localeForLayout = entryControls.Single(button =>
                    button.gameObject.name == "Soundwell entry Language button");
                var prologueForLayout = entryControls.Single(button =>
                    button.gameObject.name == "S00 Whitewater prologue button");
                var entryLabels = entry.GetComponentsInChildren<Text>(true);
                var titleForLayout = entryLabels.Single(t =>
                    t.text == SoundwellUiLocalization.Get("entry.title")).rectTransform;
                var footerForLayout = entryLabels.Single(t =>
                    t.text == SoundwellUiLocalization.Get("entry.footer")).rectTransform;
                var resultForLayout = entryLabels.Single(t =>
                    t.text == entry.ReturnSummaryText &&
                    t.rectTransform.sizeDelta.y >= 100f).rectTransform;
                var allEntryRects = new[]
                {
                    titleForLayout, (RectTransform)enterForLayout.transform,
                    footerForLayout, resultForLayout,
                    (RectTransform)localeForLayout.transform,
                    (RectTransform)prologueForLayout.transform,
                    (RectTransform)formationButton.transform,
                    (RectTransform)sfxButton.transform,
                    (RectTransform)saveCheckpoint.transform,
                    (RectTransform)loadCheckpoint.transform
                };
                foreach (float aspect in new[] { 32f / 9f, 5f })
                {
                    entry.LayoutForViewportAspect(aspect);
                    float halfWidth = 0.5f * Mathf.Sqrt(960f * 540f * aspect);
                    float halfHeight = 0.5f * Mathf.Sqrt(960f * 540f / aspect);
                    for (int i = 0; i < allEntryRects.Length; i++)
                    {
                        RectTransform current = allEntryRects[i];
                        float x = current.anchoredPosition.x;
                        float y = current.anchoredPosition.y;
                        float halfW = current.sizeDelta.x * 0.5f;
                        float halfH = current.sizeDelta.y * 0.5f;
                        Assert.That(Mathf.Abs(x) + halfW,
                            Is.LessThanOrEqualTo(halfWidth - 12f),
                            current.name + " exceeds the ultrawide canvas width at " + aspect);
                        Assert.That(Mathf.Abs(y) + halfH,
                            Is.LessThanOrEqualTo(halfHeight - 12f),
                            current.name + " exceeds the ultrawide canvas height at " + aspect);
                        for (int j = 0; j < i; j++)
                        {
                            RectTransform other = allEntryRects[j];
                            float dx = Mathf.Abs(x - other.anchoredPosition.x);
                            float dy = Mathf.Abs(y - other.anchoredPosition.y);
                            float minX = (current.sizeDelta.x + other.sizeDelta.x) * 0.5f + 4f;
                            float minY = (current.sizeDelta.y + other.sizeDelta.y) * 0.5f + 4f;
                            Assert.That(dx >= minX || dy >= minY, Is.True,
                                current.name + " overlaps " + other.name +
                                " at ultrawide aspect " + aspect);
                        }
                    }
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
                Assert.That(saveCheckpoint.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
                Assert.That(loadCheckpoint.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
                Assert.That(languageButton.navigation.selectOnLeft, Is.SameAs(saveCheckpoint));
                Assert.That(languageButton.navigation.selectOnRight, Is.SameAs(loadCheckpoint));
                Assert.That(sfxButton.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
                Assert.That(formationButton.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
                Assert.That(sfxButton.navigation.selectOnRight, Is.SameAs(formationButton));
                Assert.That(formationButton.navigation.selectOnLeft, Is.SameAs(sfxButton));
                Assert.That(formationButton.navigation.selectOnUp, Is.SameAs(enterButton));
                var prologueButton = entry.GetComponentsInChildren<Button>(true)
                    .Single(button => button.gameObject.name == "S00 Whitewater prologue button");
                Assert.That(prologueButton.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
                Assert.That(enterButton.navigation.selectOnUp, Is.SameAs(prologueButton));
                Assert.That(enterButton.navigation.selectOnDown, Is.SameAs(sfxButton));
                Assert.That(prologueButton.navigation.selectOnUp, Is.SameAs(languageButton));
                Assert.That(prologueButton.navigation.selectOnDown, Is.SameAs(enterButton));
                Assert.That(languageButton.navigation.selectOnDown, Is.SameAs(prologueButton));
                Assert.That(sfxButton.navigation.selectOnUp, Is.SameAs(enterButton));
                Assert.That(languageButton.navigation.selectOnUp, Is.SameAs(sfxButton));
                Assert.That(sfxButton.navigation.selectOnDown, Is.SameAs(languageButton));
                Assert.That(languageButton.transition, Is.EqualTo(Selectable.Transition.ColorTint));
                Assert.That(sfxButton.transition, Is.EqualTo(Selectable.Transition.ColorTint));
                Assert.That(formationButton.transition, Is.EqualTo(Selectable.Transition.ColorTint));
                Assert.That(prologueButton.transition, Is.EqualTo(Selectable.Transition.ColorTint));
                Assert.That(enterButton.colors.selectedColor,
                    Is.Not.EqualTo(enterButton.colors.normalColor));
                Assert.That(prologueButton.colors.selectedColor,
                    Is.EqualTo(enterButton.colors.selectedColor));
                Assert.That(sfxButton.colors.selectedColor,
                    Is.EqualTo(enterButton.colors.selectedColor));
                Assert.That(formationButton.colors.selectedColor,
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
                Assert.That(prologueButton.GetComponentInChildren<Text>().text,
                    Is.EqualTo(SoundwellUiLocalization.Get("entry.whitewater_prologue")),
                    "S00's playable entry must use the same saved EN/zh StringTables as the other entry choices.");
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(initialScene),
                    "Gamepad Submit on Language must not also start the trial.");
                Assert.That(events.currentSelectedGameObject,
                    Is.SameAs(languageButton.gameObject),
                    "Changing language must keep focus on its setting button.");
                input.Release(gamepad.buttonSouth);
                yield return null;

                // The real UGUI selector changes the next encounter's
                // mobility rules without pressing Enter or starting a scene.
                events.SetSelectedGameObject(formationButton.gameObject);
                bool wasFixed = OrkPartyBridge.PreferredFixedFormation;
                input.Press(gamepad.buttonSouth);
                yield return null;
                yield return null;
                Assert.That(OrkPartyBridge.PreferredFixedFormation, Is.EqualTo(!wasFixed));
                Assert.That(formationButton.GetComponentInChildren<Text>().text,
                    Is.EqualTo(SoundwellUiLocalization.Get(
                        wasFixed ? "entry.formation_mobile" : "entry.formation_fixed")));
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(initialScene),
                    "Focused formation toggle must not also load the trial.");
                Assert.That(events.currentSelectedGameObject,
                    Is.SameAs(formationButton.gameObject));
                input.Release(gamepad.buttonSouth);
                yield return null;

                // This new story route must be playable through the real
                // Bootstrap UGUI Submit, not just an unconnected component.
                events.SetSelectedGameObject(prologueButton.gameObject);
                input.Press(gamepad.buttonSouth);
                yield return null;
                yield return null;
                var prelude = entry.GetComponentInChildren<WhitewaterPrelude>(true);
                Assert.That(prelude, Is.Not.Null, "Pad A must open the S00 bridge scene.");
                Assert.That(prelude.Phase, Is.EqualTo(0));
                // The playable introduction binds the same 16 x 24 authored
                // character sprites before any rescue or bridge interaction.
                foreach (string actorName in new[] {
                    "S00 player", "S00 Shen Li",
                    "S00 Han Qiao in water", "S00 last evacuee" })
                {
                    var actorTransform = prelude.transform.Find(
                        "S00 Whitewater playable canvas/" + actorName);
                    Assert.That(actorTransform, Is.Not.Null);
                    var actorImage = actorTransform.GetComponent<Image>();
                    Assert.That(actorImage.sprite, Is.Not.Null,
                        actorName + " must have a pixel-art sprite.");
                    Assert.That(actorImage.sprite.rect.size, Is.EqualTo(new Vector2(16f, 24f)));
                    Assert.That(UnityEditor.AssetDatabase.GetAssetPath(actorImage.sprite),
                        Does.StartWith("Assets/Game/Art/"));
                    Assert.That(actorImage.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                    Assert.That(actorImage.rectTransform.sizeDelta, Is.EqualTo(new Vector2(32f, 48f)),
                        "The authored character must be enlarged by an exact 2x scale.");
                    Assert.That(actorImage.raycastTarget, Is.False,
                        "Decorative characters must never steal UGUI input.");
                }
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(initialScene),
                    "S00 must preserve the existing Bootstrap and Soundwell entry.");
                Assert.That(entry.OpenTrial(), Is.Null,
                    "A gamepad double-submit must not load Soundwell while S00 is active.");
                input.Release(gamepad.buttonSouth);
                yield return null;

                // A UGUI choice owns focus during S00 dialogue. Gamepad B
                // must cancel here as reliably as it does during movement,
                // without selecting an answer or starting the trial.
                Assert.That(SoundwellInputAdapter.IsUiContextActive, Is.True);
                Assert.That(events.currentSelectedGameObject?.name,
                    Is.EqualTo("S00 choice 0"));
                input.Press(gamepad.buttonEast);
                yield return null;
                input.Release(gamepad.buttonEast);
                yield return null;
                Assert.That(entry.GetComponentInChildren<WhitewaterPrelude>(true), Is.Null,
                    "UI Cancel from a focused S00 choice must return to Bootstrap.");
                Assert.That(SoundwellInputAdapter.IsEntryContextActive, Is.True);
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(initialScene));
                events.SetSelectedGameObject(prologueButton.gameObject);
                input.Press(gamepad.buttonSouth);
                yield return null;
                yield return null;
                input.Release(gamepad.buttonSouth);
                yield return null;
                prelude = entry.GetComponentInChildren<WhitewaterPrelude>(true);
                Assert.That(prelude, Is.Not.Null,
                    "Cancelling the dialogue must allow an intentional S00 replay.");
                Assert.That(prelude.Phase, Is.EqualTo(0),
                    "The replay must start from roll call, not the cancelled state.");

                var answer = prelude.GetComponentsInChildren<Button>(true)
                    .Single(button => button.gameObject.name == "S00 choice 0");
                FinishRollCallAndWrenchQuestion(prelude);
                yield return null;
                Assert.That(prelude.Phase, Is.EqualTo(1));
                Assert.That(SoundwellInputAdapter.IsGameplayContextActive, Is.True,
                    "Roll call must release the UI context for actual world movement.");

                // Cancel must remain reachable without a mouse while world
                // movement has disabled the UGUI navigation action map.
                input.Press(gamepad.buttonEast);
                yield return null;
                input.Release(gamepad.buttonEast);
                yield return null;
                Assert.That(entry.GetComponentInChildren<WhitewaterPrelude>(true), Is.Null,
                    "Gamepad B during the walk must return to entry.");
                Assert.That(SoundwellInputAdapter.IsEntryContextActive, Is.True);
                Assert.That(events.currentSelectedGameObject, Is.SameAs(prologueButton.gameObject));
                input.Press(gamepad.buttonSouth);
                yield return null;
                yield return null;
                input.Release(gamepad.buttonSouth);
                yield return null;
                prelude = entry.GetComponentInChildren<WhitewaterPrelude>(true);
                Assert.That(prelude, Is.Not.Null, "S00 must be replayable after cancel.");
                answer = prelude.GetComponentsInChildren<Button>(true)
                    .Single(button => button.gameObject.name == "S00 choice 0");
                FinishRollCallAndWrenchQuestion(prelude);
                yield return null;
                Assert.That(prelude.Phase, Is.EqualTo(1));

                // Move using the existing Gameplay InputAction's keyboard
                // binding; this is not a teleport or synthetic progress flag.
                Keyboard walker = InputSystem.AddDevice<Keyboard>();
                input.Press(walker.dKey);
                float reachDeadline = Time.realtimeSinceStartup + 6f;
                while (!prelude.CanRepair && Time.realtimeSinceStartup < reachDeadline)
                    yield return null;
                input.Release(walker.dKey);
                yield return null;
                Assert.That(prelude.CanRepair, Is.True,
                    "Walking to Shen Li's loose clamp must enable the real repair choice.");
                input.Press(walker.eKey);
                yield return null;
                input.Release(walker.eKey);
                yield return null;
                Assert.That(prelude.Phase, Is.EqualTo(1),
                    "The first knock cannot already open the repair choices.");
                input.Press(walker.eKey);
                yield return null;
                input.Release(walker.eKey);
                yield return null;
                Assert.That(prelude.Phase, Is.EqualTo(2));
                Assert.That(SoundwellInputAdapter.IsUiContextActive, Is.True);
                answer.onClick.Invoke();
                yield return null;
                Assert.That(prelude.Phase, Is.EqualTo(3),
                    "Fixing the cable must advance the playable scene.");
                var crossing = prelude.transform.Find("S00 Whitewater playable canvas/S00 rescue stretcher");
                Assert.That(crossing, Is.Not.Null);
                // Crossing is drawn on whole pixels. On the first rendered frame
                // its unscaled step can round to zero even though the coroutine
                // has started; wait for a visible pixel, not a fixed frame count.
                // The bounded assertion still fails if the repair never moves it.
                float firstVisibleCrossingDeadline = Time.realtimeSinceStartup + 4f;
                while (((RectTransform)crossing).anchoredPosition.x <= -320f &&
                       Time.realtimeSinceStartup < firstVisibleCrossingDeadline)
                    yield return null;
                Assert.That(((RectTransform)crossing).anchoredPosition.x, Is.GreaterThan(-320f),
                    "The stretcher must visibly start crossing only after the repair.");

                // S00-02 must be accessible only AFTER the actual cart crossing,
                // then record a distinct workshop reply without beginning ORK.
                var workshop = prelude.GetComponentsInChildren<Button>(true)
                    .Single(button => button.gameObject.name == "S00 continue to workshop");
                float crossingDeadline = Time.realtimeSinceStartup + 4f;
                while (!workshop.gameObject.activeInHierarchy &&
                       Time.realtimeSinceStartup < crossingDeadline)
                    yield return null;
                Assert.That(workshop.gameObject.activeInHierarchy, Is.True,
                    "Workshop dialogue must unlock when the repaired bridge carries the stretcher.");
                Assert.That(prelude.Phase, Is.EqualTo(3));
                workshop.onClick.Invoke();
                yield return null;
                Assert.That(prelude.Phase, Is.EqualTo(4),
                    "The workshop promise must be a real follow-up interaction.");
                Assert.That(SoundwellInputAdapter.IsUiContextActive, Is.True);
                var jokingReply = prelude.GetComponentsInChildren<Button>(true)
                    .Single(button => button.gameObject.name == "S00 choice 2");
                Assert.That(jokingReply.gameObject.activeInHierarchy, Is.True);
                jokingReply.onClick.Invoke();
                yield return null;
                Assert.That(prelude.Phase, Is.EqualTo(5));
                Assert.That(prelude.CurrentInstructions, Does.Not.Contain("S00-02"),
                    "Internal prototype chapter numbers must not appear in player instructions.");
                Assert.That(prelude.CurrentNarration, Does.Contain(
                    SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese
                        ? "嘴贫的人，账上记两笔" : "A smart mouth goes on the bill twice"),
                    "The third v1.2 workshop choice must receive Shen Li's authored two-bill-marks reply.");

                // S00-03: selecting a rescuer never teleports Han Qiao onto
                // the boat. A real Gameplay action at the matching station
                // must follow the authored choice before he starts moving.
                var lastBoat = prelude.GetComponentsInChildren<Button>(true)
                    .Single(button => button.gameObject.name == "S00 continue to last boat");
                Assert.That(lastBoat.gameObject.activeInHierarchy, Is.True);
                var nextCaption = lastBoat.GetComponentInChildren<Text>();
                Assert.That(nextCaption, Is.Not.Null);
                Assert.That(nextCaption.text, Is.EqualTo(
                    SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese
                        ? "去最后一条船救人" : "Help at the last boat"),
                    "The next-scene control must advertise the last boat, not the previous workshop.");
                lastBoat.onClick.Invoke();
                yield return null;
                Assert.That(prelude.Phase, Is.EqualTo(6));
                var inRiver = prelude.transform.Find(
                    "S00 Whitewater playable canvas/S00 Han Qiao in water") as RectTransform;
                Assert.That(inRiver, Is.Not.Null);
                Assert.That(inRiver.gameObject.activeInHierarchy, Is.True);
                float inRiverX = inRiver.anchoredPosition.x;
                var shenLiRescue = prelude.GetComponentsInChildren<Button>(true)
                    .Single(button => button.gameObject.name == "S00 choice 1");
                shenLiRescue.onClick.Invoke();
                yield return null;
                Assert.That(prelude.Phase, Is.EqualTo(7));
                Assert.That(inRiver.anchoredPosition.x, Is.EqualTo(inRiverX),
                    "Choosing a method cannot fake Han Qiao reaching the boat.");
                Assert.That(prelude.CanRescue, Is.True,
                    "The player who repaired the cable is beside Shen Li's real rescue station.");
                Assert.That(SoundwellInputAdapter.IsGameplayContextActive, Is.True);
                input.Press(walker.eKey);
                yield return null;
                input.Release(walker.eKey);
                yield return null;
                Assert.That(prelude.Phase, Is.EqualTo(8));
                Assert.That(SoundwellInputAdapter.IsUiContextActive, Is.True);
                yield return null;
                Assert.That(inRiver.anchoredPosition.x, Is.GreaterThan(inRiverX),
                    "The rescued officer must visibly move towards the last boat after the action.");
                Assert.That(prelude.CurrentNarration, Does.Contain(
                    SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese
                        ? "沈砾的绳索" : "Shen Li's rope"));

                // A new S00-04 escort must be earned through actual
                // Gameplay input, not granted by the dialogue or a timer.
                var retreat = prelude.GetComponentsInChildren<Button>(true)
                    .Single(button => button.gameObject.name == "S00 continue to final retreat" ||
                        button.gameObject.name == "S00 continue to last boat");
                float boatDeadline = Time.realtimeSinceStartup + 4f;
                while (!retreat.gameObject.activeInHierarchy &&
                       Time.realtimeSinceStartup < boatDeadline)
                    yield return null;
                Assert.That(retreat.gameObject.activeInHierarchy, Is.True,
                    "A player can begin the final retreat only after the last boat has boarded Han Qiao.");
                Assert.That(retreat.gameObject.name, Is.EqualTo("S00 continue to final retreat"));
                retreat.onClick.Invoke();
                yield return null;
                Assert.That(prelude.Phase, Is.EqualTo(9));
                Assert.That(SoundwellInputAdapter.IsGameplayContextActive, Is.True);
                var stranded = prelude.transform.Find(
                    "S00 Whitewater playable canvas/S00 last evacuee") as RectTransform;
                Assert.That(stranded, Is.Not.Null);
                Assert.That(stranded.gameObject.activeInHierarchy, Is.True);
                float strandedX = stranded.anchoredPosition.x;
                Assert.That(prelude.CanHelpEvacuee, Is.False,
                    "The player must go to the stranded person before helping.");
                input.Press(walker.aKey);
                float approachDeadline = Time.realtimeSinceStartup + 6f;
                while (!prelude.CanHelpEvacuee &&
                       Time.realtimeSinceStartup < approachDeadline)
                    yield return null;
                input.Release(walker.aKey);
                yield return null;
                Assert.That(prelude.CanHelpEvacuee, Is.True);
                Assert.That(stranded.anchoredPosition.x, Is.EqualTo(strandedX),
                    "The injured person cannot follow before the player helps.");
                input.Press(walker.eKey);
                yield return null;
                input.Release(walker.eKey);
                yield return null;
                Assert.That(prelude.Phase, Is.EqualTo(10));
                Assert.That(prelude.CanDeliverEvacuee, Is.False,
                    "Agreeing to escort is not already a successful crossing.");
                // Real keyboard movement: backtrack toward the eastern span
                // before the westward rescue. The injured follower must move
                // with the player in both directions, not freeze until a retry.
                float escortStartX = stranded.anchoredPosition.x;
                input.Press(walker.dKey);
                float backtrackDeadline = Time.realtimeSinceStartup + 2f;
                while (prelude.WalkerPosition.x < escortStartX + 70f &&
                       Time.realtimeSinceStartup < backtrackDeadline)
                    yield return null;
                input.Release(walker.dKey);
                yield return null;
                Assert.That(prelude.WalkerPosition.x, Is.GreaterThan(escortStartX + 60f));
                Assert.That(stranded.anchoredPosition.x, Is.GreaterThan(escortStartX + 20f),
                    "Backtracking must bring the wounded follower east without teleporting.");
                Assert.That(prelude.Phase, Is.EqualTo(10),
                    "Changing direction cannot falsely deliver the evacuee.");
                Assert.That(prelude.CanDeliverEvacuee, Is.False);
                input.Press(walker.aKey);
                float escortDeadline = Time.realtimeSinceStartup + 7f;
                while (!prelude.CanDeliverEvacuee &&
                       Time.realtimeSinceStartup < escortDeadline)
                    yield return null;
                input.Release(walker.aKey);
                yield return null;
                Assert.That(prelude.CanDeliverEvacuee, Is.True,
                    "Both player and follower must physically reach the safe line.");
                Assert.That(stranded.anchoredPosition.x, Is.LessThan(strandedX - 120f),
                    "The evacuee must really walk west instead of teleporting.");
                // A stopped follower may remain on the edge of the green line
                // while the player has moved farther west. The scripted waiting
                // state cannot be mistaken for a completed physical escort.
                var waitingField = typeof(WhitewaterPrelude).GetField("evacueeWaiting",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
                Assert.That(waitingField, Is.Not.Null);
                bool originalWaiting = (bool)waitingField.GetValue(prelude);
                try
                {
                    waitingField.SetValue(prelude, true);
                    Assert.That(prelude.CanDeliverEvacuee, Is.False,
                        "An evacuee still waiting for an escort must not be delivered.");
                    Assert.That(prelude.Phase, Is.EqualTo(10));
                }
                finally
                {
                    waitingField.SetValue(prelude, originalWaiting);
                }
                Assert.That(prelude.CanDeliverEvacuee, Is.True,
                    "Rejoining the wounded follower must restore the normal safe-line action.");
                input.Press(walker.eKey);
                yield return null;
                input.Release(walker.eKey);
                yield return null;
                Assert.That(prelude.Phase, Is.EqualTo(9),
                    "The first physical safe-line delivery cannot advance to the bell.");
                Assert.That(prelude.EvacueesRescued, Is.EqualTo(1));
                Assert.That(SoundwellInputAdapter.IsGameplayContextActive, Is.True);
                for (int rescued = 1; rescued < 3; rescued++)
                {
                    Assert.That(prelude.CanHelpEvacuee, Is.False);
                    input.Press(walker.dKey);
                    float nextContactDeadline = Time.realtimeSinceStartup + 6f;
                    while (!prelude.CanHelpEvacuee &&
                           Time.realtimeSinceStartup < nextContactDeadline)
                        yield return null;
                    input.Release(walker.dKey);
                    yield return null;
                    Assert.That(prelude.CanHelpEvacuee, Is.True,
                        "A separate gameplay approach must be earned for each survivor.");
                    input.Press(walker.eKey);
                    yield return null;
                    input.Release(walker.eKey);
                    yield return null;
                    Assert.That(prelude.Phase, Is.EqualTo(10));
                    input.Press(walker.aKey);
                    float nextSafeDeadline = Time.realtimeSinceStartup + 7f;
                    while (!prelude.CanDeliverEvacuee &&
                           Time.realtimeSinceStartup < nextSafeDeadline)
                        yield return null;
                    input.Release(walker.aKey);
                    yield return null;
                    Assert.That(prelude.CanDeliverEvacuee, Is.True);
                    input.Press(walker.eKey);
                    yield return null;
                    input.Release(walker.eKey);
                    yield return null;
                    Assert.That(prelude.EvacueesRescued, Is.EqualTo(rescued + 1));
                    Assert.That(prelude.Phase, Is.EqualTo(rescued == 1 ? 9 : 11));
                }
                Assert.That(SoundwellInputAdapter.IsUiContextActive, Is.True);
                Assert.That(prelude.CurrentNarration, Does.Contain(
                    SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese
                        ? "安全过桥" : "All three are across"));

                // The rope call is an earned, optional follow-up, not a timed
                // false win or a substitute for the pending ORK defence.
                var bellAudio = prelude.GetComponentInChildren<AudioSource>(true);
                Assert.That(bellAudio, Is.Not.Null);
                Assert.That(bellAudio.playOnAwake, Is.False);
                Assert.That(bellAudio.clip, Is.Null,
                    "The first bell must not sound before the earned rope call.");
                Assert.That(bellAudio.outputAudioMixerGroup, Is.Not.Null,
                    "The story cue must share the existing M6 SFX output.");
                Assert.That(bellAudio.outputAudioMixerGroup.name, Is.EqualTo("SFX"));
                var rope = prelude.transform.Find(
                    "S00 Whitewater playable canvas/S00 spare rope at clamp");
                var bridgePost = prelude.transform.Find(
                    "S00 Whitewater playable canvas/S00 bridge defence post") as RectTransform;
                Assert.That(bridgePost, Is.Not.Null);
                Assert.That(bridgePost.gameObject.activeInHierarchy, Is.False,
                    "No battle position may be awarded before the first warning bell.");
                var shenLi = prelude.transform.Find(
                    "S00 Whitewater playable canvas/S00 Shen Li") as RectTransform;
                Assert.That(shenLi, Is.Not.Null);
                Assert.That(bridgePost.anchoredPosition.x, Is.GreaterThan(175f),
                    "The guard position must be on the bridge's eastern clamp side.");
                Assert.That(Mathf.Abs(bridgePost.anchoredPosition.x - shenLi.anchoredPosition.x),
                    Is.LessThanOrEqualTo(36f),
                    "The bridge post must actually be beside Shen Li as the story states.");
                Assert.That(rope, Is.Not.Null);
                Assert.That(rope.gameObject.activeInHierarchy, Is.False,
                    "No rope response before the player reaches the safe line.");
                var callForRope = prelude.GetComponentsInChildren<Button>(true)
                    .Single(button => button.gameObject.name == "S00 call for spare rope");
                Assert.That(callForRope.gameObject.activeInHierarchy, Is.True);
                callForRope.onClick.Invoke();
                yield return null;
                Assert.That(prelude.Phase, Is.EqualTo(12));
                Assert.That(bellAudio.clip, Is.Not.Null,
                    "Player-initiated rope call must schedule a real warning bell clip.");
                Assert.That(bellAudio.clip.name, Does.Contain("first warning bell"));
                Assert.That(bellAudio.clip.channels, Is.EqualTo(1));
                Assert.That(bellAudio.clip.frequency, Is.EqualTo(22050));
                float[] firstBellSamples = new float[bellAudio.clip.samples];
                Assert.That(bellAudio.clip.GetData(firstBellSamples, 0), Is.True);
                Assert.That(firstBellSamples.Any(sample => Mathf.Abs(sample) > 0.01f),
                    Is.True, "The generated first-bell audio cannot be silent.");
                Assert.That(rope.gameObject.activeInHierarchy, Is.True,
                    "The spare coil must appear only after the player calls out.");
                var movingRope = (RectTransform)rope;
                Assert.That(movingRope.anchoredPosition.x, Is.LessThan(-200f),
                    "The coil must visibly approach from the far side rather than teleport to Shen Li.");
                Assert.That(callForRope.gameObject.activeInHierarchy, Is.False,
                    "The player cannot issue the same rope call twice.");
                Assert.That(prelude.CurrentNarration, Does.Contain(
                    SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese
                        ? "第一声号钟" : "first bell"));
                Assert.That(bridgePost.gameObject.activeInHierarchy, Is.True);
                Assert.That(SoundwellInputAdapter.IsGameplayContextActive, Is.True,
                    "The first bell returns the player to movement for the bridge post.");
                Assert.That(prelude.CanTakeBridgePost, Is.False,
                    "The player is still west of the post after the real evacuation.");
                float oldWalkerX = prelude.WalkerPosition.x;
                yield return null;
                Assert.That(prelude.Phase, Is.EqualTo(12),
                    "No automatic defence credit may be granted while waiting.");
                input.Press(walker.dKey);
                float postDeadline = Time.realtimeSinceStartup + 6f;
                while (!prelude.CanTakeBridgePost && Time.realtimeSinceStartup < postDeadline)
                    yield return null;
                input.Release(walker.dKey);
                yield return null;
                Assert.That(prelude.CanTakeBridgePost, Is.True,
                    "The player must physically reach the amber bridge post.");
                Assert.That(movingRope.anchoredPosition.x, Is.EqualTo(208f).Within(0.1f),
                    "The delivered rope must physically reach the saved clamp position.");
                Assert.That(prelude.Phase, Is.EqualTo(12),
                    "A completed visual rope handoff must never count as an ORK defence.");
                Assert.That(prelude.WalkerPosition.x, Is.GreaterThan(oldWalkerX + 150f));
                Assert.That(prelude.Phase, Is.EqualTo(12),
                    "Walking into range alone cannot grant the guard-post interaction.");
                input.Press(walker.eKey);
                yield return null;
                input.Release(walker.eKey);
                yield return null;
                Assert.That(prelude.Phase, Is.EqualTo(13));
                Assert.That(SoundwellInputAdapter.IsUiContextActive, Is.True);
                Assert.That(bridgePost.GetComponent<Image>().color.g, Is.GreaterThan(0.8f),
                    "The held post must visibly change after the accepted interaction.");
                Assert.That(prelude.CurrentNarration, Does.Contain(
                    SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese
                        ? "桥头守位" : "bridge post"));
                Assert.That(bellAudio.clip.name, Does.Contain("first warning bell"),
                    "Reaching the post must not invent the pending second/third-bell events.");
                var farLamps = new[]
                {
                    prelude.transform.Find("S00 Whitewater playable canvas/S00 far bank lantern left"),
                    prelude.transform.Find("S00 Whitewater playable canvas/S00 far bank lantern right")
                };
                Assert.That(farLamps.All(lamp => lamp != null && lamp.gameObject.activeInHierarchy),
                    Is.True, "The player must actually see the opposite-bank lamps before they go out.");
                var watchBank = prelude.GetComponentsInChildren<Button>(true)
                    .Single(button => button.gameObject.name == "S00 watch opposite bank");
                Assert.That(watchBank.gameObject.activeInHierarchy, Is.True);
                Assert.That(prelude.Phase, Is.EqualTo(13),
                    "Waiting at the guard post must not silently trigger the second bell.");
                Assert.That(bellAudio.clip.name, Does.Contain("first warning bell"));
                watchBank.onClick.Invoke();
                Assert.That(prelude.Phase, Is.EqualTo(14));
                Assert.That(farLamps[0].gameObject.activeInHierarchy, Is.False,
                    "The first opposite-bank lamp must go dark at the second bell.");
                Assert.That(farLamps[1].gameObject.activeInHierarchy, Is.True,
                    "The other lamp must remain visible for a separate retreat beat.");
                yield return null;
                Assert.That(bellAudio.clip.name, Does.Contain("second warning bell"),
                    "The second bell must have its own real routed audio clip.");
                float[] secondBellSamples = new float[bellAudio.clip.samples];
                Assert.That(bellAudio.clip.GetData(secondBellSamples, 0), Is.True);
                Assert.That(secondBellSamples.Any(sample => Mathf.Abs(sample) > 0.01f), Is.True);
                // This isolated input test intentionally does not bootstrap
                // Makinom's game: the same S00 scene is safe without it.
                // Validate its live story variable only when Makinom owns one;
                // the initialized Bootstrap/ORK integration remains covered
                // separately by the end-to-end Soundwell suite.
                if (Maki.Initialized && Maki.Game?.Variables != null)
                    Assert.That(Maki.Game.Variables.GetInt(WhitewaterPrelude.SecondBellKey),
                        Is.EqualTo(1));
                Assert.That(prelude.CurrentNarration, Does.Contain(
                    SoundwellUiLocalization.Language == SoundwellUiLocalization.Chinese
                        ? "第二声号钟" : "second bell"));
                watchBank.onClick.Invoke();
                Assert.That(prelude.Phase, Is.EqualTo(14),
                    "A stale UGUI Submit must not replay the historical second bell.");
                yield return new WaitForSecondsRealtime(0.45f);
                Assert.That(farLamps.All(lamp => !lamp.gameObject.activeInHierarchy), Is.True,
                    "Both lamps must actually go dark in sequence even if gameplay time is paused.");

                var backToEntry = prelude.GetComponentsInChildren<Button>(true)
                    .Single(button => button.gameObject.name == "S00 return to Bootstrap");
                backToEntry.onClick.Invoke();
                yield return null;
                Assert.That(entry.GetComponentInChildren<WhitewaterPrelude>(true), Is.Null,
                    "Returning from the opening must clean up the temporary S00 stage.");
                Assert.That(SoundwellInputAdapter.IsEntryContextActive, Is.True);
                Assert.That(events.currentSelectedGameObject, Is.SameAs(prologueButton.gameObject));
                // Only a real Makinom-owned S00 milestone may alter Entry's
                // replay label. This S00 input test can also run without
                // Makinom; never fabricate the state merely to pass.
                if (Maki.Initialized && Maki.Game?.Variables != null)
                {
                    yield return null;
                    var state = Maki.Game.Variables;
                    string milestone = SoundwellEntrance.PreludeProgressKey(
                        state.GetInt(WhitewaterPrelude.EvacueeEscortedKey) != 0,
                        state.GetInt(WhitewaterPrelude.SecondBellKey) != 0,
                        state.GetInt(WhitewaterPrelude.CableHoldKey) != 0);
                    Assert.That(prologueButton.GetComponentInChildren<Text>().text,
                        Is.EqualTo(SoundwellUiLocalization.Get(milestone)),
                        "Returning from S00 must update the live UGUI button from Makinom.");
                }
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
                if (hadFormationPreference)
                    PlayerPrefs.SetInt(OrkPartyBridge.FixedFormationPreferenceKey,
                        previousFormationPreference);
                else
                    PlayerPrefs.DeleteKey(OrkPartyBridge.FixedFormationPreferenceKey);
                PlayerPrefs.Save();
                SoundwellInputAdapter.SetUiContext();
                input.TearDown();
            }

            yield return new ExitPlayMode();
        }
    }
}
