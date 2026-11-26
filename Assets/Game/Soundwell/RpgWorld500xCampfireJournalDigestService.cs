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
            string formattedDigestZh,
            int readinessScore = 0,
            string readinessGradeLabelZh = "",
            int hazardousRegionCount = 0,
            string worldWeatherBulletinZh = "",
            string worldReputationSummaryZh = "",
            string milestoneSummaryZh = "")
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
            ReadinessScore = readinessScore;
            ReadinessGradeLabelZh = readinessGradeLabelZh ?? string.Empty;
            HazardousRegionCount = hazardousRegionCount;
            WorldWeatherBulletinZh = worldWeatherBulletinZh ?? string.Empty;
            WorldReputationSummaryZh = worldReputationSummaryZh ?? string.Empty;
            MilestoneSummaryZh = milestoneSummaryZh ?? string.Empty;
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
        public int ReadinessScore { get; }
        public string ReadinessGradeLabelZh { get; }
        public int HazardousRegionCount { get; }
        public string WorldWeatherBulletinZh { get; }
        public string WorldReputationSummaryZh { get; }
        public string MilestoneSummaryZh { get; }
    }

    /// <summary>
    /// Synthesizes a concise, deterministic Chinese campfire &amp; expedition recap
    /// digest across all 500x world subsystems (active map &amp; district, weather risk,
    /// cross-region weather bulletin, faction standing &amp; world reputation overview,
    /// Salt-Silver &amp; supply stock, expedition readiness &amp; camp provisioning cost,
    /// map/global completion, milestone summary, and prioritized next objective hint).
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
            RpgWorld500xWorldWeatherDigest worldWeather =
                RpgWorld500xWeatherTravelAdvisoryService.BuildWorldWeatherDigest(facade.Atmosphere, elapsedSeconds);

            IReadOnlyDictionary<string, int> store = bootstrapper.Store;
            Func<string, int> read = k => (store != null && store.TryGetValue(k, out int v)) ? v : 0;

            var repService = new RpgWorld500xRegionalReputationService(read);
            RpgRegionalReputationSnapshot rep = repService.GetSnapshot(mapId);
            RpgWorld500xWorldReputationOverview worldRep = repService.BuildWorldOverview();

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
            RpgWorld500xMilestoneSummary milestoneSummary = milestoneService.BuildMilestoneSummary();
            RpgWorld500xCrossMapProgressSnapshot globalComp = milestoneSummary.Progress;
            int claimableMilestones = milestoneSummary.ClaimableCount;

            RpgWorld500xExpeditionReadinessReport readiness =
                RpgWorld500xExpeditionReadinessService.EvaluateReadiness(
                    bootstrapper, elapsedSeconds, supplyUseService);
            RpgWorld500xProvisioningPlan localPlan =
                RpgWorld500xCampProvisioningService.BuildProvisioningPlan(
                    bootstrapper, mapId, elapsedSeconds, supplyUseService);

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

            var sb = new StringBuilder(640);
            sb.AppendLine("【远征营火纪要】" + mapNameZh + " · " + districtNameZh);
            sb.AppendLine("天时：第 " + atmo.DayCount + " 日 " + atmo.FormattedClock + " " +
                          atmo.PhaseDisplayName + " · " + atmo.WeatherDisplayName +
                          "（" + advisory.RiskLabelZh + "）");
            sb.AppendLine("声望与补给：" + rep.FactionNameZh + "「" + rep.RankNameZh + "」（营地 -" +
                          rep.CampSupplyDiscountPercent + "%）｜盐银 " + saltSilver +
                          "｜随身补给 " + totalSupplies + " 件");
            sb.Append("整备状态：").Append(readiness.Score).Append("/100（").Append(readiness.GradeLabelZh).Append("）");
            if (localPlan.Lines.Count > 0)
            {
                sb.Append("｜本境待补 ").Append(localPlan.Lines.Count).Append(" 项（需 ")
                  .Append(localPlan.TotalRequiredSaltSilver).AppendLine(" 盐银，按 P/Y 可一键整备）");
            }
            else
            {
                sb.AppendLine("｜行囊已齐备");
            }

            sb.AppendLine("堪舆进度：本图 " + mapComp.OverallCompletionPercent + "%｜四境总进度 " +
                          globalComp.OverallWorldCompletionPercent + "%（待领里程碑 " + claimableMilestones + " 项）");
            sb.AppendLine(worldWeather.WorldBulletinZh);
            sb.AppendLine(worldRep.SummaryLineZh);
            sb.AppendLine(milestoneSummary.SummaryLineZh);
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
                sb.ToString(),
                readiness.Score,
                readiness.GradeLabelZh,
                worldWeather.HazardousRegionCount,
                worldWeather.WorldBulletinZh,
                worldRep.SummaryLineZh,
                milestoneSummary.SummaryLineZh);
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
            // Ambient zones are repeatable scenery/events, not completion-tracked POIs.
            // Keep the scanner's distance/ID order, but do not let an ambient ping
            // hide the nearest actionable target or block the adjacent-region hint.
            if (TryFindActionablePing(scanner, mapId, playerPos, 900f, out var nearby))
            {
                return "优先前往近处未探索目标：" + nearby.FormattedPingZh;
            }

            // Finish the current region's tracked objectives before sending the player away.
            RpgExpandedMapSpec currentSpec = RpgExpandedWorldScale.GetSpec(mapId);
            if (TryFindActionablePing(scanner, mapId, playerPos,
                currentSpec.ExpandedWidth + currentSpec.ExpandedHeight, out var distant))
            {
                return "附近暂无未探索目标，本境仍有较远目标：" + distant.FormattedPingZh;
            }

            // Choose the shortest existing itinerary to a region that actually has
            // unfinished field objectives. Completed regions may be transit stops,
            // but must not be presented as the final exploration destination.
            bool found = false;
            RpgWorld500xTravelItinerary best = default;
            string destinationName = string.Empty;
            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                if (spec.MapId == mapId || !TryFindActionablePing(scanner, spec.MapId, Vector2.zero,
                    spec.ExpandedWidth + spec.ExpandedHeight, out _))
                {
                    continue;
                }

                RpgWorld500xTravelItinerary itinerary =
                    RpgWorld500xFastTravelItineraryService.PlanShortestItinerary(mapId, spec.MapId);
                if (itinerary.IsReachable && itinerary.Legs.Count > 0 &&
                    (!found || itinerary.TotalTravelMinutesInGame < best.TotalTravelMinutesInGame))
                {
                    found = true;
                    best = itinerary;
                    destinationName = spec.DisplayNameZh;
                }
            }

            if (found)
            {
                string routeName = best.Legs[0].Route.RouteNameZh;
                return best.HopCount == 1
                    ? "本境暂无未探索野外目标，可沿【" + routeName + "】前往相邻区域" + destinationName + "继续远征。"
                    : "本境暂无未探索野外目标，可先沿【" + routeName + "】中转，前往" + destinationName + "继续远征。";
            }

            return "四境暂无未探索野外目标，继续沿主线、悬赏与堪舆目标推进。";
        }

        private static bool TryFindActionablePing(
            RpgWorld500xFieldReconScannerService scanner,
            RpgWorldMapId mapId,
            Vector2 playerPos,
            float radius,
            out RpgWorld500xReconPing ping)
        {
            ping = default;
            if (!scanner.TryPulseScan(mapId, playerPos, 0f, out var pings, out _,
                baseRadiusTiles: radius, visibilityMultiplier: 1f, maxResults: int.MaxValue))
            {
                return false;
            }

            foreach (var candidate in pings)
            {
                if (candidate.Kind != RpgWorld500xReconPingKind.AmbientEventZone)
                {
                    ping = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
