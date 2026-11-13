using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Rpg.Soundwell;
using GamingIsLove.Makinom;
using GamingIsLove.Makinom.Editor;
using GamingIsLove.ORKFramework;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Rpg.Tests.Editor
{
    public class P0BootstrapTests
    {
        private const string ScenePath = "Assets/Game/Scenes/Bootstrap.unity";
        private const string ProjectPath = "Assets/Gaming Is Love/_Data/Project.asset";

        // Keep preparation out of SetUp/OneTimeSetUp: UTF reruns them after domain reload.
        // A real Linux Player build can exceed NUnit's 180-second default.
        [Test, Order(0), Timeout(900000)]
        public void SavedConfigurationAndWiringAreValid()
        {
            Assert.That(Application.unityVersion, Is.EqualTo("6000.6.8f1"));
            Assert.That(Type.GetType("UnityEngine.UI.Button, UnityEngine.UI", throwOnError: true), Is.Not.Null);
            Assert.That(Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro", throwOnError: true), Is.Not.Null);
            Assert.That(Assembly.Load("Makinom2.UnityUI").GetTypes(), Is.Not.Empty,
                "The pinned Makinom Unity UI DLL must resolve its types against this Unity installation.");
            Assert.That(Assembly.Load("Unity.Cinemachine").GetType("Unity.Cinemachine.CinemachineCamera"), Is.Not.Null,
                "Unity 6.6 must resolve its editor-matched Cinemachine 3 core package.");
            if (Environment.GetCommandLineArgs().Contains("-rpgPrepareDefaults"))
            {
                UnityEditor.EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.None;
                if (AssetDatabase.LoadAssetAtPath<MakinomProjectAsset>(ProjectPath) == null)
                {
                    CreateDefaultProject();
                }
                InvokeUrpSetup("Prepare");
                InvokeSoundwellAnimationSetup("Prepare");
                InvokeSoundwellTilemapSetup("Prepare");
                InvokeSoundwellSpriteAtlasSetup("Prepare");
                InvokeSoundwellAudioMixerSetup("Prepare");
                InvokeSoundwellLocalizationSetup("Prepare");
                InvokeSoundwellBuildProfilesSetup("Prepare");
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            else
            {
                Assert.That(File.Exists("ProjectSettings/EditorSettings.asset"), Is.True,
                    "Save full Play Mode reload settings before validation.");
                // Validate BEFORE wiring: verification must not repair missing configuration.
                InvokeSoundwellAnimationSetup("Validate");
                InvokeSoundwellTilemapSetup("Validate");
                InvokeSoundwellSpriteAtlasSetup("Validate");
                InvokeSoundwellAudioMixerSetup("Validate");
                InvokeSoundwellLocalizationSetup("Validate");
                InvokeSoundwellBuildProfilesSetup("Validate");
                ValidateConfiguration();
            }

            Assert.That(EditorApplication.ExecuteMenuItem("Tools/RPG/Wire ORK Bootstrap"), Is.True);
            byte[] wiredScene = File.ReadAllBytes(ScenePath);
            Assert.That(EditorApplication.ExecuteMenuItem("Tools/RPG/Wire ORK Bootstrap"), Is.True);
            Assert.That(File.ReadAllBytes(ScenePath), Is.EqualTo(wiredScene),
                "Running the command again must not rewrite the saved scene.");
            ValidateConfiguration();
            Assert.That(File.Exists("Packages/packages-lock.json"), Is.True);
            // Prepare must also persist URP normalization caused by the real Player build.
            // Verify rebuilds the same committed profiles and requires a clean checkout.
            InvokeSoundwellBuildProfilesSetup("ValidateAndBuild");
        }

        [Test, Order(1)]
        public void Urp2DRendererIsTheOnlyActivePipeline()
        {
            RenderPipelineAsset pipeline = GraphicsSettings.defaultRenderPipeline;
            Assert.That(pipeline, Is.Not.Null, "The project must assign one default URP asset.");
            Assert.That(pipeline.GetType().FullName,
                Is.EqualTo("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset"));

            var rendererProperty = pipeline.GetType().GetProperty("scriptableRenderer",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(rendererProperty, Is.Not.Null, "The URP asset must expose its active renderer.");
            var renderer = rendererProperty.GetValue(pipeline);
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.GetType().Name, Is.EqualTo("Renderer2D"),
                "The project must use the 2D Renderer, not the 3D Universal Renderer.");

            var rendererList = new SerializedObject(pipeline).FindProperty("m_RendererDataList");
            Assert.That(rendererList, Is.Not.Null);
            Assert.That(rendererList.arraySize, Is.EqualTo(1),
                "Keep one renderer only; do not leave a parallel 3D renderer configured.");
            Assert.That(rendererList.GetArrayElementAtIndex(0).objectReferenceValue.GetType().Name,
                Is.EqualTo("Renderer2DData"));
            Assert.That(GraphicsSettings.currentRenderPipeline, Is.SameAs(pipeline));

            int originalQuality = QualitySettings.GetQualityLevel();
            try
            {
                for (int i = 0; i < QualitySettings.names.Length; i++)
                {
                    QualitySettings.SetQualityLevel(i, true);
                    Assert.That(QualitySettings.renderPipeline, Is.Null,
                        "Quality level " + QualitySettings.names[i] +
                        " must inherit the single global URP asset, not override it.");
                    Assert.That(GraphicsSettings.currentRenderPipeline, Is.SameAs(pipeline));
                }
            }
            finally
            {
                QualitySettings.SetQualityLevel(originalQuality, true);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Game" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.That(material, Is.Not.Null);
                Assert.That(material.shader, Is.Not.Null, path + " has a missing shader.");
                string shaderName = material.shader.name;
                Assert.That(shaderName == "Sprites/Default" || shaderName == "Standard" ||
                    shaderName.StartsWith("Legacy Shaders/", StringComparison.Ordinal),
                    Is.False, path + " still references a Built-in-only shader: " + shaderName);
            }

            LogAssert.Expect(UnityEngine.LogType.Log, "RPG_URP2D_VALIDATED");
            Debug.Log("RPG_URP2D_VALIDATED");
            LogAssert.NoUnexpectedReceived();
        }

        [Test, Order(8)]
        public void InvalidSavedBindingOverridesRecoverDefaultInputWithoutLosingValidRemaps()
        {
            // Isolated copies: never mutate the game's cached action asset.
            // A damaged PlayerPrefs entry must not block the real entry UI.
            const string key = "rpg.soundwell.input.binding-overrides.v1";
            bool hadSetting = PlayerPrefs.HasKey(key);
            string previous = PlayerPrefs.GetString(key, string.Empty);
            var savedAsset = Resources.Load<InputActionAsset>("Input/SoundwellControls");
            Assert.That(savedAsset, Is.Not.Null);
            var broken = UnityEngine.Object.Instantiate(savedAsset);
            var remapped = UnityEngine.Object.Instantiate(savedAsset);
            var restored = UnityEngine.Object.Instantiate(savedAsset);
            try
            {
                var load = typeof(SoundwellInputAdapter).GetMethod(
                    "RestoreSavedBindingOverrides", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(load, Is.Not.Null);
                PlayerPrefs.SetString(key, "not-json");
                LogAssert.Expect(UnityEngine.LogType.Warning,
                    "Soundwell binding overrides are invalid; restored default controls (FormatException).");
                load.Invoke(null, new object[] { broken });
                Assert.That(PlayerPrefs.HasKey(key), Is.False,
                    "A corrupt local preference must not strand input initialization.");
                Assert.That(broken.FindActionMap("Gameplay").FindAction("Ring")
                    .bindings[0].overridePath, Is.Null);

                remapped.FindActionMap("Gameplay").FindAction("Ring")
                    .ApplyBindingOverride(0, "<Keyboard>/h");
                string valid = remapped.SaveBindingOverridesAsJson();
                PlayerPrefs.SetString(key, valid);
                load.Invoke(null, new object[] { restored });
                Assert.That(restored.FindActionMap("Gameplay").FindAction("Ring")
                    .bindings[0].overridePath, Is.EqualTo("<Keyboard>/h"),
                    "A valid player's custom Ring key must be restored untouched.");
                Assert.That(PlayerPrefs.GetString(key), Is.EqualTo(valid));
            }
            finally
            {
                if (hadSetting) PlayerPrefs.SetString(key, previous);
                else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
                UnityEngine.Object.DestroyImmediate(broken);
                UnityEngine.Object.DestroyImmediate(remapped);
                UnityEngine.Object.DestroyImmediate(restored);
            }
        }

        // UTF restores iterator position across reload, not ordinary local variables.
        // Use separate tests rather than a loop spanning two EnterPlayMode instructions.
        [UnityTest, Order(10)]
        public IEnumerator FirstPlayInitializesFramework()
        {
            ValidateConfiguration();
            yield return new EnterPlayMode(expectDomainReload: true);
            yield return AssertFrameworkInitialization();
            // A real saved Animator state must retain the visible beast-hit
            // cue if a gameplay action arrives before the short flinch ends.
            // Resetting a scene must clear that priority for the next attempt.
            var visualRoot = new GameObject("M4 hit-priority regression");
            try
            {
                var visual = visualRoot.AddComponent<SoundwellPlayerVisualAnimation>();
                visual.Initialize();
                Assert.That(visual.CurrentState, Is.EqualTo("Idle"));
                visual.PlayAction();
                Assert.That(visual.CurrentState, Is.EqualTo("Action"));
                visual.PlayHit();
                Assert.That(visual.CurrentState, Is.EqualTo("Hit"),
                    "An actual hit must interrupt a normal action.");
                visual.PlayAction();
                visual.UpdateMotion(true);
                Assert.That(visual.CurrentState, Is.EqualTo("Hit"),
                    "An immediate action/move must not erase the hit reaction.");
                visual.ResetAnimation();
                Assert.That(visual.CurrentState, Is.EqualTo("Idle"));
                visual.PlayAction();
                Assert.That(visual.CurrentState, Is.EqualTo("Action"),
                    "Retry must clear hit priority and restore legal actions.");
                // Rapid bell/interaction clicks must not keep rewinding the
                // same animation. Compare its actual deadline so a slow CI
                // frame cannot turn this into a timing-dependent test.
                var deadlineField = typeof(SoundwellPlayerVisualAnimation).GetField(
                    "oneShotUntil", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(deadlineField, Is.Not.Null);
                float firstDeadline = (float)deadlineField.GetValue(visual);
                visual.PlayAction();
                Assert.That((float)deadlineField.GetValue(visual), Is.EqualTo(firstDeadline),
                    "Repeated action input must not restart or extend the visual cue.");
                visual.ResetAnimation();
                visual.PlayAction();
                Assert.That((float)deadlineField.GetValue(visual), Is.GreaterThan(0f),
                    "A new attempt must still start a fresh action cue.");
            }
            finally
            {
                UnityEngine.Object.Destroy(visualRoot);
            }
            Debug.Log("RPG_BOOTSTRAP_PLAY_VALIDATED:first");
            yield return new ExitPlayMode();
            Assert.That(EditorApplication.isPlaying, Is.False);
        }

        [UnityTest, Order(20)]
        public IEnumerator SecondPlayInitializesFramework()
        {
            ValidateConfiguration();
            yield return new EnterPlayMode(expectDomainReload: true);
            yield return AssertFrameworkInitialization();
            // ORK can bring a second EventSystem to the foreground. Keep the
            // existing Soundwell-owned UGUI input owner, even when the later
            // EventSystem claims EventSystem.current during a battle transition.
            var canonical = UnityEngine.Object.FindObjectsByType<EventSystem>()
                .FirstOrDefault(system => system.name == "Soundwell EventSystem");
            if (canonical == null)
            {
                var root = new GameObject("Soundwell EventSystem", typeof(EventSystem),
                    typeof(InputSystemUIInputModule));
                canonical = root.GetComponent<EventSystem>();
            }
            Assert.That(SoundwellInputAdapter.EnsureEventSystem(null), Is.SameAs(canonical));
            var rival = new GameObject("ORK late EventSystem",
                typeof(EventSystem), typeof(InputSystemUIInputModule))
                .GetComponent<EventSystem>();
            try
            {
                EventSystem.current = rival;
                Assert.That(EventSystem.current, Is.SameAs(rival));
                Assert.That(SoundwellInputAdapter.EnsureEventSystem(null), Is.SameAs(canonical),
                    "A late ORK UI EventSystem must not replace the Soundwell UI owner.");
                Assert.That(EventSystem.current, Is.SameAs(canonical));
                Assert.That(canonical.GetComponent<InputSystemUIInputModule>().enabled, Is.True);
                Assert.That(rival.enabled, Is.False,
                    "The conflicting ORK EventSystem must be retired.");
            }
            finally
            {
                UnityEngine.Object.Destroy(rival.gameObject);
            }
            // S00 back/Submit is gamepad A, which can also be read as the
            // Bootstrap entry shortcut in the very same frame. Real UGUI
            // callbacks must not start a trial or reopen the opening twice.
            var entrance = UnityEngine.Object.FindFirstObjectByType<SoundwellEntrance>();
            Assert.That(entrance, Is.Not.Null);
            entrance.OpenPrelude();
            var prelude = entrance.GetComponentInChildren<WhitewaterPrelude>(true);
            Assert.That(prelude, Is.Not.Null);
            prelude.Close();
            Assert.That(entrance.OpenTrial(), Is.Null,
                "Closing S00 must not trigger a Soundwell trial with the same Submit.");
            entrance.OpenPrelude();
            yield return null;
            Assert.That(entrance.GetComponentInChildren<WhitewaterPrelude>(true), Is.Null,
                "The same exit Submit must not re-open S00 before the next input frame.");

            Debug.Log("RPG_BOOTSTRAP_PLAY_VALIDATED:second");
            yield return new ExitPlayMode();
            Assert.That(EditorApplication.isPlaying, Is.False);
        }

        [UnityTest, Order(30)]
        public IEnumerator SoundwellAudioMuteSurvivesSceneRerouting()
        {
            // M6 regression: switching/reloading the Soundwell scene must
            // never silently undo an explicit player mute.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode(expectDomainReload: true);

            var mixer = Resources.Load<AudioMixer>(SoundwellAudioBus.MixerResource);
            Assert.That(mixer, Is.Not.Null, "The saved mixer must load in player mode.");
            bool hadPreference = PlayerPrefs.HasKey(SoundwellAudioBus.MutePreferenceKey);
            int previousPreference = PlayerPrefs.GetInt(SoundwellAudioBus.MutePreferenceKey, 0);
            var bell = new GameObject("Audio routing test bell").AddComponent<AudioSource>();
            var reloadedBell = new GameObject("Audio routing test bell after reload")
                .AddComponent<AudioSource>();
            try
            {
                Assert.That(SoundwellAudioBus.SetMuted(mixer, true), Is.True);
                Assert.That(SoundwellAudioBus.IsMuted, Is.True);
                SoundwellAudioBus.Route(bell);
                Assert.That(bell.outputAudioMixerGroup, Is.Not.Null);
                Assert.That(bell.outputAudioMixerGroup.name, Is.EqualTo("SFX"),
                    "Bell audio must route through the SFX bus, not Music or Master.");
                Assert.That(bell.outputAudioMixerGroup.audioMixer, Is.SameAs(mixer),
                    "Bell must use the saved Soundwell mixer, not another audio authority.");

                // A scene reload builds a fresh AudioSource; checking the same
                // component twice would miss a broken new-scene routing path.
                SoundwellAudioBus.Route(reloadedBell);
                Assert.That(reloadedBell.outputAudioMixerGroup,
                    Is.SameAs(bell.outputAudioMixerGroup),
                    "A new scene's bell must inherit the same SFX output.");
                Assert.That(SoundwellAudioBus.IsMuted, Is.True,
                    "Routing another source must not clear persisted mute.");
                Assert.That(SoundwellAudioBus.TryGetVolume(mixer, out float mutedDb), Is.True);
                Assert.That(mutedDb, Is.EqualTo(-80f).Within(0.1f),
                    "Re-routing a new bell must retain player mute.");

                Assert.That(SoundwellAudioBus.SetMuted(mixer, false), Is.True);
                SoundwellAudioBus.Route(reloadedBell);
                Assert.That(SoundwellAudioBus.IsMuted, Is.False);
                Assert.That(reloadedBell.outputAudioMixerGroup, Is.SameAs(bell.outputAudioMixerGroup));
                Assert.That(SoundwellAudioBus.TryGetVolume(mixer, out float audibleDb), Is.True);
                Assert.That(audibleDb, Is.EqualTo(0f).Within(0.1f),
                    "Unmuting must survive the next scene's audio hookup.");
            }
            finally
            {
                SoundwellAudioBus.SetMuted(mixer, previousPreference != 0);
                if (!hadPreference)
                    PlayerPrefs.DeleteKey(SoundwellAudioBus.MutePreferenceKey);
                PlayerPrefs.Save();
                UnityEngine.Object.Destroy(bell.gameObject);
                UnityEngine.Object.Destroy(reloadedBell.gameObject);
            }

            yield return new ExitPlayMode();
        }

        [Test]
        public void FailedAudioMixerMuteMustPreserveTheSavedPlayerChoice()
        {
            // If the mixer has not loaded, the failed operation must not
            // overwrite an existing choice. A later scene may still route
            // its bell with that saved preference.
            string key = SoundwellAudioBus.MutePreferenceKey;
            bool hadPreference = PlayerPrefs.HasKey(key);
            int originalPreference = PlayerPrefs.GetInt(key, 0);
            try
            {
                PlayerPrefs.SetInt(key, 1);
                Assert.That(SoundwellAudioBus.SetMuted(null, false), Is.False);
                Assert.That(SoundwellAudioBus.IsMuted, Is.True,
                    "A failed unmute must not discard the player's saved mute.");

                PlayerPrefs.SetInt(key, 0);
                Assert.That(SoundwellAudioBus.SetMuted(null, true), Is.False);
                Assert.That(SoundwellAudioBus.IsMuted, Is.False,
                    "A failed mute must not silently persist an unconfirmed change.");
            }
            finally
            {
                if (hadPreference)
                    PlayerPrefs.SetInt(key, originalPreference);
                else
                    PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        [UnityTearDown]
        public IEnumerator LeavePlayModeAfterFailure()
        {
            if (EditorApplication.isPlaying)
            {
                yield return new ExitPlayMode();
            }
        }

        private static IEnumerator AssertFrameworkInitialization()
        {
            double deadline = EditorApplication.timeSinceStartup + 30;
            while ((!Maki.Initialized || !ORK.Initialized) && EditorApplication.timeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(Maki.Initialized, Is.True, "Makinom did not finish loading settings.");
            Assert.That(ORK.Initialized, Is.True, "ORK did not initialize.");
            // Reacquire references after the reload rather than retaining editor objects.
            var project = AssetDatabase.LoadAssetAtPath<MakinomProjectAsset>(ProjectPath);
            Assert.That(project, Is.Not.Null);
            Assert.That(Maki.Data.ProjectAsset, Is.SameAs(project));
            Assert.That(Maki.Game.Running, Is.False,
                "P0 only initializes the framework; player/start-game configuration comes later.");
            int frame = Time.frameCount;
            yield return null;
            yield return null;
            Assert.That(Time.frameCount, Is.GreaterThan(frame), "Play Mode frames did not advance.");
            // UTF fails on unexpected Error/Exception/Assert logs; do not clear or ignore them.
            LogAssert.NoUnexpectedReceived();
        }

        private static void InvokeUrpSetup(string methodName)
        {
            var type = Type.GetType("Rpg.Editor.Urp2DProjectSetup, Assembly-CSharp-Editor", true);
            try
            {
                type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            }
            catch (TargetInvocationException error) when (error.InnerException != null)
            {
                throw error.InnerException;
            }
        }

        private static void InvokeSoundwellBuildProfilesSetup(string methodName)
        {
            var type = Type.GetType("Rpg.Editor.SoundwellBuildProfilesSetup, Assembly-CSharp-Editor", true);
            try { type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static).Invoke(null, null); }
            catch (TargetInvocationException error) when (error.InnerException != null) { throw error.InnerException; }
        }

        private static void InvokeSoundwellLocalizationSetup(string methodName)
        {
            var type = Type.GetType("Rpg.Editor.SoundwellLocalizationSetup, Assembly-CSharp-Editor", true);
            try { type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static).Invoke(null, null); }
            catch (TargetInvocationException error) when (error.InnerException != null) { throw error.InnerException; }
        }

        private static void InvokeSoundwellAudioMixerSetup(string methodName)
        {
            var type = Type.GetType("Rpg.Editor.SoundwellAudioMixerSetup, Assembly-CSharp-Editor", true);
            try
            {
                type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            }
            catch (TargetInvocationException error) when (error.InnerException != null)
            {
                throw error.InnerException;
            }
        }

        private static void InvokeSoundwellSpriteAtlasSetup(string methodName)
        {
            var type = Type.GetType("Rpg.Editor.SoundwellSpriteAtlasSetup, Assembly-CSharp-Editor", true);
            try { type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static).Invoke(null, null); }
            catch (TargetInvocationException error) when (error.InnerException != null) { throw error.InnerException; }
        }

        private static void InvokeSoundwellTilemapSetup(string methodName)
        {
            var type = Type.GetType("Rpg.Editor.SoundwellTilemapSetup, Assembly-CSharp-Editor", true);
            try
            {
                type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            }
            catch (TargetInvocationException error) when (error.InnerException != null)
            {
                throw error.InnerException;
            }
        }

        private static void InvokeSoundwellAnimationSetup(string methodName)
        {
            var type = Type.GetType("Rpg.Editor.SoundwellAnimationSetup, Assembly-CSharp-Editor", true);
            try
            {
                type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            }
            catch (TargetInvocationException error) when (error.InnerException != null)
            {
                throw error.InnerException;
            }
        }

        private static void ValidateConfiguration()
        {
            // Tests are in an asmdef; the existing tool is in Assembly-CSharp-Editor.
            var type = Type.GetType("Rpg.Editor.OrkBootstrapSetup, Assembly-CSharp-Editor", true);
            try
            {
                type.GetMethod("ValidateBatch", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            }
            catch (TargetInvocationException error) when (error.InnerException != null)
            {
                throw error.InnerException;
            }
        }

        private static void CreateDefaultProject()
        {
            Assert.That(EditorApplication.isPlayingOrWillChangePlaymode, Is.False);
            Assert.That(Resources.FindObjectsOfTypeAll<MakinomEditorWindow>(), Is.Empty,
                "Close the framework editor before preparing defaults.");
            // Do not rebuild a partially configured project over existing game data.
            const string dataPath = "Assets/Gaming Is Love/_Data";
            if (Directory.Exists(dataPath))
            {
                Assert.That(Directory.GetFiles(dataPath, "*.asset", SearchOption.AllDirectories)
                    .Where(path => Path.GetFileName(path) != "EditorData.asset"), Is.Empty,
                    "Existing framework data needs manual review; refusing to replace it.");
            }

            var project = MakinomAssetHelper.LoadProjectAsset();
            Assert.That(AssetDatabase.GetAssetPath(project), Is.EqualTo(ProjectPath));
            Maki.Initialize(project);
            EditorDataHandler.DataHandlerLoaded();
            var window = ScriptableObject.CreateInstance<MakinomEditorWindow>();
            try
            {
                // The Instance getter opens a window. Only use its public setter here.
                MakinomEditorWindow.Instance = window;
                window.InitSections();
                new ProjectSaveHandler(window).SaveData();
                Assert.That(project.IsEmpty, Is.False);
                Assert.That(Maki.Data.ProjectAsset, Is.SameAs(project));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
