using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum EnemyIntentKind
    {
        ListeningAlert,
        ChargedLunge,
        Recovering
    }

    public enum AcousticActionKind
    {
        OrkAttack,
        OrkGuard,
        WetClothMuffle,
        DecoyBell
    }

    /// <summary>
    /// Presentation-only game-feel and telegraph feedback emitted after an
    /// acoustic action or an ORK-authoritative HP change. Carries no HP,
    /// damage math, or victory/defeat authority.
    /// </summary>
    public readonly struct AcousticPresentationFeedback
    {
        public AcousticPresentationFeedback(bool accepted, bool interruptedIntent,
            bool enemyExposedForTelegraph, bool canOfferDisengage,
            float hitstopSeconds, float shakeIntensity, int noiseAfterAction,
            string summaryEn, string summaryZh)
        {
            Accepted = accepted;
            InterruptedIntent = interruptedIntent;
            EnemyExposedForTelegraph = enemyExposedForTelegraph;
            CanOfferDisengage = canOfferDisengage;
            HitstopSeconds = hitstopSeconds;
            ShakeIntensity = shakeIntensity;
            NoiseAfterAction = noiseAfterAction;
            SummaryEn = summaryEn;
            SummaryZh = summaryZh;
        }

        public bool Accepted { get; }
        public bool InterruptedIntent { get; }
        public bool EnemyExposedForTelegraph { get; }
        public bool CanOfferDisengage { get; }
        public float HitstopSeconds { get; }
        public float ShakeIntensity { get; }
        public int NoiseAfterAction { get; }
        public string SummaryEn { get; }
        public string SummaryZh { get; }
    }

    /// <summary>
    /// Standalone acoustic noise, enemy-intent telegraph, and presentation
    /// feedback helper. Strictly subordinate to ORK 3 (via OrkPartyBridge):
    /// this layer owns NO HP, NO damage calculation, NO turn authority, and
    /// NO victory/defeat state.
    /// </summary>
    public sealed class AcousticIntentTelegraph
    {
        public int NoiseLevel { get; private set; }
        public int BellCooldownTurns { get; private set; }
        public bool EnemyIntentExposed { get; private set; }
        public bool CanOfferNonViolentDisengage { get; private set; }
        public EnemyIntentKind CurrentIntent { get; private set; }

        public AcousticIntentTelegraph(int startingNoise = 20,
            EnemyIntentKind initialIntent = EnemyIntentKind.ListeningAlert)
        {
            if (!Enum.IsDefined(typeof(EnemyIntentKind), initialIntent))
                throw new ArgumentOutOfRangeException(nameof(initialIntent));

            NoiseLevel = Mathf.Clamp(startingNoise, 0, 100);
            CurrentIntent = initialIntent;
            BellCooldownTurns = 0;
            EnemyIntentExposed = false;
            CanOfferNonViolentDisengage = false;
        }

        public bool CanTriggerAcousticAction(AcousticActionKind action)
        {
            if (!Enum.IsDefined(typeof(AcousticActionKind), action))
                return false;
            if (action == AcousticActionKind.DecoyBell)
                return BellCooldownTurns == 0;
            return true;
        }

        /// <summary>
        /// Updates acoustic noise, enemy-intent telegraphing, and presentation
        /// cues in response to a player action routed through ORK/Soundwell.
        /// Never mutates HP or resolves combat victory/defeat.
        /// </summary>
        public AcousticPresentationFeedback RecordPlayerAction(AcousticActionKind action)
        {
            if (!Enum.IsDefined(typeof(AcousticActionKind), action))
            {
                return new AcousticPresentationFeedback(
                    accepted: false, false, EnemyIntentExposed,
                    CanOfferNonViolentDisengage, 0f, 0f, NoiseLevel,
                    "Action unavailable.",
                    "该动作当前不可用。");
            }
            if (!CanTriggerAcousticAction(action))
            {
                return new AcousticPresentationFeedback(
                    accepted: false, false, EnemyIntentExposed,
                    CanOfferNonViolentDisengage, 0f, 0f, NoiseLevel,
                    "Decoy bell is still cooling down.",
                    "诱敌铜铃冷却中，暂无法再次敲响。");
            }

            if (BellCooldownTurns > 0)
                BellCooldownTurns--;

            switch (action)
            {
                case AcousticActionKind.OrkAttack:
                {
                    bool exploited = EnemyIntentExposed;
                    EnemyIntentExposed = false;
                    CanOfferNonViolentDisengage = false;
                    NoiseLevel = Mathf.Clamp(NoiseLevel + 25, 0, 100);
                    return new AcousticPresentationFeedback(
                        accepted: true, false, false, false,
                        exploited ? 0.075f : 0.035f,
                        exploited ? 0.30f : 0.12f,
                        NoiseLevel,
                        exploited
                            ? "Attack routed into ORK against an exposed telegraph window."
                            : "Attack routed into ORK; footsteps raised chamber noise.",
                        exploited
                            ? "趁敌方意图破绽向 ORK 发起攻击。"
                            : "已向 ORK 发起攻击，井内声响随之升高。");
                }
                case AcousticActionKind.OrkGuard:
                {
                    NoiseLevel = Mathf.Max(0, NoiseLevel - 15);
                    CanOfferNonViolentDisengage = NoiseLevel == 0;
                    return new AcousticPresentationFeedback(
                        accepted: true, false, EnemyIntentExposed,
                        CanOfferNonViolentDisengage, 0f, 0f, NoiseLevel,
                        "Guard routed into ORK while damping party noise.",
                        "已向 ORK 发起架挡，同时压低小队声响。");
                }
                case AcousticActionKind.WetClothMuffle:
                {
                    NoiseLevel = 0;
                    bool counteredListen = CurrentIntent == EnemyIntentKind.ListeningAlert;
                    if (counteredListen)
                        EnemyIntentExposed = true;
                    CanOfferNonViolentDisengage = true;
                    return new AcousticPresentationFeedback(
                        accepted: true, counteredListen, EnemyIntentExposed, true,
                        0.03f, 0f, NoiseLevel,
                        counteredListen
                            ? "Wet cloth silenced the party — listening intent broken."
                            : "Wet cloth silenced the party; safe disengage window open.",
                        counteredListen
                            ? "湿布消音归零，借声兽监听落空并露出破绽。"
                            : "湿布消音归零，已打开安全脱身空隙。");
                }
                case AcousticActionKind.DecoyBell:
                {
                    BellCooldownTurns = 2;
                    NoiseLevel = 15;
                    bool interrupted = CurrentIntent == EnemyIntentKind.ChargedLunge ||
                        CurrentIntent == EnemyIntentKind.ListeningAlert;
                    EnemyIntentExposed = true;
                    CanOfferNonViolentDisengage = true;
                    if (interrupted)
                        CurrentIntent = EnemyIntentKind.Recovering;
                    return new AcousticPresentationFeedback(
                        accepted: true, interrupted, true, true,
                        0.07f, 0.26f, NoiseLevel,
                        "Bronze decoy bell echoed across the chamber — intent interrupted.",
                        "铜铃声波荡开，打断敌方蓄力意图并打开脱身空隙。");
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(action));
            }
        }

        /// <summary>
        /// Advances the telegraphed enemy intent after ORK completes an enemy turn.
        /// </summary>
        public void AdvanceTelegraphAfterOrkEnemyTurn()
        {
            switch (CurrentIntent)
            {
                case EnemyIntentKind.ListeningAlert:
                    CurrentIntent = EnemyIntentKind.ChargedLunge;
                    break;
                case EnemyIntentKind.ChargedLunge:
                    EnemyIntentExposed = false;
                    CurrentIntent = EnemyIntentKind.ListeningAlert;
                    break;
                case EnemyIntentKind.Recovering:
                    CurrentIntent = EnemyIntentKind.ListeningAlert;
                    break;
                default:
                    CurrentIntent = EnemyIntentKind.ListeningAlert;
                    break;
            }
        }

        /// <summary>
        /// Converts ORK-authoritative HP deltas into hitstop and camera-shake
        /// cues. Zero HP loss produces zero impact feedback.
        /// </summary>
        public static (float hitstopSeconds, float shakeIntensity) ComputeOrkHitFeedback(
            int orkEnemyHpLoss, int orkPartyHpLoss, bool wasExposedTelegraph)
        {
            int enemyLoss = Mathf.Max(0, orkEnemyHpLoss);
            int partyLoss = Mathf.Max(0, orkPartyHpLoss);
            if (enemyLoss == 0 && partyLoss == 0)
                return (0f, 0f);

            float hitstop = 0f;
            float shake = 0f;
            if (enemyLoss > 0)
            {
                hitstop = Mathf.Max(hitstop, wasExposedTelegraph ? 0.08f : 0.04f);
                shake = Mathf.Max(shake, wasExposedTelegraph ? 0.34f : 0.15f);
            }
            if (partyLoss > 0)
            {
                hitstop = Mathf.Max(hitstop, 0.055f);
                shake = Mathf.Max(shake, Mathf.Clamp(0.14f + partyLoss * 0.02f, 0.14f, 0.40f));
            }
            return (hitstop, shake);
        }

        public static string DescribeIntent(EnemyIntentKind intent, bool zh)
        {
            switch (intent)
            {
                case EnemyIntentKind.ListeningAlert:
                    return zh
                        ? "敌方意图：竖耳监听（高声响会引来扑击；湿布消音可破其监听）"
                        : "Intent: Listening Alert (loud moves draw attention; muffle to break lock)";
                case EnemyIntentKind.ChargedLunge:
                    return zh
                        ? "敌方意图：蓄力重扑（敲铜铃可打断意图，或向 ORK 架挡减伤）"
                        : "Intent: Charged Lunge (ring decoy bell to interrupt or guard in ORK)";
                case EnemyIntentKind.Recovering:
                    return zh
                        ? "敌方意图：失衡重整（意图已被打断，适合强攻或趁机脱身）"
                        : "Intent: Staggered (intent interrupted — press attack in ORK or disengage)";
                default:
                    throw new ArgumentOutOfRangeException(nameof(intent));
            }
        }
    }
}
