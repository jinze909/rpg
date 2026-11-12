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

        // The authored eight wall segments are the single map footprint contract.
        // The saved scene is independently checked against every occupied cell;
        // a few successful sample cells cannot prove the gate/corridors are clear.
        private static readonly (Vector2 Center, Vector2 Size)[] WallSegments =
        {
            (new Vector2(0.25f, 2f), new Vector2(0.32f, 2.4f)),
            (new Vector2(0.25f, -2f), new Vector2(0.32f, 2.4f)),
            (new Vector2(-7f, 0f), new Vector2(0.32f, 6.4f)),
            (new Vector2(-3.4f, 3.2f), new Vector2(7.3f, 0.28f)),
            (new Vector2(-3.4f, -3.2f), new Vector2(7.3f, 0.28f)),
            (new Vector2(7f, 0f), new Vector2(0.32f, 6.4f)),
            (new Vector2(3.625f, 3.2f), new Vector2(6.75f, 0.28f)),
            (new Vector2(3.625f, -3.2f), new Vector2(6.75f, 0.28f)),
        };

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
                foreach (var wall in WallSegments)
                    Fill(map, tile, wall.Center, wall.Size);
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

            // A saved wall can contain every correct cell yet become
            // non-solid or blurry when its joined rule/sprite import drifts.
            // Verify the actual authored M5 visuals/collision settings in
            // clean import, not only the rule's outer asset reference.
            var joinedRule = rule.m_TilingRules[0];
            if (joinedRule.m_ColliderType != Tile.ColliderType.Grid ||
                joinedRule.m_Output != RuleTile.TilingRuleOutput.OutputSprite.Single)
                throw new InvalidOperationException(
                    "M5 joined wall rule must remain one solid sprite.");
            VerifyPixelWallSprite(rule.m_DefaultSprite, Root + "/GratingBase.png");
            VerifyPixelWallSprite(joinedRule.m_Sprites[0], Root + "/GratingJoined.png");

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects().Where(go => go.name == GridName).ToArray();
                if (roots.Length != 1 || roots[0].GetComponent<Grid>() == null)
                    throw new InvalidOperationException("The saved Soundwell scene lacks one Grid.");
                var maps = roots[0].GetComponentsInChildren<Tilemap>(true);
                var map = maps.Length == 1 ? maps[0] : null;
                var render = map != null ? map.GetComponent<TilemapRenderer>() : null;
                var collider = map != null ? map.GetComponent<TilemapCollider2D>() : null;
                var grid = roots[0].GetComponent<Grid>();
                if (map == null || render == null || collider == null ||
                    !roots[0].activeSelf || !map.gameObject.activeSelf ||
                    !render.enabled || !collider.enabled || collider.isTrigger ||
                    render.sortingLayerName != "Default" || render.sortingOrder != 0 ||
                    Mathf.Abs(grid.cellSize.x - Cell) > 0.001f ||
                    Mathf.Abs(grid.cellSize.y - Cell) > 0.001f ||
                    Mathf.Abs(grid.cellSize.z - 1f) > 0.001f ||
                    grid.cellGap.sqrMagnitude > 0.000001f ||
                    collider.offset.sqrMagnitude > 0.000001f)
                    throw new InvalidOperationException("M5 wall Tilemap renderer/solid collider is invalid.");

                // Cell occupancy is not enough: moving the Grid or child
                // Tilemap leaves all cells intact but moves walls and their
                // collisions across the rescue gate/escape lane.
                var rootTransform = roots[0].transform;
                var mapTransform = map.transform;
                if (rootTransform.position.sqrMagnitude > 0.000001f ||
                    Quaternion.Angle(rootTransform.rotation, Quaternion.identity) > 0.01f ||
                    (rootTransform.localScale - Vector3.one).sqrMagnitude > 0.000001f ||
                    mapTransform.localPosition.sqrMagnitude > 0.000001f ||
                    Quaternion.Angle(mapTransform.localRotation, Quaternion.identity) > 0.01f ||
                    (mapTransform.localScale - Vector3.one).sqrMagnitude > 0.000001f ||
                    (map.tileAnchor - new Vector3(0.5f, 0.5f, 0f)).sqrMagnitude > 0.000001f)
                    throw new InvalidOperationException(
                        "M5 wall Grid/Tilemap transform or tile anchor shifts saved collision geometry.");

                // Compare every saved cell with the intended physical footprint.
                // This rejects holes, stray walls across the gate or escape lane,
                // and swapped tiles outside the five legacy sample positions.
                var missing = new HashSet<Vector3Int>();
                foreach (var wall in WallSegments)
                    foreach (var position in Cells(wall.Center, wall.Size))
                        missing.Add(position);
                foreach (var position in map.cellBounds.allPositionsWithin)
                {
                    var saved = map.GetTile(position);
                    if (saved == null)
                        continue;
                    if (saved != rule || !missing.Remove(position))
                        throw new InvalidOperationException(
                            "M5 saved Tilemap contains an unexpected solid cell at " + position);
                }
                if (missing.Count != 0)
                    throw new InvalidOperationException(
                        "M5 saved Tilemap is missing authored solid cells, first: " + missing.First());
                if (map.GetUsedTilesCount() != 1)
                    throw new InvalidOperationException("Only the authored M5 Rule Tile may occupy solid walls.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
            Debug.Log("RPG_SOUNDWELL_TILEMAP_VALIDATED");
        }

        private static void VerifyPixelWallSprite(Sprite sprite, string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (sprite == null || importer == null ||
                sprite.rect.size != new Vector2(16f, 16f) ||
                Mathf.Abs(sprite.pixelsPerUnit - 16f) > 0.001f ||
                sprite.texture == null || sprite.texture.filterMode != FilterMode.Point ||
                importer.textureType != TextureImporterType.Sprite ||
                importer.spritePixelsPerUnit != 16f ||
                importer.filterMode != FilterMode.Point ||
                importer.textureCompression != TextureImporterCompression.Uncompressed ||
                importer.mipmapEnabled)
                throw new InvalidOperationException(
                    "M5 wall sprites must remain authored 16px point-filtered, no-mipmap assets: " +
                    assetPath);
        }

        private static IEnumerable<Vector3Int> Cells(Vector2 center, Vector2 size)
        {
            int left = Mathf.CeilToInt((center.x - size.x / 2f) / Cell - 0.5f);
            int right = Mathf.FloorToInt((center.x + size.x / 2f) / Cell - 0.5f);
            int bottom = Mathf.CeilToInt((center.y - size.y / 2f) / Cell - 0.5f);
            int top = Mathf.FloorToInt((center.y + size.y / 2f) / Cell - 0.5f);
            for (int x = left; x <= right; x++)
                for (int y = bottom; y <= top; y++)
                    yield return new Vector3Int(x, y, 0);
        }

        private static void Fill(Tilemap map, RuleTile tile, Vector2 center, Vector2 size)
        {
            foreach (var cell in Cells(center, size))
                map.SetTile(cell, tile);
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
