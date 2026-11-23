using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xEnrichedContentAuditorTests
    {
        [Test]
        public void AuditAllMaps_PassesWithZeroErrorsAcrossAll182CuratedWorldEntities()
        {
            RpgWorld500xContentAuditReport report = RpgWorld500xEnrichedContentAuditor.AuditAllMaps();

            Assert.IsTrue(report.IsValid,
                "500x enriched content audit failed:\n" + string.Join("\n", report.Errors));
            Assert.AreEqual(4, report.Summaries.Count);
            Assert.AreEqual(72, report.TotalLandmarks);
            Assert.AreEqual(42, report.TotalCaches);
            Assert.AreEqual(16, report.TotalShortcuts);
            Assert.AreEqual(24, report.TotalFieldNpcs);
            Assert.AreEqual(16, report.TotalStorySpots);
            Assert.AreEqual(12, report.TotalAmbientZones);

            for (int i = 0; i < report.Summaries.Count; i++)
            {
                RpgWorld500xMapContentSummary summary = report.Summaries[i];
                Assert.GreaterOrEqual(summary.TotalInteractiveCount, 35,
                    $"Map {summary.MapId} should maintain rich interactive POI density.");
            }
        }

        [Test]
        public void IsSafeWorldKey_EnforcesWorldNamespaceAndRejectsNarrativeCollisions()
        {
            Assert.IsTrue(RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey("rpg.return.world.cache.sw_c01"));
            Assert.IsTrue(RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey("rpg.return.world.shortcut.mc_sc_01"));
            Assert.IsTrue(RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey("rpg.return.world.rumor.ww_npc_01"));
            Assert.IsTrue(RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey("rpg.return.world.lore.rsh_lore_01"));

            Assert.IsFalse(RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey(""));
            Assert.IsFalse(RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey("rpg.return.m04.fog_focus"));
            Assert.IsFalse(RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey("rpg.return.st03.bathhouse"));
            Assert.IsFalse(RpgWorld500xEnrichedContentAuditor.IsSafeWorldKey("rpg.return.rc03.mine"));
        }
    }
}
