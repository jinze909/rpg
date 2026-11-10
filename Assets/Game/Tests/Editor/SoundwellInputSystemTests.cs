using System.IO;
using NUnit.Framework;
using Rpg.Soundwell;

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
    }
}
