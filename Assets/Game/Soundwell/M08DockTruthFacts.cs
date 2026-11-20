using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Authoritative M08 night-dock Shen Li relationship response saved in
    /// the existing Makinom global store. This records the player's authored
    /// choice, not an invented numerical affection score or an automatic
    /// reconciliation. The scene host must verify Shen's real presence.
    /// </summary>
    public static class M08DockTruthFacts
    {
        public const string ConfirmedKey = "rpg.return.m08.shen-dock-truth-confirmed";
        public const string ShenRelationResponseKey = "rpg.return.m08.shen-relation-response";

        public static bool TryRead(out M08DockTruthResponse? response)
        {
            response = null;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRead(
                key => variables.GetInt(key), out response);
        }

        public static bool TryRead(
            Func<string, int> getInt, out M08DockTruthResponse? response)
        {
            response = null;
            if (getInt == null)
                return false;

            if (getInt(ConfirmedKey) != 1)
                return true;

            int value = getInt(ShenRelationResponseKey);
            if (!Enum.IsDefined(typeof(M08DockTruthResponse), value))
                return true;

            // A stale prior-voyage reply cannot exist without the M08 winch
            // and its verified upstream M07 extraction on this voyage.
            if (!M08WorkshopDepositFacts.TryRead(getInt, out bool completed) ||
                !completed)
                return true;

            response = (M08DockTruthResponse)value;
            return true;
        }

        public static bool TryRecord(
            M08DockTruthResponse response, bool shenLiPresent)
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRecord(response, shenLiPresent,
                key => variables.GetInt(key),
                (key, value) => variables.Set(key, value));
        }

        public static bool TryRecord(
            M08DockTruthResponse response, bool shenLiPresent,
            Func<string, int> getInt, Action<string, int> setInt)
        {
            if (!shenLiPresent ||
                !Enum.IsDefined(typeof(M08DockTruthResponse), response) ||
                getInt == null || setInt == null ||
                getInt(ConfirmedKey) != 0)
                return false;

            if (!M08WorkshopDepositFacts.TryRead(getInt, out bool completed) ||
                !completed)
                return false;

            // Publish the chosen relationship response only after a complete
            // write. If an interruption occurs, the host can retry safely.
            setInt(ConfirmedKey, 0);
            setInt(ShenRelationResponseKey, (int)response);
            setInt(ConfirmedKey, 1);
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
            setInt(ConfirmedKey, 0);
            setInt(ShenRelationResponseKey, 0);
            return true;
        }
    }
}
