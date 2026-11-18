using System;
using System.IO;
using System.Linq;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Rpg.Editor
{
    /// <summary>
    /// M11: Unity-authored native Linux profiles for the ONLY two saved scenes.
    /// No release credentials, platform installations, duplicated scene lists,
    /// custom build target, or parallel build pipeline.
    /// </summary>
    public static class SoundwellBuildProfilesSetup
    {
        private const string DebugPath = "Assets/Settings/Build Profiles/Soundwell Debug Linux.asset";
        private const string TestPath = "Assets/Settings/Build Profiles/Soundwell Test Linux.asset";
        private const string BuildDirectory = "Temp/M11SoundwellTestPlayer";
        private const string ResultPath = "artifacts/p0/builds/soundwell-test-linux.json";
        private static readonly GUID LinuxGuid =
            new GUID("cb423bfea44b4d658edb8bc5d91a3024");

        [Serializable]
        private sealed class BuildEvidence
        {
            public int schema_version = 1;
            public string unity_version;
            public string platform_guid;
            public string target;
            public string test_profile;
            public string scripting_backend;
            public string[] scenes;
            public string result;
            public bool development_build;
            public int total_errors;
            public long executable_bytes;
            public long build_report_bytes;
            public bool data_directory_present;
            public bool save_restart_passed;
            public string environment =
                "Unity 6000.6.8f1 Linux Editor CI; actual Linux Player build; not a user device";
        }

        public static void Prepare()
        {
            var debug = AssetDatabase.LoadAssetAtPath<BuildProfile>(DebugPath);
            var test = AssetDatabase.LoadAssetAtPath<BuildProfile>(TestPath);
            if (debug != null || test != null)
            {
                if (debug == null || test == null)
                    throw new InvalidOperationException("Partial M11 Build Profiles; refusing to create duplicates.");
                Validate();
                return;
            }

            if (!BuildProfile.GetInstalledPlatformModules().Any(module => module.platformGuid == LinuxGuid))
                throw new InvalidOperationException(
                    "M11 Linux Standalone build module is not installed in the existing GameCI editor.");

            // CreateBuildProfile is the native Unity 6.6 Editor API. It creates
            // real .asset + .meta and may initialize asynchronously; callbacks
            // MUST be static to survive domain reload.
            BuildProfile.CreateBuildProfile(LinuxGuid, "Soundwell Debug Linux", ConfigureDebug);
            BuildProfile.CreateBuildProfile(LinuxGuid, "Soundwell Test Linux", ConfigureTest);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            Validate();
            Debug.Log("RPG_SOUNDWELL_BUILD_PROFILES_PREPARED");
        }

        private static void ConfigureDebug(BuildProfile profile) => Configure(profile, "RPG_DEBUG_PROFILE");
        private static void ConfigureTest(BuildProfile profile) => Configure(profile, "RPG_TEST_PROFILE");

        private static void Configure(BuildProfile profile, string define)
        {
            if (profile == null)
                throw new InvalidOperationException("Unity did not return an initialized native BuildProfile.");
            profile.overrideGlobalScenes = false; // Single source: EditorBuildSettings.globalScenes
            profile.scriptingDefines = new[] { define };
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        private static BuildProfile Load(string path, string define)
        {
            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(path);
            if (profile == null || profile.overrideGlobalScenes ||
                profile.scriptingDefines == null ||
                !profile.scriptingDefines.SequenceEqual(new[] { define }))
                throw new InvalidOperationException("Saved M11 BuildProfile is missing or changed: " + path);
            var expected = new[] {
                "Assets/Game/Scenes/Bootstrap.unity",
                "Assets/Game/Scenes/SoundwellTrial.unity"
            };
            var global = EditorBuildSettings.scenes.Where(x => x.enabled).Select(x => x.path).ToArray();
            var own = profile.GetScenesForBuild().Where(x => x.enabled).Select(x => x.path).ToArray();
            // The authored Whitewater prologue may later become a real saved
            // scene. Admit exactly that third scene, never arbitrary extras.
            const string prelude = "Assets/Game/Scenes/WhitewaterPrelude.unity";
            bool includesPrelude = global.Length == expected.Length + 1 &&
                global.Take(expected.Length).SequenceEqual(expected) &&
                global[global.Length - 1] == prelude;
            if ((!global.SequenceEqual(expected) && !includesPrelude) ||
                !own.SequenceEqual(global))
                throw new InvalidOperationException(
                    "Both saved profiles must use the authoritative Bootstrap/Trial scenes and optional authored Whitewater prologue.");
            foreach (string scenePath in global)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                    throw new InvalidOperationException(
                        "Build Profile references a missing authored Unity scene: " + scenePath);
            }
            return profile;
        }

        public static void Validate()
        {
            if (!BuildProfile.GetInstalledPlatformModules().Any(module => module.platformGuid == LinuxGuid))
                throw new InvalidOperationException("Native Linux Player module is unavailable.");
            Load(DebugPath, "RPG_DEBUG_PROFILE");
            Load(TestPath, "RPG_TEST_PROFILE");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64))
                throw new InvalidOperationException("Unity can edit profiles but cannot build Linux Standalone.");
            ScriptingImplementation backend =
                PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            if (backend != ScriptingImplementation.Mono2x &&
                backend != ScriptingImplementation.IL2CPP)
                throw new InvalidOperationException("Unknown Standalone scripting backend: " + backend);
            Debug.Log("RPG_SOUNDWELL_BUILD_PROFILES_VALIDATED");
        }

        // The file extension and BuildReport are not enough to prove that
        // the saved Test profile produced a Linux x86-64 executable. Read only
        // the fixed ELF identification header; never load the full Player
        // binary into managed memory in CI.
        private static void ValidateLinuxX64Binary(string executable)
        {
            var header = new byte[24];
            using (var stream = File.OpenRead(executable))
            {
                int offset = 0;
                while (offset < header.Length)
                {
                    int count = stream.Read(header, offset, header.Length - offset);
                    if (count == 0)
                        throw new InvalidOperationException("M11 Player is shorter than the ELF header.");
                    offset += count;
                }
            }

            // ELF magic, 64-bit little-endian ELFv1, ET_EXEC or ET_DYN
            // (modern PIE), x86-64 machine 62, and current ELF header version.
            // An ELF64 x86-64 relocatable object has the same magic/machine
            // but is not a runnable Linux Player; do not accept ET_REL.
            if (header[0] != 0x7f || header[1] != (byte)'E' ||
                header[2] != (byte)'L' || header[3] != (byte)'F' ||
                header[4] != 2 || header[5] != 1 || header[6] != 1 ||
                (header[16] != 2 && header[16] != 3) || header[17] != 0 ||
                header[18] != 62 || header[19] != 0 ||
                header[20] != 1 || header[21] != 0 ||
                header[22] != 0 || header[23] != 0)
                throw new InvalidOperationException(
                    "M11 saved Linux Test profile did not produce an executable ELF64 x86-64 Player.");
        }

        public static void ValidateAndBuild()
        {
            Validate();
            BuildProfile profile = Load(TestPath, "RPG_TEST_PROFILE");
            if (Directory.Exists(BuildDirectory))
                Directory.Delete(BuildDirectory, recursive: true);
            Directory.CreateDirectory(BuildDirectory);
            string executable = BuildDirectory + "/Soundwell.x86_64";
            var report = BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions
            {
                buildProfile = profile,
                locationPathName = executable,
                options = BuildOptions.Development | BuildOptions.StrictMode
            });
            if (report == null || report.summary.result != BuildResult.Succeeded ||
                report.summary.totalErrors != 0)
                throw new InvalidOperationException("Real M11 Unity Linux Player build failed: " +
                    (report == null ? "no report" : report.summary.result.ToString()));
            var playerFile = new FileInfo(executable);
            if (!playerFile.Exists || playerFile.Length <= 1024)
                throw new InvalidOperationException("Successful BuildReport produced no Linux Player executable.");
            ValidateLinuxX64Binary(executable);
            bool dataFound = Directory.Exists(BuildDirectory + "/Soundwell_Data");
            if (!dataFound)
                throw new InvalidOperationException("The real built Player has no scene data directory.");
            bool ranSaveRegression = Application.platform == RuntimePlatform.LinuxEditor;
            if (ranSaveRegression)
                VerifySaveAcrossPlayerRestart(executable);
            var result = new BuildEvidence
            {
                unity_version = Application.unityVersion,
                platform_guid = LinuxGuid.ToString(),
                target = BuildTarget.StandaloneLinux64.ToString(),
                test_profile = AssetDatabase.GetAssetPath(profile),
                scripting_backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone).ToString(),
                scenes = profile.GetScenesForBuild().Where(x => x.enabled).Select(x => x.path).ToArray(),
                result = report.summary.result.ToString(),
                development_build = true,
                total_errors = report.summary.totalErrors,
                executable_bytes = playerFile.Length,
                build_report_bytes = (long)report.summary.totalSize,
                data_directory_present = dataFound,
                save_restart_passed = ranSaveRegression,
            };
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            File.WriteAllText(ResultPath, JsonUtility.ToJson(result, true) + "\n");
            Debug.Log("RPG_SOUNDWELL_LINUX_PLAYER_BUILT");
        }

        private static void VerifySaveAcrossPlayerRestart(string executable)
        {
            string evidence = Path.GetDirectoryName(ResultPath);
            Directory.CreateDirectory(evidence);
            string sandbox = Path.GetFullPath(Path.Combine(BuildDirectory, Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(sandbox);
            try
            {
                foreach (string phase in new[] { "write", "read" })
                {
                    string log = Path.Combine(evidence, $"ork-save-{phase}.log");
                    var start = new ProcessStartInfo
                    {
                        FileName = Path.GetFullPath(executable),
                        Arguments = $"-batchmode -nographics -rpg-save-restart {phase} -logFile {log}",
                        UseShellExecute = false,
                    };
                    start.EnvironmentVariables["XDG_CONFIG_HOME"] = sandbox;
                    using var player = Process.Start(start);
                    if (player == null)
                        throw new InvalidOperationException($"Could not launch save regression ({phase}).");
                    if (!player.WaitForExit(90000))
                    {
                        player.Kill();
                        player.WaitForExit();
                        throw new InvalidOperationException($"Save regression timed out ({phase}). See {log}.");
                    }
                    string marker = "RPG_ORK_SAVE_RESTART_" + phase.ToUpperInvariant();
                    if (player.ExitCode != 0 || !File.Exists(log) || !File.ReadAllText(log).Contains(marker))
                        throw new InvalidOperationException($"Save regression failed ({phase}). See {log}.");
                }
            }
            finally
            {
                Directory.Delete(sandbox, recursive: true);
            }
        }
    }
}
