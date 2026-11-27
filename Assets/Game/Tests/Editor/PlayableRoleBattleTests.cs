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

        [UnityTest, Timeout(240000)]
        public IEnumerator VisibleEntryShowsAndResolvesUninterruptedLunge()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return RunUninterruptedLunge();
            yield return new ExitPlayMode();
        }

        private static IEnumerator OpenVisibleRoleEncounter(Action<OrkRoleEncounter> receive)
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
            receive(encounter);
        }

        private static IEnumerator RunVisibleEntryExchange()
        {
            OrkRoleEncounter encounter = null;
            yield return OpenVisibleRoleEncounter(value => encounter = value);
            yield return Wait(() => !string.IsNullOrEmpty(encounter.View.ThreatLabel.text),
                "initialize the role HUD before capturing the live arena");
            var art = Resources.Load<AdventureArt>("AdventureArt");
            Assert.That(art, Is.Not.Null);
            AssertBeastFrame(art.BeastLunge, "sound_borrowing_beast_lunge");
            var beastRenderer = encounter.Party.Beast.GameObject.GetComponentsInChildren<SpriteRenderer>()
                .Single(renderer => renderer.gameObject.name == "Rescue actor visual 3");
            Assert.That(beastRenderer.sprite, Is.SameAs(art.Actors[3]));
            Capture(encounter, "role-01-queued-threat", beastRenderer);
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
                "player response window", () =>
                    "Actor=" + encounter.Party.ChoosingRescuerIndex +
                    ", threat=" + encounter.PendingThreat +
                    ", presenting=" + encounter.IsPresenting +
                    ", enemyChooser=" + encounter.EnemyChooserActive +
                    ", threatSubmitted=" + encounter.EnemyThreatSubmitted +
                    ", activePartyChoosers=" + string.Join(",", encounter.Party.Members
                        .Select(member => member.Actions.IsChoosing)) +
                    ", actionStates=" + string.Join(",", encounter.Party.Members
                        .Select(member => member.Actions.ActionState.ToString())) +
                    ", orkInBattle=" + ORK.Control.InBattle +
                    ", participants=" + ORK.Game.Combatants.GetInBattle().Count +
                    ", submittedActions=" + encounter.SubmittedActionCount +
                    ", animations=" + encounter.ActivePresentationCount);
            AssertBeastFrame(art.BeastWindup, "sound_borrowing_beast_windup");
            Assert.That(beastRenderer.sprite, Is.SameAs(art.BeastWindup));
            Capture(encounter, "role-beast-windup", beastRenderer);

            bool capturedInterruption = false;
            AssertBeastFrame(art.BeastStagger, "sound_borrowing_beast_stagger_low");
            yield return PlayUntil(encounter,
                () => encounter.PendingThreat == OrkRoleEncounter.Threat.Howl,
                interrupt: true, observe: () =>
                {
                    if (capturedInterruption || !encounter.IsInterrupted ||
                        encounter.PendingThreat != OrkRoleEncounter.Threat.Lunge ||
                        beastRenderer.sprite != art.BeastStagger)
                        return;
                    Capture(encounter, "role-02-interrupted-lunge", beastRenderer);
                    capturedInterruption = true;
                });
            Assert.That(capturedInterruption, Is.True,
                "The accepted decoy must show its own pose while the actual lunge is interrupted.");
            Assert.That(encounter.Party.Members.Select(member => member.Status[1].GetValue()),
                Is.EqualTo(beforeLunge), "The cancelled ORK lunge must deal no damage.");
            Assert.That(encounter.IsExposed, Is.True, "A real interrupted action leaves a punish window.");

            int enemyBefore = encounter.Party.Beast.Status[1].GetValue();
            AssertBeastFrame(art.BeastRecoil, "sound_borrowing_beast_recoil");
            bool capturedRecoil = false;
            yield return PlayUntil(encounter,
                () => encounter.PendingThreat == OrkRoleEncounter.Threat.Lunge,
                interrupt: true, observe: () =>
                {
                    if (capturedRecoil || encounter.Party.Beast.Status.IsDead ||
                        beastRenderer.sprite != art.BeastRecoil)
                        return;
                    int hp = encounter.Party.Beast.Status[1].GetValue();
                    if (hp <= 0 || hp >= enemyBefore)
                        return;
                    Capture(encounter, "role-beast-nonlethal-recoil", beastRenderer);
                    capturedRecoil = true;
                });
            Assert.That(capturedRecoil, Is.True,
                "An actual nonlethal ORK hit must reach the living beast renderer.");
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

        private static IEnumerator RunUninterruptedLunge()
        {
            OrkRoleEncounter encounter = null;
            yield return OpenVisibleRoleEncounter(value => encounter = value);
            var art = Resources.Load<AdventureArt>("AdventureArt");
            Assert.That(art, Is.Not.Null);
            AssertBeastFrame(art.BeastLunge, "sound_borrowing_beast_lunge");
            var beastRenderer = encounter.Party.Beast.GameObject.GetComponentsInChildren<SpriteRenderer>()
                .Single(renderer => renderer.gameObject.name == "Rescue actor visual 3");

            yield return PlayUntil(encounter,
                () => encounter.PendingThreat == OrkRoleEncounter.Threat.Lunge, interrupt: false);
            yield return Wait(() => !encounter.IsPresenting && encounter.Party.ChoosingRescuerIndex >= 0,
                "wait for the charged lunge response window");
            Assert.That(beastRenderer.sprite, Is.SameAs(art.BeastWindup));

            int target = encounter.TargetIndex;
            var members = encounter.Party.Members;
            int[] before = members.Select(member =>
                member.Status[OrkPartyBridge.HealthStatusIndex].GetValue()).ToArray();
            Transform targetActor = members[target].GameObject.transform;
            Transform visual = beastRenderer.transform;
            Vector3 restingOffset = visual.localPosition;
            float Approach()
            {
                Vector3 home = visual.parent.TransformPoint(restingOffset);
                Vector2 toward = (Vector2)(targetActor.position - home);
                return Vector2.Dot((Vector2)(visual.position - home), toward.normalized);
            }

            // Use the normal commands and let this attack land at full strength.
            // Refresh advances the queued threat to Howl while the lunge is still being drawn.
            yield return PlayUntil(encounter,
                () => encounter.PendingThreat == OrkRoleEncounter.Threat.Howl, interrupt: false);
            yield return Wait(() => encounter.IsPresenting && !encounter.IsInterrupted &&
                beastRenderer.sprite == art.BeastLunge && Approach() > 0.2f,
                "the live beast must use the authored lunge frame while moving toward the original target",
                () => $"pose={beastRenderer.sprite?.name}, towardTarget={Approach():F2}, " +
                    $"presenting={encounter.IsPresenting}, threat={encounter.PendingThreat}, " +
                    $"targetHP={members[target].Status[OrkPartyBridge.HealthStatusIndex].GetValue()}");
            Capture(encounter, "role-beast-uninterrupted-lunge", beastRenderer);

            for (int i = 0; i < members.Count; i++)
            {
                int expected = i == target ? before[i] - 8 : before[i];
                Assert.That(members[i].Status[OrkPartyBridge.HealthStatusIndex].GetValue(),
                    Is.EqualTo(expected), $"ORK health for rescuer {i} after the full lunge.");
            }

            yield return Wait(() => !encounter.IsPresenting && beastRenderer.sprite == art.BeastWindup &&
                encounter.View.RetreatButton.interactable, "restore the queued pose and allow retreat");
            encounter.View.RetreatButton.onClick.Invoke();
            yield return Wait(() => SceneManager.GetActiveScene().name == SoundwellEntrance.BootstrapSceneName,
                "return to Bootstrap after the real lunge");
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator PlayUntil(OrkRoleEncounter encounter, Func<bool> completed,
            bool interrupt, Action observe = null)
        {
            float deadline = Time.realtimeSinceStartup + 60;
            while (!completed())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline),
                    "Role battle stalled. B=" + encounter.Party.Beast.Actions.ActionState +
                    ", P=" + string.Join("/", encounter.Party.Members
                        .Select(member => member.Actions.ActionState.ToString())) +
                    ", threat=" + encounter.PendingThreat +
                    ", hp=" + encounter.Party.Beast.Status[1].GetValue() +
                    ", sub=" + encounter.SubmittedActionCount +
                    ", turn=" + encounter.TurnSubmittedPartyCount +
                    ", perf=" + encounter.PlannedPerformed +
                    ", tSub=" + encounter.EnemyThreatSubmitted +
                    ", eCh=" + encounter.EnemyChooserActive +
                    ", Actor=" + encounter.Party.ChoosingRescuerIndex +
                    ", busy=" + encounter.IsPresenting);
                observe?.Invoke();
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

        private static IEnumerator Wait(Func<bool> condition, string stage,
            Func<string> diagnostic = null)
        {
            float deadline = Time.realtimeSinceStartup + 30;
            while (!condition())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline),
                    stage + (diagnostic != null ? ": " + diagnostic() : string.Empty));
                yield return null;
            }
        }

        private static void AssertBeastFrame(Sprite sprite, string name)
        {
            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.name, Is.EqualTo(name));
            Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(16, 24)));
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(16));
            Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(AssetDatabase.GetAssetPath(sprite),
                Is.EqualTo("Assets/Game/Art/Soundwell/soundwell_rescue_action_frames.png"));
        }

        private static void Capture(OrkRoleEncounter encounter, string name,
            SpriteRenderer visibleBeast = null)
        {
            const int width = 960, height = 540;
            Camera camera = Camera.main;
            Assert.That(camera, Is.Not.Null);
            Rect arena = camera.rect;
            int left = Mathf.Clamp(Mathf.RoundToInt(width * arena.xMin), 0, width);
            int right = Mathf.Clamp(Mathf.RoundToInt(width * arena.xMax), 0, width);
            int bottom = Mathf.Clamp(Mathf.RoundToInt(height * arena.yMin), 0, height);
            int top = Mathf.Clamp(Mathf.RoundToInt(height * arena.yMax), 0, height);
            Assert.That(bottom, Is.GreaterThan(0), "Leave the reserved bottom band to the real HUD.");
            Assert.That(top, Is.LessThan(height), "Leave the reserved top band to the real HUD.");
            int arenaWidth = right - left, arenaHeight = top - bottom;
            Assert.That(arenaWidth, Is.GreaterThan(20));
            Assert.That(arenaHeight, Is.GreaterThan(20));

            var worldTarget = RenderTexture.GetTemporary(arenaWidth, arenaHeight, 24);
            var hudTarget = RenderTexture.GetTemporary(width, height, 24);
            var previous = camera.targetTexture;
            var active = RenderTexture.active;
            var uiObject = new GameObject("Role evidence camera", typeof(Camera));
            var uiCamera = uiObject.GetComponent<Camera>();
            uiCamera.enabled = false;
            var hud = encounter.View.Canvas;
            var previousMode = hud.renderMode;
            var previousCamera = hud.worldCamera;
            float previousPlane = hud.planeDistance;
            var worldFrame = new Texture2D(arenaWidth, arenaHeight, TextureFormat.RGBA32, false);
            var frame = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                // A camera may ignore rect when drawing to a RenderTexture.
                // Give the world target exactly the dimensions of its own band.
                camera.targetTexture = worldTarget;
                camera.rect = new Rect(0f, 0f, 1f, 1f);
                camera.Render();
                if (visibleBeast != null)
                {
                    Assert.That(visibleBeast.enabled && visibleBeast.gameObject.activeInHierarchy, Is.True);
                    Vector3 lower = camera.WorldToViewportPoint(visibleBeast.bounds.min);
                    Vector3 upper = camera.WorldToViewportPoint(visibleBeast.bounds.max);
                    Assert.That(lower.x, Is.GreaterThan(0f));
                    Assert.That(lower.y, Is.GreaterThan(0f));
                    Assert.That(lower.z, Is.GreaterThan(0f));
                    Assert.That(upper.x, Is.LessThan(1f));
                    Assert.That(upper.y, Is.LessThan(1f));
                }

                RenderTexture.active = worldTarget;
                worldFrame.ReadPixels(new Rect(0, 0, arenaWidth, arenaHeight), 0, 0);
                Color32[] world = worldFrame.GetPixels32();
                Assert.That(world.Where((_, index) => index % 173 == 0).Distinct().Take(11).Count(),
                    Is.EqualTo(11), name + " must render the live arena before drawing the HUD.");
                camera.targetTexture = previous;
                camera.rect = arena;

                // Independent URP camera renders may clear the shared target.
                // Capture both live passes and copy the world into its actual
                // reserved viewport instead of treating a HUD-only image as evidence.
                uiCamera.targetTexture = hudTarget;
                uiCamera.clearFlags = CameraClearFlags.SolidColor;
                uiCamera.backgroundColor = Color.black;
                uiCamera.cullingMask = 1 << hud.gameObject.layer;
                hud.worldCamera = uiCamera;
                hud.planeDistance = 1f;
                hud.renderMode = RenderMode.ScreenSpaceCamera;
                Canvas.ForceUpdateCanvases();
                uiCamera.Render();
                RenderTexture.active = hudTarget;
                frame.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                Color32[] composed = frame.GetPixels32();
                Assert.That(composed.Take(bottom * width).Where((_, i) => i % 97 == 0).Distinct().Count(),
                    Is.GreaterThan(6), name + " must also contain the real lower HUD.");
                for (int y = 0; y < arenaHeight; y++)
                    Array.Copy(world, y * arenaWidth, composed, (y + bottom) * width + left, arenaWidth);
                frame.SetPixels32(composed);
                frame.Apply();
                const string folder = "artifacts/p0/soundwell";
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), frame.EncodeToPNG());
            }
            finally
            {
                hud.renderMode = previousMode;
                hud.worldCamera = previousCamera;
                hud.planeDistance = previousPlane;
                camera.targetTexture = previous;
                camera.rect = arena;
                uiCamera.targetTexture = null;
                RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(worldTarget);
                RenderTexture.ReleaseTemporary(hudTarget);
                UnityEngine.Object.DestroyImmediate(worldFrame);
                UnityEngine.Object.DestroyImmediate(frame);
                UnityEngine.Object.DestroyImmediate(uiObject);
                Canvas.ForceUpdateCanvases();
            }
        }

        [UnityTearDown]
        public IEnumerator LeavePlayModeAfterFailure()
        {
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
        }
    }
}
