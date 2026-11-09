using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rpg.Tests.Editor
{
    public sealed class SoundwellTrialTests
    {
        private const string SceneName = "SoundwellTrial";
        private static Component Trial => Find("Soundwell signal trial", "SoundwellTrial");

        [UnityTest]
        public IEnumerator PlayerCanRingWrapRetreatAndRetry()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);

            var entrance = Find("Soundwell trial entrance", "SoundwellEntrance");
            entrance.GetType().GetMethod("OpenTrial").Invoke(entrance, null);
            yield return Until(() => SceneManager.GetActiveScene().name == SceneName, "load the saved trial scene");
            var trial = Trial;
            trial.GetType().GetProperty("ReadKeyboard").SetValue(trial, false);
            Vector2 home = Read<Vector2>(trial, "BeastPosition");
            Vector2 retreat = Read<Vector2>(trial, "PlayerPosition");
            Press(trial, ring: true);
            Assert.That(Read<bool>(trial, "HasBellTarget"), Is.False, "Interaction must require proximity.");

            Press(trial, Vector2.right);
            yield return Until(() => Read<Vector2>(trial, "PlayerPosition").x >= -2.25f, "walk into bell range");
            Press(trial, ring: true);
            Assert.That(Read<bool>(trial, "HasBellTarget"), Is.True);
            yield return Until(() => Read<Vector2>(trial, "BeastPosition").x < home.x - 0.7f,
                "the beast turns and advances toward the bell");
            Assert.That(Read<Vector2>(trial, "BeastPosition").y, Is.LessThan(home.y));
            Vector2 pursued = Read<Vector2>(trial, "BeastPosition");
            var clear = Capture(trial, "01-ringing");

            Press(trial, wrap: true);
            Assert.That(Read<bool>(trial, "IsWrapped"), Is.True);
            Assert.That(Read<bool>(trial, "HasBellTarget"), Is.False);
            Assert.That(Read<bool>(trial, "IsSearching"), Is.True);
            Press(trial, ring: true);
            Assert.That(Read<bool>(trial, "HasBellTarget"), Is.False, "A wrapped bell must not attract the beast.");
            yield return null;
            yield return null;
            var muffled = Capture(trial, "02-wrapped-searching");
            Assert.That(muffled.SequenceEqual(clear), Is.False, "The two signal phases must render differently.");
            yield return Until(() => Vector2.Distance(Read<Vector2>(trial, "BeastPosition"), pursued) > 0.35f,
                "the beast continues searching after losing the bell");

            Press(trial, retry: true);
            Assert.That(Read<bool>(trial, "IsWrapped"), Is.True, "Retry requires a retreat to safety.");
            Press(trial, Vector2.right);
            float collisionCheck = Time.time + 0.85f;
            yield return Until(() => Time.time >= collisionCheck, "hold movement against the grating");
            Assert.That(Read<Vector2>(trial, "PlayerPosition").x, Is.LessThan(-0.10f),
                "The safety grating must block the player.");

            Press(trial, Vector2.left);
            yield return Until(() => Vector2.Distance(Read<Vector2>(trial, "PlayerPosition"), retreat) < 0.25f,
                "return to the safety mark");
            Press(trial, retry: true);
            Assert.That(Read<bool>(trial, "IsWrapped"), Is.False);
            Assert.That(Read<bool>(trial, "IsSearching"), Is.False);
            Assert.That(Read<Vector2>(trial, "BeastPosition"), Is.EqualTo(home));
            yield return null;
            Capture(trial, "03-retreat-reset");

            Press(trial, Vector2.right);
            yield return Until(() => Read<Vector2>(trial, "PlayerPosition").x >= -2.25f, "reach the bell on retry");
            Press(trial, ring: true);
            Assert.That(Read<bool>(trial, "HasBellTarget"), Is.True, "The retry must allow the experiment again.");
            var reload = SceneManager.LoadSceneAsync(SceneName);
            yield return Until(() => reload.isDone, "reload the trial");
            var fresh = Trial;
            Assert.That(fresh, Is.Not.SameAs(trial));
            Assert.That(Read<bool>(fresh, "HasBellTarget"), Is.False);
            Assert.That(Read<bool>(fresh, "IsWrapped"), Is.False);
            Assert.That(Read<Vector2>(fresh, "PlayerPosition"), Is.EqualTo(retreat));
            Assert.That(SceneManager.GetActiveScene().GetRootGameObjects()
                .Count(root => root.name == "PLACEHOLDER Player"), Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
            Debug.Log("RPG_SOUNDWELL_SIGNAL_VALIDATED");
            yield return new ExitPlayMode();
        }

        [UnityTearDown]
        public IEnumerator RestoreEditMode()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();
        }

        private static Component Find(string objectName, string component)
        {
            var root = GameObject.Find(objectName);
            Assert.That(root, Is.Not.Null, objectName);
            var type = Type.GetType($"Rpg.Soundwell.{component}, Assembly-CSharp", true);
            var result = root.GetComponent(type);
            Assert.That(result, Is.Not.Null, component);
            return result;
        }

        private static T Read<T>(Component component, string property) =>
            (T)component.GetType().GetProperty(property).GetValue(component);

        private static void Press(Component component, Vector2 direction = default, bool ring = false,
            bool wrap = false, bool retry = false) =>
            component.GetType().GetMethod("ApplyInput").Invoke(component, new object[] { direction, ring, wrap, retry });

        private static IEnumerator Until(Func<bool> condition, string action)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!condition() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(condition(), Is.True, action);
        }

        private static byte[] Capture(Component trial, string stage)
        {
            const int width = 960;
            const int height = 540;
            var camera = Read<Camera>(trial, "TrialCamera");
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var target = RenderTexture.GetTemporary(width, height, 24);
            var frame = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                frame.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                frame.Apply();
                var colors = frame.GetPixels32().Where((_, index) => index % 173 == 0).Distinct().Count();
                Assert.That(colors, Is.GreaterThan(6), $"The captured scene is blank or incomplete: {stage}");
                byte[] png = frame.EncodeToPNG();
                const string directory = "artifacts/p0/soundwell";
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, stage + ".png"), png);
                return png;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.Destroy(frame);
            }
        }
    }
}
