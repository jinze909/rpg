using System.Collections;
using System.IO;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
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
                Assert.That(SoundwellInputAdapter.ReadGameplay().Ring, Is.True,
                    "Gamepad South must ring only after the Gameplay map is active.");
                Assert.That(SoundwellInputAdapter.InteractPressed(), Is.False,
                    "Gameplay input must not also activate the Entry action.");
                input.Release(gamepad.buttonSouth);

                input.Press(keyboard.wKey);
                yield return null;
                Assert.That(SoundwellInputAdapter.ReadGameplay().Movement.y, Is.GreaterThan(0.9f),
                    "W must drive the real Gameplay Move action.");

                SoundwellInputAdapter.SetUiContext();
                input.Release(keyboard.wKey);
                yield return null;
                input.Press(keyboard.sKey);
                yield return null;
                Assert.That(SoundwellInputAdapter.ReadGameplay().Movement, Is.EqualTo(Vector2.zero),
                    "UI focus must disable gameplay movement rather than moving behind a focused menu.");
                input.Release(keyboard.sKey);
                yield return null;
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
