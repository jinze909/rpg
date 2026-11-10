using System.IO;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

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

        [Test]
        public void EntryAndGameplayContextsRouteLiveKeyboardAndGamepadEvents()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            try
            {
                SoundwellInputAdapter.SetEntryContext();
                Assert.That(SoundwellInputAdapter.IsEntryContextActive, Is.True);

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F2));
                InputSystem.Update(InputUpdateType.Editor);
                Assert.That(SoundwellInputAdapter.InteractPressed(), Is.True,
                    "F2 must activate the real Entry action.");

                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update(InputUpdateType.Editor);
                InputSystem.QueueStateEvent(gamepad, new GamepadState(GamepadButton.South));
                InputSystem.Update(InputUpdateType.Editor);
                Assert.That(SoundwellInputAdapter.InteractPressed(), Is.True,
                    "Gamepad South must activate the real Entry action.");
                Assert.That(SoundwellInputAdapter.ReadGameplay().Ring, Is.False,
                    "Entry input must not also ring the gameplay bell.");

                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                InputSystem.Update(InputUpdateType.Editor);
                SoundwellInputAdapter.SetGameplayContext();
                Assert.That(SoundwellInputAdapter.IsGameplayContextActive, Is.True);

                InputSystem.QueueStateEvent(gamepad, new GamepadState(GamepadButton.South));
                InputSystem.Update(InputUpdateType.Editor);
                Assert.That(SoundwellInputAdapter.ReadGameplay().Ring, Is.True,
                    "Gamepad South must ring only after the Gameplay map is active.");
                Assert.That(SoundwellInputAdapter.InteractPressed(), Is.False,
                    "Gameplay input must not also activate the Entry action.");

                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                InputSystem.Update(InputUpdateType.Editor);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                InputSystem.Update(InputUpdateType.Editor);
                Assert.That(SoundwellInputAdapter.ReadGameplay().Movement.y, Is.GreaterThan(0.9f),
                    "W must drive the real Gameplay Move action.");

                SoundwellInputAdapter.SetUiContext();
                Assert.That(SoundwellInputAdapter.IsUiContextActive, Is.True);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update(InputUpdateType.Editor);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
                InputSystem.Update(InputUpdateType.Editor);
                Assert.That(SoundwellInputAdapter.ReadGameplay().Movement, Is.EqualTo(Vector2.zero),
                    "UI focus must disable gameplay movement rather than moving behind a focused menu.");
            }
            finally
            {
                SoundwellInputAdapter.SetUiContext();
                InputSystem.RemoveDevice(gamepad);
                InputSystem.RemoveDevice(keyboard);
            }
        }
    }
}
