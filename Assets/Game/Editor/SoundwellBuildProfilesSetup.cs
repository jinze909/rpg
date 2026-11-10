using System;
using System.IO;
using System.Linq;
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
            if (!global.SequenceEqual(expected) || !own.SequenceEqual(global))
                throw new InvalidOperationException("Both profiles must use the same two authoritative scenes.");
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
            bool dataFound = Directory.Exists(BuildDirectory + "/Soundwell_Data");
            if (!dataFound)
                throw new InvalidOperationException("The real built Player has no scene data directory.");
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
            };
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            File.WriteAllText(ResultPath, JsonUtility.ToJson(result, true) + "\n");
            Debug.Log("RPG_SOUNDWELL_LINUX_PLAYER_BUILT");
        }
    }
}
