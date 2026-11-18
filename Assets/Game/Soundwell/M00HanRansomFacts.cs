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
        public const int ConfirmedCount = 41;

        public static bool TryRecordConfirmedCount()
        {
            if (!Maki.Initialized || Maki.Game?.Variables == null)
                return false;

            Maki.Game.Variables.Set(CountKey, ConfirmedCount);
            return true;
        }

        public static bool TryRead(out int confirmedCount)
        {
            confirmedCount = ConfirmedCount;
            if (!Maki.Initialized || Maki.Game?.Variables == null)
                return false;

            // The narrative register explicitly defines no missing branch:
            // a legacy 0 or stale value must not rewrite the authored count.
            int stored = Maki.Game.Variables.GetInt(CountKey);
            confirmedCount = stored == ConfirmedCount ? stored : ConfirmedCount;
            return true;
        }
    }
}
