using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    /// <summary>
    /// The four M06 mine-camp reconnaissance leads inspected before settling
    /// the rescue plan: escort schedules, guard patrols, supply ledgers, and
    /// mine tunnels / main drainage pump.
    /// </summary>
    public enum M06MineCampLead
    {
        EscortSchedule = 0,
        GuardPatrol = 1,
        SupplyLedger = 2,
        MineTunnelAndPump = 3
    }

    /// <summary>
    /// Persists which M06 mine-camp reconnaissance leads were actually
    /// inspected on this voyage using the single Makinom integer variable
    /// store. Strictly gated on a verified M05 outer-harbor landing and never
    /// fabricates uninspected leads or accepts corrupt bitmasks.
    /// </summary>
    public static class M06MineCampReconFacts
    {
        public const string ObservedMaskKey = "rpg.return.m06.mine-camp-recon-observed";
        // ArrivalSequence + 1. A zero identity means an unbound older
        // observation that must be re-inspected before this voyage earns it.
        public const string ObservedArrivalIdentityKey =
            "rpg.return.m06.mine-camp-recon-arrival-identity";
        public const int AllLeadsMask = 15;

        public static bool TryRead(out int mask)
        {
            mask = 0;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRead(key => variables.GetInt(key), out mask);
        }

        public static bool TryRead(Func<string, int> getInt, out int mask)
        {
            mask = 0;
            if (getInt == null)
                return false;

            int saved = getInt(ObservedMaskKey);
            int bound = getInt(ObservedArrivalIdentityKey);
            if (saved < 0 || (saved & ~AllLeadsMask) != 0 || bound < 0)
                return false;
            if (saved == 0)
                return true;

            // A verified landing is necessary but not sufficient: an
            // interrupted voyage reset can preserve the old recon bits while
            // confirming an otherwise identical second landing.
            if (!M05LandingFacts.TryRead(getInt, out M05LandingReport? landing) ||
                !landing.HasValue)
                return true;
            int sequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            if (sequence < 0 || sequence == int.MaxValue)
                return false;
            if (bound != sequence + 1)
                return true;

            mask = saved;
            return true;
        }

        public static bool Contains(int mask, M06MineCampLead lead)
        {
            return IsValidLead(lead) && (mask & (1 << (int)lead)) != 0;
        }

        public static int CountObserved(int mask)
        {
            int count = 0;
            for (int i = 0; i < 4; i++)
            {
                if ((mask & (1 << i)) != 0)
                    count++;
            }
            return count;
        }

        public static bool TryRecord(M06MineCampLead lead)
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRecord(
                lead,
                key => variables.GetInt(key),
                (key, value) => variables.Set(key, value));
        }

        public static bool TryRecord(
            M06MineCampLead lead,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (!IsValidLead(lead) || getInt == null || setInt == null)
                return false;

            if (!M05LandingFacts.TryRead(getInt, out M05LandingReport? landing) ||
                !landing.HasValue)
                return false;

            int saved = getInt(ObservedMaskKey);
            int bound = getInt(ObservedArrivalIdentityKey);
            int sequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            if (saved < 0 || (saved & ~AllLeadsMask) != 0 ||
                bound < 0 || sequence < 0 || sequence == int.MaxValue)
                return false;

            int identity = sequence + 1;
            if (bound != identity)
            {
                // A real inspection may start this arrival's recon record.
                // Clear the previous mask BEFORE publishing the new arrival
                // binding: a torn Makinom write must never grant old leads
                // to the new voyage or silently convert an unsigned legacy
                // save into a verified observation.
                if (saved != 0)
                    setInt(ObservedMaskKey, 0);
                setInt(ObservedArrivalIdentityKey, identity);
                saved = 0;
            }

            int next = saved | (1 << (int)lead);
            if (next != saved)
                setInt(ObservedMaskKey, next);
            return true;
        }

        public static bool TryResetForNewVoyage()
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryResetForNewVoyage(
                (key, value) => variables.Set(key, value));
        }

        public static bool TryResetForNewVoyage(Action<string, int> setInt)
        {
            if (setInt == null)
                return false;
            setInt(ObservedMaskKey, 0);
            setInt(ObservedArrivalIdentityKey, 0);
            return true;
        }

        private static bool IsValidLead(M06MineCampLead lead)
        {
            return lead >= M06MineCampLead.EscortSchedule &&
                lead <= M06MineCampLead.MineTunnelAndPump;
        }
    }
}
