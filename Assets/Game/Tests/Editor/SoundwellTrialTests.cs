using System.Collections;
using System.Linq;
using GamingIsLove.Makinom;
using GamingIsLove.ORKFramework;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

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

        [UnityTest]
        public IEnumerator ReturnScreenKeepsRescueOutcomeLocalizedAcrossLanguageChanges()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);
            double deadline = EditorApplication.timeSinceStartup + 30;
            while ((!Maki.Initialized || !ORK.Initialized) &&
                   EditorApplication.timeSinceStartup < deadline)
                yield return null;
            Assert.That(Maki.Initialized && ORK.Initialized, Is.True);

            var entry = Object.FindFirstObjectByType<SoundwellEntrance>();
            Assert.That(entry, Is.Not.Null, "The saved Bootstrap entry must be present.");
            var variables = Maki.Game.Variables;
            Assert.That(variables, Is.Not.Null);
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            variables.Set(SoundwellTrial.EntryOutcomeKey, 2);
            variables.Set(SoundwellTrial.EntryCartKitKey, 1);
            variables.Set(SoundwellTrial.EntrySafeRouteKey, 1);
            variables.Set(SoundwellTrial.EntryBeastContactsKey, 0);
            entry.RefreshReturnOutcome();
            Assert.That(entry.ReturnSummaryText, Does.Contain("SURVEYOR SAFE"));
            Assert.That(entry.ReturnSummaryText, Does.Contain("MAP KIT RECOVERED"));
            Assert.That(entry.ReturnSummaryText, Does.Contain("SAFE ROUTE POSTED"));

            // A language toggle must refresh the existing summary immediately,
            // even when Makinom's outcome variables have not changed.
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
            Assert.That(entry.ReturnSummaryText, Does.Contain("测路人获救"));
            Assert.That(entry.ReturnSummaryText, Does.Contain("地图工具已取回"));
            Assert.That(entry.ReturnSummaryText, Does.Contain("安全返程路线已标记"));
            Assert.That(entry.GetComponentsInChildren<Text>(true).Any(label =>
                label.text.Contains("进入试炼") &&
                label.font.HasCharacter('中')), Is.True,
                "The entry prompt must use the authored Chinese font and translation.");

            variables.Set(SoundwellTrial.EntryBeastContactsKey, 2);
            entry.RefreshReturnOutcome();
            Assert.That(entry.ReturnSummaryText, Does.Contain("遭遇野兽次数：2"));
            variables.Set(SoundwellTrial.EntryOutcomeKey, 0);
            entry.RefreshReturnOutcome();
            Assert.That(entry.ReturnSummaryText, Does.Contain("撤退"));
            Assert.That(entry.ReturnSummaryText, Does.Contain("遭遇野兽次数：2"));
            SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
            Assert.That(entry.ReturnSummaryText, Does.Contain("WITHDREW / BEAST CONTACTS: 2"));
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
            // Bootstrap has its own 30-second watchdog. The full end-to-end
            // play-through now contains an additional real cart inspection,
            // physical rescue, sign correction and map-kit recovery. Give the
            // scenario a separate bounded window; individual interaction
            // waits in the smoke host still fail after 10 seconds.
            deadline = EditorApplication.timeSinceStartup + 90;
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
