using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rpg.Editor
{
    public static class OrkBootstrapSetup
    {
        private const string ScenePath = "Assets/Game/Scenes/Bootstrap.unity";
        private const string ProjectPath = "Assets/Gaming Is Love/_Data/Project.asset";
        private const string StarterName = "GamingIsLove.ORKFramework.Components.ORKGameStarter";

        [MenuItem("Tools/RPG/Wire ORK Bootstrap")]
        private static void WireBootstrap()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError($"Open {ScenePath} in Edit Mode before wiring ORK.");
                return;
            }

            try
            {
                RequireFullPlayModeReload();
                var starterType = FindStarterType();
                var project = LoadSavedProject(starterType);
                var starters = FindStarters(scene, starterType);
                if (starters.Length != 0)
                {
                    ValidateStarter(starters, project);
                    Debug.Log("Bootstrap already uses the saved ORK project asset.");
                    return;
                }

                // Validation above is read-only. Protect unsaved edits only before writing the scene.
                if (scene.isDirty)
                {
                    throw new InvalidOperationException($"Save {ScenePath} before adding the ORK Game Starter.");
                }

                Undo.IncrementCurrentGroup();
                int undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Wire ORK Bootstrap");
                try
                {
                    var starterObject = new GameObject("ORK Game Starter");
                    Undo.RegisterCreatedObjectUndo(starterObject, "Wire ORK Bootstrap");
                    var starter = Undo.AddComponent(starterObject, starterType);
                    var settings = new SerializedObject(starter);
                    settings.FindProperty("project").objectReferenceValue = project;
                    settings.ApplyModifiedProperties();
                    ValidateStarter(FindStarters(scene, starterType), project);

                    if (!EditorSceneManager.SaveScene(scene))
                    {
                        throw new InvalidOperationException($"Could not save {ScenePath}.");
                    }

                    Undo.CollapseUndoOperations(undoGroup);
                }
                catch
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                    throw;
                }

                Debug.Log("Saved the ORK starter. Configure a player and start schematic before enabling Start Game.");
            }
            catch (InvalidOperationException error)
            {
                Debug.LogError(error.Message);
            }
        }

        // Invoked by Unity with -batchmode -quit -executeMethod.
        public static void ValidateBatch()
        {
            RequireFullPlayModeReload();

            var firstScene = EditorBuildSettings.scenes.FirstOrDefault(scene => scene.enabled);
            if (firstScene == null || firstScene.path != ScenePath)
            {
                throw new InvalidOperationException($"{ScenePath} must be the first enabled build scene.");
            }

            var starterType = FindStarterType();
            var project = LoadSavedProject(starterType);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ValidateStarter(FindStarters(scene, starterType), project);
            Debug.Log("RPG_BOOTSTRAP_VALIDATED");
        }

        private static void RequireFullPlayModeReload()
        {
            const EnterPlayModeOptions disabledReloads =
                EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
            if ((EditorSettings.enterPlayModeOptions & disabledReloads) != 0)
            {
                throw new InvalidOperationException(
                    "For the ORK 3 bootstrap, set Edit > Project Settings > Editor > When entering play mode to Reload Domain and Scene.");
            }
        }

        private static Type FindStarterType()
        {
            var starterType = TypeCache.GetTypesDerivedFrom<MonoBehaviour>()
                .SingleOrDefault(type => type.FullName == StarterName && type.Assembly.GetName().Name == "ORKFramework3");
            return starterType ?? throw new InvalidOperationException("Install the pinned ORK package and let Unity compile.");
        }

        private static ScriptableObject LoadSavedProject(Type starterType)
        {
            var project = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ProjectPath);
            var projectType = starterType.GetField("project")?.FieldType;
            if (project == null || projectType == null || !projectType.IsInstanceOfType(project))
            {
                throw new InvalidOperationException($"Save the framework settings in the ORK editor to create {ProjectPath}.");
            }

            var isEmpty = projectType.GetProperty("IsEmpty")?.GetValue(project);
            if (!(isEmpty is bool empty) || empty)
            {
                throw new InvalidOperationException($"Save non-empty framework settings in the ORK editor before using {ProjectPath}.");
            }

            return project;
        }

        private static Component[] FindStarters(Scene scene, Type starterType)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren(starterType, true))
                .ToArray();
        }

        private static void ValidateStarter(Component[] starters, ScriptableObject project)
        {
            if (starters.Length != 1 || !(starters[0] is Behaviour starter) || !starter.enabled || !starter.gameObject.activeInHierarchy)
            {
                throw new InvalidOperationException("Bootstrap must contain exactly one active, enabled ORK Game Starter.");
            }

            var settings = new SerializedObject(starter);
            if (settings.FindProperty("useAssetBundle").boolValue ||
                settings.FindProperty("project").objectReferenceValue != project)
            {
                throw new InvalidOperationException($"The ORK Game Starter must use the saved project asset at {ProjectPath}.");
            }
        }
    }
}
