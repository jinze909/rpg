using System;

namespace Rpg.Soundwell
{
    public enum RC04EchoChoice
    {
        FastMimicReply = 0,
        SlowHumanEcho = 1
    }

    /// <summary>
    /// Authoritative RC-04 fog-listening and reef-landing state machine.
    /// The player must distinguish the slow human echo from the fast mimic
    /// beast reply across 3 consecutive rounds to locate the short tidal
    /// window and land on Ding Yue's reef before resolving the encounter.
    /// </summary>
    public sealed class RC04DingYueReefSession
    {
        public int CompletedListeningRounds { get; private set; }
        public int FalseMimicPicks { get; private set; }
        public bool TidalWindowLocated =>
            CompletedListeningRounds >= RC04DingYueReefFacts.RequiredListeningRounds;
        public bool AyuanSongPlayed { get; private set; }
        public bool IsResolved { get; private set; }
        public RC04ReefRecord? CompletedRecord { get; private set; }

        public bool SubmitEchoDiscrimination(RC04EchoChoice choice)
        {
            if (IsResolved || TidalWindowLocated)
                return false;
            if (!Enum.IsDefined(typeof(RC04EchoChoice), choice))
                throw new ArgumentOutOfRangeException(nameof(choice));

            if (choice == RC04EchoChoice.SlowHumanEcho)
            {
                CompletedListeningRounds++;
                return true;
            }

            // Following the beast's premature reply does not advance bearing lock.
            FalseMimicPicks++;
            return false;
        }

        public bool TryPlayAyuanVaryingSong()
        {
            if (IsResolved || !TidalWindowLocated || AyuanSongPlayed)
                return false;
            AyuanSongPlayed = true;
            return true;
        }

        public bool TryResolveReef(
            RC04Resolution resolution,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (IsResolved || !TidalWindowLocated)
                return false;
            if (!Enum.IsDefined(typeof(RC04Resolution), resolution))
                throw new ArgumentOutOfRangeException(nameof(resolution));

            var record = new RC04ReefRecord(
                CompletedListeningRounds,
                AyuanSongPlayed,
                resolution);

            if (!RC04DingYueReefFacts.TryRecord(record, getInt, setInt))
                return false;

            CompletedRecord = record;
            IsResolved = true;
            return true;
        }
    }
}
