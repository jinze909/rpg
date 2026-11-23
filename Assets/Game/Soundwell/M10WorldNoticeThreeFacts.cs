using System;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Records that the player read M10's third omen at Linchao Ferry before
    /// entering the Jingwan assembly. The fact is bound to the verified M05
    /// arrival sequence, so a later voyage cannot inherit an old notice.
    /// </summary>
    public static class M10WorldNoticeThreeFacts
    {
        public const string ConfirmedKey = "rpg.return.m10.world-notice-three-confirmed";
        public const string ArrivalIdentityKey = "rpg.return.m10.world-notice-three-arrival-identity";

        public static bool TryReadForCurrentArrival(
            Func<string, int> getInt,
            out bool observed)
        {
            observed = false;
            if (getInt == null)
                return false;

            int confirmed = getInt(ConfirmedKey);
            int identity = getInt(ArrivalIdentityKey);
            if ((confirmed != 0 && confirmed != 1) || identity < 0)
                return false;

            if (confirmed == 0)
                return true;

            if (!M05LandingFacts.TryRead(getInt, out M05LandingReport? landing) ||
                !landing.HasValue ||
                !M09BerthTableFacts.TryRead(getInt, out M09BerthTableRecord? berth) ||
                !berth.HasValue)
                return false;

            int currentIdentity = getInt(M05LandingFacts.ArrivalSequenceKey);
            if (currentIdentity < 0)
                return false;

            observed = identity == currentIdentity;
            return true;
        }

        public static bool TryRecord(
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (getInt == null || setInt == null ||
                !M05LandingFacts.TryRead(getInt, out M05LandingReport? landing) ||
                !landing.HasValue ||
                !M09BerthTableFacts.TryRead(getInt, out M09BerthTableRecord? berth) ||
                !berth.HasValue ||
                M10AssemblyFacts.HasAnyPersistedFacts(getInt) ||
                M10AssemblyFacts.HasOrphanedDownstreamFacts(getInt))
                return false;

            int currentIdentity = getInt(M05LandingFacts.ArrivalSequenceKey);
            if (currentIdentity < 0 ||
                !TryReadForCurrentArrival(getInt, out bool alreadyObserved))
                return false;
            if (alreadyObserved)
                return true;

            // Invalidate first, write the voyage identity, then publish the
            // confirmation marker last. A torn write can safely retry.
            setInt(ConfirmedKey, 0);
            setInt(ArrivalIdentityKey, currentIdentity);
            setInt(ConfirmedKey, 1);

            return TryReadForCurrentArrival(getInt, out bool saved) && saved;
        }

        public static bool TryResetForNewVoyage(Action<string, int> setInt)
        {
            if (setInt == null)
                return false;
            setInt(ConfirmedKey, 0);
            setInt(ArrivalIdentityKey, 0);
            return true;
        }
    }
}