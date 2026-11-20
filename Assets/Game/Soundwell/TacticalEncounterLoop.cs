using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum TacticalVerb
    {
        Strike,
        GuardAndCover,
        MuffleWithWetCloth,
        RingDecoyBell,
        Disengage
    }

    public enum EnemyIntentKind
    {
        ListeningAlert,
        ChargedLunge,
        Recovering
    }

    public enum TacticalEncounterOutcome
    {
        Ongoing,
        Victory,
        DisengagedSafely,
        Defeat
    }

    public readonly struct TacticalTurnFeedback
    {
        public TacticalTurnFeedback(bool accepted, int playerDamageDealt, int enemyDamageDealt,
            bool isCritical, bool interruptedIntent, bool enemyBecameExposed,
            float hitstopSeconds, float shakeIntensity, int noiseAfterTurn,
            TacticalEncounterOutcome outcome, string summaryEn, string summaryZh)
        {
            Accepted = accepted;
            PlayerDamageDealt = playerDamageDealt;
            EnemyDamageDealt = enemyDamageDealt;
            IsCritical = isCritical;
            InterruptedIntent = interruptedIntent;
            EnemyBecameExposed = enemyBecameExposed;
            HitstopSeconds = hitstopSeconds;
            ShakeIntensity = shakeIntensity;
            NoiseAfterTurn = noiseAfterTurn;
            Outcome = outcome;
            SummaryEn = summaryEn;
            SummaryZh = summaryZh;
        }

        public bool Accepted { get; }
        public int PlayerDamageDealt { get; }
        public int EnemyDamageDealt { get; }
        public bool IsCritical { get; }
        public bool InterruptedIntent { get; }
        public bool EnemyBecameExposed { get; }
        public float HitstopSeconds { get; }
        public float ShakeIntensity { get; }
        public int NoiseAfterTurn { get; }
        public TacticalEncounterOutcome Outcome { get; }
        public string SummaryEn { get; }
        public string SummaryZh { get; }
    }

    /// <summary>
    /// Turn-based tactical encounter core with enemy intent telegraphing,
    /// acoustic noise tracking, wet-cloth muffling, decoy-bell interruption,
    /// non-violent disengagement, and game-feel feedback metrics (hitstop /
    /// camera shake / critical popups). Replaces flat Attack/Guard trading
    /// with meaningful per-turn counterplay tied to Return's sound mechanics.
    /// </summary>
    public sealed class TacticalEncounterLoop
    {
        public const int BaseStrikeDamage = 8;
        public const int ExposedStrikeDamage = 14;
        public const int MaxFocus = 4;

        public int Turn { get; private set; } = 1;
        public int PartyHp { get; private set; }
        public int MaxPartyHp { get; }
        public int EnemyHp { get; private set; }
        public int MaxEnemyHp { get; }
        public int Focus { get; private set; }
        public int WetCloths { get; private set; }
        public int NoiseLevel { get; private set; }
        public int BellCooldown { get; private set; }
        public bool EnemyExposed { get; private set; }
        public bool CanDisengageSafely { get; private set; }
        public EnemyIntentKind NextEnemyIntent { get; private set; }
        public TacticalEncounterOutcome Outcome { get; private set; }

        public TacticalEncounterLoop(int maxPartyHp = 30, int maxEnemyHp = 28,
            int startingFocus = 2, int startingWetCloths = 2, int startingNoise = 20)
        {
            if (maxPartyHp <= 0) throw new ArgumentOutOfRangeException(nameof(maxPartyHp));
            if (maxEnemyHp <= 0) throw new ArgumentOutOfRangeException(nameof(maxEnemyHp));

            MaxPartyHp = maxPartyHp;
            PartyHp = maxPartyHp;
            MaxEnemyHp = maxEnemyHp;
            EnemyHp = maxEnemyHp;
            Focus = Mathf.Clamp(startingFocus, 0, MaxFocus);
            WetCloths = Mathf.Max(0, startingWetCloths);
            NoiseLevel = Mathf.Clamp(startingNoise, 0, 100);
            BellCooldown = 0;
            EnemyExposed = false;
            CanDisengageSafely = false;
            NextEnemyIntent = EnemyIntentKind.ListeningAlert;
            Outcome = TacticalEncounterOutcome.Ongoing;
        }

        public bool CanUseVerb(TacticalVerb verb)
        {
            if (Outcome != TacticalEncounterOutcome.Ongoing)
                return false;

            switch (verb)
            {
                case TacticalVerb.Strike:
                case TacticalVerb.GuardAndCover:
                    return true;
                case TacticalVerb.MuffleWithWetCloth:
                    return WetCloths > 0 || Focus >= 1;
                case TacticalVerb.RingDecoyBell:
                    return BellCooldown == 0 && Focus >= 1;
                case TacticalVerb.Disengage:
                    return CanDisengageSafely;
                default:
                    return false;
            }
        }

        public TacticalTurnFeedback ExecuteTurn(TacticalVerb verb)
        {
            if (!CanUseVerb(verb))
            {
                return new TacticalTurnFeedback(
                    accepted: false, 0, 0, false, false, false, 0f, 0f,
                    NoiseLevel, Outcome,
                    "Action unavailable on this turn.",
                    "本回合无法执行该动作。");
            }

            int playerDamage = 0;
            int enemyDamage = 0;
            bool isCrit = false;
            bool interrupted = false;
            bool becameExposed = false;
            bool guarding = false;
            bool muffled = false;
            float hitstop = 0f;
            float shake = 0f;
            string en;
            string zh;

            if (BellCooldown > 0)
                BellCooldown--;

            switch (verb)
            {
                case TacticalVerb.Strike:
                {
                    isCrit = EnemyExposed;
                    playerDamage = isCrit ? ExposedStrikeDamage : BaseStrikeDamage;
                    EnemyHp = Mathf.Max(0, EnemyHp - playerDamage);
                    EnemyExposed = false;
                    CanDisengageSafely = false;
                    NoiseLevel = Mathf.Clamp(NoiseLevel + 25, 0, 100);
                    hitstop = isCrit ? 0.085f : 0.04f;
                    shake = isCrit ? 0.36f : 0.14f;
                    en = isCrit
                        ? "Exploited the opening for " + playerDamage + " critical damage!"
                        : "Struck the foe for " + playerDamage + " damage.";
                    zh = isCrit
                        ? "抓住破绽重击，造成 " + playerDamage + " 点破防暴击！"
                        : "稳击命中，造成 " + playerDamage + " 点伤害。";
                    break;
                }
                case TacticalVerb.GuardAndCover:
                {
                    guarding = true;
                    Focus = Mathf.Min(MaxFocus, Focus + 1);
                    NoiseLevel = Mathf.Max(0, NoiseLevel - 15);
                    CanDisengageSafely = NoiseLevel == 0;
                    en = "Raised guard, damped footsteps, and recovered 1 focus.";
                    zh = "架挡援护，压低脚步声并恢复 1 点战术气势。";
                    break;
                }
                case TacticalVerb.MuffleWithWetCloth:
                {
                    if (WetCloths > 0) WetCloths--;
                    else Focus = Mathf.Max(0, Focus - 1);
                    muffled = true;
                    NoiseLevel = 0;
                    CanDisengageSafely = true;
                    hitstop = 0.03f;
                    en = "Wrapped wet cloth to silence the party.";
                    zh = "甩出湿布消音，全队声响归零。";
                    break;
                }
                case TacticalVerb.RingDecoyBell:
                {
                    Focus = Mathf.Max(0, Focus - 1);
                    BellCooldown = 2;
                    NoiseLevel = 15;
                    interrupted = NextEnemyIntent == EnemyIntentKind.ChargedLunge ||
                        NextEnemyIntent == EnemyIntentKind.ListeningAlert;
                    EnemyExposed = true;
                    becameExposed = true;
                    CanDisengageSafely = true;
                    hitstop = 0.07f;
                    shake = 0.28f;
                    en = "Bronze decoy bell echoed across the chamber — enemy intent broken!";
                    zh = "铜铃声波荡开，强行打断敌方蓄力并打开撤离空隙！";
                    break;
                }
                case TacticalVerb.Disengage:
                {
                    Outcome = TacticalEncounterOutcome.DisengagedSafely;
                    return new TacticalTurnFeedback(
                        accepted: true, 0, 0, false, false, false, 0.04f, 0f,
                        NoiseLevel, Outcome,
                        "Slipped past the distracted beast without further bloodshed.",
                        "趁借声兽失位安全脱身，避免无谓缠斗。");
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(verb));
            }

            if (EnemyHp <= 0)
            {
                Outcome = TacticalEncounterOutcome.Victory;
                return new TacticalTurnFeedback(
                    accepted: true, playerDamage, 0, isCrit, interrupted, becameExposed,
                    Mathf.Max(hitstop, 0.09f), Mathf.Max(shake, 0.32f),
                    NoiseLevel, Outcome, en, zh);
            }

            // Resolve telegraphed enemy intent.
            if (interrupted)
            {
                NextEnemyIntent = EnemyIntentKind.Recovering;
            }
            else if (NextEnemyIntent == EnemyIntentKind.ListeningAlert)
            {
                if (muffled || NoiseLevel == 0)
                {
                    EnemyExposed = true;
                    becameExposed = true;
                    CanDisengageSafely = true;
                    NextEnemyIntent = EnemyIntentKind.ChargedLunge;
                    en += " The beast lunged at silence and exposed its flank!";
                    zh += " 借声兽扑空失衡，露出破绽！";
                }
                else
                {
                    int raw = NoiseLevel >= 40 ? 7 : 4;
                    enemyDamage = guarding ? raw / 2 : raw;
                    PartyHp = Mathf.Max(0, PartyHp - enemyDamage);
                    shake = Mathf.Max(shake, guarding ? 0.10f : 0.22f);
                    NextEnemyIntent = EnemyIntentKind.ChargedLunge;
                }
            }
            else if (NextEnemyIntent == EnemyIntentKind.ChargedLunge)
            {
                int raw = 12;
                enemyDamage = guarding ? raw / 2 : raw;
                PartyHp = Mathf.Max(0, PartyHp - enemyDamage);
                EnemyExposed = false;
                shake = Mathf.Max(shake, guarding ? 0.16f : 0.38f);
                hitstop = Mathf.Max(hitstop, 0.06f);
                NextEnemyIntent = EnemyIntentKind.ListeningAlert;
            }
            else
            {
                NextEnemyIntent = EnemyIntentKind.ListeningAlert;
            }

            if (PartyHp <= 0)
                Outcome = TacticalEncounterOutcome.Defeat;

            Turn++;
            return new TacticalTurnFeedback(
                accepted: true, playerDamage, enemyDamage, isCrit, interrupted,
                becameExposed, hitstop, shake, NoiseLevel, Outcome, en, zh);
        }

        public static string DescribeIntent(EnemyIntentKind intent, bool zh)
        {
            switch (intent)
            {
                case EnemyIntentKind.ListeningAlert:
                    return zh
                        ? "敌方意图：竖耳监听（高声响会引来扑击；用湿布消音可令其扑空破防）"
                        : "Intent: Listening Alert (loud moves draw a strike; muffle to expose flank)";
                case EnemyIntentKind.ChargedLunge:
                    return zh
                        ? "敌方意图：蓄力重扑（高额伤害；敲铜铃可打断，或架挡减半）"
                        : "Intent: Charged Lunge (heavy damage; ring decoy bell to interrupt or guard)";
                case EnemyIntentKind.Recovering:
                    return zh
                        ? "敌方意图：失衡重整（本拍无攻击，适合破防强攻或安全撤离）"
                        : "Intent: Staggered (no attack this beat — punish or disengage safely)";
                default:
                    throw new ArgumentOutOfRangeException(nameof(intent));
            }
        }
    }
}
