#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace Rpg.Soundwell
{
    /// <summary>
    /// M10: live Unity Editor memory samples on the exact Soundwell story path.
    /// This is deliberately not a fake .snap file. The Memory Profiler package
    /// supplies Editor analysis, while stable built-in Profiler counters provide
    /// small machine-readable CI measurements. Samples are not player benchmarks.
    /// </summary>
    public static class SoundwellMemoryProbe
    {
        private static readonly string[] Phases = {
            "bootstrap_before_entry", "soundwell_entered", "rescue_completed",
            "soundwell_reloaded", "bootstrap_after_return"
        };

        [Serializable]
        private sealed class PhaseSample
        {
            public string phase;
            public string scene;
            public int unity_frame;
            public long managed_gc_heap_bytes;
            public long unity_managed_used_bytes;
            public long unity_managed_reserved_bytes;
            public long unity_total_allocated_bytes;
            public long unity_total_reserved_bytes;
            public long unity_unused_reserved_bytes;
            public int loaded_game_objects;
            public int loaded_textures;
            // Sum of the Editor's runtime-reported memory for loaded Texture2D objects.
            // This is a footprint trend, not a GPU-only budget or atlas A/B result.
            public long loaded_texture2d_memory_bytes;
            public int loaded_audio_clips;
        }

        [Serializable]
        private sealed class Evidence
        {
            public int schema_version = 1;
            public string scenario = "soundwell-enter-rescue-reload-return";
            public string environment = "Unity Editor Play Mode (GameCI Linux container; not player hardware)";
            public string sample_source = "UnityEngine.Profiling.Profiler + System.GC";
            public string unity_version;
            public List<PhaseSample> snapshots = new List<PhaseSample>();
            public long bootstrap_managed_gc_heap_delta_bytes;
            public long bootstrap_unity_total_allocated_delta_bytes;
            public long bootstrap_loaded_texture2d_delta_bytes;
        }

        private static readonly Evidence Data = new Evidence();

        public static void Reset()
        {
            Data.snapshots.Clear();
            Data.unity_version = Application.unityVersion;
            Data.bootstrap_managed_gc_heap_delta_bytes = 0;
            Data.bootstrap_unity_total_allocated_delta_bytes = 0;
            Data.bootstrap_loaded_texture2d_delta_bytes = 0;
        }

        public static void Capture(string phase)
        {
            int index = Data.snapshots.Count;
            if (index >= Phases.Length || phase != Phases[index])
                throw new InvalidOperationException("Out-of-order M10 memory sample: " + phase);

            // Capture once per story phase, without artificially loading any assets.
            // Counting objects alone misses texture footprint changes after a reload.
            var textures = Resources.FindObjectsOfTypeAll<Texture2D>();
            long textureBytes = 0;
            foreach (var texture in textures)
            {
                if (texture != null)
                    textureBytes += Profiler.GetRuntimeMemorySizeLong(texture);
            }

            Data.snapshots.Add(new PhaseSample
            {
                phase = phase,
                scene = SceneManager.GetActiveScene().name,
                unity_frame = Time.frameCount,
                managed_gc_heap_bytes = GC.GetTotalMemory(false),
                unity_managed_used_bytes = Profiler.GetMonoUsedSizeLong(),
                unity_managed_reserved_bytes = Profiler.GetMonoHeapSizeLong(),
                unity_total_allocated_bytes = Profiler.GetTotalAllocatedMemoryLong(),
                unity_total_reserved_bytes = Profiler.GetTotalReservedMemoryLong(),
                unity_unused_reserved_bytes = Profiler.GetTotalUnusedReservedMemoryLong(),
                loaded_game_objects = Resources.FindObjectsOfTypeAll<GameObject>().Length,
                loaded_textures = textures.Length,
                loaded_texture2d_memory_bytes = textureBytes,
                loaded_audio_clips = Resources.FindObjectsOfTypeAll<AudioClip>().Length,
            });
        }

        public static void Save()
        {
            if (Data.snapshots.Count != Phases.Length ||
                Data.snapshots.Any(s => s.managed_gc_heap_bytes <= 0 ||
                    s.loaded_game_objects <= 0 || s.loaded_textures < 0 ||
                    s.loaded_audio_clips < 0 || s.loaded_texture2d_memory_bytes < 0 ||
                    s.unity_total_allocated_bytes < 0 ||
                    s.unity_total_reserved_bytes < 0 || s.unity_managed_used_bytes < 0 ||
                    s.unity_managed_reserved_bytes < 0 || s.unity_unused_reserved_bytes < 0))
                throw new InvalidOperationException("M10 captured invalid or incomplete real memory metrics.");

            var first = Data.snapshots[0];
            var last = Data.snapshots[Data.snapshots.Count - 1];
            Data.bootstrap_managed_gc_heap_delta_bytes =
                last.managed_gc_heap_bytes - first.managed_gc_heap_bytes;
            Data.bootstrap_unity_total_allocated_delta_bytes =
                last.unity_total_allocated_bytes - first.unity_total_allocated_bytes;
            Data.bootstrap_loaded_texture2d_delta_bytes =
                last.loaded_texture2d_memory_bytes - first.loaded_texture2d_memory_bytes;
            const string target = "artifacts/p0/performance/soundwell-memory-phases.json";
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllText(target, JsonUtility.ToJson(Data, true) + "\n");
        }
    }
}
#endif
