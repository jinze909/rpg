using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    // The M05 Seran dialogue offers two authored answers; there is no
    // inferred consent and no individual delivery until later story scenes.
    public enum M05LetterDecision
    {
        TakePacket = 1,
        LeaveForNextBoat = 2
    }

    /// <summary>
    /// Persists whether the player took the eight family letters at the
    /// outer harbor. The decision is bound to the verified M05 arrival in the
    /// same Makinom store; a carried packet is not delivered mail.
    /// </summary>
    public static class M05LettersFacts
    {
        public const string DecisionKey = "rpg.return.m05.seran-letters-decision";
        // ArrivalSequence + 1. Zero denotes an unsigned legacy decision.
        public const string DecisionArrivalIdentityKey =
            "rpg.return.m05.seran-letters-arrival-identity";
        public const int PacketLetterCount = 8;

        public static bool TryRecord(M05LetterDecision decision)
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
                return false;
            return TryRecord(decision, key => variables.GetInt(key),
                (key, value) => variables.Set(key, value));
        }

        public static bool TryRecord(M05LetterDecision decision,
            Func<string, int> getInt, Action<string, int> setInt)
        {
            if (!Enum.IsDefined(typeof(M05LetterDecision), decision))
                throw new ArgumentOutOfRangeException(nameof(decision));
            if (getInt == null || setInt == null ||
                M06RescuePlanFacts.HasOrphanedDownstreamFacts(getInt))
                return false;

            // A packet choice is meaningful only after the actual landing
            // has been committed. Route selection, sailing and fog success
            // alone must never create an arrival-specific answer.
            if (!M05LandingFacts.TryRead(getInt, out M05LandingReport? landing) ||
                !landing.HasValue)
                return false;

            int sequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            int saved = getInt(DecisionKey);
            int bound = getInt(DecisionArrivalIdentityKey);
            if (sequence < 0 || sequence == int.MaxValue || bound < 0 ||
                (saved != 0 && !Enum.IsDefined(typeof(M05LetterDecision), saved)))
                return false;
            int currentIdentity = sequence + 1;

            // Same-voyage retries are idempotent. A conflicting answer is
            // never silently substituted after a completed player choice.
            if (bound == currentIdentity)
            {
                if (saved == (int)decision)
                    return true;
                if (saved != 0)
                    return false;

                // A previous attempt may have persisted the binding but
                // failed before writing the actual answer.
                setInt(DecisionKey, (int)decision);
                return true;
            }

            // A valid old or unsigned decision is not evidence for this
            // landing. Clear it BEFORE publishing the new identity. If a
            // Makinom write is interrupted, TryRead still hides the old answer.
            if (saved != 0)
                setInt(DecisionKey, 0);
            setInt(DecisionArrivalIdentityKey, currentIdentity);
            setInt(DecisionKey, (int)decision);
            return true;
        }

        public static bool TryRead(out M05LetterDecision? decision)
        {
            decision = null;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null &&
                TryRead(key => variables.GetInt(key), out decision);
        }

        public static bool TryRead(Func<string, int> getInt,
            out M05LetterDecision? decision)
        {
            decision = null;
            if (getInt == null)
                return false;

            int saved = getInt(DecisionKey);
            int bound = getInt(DecisionArrivalIdentityKey);
            if (bound < 0)
                return false;
            if (saved == 0)
                return true;
            if (!Enum.IsDefined(typeof(M05LetterDecision), saved))
                return false;

            // A saved answer without a verified landing, or without the
            // exact current landing identity, is stale rather than consent.
            if (!M05LandingFacts.TryRead(getInt, out M05LandingReport? landing))
                return false;
            if (!landing.HasValue)
                return true;

            int sequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            if (sequence < 0 || sequence == int.MaxValue)
                return false;
            if (bound != sequence + 1)
                return true;

            decision = (M05LetterDecision)saved;
            return true;
        }

        public static int LettersCarried(M05LetterDecision? decision)
        {
            return decision == M05LetterDecision.TakePacket ? PacketLetterCount : 0;
        }

        public static bool TryResetForNewVoyage()
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
                return false;
            return TryResetForNewVoyage((key, value) => variables.Set(key, value));
        }

        public static bool TryResetForNewVoyage(Action<string, int> setInt)
        {
            if (setInt == null)
                return false;
            setInt(DecisionKey, 0);
            setInt(DecisionArrivalIdentityKey, 0);
            return true;
        }
    }
}
