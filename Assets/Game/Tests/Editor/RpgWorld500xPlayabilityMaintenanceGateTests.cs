using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xPlayabilityMaintenanceGateTests
    {
        [Test]
        public void PlayabilityMaintenanceGate_PassesAllFour500xRegionsWithZeroLeaks()
        {
            // Warm up one-time UGUI/font caches before steady-state leak baseline.
            RpgWorld500xPlayabilityGateReport warmup = RpgWorld500xPlayabilityMaintenanceGate.RunFullAudit();
            Assert.IsTrue(warmup.Passed, string.Join("\n", warmup.Issues));

            int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

            RpgWorld500xPlayabilityGateReport report = RpgWorld500xPlayabilityMaintenanceGate.RunFullAudit();
            Assert.IsTrue(report.Passed, string.Join("\n", report.Issues));
            Assert.Greater(report.TotalChecks, 123);
            Assert.AreEqual(report.TotalChecks, report.PassedChecks);
            Assert.AreEqual(0, report.Issues.Count);
            Assert.IsTrue(report.FormattedReportZh.Contains("通过"));

            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
        }
    }
}
