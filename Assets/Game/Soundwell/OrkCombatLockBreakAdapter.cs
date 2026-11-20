using System;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Strictly ORK-subordinate adapter that translates authoritative
    /// OrkPartyBridge.BattleUnitSnapshot transitions into S00BridgeheadCableTracker
    /// or SoundwellAcousticBreakTracker updates and refreshes an optional
    /// CombatLockBreakHudView. Owns zero HP, damage, turn, or defeat authority
    /// and never acts on unverified or guessed enemy intents.
    /// </summary>
    public sealed class OrkCombatLockBreakAdapter
    {
        private readonly S00BridgeheadCableTracker s00Tracker;
        private readonly SoundwellAcousticBreakTracker soundwellTracker;
        private readonly CombatLockBreakHudView hudView;
        private OrkPartyBridge.BattleUnitSnapshot[] previousSnapshots;

        public S00BridgeheadCableTracker S00Tracker => s00Tracker;
        public SoundwellAcousticBreakTracker SoundwellTracker => soundwellTracker;

        public event Action<CombatLockKind, bool> CounterObserved;
        public event Action AcousticCycleReset;

        public OrkCombatLockBreakAdapter(S00BridgeheadCableTracker s00Tracker,
            CombatLockBreakHudView hudView = null)
        {
            this.s00Tracker = s00Tracker ?? throw new ArgumentNullException(nameof(s00Tracker));
            this.hudView = hudView;
        }

        public OrkCombatLockBreakAdapter(SoundwellAcousticBreakTracker soundwellTracker,
            CombatLockBreakHudView hudView = null)
        {
            this.soundwellTracker = soundwellTracker ?? throw new ArgumentNullException(nameof(soundwellTracker));
            this.hudView = hudView;
        }

        public void ResetSnapshotBaseline()
        {
            previousSnapshots = null;
        }

        /// <summary>
        /// Observes the latest authoritative ORK battle snapshots. The first
        /// non-empty snapshot establishes the baseline; subsequent deltas
        /// translate verified ORK Defend / Attack / Enemy-strike transitions.
        /// Returns true if tracker state changed.
        /// </summary>
        public bool ObserveOrkSnapshots(OrkPartyBridge.BattleUnitSnapshot[] currentSnapshots)
        {
            if (currentSnapshots == null || currentSnapshots.Length == 0)
                return false;

            if (previousSnapshots == null)
            {
                previousSnapshots = CloneSnapshots(currentSnapshots);
                return false;
            }

            BattleSnapshotDelta delta = BattleFeelAndTelegraphDirector.ComputeSnapshotDeltas(
                previousSnapshots, currentSnapshots);
            previousSnapshots = CloneSnapshots(currentSnapshots);

            bool changed = false;
            if (s00Tracker != null)
            {
                if (delta.NewGuardAllyIndex >= 0)
                {
                    bool broke = s00Tracker.ResolveHeroCounter(CombatLockKind.ShieldParry);
                    changed = true;
                    CounterObserved?.Invoke(CombatLockKind.ShieldParry, broke);
                }
                else if (delta.TotalEnemyHpLoss > 0)
                {
                    bool broke = s00Tracker.ResolveHeroCounter(CombatLockKind.BladeRepel);
                    changed = true;
                    CounterObserved?.Invoke(CombatLockKind.BladeRepel, broke);
                }
            }
            else if (soundwellTracker != null)
            {
                if (delta.NewGuardAllyIndex >= 0)
                {
                    int before = soundwellTracker.Momentum.Current;
                    soundwellTracker.Momentum.Gain(1);
                    if (soundwellTracker.Momentum.Current != before)
                        changed = true;
                }

                // Once the beast recovers and lands an authoritative ORK strike,
                // start a fresh acoustic lock cycle.
                if (delta.TotalAllyHpLoss > 0 && soundwellTracker.IsBeastBroken)
                {
                    soundwellTracker.ResetCycle();
                    changed = true;
                    AcousticCycleReset?.Invoke();
                }
            }

            if (changed)
                hudView?.Refresh();
            return changed;
        }

        /// <summary>
        /// Records an authoritative Soundwell acoustic action (WetClothMuffle or
        /// DecoyBellRing) triggered by the encounter host and refreshes HUD.
        /// </summary>
        public bool ObserveSoundwellAcousticAction(CombatLockKind action, int momentumCost = 0)
        {
            if (soundwellTracker == null)
                return false;

            bool broke = soundwellTracker.ApplyAcousticCounter(action, momentumCost);
            if (broke)
            {
                hudView?.Refresh();
                CounterObserved?.Invoke(action, true);
            }
            return broke;
        }

        private static OrkPartyBridge.BattleUnitSnapshot[] CloneSnapshots(
            OrkPartyBridge.BattleUnitSnapshot[] source)
        {
            var copy = new OrkPartyBridge.BattleUnitSnapshot[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }
    }
}
