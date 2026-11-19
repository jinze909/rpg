using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Immutable snapshot of a completed restricted fog-listening leg read back
    /// from the single existing Makinom global variable store.
    /// </summary>
    public readonly struct FogListeningRecord
    {
        public FogListeningRecord(
            bool succeeded,
            int hullIntegrity,
            int avoidedHazards,
            int matchedSignals,
            int remainingFocus)
        {
            Succeeded = succeeded;
            HullIntegrity = hullIntegrity;
            AvoidedHazards = avoidedHazards;
            MatchedSignals = matchedSignals;
            RemainingFocus = remainingFocus;
        }

        public bool Succeeded { get; }
        public int HullIntegrity { get; }
        public int AvoidedHazards { get; }
        public int MatchedSignals { get; }
        public int RemainingFocus { get; }
    }

    /// <summary>
    /// Persists, reads back, and replay-resets completed FogListeningSession
    /// outcomes through Makinom's global variable store (single RPG/ORK save
    /// authority). Invalidates the completion marker before writing fields and
    /// publishes it last so interrupted writes never expose torn state.
    /// </summary>
    public static class FogListeningFacts
    {
        public const string CompletedKey = "rpg.return.sailing.fog-leg-completed";
        public const string SucceededKey = "rpg.return.sailing.fog-leg-succeeded";
        public const string HullIntegrityKey = "rpg.return.sailing.fog-leg-hull";
        public const string AvoidedHazardsKey = "rpg.return.sailing.fog-leg-avoided-hazards";
        public const string MatchedSignalsKey = "rpg.return.sailing.fog-leg-matched-signals";
        public const string RemainingFocusKey = "rpg.return.sailing.fog-leg-remaining-focus";

        public static bool TryResetForNewLeg()
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
                return false;
            return TryResetForNewLeg((key, value) => variables.Set(key, value));
        }

        public static bool TryResetForNewLeg(Action<string, int> setInt)
        {
            if (setInt == null)
                return false;
            setInt(CompletedKey, 0);
            setInt(SucceededKey, 0);
            setInt(HullIntegrityKey, 0);
            setInt(AvoidedHazardsKey, 0);
            setInt(MatchedSignalsKey, 0);
            setInt(RemainingFocusKey, 0);
            return true;
        }

        public static bool TryRecord(FogListeningSession session)
        {
            ValidateCompleted(session);
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
                return false;
            return TryRecord(session, (key, value) => variables.Set(key, value));
        }

        public static bool TryRecord(FogListeningSession session, Action<string, int> setInt)
        {
            ValidateCompleted(session);
            if (setInt == null)
                return false;

            // Invalidate prior completion first and publish completion last.
            setInt(CompletedKey, 0);
            setInt(SucceededKey, session.Succeeded ? 1 : 0);
            setInt(HullIntegrityKey, session.HullIntegrity);
            setInt(AvoidedHazardsKey, session.AvoidedHazards);
            setInt(MatchedSignalsKey, session.MatchedSignals);
            setInt(RemainingFocusKey, session.RemainingFocus);
            setInt(CompletedKey, 1);
            return true;
        }

        public static bool TryRead(out FogListeningRecord? record)
        {
            record = null;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
                return false;
            return TryRead(key => variables.GetInt(key), out record);
        }

        public static bool TryRead(Func<string, int> getInt, out FogListeningRecord? record)
        {
            record = null;
            if (getInt == null)
                return false;
            if (getInt(CompletedKey) != 1)
                return true;

            int rawSucceeded = getInt(SucceededKey);
            if (rawSucceeded != 0 && rawSucceeded != 1)
                return true;
            bool succeeded = rawSucceeded == 1;

            int hull = getInt(HullIntegrityKey);
            int avoided = getInt(AvoidedHazardsKey);
            int matched = getInt(MatchedSignalsKey);
            int focus = getInt(RemainingFocusKey);

            if (hull < 0 || avoided < 0 || matched < 0 || focus < 0)
                return true;
            if (succeeded && hull <= 0)
                return true;
            if (!succeeded && hull > 0)
                return true;

            record = new FogListeningRecord(succeeded, hull, avoided, matched, focus);
            return true;
        }

        private static void ValidateCompleted(FogListeningSession session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (!session.IsComplete)
                throw new InvalidOperationException(
                    "Incomplete fog-listening sessions cannot be recorded.");
        }
    }
}
