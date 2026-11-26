using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum RpgWorld500xReadinessGrade : byte
    {
        Critical = 0,
        Underprepared = 1,
        Ready = 2,
        WellPrepared = 3
    }

    public readonly struct RpgWorld500xExpeditionReadinessReport
    {
        public RpgWorld500xExpeditionReadinessReport(
            RpgWorldMapId mapId,
            string mapDisplayNameZh,
            int score,
            RpgWorld500xReadinessGrade grade,
            string gradeLabelZh,
            RpgWorld500xTravelRiskLevel travelRiskLevel,
            bool hasHealingStock,
            bool hasVisibilityPrep,
            bool hasStealthOrFlarePrep,
            bool hasShortcutTools,
            int lockedShortcutCount,
            IReadOnlyList<string> recommendedSupplyIds,
            string checklistSummaryZh)
        {
            MapId = mapId;
            MapDisplayNameZh = mapDisplayNameZh ?? string.Empty;
            Score = score;
            Grade = grade;
            GradeLabelZh = gradeLabelZh ?? string.Empty;
            TravelRiskLevel = travelRiskLevel;
            HasHealingStock = hasHealingStock;
            HasVisibilityPrep = hasVisibilityPrep;
            HasStealthOrFlarePrep = hasStealthOrFlarePrep;
            HasShortcutTools = hasShortcutTools;
            LockedShortcutCount = lockedShortcutCount;
            RecommendedSupplyIds = recommendedSupplyIds ?? Array.Empty<string>();
            ChecklistSummaryZh = checklistSummaryZh ?? string.Empty;
        }

        public RpgWorldMapId MapId { get; }
        public string MapDisplayNameZh { get; }
        public int Score { get; }
        public RpgWorld500xReadinessGrade Grade { get; }
        public string GradeLabelZh { get; }
        public RpgWorld500xTravelRiskLevel TravelRiskLevel { get; }
        public bool HasHealingStock { get; }
        public bool HasVisibilityPrep { get; }
        public bool HasStealthOrFlarePrep { get; }
        public bool HasShortcutTools { get; }
        public int LockedShortcutCount { get; }
        public IReadOnlyList<string> RecommendedSupplyIds { get; }
        public string ChecklistSummaryZh { get; }
    }

    /// <summary>
    /// Evaluates pre-departure expedition readiness across live 500x weather risk,
    /// owned camp supplies, active timed buffs, remaining locked shortcuts, and
    /// regional faction standing for the current or any target destination region,
    /// emitting a 0-100 readiness score and prioritized camp shopping recommendations.
    /// </summary>
    public static class RpgWorld500xExpeditionReadinessService
    {
        public static RpgWorld500xExpeditionReadinessReport EvaluateReadiness(
            RpgWorld500xSceneBootstrapper bootstrapper,
            float elapsedSeconds = 0f,
            RpgWorld500xFieldSupplyUseService supplyUseService = null)
        {
            if (bootstrapper == null || bootstrapper.IsDisposed)
            {
                throw new ArgumentNullException(nameof(bootstrapper));
            }

            return EvaluateReadinessForMap(
                bootstrapper,
                bootstrapper.Driver.Facade.CurrentMapId,
                elapsedSeconds,
                supplyUseService);
        }

        public static RpgWorld500xExpeditionReadinessReport EvaluateReadinessForMap(
            RpgWorld500xSceneBootstrapper bootstrapper,
            RpgWorldMapId targetMapId,
            float elapsedSeconds = 0f,
            RpgWorld500xFieldSupplyUseService supplyUseService = null)
        {
            if (bootstrapper == null || bootstrapper.IsDisposed)
            {
                throw new ArgumentNullException(nameof(bootstrapper));
            }

            RpgWorld500xGrandRuntimeFacade facade = bootstrapper.Driver.Facade;
            RpgWorldMapId mapId = targetMapId;
            string mapNameZh = ResolveMapDisplayNameZh(mapId, facade);
            RpgWorld500xAtmosphereSnapshot atmo = facade.Atmosphere.GetSnapshot(mapId);
            RpgWorld500xTravelAdvisoryReport advisory =
                RpgWorld500xWeatherTravelAdvisoryService.BuildAdvisory(atmo, elapsedSeconds);

            IReadOnlyDictionary<string, int> store = bootstrapper.Store;
            Func<string, int> read = k => (store != null && store.TryGetValue(k, out int v)) ? v : 0;

            RpgWorld500xFieldBountyAndSupplyService shop = facade.BountyAndSupply;
            int gauze = shop.GetOwnedSupplyCount("gauze");
            int rations = shop.GetOwnedSupplyCount("rations");
            int lampOil = shop.GetOwnedSupplyCount("lamp_oil");
            int dampCloth = shop.GetOwnedSupplyCount("damp_cloth");
            int flare = shop.GetOwnedSupplyCount("flare");
            int ironClamps = shop.GetOwnedSupplyCount("iron_clamps");

            bool lampOilActive = supplyUseService != null && supplyUseService.GetRemainingBuffSeconds("lamp_oil", elapsedSeconds) > 0f;
            bool dampClothActive = supplyUseService != null && supplyUseService.GetRemainingBuffSeconds("damp_cloth", elapsedSeconds) > 0f;
            bool flareActive = supplyUseService != null && supplyUseService.GetRemainingBuffSeconds("flare", elapsedSeconds) > 0f;
            bool clampsActive = supplyUseService != null && supplyUseService.GetRemainingBuffSeconds("iron_clamps", elapsedSeconds) > 0f;

            IReadOnlyList<RpgWorldShortcutDef> shortcuts = RpgWorld500xContentCatalog.GetShortcuts(mapId);
            int lockedShortcuts = 0;
            for (int i = 0; i < shortcuts.Count; i++)
            {
                if (read(shortcuts[i].MakinomKey) != 1)
                {
                    lockedShortcuts++;
                }
            }

            var repService = new RpgWorld500xRegionalReputationService(read);
            RpgRegionalReputationSnapshot rep = repService.GetSnapshot(mapId);

            bool hasHealing = gauze > 0 || rations > 0;
            bool hasVisibility = lampOil > 0 || lampOilActive;
            bool hasStealthOrFlare = dampCloth > 0 || flare > 0 || dampClothActive || flareActive;
            bool hasShortcutTools = lockedShortcuts == 0 || ironClamps > 0 || clampsActive;

            int score = ResolveBaseScore(advisory.RiskLevel);
            if (hasHealing)
            {
                score += 20;
                if (gauze > 0 && rations > 0)
                {
                    score += 5;
                }
            }

            if (hasVisibility)
            {
                score += 15;
            }

            if (hasStealthOrFlare)
            {
                score += 15;
            }

            if (hasShortcutTools)
            {
                score += 10;
            }

            if (rep.CampSupplyDiscountPercent > 0)
            {
                score += 5;
            }

            score = Mathf.Clamp(score, 0, 100);
            RpgWorld500xReadinessGrade grade = ClassifyGrade(score);
            string gradeLabel = GetGradeLabelZh(grade);

            var recommended = new List<string>(5);
            if (gauze == 0)
            {
                recommended.Add("gauze");
            }

            if (rations == 0)
            {
                recommended.Add("rations");
            }

            bool lowVisibilityCondition =
                atmo.Phase == RpgWorld500xTimePhase.Night ||
                atmo.Weather == RpgWorld500xWeatherKind.MistFog ||
                atmo.Weather == RpgWorld500xWeatherKind.AshHaze ||
                atmo.Weather == RpgWorld500xWeatherKind.GaleStorm;
            if (lowVisibilityCondition && !hasVisibility)
            {
                recommended.Add("lamp_oil");
            }

            if ((advisory.RiskLevel == RpgWorld500xTravelRiskLevel.Hazardous ||
                 advisory.RiskLevel == RpgWorld500xTravelRiskLevel.Caution) &&
                flare == 0 && !flareActive)
            {
                recommended.Add("flare");
            }

            if (lockedShortcuts > 0 && ironClamps == 0 && !clampsActive)
            {
                recommended.Add("iron_clamps");
            }

            var sb = new StringBuilder(192);
            sb.Append("【整备评估】").Append(mapNameZh)
              .Append("｜评分 ").Append(score).Append("/100（").Append(gradeLabel).Append("）")
              .Append("｜").Append(advisory.RiskLabelZh);

            if (recommended.Count > 0)
            {
                sb.Append("｜建议补购：");
                for (int i = 0; i < recommended.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.Append("、");
                    }

                    sb.Append(ResolveSupplyNameZh(recommended[i]));
                }
            }
            else
            {
                sb.Append(advisory.RecommendCampfireRest
                    ? "｜随身整备齐全"
                    : "｜随身整备齐全，可直接启程");
            }

            // Supply readiness does not override hazardous weather guidance.
            // Even a fully stocked player should see the existing rest advisory.
            if (advisory.RecommendCampfireRest)
            {
                sb.Append("｜天时高危，建议营火休整后启程");
            }

            return new RpgWorld500xExpeditionReadinessReport(
                mapId,
                mapNameZh,
                score,
                grade,
                gradeLabel,
                advisory.RiskLevel,
                hasHealing,
                hasVisibility,
                hasStealthOrFlare,
                hasShortcutTools,
                lockedShortcuts,
                recommended,
                sb.ToString());
        }

        public static RpgWorld500xReadinessGrade ClassifyGrade(int score)
        {
            if (score >= 80) return RpgWorld500xReadinessGrade.WellPrepared;
            if (score >= 55) return RpgWorld500xReadinessGrade.Ready;
            if (score >= 30) return RpgWorld500xReadinessGrade.Underprepared;
            return RpgWorld500xReadinessGrade.Critical;
        }

        public static string GetGradeLabelZh(RpgWorld500xReadinessGrade grade)
        {
            switch (grade)
            {
                case RpgWorld500xReadinessGrade.WellPrepared: return "整备周全";
                case RpgWorld500xReadinessGrade.Ready: return "可启程";
                case RpgWorld500xReadinessGrade.Underprepared: return "补给不足";
                default: return "高危缺备";
            }
        }

        private static int ResolveBaseScore(RpgWorld500xTravelRiskLevel risk)
        {
            switch (risk)
            {
                case RpgWorld500xTravelRiskLevel.Hazardous: return 5;
                case RpgWorld500xTravelRiskLevel.Caution: return 20;
                default: return 35;
            }
        }

        private static string ResolveMapDisplayNameZh(
            RpgWorldMapId mapId,
            RpgWorld500xGrandRuntimeFacade facade)
        {
            if (facade != null && facade.CurrentMapId == mapId)
            {
                return facade.Session.Host.Layout.Spec.DisplayNameZh;
            }

            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            for (int i = 0; i < specs.Count; i++)
            {
                if (specs[i].MapId == mapId)
                {
                    return specs[i].DisplayNameZh;
                }
            }

            return mapId.ToString();
        }

        private static string ResolveSupplyNameZh(string supplyId)
        {
            switch (supplyId)
            {
                case "gauze": return "止血纱布";
                case "rations": return "行军干粮";
                case "lamp_oil": return "矿灯灯油";
                case "damp_cloth": return "防潮湿布";
                case "flare": return "应急信号弹";
                case "iron_clamps": return "便携修桥铁件";
                default: return supplyId;
            }
        }
    }
}
