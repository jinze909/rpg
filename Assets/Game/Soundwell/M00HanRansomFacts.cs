using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    /// <summary>
    /// M00's fixed Han Qiao redemption count, as stated aloud in the meeting.
    /// The scene records it only when the forty-one-person exchange is reached.
    /// Later chapters always use the v1.2 fixed count, including before a save
    /// migration or when an old save lacks this new Makinom key.
    /// </summary>
    public static class M00HanRansomFacts
    {
        public const string CountKey = "rpg.return.m00.han-ransom-count";
        public const int ConfirmedCount = M00HanQiaoMeetingSequence.HanRansomCount;

        public static bool TryRecordConfirmedCount()
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
                return false;

            return TryRecordConfirmedCount(
                key => variables.GetInt(key),
                (key, value) => variables.Set(key, value));
        }

        public static bool TryRecordConfirmedCount(
            Func<string, int> getInt, Action<string, int> setInt)
        {
            if (getInt == null || setInt == null ||
                M06RescuePlanFacts.HasOrphanedDownstreamFacts(getInt))
                return false;

            setInt(CountKey, ConfirmedCount);
            return true;
        }

        public static bool TryRead(out int confirmedCount)
        {
            confirmedCount = ConfirmedCount;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
                return false;

            return TryRead(key => variables.GetInt(key), out confirmedCount);
        }

        public static bool TryRead(Func<string, int> getInt, out int confirmedCount)
        {
            confirmedCount = ConfirmedCount;
            if (getInt == null)
                return false;

            // The narrative register explicitly defines no missing branch:
            // a legacy 0 or stale value must not rewrite the authored count.
            int stored = getInt(CountKey);
            confirmedCount = stored == ConfirmedCount ? stored : ConfirmedCount;
            return true;
        }
    }
}
