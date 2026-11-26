using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum CombatLockKind
    {
        ShieldParry,
        BladeRepel,
        WetClothMuffle,
        DecoyBellRing
    }

    public enum S00BridgeThreatKind
    {
        SpearThrustAtHero,
        SapperRushCable
    }

    /// <summary>
    /// Shared tactical Momentum gauge (0..3, similar to Boost/BP).
    /// Strictly subordinate to ORK 3: owns no HP, damage, or defeat authority.
    /// </summary>
    public sealed class MomentumGauge
    {
        public const int MaxPoints = 3;

        public int Current { get; private set; }

        public MomentumGauge(int startingPoints = 1)
        {
            Current = Mathf.Clamp(startingPoints, 0, MaxPoints);
        }

        public void Gain(int amount)
        {
            if (amount <= 0) return;
            int space = MaxPoints - Current;
            if (amount >= space)
                Current = MaxPoints;
            else
                Current += amount;
        }

        public bool TrySpend(int cost)
        {
            if (cost < 0) return false;
            if (cost == 0) return true;
            if (Current < cost) return false;
            Current -= cost;
            return true;
        }
    }

    /// <summary>
    /// Sea-of-Stars / Octopath-style enemy lock bar. Breaking partial locks
    /// weakens the telegraphed threat; breaking all locks cancels the enemy
    /// skill and enters the Broken (staggered) opening state.
    /// </summary>
    public sealed class EnemyLockBar
    {
        private readonly List<CombatLockKind> locks;
        private readonly bool[] brokenFlags;

        public int TotalLocks => locks.Count;
        public int BrokenCount { get; private set; }
        public int RemainingLocks => TotalLocks - BrokenCount;
        public bool IsBroken => TotalLocks > 0 && BrokenCount >= TotalLocks;

        /// <summary>
        /// Percentage threat mitigation (0..100). Each broken lock reduces
        /// threat linearly up to 50% for partial breaks, and 100% when fully broken.
        /// </summary>
        public int ThreatMitigationPercent
        {
            get
            {
                if (TotalLocks == 0 || BrokenCount == 0) return 0;
                if (IsBroken) return 100;
                return Mathf.RoundToInt(50f * BrokenCount / TotalLocks);
            }
        }

        public EnemyLockBar(params CombatLockKind[] requiredLocks)
        {
            if (requiredLocks == null || requiredLocks.Length == 0)
                throw new ArgumentException("At least one lock is required.", nameof(requiredLocks));
            for (int i = 0; i < requiredLocks.Length; i++)
            {
                if (!Enum.IsDefined(typeof(CombatLockKind), requiredLocks[i]))
                    throw new ArgumentOutOfRangeException(nameof(requiredLocks));
            }
            locks = new List<CombatLockKind>(requiredLocks);
            brokenFlags = new bool[locks.Count];
            BrokenCount = 0;
        }

        public bool HasRemainingLock(CombatLockKind kind)
        {
            for (int i = 0; i < locks.Count; i++)
                if (!brokenFlags[i] && locks[i] == kind)
                    return true;
            return false;
        }

        public bool TryBreakLock(CombatLockKind kind)
        {
            for (int i = 0; i < locks.Count; i++)
            {
                if (!brokenFlags[i] && locks[i] == kind)
                {
                    brokenFlags[i] = true;
                    BrokenCount++;
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Objective tracker for S00 Whitewater Crossing solo bridgehead defense:
    /// the player holds off Spear Thrusts and Cable Sappers alone while Shen Li
    /// cuts the pontoon cable across 3 bells. ORK 3 remains sole HP/defeat authority.
    /// </summary>
    public sealed class S00BridgeheadCableTracker
    {
        public const int RequiredBells = 3;

        public int BellsCompleted { get; private set; }
        public bool ReadyForThirdBellCollapse => BellsCompleted >= RequiredBells;
        public S00BridgeThreatKind CurrentThreat { get; private set; }
        public EnemyLockBar CurrentLocks { get; private set; }
        public MomentumGauge Momentum { get; }

        public S00BridgeheadCableTracker(int startingMomentum = 1)
        {
            Momentum = new MomentumGauge(startingMomentum);
            BellsCompleted = 0;
            SetThreat(S00BridgeThreatKind.SpearThrustAtHero);
        }

        public bool ResolveHeroCounter(CombatLockKind counterAction)
        {
            if (ReadyForThirdBellCollapse || !Enum.IsDefined(typeof(CombatLockKind), counterAction))
                return false;

            bool brokeLock = CurrentLocks.TryBreakLock(counterAction);
            if (CurrentThreat == S00BridgeThreatKind.SpearThrustAtHero)
            {
                if (brokeLock)
                    Momentum.Gain(1);
                BellsCompleted++;
            }
            else
            {
                // Sapper rushing Shen Li's cable: repelling preserves this bell's progress;
                // failing to repel stalls Shen Li's cut without advancing the bell.
                if (brokeLock)
                {
                    Momentum.Gain(1);
                    BellsCompleted++;
                }
            }

            if (!ReadyForThirdBellCollapse)
            {
                // A failed cable defense must remain an unresolved sapper
                // threat. Alternating to the spear anyway would let players
                // progress all three bells while never defending Shen Li.
                if (CurrentThreat == S00BridgeThreatKind.SpearThrustAtHero ||
                    brokeLock)
                {
                    SetThreat(CurrentThreat == S00BridgeThreatKind.SpearThrustAtHero
                        ? S00BridgeThreatKind.SapperRushCable
                        : S00BridgeThreatKind.SpearThrustAtHero);
                }
            }
            return brokeLock;
        }

        public static string DescribeThreat(S00BridgeThreatKind threat, bool zh)
        {
            switch (threat)
            {
                case S00BridgeThreatKind.SpearThrustAtHero:
                    return zh
                        ? "持矛小队蓄力直刺桥头 — 举盾格挡可化解重刺并稳住沈砾断缆"
                        : "Spear squad lunges at you — Shield Parry mitigates the thrust and buys Shen Li a bell";
                case S00BridgeThreatKind.SapperRushCable:
                    return zh
                        ? "敌方工兵扑向浮桥缆索 — 挥刀逼退以保住本拍断缆进度！"
                        : "Sapper rushes the pontoon cable — Blade Repel interrupts them to keep Shen Li cutting!";
                default:
                    throw new ArgumentOutOfRangeException(nameof(threat));
            }
        }

        private void SetThreat(S00BridgeThreatKind threat)
        {
            CurrentThreat = threat;
            CurrentLocks = threat == S00BridgeThreatKind.SpearThrustAtHero
                ? new EnemyLockBar(CombatLockKind.ShieldParry)
                : new EnemyLockBar(CombatLockKind.BladeRepel);
        }
    }

    /// <summary>
    /// Objective tracker for Soundwell 3-role acoustic lock-breaking against the
    /// Sound-borrowing Beast: breaking both acoustic locks (WetClothMuffle +
    /// DecoyBellRing) staggers the beast and opens both an ORK punish window
    /// and a non-violent gate/carry escape window.
    /// </summary>
    public sealed class SoundwellAcousticBreakTracker
    {
        public EnemyLockBar AcousticLocks { get; private set; }
        public MomentumGauge Momentum { get; }
        public bool IsBeastBroken => AcousticLocks.IsBroken;
        public bool CanTriggerNonViolentEscape => IsBeastBroken;

        public SoundwellAcousticBreakTracker(int startingMomentum = 1)
        {
            Momentum = new MomentumGauge(startingMomentum);
            ResetCycle();
        }

        public void ResetCycle()
        {
            AcousticLocks = new EnemyLockBar(
                CombatLockKind.WetClothMuffle,
                CombatLockKind.DecoyBellRing);
        }

        public bool ApplyAcousticCounter(CombatLockKind action, int momentumCost = 0)
        {
            if (IsBeastBroken || momentumCost < 0 ||
                !Enum.IsDefined(typeof(CombatLockKind), action) ||
                !AcousticLocks.HasRemainingLock(action))
                return false;
            if (momentumCost > 0 && !Momentum.TrySpend(momentumCost))
                return false;

            return AcousticLocks.TryBreakLock(action);
        }
    }
}
