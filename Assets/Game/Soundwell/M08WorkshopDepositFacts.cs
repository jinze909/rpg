using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Persist the M08 first civilian commission deposit in the existing
    /// Makinom story store. This is a narrative completion fact, NOT an
    /// ORK currency payout or permission to publish M08's world notice.
    /// Only a completed winch and a verified M07 extraction can set it.
    /// </summary>
    public static class M08WorkshopDepositFacts
    {
        public const string CompletedKey = "rpg.return.m08.first-winch-deposit-confirmed";

        public static bool TryRead(out bool completed)
        {
            completed = false;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRead(key => variables.GetInt(key), out completed);
        }

        public static bool TryRead(Func<string, int> getInt, out bool completed)
        {
            completed = false;
            if (getInt == null)
                return false;

            // An invalid value must not be treated as an earned deposit.
            if (getInt(CompletedKey) != 1)
                return true;

            // A different/new voyage invalidates a previous M08 reward.
            if (!M07ExtractionFacts.TryRead(getInt, out M07ExtractionRecord? extraction) ||
                !extraction.HasValue)
                return true;

            completed = true;
            return true;
        }

        // A torn new-voyage reset can clear the workshop marker while
        // leaving an older dock reply or dawn notice in the same Makinom
        // store. Both occur strictly AFTER a completed M08 winch, so any
        // nonzero downstream value forbids issuing a fresh deposit.
        public static bool HasOrphanedDownstreamFacts(Func<string, int> getInt)
        {
            return getInt == null ||
                getInt(M08DockTruthFacts.ConfirmedKey) != 0 ||
                getInt(M08DockTruthFacts.ShenRelationResponseKey) != 0 ||
                getInt(M08FirstWorldNoticeFacts.ConfirmedKey) != 0;
        }

        public static bool TryConfirm(M08FirstWinchOrderSession session)
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryConfirm(session,
                key => variables.GetInt(key),
                (key, value) => variables.Set(key, value));
        }

        public static bool TryConfirm(
            M08FirstWinchOrderSession session,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (session == null || !session.WorkshopDepositEarned ||
                getInt == null || setInt == null || getInt(CompletedKey) != 0 ||
                HasOrphanedDownstreamFacts(getInt))
                return false;
            if (!M07ExtractionFacts.TryRead(getInt, out M07ExtractionRecord? extraction) ||
                !extraction.HasValue)
                return false;

            // One authoritative write; no duplicate currency or inventory rewards.
            // A thrown write never makes the caller acknowledge completion.
            setInt(CompletedKey, 1);
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
            setInt(CompletedKey, 0);
            return true;
        }
    }
}
