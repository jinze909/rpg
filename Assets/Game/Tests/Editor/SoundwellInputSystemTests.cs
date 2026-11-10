using System.IO;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.InputSystem;

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

                InputSystem.QueueDeltaStateEvent(keyboard.f2Key, 1f);
                InputSystem.Update();
                Assert.That(SoundwellInputAdapter.InteractPressed(), Is.True,
                    "F2 must activate the real Entry action.");

                InputSystem.QueueDeltaStateEvent(keyboard.f2Key, 0f);
                InputSystem.Update();
                InputSystem.QueueDeltaStateEvent(gamepad.buttonSouth, 1f);
                InputSystem.Update();
                Assert.That(SoundwellInputAdapter.InteractPressed(), Is.True,
                    "Gamepad South must activate the real Entry action.");
                Assert.That(SoundwellInputAdapter.ReadGameplay().Ring, Is.False,
                    "Entry input must not also ring the gameplay bell.");

                InputSystem.QueueDeltaStateEvent(gamepad.buttonSouth, 0f);
                InputSystem.Update();
                SoundwellInputAdapter.SetGameplayContext();
                Assert.That(SoundwellInputAdapter.IsGameplayContextActive, Is.True);

                InputSystem.QueueDeltaStateEvent(gamepad.buttonSouth, 1f);
                InputSystem.Update();
                Assert.That(SoundwellInputAdapter.ReadGameplay().Ring, Is.True,
                    "Gamepad South must ring only after the Gameplay map is active.");
                Assert.That(SoundwellInputAdapter.InteractPressed(), Is.False,
                    "Gameplay input must not also activate the Entry action.");

                InputSystem.QueueDeltaStateEvent(gamepad.buttonSouth, 0f);
                InputSystem.Update();
                InputSystem.QueueDeltaStateEvent(keyboard.wKey, 1f);
                InputSystem.Update();
                Assert.That(SoundwellInputAdapter.ReadGameplay().Movement.y, Is.GreaterThan(0.9f),
                    "W must drive the real Gameplay Move action.");

                SoundwellInputAdapter.SetUiContext();
                Assert.That(SoundwellInputAdapter.IsUiContextActive, Is.True);
                InputSystem.QueueDeltaStateEvent(keyboard.wKey, 0f);
                InputSystem.Update();
                InputSystem.QueueDeltaStateEvent(keyboard.sKey, 1f);
                InputSystem.Update();
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
