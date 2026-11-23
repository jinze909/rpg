using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum RpgWorld500xInteractionKind : byte
    {
        None = 0,
        ShortcutUnlocked = 1,
        CacheLooted = 2,
        NpcDialogue = 3,
        StorySpotInspected = 4
    }

    public readonly struct RpgWorld500xInteractionResult
    {
        public RpgWorld500xInteractionResult(
            RpgWorld500xInteractionKind kind,
            string entityId,
            string titleZh,
            string bodyZh,
            string revealedMarkerId = null,
            int saltSilverGranted = 0,
            bool firstTimeDiscovery = false)
        {
            Kind = kind;
            EntityId = entityId ?? string.Empty;
            TitleZh = titleZh ?? string.Empty;
            BodyZh = bodyZh ?? string.Empty;
            RevealedMarkerId = revealedMarkerId ?? string.Empty;
            SaltSilverGranted = saltSilverGranted;
            FirstTimeDiscovery = firstTimeDiscovery;
        }

        public RpgWorld500xInteractionKind Kind { get; }
        public string EntityId { get; }
        public string TitleZh { get; }
        public string BodyZh { get; }
        public string RevealedMarkerId { get; }
        public int SaltSilverGranted { get; }
        public bool FirstTimeDiscovery { get; }
        public bool Succeeded => Kind != RpgWorld500xInteractionKind.None;

        public static RpgWorld500xInteractionResult None =>
            new RpgWorld500xInteractionResult(RpgWorld500xInteractionKind.None, string.Empty, string.Empty, string.Empty);
    }

    public readonly struct RpgWorld500xEffectiveExplorationModifiers
    {
        public RpgWorld500xEffectiveExplorationModifiers(
            RpgWorld500xAtmosphereSnapshot atmosphere,
            RpgActiveAmbientState ambientEvent,
            float effectiveMoveSpeedMultiplier,
            float effectivePatrolSightMultiplier,
            float effectivePatrolNoiseMultiplier,
            float effectiveEncounterTensionMultiplier,
            string combinedStatusCaptionZh)
        {
            Atmosphere = atmosphere;
            AmbientEvent = ambientEvent;
            EffectiveMoveSpeedMultiplier = effectiveMoveSpeedMultiplier;
            EffectivePatrolSightMultiplier = effectivePatrolSightMultiplier;
            EffectivePatrolNoiseMultiplier = effectivePatrolNoiseMultiplier;
            EffectiveEncounterTensionMultiplier = effectiveEncounterTensionMultiplier;
            CombinedStatusCaptionZh = combinedStatusCaptionZh ?? string.Empty;
        }

        public RpgWorld500xAtmosphereSnapshot Atmosphere { get; }
        public RpgActiveAmbientState AmbientEvent { get; }
        public float EffectiveMoveSpeedMultiplier { get; }
        public float EffectivePatrolSightMultiplier { get; }
        public float EffectivePatrolNoiseMultiplier { get; }
        public float EffectiveEncounterTensionMultiplier { get; }
        public string CombinedStatusCaptionZh { get; }
    }

    /// <summary>
    /// Unified coordinator combining 500x world interactive activities (shortcuts,
    /// hidden caches/relics, field NPCs & rumor brokers, environmental story spots)
    /// with live 24h weather/time-of-day + regional ambient micro-event modifiers.
    /// </summary>
    public sealed class RpgWorld500xWorldActivityDirector
    {
        private readonly RpgWorldMapId _mapId;
        private readonly RpgWorld500xCachesAndShortcutsController _cachesAndShortcuts;
        private readonly RpgWorld500xLivingWorldService _livingWorld;
        private readonly RpgWorld500xWeatherAndTimeOfDayService _atmosphere;
        private readonly Action<string, string> _onToast;

        public RpgWorldMapId MapId => _mapId;
        public int TotalSaltSilverEarned { get; private set; }
        public int InteractionCount { get; private set; }

        public RpgWorld500xWorldActivityDirector(
            RpgWorld500xMapLayout layout,
            RpgMapExplorationTracker explorationTracker,
            Func<string, int> getInt = null,
            Action<string, int> setInt = null,
            RpgWorld500xWeatherAndTimeOfDayService atmosphere = null,
            Action<string, string> onToast = null)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            if (explorationTracker == null)
            {
                throw new ArgumentNullException(nameof(explorationTracker));
            }

            _mapId = layout.MapId;
            _onToast = onToast;
            _cachesAndShortcuts = new RpgWorld500xCachesAndShortcutsController(
                layout,
                getInt,
                setInt,
                cache => TotalSaltSilverEarned += cache.SaltSilverReward);
            _livingWorld = new RpgWorld500xLivingWorldService(_mapId, explorationTracker, getInt, setInt);
            _atmosphere = atmosphere ?? new RpgWorld500xWeatherAndTimeOfDayService();
        }

        public RpgWorld500xCachesAndShortcutsController CachesAndShortcuts => _cachesAndShortcuts;
        public RpgWorld500xLivingWorldService LivingWorld => _livingWorld;
        public RpgWorld500xWeatherAndTimeOfDayService Atmosphere => _atmosphere;

        /// <summary>
        /// Priority-dispatched single-button world interaction:
        /// 1. Unlock nearby shortcut -> 2. Loot nearby cache/relic ->
        /// 3. Talk to nearby field NPC / rumor broker -> 4. Inspect nearby story spot.
        /// </summary>
        public bool TryInteractPriority(
            Vector2 playerTile,
            float interactRadius,
            out RpgWorld500xInteractionResult result)
        {
            if (_cachesAndShortcuts.TryUnlockNearbyShortcut(playerTile, interactRadius, out RpgWorldShortcutDef shortcut))
            {
                InteractionCount++;
                result = new RpgWorld500xInteractionResult(
                    RpgWorld500xInteractionKind.ShortcutUnlocked,
                    shortcut.Id,
                    shortcut.TitleZh,
                    shortcut.UnlockedToastZh,
                    firstTimeDiscovery: true);
                _onToast?.Invoke(result.TitleZh, result.BodyZh);
                return true;
            }

            if (_cachesAndShortcuts.TryLootNearbyCache(playerTile, interactRadius, out RpgSupplyCacheDef cache))
            {
                InteractionCount++;
                string body = string.IsNullOrEmpty(cache.LoreNoteZh)
                    ? cache.RewardSummaryZh
                    : cache.RewardSummaryZh + "｜" + cache.LoreNoteZh;
                result = new RpgWorld500xInteractionResult(
                    RpgWorld500xInteractionKind.CacheLooted,
                    cache.Id,
                    cache.TitleZh,
                    body,
                    saltSilverGranted: cache.SaltSilverReward,
                    firstTimeDiscovery: true);
                _onToast?.Invoke(result.TitleZh, result.BodyZh);
                return true;
            }

            if (_livingWorld.TryTalkToNearbyNpc(playerTile, interactRadius,
                out RpgFieldNpcDef npc, out string dialogueLineZh, out string revealedMarkerId))
            {
                InteractionCount++;
                result = new RpgWorld500xInteractionResult(
                    RpgWorld500xInteractionKind.NpcDialogue,
                    npc.Id,
                    npc.DisplayNameZh,
                    dialogueLineZh,
                    revealedMarkerId: revealedMarkerId,
                    firstTimeDiscovery: !string.IsNullOrEmpty(revealedMarkerId));
                _onToast?.Invoke(result.TitleZh, result.BodyZh);
                return true;
            }

            if (_livingWorld.TryInspectNearbyStorySpot(playerTile, interactRadius,
                out RpgStorySpotDef spot, out bool firstInspection))
            {
                InteractionCount++;
                result = new RpgWorld500xInteractionResult(
                    RpgWorld500xInteractionKind.StorySpotInspected,
                    spot.Id,
                    spot.TitleZh,
                    spot.DescriptionZh,
                    firstTimeDiscovery: firstInspection);
                _onToast?.Invoke(result.TitleZh, result.BodyZh);
                return true;
            }

            result = RpgWorld500xInteractionResult.None;
            return false;
        }

        public RpgWorld500xEffectiveExplorationModifiers StepEnvironment(
            Vector2 playerTile,
            float deltaSeconds,
            float totalElapsedSeconds)
        {
            RpgWorld500xAtmosphereSnapshot atmo = _atmosphere.AdvanceRealSeconds(deltaSeconds, _mapId);
            RpgActiveAmbientState ambient = _livingWorld.EvaluateAmbientEvent(playerTile, totalElapsedSeconds);

            float moveSpeed = Mathf.Clamp(atmo.MovementSpeedMultiplier * ambient.MoveSpeedMultiplier, 0.72f, 1.15f);
            float patrolSight = Mathf.Clamp(atmo.PatrolDetectionMultiplier * ambient.PatrolSightMultiplier, 0.45f, 1.35f);
            float patrolNoise = Mathf.Clamp(ambient.PatrolNoiseMultiplier, 0.60f, 1.50f);
            float tension = Mathf.Clamp(atmo.EncounterTensionMultiplier, 0.85f, 1.50f);

            string caption = atmo.FormattedClock + " " + atmo.PhaseDisplayName + " · " + atmo.WeatherDisplayName;
            if (ambient.IsActive && !string.IsNullOrEmpty(ambient.CaptionZh))
            {
                caption += "｜" + ambient.CaptionZh;
            }

            return new RpgWorld500xEffectiveExplorationModifiers(
                atmo,
                ambient,
                moveSpeed,
                patrolSight,
                patrolNoise,
                tension,
                caption);
        }

        public RpgWorld500xAtmosphereSnapshot RestAtCampfire()
        {
            RpgWorld500xAtmosphereSnapshot dawn = _atmosphere.RestAtCampfire(_mapId);
            _onToast?.Invoke("营火休整完成", "第 " + dawn.DayCount + " 日 " + dawn.FormattedClock + " " + dawn.WeatherDisplayName);
            return dawn;
        }
    }
}
