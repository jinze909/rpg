using System.Collections;
using System.IO;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.InputSystem;
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
                input.Release(keyboard.f2Key);
                yield return null;

                input.Press(gamepad.buttonSouth);
                yield return null;
                Assert.That(SoundwellInputAdapter.InteractPressed(), Is.True,
                    "Gamepad South must activate the real Entry action.");
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
            }
            finally
            {
                SoundwellInputAdapter.SetUiContext();
                input.TearDown();
            }

            yield return new ExitPlayMode();
        }
    }
}
