// Game-owned M7 Sprite Atlas V2. ORK/Makinom assets remain untouched.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rpg.Editor
{
    /// <summary>
    /// Packs the two saved wall sprites actually used by Soundwell's RuleTile.
    /// Prepare authors the V2 importer once; every subsequent clean build only
    /// validates the persisted atlas and its source GUID dependencies.
    /// </summary>
    public sealed class SoundwellSpriteAtlasSetup : IPreprocessBuildWithReport
    {
        private const string Root = "Assets/Game/Resources/Soundwell/Tiles";
        private const string AtlasPath = Root + "/SoundwellWalls.spriteatlasv2";
        private const string RulePath = Root + "/GratingWall.asset";
        private static readonly string[] TexturePaths =
        {
            Root + "/GratingBase.png",
            Root + "/GratingJoined.png"
        };

        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            // Do not generate untracked assets in a clean verify build.
            // The P0 prepare/publish workflow owns asset authoring and writeback.
            Validate();
        }

        public static void Prepare()
        {
            if (EditorSettings.spritePackerMode != SpritePackerMode.SpriteAtlasV2)
                EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
            if (EditorSettings.spritePackerMode != SpritePackerMode.SpriteAtlasV2)
                throw new InvalidOperationException("Unity did not enable Sprite Atlas V2 packing.");

            if (!File.Exists(AtlasPath))
            {
                var sprites = LoadSources();
                if (!AssetDatabase.IsValidFolder(Root))
                    throw new InvalidOperationException("The saved M5 wall textures folder is missing.");
                var authoring = new SpriteAtlasAsset();
                authoring.Add(sprites.Cast<UnityEngine.Object>().ToArray());
                SpriteAtlasAsset.Save(authoring, AtlasPath);
                AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport);

                var importer = AssetImporter.GetAtPath(AtlasPath) as SpriteAtlasImporter;
                if (importer == null)
                    throw new InvalidOperationException("Unity failed to import a V2 SpriteAtlasImporter.");
                importer.includeInBuild = true;
                var packing = importer.packingSettings;
                packing.padding = 2;
                packing.enableRotation = false;
                packing.enableTightPacking = false;
                packing.enableAlphaDilation = false;
                importer.packingSettings = packing;
                var texture = importer.textureSettings;
                texture.filterMode = FilterMode.Point;
                texture.generateMipMaps = false;
                texture.sRGB = true;
                importer.textureSettings = texture;
                importer.SaveAndReimport();
                AssetDatabase.SaveAssets();
            }
            Validate();
        }

        public static void Validate()
        {
            if (EditorSettings.spritePackerMode != SpritePackerMode.SpriteAtlasV2)
                throw new InvalidOperationException("Sprite Packer is not in V2 mode.");
            if (!File.Exists(AtlasPath) || SpriteAtlasAsset.Load(AtlasPath) == null)
                throw new InvalidOperationException("The committed Sprite Atlas V2 asset is missing.");
            var importer = AssetImporter.GetAtPath(AtlasPath) as SpriteAtlasImporter;
            if (importer == null || !importer.includeInBuild)
                throw new InvalidOperationException("The wall atlas must use V2 and be included in the build.");

            var packing = importer.packingSettings;
            var texture = importer.textureSettings;
            if (packing.enableRotation || packing.enableTightPacking || packing.padding != 2 ||
                texture.filterMode != FilterMode.Point || texture.generateMipMaps)
                throw new InvalidOperationException("Atlas settings would distort the saved 16-pixel wall sprites.");

            var sprites = LoadSources();
            var dependencies = AssetDatabase.GetDependencies(AtlasPath, true);
            foreach (string path in TexturePaths)
            {
                if (!dependencies.Contains(path))
                    throw new InvalidOperationException("The V2 atlas lost its source sprite: " + path);
            }
            if (dependencies.Count(path => TexturePaths.Contains(path)) != 2)
                throw new InvalidOperationException("Atlas source selection is not the two real M5 tiles.");

            var rule = AssetDatabase.LoadAssetAtPath<RuleTile>(RulePath);
            if (rule == null || rule.m_DefaultSprite != sprites[0] ||
                rule.m_TilingRules.Count != 1 || rule.m_TilingRules[0].m_Sprites.Length != 1 ||
                rule.m_TilingRules[0].m_Sprites[0] != sprites[1])
                throw new InvalidOperationException("The M5 RuleTile must still reference both original sprite GUIDs.");
            Debug.Log("RPG_SOUNDWELL_SPRITE_ATLAS_VALIDATED: two original 16x16 sprites, one saved wall RuleTile, V2 included in build.");
        }

        private static Sprite[] LoadSources()
        {
            var sprites = TexturePaths.Select(path => AssetDatabase.LoadAssetAtPath<Sprite>(path)).ToArray();
            for (int i = 0; i < TexturePaths.Length; i++)
            {
                string path = TexturePaths[i];
                var sprite = sprites[i];
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                // The V2 atlas filter alone cannot repair a source imported at
                // the wrong PPU, with lossy compression, or with mipmaps.
                // Reject these changes before the trial builds an unreadable
                // or incorrectly scaled pixel-art wall.
                if (sprite == null || sprite.rect.width != 16 || sprite.rect.height != 16 ||
                    Mathf.Abs(sprite.pixelsPerUnit - 16f) > 0.001f ||
                    importer == null || importer.textureType != TextureImporterType.Sprite ||
                    Mathf.Abs(importer.spritePixelsPerUnit - 16f) > 0.001f ||
                    importer.filterMode != FilterMode.Point ||
                    importer.textureCompression != TextureImporterCompression.Uncompressed ||
                    importer.mipmapEnabled)
                    throw new InvalidOperationException(
                        "M7 source sprite must remain 16x16, 16 PPU, point-filtered, uncompressed and mip-free: " + path);
            }
            return sprites;
        }
    }
}
