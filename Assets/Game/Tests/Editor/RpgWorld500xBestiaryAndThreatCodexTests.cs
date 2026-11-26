using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xBestiaryAndThreatCodexTests
    {
        [Test]
        public void All12ThreatProfilesAndEveryDangerZoneAcrossAllFourMaps_HaveValidDossiers()
        {
            var profileIds = new HashSet<string>(StringComparer.Ordinal);
            var codex = new RpgWorld500xThreatCodexService();
            int totalProfiles = 0;
            int totalDangerZones = 0;

            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                IReadOnlyList<RpgWorld500xThreatProfileDef> profiles = RpgWorld500xBestiaryCatalog.GetProfiles(spec.MapId);
                Assert.AreEqual(3, profiles.Count, spec.DisplayNameEn);
                totalProfiles += profiles.Count;

                for (int i = 0; i < profiles.Count; i++)
                {
                    RpgWorld500xThreatProfileDef p = profiles[i];
                    Assert.IsTrue(profileIds.Add(p.ProfileId), p.ProfileId);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(p.EnemyNameZh), p.ProfileId);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(p.WeaknessHintZh), p.ProfileId);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(p.CounterplayNoteZh), p.ProfileId);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(p.RecommendedSupplyId), p.ProfileId);
                    Assert.Greater(p.CodexStudySilverBonus, 0, p.ProfileId);
                }

                RpgWorld500xMapLayout map = RpgWorld500xMapLayout.Build(spec.MapId);
                List<RpgWorld500xDangerZoneDossier> dossiers = codex.BuildMapDossiers(map);
                Assert.Greater(dossiers.Count, 0, spec.DisplayNameEn + " must have DangerZone dossiers.");
                totalDangerZones += dossiers.Count;

                for (int i = 0; i < dossiers.Count; i++)
                {
                    RpgWorld500xDangerZoneDossier d = dossiers[i];
                    Assert.IsTrue(d.CodexMakinomKey.StartsWith("rpg.return.world.codex.threat.", StringComparison.Ordinal));
                    Assert.AreEqual(spec.MapId, d.Profile.MapId);
                }
            }

            Assert.AreEqual(12, totalProfiles);
            Assert.GreaterOrEqual(totalDangerZones, 8);
        }

        [Test]
        public void TryRecordDangerZoneStudied_UnlocksIdempotentlyAndPersistsInWorldNamespace()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            int totalBonus = 0;
            var codex = new RpgWorld500xThreatCodexService(
                k => store.TryGetValue(k, out int v) ? v : 0,
                (k, v) => store[k] = v,
                d => totalBonus += d.Profile.CodexStudySilverBonus);

            RpgWorld500xMapLayout soundwell = RpgWorld500xMapLayout.Build(RpgWorldMapId.Soundwell);
            List<RpgWorld500xDangerZoneDossier> dossiers = codex.BuildMapDossiers(soundwell);
            string markerId = dossiers[0].MarkerId;

            Assert.IsFalse(codex.IsStudied(markerId));
            Assert.IsTrue(codex.TryRecordDangerZoneStudied(RpgWorldMapId.Soundwell, markerId, out RpgWorld500xDangerZoneDossier first));
            Assert.IsTrue(first.IsStudied);
            Assert.IsTrue(codex.IsStudied(markerId));
            Assert.AreEqual(first.Profile.CodexStudySilverBonus, totalBonus);

            // Second call is idempotent
            Assert.IsFalse(codex.TryRecordDangerZoneStudied(RpgWorldMapId.Soundwell, markerId, out _));
            Assert.AreEqual(first.Profile.CodexStudySilverBonus, totalBonus);
            Assert.AreEqual(1, store[RpgWorld500xThreatCodexService.CodexKey(markerId)]);
        }

        [Test]
        public void BuildMapCodexSummaryAndFilterDossiers_AreReadOnlyAndTrackStudyProgress()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            var codex = new RpgWorld500xThreatCodexService(
                k => store.TryGetValue(k, out int v) ? v : 0,
                (k, v) => store[k] = v);

            RpgWorld500xMapCodexSummary nullSummary = codex.BuildMapCodexSummary(null);
            Assert.AreEqual(0, nullSummary.TotalDossiers);
            Assert.AreEqual(0, codex.FilterDossiers(null).Count);

            RpgWorld500xMapLayout soundwell = RpgWorld500xMapLayout.Build(RpgWorldMapId.Soundwell);
            List<RpgWorld500xDangerZoneDossier> all = codex.BuildMapDossiers(soundwell);
            RpgWorld500xMapCodexSummary before = codex.BuildMapCodexSummary(soundwell);
            Assert.AreEqual(RpgWorldMapId.Soundwell, before.MapId);
            Assert.AreEqual(all.Count, before.TotalDossiers);
            Assert.AreEqual(0, before.StudiedCount);
            Assert.AreEqual(all.Count, before.UnstudiedCount);
            Assert.Greater(before.RemainingStudySilverBonus, 0);
            Assert.Greater(before.RecommendedSupplyIds.Count, 0);
            StringAssert.Contains("【图鉴考据】", before.SummaryZh);
            // Strictly read-only: no writes performed.
            Assert.AreEqual(0, store.Count);

            // Filter by tier matching first dossier.
            RpgWorld500xThreatTier targetTier = all[0].Profile.Tier;
            List<RpgWorld500xDangerZoneDossier> tierMatches = codex.FilterDossiers(soundwell, unstudiedOnly: true, tierFilter: targetTier);
            Assert.Greater(tierMatches.Count, 0);
            for (int i = 0; i < tierMatches.Count; i++)
            {
                Assert.AreEqual(targetTier, tierMatches[i].Profile.Tier);
                Assert.IsFalse(tierMatches[i].IsStudied);
            }

            // Study first dossier -> unstudied count and remaining silver drop accurately.
            Assert.IsTrue(codex.TryRecordDangerZoneStudied(RpgWorldMapId.Soundwell, all[0].MarkerId, out RpgWorld500xDangerZoneDossier studiedFirst));
            RpgWorld500xMapCodexSummary afterOne = codex.BuildMapCodexSummary(soundwell);
            Assert.AreEqual(1, afterOne.StudiedCount);
            Assert.AreEqual(all.Count - 1, afterOne.UnstudiedCount);
            Assert.AreEqual(before.RemainingStudySilverBonus - studiedFirst.Profile.CodexStudySilverBonus, afterOne.RemainingStudySilverBonus);
            Assert.AreEqual(all.Count - 1, codex.FilterDossiers(soundwell, unstudiedOnly: true).Count);

            // Study remaining dossiers -> complete summary banner, 0 remaining silver.
            for (int i = 1; i < all.Count; i++)
            {
                codex.TryRecordDangerZoneStudied(RpgWorldMapId.Soundwell, all[i].MarkerId, out _);
            }

            RpgWorld500xMapCodexSummary completed = codex.BuildMapCodexSummary(soundwell);
            Assert.AreEqual(all.Count, completed.StudiedCount);
            Assert.AreEqual(0, completed.UnstudiedCount);
            Assert.AreEqual(0, completed.RemainingStudySilverBonus);
            Assert.AreEqual(0, completed.RecommendedSupplyIds.Count);
            StringAssert.Contains("已全部收录", completed.SummaryZh);
        }
    }
}
