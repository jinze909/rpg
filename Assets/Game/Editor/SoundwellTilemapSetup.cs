using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Rpg.Editor
{
    /// <summary>
    /// M5: Author the Soundwell's real solid walls as a SAVED scene Tilemap.
    /// Unity Editor creates the RuleTile, texture/sprite imports, collider,
    /// and Scene YAML; clean verification reads all of them without repairs.
    /// The central gate remains the only runtime-controlled BoxCollider2D.
    /// </summary>
    public static class SoundwellTilemapSetup
    {
        private const string ScenePath = "Assets/Game/Scenes/SoundwellTrial.unity";
        private const string Root = "Assets/Game/Resources/Soundwell/Tiles";
        private const string RulePath = Root + "/GratingWall.asset";
        private const string GridName = "Soundwell solid Tilemap";
        private const float Cell = 0.2f;

        public static void Prepare()
        {
            var existing = AssetDatabase.LoadAssetAtPath<RuleTile>(RulePath);
            if (existing != null)
            {
                Validate();
                return;
            }
            if (AssetDatabase.IsValidFolder(Root) ||
                File.Exists(Root + "/GratingBase.png") ||
                File.Exists(Root + "/GratingJoined.png"))
                throw new InvalidOperationException("Partial M5 wall assets found; refusing to overwrite.");

            if (!AssetDatabase.IsValidFolder("Assets/Game/Resources/Soundwell"))
                throw new InvalidOperationException("M4 game-owned Resources path is missing.");
            AssetDatabase.CreateFolder("Assets/Game/Resources/Soundwell", "Tiles");
            Sprite baseSprite = CreateSprite("GratingBase", false);
            Sprite joinedSprite = CreateSprite("GratingJoined", true);
            var rule = ScriptableObject.CreateInstance<RuleTile>();
            rule.name = "Soundwell Grating Rule Tile";
            rule.m_DefaultSprite = baseSprite;
            rule.m_DefaultColliderType = Tile.ColliderType.Grid;

            // A continuous run receives the joined surface. Isolated/corner
            // cells fall back to the ordinary grating. Both remain solid.
            var joined = new RuleTile.TilingRule
            {
                m_Sprites = new[] { joinedSprite },
                m_ColliderType = Tile.ColliderType.Grid,
                m_Output = RuleTile.TilingRuleOutput.OutputSprite.Single,
            };
            joined.ApplyNeighbors(new Dictionary<Vector3Int, int>
            {
                { Vector3Int.left, RuleTile.TilingRuleOutput.Neighbor.This },
                { Vector3Int.right, RuleTile.TilingRuleOutput.Neighbor.This },
            });
            rule.m_TilingRules.Add(joined);
            AssetDatabase.CreateAsset(rule, RulePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                if (scene.GetRootGameObjects().Any(go => go.name == GridName))
                    throw new InvalidOperationException("The Soundwell scene already contains a wall Tilemap.");

                var gridRoot = new GameObject(GridName, typeof(Grid));
                SceneManager.MoveGameObjectToScene(gridRoot, scene);
                gridRoot.GetComponent<Grid>().cellSize = new Vector3(Cell, Cell, 1f);
                var mapRoot = new GameObject("Solid walls", typeof(Tilemap),
                    typeof(TilemapRenderer), typeof(TilemapCollider2D));
                mapRoot.transform.SetParent(gridRoot.transform, false);
                var map = mapRoot.GetComponent<Tilemap>();
                map.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
                var renderer = mapRoot.GetComponent<TilemapRenderer>();
                renderer.sortingLayerName = "Default";
                renderer.sortingOrder = 0;
                var tile = AssetDatabase.LoadAssetAtPath<RuleTile>(RulePath);

                // These are the eight existing physical wall rectangles.
                // The controlled central gate is intentionally NOT tiled.
                Fill(map, tile, new Vector2(0.25f, 2f), new Vector2(0.32f, 2.4f));
                Fill(map, tile, new Vector2(0.25f, -2f), new Vector2(0.32f, 2.4f));
                Fill(map, tile, new Vector2(-7f, 0f), new Vector2(0.32f, 6.4f));
                Fill(map, tile, new Vector2(-3.4f, 3.2f), new Vector2(7.3f, 0.28f));
                Fill(map, tile, new Vector2(-3.4f, -3.2f), new Vector2(7.3f, 0.28f));
                Fill(map, tile, new Vector2(7f, 0f), new Vector2(0.32f, 6.4f));
                Fill(map, tile, new Vector2(3.625f, 3.2f), new Vector2(6.75f, 0.28f));
                Fill(map, tile, new Vector2(3.625f, -3.2f), new Vector2(6.75f, 0.28f));
                map.CompressBounds();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException("Unity did not save the authored Soundwell Tilemap.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
            Validate();
        }

        public static void Validate()
        {
            var rule = AssetDatabase.LoadAssetAtPath<RuleTile>(RulePath);
            if (rule == null || rule.m_DefaultSprite == null ||
                rule.m_DefaultColliderType != Tile.ColliderType.Grid ||
                rule.m_TilingRules.Count != 1 ||
                rule.m_TilingRules[0].m_Sprites.Length != 1 ||
                rule.m_TilingRules[0].m_Sprites[0] == null)
                throw new InvalidOperationException("M5 must have a persisted, configured solid Rule Tile.");
            if (AssetDatabase.GetAssetPath(rule.m_DefaultSprite) != Root + "/GratingBase.png" ||
                AssetDatabase.GetAssetPath(rule.m_TilingRules[0].m_Sprites[0]) != Root + "/GratingJoined.png")
                throw new InvalidOperationException("Rule Tile references an unsaved or foreign sprite.");

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects().Where(go => go.name == GridName).ToArray();
                if (roots.Length != 1 || roots[0].GetComponent<Grid>() == null)
                    throw new InvalidOperationException("The saved Soundwell scene lacks one Grid.");
                var map = roots[0].GetComponentInChildren<Tilemap>();
                var render = map != null ? map.GetComponent<TilemapRenderer>() : null;
                var collider = map != null ? map.GetComponent<TilemapCollider2D>() : null;
                if (map == null || render == null || collider == null ||
                    render.sortingLayerName != "Default" ||
                    Mathf.Abs(roots[0].GetComponent<Grid>().cellSize.x - Cell) > 0.001f)
                    throw new InvalidOperationException("M5 wall Tilemap renderer/collider is invalid.");

                Check(map, new Vector3Int(-35, 0, 0), rule, true);
                Check(map, new Vector3Int(35, 0, 0), rule, true);
                Check(map, new Vector3Int(1, 9, 0), rule, true);
                Check(map, new Vector3Int(1, -10, 0), rule, true);
                Check(map, new Vector3Int(1, 0, 0), rule, false);
                if (map.GetUsedTilesCount() != 1)
                    throw new InvalidOperationException("Only the authored M5 Rule Tile may occupy solid walls.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
            Debug.Log("RPG_SOUNDWELL_TILEMAP_VALIDATED");
        }

        private static void Check(Tilemap map, Vector3Int cell, RuleTile rule, bool occupied)
        {
            bool matches = map.GetTile(cell) == rule;
            if (matches != occupied)
                throw new InvalidOperationException("M5 solid wall or controlled opening differs at " + cell);
        }

        private static void Fill(Tilemap map, RuleTile tile, Vector2 center, Vector2 size)
        {
            int left = Mathf.CeilToInt((center.x - size.x / 2f) / Cell - 0.5f);
            int right = Mathf.FloorToInt((center.x + size.x / 2f) / Cell - 0.5f);
            int bottom = Mathf.CeilToInt((center.y - size.y / 2f) / Cell - 0.5f);
            int top = Mathf.FloorToInt((center.y + size.y / 2f) / Cell - 0.5f);
            for (int x = left; x <= right; x++)
                for (int y = bottom; y <= top; y++)
                    map.SetTile(new Vector3Int(x, y, 0), tile);
        }

        private static Sprite CreateSprite(string name, bool joined)
        {
            const int side = 16;
            var texture = new Texture2D(side, side, TextureFormat.RGBA32, false);
            for (int y = 0; y < side; y++)
            {
                for (int x = 0; x < side; x++)
                {
                    bool metal = x == 0 || y == 0 || y == side - 1 ||
                        (joined ? y == 7 || y == 8 : x == 7 || x == 8);
                    texture.SetPixel(x, y, metal ?
                        new Color(0.38f, 0.53f, 0.57f) :
                        new Color(0.23f, 0.34f, 0.40f));
                }
            }
            texture.Apply();
            string path = Root + "/" + name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = side;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                throw new InvalidOperationException("Failed to import game-owned M5 wall sprite: " + path);
            return sprite;
        }
    }
}
