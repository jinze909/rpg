using System;
using System.Collections.Generic;
using System.Text;

namespace Rpg.Soundwell
{
    public readonly struct RpgWorld500xItineraryLeg
    {
        public RpgWorld500xItineraryLeg(
            RpgWorld500xRegionRouteDef route,
            RpgWorld500xTravelRiskLevel destinationRiskLevel,
            string destinationRiskLabelZh)
        {
            Route = route;
            DestinationRiskLevel = destinationRiskLevel;
            DestinationRiskLabelZh = destinationRiskLabelZh ?? string.Empty;
        }

        public RpgWorld500xRegionRouteDef Route { get; }
        public RpgWorld500xTravelRiskLevel DestinationRiskLevel { get; }
        public string DestinationRiskLabelZh { get; }
    }

    public readonly struct RpgWorld500xTravelItinerary
    {
        public RpgWorld500xTravelItinerary(
            RpgWorldMapId originMapId,
            RpgWorldMapId destinationMapId,
            bool isReachable,
            int hopCount,
            int totalTravelMinutesInGame,
            RpgWorld500xTravelRiskLevel highestRiskLevel,
            bool recommendCampfireStop,
            IReadOnlyList<RpgWorld500xItineraryLeg> legs,
            string summaryZh)
        {
            OriginMapId = originMapId;
            DestinationMapId = destinationMapId;
            IsReachable = isReachable;
            HopCount = hopCount;
            TotalTravelMinutesInGame = totalTravelMinutesInGame;
            HighestRiskLevel = highestRiskLevel;
            RecommendCampfireStop = recommendCampfireStop;
            Legs = legs ?? Array.Empty<RpgWorld500xItineraryLeg>();
            SummaryZh = summaryZh ?? string.Empty;
        }

        public RpgWorldMapId OriginMapId { get; }
        public RpgWorldMapId DestinationMapId { get; }
        public bool IsReachable { get; }
        public int HopCount { get; }
        public int TotalTravelMinutesInGame { get; }
        public RpgWorld500xTravelRiskLevel HighestRiskLevel { get; }
        public bool RecommendCampfireStop { get; }
        public IReadOnlyList<RpgWorld500xItineraryLeg> Legs { get; }
        public string SummaryZh { get; }
    }

    /// <summary>
    /// Multi-hop shortest-time and weather-safest cross-region itinerary planner over
    /// the four 500x playable maps (Soundwell, RedSaltHarbor, WhitewaterCrossing, MineCamp),
    /// annotating each travel leg with live weather risk and campfire stop advisories.
    /// </summary>
    public static class RpgWorld500xFastTravelItineraryService
    {
        private const int RiskWeightPerLevel = 10000;

        public static RpgWorld500xTravelItinerary PlanShortestItinerary(
            RpgWorldMapId originMapId,
            RpgWorldMapId destinationMapId,
            RpgWorld500xWeatherAndTimeOfDayService atmosphere = null)
        {
            return PlanItineraryCore(originMapId, destinationMapId, atmosphere, prioritizeSafety: false);
        }

        public static RpgWorld500xTravelItinerary PlanSafestItinerary(
            RpgWorldMapId originMapId,
            RpgWorldMapId destinationMapId,
            RpgWorld500xWeatherAndTimeOfDayService atmosphere = null)
        {
            return PlanItineraryCore(originMapId, destinationMapId, atmosphere, prioritizeSafety: true);
        }

