using System;

namespace Rpg.Soundwell
{
    /// <summary>
    /// The three M05 map conversations actually witnessed on this voyage.
    /// The same Makinom integer store as landing/letters owns the record.
    /// Never assume an unvisited scene, a corrupt bitmask, or an unverified
    /// arrival is complete just because the map UI is reconstructed.
    /// </summary>
    public static class M05HarborVisitFacts
    {
        public const string VisitedMaskKey = "rpg.return.m05.harbor-beats-observed";
        public const int AllBeatsMask = 7;

        public static bool TryRead(Func<string, int> getInt, out int mask)
        {
            mask = 0;
            if (getInt == null)
                return false;
            int saved = getInt(VisitedMaskKey);
            if (saved < 0 || (saved & ~AllBeatsMask) != 0)
                return false;
            mask = saved;
            return true;
        }

        public static bool Contains(int mask, M05HarborBeat beat)
        {
            return beat >= M05HarborBeat.RepairStall &&
                beat < M05HarborBeat.Finished &&
                (mask & (1 << (int)beat)) != 0;
        }

        public static bool TryRecord(M05HarborBeat beat, Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (beat < M05HarborBeat.RepairStall ||
                beat >= M05HarborBeat.Finished ||
                getInt == null || setInt == null ||
                !M05LandingFacts.TryRead(getInt, out M05LandingReport? landing) ||
                !landing.HasValue || !TryRead(getInt, out int mask))
                return false;

            // Idempotent after a successful Makinom write: if a UI handoff
            // then throws, retrying the same observed scene must not write
            // a duplicate event or unlock an unvisited location.
            int next = mask | (1 << (int)beat);
            if (next != mask)
                setInt(VisitedMaskKey, next);
            return true;
        }

        public static bool TryResetForNewVoyage(Action<string, int> setInt)
        {
            if (setInt == null) return false;
            setInt(VisitedMaskKey, 0);
            return true;
        }
    }
}
