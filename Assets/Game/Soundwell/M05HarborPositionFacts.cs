using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Tile-level player location on the current verified M05 landing.
    /// A single Makinom integer avoids partially saved x/y coordinates.
    /// Zero means no position was observed yet; every nonzero value must
    /// identify a walkable tile in the currently authored harbor layout.
    /// It does not assert landing, delivered letters or any visited NPC.
    /// </summary>
    public static class M05HarborPositionFacts
    {
        public const string PositionKey = "rpg.return.m05.harbor-player-tile";

        public static bool TryRead(Func<string, int> getInt,
            RedSaltHarborMapLayout layout, out Vector2Int? tile)
        {
            tile = null;
            if (getInt == null || layout == null)
                return false;

            int encoded = getInt(PositionKey);
            if (encoded == 0) return true;
            if (encoded < 0 || encoded > layout.Width * layout.Height)
                return false;

            int offset = encoded - 1;
            var location = new Vector2Int(
                offset % layout.Width, offset / layout.Width);
            if (!IsWalkable(layout, location))
                return false;
            tile = location;
            return true;
        }

        public static bool TryRecord(Vector2Int tile, RedSaltHarborMapLayout layout,
            Func<string, int> getInt, Action<string, int> setInt)
        {
            if (getInt == null || setInt == null ||
                !IsWalkable(layout, tile) ||
                !M05LandingFacts.TryRead(getInt, out M05LandingReport? savedLanding) ||
                !savedLanding.HasValue)
                return false;

            int encoded = 1 + tile.x + layout.Width * tile.y;
            // A player walking in the same tile does not write hundreds of
            // Makinom updates per second. Real movement can replace a corrupt
            // old position because only the observed walked-to tile is saved.
            if (getInt(PositionKey) != encoded)
                setInt(PositionKey, encoded);
            return true;
        }

        private static bool IsWalkable(RedSaltHarborMapLayout layout, Vector2Int tile)
        {
            return layout != null && tile.x >= 0 && tile.x < layout.Width &&
                tile.y >= 0 && tile.y < layout.Height &&
                RedSaltHarborMapLayout.IsWalkable(layout.Tiles[tile.x, tile.y]);
        }
    }
}
