using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionThreatCodexGuardE2ETests
    {
        [Test]
        public void AllFourRegions_ThreatCodexEnforcesDangerValidationReadOnlyGuardAndIdempotentStudy()
        {
            RpgWorldMapId[] regions =
            {
                RpgWorldMapId.Soundwell,
                RpgWorldMapId.RedSaltHarbor,
                RpgWorldMapId.WhitewaterCrossing,
                RpgWorldMapId.MineCamp
            };

            int verifiedRegions = 0;
            for (int r = 0; r < regions.Length; r++)
            {
                RpgWorldMapId mapId = regions[r];
                RpgWorldMapId otherMapId = regions[(r + 1) % regions.Length];

                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
                int rewards = 0;

                var codex = new RpgWorld500xThreatCodexService(
                    read, (k, v) => store[k] = v, _ => rewards++);
                var readOnlyCodex = new RpgWorld500xThreatCodexService(
                    read, onDossierStudied: _ => rewards++);

                var map = RpgWorld500xMapLayout.Build(mapId);
                var dossiers = codex.BuildMapDossiers(map);
                Assert.IsNotEmpty(dossiers, "Expected danger dossiers in " + mapId);

                string ordinaryMarker = null;
                for (int i = 0; i < map.Markers.Count; i++)
                {
                    if (map.Markers[i].Category != RpgMapMarkerCategory.DangerZone)
                    {
                        ordinaryMarker = map.Markers[i].Id;
                        break;
                    }
                }

                Assert.IsNotNull(ordinaryMarker, "Expected non-danger marker in " + mapId);
                var firstDanger = dossiers[0];

                // Reject non-danger markers, unknown IDs, cross-region mismatches, invalid enums, and read-only instances.
                Assert.IsFalse(codex.TryRecordDangerZoneStudied(mapId, ordinaryMarker, out _));
                Assert.IsFalse(codex.TryRecordDangerZoneStudied(mapId, "not-a-real-marker", out _));
                Assert.IsFalse(codex.TryRecordDangerZoneStudied(otherMapId, firstDanger.MarkerId, out _));
                Assert.IsFalse(codex.TryRecordDangerZoneStudied((RpgWorldMapId)255, firstDanger.MarkerId, out _));
                Assert.IsFalse(readOnlyCodex.TryRecordDangerZoneStudied(mapId, firstDanger.MarkerId, out _));
                Assert.AreEqual(0, rewards);
                Assert.AreEqual(0, store.Count);

                // Study every real danger zone in the region idempotently.
                for (int i = 0; i < dossiers.Count; i++)
                {
                    var d = dossiers[i];
                    Assert.IsTrue(codex.TryRecordDangerZoneStudied(mapId, d.MarkerId, out var studied));
                    Assert.IsTrue(studied.IsStudied);
                    Assert.IsFalse(codex.TryRecordDangerZoneStudied(mapId, d.MarkerId, out _));
                    Assert.AreEqual(1, store[d.CodexMakinomKey]);
                }

                Assert.AreEqual(dossiers.Count, rewards);
                Assert.AreEqual(dossiers.Count, store.Count);
                verifiedRegions++;
            }

            Assert.AreEqual(4, verifiedRegions);
        }
    }
}
