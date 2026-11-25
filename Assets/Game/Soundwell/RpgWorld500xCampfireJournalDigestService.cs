using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Rpg.Soundwell
{
    public readonly struct RpgWorld500xExpeditionDigest
    {
        public RpgWorld500xExpeditionDigest(
            RpgWorldMapId mapId,
            string mapDisplayNameZh,
            string districtDisplayNameZh,
            int dayCount,
            string formattedClock,
            string weatherDisplayNameZh,
            RpgWorld500xTravelRiskLevel travelRiskLevel,
            string factionNameZh,
            string reputationRankZh,
            int campDiscountPercent,
            int saltSilverBalance,
            int totalOwnedSupplyItems,
            int activeMapCompletionPercent,
            int globalWorldCompletionPercent,
            int claimableMilestoneCount,
            string nextObjectiveHintZh,
            string formattedDigestZh)
        {
            MapId = mapId;
            MapDisplayNameZh = mapDisplayNameZh ?? string.Empty;
            DistrictDisplayNameZh = districtDisplayNameZh ?? string.Empty;
            DayCount = dayCount;
            FormattedClock = formattedClock ?? string.Empty;
            WeatherDisplayNameZh = weatherDisplayNameZh ?? string.Empty;
            TravelRiskLevel = travelRiskLevel;
            FactionNameZh = factionNameZh ?? string.Empty;
            ReputationRankZh = reputationRankZh ?? string.Empty;
            CampDiscountPercent = campDiscountPercent;
            SaltSilverBalance = saltSilverBalance;
            TotalOwnedSupplyItems = totalOwnedSupplyItems;
            ActiveMapCompletionPercent = activeMapCompletionPercent;
            GlobalWorldCompletionPercent = globalWorldCompletionPercent;
            ClaimableMilestoneCount = claimableMilestoneCount;
            NextObjectiveHintZh = nextObjectiveHintZh ?? string.Empty;
            FormattedDigestZh = formattedDigestZh ?? string.Empty;
        }

        public RpgWorldMapId MapId { get; }
        public string MapDisplayNameZh { get; }
        public string DistrictDisplayNameZh { get; }
        public int DayCount { get; }
        public string FormattedClock { get; }
        public string WeatherDisplayNameZh { get; }
        public RpgWorld500xTravelRiskLevel TravelRiskLevel { get; }
        public string FactionNameZh { get; }
        public string ReputationRankZh { get; }
        public int CampDiscountPercent { get; }
        public int SaltSilverBalance { get; }
        public int TotalOwnedSupplyItems { get; }
        public int ActiveMapCompletionPercent { get; }
        public int GlobalWorldCompletionPercent { get; }
        public int ClaimableMilestoneCount { get; }
        public string NextObjectiveHintZh { get; }
        public string FormattedDigestZh { get; }
    }

    /// <summary>
    /// Synthesizes a concise, deterministic Chinese campfire &amp; expedition recap
    /// digest across all 500x world subsystems (active map &amp; district, weather risk,
    /// faction standing, Salt-Silver &amp; supply stock, map/global completion, claimable
    /// milestones, and prioritized next objective hint).
    /// </summary>
    public static class RpgWorld500xCampfireJournalDigestService
    {
        public static RpgWorld500xExpeditionDigest BuildDigest(
            RpgWorld500xSceneBootstrapper bootstrapper,
            float elapsedSeconds = 0f,
            RpgWorld500xFieldSupplyUseService supplyUseService = null)
        {
            if (bootstrapper == null || bootstrapper.IsDisposed)
            {
                throw new ArgumentNullException(nameof(bootstrapper));
            }

            RpgWorld500xGrandRuntimeFacade facade = bootstrapper.Driver.Facade;
            RpgWorldMapId mapId = facade.CurrentMapId;
            string mapNameZh = facade.Session.Host.Layout.Spec.DisplayNameZh;
            Vector2 playerPos = facade.Session.Host.PlayerPosition;
            var playerTile = new Vector2Int(Mathf.FloorToInt(playerPos.x), Mathf.FloorToInt(playerPos.y));

            string districtNameZh = mapNameZh;
            if (RpgWorld500xDistrictCatalog.TryResolveDistrictAtTile(mapId, playerTile, out RpgWorld500xDistrictDef district))
            {
                districtNameZh = district.DisplayNameZh;
            }

            RpgWorld500xAtmosphereSnapshot atmo = facade.Atmosphere.GetSnapshot(mapId);
            RpgWorld500xTravelAdvisoryReport advisory =
                RpgWorld500xWeatherTravelAdvisoryService.BuildAdvisory(atmo, elapsedSeconds);

            IReadOnlyDictionary<string, int> store = bootstrapper.Store;
            Func<string, int> read = k => (store != null && store.TryGetValue(k, out int v)) ? v : 0;

            var repService = new RpgWorld500xRegionalReputationService(read);
            RpgRegionalReputationSnapshot rep = repService.GetSnapshot(mapId);

            int saltSilver = facade.BountyAndSupply.SaltSilverBalance;
            int totalSupplies = 0;
            IReadOnlyList<RpgWorld500xCampSupplyItemDef> supplyDefs =
                RpgWorld500xBountyAndSupplyCatalog.GetCampSupplies();
            for (int i = 0; i < supplyDefs.Count; i++)
            {
                totalSupplies += facade.BountyAndSupply.GetOwnedSupplyCount(supplyDefs[i].Id);
            }

            RpgWorld500xMapCompletionSummary mapComp = facade.GetCompletionSummary();

            var milestoneService = new RpgWorld500xExpeditionMilestoneService(read);
            List<RpgWorld500xMilestoneStatus> milestoneStatuses =
                milestoneService.BuildStatusList(out RpgWorld500xCrossMapProgressSnapshot globalComp);
            int claimableMilestones = 0;
            for (int i = 0; i < milestoneStatuses.Count; i++)
            {
                if (!milestoneStatuses[i].IsClaimed && milestoneStatuses[i].IsSatisfied)
                {
                    claimableMilestones++;
                }
            }

            string activeBuffCaption = string.Empty;
            if (supplyUseService != null)
            {
                RpgWorld500xActiveSupplyBuffModifiers mods = supplyUseService.EvaluateActiveModifiers(elapsedSeconds);
                if (mods.ActiveBuffCount > 0)
                {
                    activeBuffCaption = mods.ActiveBuffSummaryZh;
                }
            }

            string nextHint = ResolveNextObjectiveHintZh(
                mapId,
                playerPos,
                read,
                advisory,
                claimableMilestones);

            var sb = new StringBuilder(384);
            sb.AppendLine("【远征营火纪要】" + mapNameZh + " · " + districtNameZh);
            sb.AppendLine("天时：第 " + atmo.DayCount + " 日 " + atmo.FormattedClock + " " +
                          atmo.PhaseDisplayName + " · " + atmo.WeatherDisplayName +
                          "（" + advisory.RiskLabelZh + "）");
            sb.AppendLine("声望与补给：" + rep.FactionNameZh + "「" + rep.RankNameZh + "」（营地 -" +
                          rep.CampSupplyDiscountPercent + "%）｜盐银 " + saltSilver +
                          "｜随身补给 " + totalSupplies + " 件");
            sb.AppendLine("堪舆进度：本图 " + mapComp.OverallCompletionPercent + "%｜四境总进度 " +
                          globalComp.OverallWorldCompletionPercent + "%（待领里程碑 " + claimableMilestones + " 项）");
            if (!string.IsNullOrEmpty(activeBuffCaption))
            {
                sb.AppendLine("当前增益：" + activeBuffCaption);
            }

            sb.Append("下一步建议：" + nextHint);

            return new RpgWorld500xExpeditionDigest(
                mapId,
                mapNameZh,
                districtNameZh,
                atmo.DayCount,
                atmo.FormattedClock,
                atmo.WeatherDisplayName,
                advisory.RiskLevel,
                rep.FactionNameZh,
                rep.RankNameZh,
                rep.CampSupplyDiscountPercent,
                saltSilver,
                totalSupplies,
                mapComp.OverallCompletionPercent,
                globalComp.OverallWorldCompletionPercent,
                claimableMilestones,
                nextHint,
                sb.ToString());
        }

        private static string ResolveNextObjectiveHintZh(
            RpgWorldMapId mapId,
            Vector2 playerPos,
            Func<string, int> read,
            RpgWorld500xTravelAdvisoryReport advisory,
            int claimableMilestones)
        {
            if (claimableMilestones > 0)
            {
                return "有 " + claimableMilestones + " 项跨境远征里程碑待领取，打开远征总录（U）领取盐银奖励。";
            }

            if (advisory.RecommendCampfireRest)
            {
                return "当前天时高危，建议先在附近营火休整至次日拂晓再继续深入。";
            }

            var scanner = new RpgWorld500xFieldReconScannerService(read, cooldownSeconds: 0f);
            if (scanner.TryPulseScan(mapId, playerPos, 0f, out List<RpgWorld500xReconPing> pings, out _,
                baseRadiusTiles: 900f, visibilityMultiplier: 1f, maxResults: 1) &&
                pings.Count > 0)
            {
                return "优先前往近处未探索目标：" + pings[0].FormattedPingZh;
            }

            List<RpgWorld500xRegionRouteDef> routes = RpgWorld500xWorldAtlasPortalService.GetConnectedRegions(mapId);
            if (routes.Count > 0)
            {
                return "本片区已大致踏勘，可沿【" + routes[0].RouteNameZh + "】前往相邻区域继续远征。";
            }

            return "继续沿主线推进当前区域探索。";
        }
    }
}
