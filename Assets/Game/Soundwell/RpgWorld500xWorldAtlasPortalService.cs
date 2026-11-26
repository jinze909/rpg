using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rpg.Soundwell
{
    public readonly struct RpgWorld500xRegionRouteDef
    {
        public RpgWorld500xRegionRouteDef(
            string routeId,
            RpgWorldMapId fromMap,
            RpgWorldMapId toMap,
            string routeNameZh,
            int travelMinutesInGame,
            string flavorNoteZh)
        {
            RouteId = routeId;
            FromMap = fromMap;
            ToMap = toMap;
            RouteNameZh = routeNameZh;
            TravelMinutesInGame = travelMinutesInGame;
            FlavorNoteZh = flavorNoteZh;
        }

        public string RouteId { get; }
        public RpgWorldMapId FromMap { get; }
        public RpgWorldMapId ToMap { get; }
        public string RouteNameZh { get; }
        public int TravelMinutesInGame { get; }
        public string FlavorNoteZh { get; }
    }

    public readonly struct RpgWorld500xRegionTravelResult
    {
        public RpgWorld500xRegionTravelResult(
            bool succeeded,
            RpgWorldMapId fromMap,
            RpgWorldMapId toMap,
            string routeNameZh,
            int travelMinutesElapsed,
            RpgWorld500xAtmosphereSnapshot arrivalAtmosphere,
            string arrivalBannerZh)
        {
            Succeeded = succeeded;
            FromMap = fromMap;
            ToMap = toMap;
            RouteNameZh = routeNameZh ?? string.Empty;
            TravelMinutesElapsed = travelMinutesElapsed;
            ArrivalAtmosphere = arrivalAtmosphere;
            ArrivalBannerZh = arrivalBannerZh ?? string.Empty;
        }

        public bool Succeeded { get; }
        public RpgWorldMapId FromMap { get; }
        public RpgWorldMapId ToMap { get; }
        public string RouteNameZh { get; }
        public int TravelMinutesElapsed { get; }
        public RpgWorld500xAtmosphereSnapshot ArrivalAtmosphere { get; }
        public string ArrivalBannerZh { get; }
    }

    public readonly struct RpgWorld500xRegionAtlasEntry
    {
        public RpgWorld500xRegionAtlasEntry(
            RpgWorldMapId mapId,
            string displayNameZh,
            RpgRegionalReputationSnapshot reputation,
            int claimedBounties,
            int totalBounties,
            int connectedRouteCount)
        {
            MapId = mapId;
            DisplayNameZh = displayNameZh;
            Reputation = reputation;
            ClaimedBounties = claimedBounties;
            TotalBounties = totalBounties;
            ConnectedRouteCount = connectedRouteCount;
        }

        public RpgWorldMapId MapId { get; }
        public string DisplayNameZh { get; }
        public RpgRegionalReputationSnapshot Reputation { get; }
        public int ClaimedBounties { get; }
        public int TotalBounties { get; }
        public int ConnectedRouteCount { get; }
    }

    public readonly struct RpgWorld500xRegionPathPlan
    {
        public RpgWorld500xRegionPathPlan(
            RpgWorldMapId fromMap,
            RpgWorldMapId toMap,
            bool isSameRegion,
            bool isDirect,
            int totalTravelMinutesInGame,
            IReadOnlyList<RpgWorld500xRegionRouteDef> hops,
            string summaryZh)
        {
            FromMap = fromMap;
            ToMap = toMap;
            IsSameRegion = isSameRegion;
            IsDirect = isDirect;
            TotalTravelMinutesInGame = totalTravelMinutesInGame;
            Hops = hops ?? Array.Empty<RpgWorld500xRegionRouteDef>();
            SummaryZh = summaryZh ?? string.Empty;
        }

        public RpgWorldMapId FromMap { get; }
        public RpgWorldMapId ToMap { get; }
        public bool IsSameRegion { get; }
        public bool IsDirect { get; }
        public int TotalTravelMinutesInGame { get; }
        public IReadOnlyList<RpgWorld500xRegionRouteDef> Hops { get; }
        public string SummaryZh { get; }
    }

    public readonly struct RpgWorld500xWorldAtlasSummary
    {
        public RpgWorld500xWorldAtlasSummary(
            IReadOnlyList<RpgWorld500xRegionAtlasEntry> entries,
            int totalClaimedBounties,
            int totalBounties,
            int remainingBounties,
            int totalReputationScore,
            RpgWorldMapId highestReputationMap,
            string summaryZh)
        {
            Entries = entries ?? Array.Empty<RpgWorld500xRegionAtlasEntry>();
            TotalClaimedBounties = totalClaimedBounties;
            TotalBounties = totalBounties;
            RemainingBounties = remainingBounties;
            TotalReputationScore = totalReputationScore;
            HighestReputationMap = highestReputationMap;
            SummaryZh = summaryZh ?? string.Empty;
        }

        public IReadOnlyList<RpgWorld500xRegionAtlasEntry> Entries { get; }
        public int TotalRegions => Entries.Count;
        public int TotalClaimedBounties { get; }
        public int TotalBounties { get; }
        public int RemainingBounties { get; }
        public int TotalReputationScore { get; }
        public RpgWorldMapId HighestReputationMap { get; }
        public string SummaryZh { get; }
    }

    /// <summary>
    /// Connects all four 500x-area maps (Soundwell, Red Salt Harbor, Whitewater
    /// Crossing, Mine Camp) into a bidirectional regional travel graph with in-game
    /// travel-time clock advancement, clean map switching, and a 4-region world atlas.
    /// </summary>
    public static class RpgWorld500xWorldAtlasPortalService
    {
        private static readonly RpgWorld500xRegionRouteDef[] BidirectionalRoutes =
        {
            new RpgWorld500xRegionRouteDef("route_sw_rsh", RpgWorldMapId.Soundwell, RpgWorldMapId.RedSaltHarbor,
                "山径古道 · 声井至赤盐港", 120, "沿钟岭南麓下行，穿过盐雾谷直抵赤盐外港。"),
            new RpgWorld500xRegionRouteDef("route_rsh_sw", RpgWorldMapId.RedSaltHarbor, RpgWorldMapId.Soundwell,
                "山径古道 · 赤盐港至声井", 120, "离开码头北坡，沿测绘班旧路标重返声井。"),
            new RpgWorld500xRegionRouteDef("route_rsh_ww", RpgWorldMapId.RedSaltHarbor, RpgWorldMapId.WhitewaterCrossing,
                "赤盐官渡水路 · 港至白水渡", 90, "乘浅水驳船溯流而上，抵达白水渡西岸。"),
            new RpgWorld500xRegionRouteDef("route_ww_rsh", RpgWorldMapId.WhitewaterCrossing, RpgWorldMapId.RedSaltHarbor,
                "赤盐官渡水路 · 白水渡至港", 90, "顺急流南下返港，途中注意暗礁标杆。"),
            new RpgWorld500xRegionRouteDef("route_rsh_mc", RpgWorldMapId.RedSaltHarbor, RpgWorldMapId.MineCamp,
                "北口矿运山道 · 港至赤盐矿营", 150, "过北口石门坊后沿运盐车辙直进矿营南门。"),
            new RpgWorld500xRegionRouteDef("route_mc_rsh", RpgWorldMapId.MineCamp, RpgWorldMapId.RedSaltHarbor,
                "北口矿运山道 · 矿营至赤盐港", 150, "避开护矿队外围哨楼，沿旧盐道返回港区。"),
            new RpgWorld500xRegionRouteDef("route_ww_mc", RpgWorldMapId.WhitewaterCrossing, RpgWorldMapId.MineCamp,
                "东岸山口密径 · 白水渡至矿营", 180, "自东岸撤离山口翻越山脊，切入矿营西侧。"),
            new RpgWorld500xRegionRouteDef("route_mc_ww", RpgWorldMapId.MineCamp, RpgWorldMapId.WhitewaterCrossing,
                "东岸山口密径 · 矿营至白水渡", 180, "沿旧矿工撤离小道穿过山梁直抵白水渡东岸。")
        };

        public static List<RpgWorld500xRegionRouteDef> GetConnectedRegions(RpgWorldMapId fromMap)
        {
            var result = new List<RpgWorld500xRegionRouteDef>();
            for (int i = 0; i < BidirectionalRoutes.Length; i++)
            {
                if (BidirectionalRoutes[i].FromMap == fromMap)
                {
                    result.Add(BidirectionalRoutes[i]);
                }
            }

            return result;
        }

        public static bool TryFindDirectRoute(
            RpgWorldMapId fromMap,
            RpgWorldMapId toMap,
            out RpgWorld500xRegionRouteDef route)
        {
            for (int i = 0; i < BidirectionalRoutes.Length; i++)
            {
                if (BidirectionalRoutes[i].FromMap == fromMap && BidirectionalRoutes[i].ToMap == toMap)
                {
                    route = BidirectionalRoutes[i];
                    return true;
                }
            }

            route = default;
            return false;
        }

        public static bool TryFindShortestRegionPath(
            RpgWorldMapId fromMap,
            RpgWorldMapId toMap,
            out RpgWorld500xRegionPathPlan plan)
        {
            plan = default;
            if (!Enum.IsDefined(typeof(RpgWorldMapId), fromMap) ||
                !Enum.IsDefined(typeof(RpgWorldMapId), toMap))
            {
                return false;
            }

            if (fromMap == toMap)
            {
                plan = new RpgWorld500xRegionPathPlan(
                    fromMap,
                    toMap,
                    isSameRegion: true,
                    isDirect: false,
                    totalTravelMinutesInGame: 0,
                    hops: Array.Empty<RpgWorld500xRegionRouteDef>(),
                    summaryZh: "【跨境路线】已在当前大区，无需跨区跋涉");
                return true;
            }

            var bestMinutes = new Dictionary<RpgWorldMapId, int> { [fromMap] = 0 };
            var bestHopCount = new Dictionary<RpgWorldMapId, int> { [fromMap] = 0 };
            var prevEdge = new Dictionary<RpgWorldMapId, RpgWorld500xRegionRouteDef>();
            var visited = new HashSet<RpgWorldMapId>();

            while (true)
            {
                bool foundNext = false;
                RpgWorldMapId current = default;
                int currentMin = int.MaxValue;
                int currentHops = int.MaxValue;

                foreach (KeyValuePair<RpgWorldMapId, int> kvp in bestMinutes)
                {
                    if (visited.Contains(kvp.Key))
                    {
                        continue;
                    }

                    int hops = bestHopCount[kvp.Key];
                    if (!foundNext ||
                        kvp.Value < currentMin ||
                        (kvp.Value == currentMin && hops < currentHops) ||
                        (kvp.Value == currentMin && hops == currentHops && (int)kvp.Key < (int)current))
                    {
                        foundNext = true;
                        current = kvp.Key;
                        currentMin = kvp.Value;
                        currentHops = hops;
                    }
                }

                if (!foundNext)
                {
                    break;
                }

                visited.Add(current);
                if (current == toMap)
                {
                    break;
                }

                for (int i = 0; i < BidirectionalRoutes.Length; i++)
                {
                    RpgWorld500xRegionRouteDef edge = BidirectionalRoutes[i];
                    if (edge.FromMap != current || visited.Contains(edge.ToMap))
                    {
                        continue;
                    }

                    int candMin = currentMin + Math.Max(0, edge.TravelMinutesInGame);
                    int candHops = currentHops + 1;
                    bool hasExisting = bestMinutes.TryGetValue(edge.ToMap, out int existingMin);
                    int existingHops = hasExisting ? bestHopCount[edge.ToMap] : int.MaxValue;

                    if (!hasExisting ||
                        candMin < existingMin ||
                        (candMin == existingMin && candHops < existingHops) ||
                        (candMin == existingMin && candHops == existingHops &&
                         StringComparer.Ordinal.Compare(edge.RouteId, prevEdge[edge.ToMap].RouteId) < 0))
                    {
                        bestMinutes[edge.ToMap] = candMin;
                        bestHopCount[edge.ToMap] = candHops;
                        prevEdge[edge.ToMap] = edge;
                    }
                }
            }

            if (!prevEdge.ContainsKey(toMap))
            {
                return false;
            }

            var hopsList = new List<RpgWorld500xRegionRouteDef>();
            RpgWorldMapId cursor = toMap;
            while (cursor != fromMap && prevEdge.TryGetValue(cursor, out RpgWorld500xRegionRouteDef step))
            {
                hopsList.Add(step);
                cursor = step.FromMap;
            }

            hopsList.Reverse();
            int totalMinutes = bestMinutes[toMap];
            var chain = new string[hopsList.Count];
            for (int i = 0; i < hopsList.Count; i++)
            {
                chain[i] = hopsList[i].RouteNameZh;
            }

            string summaryZh = "【跨境路线】共 " + hopsList.Count + " 段｜预计耗时 " + totalMinutes +
                               " 分钟（" + string.Join(" -> ", chain) + "）";

            plan = new RpgWorld500xRegionPathPlan(
                fromMap,
                toMap,
                isSameRegion: false,
                isDirect: hopsList.Count == 1,
                totalTravelMinutesInGame: totalMinutes,
                hops: hopsList,
                summaryZh: summaryZh);
            return true;
        }

        public static bool TryTravelToConnectedRegion(
            RpgWorld500xSceneBootstrapper bootstrapper,
            RpgWorldMapId targetMap,
            out RpgWorld500xRegionTravelResult result)
        {
            result = default;
            if (bootstrapper == null || bootstrapper.IsDisposed)
            {
                return false;
            }

            RpgWorld500xGrandRuntimeFacade facade = bootstrapper.Driver.Facade;
            RpgWorldMapId currentMap = facade.CurrentMapId;
            // Region travel must respect the same active encounter lock as local fast travel.
            if (facade.Session.Host.Bridge.IsInDangerEncounter ||
                !TryFindDirectRoute(currentMap, targetMap, out RpgWorld500xRegionRouteDef route))
            {
                return false;
            }

            if (!bootstrapper.SwitchMap(targetMap))
            {
                return false;
            }

            // Charge travel time only after a successful map transition.
            float realSecondsEquivalent = route.TravelMinutesInGame /
                                          RpgWorld500xWeatherAndTimeOfDayService.DefaultGameMinutesPerRealSecond;
            bootstrapper.Driver.Facade.Atmosphere.AdvanceRealSeconds(realSecondsEquivalent, targetMap);
            bootstrapper.StatusHud.SyncFromDriver(bootstrapper.Driver);

            RpgWorld500xAtmosphereSnapshot arrivalAtmo = bootstrapper.Driver.Facade.Atmosphere.GetSnapshot(targetMap);
            string banner = "已沿【" + route.RouteNameZh + "】抵达 · 第 " + arrivalAtmo.DayCount + " 日 " +
                            arrivalAtmo.FormattedClock + " " + arrivalAtmo.WeatherDisplayName;

            result = new RpgWorld500xRegionTravelResult(
                true,
                currentMap,
                targetMap,
                route.RouteNameZh,
                route.TravelMinutesInGame,
                arrivalAtmo,
                banner);
            return true;
        }

        public static List<RpgWorld500xRegionAtlasEntry> BuildWorldAtlasOverview(
            IReadOnlyDictionary<string, int> intStore)
        {
            Func<string, int> read = k => (intStore != null && intStore.TryGetValue(k, out int v)) ? v : 0;
            var repService = new RpgWorld500xRegionalReputationService(read);
            var entries = new List<RpgWorld500xRegionAtlasEntry>();

            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                IReadOnlyList<RpgWorld500xFieldBountyDef> bounties = RpgWorld500xBountyAndSupplyCatalog.GetBounties(spec.MapId);
                int claimed = 0;
                for (int i = 0; i < bounties.Count; i++)
                {
                    if (read(bounties[i].MakinomKey) == 1)
                    {
                        claimed++;
                    }
                }

                int routes = GetConnectedRegions(spec.MapId).Count;
                entries.Add(new RpgWorld500xRegionAtlasEntry(
                    spec.MapId,
                    spec.DisplayNameZh,
                    repService.GetSnapshot(spec.MapId),
                    claimed,
                    bounties.Count,
                    routes));
            }

            return entries;
        }

        public static RpgWorld500xWorldAtlasSummary BuildWorldAtlasSummary(
            IReadOnlyDictionary<string, int> intStore)
        {
            List<RpgWorld500xRegionAtlasEntry> entries = BuildWorldAtlasOverview(intStore);
            int totalClaimed = 0;
            int totalBounties = 0;
            int totalRep = 0;
            RpgWorldMapId highestMap = entries.Count > 0 ? entries[0].MapId : RpgWorldMapId.Soundwell;
            string highestNameZh = entries.Count > 0 ? entries[0].DisplayNameZh : string.Empty;
            int highestScore = entries.Count > 0 ? entries[0].Reputation.Score : int.MinValue;

            for (int i = 0; i < entries.Count; i++)
            {
                RpgWorld500xRegionAtlasEntry e = entries[i];
                totalClaimed += e.ClaimedBounties;
                totalBounties += e.TotalBounties;
                totalRep += e.Reputation.Score;
                if (e.Reputation.Score > highestScore)
                {
                    highestScore = e.Reputation.Score;
                    highestMap = e.MapId;
                    highestNameZh = e.DisplayNameZh;
                }
            }

            int remaining = Math.Max(0, totalBounties - totalClaimed);
            string summaryZh = "【四境总览】悬赏 " + totalClaimed + "/" + totalBounties +
                               "｜总声望 " + totalRep + "｜最高声望：" + highestNameZh;

            return new RpgWorld500xWorldAtlasSummary(
                entries,
                totalClaimed,
                totalBounties,
                remaining,
                totalRep,
                highestMap,
                summaryZh);
        }
    }
}
