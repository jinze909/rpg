using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xMaintenanceReportAndPortalGraphE2ETests
    {
        [Test]
        public void LiveFourRegionPortalGraph_IsStronglyConnectedMatchesContentAuditAndFailsWhenDisconnected()
        {
            RpgWorld500xStudioHealthSnapshot report = RpgWorld500xMaintenanceReport.Generate();
            Assert.IsNotNull(report);

            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            var allMaps = new List<RpgWorldMapId>(specs.Count);
            var allRoutes = new List<RpgWorld500xRegionRouteDef>();
            var knownMaps = new HashSet<RpgWorldMapId>();

            for (int i = 0; i < specs.Count; i++)
            {
                allMaps.Add(specs[i].MapId);
                knownMaps.Add(specs[i].MapId);
            }

            for (int i = 0; i < specs.Count; i++)
            {
                RpgWorldMapId fromMap = specs[i].MapId;
                List<RpgWorld500xRegionRouteDef> routes = RpgWorld500xWorldAtlasPortalService.GetConnectedRegions(fromMap);
                Assert.IsNotNull(routes);
                Assert.Greater(routes.Count, 0, "Each 500x region must expose at least one outbound route.");

                for (int r = 0; r < routes.Count; r++)
                {
                    RpgWorld500xRegionRouteDef route = routes[r];
                    Assert.AreEqual(fromMap, route.FromMap);
                    Assert.IsTrue(knownMaps.Contains(route.ToMap));
                    Assert.AreNotEqual(route.FromMap, route.ToMap);
                    Assert.Greater(route.TravelMinutesInGame, 0);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(route.RouteId));
                    Assert.IsFalse(string.IsNullOrWhiteSpace(route.RouteNameZh));
                    allRoutes.Add(route);
                }
            }

            Assert.IsTrue(RpgWorld500xMaintenanceReport.AreRegionRoutesStronglyConnected(allMaps, allRoutes));

            // Removing all outbound routes from any single region must break strong connectivity.
            for (int i = 0; i < allMaps.Count; i++)
            {
                RpgWorldMapId isolated = allMaps[i];
                var pruned = new List<RpgWorld500xRegionRouteDef>(allRoutes.Count);
                for (int r = 0; r < allRoutes.Count; r++)
                {
                    if (allRoutes[r].FromMap != isolated)
                    {
                        pruned.Add(allRoutes[r]);
                    }
                }

                Assert.IsFalse(
                    RpgWorld500xMaintenanceReport.AreRegionRoutesStronglyConnected(allMaps, pruned),
                    "Isolating outbound edges from " + isolated + " must fail strong connectivity.");
            }

            RpgWorld500xContentAuditReport audit = RpgWorld500xEnrichedContentAuditor.AuditAllMaps();
            Assert.IsTrue(audit.IsValid, string.Join("\n", audit.Errors));
            Assert.AreEqual(specs.Count, audit.Summaries.Count);
        }
    }
}
