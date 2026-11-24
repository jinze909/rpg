using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xMaintenanceReportTests
    {
        [Test]
        public void DirectedRegionGraph_RejectsOneWayPathsEvenWhenSoundwellReachesEveryMap()
        {
            RpgWorldMapId[] maps =
            {
                RpgWorldMapId.Soundwell,
                RpgWorldMapId.RedSaltHarbor,
                RpgWorldMapId.MineCamp
            };
            var routes = new List<RpgWorld500xRegionRouteDef>
            {
                new RpgWorld500xRegionRouteDef("sw_to_harbor",
                    RpgWorldMapId.Soundwell, RpgWorldMapId.RedSaltHarbor, "去港口", 30, ""),
                new RpgWorld500xRegionRouteDef("harbor_to_mine",
                    RpgWorldMapId.RedSaltHarbor, RpgWorldMapId.MineCamp, "去矿区", 30, "")
            };

            Assert.IsFalse(RpgWorld500xMaintenanceReport.AreRegionRoutesStronglyConnected(maps, routes),
                "Soundwell can reach all regions but neither destination can return; this is not strong connectivity.");

            routes.Add(new RpgWorld500xRegionRouteDef("mine_to_harbor",
                RpgWorldMapId.MineCamp, RpgWorldMapId.RedSaltHarbor, "回港口", 30, ""));
            routes.Add(new RpgWorld500xRegionRouteDef("harbor_to_sw",
                RpgWorldMapId.RedSaltHarbor, RpgWorldMapId.Soundwell, "回声井", 30, ""));
            Assert.IsTrue(RpgWorld500xMaintenanceReport.AreRegionRoutesStronglyConnected(maps, routes));
        }

        [Test]
        public void Generate_ProducesHealthyFullWorldStudioReportWithExpectedCounts()
        {
            RpgWorld500xStudioHealthSnapshot report = RpgWorld500xMaintenanceReport.Generate();

            Assert.IsTrue(report.IsHealthy, report.FormattedReport);
            Assert.AreEqual(0, report.SpatialIssueCount);
            Assert.AreEqual(0, report.ContentAuditErrorCount);
            Assert.AreEqual(0, report.MetaSystemAuditErrorCount);
            Assert.AreEqual(13667328L, report.TotalTiles);
            Assert.AreEqual(13347, report.TotalChunks);
            Assert.AreEqual(72, report.TotalLandmarks);
            Assert.AreEqual(42, report.TotalCaches);
            Assert.AreEqual(16, report.TotalShortcuts);
            Assert.AreEqual(24, report.TotalFieldNpcs);
            Assert.AreEqual(16, report.TotalStorySpots);
            Assert.AreEqual(12, report.TotalAmbientZones);
            Assert.AreEqual(15, report.TotalDistricts);
            Assert.AreEqual(12, report.TotalBounties);
            Assert.AreEqual(6, report.TotalCampSupplies);
            Assert.AreEqual(12, report.TotalThreatProfiles);
            Assert.AreEqual(8, report.TotalRegionRoutes);
            Assert.AreEqual(4, report.TotalSaveSlots);
            Assert.AreEqual(8, report.TotalExpeditionMilestones);
            Assert.IsTrue(report.FormattedReport.Contains("Status: HEALTHY"));
            Assert.IsTrue(report.FormattedReport.Contains("meta=0"));
        }
    }
}
