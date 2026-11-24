using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xWorldAtlasPortalTests
    {
        [Test]
        public void RegionGraph_IsStronglyConnectedAndAtlasCoversAllFourMaps()
        {
            // BFS from Soundwell must reach all 4 maps via direct routes.
            var visited = new HashSet<RpgWorldMapId> { RpgWorldMapId.Soundwell };
            var queue = new Queue<RpgWorldMapId>();
            queue.Enqueue(RpgWorldMapId.Soundwell);

            while (queue.Count > 0)
            {
                RpgWorldMapId current = queue.Dequeue();
                List<RpgWorld500xRegionRouteDef> routes = RpgWorld500xWorldAtlasPortalService.GetConnectedRegions(current);
                Assert.Greater(routes.Count, 0);
                for (int i = 0; i < routes.Count; i++)
                {
                    Assert.Greater(routes[i].TravelMinutesInGame, 0);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(routes[i].RouteNameZh));
                    if (visited.Add(routes[i].ToMap))
                    {
                        queue.Enqueue(routes[i].ToMap);
                    }
                }
            }

            Assert.AreEqual(4, visited.Count);

            var store = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [RpgWorld500xRegionalReputationService.ReputationKey(RpgWorldMapId.RedSaltHarbor)] = 420
            };
            List<RpgWorld500xRegionAtlasEntry> atlas = RpgWorld500xWorldAtlasPortalService.BuildWorldAtlasOverview(store);
            Assert.AreEqual(4, atlas.Count);
        }

        [Test]
        public void TryTravelToConnectedRegion_RejectsActiveDangerWithoutChangingMapOrClock()
        {
            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                var facade = boot.Driver.Facade;
                var host = facade.Session.Host;
                RpgMapLandmarkMarker danger = default;
                bool found = false;
                foreach (RpgMapLandmarkMarker marker in host.Layout.Markers)
                {
                    if (marker.Category == RpgMapMarkerCategory.DangerZone)
                    {
                        danger = marker;
                        found = true;
                        break;
                    }
                }

                Assert.IsTrue(found);
                Assert.IsTrue(host.Bridge.TryTriggerDangerZoneEncounter(
                    danger.Id, new Vector2(danger.Tile.x + 0.5f, danger.Tile.y + 0.5f)));
                float minutes = facade.Atmosphere.TimeOfDayMinutes;
                int day = facade.Atmosphere.DayCount;
                Vector2 position = host.PlayerPosition;

                Assert.IsFalse(RpgWorld500xWorldAtlasPortalService.TryTravelToConnectedRegion(
                    boot, RpgWorldMapId.RedSaltHarbor, out RpgWorld500xRegionTravelResult blocked));
                Assert.IsFalse(blocked.Succeeded);
                Assert.AreEqual(RpgWorldMapId.Soundwell, facade.CurrentMapId);
                Assert.AreSame(host, facade.Session.Host);
                Assert.AreEqual(position, host.PlayerPosition);
                Assert.AreEqual(danger.Id, host.Bridge.ActiveEncounterMarkerId);
                Assert.AreEqual(minutes, facade.Atmosphere.TimeOfDayMinutes);
                Assert.AreEqual(day, facade.Atmosphere.DayCount);
            }
        }

        [Test]
        public void TryTravelToConnectedRegion_AdvancesClockSwitchesMapAndRejectsNonAdjacentHop()
        {
            using (var boot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                // Soundwell -> MineCamp is not directly adjacent; must be rejected safely.
                Assert.IsFalse(RpgWorld500xWorldAtlasPortalService.TryTravelToConnectedRegion(
                    boot, RpgWorldMapId.MineCamp, out _));
                Assert.AreEqual(RpgWorldMapId.Soundwell, boot.Driver.Facade.CurrentMapId);

                float minutesBefore = boot.Driver.Facade.Atmosphere.TimeOfDayMinutes;
                Assert.IsTrue(RpgWorld500xWorldAtlasPortalService.TryTravelToConnectedRegion(
                    boot, RpgWorldMapId.RedSaltHarbor, out RpgWorld500xRegionTravelResult travel));

                Assert.IsTrue(travel.Succeeded);
                Assert.AreEqual(RpgWorldMapId.Soundwell, travel.FromMap);
                Assert.AreEqual(RpgWorldMapId.RedSaltHarbor, travel.ToMap);
                Assert.AreEqual(120, travel.TravelMinutesElapsed);
                Assert.AreEqual(RpgWorldMapId.RedSaltHarbor, boot.Driver.Facade.CurrentMapId);
                Assert.AreEqual((minutesBefore + 120f) % 1440f, boot.Driver.Facade.Atmosphere.TimeOfDayMinutes, 0.1f);
                Assert.IsTrue(travel.ArrivalBannerZh.Contains("已沿"));
            }
        }
    }
}
