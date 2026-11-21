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
        // Identity of the actual M05 landing. Stored as arrival sequence + 1
        // so unsigned legacy visits stay unverified until observed again.
        public const string VisitedArrivalIdentityKey =
            "rpg.return.m05.harbor-beats-arrival-identity";
        public const int AllBeatsMask = 7;

        public static bool TryRead(Func<string, int> getInt, out int mask)
        {
            mask = 0;
            if (getInt == null)
                return false;
            int saved = getInt(VisitedMaskKey);
            int identity = getInt(VisitedArrivalIdentityKey);
            if (saved < 0 || (saved & ~AllBeatsMask) != 0 ||
                identity < 0)
                return false;
            if (saved == 0)
                return true;

            // An old incomplete replay reset may leave an earlier voyage's
            // dialogue mask. It must never become observed dialogue at a new
            // landing simply because this replay arrived successfully.
            if (!M05LandingFacts.TryRead(getInt, out M05LandingReport? landing) ||
                !landing.HasValue)
                return true;
            int sequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            if (sequence < 0 || sequence == int.MaxValue)
                return false;
            // Unsigned legacy masks have no evidence of which voyage
            // witnessed them. Never promote their old visits to this arrival.
            if (identity != sequence + 1)
                return true;

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
            int sequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            if (sequence < 0 || sequence == int.MaxValue)
                return false;
            int currentIdentity = sequence + 1;
            int bound = getInt(VisitedArrivalIdentityKey);
            if (bound < 0)
                return false;

            if (bound != currentIdentity)
            {
                // A real new observation may recover an old, torn reset or
                // unsigned legacy save. Clear old visits BEFORE assigning
                // the new identity: interruption at either write point
                // must leave the current voyage with zero observed beats.
                if (getInt(VisitedMaskKey) != 0)
                    setInt(VisitedMaskKey, 0);
                setInt(VisitedArrivalIdentityKey, currentIdentity);
            }

            int next = mask | (1 << (int)beat);
            if (getInt(VisitedMaskKey) != next)
                setInt(VisitedMaskKey, next);
            return true;
        }

        public static bool TryResetForNewVoyage(Action<string, int> setInt)
        {
            if (setInt == null) return false;
            setInt(VisitedMaskKey, 0);
            setInt(VisitedArrivalIdentityKey, 0);
            return true;
        }
    }
}
