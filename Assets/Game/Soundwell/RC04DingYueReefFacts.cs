using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    public enum RC04Resolution
    {
        KillMimicBeastWithAyuanSong = 1,
        BringDingYueBack = 2,
        LeaveWithSignalBeacon = 3
    }

    /// <summary>
    /// Immutable RC-04 ("丁岳与借声兽") reef listening & resolution record.
    /// Requires all 3 fog-listening discrimination rounds (slow human echo vs.
    /// fast mimic beast reply) to locate the short tidal window before landing
    /// and settling Ding Yue's outcome.
    /// </summary>
    public readonly struct RC04ReefRecord
    {
        public RC04ReefRecord(
            int listeningRoundsCompleted,
            bool ayuanSongUsed,
            RC04Resolution resolution)
        {
            ListeningRoundsCompleted = listeningRoundsCompleted;
            AyuanSongUsed = ayuanSongUsed;
            Resolution = resolution;
        }

        public int ListeningRoundsCompleted { get; }
        public bool AyuanSongUsed { get; }
        public RC04Resolution Resolution { get; }
    }

    public static class RC04DingYueReefFacts
    {
        public const int RequiredListeningRounds = 3;
        public const string ConfirmedKey = "rpg.return.rc04.reef-confirmed";
        public const string RoundsCompletedKey = "rpg.return.rc04.listening-rounds";
        public const string AyuanSongUsedKey = "rpg.return.rc04.ayuan-song-used";
        public const string ResolutionKey = "rpg.return.rc04.resolution";

        public static bool IsValidRecord(RC04ReefRecord record)
        {
            if (record.ListeningRoundsCompleted != RequiredListeningRounds)
                return false;
            if (!Enum.IsDefined(typeof(RC04Resolution), record.Resolution))
                return false;
            if (record.Resolution == RC04Resolution.KillMimicBeastWithAyuanSong &&
                !record.AyuanSongUsed)
                return false;
            return true;
        }

        public static bool CanRecord(RC04ReefRecord record, Func<string, int> getInt)
        {
            return getInt != null &&
                getInt(ConfirmedKey) == 0 &&
                IsValidRecord(record);
        }

        public static bool TryRecord(RC04ReefRecord record)
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRecord(
                record,
                key => variables.GetInt(key),
                (key, value) => variables.Set(key, value));
        }

        public static bool TryRecord(
            RC04ReefRecord record,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (getInt == null || setInt == null || !CanRecord(record, getInt))
                return false;

            setInt(ConfirmedKey, 0);
            setInt(RoundsCompletedKey, record.ListeningRoundsCompleted);
            setInt(AyuanSongUsedKey, record.AyuanSongUsed ? 1 : 0);
            setInt(ResolutionKey, (int)record.Resolution);
            setInt(ConfirmedKey, 1);
            return true;
        }

        public static bool TryRead(out RC04ReefRecord? record)
        {
            record = null;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryRead(key => variables.GetInt(key), out record);
        }

        public static bool TryRead(Func<string, int> getInt, out RC04ReefRecord? record)
        {
            record = null;
            if (getInt == null)
                return false;
            if (getInt(ConfirmedKey) != 1)
                return true;

            int rounds = getInt(RoundsCompletedKey);
            int rawSong = getInt(AyuanSongUsedKey);
            int rawRes = getInt(ResolutionKey);

            if ((rawSong != 0 && rawSong != 1) ||
                !Enum.IsDefined(typeof(RC04Resolution), rawRes))
                return true;

            var saved = new RC04ReefRecord(rounds, rawSong == 1, (RC04Resolution)rawRes);
            if (IsValidRecord(saved))
                record = saved;
            return true;
        }

        public static bool TryResetForNewVoyage(Action<string, int> setInt)
        {
            if (setInt == null)
                return false;
            setInt(ConfirmedKey, 0);
            setInt(RoundsCompletedKey, 0);
            setInt(AyuanSongUsedKey, 0);
            setInt(ResolutionKey, 0);
            return true;
        }
    }
}