        private static RpgWorld500xTravelItinerary PlanItineraryCore(
            RpgWorldMapId originMapId,
            RpgWorldMapId destinationMapId,
            RpgWorld500xWeatherAndTimeOfDayService atmosphere,
            bool prioritizeSafety)
        {
            // Validate against the playable catalog before the zero-hop shortcut.
            // An unknown ID is not a reachable destination, even if both IDs match.
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            bool hasOrigin = false;
            bool hasDestination = false;
            for (int i = 0; i < specs.Count; i++)
            {
                hasOrigin |= specs[i].MapId == originMapId;
                hasDestination |= specs[i].MapId == destinationMapId;
            }

            if (!hasOrigin || !hasDestination)
            {
                return new RpgWorld500xTravelItinerary(
                    originMapId, destinationMapId, false, 0, 0,
                    RpgWorld500xTravelRiskLevel.Safe, false,
                    Array.Empty<RpgWorld500xItineraryLeg>(),
                    "【行程规划】未找到有效区域路径。");
            }

            if (originMapId == destinationMapId)
            {
                RpgWorld500xTravelRiskLevel localRisk = ResolveMapRisk(destinationMapId, atmosphere);
                string sameSummary = "【行程规划】当前已在目的地（" + ResolveMapNameZh(destinationMapId) + "），无需跨区跋涉。";
                return new RpgWorld500xTravelItinerary(
                    originMapId,
                    destinationMapId,
                    isReachable: true,
                    hopCount: 0,
                    totalTravelMinutesInGame: 0,
                    highestRiskLevel: localRisk,
                    recommendCampfireStop: localRisk == RpgWorld500xTravelRiskLevel.Hazardous,
                    legs: Array.Empty<RpgWorld500xItineraryLeg>(),
                    summaryZh: sameSummary);
            }

            // Deterministic Dijkstra over the 4-region portal graph by in-game travel minutes
            // (with weather-risk penalty weighted above minutes when prioritizeSafety is true).
            var dist = new Dictionary<RpgWorldMapId, int>();
            var prevEdge = new Dictionary<RpgWorldMapId, RpgWorld500xRegionRouteDef>();
            var unvisited = new List<RpgWorldMapId>();

            for (int i = 0; i < specs.Count; i++)
            {
                RpgWorldMapId id = specs[i].MapId;
                dist[id] = int.MaxValue;
                unvisited.Add(id);
            }

            dist[originMapId] = 0;

            while (unvisited.Count > 0)
            {
                int bestIndex = -1;
                int bestCost = int.MaxValue;
                for (int i = 0; i < unvisited.Count; i++)
                {
                    int c = dist[unvisited[i]];
                    if (c < bestCost)
                    {
                        bestCost = c;
                        bestIndex = i;
                    }
                }

                if (bestIndex < 0 || bestCost == int.MaxValue)
                {
                    break;
                }

                RpgWorldMapId current = unvisited[bestIndex];
                unvisited.RemoveAt(bestIndex);
                if (current == destinationMapId)
                {
                    break;
                }

                List<RpgWorld500xRegionRouteDef> edges = RpgWorld500xWorldAtlasPortalService.GetConnectedRegions(current);
                for (int i = 0; i < edges.Count; i++)
                {
                    RpgWorld500xRegionRouteDef edge = edges[i];
                    if (!dist.TryGetValue(edge.ToMap, out int existingCost))
                    {
                        continue;
                    }

                    int riskPenalty = prioritizeSafety
                        ? (int)ResolveMapRisk(edge.ToMap, atmosphere) * RiskWeightPerLevel
                        : 0;
                    int candidate = bestCost + riskPenalty + edge.TravelMinutesInGame;
                    if (candidate < existingCost)
                    {
                        dist[edge.ToMap] = candidate;
                        prevEdge[edge.ToMap] = edge;
                    }
                }
            }

            if (!prevEdge.ContainsKey(destinationMapId))
            {
                return new RpgWorld500xTravelItinerary(
                    originMapId, destinationMapId, false, 0, 0,
                    RpgWorld500xTravelRiskLevel.Safe, false,
                    Array.Empty<RpgWorld500xItineraryLeg>(),
                    "【行程规划】目标区域当前不可达。");
            }

            var reversedRoutes = new List<RpgWorld500xRegionRouteDef>();
            RpgWorldMapId cursor = destinationMapId;
            while (cursor != originMapId && prevEdge.TryGetValue(cursor, out RpgWorld500xRegionRouteDef step))
            {
                reversedRoutes.Add(step);
                cursor = step.FromMap;
            }

            reversedRoutes.Reverse();
            var legs = new List<RpgWorld500xItineraryLeg>(reversedRoutes.Count);
            int totalMinutes = 0;
            RpgWorld500xTravelRiskLevel highestRisk = RpgWorld500xTravelRiskLevel.Safe;

            for (int i = 0; i < reversedRoutes.Count; i++)
            {
                RpgWorld500xRegionRouteDef route = reversedRoutes[i];
                totalMinutes += route.TravelMinutesInGame;
                RpgWorld500xTravelRiskLevel legRisk = ResolveMapRisk(route.ToMap, atmosphere);
                if ((int)legRisk > (int)highestRisk)
                {
                    highestRisk = legRisk;
                }

                legs.Add(new RpgWorld500xItineraryLeg(
                    route,
                    legRisk,
                    RpgWorld500xWeatherTravelAdvisoryService.GetRiskLabelZh(legRisk)));
            }

            bool recommendRest = highestRisk == RpgWorld500xTravelRiskLevel.Hazardous || totalMinutes >= 240;

            var sb = new StringBuilder(192);
            sb.Append(prioritizeSafety ? "【安全绕行行程】" : "【跨境行程】")
              .Append(ResolveMapNameZh(originMapId)).Append(" → ").Append(ResolveMapNameZh(destinationMapId))
              .Append("（共 ").Append(legs.Count).Append(" 段，耗时 ").Append(totalMinutes).Append(" 分钟｜最高风险：")
              .Append(RpgWorld500xWeatherTravelAdvisoryService.GetRiskLabelZh(highestRisk)).Append("）");
            if (recommendRest)
            {
                sb.Append("｜建议途经营火休整");
            }

            return new RpgWorld500xTravelItinerary(
                originMapId,
                destinationMapId,
                isReachable: true,
                hopCount: legs.Count,
                totalTravelMinutesInGame: totalMinutes,
                highestRiskLevel: highestRisk,
                recommendCampfireStop: recommendRest,
                legs: legs,
                summaryZh: sb.ToString());
        }

        private static RpgWorld500xTravelRiskLevel ResolveMapRisk(
            RpgWorldMapId mapId,
            RpgWorld500xWeatherAndTimeOfDayService atmosphere)
        {
            if (atmosphere == null)
            {
                return RpgWorld500xTravelRiskLevel.Safe;
            }

            return RpgWorld500xWeatherTravelAdvisoryService.ClassifyRisk(atmosphere.GetSnapshot(mapId));
        }

        private static string ResolveMapNameZh(RpgWorldMapId mapId)
        {
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
    }
}
