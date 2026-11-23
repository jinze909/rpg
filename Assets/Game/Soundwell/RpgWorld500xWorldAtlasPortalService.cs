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

            RpgWorldMapId currentMap = bootstrapper.Driver.Facade.CurrentMapId;
            if (!TryFindDirectRoute(currentMap, targetMap, out RpgWorld500xRegionRouteDef route))
            {
                return false;
            }

            float realSecondsEquivalent = route.TravelMinutesInGame /
                                          RpgWorld500xWeatherAndTimeOfDayService.DefaultGameMinutesPerRealSecond;
            bootstrapper.Driver.Facade.Atmosphere.AdvanceRealSeconds(realSecondsEquivalent, targetMap);

            bool switched = bootstrapper.SwitchMap(targetMap);
            if (!switched)
            {
                return false;
            }

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
    }
}
