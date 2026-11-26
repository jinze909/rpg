using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xMaintenanceGateAndContentAuditE2ETests
    {
        [Test]
        public void EnrichedContentAuditGlobalLedgerDangerSummaryAndPlayabilityGate_CrossCheckCleanlyWithZeroLeak()
        {
            // Warm up one-time UGUI/font caches before steady-state leak baseline.
            RpgWorld500xPlayabilityGateReport warmup = RpgWorld500xPlayabilityMaintenanceGate.RunFullAudit();
            Assert.IsTrue(warmup.Passed, string.Join("\n", warmup.Issues));

            int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

            RpgWorld500xContentAuditReport contentReport = RpgWorld500xEnrichedContentAuditor.AuditAllMaps();
            Assert.IsTrue(contentReport.IsValid, string.Join("\n", contentReport.Errors));
            Assert.AreEqual(4, contentReport.Summaries.Count);
            Assert.AreEqual(72, contentReport.TotalLandmarks);
            Assert.AreEqual(42, contentReport.TotalCaches);
            Assert.AreEqual(16, contentReport.TotalShortcuts);
            Assert.AreEqual(24, contentReport.TotalFieldNpcs);
            Assert.AreEqual(16, contentReport.TotalStorySpots);
            Assert.AreEqual(12, contentReport.TotalAmbientZones);

            RpgWorld500xGlobalProgress ledger = RpgWorld500xLedgerSummary.Capture(_ => 0);
            Assert.AreEqual(contentReport.Summaries.Count, ledger.PerMap.Count);
            Assert.AreEqual(contentReport.TotalLandmarks, ledger.TotalMarkers);

            for (int i = 0; i < contentReport.Summaries.Count; i++)
            {
                RpgWorld500xMapContentSummary mapContent = contentReport.Summaries[i];
                RpgWorld500xMapProgress mapProgress = ledger.PerMap[i];
                Assert.AreEqual(mapContent.MapId, mapProgress.MapId);
                Assert.AreEqual(mapContent.LandmarkCount, mapProgress.TotalMarkers);
                Assert.GreaterOrEqual(mapContent.TotalInteractiveCount, 35);

                RpgWorld500xMapLayout layout = RpgWorld500xMapLayout.Build(mapContent.MapId);
                RpgWorld500xMapFirstClearSummary dangerSummary =
                    RpgWorld500xDangerVictoryRewardService.SummarizeRemainingFirstClearRewards(layout, _ => 0);
                Assert.AreEqual(mapProgress.TotalDangerZones, dangerSummary.TotalDangerZones);
                Assert.AreEqual(mapProgress.TotalDangerZones, dangerSummary.RemainingDangerZones);
            }

            RpgWorld500xPlayabilityGateReport gateReport = RpgWorld500xPlayabilityMaintenanceGate.RunFullAudit();
            Assert.IsTrue(gateReport.Passed, string.Join("\n", gateReport.Issues));
            Assert.Greater(gateReport.TotalChecks, 123);
            Assert.AreEqual(gateReport.TotalChecks, gateReport.PassedChecks);
            Assert.AreEqual(0, gateReport.Issues.Count);
            StringAssert.Contains("通过", gateReport.FormattedReportZh);

            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
        }
    }
}
