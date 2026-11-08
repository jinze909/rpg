using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rpg.Editor
{
    internal static class OrkBootstrapSetup
    {
        private const string ScenePath = "Assets/Game/Scenes/Bootstrap.unity";
        private const string ProjectPath = "Assets/Gaming Is Love/_Data/Project.asset";
        private const string StarterName = "GamingIsLove.ORKFramework.Components.ORKGameStarter";

        [MenuItem("Tools/RPG/Wire ORK Bootstrap")]
        private static void WireBootstrap()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || scene.isDirty || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError($"Open and save {ScenePath} in Edit Mode before wiring ORK.");
                return;
            }

            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .SingleOrDefault(candidate => candidate.GetName().Name == "ORKFramework3");
            var starterType = assembly?.GetType(StarterName);
            var project = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ProjectPath);
            if (starterType == null || project == null)
            {
                Debug.LogError("Install ORK, then open its editor and save the project settings.");
                return;
            }

            var projectField = starterType.GetField("project");
            if (projectField == null || !projectField.FieldType.IsInstanceOfType(project))
            {
                Debug.LogError($"The installed ORK starter cannot use {ProjectPath}.");
                return;
            }

            var existing = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren(starterType, true))
                .ToArray();
            if (existing.Length > 0)
            {
                if (existing.Length == 1 && project.Equals(projectField.GetValue(existing[0])))
                {
                    Debug.Log("Bootstrap already uses the ORK project asset.");
                    return;
                }

                Debug.LogError("Bootstrap already contains an ORK starter with different wiring. Inspect it before continuing.");
                return;
            }

            var starterObject = new GameObject("ORK Game Starter");
            Undo.RegisterCreatedObjectUndo(starterObject, "Wire ORK Bootstrap");
            var starter = Undo.AddComponent(starterObject, starterType);
            var serializedStarter = new SerializedObject(starter);
            serializedStarter.FindProperty("project").objectReferenceValue = project;
            serializedStarter.ApplyModifiedProperties();

            if (EditorSceneManager.SaveScene(scene))
            {
                Debug.Log("Saved the ORK starter. Configure a player and start schematic before enabling Start Game.");
            }
            else
            {
                Debug.LogError("The ORK starter was added but the scene could not be saved.");
            }
        }
    }
}
