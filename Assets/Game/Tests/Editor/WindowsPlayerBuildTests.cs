using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Rpg.Tests.Windows
{
    public class WindowsPlayerBuildTests
    {
        [Test, Timeout(900000)]
        public void BuildsWindows64PlayerWithBothGameScenes()
        {
            const BuildTarget target = BuildTarget.StandaloneWindows64;
            Assert.That(BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target), Is.True);
            Assert.That(EditorUserBuildSettings.activeBuildTarget, Is.EqualTo(target));
            Assert.That(PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone), Is.EqualTo(ScriptingImplementation.Mono2x));

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Assert.That(scenes, Does.Contain("Assets/Game/Scenes/Bootstrap.unity"));
            Assert.That(scenes, Does.Contain("Assets/Game/Scenes/SoundwellTrial.unity"));
            const string path = "Temp/WindowsBuildSmoke/Soundwell.exe";
            string buildDirectory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(buildDirectory);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = target,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.StrictMode,
            });
            Assert.That(report.summary.result, Is.EqualTo(BuildResult.Succeeded));
            Assert.That(report.summary.platform, Is.EqualTo(target));
            Assert.That(report.summary.totalErrors, Is.Zero);

            using (var reader = new BinaryReader(File.OpenRead(path)))
            {
                Assert.That(reader.ReadUInt16(), Is.EqualTo(0x5a4d), "Windows executable DOS signature");
                reader.BaseStream.Position = 0x3c;
                int offset = reader.ReadInt32();
                Assert.That((long)offset, Is.InRange(0x40L, reader.BaseStream.Length - 6L));
                reader.BaseStream.Position = offset;
                Assert.That(reader.ReadUInt32(), Is.EqualTo(0x4550), "Windows PE signature");
                Assert.That(reader.ReadUInt16(), Is.EqualTo(0x8664), "x86-64 machine type");
            }
            Assert.That(File.Exists(Path.Combine(buildDirectory, "UnityPlayer.dll")), Is.True);
            Assert.That(Directory.Exists(Path.Combine(buildDirectory, "Soundwell_Data")), Is.True);
            Debug.Log("RPG_WINDOWS_PLAYER_BUILT");
        }
    }
}
