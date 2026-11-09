#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rpg.Soundwell
{
    // Runs only inside the isolated CI Play Mode session.
    public sealed class SoundwellSmokeHost : MonoBehaviour
    {
        private const string SceneName = "SoundwellTrial";
        private string runtimeError;
        private readonly List<float> frameTimeMs = new List<float>(512);
        private int startGc0;
        private int startGc1;
        private int startGc2;
        private long startHeapBytes;
        private long peakHeapBytes;

        [Serializable]
        private sealed class PlayModePerformance
        {
            public int schema_version = 1;
            public string scenario = "soundwell-ring-wrap-retreat-reload-reenter";
            public string sample_source = "UnityEngine.Time.unscaledDeltaTime";
            public string environment = "Unity Editor Play Mode (GameCI Linux container; not player hardware)";
            public string unity_version;
            public int frame_samples;
            public float frame_time_ms_p50;
            public float frame_time_ms_p95;
            public float frame_time_ms_max;
            public int gc_gen0_collections_delta;
            public int gc_gen1_collections_delta;
            public int gc_gen2_collections_delta;
            public long managed_heap_start_bytes;
            public long managed_heap_end_bytes;
            public long managed_heap_peak_bytes;
        }

        public int? ExitCode { get; private set; }

        public void Run()
        {
            startGc0 = GC.CollectionCount(0);
            startGc1 = GC.CollectionCount(1);
            startGc2 = GC.CollectionCount(2);
            startHeapBytes = GC.GetTotalMemory(false);
            peakHeapBytes = startHeapBytes;
            frameTimeMs.Clear();
            Application.logMessageReceived += RecordRuntimeError;
            StartCoroutine(Drive(VerifyTrial()));
        }

        // Record real Play Mode frame intervals, including trial transitions and scene reload.
        // These headless Editor measurements support comparisons only; they are not FPS targets.
        private void Update()
        {
            if (ExitCode.HasValue)
                return;

            float milliseconds = Time.unscaledDeltaTime * 1000f;
            if (milliseconds > 0f && !float.IsNaN(milliseconds) && !float.IsInfinity(milliseconds))
                frameTimeMs.Add(milliseconds);

            // Heap collection itself has overhead; sample it sparsely rather than each frame.
            if (frameTimeMs.Count > 0 && frameTimeMs.Count % 30 == 0)
                peakHeapBytes = Math.Max(peakHeapBytes, GC.GetTotalMemory(false));
        }

        private void SavePerformanceEvidence()
        {
            if (frameTimeMs.Count < 30)
                throw new InvalidOperationException("Soundwell performance capture has fewer than 30 Play Mode frames.");

            var sorted = frameTimeMs.ToArray();
            Array.Sort(sorted);
            long endHeapBytes = GC.GetTotalMemory(false);
            var evidence = new PlayModePerformance
            {
                unity_version = Application.unityVersion,
                frame_samples = sorted.Length,
                frame_time_ms_p50 = sorted[(int)Math.Ceiling(sorted.Length * 0.50) - 1],
                frame_time_ms_p95 = sorted[(int)Math.Ceiling(sorted.Length * 0.95) - 1],
                frame_time_ms_max = sorted[sorted.Length - 1],
                gc_gen0_collections_delta = GC.CollectionCount(0) - startGc0,
                gc_gen1_collections_delta = GC.CollectionCount(1) - startGc1,
                gc_gen2_collections_delta = GC.CollectionCount(2) - startGc2,
                managed_heap_start_bytes = startHeapBytes,
                managed_heap_end_bytes = endHeapBytes,
                managed_heap_peak_bytes = Math.Max(peakHeapBytes, endHeapBytes)
            };
            const string file = "artifacts/p0/performance/soundwell-playmode.json";
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(file, JsonUtility.ToJson(evidence, true) + "\n");
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
                        try
                        {
                            // Only successful end-to-end input paths produce performance evidence.
                            SavePerformanceEvidence();
                            Debug.Log("RPG_SOUNDWELL_SIGNAL_VALIDATED");
                            status = 0;
                        }
                        catch (Exception error)
                        {
                            Debug.LogException(error);
                        }
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
            Require(ShowsPrompt(entrance, "[F2] Enter the trial"), "The entry screen must show its entry control.");
            var load = entrance.OpenTrial();
            Require(load != null && ReferenceEquals(load, entrance.OpenTrial()),
                "A second entry request during loading must reuse the same operation.");
            yield return load;
            Require(SceneManager.GetActiveScene().name == SceneName, "The Bootstrap entrance must load Soundwell.");
            var trial = FindRootComponent<SoundwellTrial>();
            Require(trial != null, "The saved trial scene must include the trial component.");
            trial.ReadKeyboard = false;
            for (var wait = Until(() => trial.IsReady, "initialize Soundwell"); wait.MoveNext();)
                yield return null;
            var camera = trial.TrialCamera;
            camera.aspect = 1f;
            yield return null;
            yield return null;
            float left = camera.WorldToViewportPoint(new Vector3(-8.1f, 0f, 0f)).x;
            float right = camera.WorldToViewportPoint(new Vector3(8.1f, 0f, 0f)).x;
            Require(left > 0f && right < 1f, "A square viewport must retain both ends of the trial.");
            camera.ResetAspect();
            Require(ShowsPrompt(trial, "Walk to the copper bell"), "The initial view must show where to go.");
            Require(ShowsPrompt(trial, "[Esc] Back to entry"), "The exit control must be visible at the entrance.");

            var bellAudio = FindRootComponent<AudioSource>();
            Require(bellAudio != null, "The copper bell must expose its audio source.");
            Vector2 home = trial.BeastPosition;
            Vector2 retreat = trial.PlayerPosition;
            Press(trial, ring: true);
            Require(!trial.HasBellTarget, "Interaction must require proximity.");

            Press(trial, Vector2.right);
            for (var wait = Until(() => trial.PlayerPosition.x >= -2.25f, "walk into bell range"); wait.MoveNext();)
                yield return null;
            yield return null;
            yield return null;
            Require(ShowsPrompt(trial, "[E] Ring the bell"), "Bell actions must appear within reach.");
            Require(ShowsPrompt(trial, "[WASD / Arrows] Move"), "The movement control must stay visible at the bell.");
            Press(trial, ring: true);
            Require(trial.HasBellTarget, "trial.HasBellTarget must be true.");
            Require(bellAudio.loop && bellAudio.clip != null && bellAudio.clip.name == "Copper bell",
                "The clear sound must repeat while the beast tracks it.");
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
            Require(!bellAudio.loop && bellAudio.clip != null && bellAudio.clip.name == "Wrapped bell",
                "Wrapping must stop the clear loop and switch to a muffled sound.");
            Press(trial, ring: true);
            Require(!trial.HasBellTarget, "A wrapped bell must not attract the beast.");
            yield return null;
            yield return null;
            Require(ShowsPrompt(trial, "[Q] Remove the cloth"), "Wrapped bells must show the available actions.");
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
            Press(trial);
            yield return null;
            yield return null;
            Require(ShowsPrompt(trial, "[R] Reset the experiment"), "Retreat must reveal the reset control.");
            Press(trial, retry: true);
            Require(!trial.IsWrapped, "trial.IsWrapped must be false.");
            Require(!trial.IsSearching, "trial.IsSearching must be false.");
            Require(trial.BeastPosition == home, "Reset must return the beast to its initial position.");
            Require(!bellAudio.loop && bellAudio.clip == null, "Reset must clear the bell's playback state.");
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

            var back = fresh.ReturnToBootstrap();
            Require(back != null && ReferenceEquals(back, fresh.ReturnToBootstrap()),
                "Repeated exit requests must reuse the same load.");
            yield return back;
            Require(SceneManager.GetActiveScene().name == SoundwellEntrance.BootstrapSceneName,
                "Returning from the trial must restore the entry scene.");
            var returnedEntrance = FindRootComponent<SoundwellEntrance>();
            Require(returnedEntrance != null && ShowsPrompt(returnedEntrance, "[F2] Enter the trial"),
                "The entry screen must be usable after returning from the trial.");
            yield return returnedEntrance.OpenTrial();
            var restarted = FindRootComponent<SoundwellTrial>();
            Require(restarted != null, "The trial must be playable after returning to the entry screen.");
            for (var wait = Until(() => restarted.IsReady, "initialize the reentered trial"); wait.MoveNext();)
                yield return null;
            Require(!restarted.HasBellTarget && !restarted.IsWrapped && restarted.PlayerPosition == retreat,
                "Reentering from Bootstrap must start a fresh experiment.");
        }

        private static bool ShowsPrompt(Component owner, string prompt) =>
            owner.GetComponentsInChildren<Text>().Any(label => label.text.Contains(prompt, StringComparison.Ordinal));

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
