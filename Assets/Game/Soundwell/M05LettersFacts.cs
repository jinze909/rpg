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
    /// outer harbor. Uses only the existing Makinom global variable store;
    /// absence is not a refusal, and a carried packet is not delivered mail.
    /// </summary>
    public static class M05LettersFacts
    {
        public const string DecisionKey = "rpg.return.m05.seran-letters-decision";
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
            if (getInt == null || setInt == null)
                return false;

            // One completed harbor choice per voyage. Refuse duplicate or
            // corrupted persisted state rather than swapping accepted mail
            // for a different outcome under a repeated UI callback.
            if (getInt(DecisionKey) != 0)
                return false;
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
            if (saved == (int)M05LetterDecision.TakePacket ||
                saved == (int)M05LetterDecision.LeaveForNextBoat)
                decision = (M05LetterDecision)saved;
            // A missing or invalid code never fabricates an answer.
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
            return true;
        }
    }
}
