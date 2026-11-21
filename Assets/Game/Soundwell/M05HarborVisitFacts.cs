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
        // so old pre-sequence saves can be bound without using zero (legacy).
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
            if (identity != 0 && identity != sequence + 1)
                return true;

            // Identity=0 represents an existing pre-signature save; retain
            // its verified observations for backward compatibility.
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
            if (bound < 0 || (bound != 0 && bound != currentIdentity))
                return false;

            // Persist the arrival binding before the first observed beat.
            // If Makinom interrupts this write, only the same arrival can
            // resume; an old mask never migrates into the next voyage.
            if (bound == 0)
                setInt(VisitedArrivalIdentityKey, currentIdentity);

            int next = mask | (1 << (int)beat);
            if (next != mask)
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
