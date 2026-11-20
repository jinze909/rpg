using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum PartyCombatRole
    {
        VanguardLead = 0,
        AcousticBreaker = 1,
        FieldSupport = 2
    }

    public enum RoleTacticalVerb
    {
        OrkAttack,
        OrkGuardCoverAlly,
        OrkBreakStrike,
        DecoyBellInterrupt,
        WetClothMuffle,
        OrkGuardWithdraw
    }

    public enum BattleThreatKind
    {
        ListeningScan,
        LockedCharge,
        StaggeredOpening
    }

    public readonly struct RoleActionOption
    {
        public RoleActionOption(RoleTacticalVerb verb, string labelEn, string labelZh,
            string shortHintEn, string shortHintZh, bool routesToOrkAttack, bool routesToOrkGuard,
            bool recommendedNow)
        {
            Verb = verb;
            LabelEn = labelEn;
            LabelZh = labelZh;
            ShortHintEn = shortHintEn;
            ShortHintZh = shortHintZh;
            RoutesToOrkAttack = routesToOrkAttack;
            RoutesToOrkGuard = routesToOrkGuard;
            RecommendedNow = recommendedNow;
        }

        public RoleTacticalVerb Verb { get; }
        public string LabelEn { get; }
        public string LabelZh { get; }
        public string ShortHintEn { get; }
        public string ShortHintZh { get; }
        public bool RoutesToOrkAttack { get; }
        public bool RoutesToOrkGuard { get; }
        public bool RecommendedNow { get; }
    }

    public readonly struct BattleThreatVisualCue
    {
        public BattleThreatVisualCue(BattleThreatKind kind, int lockedAllyIndex,
            Color indicatorColor, string badgeEn, string badgeZh, bool drawTargetLine)
        {
            Kind = kind;
            LockedAllyIndex = lockedAllyIndex;
            IndicatorColor = indicatorColor;
            BadgeEn = badgeEn;
            BadgeZh = badgeZh;
            DrawTargetLine = drawTargetLine;
        }

        public BattleThreatKind Kind { get; }
        public int LockedAllyIndex { get; }
        public Color IndicatorColor { get; }
        public string BadgeEn { get; }
        public string BadgeZh { get; }
        public bool DrawTargetLine { get; }
    }

    /// <summary>
    /// Differentiates the 3 fixed-formation party slots so every ORK turn
    /// presents two distinct, readable tactical choices instead of three
    /// identical Attack/Guard clones or tedious micro-stepping.
    /// Strictly subordinate to ORK 3: owns no HP, damage, or victory state.
    /// </summary>
    public static class BattleRoleKit
    {
        public static PartyCombatRole RoleForSlot(int memberIndex)
        {
            if (memberIndex <= 0) return PartyCombatRole.VanguardLead;
            if (memberIndex == 1) return PartyCombatRole.AcousticBreaker;
            return PartyCombatRole.FieldSupport;
        }

        public static string RoleName(PartyCombatRole role, bool zh)
        {
            switch (role)
            {
                case PartyCombatRole.VanguardLead:
                    return zh ? "前排护阵" : "Vanguard";
                case PartyCombatRole.AcousticBreaker:
                    return zh ? "声学破局" : "Breaker";
                case PartyCombatRole.FieldSupport:
                    return zh ? "控场支援" : "Support";
                default:
                    throw new ArgumentOutOfRangeException(nameof(role));
            }
        }

        public static (RoleActionOption primary, RoleActionOption secondary) GetTwoChoices(
            int memberIndex, BattleThreatKind threat, int lockedAllyIndex)
        {
            PartyCombatRole role = RoleForSlot(memberIndex);
            bool enemyCharging = threat == BattleThreatKind.LockedCharge;
            bool enemyListening = threat == BattleThreatKind.ListeningScan;
            bool enemyStaggered = threat == BattleThreatKind.StaggeredOpening;
            bool allyTargeted = lockedAllyIndex >= 0 && enemyCharging;

            switch (role)
            {
                case PartyCombatRole.VanguardLead:
                    return (
                        new RoleActionOption(
                            RoleTacticalVerb.OrkAttack,
                            "Press Strike", "稳击压阵",
                            "Direct ORK strike", "直接发起 ORK 攻击",
                            routesToOrkAttack: true, routesToOrkGuard: false,
                            recommendedNow: enemyStaggered),
                        new RoleActionOption(
                            RoleTacticalVerb.OrkGuardCoverAlly,
                            "Shield Cover", "举盾援护",
                            "Guard in ORK & cover locked ally", "ORK 防御并护住被锁定队友",
                            routesToOrkAttack: false, routesToOrkGuard: true,
                            recommendedNow: allyTargeted));

                case PartyCombatRole.AcousticBreaker:
                    return (
                        new RoleActionOption(
                            RoleTacticalVerb.OrkBreakStrike,
                            "Punish Opening", "破绽重击",
                            "ORK strike on staggered foe", "趁失衡发起 ORK 攻击",
                            routesToOrkAttack: true, routesToOrkGuard: false,
                            recommendedNow: enemyStaggered),
                        new RoleActionOption(
                            RoleTacticalVerb.DecoyBellInterrupt,
                            "Ring Bell", "敲铃断招",
                            "Interrupt charged lunge", "打断敌方红色蓄力锁定",
                            routesToOrkAttack: false, routesToOrkGuard: false,
                            recommendedNow: enemyCharging));

                case PartyCombatRole.FieldSupport:
                    return (
                        new RoleActionOption(
                            RoleTacticalVerb.WetClothMuffle,
                            "Wet-Cloth Muffle", "湿布消音",
                            "Break listening scan", "压低声响破除监听锁定",
                            routesToOrkAttack: false, routesToOrkGuard: false,
                            recommendedNow: enemyListening),
                        new RoleActionOption(
                            RoleTacticalVerb.OrkGuardWithdraw,
                            "Cover & Steady", "掩护稳阵",
                            "Guard in ORK & ready escape", "ORK 防御并稳住阵型",
                            routesToOrkAttack: false, routesToOrkGuard: true,
                            recommendedNow: !enemyListening));

                default:
                    throw new ArgumentOutOfRangeException(nameof(memberIndex));
            }
        }

        public static BattleThreatVisualCue BuildThreatVisual(
            BattleThreatKind kind, int lockedAllyIndex)
        {
            switch (kind)
            {
                case BattleThreatKind.LockedCharge:
                    return new BattleThreatVisualCue(
                        kind, Mathf.Max(0, lockedAllyIndex),
                        new Color(0.92f, 0.24f, 0.22f, 1f),
                        "LOCKED LUNGE", "红线锁定 · 蓄力重扑",
                        drawTargetLine: true);
                case BattleThreatKind.ListeningScan:
                    return new BattleThreatVisualCue(
                        kind, -1,
                        new Color(0.95f, 0.72f, 0.28f, 1f),
                        "LISTENING", "竖耳监听",
                        drawTargetLine: false);
                case BattleThreatKind.StaggeredOpening:
                    return new BattleThreatVisualCue(
                        kind, -1,
                        new Color(0.55f, 0.78f, 0.90f, 1f),
                        "OPENING", "失衡破绽",
                        drawTargetLine: false);
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
    }

    /// <summary>
    /// Pure visual exchange choreography for ORK-driven combatant GameObjects:
    /// forward lunge on strike, hit recoil + white flash strictly gated on
    /// ORK-authoritative HP loss, and a clean world-space threat target line.
    /// </summary>
    public static class BattleExchangeChoreography
    {
        public static Vector2 ComputeLungeOffset(float normalizedTime,
            Vector2 directionToTarget, float lungeDistance = 0.75f)
        {
            float t = Mathf.Clamp01(normalizedTime);
            if (directionToTarget.sqrMagnitude < 0.0001f || lungeDistance <= 0f)
                return Vector2.zero;

            Vector2 dir = directionToTarget.normalized;
            // Fast snap forward (0..0.35), crisp return (0.35..1.0).
            float curve = t <= 0.35f
                ? Mathf.Sin((t / 0.35f) * Mathf.PI * 0.5f)
                : Mathf.Cos(((t - 0.35f) / 0.65f) * Mathf.PI * 0.5f);
            return dir * (lungeDistance * Mathf.Max(0f, curve));
        }

        public static (Vector2 recoilOffset, float flashAmount) ComputeHitRecoilAndFlash(
            float normalizedTime, Vector2 knockbackDirection, int actualOrkHpLoss,
            float maxRecoilDistance = 0.42f)
        {
            if (actualOrkHpLoss <= 0 || knockbackDirection.sqrMagnitude < 0.0001f)
                return (Vector2.zero, 0f);

            float t = Mathf.Clamp01(normalizedTime);
            float decay = 1f - t;
            Vector2 recoil = knockbackDirection.normalized * (maxRecoilDistance * decay * decay);
            float flash = t < 0.35f ? 1f - (t / 0.35f) : 0f;
            return (recoil, Mathf.Clamp01(flash));
        }

        public static LineRenderer CreateOrUpdateThreatLine(Transform parent,
            LineRenderer existing, Vector3 enemyPosition, Vector3 targetAllyPosition,
            BattleThreatVisualCue cue)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            LineRenderer line = existing;
            if (line == null)
            {
                var go = new GameObject("Battle threat telegraph line");
                go.transform.SetParent(parent, false);
                line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.startWidth = 0.08f;
                line.endWidth = 0.04f;
                line.sortingOrder = 40;
            }

            line.enabled = cue.DrawTargetLine;
            if (cue.DrawTargetLine)
            {
                line.startColor = cue.IndicatorColor;
                line.endColor = new Color(
                    cue.IndicatorColor.r, cue.IndicatorColor.g, cue.IndicatorColor.b, 0.35f);
                line.SetPosition(0, enemyPosition);
                line.SetPosition(1, targetAllyPosition);
            }
            return line;
        }

        public static void SafeDestroy(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(obj);
            else
                UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}
