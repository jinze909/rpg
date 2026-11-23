using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xMaintenanceReportTests
    {
        [Test]
        public void Generate_ProducesHealthyFullWorldStudioReportWithExpectedCounts()
        {
            RpgWorld500xStudioHealthSnapshot report = RpgWorld500xMaintenanceReport.Generate();

            Assert.IsTrue(report.IsHealthy, report.FormattedReport);
            Assert.AreEqual(0, report.SpatialIssueCount);
            Assert.AreEqual(0, report.ContentAuditErrorCount);
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
            Assert.IsTrue(report.FormattedReport.Contains("Status: HEALTHY"));
        }
    }
}
