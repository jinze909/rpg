using System.Collections;
using GamingIsLove.Makinom;
using GamingIsLove.ORKFramework;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rpg.Tests.Editor
{
    public sealed class SoundwellTrialTests
    {
        [UnityTest]
        public IEnumerator PlayerCanRingWrapRetreatAndRetry()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);
            yield return RunTrialInPlayerLoop();
            yield return new ExitPlayMode();
        }

        [UnityTearDown]
        public IEnumerator RestoreEditMode()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();
        }

        private static IEnumerator RunTrialInPlayerLoop()
        {
            double deadline = EditorApplication.timeSinceStartup + 30;
            while ((!Maki.Initialized || !ORK.Initialized) && EditorApplication.timeSinceStartup < deadline)
                yield return null;

            Assert.That(Maki.Initialized && ORK.Initialized, Is.True, "The saved Bootstrap project must start.");
            var root = new GameObject("Soundwell CI runner");
            Object.DontDestroyOnLoad(root);
            var host = root.AddComponent<SoundwellSmokeHost>();
            LogAssert.Expect(UnityEngine.LogType.Log, "RPG_SOUNDWELL_SIGNAL_VALIDATED");
            host.Run();
            while (!host.ExitCode.HasValue && EditorApplication.timeSinceStartup < deadline)
                yield return null;
            Assert.That(host.ExitCode, Is.EqualTo(0), "The real Play Mode trial must complete.");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
