using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Tile-level player location on the current verified M05 landing.
    /// Position and its arrival identity are stored in the existing Makinom
    /// authority. The tile is never restored for a different M05 arrival,
    /// even if an interrupted voyage reset left the old tile behind.
    /// It does not assert landing, delivered letters or any visited NPC.
    /// </summary>
    public static class M05HarborPositionFacts
    {
        public const string PositionKey = "rpg.return.m05.harbor-player-tile";
        // ArrivalSequence + 1; zero is an unbound legacy position, not proof
        // that a tile belongs to the current voyage.
        public const string PositionArrivalIdentityKey =
            "rpg.return.m05.harbor-player-arrival-identity";

        public static bool TryRead(Func<string, int> getInt,
            RedSaltHarborMapLayout layout, out Vector2Int? tile)
        {
            tile = null;
            if (getInt == null || layout == null)
                return false;

            int encoded = getInt(PositionKey);
            int boundArrival = getInt(PositionArrivalIdentityKey);
            if (encoded < 0 || encoded > layout.Width * layout.Height ||
                boundArrival < 0)
                return false;
            if (encoded == 0) return true;

            if (!M05LandingFacts.TryRead(getInt, out M05LandingReport? landing) ||
                !landing.HasValue)
                return true;

            int arrivalSequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            if (arrivalSequence < 0 || arrivalSequence == int.MaxValue)
                return false;
            // Legacy positions lacking a binding are intentionally not
            // restored: keeping the player at the authored pier spawn is
            // safer than teleporting to an old voyage after a torn reset.
            if (boundArrival != arrivalSequence + 1)
                return true;

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

            int sequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            if (sequence < 0 || sequence == int.MaxValue)
                return false;
            int arrivalIdentity = sequence + 1;
            int encoded = 1 + tile.x + layout.Width * tile.y;
            if (getInt(PositionArrivalIdentityKey) != arrivalIdentity)
            {
                // Clear the old tile BEFORE rebinding its identity. If an
                // interrupted Makinom write throws at either step, the new
                // arrival can never read the previous voyage's position.
                setInt(PositionKey, 0);
                setInt(PositionArrivalIdentityKey, arrivalIdentity);
            }
            // Avoid writing on every frame spent in the same walked-to tile.
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
