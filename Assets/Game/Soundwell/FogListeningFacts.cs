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
        public const string PendingSignatureKey = "rpg.return.sailing.fog-leg-pending-signature";
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
            setInt(PendingSignatureKey, 0);
            setInt(SucceededKey, 0);
            setInt(HullIntegrityKey, 0);
            setInt(AvoidedHazardsKey, 0);
            setInt(MatchedSignalsKey, 0);
            setInt(RemainingFocusKey, 0);
            return true;
        }

        /// <summary>
        /// Detects an interrupted fog save before a *new* scene can start.
        /// A currently open fog panel may retry its own unfinished write;
        /// a reopened one must never replace unconfirmed payload from it.
        /// </summary>
        public static bool HasUnconfirmedPayload(Func<string, int> getInt)
        {
            return getInt != null && getInt(CompletedKey) == 0 &&
                (getInt(PendingSignatureKey) != 0 ||
                 getInt(SucceededKey) != 0 ||
                 getInt(HullIntegrityKey) != 0 ||
                 getInt(AvoidedHazardsKey) != 0 ||
                 getInt(MatchedSignalsKey) != 0 ||
                 getInt(RemainingFocusKey) != 0);
        }

        public static bool TryRecord(FogListeningSession session)
        {
            ValidateCompleted(session);
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
                return false;
            return TryRecord(session, key => variables.GetInt(key),
                (key, value) => variables.Set(key, value));
        }


        // A fog leg is an irreversible observation on one sailed voyage. The
        // intent is saved before the first outcome field, so a failed save
        // cannot later be claimed by a different helm result or voyage.
        private static uint Mix(uint hash, int value)
        {
            unchecked
            {
                for (int shift = 0; shift < 32; shift += 8)
                {
                    hash ^= (byte)(value >> shift);
                    hash *= 16777619u;
                }
                return hash;
            }
        }

        private static int Signature(FogListeningRecord record, int sailingSequence)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = Mix(hash, sailingSequence);
                hash = Mix(hash, record.Succeeded ? 1 : 0);
                hash = Mix(hash, record.HullIntegrity);
                hash = Mix(hash, record.AvoidedHazards);
                hash = Mix(hash, record.MatchedSignals);
                hash = Mix(hash, record.RemainingFocus);
                int signature = (int)hash;
                return signature == 0 ? 1 : signature;
            }
        }

        private static bool CompatiblePending(
            Func<string, int> read, string key, int expected)
        {
            int saved = read(key);
            return saved == 0 || saved == expected;
        }

        // New and active gameplay must use this read-aware overload, not the
        // legacy writer-only overload (which is kept for older fixtures).
        public static bool TryRecord(FogListeningSession session,
            Func<string, int> getInt, Action<string, int> setInt)
        {
            ValidateCompleted(session);
            if (getInt == null || setInt == null ||
                getInt(CompletedKey) != 0 ||
                M06RescuePlanFacts.HasOrphanedDownstreamFacts(getInt))
                return false;

            int sequence = getInt(M04DepartureFacts.SailingSequenceKey);
            if (sequence < 0)
                return false;
            var record = new FogListeningRecord(
                session.Succeeded, session.HullIntegrity,
                session.AvoidedHazards, session.MatchedSignals,
                session.RemainingFocus);
            int signature = Signature(record, sequence);
            int pending = getInt(PendingSignatureKey);
            if (pending == 0)
            {
                // An unsigned partial old write must not be overwritten.
                if (getInt(SucceededKey) != 0 ||
                    getInt(HullIntegrityKey) != 0 ||
                    getInt(AvoidedHazardsKey) != 0 ||
                    getInt(MatchedSignalsKey) != 0 ||
                    getInt(RemainingFocusKey) != 0)
                    return false;
            }
            else if (pending != signature ||
                !CompatiblePending(getInt, SucceededKey, record.Succeeded ? 1 : 0) ||
                !CompatiblePending(getInt, HullIntegrityKey, record.HullIntegrity) ||
                !CompatiblePending(getInt, AvoidedHazardsKey, record.AvoidedHazards) ||
                !CompatiblePending(getInt, MatchedSignalsKey, record.MatchedSignals) ||
                !CompatiblePending(getInt, RemainingFocusKey, record.RemainingFocus))
            {
                return false;
            }

            // Publish original intent first. The completion bit is last.
            setInt(PendingSignatureKey, signature);
            setInt(SucceededKey, record.Succeeded ? 1 : 0);
            setInt(HullIntegrityKey, record.HullIntegrity);
            setInt(AvoidedHazardsKey, record.AvoidedHazards);
            setInt(MatchedSignalsKey, record.MatchedSignals);
            setInt(RemainingFocusKey, record.RemainingFocus);
            setInt(CompletedKey, 1);
            return true;
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

            var saved = new FogListeningRecord(succeeded, hull, avoided, matched, focus);
            // Signed outcomes also belong to the original voyage. Old fully
            // confirmed outcomes without an intent remain backwards compatible.
            int pending = getInt(PendingSignatureKey);
            if (pending == 0 ||
                pending == Signature(saved, getInt(M04DepartureFacts.SailingSequenceKey)))
                record = saved;
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
