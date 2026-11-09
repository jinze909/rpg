#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rpg.Soundwell
{
    // Runs only inside the isolated CI Play Mode session.
    public sealed class SoundwellSmokeHost : MonoBehaviour
    {
        private const string SceneName = "SoundwellTrial";
        private string runtimeError;

        public int? ExitCode { get; private set; }

        public void Run()
        {
            Application.logMessageReceived += RecordRuntimeError;
            StartCoroutine(Drive(VerifyTrial()));
        }

        private IEnumerator Drive(IEnumerator steps)
        {
            int status = 1;
            while (true)
            {
                bool more;
                try
                {
                    if (runtimeError != null)
                        throw new InvalidOperationException(runtimeError);
                    more = steps.MoveNext();
                }
                catch (Exception error)
                {
                    Debug.LogException(error);
                    break;
                }

                if (!more)
                {
                    if (runtimeError == null)
                    {
                        Debug.Log("RPG_SOUNDWELL_SIGNAL_VALIDATED");
                        status = 0;
                    }
                    break;
                }
                yield return steps.Current;
            }

            Application.logMessageReceived -= RecordRuntimeError;
            ExitCode = status;
        }

        private void RecordRuntimeError(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception)
                runtimeError ??= message;
        }

        private void OnDestroy() => Application.logMessageReceived -= RecordRuntimeError;

        private static IEnumerator VerifyTrial()
        {
            yield return null;
            Require(SceneManager.GetActiveScene().name == "Bootstrap",
                "Unexpected initial scene: " + SceneManager.GetActiveScene().path);
            var entrance = FindRootComponent<SoundwellEntrance>();
            Require(entrance != null, "The saved Bootstrap scene must expose the trial.");
            Debug.Log("SOUNDWELL_CI_BOOTSTRAP_READY");
            yield return entrance.OpenTrial();
            Require(SceneManager.GetActiveScene().name == SceneName, "The Bootstrap entrance must load Soundwell.");
            var trial = FindRootComponent<SoundwellTrial>();
            Require(trial != null, "The saved trial scene must include the trial component.");
            trial.ReadKeyboard = false;
            for (var wait = Until(() => trial.IsReady, "initialize Soundwell"); wait.MoveNext();)
                yield return null;
            Debug.Log("SOUNDWELL_CI_TRIAL_READY");
            Vector2 home = trial.BeastPosition;
            Vector2 retreat = trial.PlayerPosition;
            Press(trial, ring: true);
            Require(!trial.HasBellTarget, "Interaction must require proximity.");

            Press(trial, Vector2.right);
            for (var wait = Until(() => trial.PlayerPosition.x >= -2.25f, "walk into bell range"); wait.MoveNext();)
                yield return null;
            Press(trial, ring: true);
            Require(trial.HasBellTarget, "trial.HasBellTarget must be true.");
            for (var wait = Until(() => trial.BeastPosition.x < home.x - 0.7f,
                "the beast turns and advances toward the bell"); wait.MoveNext();)
                yield return null;
            Require(trial.BeastPosition.y < home.y, "The beast must approach the bell's lane.");
            Vector2 pursued = trial.BeastPosition;
            var clear = Capture(trial, "01-ringing");

            Press(trial, wrap: true);
            Require(trial.IsWrapped, "trial.IsWrapped must be true.");
            Require(!trial.HasBellTarget, "trial.HasBellTarget must be false.");
            Require(trial.IsSearching, "trial.IsSearching must be true.");
            Press(trial, ring: true);
            Require(!trial.HasBellTarget, "A wrapped bell must not attract the beast.");
            yield return null;
            yield return null;
            var muffled = Capture(trial, "02-wrapped-searching");
            Require(!muffled.SequenceEqual(clear), "The two signal phases must render differently.");
            for (var wait = Until(() => Vector2.Distance(trial.BeastPosition, pursued) > 0.35f,
                "the beast continues searching after losing the bell"); wait.MoveNext();)
                yield return null;

            Press(trial, retry: true);
            Require(trial.IsWrapped, "Retry requires a retreat to safety.");
            Press(trial, Vector2.right);
            float collisionCheck = Time.time + 0.85f;
            for (var wait = Until(() => Time.time >= collisionCheck, "hold movement against the grating"); wait.MoveNext();)
                yield return null;
            Require(trial.PlayerPosition.x < -0.10f, "The safety grating must block the player.");

            Press(trial, Vector2.left);
            for (var wait = Until(() => Vector2.Distance(trial.PlayerPosition, retreat) < 0.25f,
                "return to the safety mark"); wait.MoveNext();)
                yield return null;
            Press(trial, retry: true);
            Require(!trial.IsWrapped, "trial.IsWrapped must be false.");
            Require(!trial.IsSearching, "trial.IsSearching must be false.");
            Require(trial.BeastPosition == home, "Reset must return the beast to its initial position.");
            yield return null;
            Capture(trial, "03-retreat-reset");

            Press(trial, Vector2.right);
            for (var wait = Until(() => trial.PlayerPosition.x >= -2.25f, "reach the bell on retry"); wait.MoveNext();)
                yield return null;
            Press(trial, ring: true);
            Require(trial.HasBellTarget, "The retry must allow the experiment again.");
            yield return SceneManager.LoadSceneAsync(SceneName);
            var fresh = FindRootComponent<SoundwellTrial>();
            Require(fresh != null && !ReferenceEquals(fresh, trial), "Reload must create a fresh trial.");
            for (var wait = Until(() => fresh.IsReady, "initialize the reloaded trial"); wait.MoveNext();)
                yield return null;
            Require(!fresh.HasBellTarget, "fresh.HasBellTarget must be false.");
            Require(!fresh.IsWrapped, "fresh.IsWrapped must be false.");
            Require(fresh.PlayerPosition == retreat, "Reload must restore the entrance position.");
            Require(SceneManager.GetActiveScene().GetRootGameObjects()
                .Count(root => root.name == "PLACEHOLDER Player") == 1, "Reload must have exactly one player.");
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
            Require(condition(), "Timed out: " + action);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
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
                Require(colors > 6, $"The captured scene is blank or incomplete: {stage}");
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
#endif
