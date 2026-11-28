using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    [TestFixture]
    public sealed class RpgWorld500xFourRegionMaintenanceReportAuditE2ETests
    {
        [Test]
        public void AllFourRegions_LivePortalGraphAndHealthSnapshot_PassStrictDirectedConnectivityAndInventoryParity()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count, "Expected exactly 4 regions in 500x world scale.");

            var allMaps = new List<RpgWorldMapId>(specs.Count);
            var allRoutes = new List<RpgWorld500xRegionRouteDef>();
            long expectedTiles = 0;
            int expectedDistricts = 0;
            int expectedBounties = 0;
            int expectedThreats = 0;

            foreach (RpgExpandedMapSpec spec in specs)
            {
                allMaps.Add(spec.MapId);
                expectedTiles += (long)spec.ExpandedWidth * spec.ExpandedHeight;
                expectedDistricts += RpgWorld500xDistrictCatalog.GetDistricts(spec.MapId).Count;
                expectedBounties += RpgWorld500xBountyAndSupplyCatalog.GetBounties(spec.MapId).Count;
                expectedThreats += RpgWorld500xBestiaryCatalog.GetProfiles(spec.MapId).Count;

                List<RpgWorld500xRegionRouteDef> routes = RpgWorld500xWorldAtlasPortalService.GetConnectedRegions(spec.MapId);
                Assert.Greater(routes.Count, 0, $"Region {spec.MapId} must expose at least one outgoing portal route.");
                allRoutes.AddRange(routes);
            }

            Assert.IsTrue(
                RpgWorld500xMaintenanceReport.AreRegionRoutesStronglyConnected(allMaps, allRoutes),
                "Live 4-region portal graph must be strongly connected.");

            RpgWorld500xStudioHealthSnapshot snapshot = RpgWorld500xMaintenanceReport.Generate();
            Assert.IsTrue(snapshot.IsHealthy, snapshot.FormattedReport);
            Assert.AreEqual(0, snapshot.SpatialIssueCount);
            Assert.AreEqual(0, snapshot.ContentAuditErrorCount);
            Assert.AreEqual(0, snapshot.MetaSystemAuditErrorCount);
            Assert.AreEqual(expectedTiles, snapshot.TotalTiles);
            Assert.AreEqual(expectedDistricts, snapshot.TotalDistricts);
            Assert.AreEqual(expectedBounties, snapshot.TotalBounties);
            Assert.AreEqual(expectedThreats, snapshot.TotalThreatProfiles);
            Assert.AreEqual(allRoutes.Count, snapshot.TotalRegionRoutes);
            Assert.AreEqual(RpgWorld500xSaveSlotManager.AllSlots.Count, snapshot.TotalSaveSlots);
            Assert.AreEqual(RpgWorld500xExpeditionMilestoneService.AllMilestones.Count, snapshot.TotalExpeditionMilestones);
            Assert.AreEqual(RpgWorld500xBountyAndSupplyCatalog.GetCampSupplies().Count, snapshot.TotalCampSupplies);
            StringAssert.Contains("Status: HEALTHY", snapshot.FormattedReport);
            StringAssert.Contains("spatial=0, content=0, meta=0", snapshot.FormattedReport);
        }

        [Test]
        public void AllFourRegions_DirectedConnectivityGuard_RejectsNullEmptyDuplicatesAndBrokenReturnCycle()
        {
            var fourMaps = new[]
            {
                RpgWorldMapId.Soundwell,
                RpgWorldMapId.RedSaltHarbor,
                RpgWorldMapId.WhitewaterCrossing,
                RpgWorldMapId.MineCamp
            };

            var emptyRoutes = new List<RpgWorld500xRegionRouteDef>();
            Assert.IsFalse(RpgWorld500xMaintenanceReport.AreRegionRoutesStronglyConnected(null, emptyRoutes));
            Assert.IsFalse(RpgWorld500xMaintenanceReport.AreRegionRoutesStronglyConnected(fourMaps, null));
            Assert.IsFalse(RpgWorld500xMaintenanceReport.AreRegionRoutesStronglyConnected(new RpgWorldMapId[0], emptyRoutes));
            Assert.IsFalse(RpgWorld500xMaintenanceReport.AreRegionRoutesStronglyConnected(
                new[] { RpgWorldMapId.Soundwell, RpgWorldMapId.Soundwell }, emptyRoutes));

            var chainRoutes = new List<RpgWorld500xRegionRouteDef>
            {
                new RpgWorld500xRegionRouteDef("r1", RpgWorldMapId.Soundwell, RpgWorldMapId.RedSaltHarbor, "声井至赤盐港", 30, ""),
                new RpgWorld500xRegionRouteDef("r2", RpgWorldMapId.RedSaltHarbor, RpgWorldMapId.WhitewaterCrossing, "赤盐港至白水渡", 30, ""),
                new RpgWorld500xRegionRouteDef("r3", RpgWorldMapId.WhitewaterCrossing, RpgWorldMapId.MineCamp, "白水渡至矿营", 30, "")
            };

            Assert.IsFalse(
                RpgWorld500xMaintenanceReport.AreRegionRoutesStronglyConnected(fourMaps, chainRoutes),
                "Open 4-region chain without return edge must fail strong connectivity.");

            chainRoutes.Add(new RpgWorld500xRegionRouteDef("r4", RpgWorldMapId.MineCamp, RpgWorldMapId.Soundwell, "矿营回声井", 30, ""));
            Assert.IsTrue(
                RpgWorld500xMaintenanceReport.AreRegionRoutesStronglyConnected(fourMaps, chainRoutes),
                "Closed 4-region directed cycle must pass strong connectivity.");
        }
    }
}
