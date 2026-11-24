using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum RpgWorld500xTimePhase
    {
        Dawn = 0,
        Day = 1,
        Dusk = 2,
        Night = 3
    }

    public enum RpgWorld500xWeatherKind
    {
        Clear = 0,
        MistFog = 1,
        RainDrizzle = 2,
        GaleStorm = 3,
        AshHaze = 4
    }

    public sealed class RpgWorld500xAtmosphereSnapshot
    {
        public RpgWorldMapId MapId { get; }
        public int DayCount { get; }
        public float TimeOfDayMinutes { get; }
        public RpgWorld500xTimePhase Phase { get; }
        public RpgWorld500xWeatherKind Weather { get; }
        public string PhaseDisplayName { get; }
        public string WeatherDisplayName { get; }
        public string FormattedClock { get; }
        public Color32 AmbientTint { get; }
        public float PatrolDetectionMultiplier { get; }
        public float MovementSpeedMultiplier { get; }
        public float EncounterTensionMultiplier { get; }
        public float LanternHaloScale { get; }

        public RpgWorld500xAtmosphereSnapshot(
            RpgWorldMapId mapId,
            int dayCount,
            float timeOfDayMinutes,
            RpgWorld500xTimePhase phase,
            RpgWorld500xWeatherKind weather,
            string phaseDisplayName,
            string weatherDisplayName,
            string formattedClock,
            Color32 ambientTint,
            float patrolDetectionMultiplier,
            float movementSpeedMultiplier,
            float encounterTensionMultiplier,
            float lanternHaloScale)
        {
            MapId = mapId;
            DayCount = dayCount;
            TimeOfDayMinutes = timeOfDayMinutes;
            Phase = phase;
            Weather = weather;
            PhaseDisplayName = phaseDisplayName;
            WeatherDisplayName = weatherDisplayName;
            FormattedClock = formattedClock;
            AmbientTint = ambientTint;
            PatrolDetectionMultiplier = patrolDetectionMultiplier;
            MovementSpeedMultiplier = movementSpeedMultiplier;
            EncounterTensionMultiplier = encounterTensionMultiplier;
            LanternHaloScale = lanternHaloScale;
        }
    }

    /// <summary>
    /// Deterministic 24-hour time-of-day clock, regional weather cycle, campfire rest
    /// transition, and gameplay modifier service for the 500x-area RPG world.
    /// </summary>
    public sealed class RpgWorld500xWeatherAndTimeOfDayService
    {
        public const float MinutesPerDay = 1440f;
        public const float DefaultDawnMinutes = 360f;
        public const float DefaultGameMinutesPerRealSecond = 2f;
        public const float WeatherSlotMinutes = 180f;

        public const string TimeMinutesKey = "rpg.return.world.atmosphere.time_minutes";
        public const string DayCountKey = "rpg.return.world.atmosphere.day_count";

        private readonly Dictionary<RpgWorldMapId, RpgWorld500xWeatherKind> _mapWeathers =
            new Dictionary<RpgWorldMapId, RpgWorld500xWeatherKind>();
        private readonly Dictionary<RpgWorldMapId, int> _lastAutoWeatherSlot =
            new Dictionary<RpgWorldMapId, int>();

        private float _timeOfDayMinutes;
        private int _dayCount;
        private readonly float _gameMinutesPerRealSecond;

        public RpgWorld500xWeatherAndTimeOfDayService(
            float initialMinutes = 480f,
            int initialDay = 1,
            float gameMinutesPerRealSecond = DefaultGameMinutesPerRealSecond)
        {
            _dayCount = Math.Max(1, initialDay);
            _timeOfDayMinutes = NormalizeMinutes(initialMinutes);
            _gameMinutesPerRealSecond = gameMinutesPerRealSecond > 0f &&
                !float.IsInfinity(gameMinutesPerRealSecond)
                ? gameMinutesPerRealSecond
                : DefaultGameMinutesPerRealSecond;

            var maps = (RpgWorldMapId[])Enum.GetValues(typeof(RpgWorldMapId));
            int slot = ComputeWeatherSlot(_dayCount, _timeOfDayMinutes);
            for (int i = 0; i < maps.Length; i++)
            {
                RpgWorldMapId map = maps[i];
                _mapWeathers[map] = PickDeterministicWeather(map, _dayCount, slot);
                _lastAutoWeatherSlot[map] = slot;
            }
        }

        public int DayCount => _dayCount;

        public float TimeOfDayMinutes => _timeOfDayMinutes;

        public RpgWorld500xTimePhase CurrentPhase => ResolveTimePhase(_timeOfDayMinutes);

        public static string GetWeatherKey(RpgWorldMapId mapId)
        {
            return "rpg.return.world.atmosphere.weather." + mapId.ToString().ToLowerInvariant();
        }

        public static RpgWorld500xTimePhase ResolveTimePhase(float minutes)
        {
            float normalized = NormalizeMinutes(minutes);
            if (normalized >= 300f && normalized < 480f)
            {
                return RpgWorld500xTimePhase.Dawn;
            }

            if (normalized >= 480f && normalized < 1020f)
            {
                return RpgWorld500xTimePhase.Day;
            }

            if (normalized >= 1020f && normalized < 1200f)
            {
                return RpgWorld500xTimePhase.Dusk;
            }

            return RpgWorld500xTimePhase.Night;
        }

        public static RpgWorld500xWeatherKind[] GetAllowedWeathers(RpgWorldMapId mapId)
        {
            switch (mapId)
            {
                case RpgWorldMapId.RedSaltHarbor:
                    return new[]
                    {
                        RpgWorld500xWeatherKind.Clear,
                        RpgWorld500xWeatherKind.MistFog,
                        RpgWorld500xWeatherKind.RainDrizzle,
                        RpgWorld500xWeatherKind.GaleStorm
                    };
                case RpgWorldMapId.WhitewaterCrossing:
                    return new[]
                    {
                        RpgWorld500xWeatherKind.Clear,
                        RpgWorld500xWeatherKind.MistFog,
                        RpgWorld500xWeatherKind.RainDrizzle,
                        RpgWorld500xWeatherKind.GaleStorm
                    };
                case RpgWorldMapId.MineCamp:
                    return new[]
                    {
                        RpgWorld500xWeatherKind.Clear,
                        RpgWorld500xWeatherKind.AshHaze,
                        RpgWorld500xWeatherKind.GaleStorm,
                        RpgWorld500xWeatherKind.MistFog
                    };
                case RpgWorldMapId.Soundwell:
                default:
                    return new[]
                    {
                        RpgWorld500xWeatherKind.Clear,
                        RpgWorld500xWeatherKind.MistFog,
                        RpgWorld500xWeatherKind.RainDrizzle,
                        RpgWorld500xWeatherKind.MistFog
                    };
            }
        }

        public RpgWorld500xAtmosphereSnapshot AdvanceRealSeconds(float deltaSeconds, RpgWorldMapId activeMap)
        {
            if (deltaSeconds > 0f && !float.IsNaN(deltaSeconds) && !float.IsInfinity(deltaSeconds))
            {
                // Calculate elapsed days directly: large finite time steps must
                // not hang in a per-day loop or overflow the persisted day count.
                double totalMinutes = _timeOfDayMinutes + (double)deltaSeconds * _gameMinutesPerRealSecond;
                double elapsedDays = Math.Floor(totalMinutes / MinutesPerDay);
                _dayCount += (int)Math.Min(elapsedDays, int.MaxValue - _dayCount);
                _timeOfDayMinutes = (float)(totalMinutes % MinutesPerDay);
                // Rounding the remainder back to float may land exactly at midnight.
                if (_timeOfDayMinutes >= MinutesPerDay)
                {
                    _timeOfDayMinutes = 0f;
                    if (_dayCount < int.MaxValue)
                        _dayCount++;
                }
            }

            int slot = ComputeWeatherSlot(_dayCount, _timeOfDayMinutes);
            if (!_lastAutoWeatherSlot.TryGetValue(activeMap, out int prevSlot) || prevSlot != slot)
            {
                _mapWeathers[activeMap] = PickDeterministicWeather(activeMap, _dayCount, slot);
                _lastAutoWeatherSlot[activeMap] = slot;
            }

            return GetSnapshot(activeMap);
        }

        public RpgWorld500xAtmosphereSnapshot RestAtCampfire(RpgWorldMapId activeMap)
        {
            if (_timeOfDayMinutes >= DefaultDawnMinutes && _dayCount < int.MaxValue)
            {
                _dayCount++;
            }

            _timeOfDayMinutes = DefaultDawnMinutes;
            RpgWorld500xWeatherKind current = GetWeather(activeMap);
            if (current == RpgWorld500xWeatherKind.GaleStorm || current == RpgWorld500xWeatherKind.AshHaze)
            {
                _mapWeathers[activeMap] = activeMap == RpgWorldMapId.Soundwell
                    ? RpgWorld500xWeatherKind.MistFog
                    : RpgWorld500xWeatherKind.Clear;
            }

            _lastAutoWeatherSlot[activeMap] = ComputeWeatherSlot(_dayCount, _timeOfDayMinutes);
            return GetSnapshot(activeMap);
        }

        public void SetWeather(RpgWorldMapId mapId, RpgWorld500xWeatherKind weather)
        {
            _mapWeathers[mapId] = weather;
            _lastAutoWeatherSlot[mapId] = ComputeWeatherSlot(_dayCount, _timeOfDayMinutes);
        }

        public RpgWorld500xWeatherKind GetWeather(RpgWorldMapId mapId)
        {
            return _mapWeathers.TryGetValue(mapId, out RpgWorld500xWeatherKind weather)
                ? weather
                : RpgWorld500xWeatherKind.Clear;
        }

        public RpgWorld500xAtmosphereSnapshot GetSnapshot(RpgWorldMapId mapId)
        {
            RpgWorld500xTimePhase phase = CurrentPhase;
            RpgWorld500xWeatherKind weather = GetWeather(mapId);

            Color32 tint = ComputeAmbientTint(phase, weather);
            float patrolDetection = ComputePatrolDetectionMultiplier(phase, weather);
            float moveSpeed = ComputeMovementSpeedMultiplier(weather);
            float encounterTension = ComputeEncounterTensionMultiplier(phase, weather);
            float lanternScale = ComputeLanternHaloScale(phase, weather);

            return new RpgWorld500xAtmosphereSnapshot(
                mapId,
                _dayCount,
                _timeOfDayMinutes,
                phase,
                weather,
                GetPhaseDisplayName(phase),
                GetWeatherDisplayName(mapId, weather),
                FormatClock(_timeOfDayMinutes),
                tint,
                patrolDetection,
                moveSpeed,
                encounterTension,
                lanternScale);
        }

        public Dictionary<string, string> ExportPersistenceState()
        {
            var data = new Dictionary<string, string>
            {
                [TimeMinutesKey] = _timeOfDayMinutes.ToString("F2", CultureInfo.InvariantCulture),
                [DayCountKey] = _dayCount.ToString(CultureInfo.InvariantCulture)
            };

            var maps = (RpgWorldMapId[])Enum.GetValues(typeof(RpgWorldMapId));
            for (int i = 0; i < maps.Length; i++)
            {
                RpgWorldMapId map = maps[i];
                data[GetWeatherKey(map)] = GetWeather(map).ToString();
            }

            return data;
        }

        public void ImportPersistenceState(IReadOnlyDictionary<string, string> state)
        {
            if (state == null)
            {
                return;
            }

            if (state.TryGetValue(DayCountKey, out string dayStr) &&
                int.TryParse(dayStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedDay))
            {
                _dayCount = Math.Max(1, parsedDay);
            }

            if (state.TryGetValue(TimeMinutesKey, out string minStr) &&
                float.TryParse(minStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedMinutes) &&
                !float.IsNaN(parsedMinutes) && !float.IsInfinity(parsedMinutes))
            {
                _timeOfDayMinutes = NormalizeMinutes(parsedMinutes);
            }

            int slot = ComputeWeatherSlot(_dayCount, _timeOfDayMinutes);
            var maps = (RpgWorldMapId[])Enum.GetValues(typeof(RpgWorldMapId));
            for (int i = 0; i < maps.Length; i++)
            {
                RpgWorldMapId map = maps[i];
                if (state.TryGetValue(GetWeatherKey(map), out string weatherStr) &&
                    Enum.TryParse(weatherStr, true, out RpgWorld500xWeatherKind parsedWeather))
                {
                    _mapWeathers[map] = parsedWeather;
                    _lastAutoWeatherSlot[map] = slot;
                }
            }
        }

        public static string GetPhaseDisplayName(RpgWorld500xTimePhase phase)
        {
            switch (phase)
            {
                case RpgWorld500xTimePhase.Dawn:
                    return "拂晓";
                case RpgWorld500xTimePhase.Day:
                    return "白昼";
                case RpgWorld500xTimePhase.Dusk:
                    return "黄昏";
                case RpgWorld500xTimePhase.Night:
                default:
                    return "深夜";
            }
        }

        public static string GetWeatherDisplayName(RpgWorldMapId mapId, RpgWorld500xWeatherKind weather)
        {
            switch (mapId)
            {
                case RpgWorldMapId.RedSaltHarbor:
                    switch (weather)
                    {
                        case RpgWorld500xWeatherKind.MistFog: return "海港浓雾";
                        case RpgWorld500xWeatherKind.RainDrizzle: return "咸风骤雨";
                        case RpgWorld500xWeatherKind.GaleStorm: return "盐潮狂风";
                        case RpgWorld500xWeatherKind.AshHaze: return "码头烟霾";
                        default: return "海盐晴日";
                    }
                case RpgWorldMapId.WhitewaterCrossing:
                    switch (weather)
                    {
                        case RpgWorld500xWeatherKind.MistFog: return "急流江雾";
                        case RpgWorld500xWeatherKind.RainDrizzle: return "渡口细雨";
                        case RpgWorld500xWeatherKind.GaleStorm: return "江面疾风";
                        case RpgWorld500xWeatherKind.AshHaze: return "渡口薄烟";
                        default: return "白水晴空";
                    }
                case RpgWorldMapId.MineCamp:
                    switch (weather)
                    {
                        case RpgWorld500xWeatherKind.MistFog: return "矿谷寒雾";
                        case RpgWorld500xWeatherKind.RainDrizzle: return "矿坡冷雨";
                        case RpgWorld500xWeatherKind.GaleStorm: return "矿坑砂暴";
                        case RpgWorld500xWeatherKind.AshHaze: return "煤烟灰霾";
                        default: return "矿场干风";
                    }
                case RpgWorldMapId.Soundwell:
                default:
                    switch (weather)
                    {
                        case RpgWorld500xWeatherKind.MistFog: return "山间薄雾";
                        case RpgWorld500xWeatherKind.RainDrizzle: return "轻岚细雨";
                        case RpgWorld500xWeatherKind.GaleStorm: return "山谷疾风";
                        case RpgWorld500xWeatherKind.AshHaze: return "炊烟薄霭";
                        default: return "晴岚微风";
                    }
            }
        }

        public static string FormatClock(float minutes)
        {
            int total = Mathf.FloorToInt(NormalizeMinutes(minutes));
            int hours = (total / 60) % 24;
            int mins = total % 60;
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", hours, mins);
        }

        private static Color32 ComputeAmbientTint(RpgWorld500xTimePhase phase, RpgWorld500xWeatherKind weather)
        {
            Color baseColor;
            switch (phase)
            {
                case RpgWorld500xTimePhase.Dawn:
                    baseColor = new Color(0.95f, 0.86f, 0.78f, 1f);
                    break;
                case RpgWorld500xTimePhase.Dusk:
                    baseColor = new Color(0.92f, 0.72f, 0.58f, 1f);
                    break;
                case RpgWorld500xTimePhase.Night:
                    baseColor = new Color(0.46f, 0.52f, 0.70f, 1f);
                    break;
                case RpgWorld500xTimePhase.Day:
                default:
                    baseColor = Color.white;
                    break;
            }

            Color weatherFactor;
            switch (weather)
            {
                case RpgWorld500xWeatherKind.MistFog:
                    weatherFactor = new Color(0.88f, 0.92f, 0.96f, 1f);
                    break;
                case RpgWorld500xWeatherKind.RainDrizzle:
                    weatherFactor = new Color(0.78f, 0.84f, 0.92f, 1f);
                    break;
                case RpgWorld500xWeatherKind.GaleStorm:
                    weatherFactor = new Color(0.70f, 0.74f, 0.82f, 1f);
                    break;
                case RpgWorld500xWeatherKind.AshHaze:
                    weatherFactor = new Color(0.84f, 0.78f, 0.70f, 1f);
                    break;
                case RpgWorld500xWeatherKind.Clear:
                default:
                    weatherFactor = Color.white;
                    break;
            }

            Color blended = baseColor * weatherFactor;
            return new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(blended.r * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(blended.g * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(blended.b * 255f), 0, 255),
                255);
        }

        private static float ComputePatrolDetectionMultiplier(RpgWorld500xTimePhase phase, RpgWorld500xWeatherKind weather)
        {
            float value = phase == RpgWorld500xTimePhase.Night ? 0.82f
                : phase == RpgWorld500xTimePhase.Dusk ? 0.92f
                : 1.0f;

            switch (weather)
            {
                case RpgWorld500xWeatherKind.MistFog:
                case RpgWorld500xWeatherKind.AshHaze:
                    value *= 0.82f;
                    break;
                case RpgWorld500xWeatherKind.RainDrizzle:
                    value *= 0.90f;
                    break;
                case RpgWorld500xWeatherKind.GaleStorm:
                    value *= 0.85f;
                    break;
            }

            return Mathf.Clamp(value, 0.65f, 1.20f);
        }

        private static float ComputeMovementSpeedMultiplier(RpgWorld500xWeatherKind weather)
        {
            switch (weather)
            {
                case RpgWorld500xWeatherKind.GaleStorm:
                    return 0.90f;
                case RpgWorld500xWeatherKind.RainDrizzle:
                    return 0.96f;
                case RpgWorld500xWeatherKind.AshHaze:
                    return 0.95f;
                default:
                    return 1.0f;
            }
        }

        private static float ComputeEncounterTensionMultiplier(RpgWorld500xTimePhase phase, RpgWorld500xWeatherKind weather)
        {
            float value = phase == RpgWorld500xTimePhase.Night ? 1.20f
                : phase == RpgWorld500xTimePhase.Dusk ? 1.08f
                : 1.0f;

            if (weather == RpgWorld500xWeatherKind.GaleStorm)
            {
                value *= 1.12f;
            }
            else if (weather == RpgWorld500xWeatherKind.MistFog || weather == RpgWorld500xWeatherKind.AshHaze)
            {
                value *= 1.05f;
            }

            return Mathf.Clamp(value, 0.90f, 1.35f);
        }

        private static float ComputeLanternHaloScale(RpgWorld500xTimePhase phase, RpgWorld500xWeatherKind weather)
        {
            float value = phase == RpgWorld500xTimePhase.Night ? 1.40f
                : phase == RpgWorld500xTimePhase.Dusk ? 1.20f
                : phase == RpgWorld500xTimePhase.Dawn ? 1.05f
                : 0.80f;

            if (weather == RpgWorld500xWeatherKind.MistFog || weather == RpgWorld500xWeatherKind.AshHaze)
            {
                value += 0.15f;
            }

            return Mathf.Clamp(value, 0.60f, 1.60f);
        }

        private static int ComputeWeatherSlot(int dayCount, float minutes)
        {
            int slotInDay = Mathf.FloorToInt(NormalizeMinutes(minutes) / WeatherSlotMinutes);
            return unchecked((dayCount * 8) + slotInDay);
        }

        private static RpgWorld500xWeatherKind PickDeterministicWeather(RpgWorldMapId mapId, int dayCount, int slot)
        {
            RpgWorld500xWeatherKind[] allowed = GetAllowedWeathers(mapId);
            unchecked
            {
                int hash = ((int)mapId + 1) * 73856093 ^ (dayCount * 19349663) ^ (slot * 83492791);
                int index = (int)(Math.Abs((long)hash) % allowed.Length);
                return allowed[index];
            }
        }

        private static float NormalizeMinutes(float minutes)
        {
            if (float.IsNaN(minutes) || float.IsInfinity(minutes))
            {
                return 0f;
            }

            float mod = minutes % MinutesPerDay;
            return mod < 0f ? mod + MinutesPerDay : mod;
        }
    }
}
