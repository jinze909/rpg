using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum RpgWorld500xTravelRiskLevel : byte
    {
        Safe = 0,
        StealthAdvantage = 1,
        Caution = 2,
        Hazardous = 3
    }

    public readonly struct RpgWorld500xAmbientWindowForecast
    {
        public RpgWorld500xAmbientWindowForecast(
            string zoneId,
            RpgAmbientMicroEventKind kind,
            string captionZh,
            bool isCurrentlyActive,
            float secondsUntilNextChange)
        {
            ZoneId = zoneId ?? string.Empty;
            Kind = kind;
            CaptionZh = captionZh ?? string.Empty;
            IsCurrentlyActive = isCurrentlyActive;
            SecondsUntilNextChange = secondsUntilNextChange;
        }

        public string ZoneId { get; }
        public RpgAmbientMicroEventKind Kind { get; }
        public string CaptionZh { get; }
        public bool IsCurrentlyActive { get; }
        public float SecondsUntilNextChange { get; }
    }

    public readonly struct RpgWorld500xTravelAdvisoryReport
    {
        public RpgWorld500xTravelAdvisoryReport(
            RpgWorldMapId mapId,
            RpgWorld500xTravelRiskLevel riskLevel,
            string riskLabelZh,
            string tacticalAdvisoryZh,
            bool recommendCampfireRest,
            RpgWorld500xTimePhase nextPhase,
            string nextPhaseNameZh,
            int minutesUntilNextPhase,
            IReadOnlyList<RpgWorld500xAmbientWindowForecast> ambientWindows)
        {
            MapId = mapId;
            RiskLevel = riskLevel;
            RiskLabelZh = riskLabelZh ?? string.Empty;
            TacticalAdvisoryZh = tacticalAdvisoryZh ?? string.Empty;
            RecommendCampfireRest = recommendCampfireRest;
            NextPhase = nextPhase;
            NextPhaseNameZh = nextPhaseNameZh ?? string.Empty;
            MinutesUntilNextPhase = minutesUntilNextPhase;
            AmbientWindows = ambientWindows ?? Array.Empty<RpgWorld500xAmbientWindowForecast>();
        }

        public RpgWorldMapId MapId { get; }
        public RpgWorld500xTravelRiskLevel RiskLevel { get; }
        public string RiskLabelZh { get; }
        public string TacticalAdvisoryZh { get; }
        public bool RecommendCampfireRest { get; }
        public RpgWorld500xTimePhase NextPhase { get; }
        public string NextPhaseNameZh { get; }
        public int MinutesUntilNextPhase { get; }
        public IReadOnlyList<RpgWorld500xAmbientWindowForecast> AmbientWindows { get; }
    }

    public readonly struct RpgWorld500xWorldWeatherDigest
    {
        public RpgWorld500xWorldWeatherDigest(
            IReadOnlyList<RpgWorld500xTravelAdvisoryReport> regionReports,
            int hazardousRegionCount,
            int stealthWindowRegionCount,
            RpgWorldMapId safestMapId,
            string worldBulletinZh)
        {
            RegionReports = regionReports ?? Array.Empty<RpgWorld500xTravelAdvisoryReport>();
            HazardousRegionCount = hazardousRegionCount;
            StealthWindowRegionCount = stealthWindowRegionCount;
            SafestMapId = safestMapId;
            WorldBulletinZh = worldBulletinZh ?? string.Empty;
        }

        public IReadOnlyList<RpgWorld500xTravelAdvisoryReport> RegionReports { get; }
        public int HazardousRegionCount { get; }
        public int StealthWindowRegionCount { get; }
        public RpgWorldMapId SafestMapId { get; }
        public string WorldBulletinZh { get; }
    }

    /// <summary>
    /// Computes deterministic travel risk ratings, next phase countdowns, campfire-rest
    /// recommendations, cross-region weather digests, and per-zone ambient micro-event
    /// active/cooldown window forecasts for all four 500x-area RPG maps.
    /// </summary>
    public static class RpgWorld500xWeatherTravelAdvisoryService
    {
        public static RpgWorld500xWorldWeatherDigest BuildWorldWeatherDigest(
            RpgWorld500xWeatherAndTimeOfDayService atmosphere,
            float elapsedSeconds = 0f)
        {
            if (atmosphere == null)
            {
                throw new ArgumentNullException(nameof(atmosphere));
            }

            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            var reports = new List<RpgWorld500xTravelAdvisoryReport>(specs.Count);
            int hazardousCount = 0;
            int stealthCount = 0;
            RpgWorldMapId safestMapId = specs.Count > 0 ? specs[0].MapId : RpgWorldMapId.Soundwell;
            int lowestRiskRank = int.MaxValue;
            var sb = new StringBuilder(192);
            sb.Append("【四境气象速报】");

            for (int i = 0; i < specs.Count; i++)
            {
                RpgExpandedMapSpec spec = specs[i];
                RpgWorld500xTravelAdvisoryReport report = BuildAdvisory(
                    atmosphere.GetSnapshot(spec.MapId), elapsedSeconds);
                reports.Add(report);

                if (report.RiskLevel == RpgWorld500xTravelRiskLevel.Hazardous)
                {
                    hazardousCount++;
                }
                else if (report.RiskLevel == RpgWorld500xTravelRiskLevel.StealthAdvantage)
                {
                    stealthCount++;
                }

                int rank = (int)report.RiskLevel;
                if (rank < lowestRiskRank)
                {
                    lowestRiskRank = rank;
                    safestMapId = spec.MapId;
                }

                if (i > 0)
                {
                    sb.Append("｜");
                }

                sb.Append(spec.DisplayNameZh).Append('(').Append(report.RiskLabelZh).Append(')');
            }

            return new RpgWorld500xWorldWeatherDigest(
                reports,
                hazardousCount,
                stealthCount,
                safestMapId,
                sb.ToString());
        }

        public static RpgWorld500xTravelAdvisoryReport BuildAdvisory(
            RpgWorld500xAtmosphereSnapshot snapshot,
            float elapsedSeconds = 0f)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            RpgWorld500xTravelRiskLevel risk = ClassifyRisk(snapshot);
            bool recommendRest = risk == RpgWorld500xTravelRiskLevel.Hazardous ||
                (snapshot.Phase == RpgWorld500xTimePhase.Night &&
                 (snapshot.Weather == RpgWorld500xWeatherKind.GaleStorm ||
                  snapshot.Weather == RpgWorld500xWeatherKind.AshHaze));

            ResolveNextPhase(snapshot.TimeOfDayMinutes, out RpgWorld500xTimePhase nextPhase, out int minutesToNext);
            string nextPhaseName = RpgWorld500xWeatherAndTimeOfDayService.GetPhaseDisplayName(nextPhase);
            string riskLabel = GetRiskLabelZh(risk);
            string advisory = BuildAdvisoryTextZh(snapshot, risk, recommendRest, nextPhaseName, minutesToNext);
            List<RpgWorld500xAmbientWindowForecast> windows = ForecastAmbientWindows(snapshot.MapId, elapsedSeconds);

            return new RpgWorld500xTravelAdvisoryReport(
                snapshot.MapId,
                risk,
                riskLabel,
                advisory,
                recommendRest,
                nextPhase,
                nextPhaseName,
                minutesToNext,
                windows);
        }

        public static RpgWorld500xTravelRiskLevel ClassifyRisk(RpgWorld500xAtmosphereSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return RpgWorld500xTravelRiskLevel.Safe;
            }

            if (snapshot.Weather == RpgWorld500xWeatherKind.GaleStorm ||
                snapshot.EncounterTensionMultiplier >= 1.25f)
            {
                return RpgWorld500xTravelRiskLevel.Hazardous;
            }

            if (snapshot.Weather == RpgWorld500xWeatherKind.AshHaze ||
                snapshot.Phase == RpgWorld500xTimePhase.Night)
            {
                return RpgWorld500xTravelRiskLevel.Caution;
            }

            if (snapshot.Weather == RpgWorld500xWeatherKind.MistFog ||
                snapshot.PatrolDetectionMultiplier <= 0.85f)
            {
                return RpgWorld500xTravelRiskLevel.StealthAdvantage;
            }

            return RpgWorld500xTravelRiskLevel.Safe;
        }

        public static void ResolveNextPhase(
            float timeOfDayMinutes,
            out RpgWorld500xTimePhase nextPhase,
            out int minutesUntilNext)
        {
            // Match the atmosphere clock's normalization. Adding a whole day
            // to an already positive float can round it across a phase boundary.
            float normalized = (!float.IsNaN(timeOfDayMinutes) && !float.IsInfinity(timeOfDayMinutes))
                ? timeOfDayMinutes % 1440f
                : 0f;
            if (normalized < 0f)
            {
                normalized += 1440f;
            }

            if (normalized < 300f)
            {
                nextPhase = RpgWorld500xTimePhase.Dawn;
                minutesUntilNext = Mathf.CeilToInt(300f - normalized);
            }
            else if (normalized < 480f)
            {
                nextPhase = RpgWorld500xTimePhase.Day;
                minutesUntilNext = Mathf.CeilToInt(480f - normalized);
            }
            else if (normalized < 1020f)
            {
                nextPhase = RpgWorld500xTimePhase.Dusk;
                minutesUntilNext = Mathf.CeilToInt(1020f - normalized);
            }
            else if (normalized < 1200f)
            {
                nextPhase = RpgWorld500xTimePhase.Night;
                minutesUntilNext = Mathf.CeilToInt(1200f - normalized);
            }
            else
            {
                nextPhase = RpgWorld500xTimePhase.Dawn;
                minutesUntilNext = Mathf.CeilToInt((1440f - normalized) + 300f);
            }
        }

        public static List<RpgWorld500xAmbientWindowForecast> ForecastAmbientWindows(
            RpgWorldMapId mapId,
            float elapsedSeconds)
        {
            float safeElapsed = (!float.IsNaN(elapsedSeconds) && !float.IsInfinity(elapsedSeconds) && elapsedSeconds >= 0f)
                ? elapsedSeconds
                : 0f;

            IReadOnlyList<RpgAmbientMicroEventZoneDef> zones = RpgWorld500xLivingWorldCatalog.GetAmbientZones(mapId);
            var list = new List<RpgWorld500xAmbientWindowForecast>(zones.Count);

            for (int i = 0; i < zones.Count; i++)
            {
                RpgAmbientMicroEventZoneDef z = zones[i];
                float phase = z.CycleSeconds > 0f ? (safeElapsed % z.CycleSeconds) : 0f;
                bool active = phase < z.ActiveSeconds;
                float secondsUntilChange = active
                    ? Mathf.Max(0f, z.ActiveSeconds - phase)
                    : Mathf.Max(0f, z.CycleSeconds - phase);

                list.Add(new RpgWorld500xAmbientWindowForecast(
                    z.Id,
                    z.Kind,
                    z.CaptionZh,
                    active,
                    secondsUntilChange));
            }

            return list;
        }

        public static string GetRiskLabelZh(RpgWorld500xTravelRiskLevel level)
        {
            switch (level)
            {
                case RpgWorld500xTravelRiskLevel.Hazardous: return "高危预警";
                case RpgWorld500xTravelRiskLevel.Caution: return "谨慎行进";
                case RpgWorld500xTravelRiskLevel.StealthAdvantage: return "潜行良机";
                case RpgWorld500xTravelRiskLevel.Safe:
                default: return "行路平稳";
            }
        }

        private static string BuildAdvisoryTextZh(
            RpgWorld500xAtmosphereSnapshot snapshot,
            RpgWorld500xTravelRiskLevel risk,
            bool recommendRest,
            string nextPhaseName,
            int minutesToNext)
        {
            string head = "[" + GetRiskLabelZh(risk) + "] " + snapshot.FormattedClock + " " +
                          snapshot.PhaseDisplayName + " · " + snapshot.WeatherDisplayName;
            string tail = "｜距" + nextPhaseName + "约 " + minutesToNext + " 分钟";

            if (recommendRest)
            {
                return head + "：行进受阻且遭遇张力升高，建议先在营火休整至次日拂晓" + tail;
            }

            if (risk == RpgWorld500xTravelRiskLevel.StealthAdvantage)
            {
                return head + "：巡逻视野受限，适合潜行穿越危险区外围" + tail;
            }

            if (risk == RpgWorld500xTravelRiskLevel.Caution)
            {
                return head + "：夜色或烟霾偏重，建议备足干粮与风灯再深入" + tail;
            }

            return head + "：视野明朗、步速平稳，适合长途勘测与清扫支线" + tail;
        }
    }
}
