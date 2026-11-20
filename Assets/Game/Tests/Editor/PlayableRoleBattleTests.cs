using System;
using System.Collections;
using System.IO;
using System.Linq;
using GamingIsLove.Makinom;
using GamingIsLove.ORKFramework;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rpg.Tests.Editor
{
    public sealed class PlayableRoleBattleTests
    {
        [UnityTest, Timeout(240000)]
        public IEnumerator VisibleEntryPlaysRealInterruptAndPunishExchange()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return RunVisibleEntryExchange();
            yield return new ExitPlayMode();
        }

        private static IEnumerator RunVisibleEntryExchange()
        {
            yield return Wait(() => Maki.Initialized && ORK.Initialized, "framework startup");
            var entry = UnityEngine.Object.FindFirstObjectByType<SoundwellEntrance>();
            Assert.That(entry, Is.Not.Null);
            var button = entry.GetComponentsInChildren<UnityEngine.UI.Button>()
                .Single(value => value.name == "Enter Soundwell button");
            button.onClick.Invoke();
            OrkRoleEncounter encounter = null;
            yield return Wait(() =>
            {
                encounter = UnityEngine.Object.FindFirstObjectByType<OrkRoleEncounter>();
                return encounter != null && encounter.IsActive && encounter.Party != null &&
                    encounter.Party.Beast != null && encounter.Party.Members != null &&
                    encounter.Party.Members.Count == 3 && encounter.View != null;
            }, "the visible entry must start a fully initialized ORK role encounter");
            Assert.That(encounter, Is.Not.Null, "The visible entry must retain the live encounter.");
            Assert.That(SoundwellEntrance.RoleEncounterRequested, Is.False,
                "The selected encounter mode must be consumed at scene load.");
            Assert.That(encounter.Party.Members.Count, Is.EqualTo(3));
            Assert.That(encounter.Party.Beast.Status[1].GetValue(), Is.EqualTo(36));
            Assert.That(encounter.Party.Members.All(member => member.Status[1].GetValue() == 16), Is.True);
            Capture(encounter, "role-01-queued-threat");
            int[] hpBeforeGather = encounter.Party.Members
                .Select(member => member.Status[1].GetValue()).ToArray();

            // ORK's first enemy action winds up. Player commands are sent
            // through the same buttons used by keyboard/gamepad Submit.
            yield return PlayUntil(encounter,
                () => encounter.PendingThreat == OrkRoleEncounter.Threat.Lunge,
                interrupt: true);
            Assert.That(encounter.Party.Members.Select(member => member.Status[1].GetValue()),
                Is.EqualTo(hpBeforeGather),
                "The completed ORK gather action must not damage the party.");
            int[] beforeLunge = encounter.Party.Members.Select(member => member.Status[1].GetValue()).ToArray();
            yield return Wait(() => encounter.Party.ChoosingRescuerIndex >= 0 && !encounter.IsPresenting,
                "player response window");
            yield return PlayUntil(encounter,
                () => encounter.PendingThreat == OrkRoleEncounter.Threat.Howl,
                interrupt: true);
            Assert.That(encounter.Party.Members.Select(member => member.Status[1].GetValue()),
                Is.EqualTo(beforeLunge), "The cancelled ORK lunge must deal no damage.");
            Assert.That(encounter.IsExposed, Is.True, "A real interrupted action leaves a punish window.");
            Capture(encounter, "role-02-interrupted-lunge");

            int enemyBefore = encounter.Party.Beast.Status[1].GetValue();
            yield return PlayUntil(encounter,
                () => encounter.PendingThreat == OrkRoleEncounter.Threat.Lunge,
                interrupt: true);
            Assert.That(enemyBefore - encounter.Party.Beast.Status[1].GetValue(), Is.GreaterThanOrEqualTo(10),
                "The breaker must exploit the persisted ORK combatant opening through a real ability.");

            yield return PlayUntil(encounter, () => encounter.Party.Beast.Status.IsDead, interrupt: true);
            yield return Wait(() => !encounter.Party.BattleInProgress && !encounter.IsPresenting, "ORK victory");
            Capture(encounter, "role-03-rescue-victory");
            encounter.View.PrimaryButton.onClick.Invoke();
            yield return Wait(() => SceneManager.GetActiveScene().name == SoundwellEntrance.BootstrapSceneName,
                "rescue return");
            Assert.That(OrkPartyBridge.RescueSettled, Is.True);
            Assert.That(Maki.Game.Variables.GetInt(SoundwellSignalKeys.RescueKey), Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator PlayUntil(OrkRoleEncounter encounter, Func<bool> completed, bool interrupt)
        {
            float deadline = Time.realtimeSinceStartup + 60;
            while (!completed())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline),
                    "Role battle stalled. Actor=" + encounter.Party.ChoosingRescuerIndex +
                    ", threat=" + encounter.PendingThreat + ", busy=" + encounter.IsPresenting +
                    ", beastHp=" + encounter.Party.Beast.Status[1].GetValue() +
                    ", exposed=" + encounter.IsExposed);
                int actor = encounter.Party.ChoosingRescuerIndex;
                if (!encounter.IsPresenting && actor >= 0)
                {
                    bool useSecond = actor == 1 && interrupt && encounter.CanChoose(1) ||
                        actor == 2 && !encounter.CanChoose(0);
                    var command = useSecond ? encounter.View.SecondaryButton : encounter.View.PrimaryButton;
                    if (command.interactable)
                    {
                        command.onClick.Invoke();
                        Assert.That(encounter.Choose(0), Is.False, "A second same-frame command must be rejected.");
                    }
                }
                yield return null;
            }
        }

        private static IEnumerator Wait(Func<bool> condition, string stage)
        {
            float deadline = Time.realtimeSinceStartup + 30;
            while (!condition())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), stage);
                yield return null;
            }
        }

        private static void Capture(OrkRoleEncounter encounter, string name)
        {
            const int width = 960, height = 540;
            Camera camera = Camera.main;
            var target = RenderTexture.GetTemporary(width, height, 24);
            var previous = camera.targetTexture;
            var active = RenderTexture.active;
            var uiObject = new GameObject("Role evidence camera", typeof(Camera));
            var uiCamera = uiObject.GetComponent<Camera>();
            var hud = encounter.View.Canvas;
            var frame = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                uiCamera.targetTexture = target;
                uiCamera.clearFlags = CameraClearFlags.Depth;
                uiCamera.cullingMask = 1 << 5;
                hud.renderMode = RenderMode.ScreenSpaceCamera;
                hud.worldCamera = uiCamera;
                hud.planeDistance = 1;
                Canvas.ForceUpdateCanvases();
                uiCamera.Render();
                RenderTexture.active = target;
                frame.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                frame.Apply();
                const string folder = "artifacts/p0/soundwell";
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), frame.EncodeToPNG());
            }
            finally
            {
                hud.renderMode = RenderMode.ScreenSpaceOverlay;
                hud.worldCamera = null;
                camera.targetTexture = previous;
                RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(frame);
                UnityEngine.Object.DestroyImmediate(uiObject);
            }
        }

        [UnityTearDown]
        public IEnumerator LeavePlayModeAfterFailure()
        {
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
        }
    }
}
