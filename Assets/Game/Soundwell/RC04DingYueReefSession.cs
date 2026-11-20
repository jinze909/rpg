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
        // The no-A Yuan route cannot impersonate him. Real crew consent,
        // safety, capability and extra preparation cost must be verified
        // by the host before the decoy ambush can be prepared.
        public bool DecoyAmbushPrepared { get; private set; }
        public bool DecoyBeastDefeatVerified { get; private set; }
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
            if (IsResolved || !TidalWindowLocated || AyuanSongPlayed ||
                DecoyAmbushPrepared)
                return false;
            AyuanSongPlayed = true;
            return true;
        }

        /// <summary>
        /// Alternative to A Yuan's varying song when he is absent. The
        /// caller must confirm an actual safe return window, consenting
        /// participating crew, real battle capability and the paid extra
        /// decoy-preparation cost. No absent companion sings on this path.
        /// </summary>
        public bool TryPrepareConsentedDecoyAmbush(
            bool safeReturnConfirmed,
            bool crewVolunteered,
            bool combatCapable,
            bool extraPreparationCostPaid)
        {
            if (IsResolved || !TidalWindowLocated || AyuanSongPlayed ||
                DecoyAmbushPrepared || !safeReturnConfirmed ||
                !crewVolunteered || !combatCapable ||
                !extraPreparationCostPaid)
                return false;
            DecoyAmbushPrepared = true;
            return true;
        }

        /// <summary>
        /// The scene's actual combat resolution must verify the ambush
        /// defeated the beast; preparing a lure alone cannot claim a kill.
        /// </summary>
        public bool TryConfirmDecoyBeastDefeat(bool actualBattleVictory)
        {
            if (IsResolved || !DecoyAmbushPrepared ||
                DecoyBeastDefeatVerified || !actualBattleVictory)
                return false;
            DecoyBeastDefeatVerified = true;
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
            if (resolution == RC04Resolution.KillMimicBeastWithPreparedDecoy &&
                (!DecoyAmbushPrepared || !DecoyBeastDefeatVerified))
                return false;

            var record = new RC04ReefRecord(
                CompletedListeningRounds,
                AyuanSongPlayed,
                resolution,
                decoyAmbushPrepared: DecoyAmbushPrepared);

            if (!RC04DingYueReefFacts.TryRecord(record, getInt, setInt))
                return false;

            CompletedRecord = record;
            IsResolved = true;
            return true;
        }
    }
}
