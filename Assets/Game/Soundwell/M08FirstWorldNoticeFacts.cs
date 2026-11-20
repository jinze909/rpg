using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    /// <summary>
    /// First of the three chapter world notices: Huilan Pass's new transit
    /// tax. The five roster cases become AVAILABLE to investigate only
    /// when this actual morning notice has been read and the verified M07
    /// rescue roster exists. Availability is never case completion, berth
    /// assignment, successful mediation, or an M09/M10 notice.
    /// </summary>
    public static class M08FirstWorldNoticeFacts
    {
        public const string ConfirmedKey = "rpg.return.m08.huilan-tax-notice-read";
        public const int FirstCase = 1;
        public const int LastCase = 5;

        public static bool TryRead(out bool seen)
        {
            seen = false;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRead(key => variables.GetInt(key), out seen);
        }

        public static bool TryRead(Func<string, int> getInt, out bool seen)
        {
            seen = false;
            if (getInt == null)
                return false;
            if (getInt(ConfirmedKey) != 1)
                return true;
            if (!M08WorkshopDepositFacts.TryRead(getInt, out bool paid) || !paid)
                return true;
            seen = true;
            return true;
        }

        public static bool TryRecord(bool realDaybreakObserved)
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRecord(realDaybreakObserved,
                key => variables.GetInt(key),
                (key, value) => variables.Set(key, value));
        }

        public static bool TryRecord(
            bool realDaybreakObserved,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (!realDaybreakObserved || getInt == null || setInt == null ||
                getInt(ConfirmedKey) != 0)
                return false;

            if (!M08WorkshopDepositFacts.TryRead(getInt, out bool paid) || !paid)
                return false;

            // Only the M08 Huilan toll notice is recorded. Never create
            // the later ship-requisition or conscription notices here.
            setInt(ConfirmedKey, 1);
            return true;
        }

        public static bool TryCaseAvailable(
            int caseNumber, Func<string, int> getInt, out bool available)
        {
            available = false;
            if (caseNumber < FirstCase || caseNumber > LastCase || getInt == null)
                return false;

            if (!TryRead(getInt, out bool noticeRead) || !noticeRead)
                return true;

            if (!M07ExtractionFacts.TryRead(getInt, out M07ExtractionRecord? roster) ||
                !roster.HasValue)
                return true;

            available = true;
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
            if (setInt == null) return false;
            setInt(ConfirmedKey, 0);
            return true;
        }
    }
}
