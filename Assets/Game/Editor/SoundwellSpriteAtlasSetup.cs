// Game-owned M7 Sprite Atlas V2. ORK/Makinom assets remain untouched.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
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
    public static class SoundwellSpriteAtlasSetup
    {
        private const string Root = "Assets/Game/Resources/Soundwell/Tiles";
        private const string AtlasPath = Root + "/SoundwellWalls.spriteatlasv2";
        private const string RulePath = Root + "/GratingWall.asset";

        // CI and the explicit P0 validation entrypoint invoke Validate().
        // A project-wide build interceptor would block unrelated
        // builds when Soundwell assets have not been imported yet.

        public static void Prepare()
        {
            if (EditorSettings.spritePackerMode != SpritePackerMode.SpriteAtlasV2)
                EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
            if (EditorSettings.spritePackerMode != SpritePackerMode.SpriteAtlasV2)
                throw new InvalidOperationException("Unity did not enable Sprite Atlas V2 packing.");

            if (!File.Exists(AtlasPath))
            {
                var (baseSprite, joinedSprite) = SoundwellTilemapSetup.LoadWallSprites();
                if (!AssetDatabase.IsValidFolder(Root))
                    throw new InvalidOperationException("The saved M5 wall textures folder is missing.");
                var authoring = new SpriteAtlasAsset();
                authoring.Add(new UnityEngine.Object[] { baseSprite, joinedSprite });
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
                texture.readable = true;
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
                texture.filterMode != FilterMode.Point || texture.generateMipMaps || !texture.readable)
                throw new InvalidOperationException("Atlas settings would distort the saved 16-pixel wall sprites.");

            var (baseSprite, joinedSprite) = SoundwellTilemapSetup.LoadWallSprites();
            var dependencies = AssetDatabase.GetDependencies(AtlasPath, true);
            if (!dependencies.Contains(SoundwellTilemapSetup.WallSpritePath))
                throw new InvalidOperationException("The wall atlas must pack the authored Soundwell wall sprites.");

            var rule = AssetDatabase.LoadAssetAtPath<RuleTile>(RulePath);
            if (rule == null || rule.m_DefaultSprite != baseSprite ||
                rule.m_TilingRules.Count != 1 || rule.m_TilingRules[0].m_Sprites.Length != 1 ||
                rule.m_TilingRules[0].m_Sprites[0] != joinedSprite)
                throw new InvalidOperationException("The wall RuleTile and atlas must use the two authored wall sprites.");
            Debug.Log("RPG_SOUNDWELL_SPRITE_ATLAS_VALIDATED: authored 16x16 walls, one saved wall RuleTile, readable V2 atlas.");
        }
    }
}
