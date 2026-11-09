using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rpg.Tests.PlayMode
{
    public sealed class SoundwellTrialTests
    {
        private const string SceneName = "SoundwellTrial";

        [UnityTest]
        public IEnumerator PlayerCanRingWrapRetreatAndRetry()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap");
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Bootstrap"));

            var entrance = FindRootComponent<SoundwellEntrance>();
            Assert.That(entrance, Is.Not.Null, "The saved Bootstrap scene must expose the trial.");
            var load = entrance.OpenTrial();
            while (!load.isDone)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneName));
            var trial = FindRootComponent<SoundwellTrial>();
            Assert.That(trial, Is.Not.Null, "The saved trial scene must include the trial component.");
            trial.ReadKeyboard = false;
            Vector2 home = trial.BeastPosition;
            Vector2 retreat = trial.PlayerPosition;
            Press(trial, ring: true);
            Assert.That(trial.HasBellTarget, Is.False, "Interaction must require proximity.");

            Press(trial, Vector2.right);
            for (var wait = Until(() => trial.PlayerPosition.x >= -2.25f, "walk into bell range"); wait.MoveNext();)
                yield return null;
            Press(trial, ring: true);
            Assert.That(trial.HasBellTarget, Is.True);
            for (var wait = Until(() => trial.BeastPosition.x < home.x - 0.7f,
                "the beast turns and advances toward the bell"); wait.MoveNext();)
                yield return null;
            Assert.That(trial.BeastPosition.y, Is.LessThan(home.y));
            Vector2 pursued = trial.BeastPosition;
            var clear = Capture(trial, "01-ringing");

            Press(trial, wrap: true);
            Assert.That(trial.IsWrapped, Is.True);
            Assert.That(trial.HasBellTarget, Is.False);
            Assert.That(trial.IsSearching, Is.True);
            Press(trial, ring: true);
            Assert.That(trial.HasBellTarget, Is.False, "A wrapped bell must not attract the beast.");
            yield return null;
            yield return null;
            var muffled = Capture(trial, "02-wrapped-searching");
            Assert.That(muffled.SequenceEqual(clear), Is.False, "The two signal phases must render differently.");
            for (var wait = Until(() => Vector2.Distance(trial.BeastPosition, pursued) > 0.35f,
                "the beast continues searching after losing the bell"); wait.MoveNext();)
                yield return null;

            Press(trial, retry: true);
            Assert.That(trial.IsWrapped, Is.True, "Retry requires a retreat to safety.");
            Press(trial, Vector2.right);
            float collisionCheck = Time.time + 0.85f;
            for (var wait = Until(() => Time.time >= collisionCheck, "hold movement against the grating"); wait.MoveNext();)
                yield return null;
            Assert.That(trial.PlayerPosition.x, Is.LessThan(-0.10f),
                "The safety grating must block the player.");

            Press(trial, Vector2.left);
            for (var wait = Until(() => Vector2.Distance(trial.PlayerPosition, retreat) < 0.25f,
                "return to the safety mark"); wait.MoveNext();)
                yield return null;
            Press(trial, retry: true);
            Assert.That(trial.IsWrapped, Is.False);
            Assert.That(trial.IsSearching, Is.False);
            Assert.That(trial.BeastPosition, Is.EqualTo(home));
            yield return null;
            Capture(trial, "03-retreat-reset");

            Press(trial, Vector2.right);
            for (var wait = Until(() => trial.PlayerPosition.x >= -2.25f, "reach the bell on retry"); wait.MoveNext();)
                yield return null;
            Press(trial, ring: true);
            Assert.That(trial.HasBellTarget, Is.True, "The retry must allow the experiment again.");
            var reload = SceneManager.LoadSceneAsync(SceneName);
            for (var wait = Until(() => reload.isDone, "reload the trial"); wait.MoveNext();)
                yield return null;
            var fresh = FindRootComponent<SoundwellTrial>();
            Assert.That(fresh, Is.Not.Null);
            Assert.That(fresh, Is.Not.SameAs(trial));
            Assert.That(fresh.HasBellTarget, Is.False);
            Assert.That(fresh.IsWrapped, Is.False);
            Assert.That(fresh.PlayerPosition, Is.EqualTo(retreat));
            Assert.That(SceneManager.GetActiveScene().GetRootGameObjects()
                .Count(root => root.name == "PLACEHOLDER Player"), Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
            Debug.Log("RPG_SOUNDWELL_SIGNAL_VALIDATED");
        }

        private static T FindRootComponent<T>() where T : Component =>
            SceneManager.GetActiveScene().GetRootGameObjects()
                .Select(root => root.GetComponent<T>())
                .SingleOrDefault(component => component != null);

        private static void Press(SoundwellTrial trial, Vector2 direction = default, bool ring = false,
            bool wrap = false, bool retry = false) =>
            trial.ApplyInput(direction, ring, wrap, retry);

        private static IEnumerator Until(Func<bool> condition, string action)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!condition() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(condition(), Is.True, action);
        }

        private static byte[] Capture(SoundwellTrial trial, string stage)
        {
            const int width = 960;
            const int height = 540;
            var camera = trial.TrialCamera;
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
