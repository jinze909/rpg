using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Rpg.Editor
{
    /// <summary>
    /// Creates one Unity-generated URP 2D pipeline and makes every quality
    /// level inherit it. Run only from the isolated URP migration candidate.
    /// </summary>
    public static class Urp2DProjectSetup
    {
        private const string FolderPath = "Assets/Game/Rendering/URP2D";
        private const string RendererPath = FolderPath + "/Renderer2DData.asset";
        private const string PipelinePath = FolderPath + "/UniversalRenderPipelineAsset.asset";

        public static void Prepare()
        {
            if (Application.unityVersion != "6000.6.8f1")
                throw new InvalidOperationException("URP preparation is pinned to Unity 6000.6.8f1.");

            EnsureFolders();
            var renderer = LoadOrCreateRenderer();
            var pipeline = LoadOrCreatePipeline(renderer);
            var pipelineGuids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { "Assets" });
            if (pipelineGuids.Length != 1 ||
                AssetDatabase.GUIDToAssetPath(pipelineGuids[0]) != PipelinePath)
                throw new InvalidOperationException(
                    "The project must have exactly one project-owned URP pipeline asset.");

            GraphicsSettings.defaultRenderPipeline = pipeline;
            ClearQualityOverrides();
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Validate();
            Debug.Log("URP 2D prepared: " + PipelinePath);
        }

        public static void Validate()
        {
            var pipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null)
                throw new InvalidOperationException("Graphics Settings does not reference the saved URP asset.");

            var renderer = pipeline.scriptableRenderer;
            if (renderer == null || renderer.GetType().Name != "Renderer2D")
                throw new InvalidOperationException("The active URP asset must use Renderer2D.");

            if (AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath) == null ||
                AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath) != pipeline)
                throw new InvalidOperationException("The saved URP 2D assets are missing or mismatched.");

            var rendererList = new SerializedObject(pipeline).FindProperty("m_RendererDataList");
            if (rendererList == null || rendererList.arraySize != 1 ||
                rendererList.GetArrayElementAtIndex(0).objectReferenceValue == null ||
                rendererList.GetArrayElementAtIndex(0).objectReferenceValue.GetType() != typeof(Renderer2DData))
                throw new InvalidOperationException("URP must contain exactly one 2D Renderer Data asset.");

            var pipelineGuids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { "Assets" });
            if (pipelineGuids.Length != 1 ||
                AssetDatabase.GUIDToAssetPath(pipelineGuids[0]) != PipelinePath)
                throw new InvalidOperationException("A second project-owned URP pipeline asset was found.");

            int originalQuality = QualitySettings.GetQualityLevel();
            try
            {
                for (int i = 0; i < QualitySettings.names.Length; i++)
                {
                    QualitySettings.SetQualityLevel(i, true);
                    if (QualitySettings.renderPipeline != null ||
                        GraphicsSettings.currentRenderPipeline != pipeline)
                        throw new InvalidOperationException(
                            "Quality level " + QualitySettings.names[i] +
                            " overrides the single global URP 2D pipeline.");
                }
            }
            finally
            {
                QualitySettings.SetQualityLevel(originalQuality, true);
            }
        }

        private static Renderer2DData LoadOrCreateRenderer()
        {
            var existing = AssetDatabase.LoadMainAssetAtPath(RendererPath);
            if (existing != null)
            {
                if (!(existing is Renderer2DData typed))
                    throw new InvalidOperationException(RendererPath + " is occupied by a non-2D renderer asset.");
                return typed;
            }

            var renderer = ScriptableObject.CreateInstance<Renderer2DData>();
            renderer.name = "Rpg2DRenderer";
            AssetDatabase.CreateAsset(renderer, RendererPath);
            return renderer;
        }

        private static UniversalRenderPipelineAsset LoadOrCreatePipeline(Renderer2DData renderer)
        {
            var existing = AssetDatabase.LoadMainAssetAtPath(PipelinePath);
            if (existing != null)
            {
                if (!(existing is UniversalRenderPipelineAsset typed))
                    throw new InvalidOperationException(PipelinePath + " is occupied by a non-URP asset.");
                var serialized = new SerializedObject(typed);
                var renderers = serialized.FindProperty("m_RendererDataList");
                var defaultIndex = serialized.FindProperty("m_DefaultRendererIndex");
                if (renderers == null || defaultIndex == null)
                    throw new InvalidOperationException("URP renderer-list serialization changed; refusing unsafe setup.");
                renderers.arraySize = 1;
                renderers.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
                defaultIndex.intValue = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return typed;
            }

            var pipeline = UniversalRenderPipelineAsset.Create(renderer);
            if (pipeline == null)
                throw new InvalidOperationException("Unity failed to create the URP asset for Renderer2D.");
            pipeline.name = "Rpg2DPipeline";
            AssetDatabase.CreateAsset(pipeline, PipelinePath);
            return pipeline;
        }

        private static void ClearQualityOverrides()
        {
            int originalQuality = QualitySettings.GetQualityLevel();
            try
            {
                for (int i = 0; i < QualitySettings.names.Length; i++)
                {
                    QualitySettings.SetQualityLevel(i, true);
                    QualitySettings.renderPipeline = null;
                }
            }
            finally
            {
                QualitySettings.SetQualityLevel(originalQuality, true);
            }
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Game/Rendering"))
                AssetDatabase.CreateFolder("Assets/Game", "Rendering");
            if (!AssetDatabase.IsValidFolder(FolderPath))
                AssetDatabase.CreateFolder("Assets/Game/Rendering", "URP2D");
        }
    }
}
