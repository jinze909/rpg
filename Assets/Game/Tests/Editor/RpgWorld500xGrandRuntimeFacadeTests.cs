using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xGrandRuntimeFacadeTests
    {
        [Test]
        public void GrandRuntimeFacade_UnifiesAll500xSubsystemsAcrossAllFourMapsWithZeroLeak()
        {
            // Verify both integrity gates pass across the entire 500x world before mounting.
            List<RpgMapValidationIssue> mapIssues = RpgWorld500xMapIntegrityValidator.ValidateAllFourMaps();
            Assert.That(mapIssues, Is.Empty, string.Join("\n", mapIssues));

            RpgWorld500xContentAuditReport contentAudit = RpgWorld500xEnrichedContentAuditor.AuditAllMaps();
            Assert.IsTrue(contentAudit.IsValid, string.Join("\n", contentAudit.Errors));

            var root = new GameObject("World500xGrandRuntimeFacadeTestRoot");
            RpgWorld500xGrandRuntimeFacade facade = null;
            try
            {
                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                var toasts = new List<string>();
                facade = new RpgWorld500xGrandRuntimeFacade(
                    root.transform,
                    RpgWorldMapId.Soundwell,
                    getInt: k => store.TryGetValue(k, out int v) ? v : 0,
                    setInt: (k, v) => store[k] = v,
                    onToast: (t, b) => toasts.Add(t + ":" + b),
                    streamingRadiusChunks: 1,
                    overrideTileSlices: new Sprite[] { null });

                // Step frame advances environment, fog-of-war, and prop spawner cleanly.
                RpgWorld500xEffectiveExplorationModifiers mods = facade.StepFrame(Vector2.right, 0.25f, sprinting: false);
                Assert.IsFalse(string.IsNullOrEmpty(mods.CombinedStatusCaptionZh));
                Assert.Greater(facade.DistrictTracker.VisitedChunkCount, 0);
                Assert.IsFalse(string.IsNullOrEmpty(facade.DistrictTracker.CurrentDistrictId));

                // Top nearby waypoints work on live session position.
                List<RpgWorld500xWaypointGuidance> waypoints = facade.GetTopNearbyWaypoints(5);
                Assert.AreEqual(5, waypoints.Count);

                // Threat codex records DangerZone study and credits Salt-Silver into shared supply service.
                List<RpgWorld500xDangerZoneDossier> dossiers = facade.ThreatCodex.BuildMapDossiers(facade.Session.Host.Layout);
                Assert.Greater(dossiers.Count, 0);
                Assert.IsTrue(facade.ThreatCodex.TryRecordDangerZoneStudied(
                    RpgWorldMapId.Soundwell, dossiers[0].MarkerId, out RpgWorld500xDangerZoneDossier studied));
                Assert.GreaterOrEqual(facade.BountyAndSupply.SaltSilverBalance, studied.Profile.CodexStudySilverBonus);

                // Camp supply purchase spends Salt-Silver from shared balance.
                Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded, facade.BountyAndSupply.TryPurchaseSupply("rations", 1));
                Assert.AreEqual(1, facade.BountyAndSupply.GetOwnedSupplyCount("rations"));

                // Seamless switch across all remaining 3 maps with full subsystem remount.
                Assert.IsTrue(facade.SwitchMap(RpgWorldMapId.RedSaltHarbor));
                Assert.AreEqual(RpgWorldMapId.RedSaltHarbor, facade.CurrentMapId);
                Assert.IsTrue(facade.SwitchMap(RpgWorldMapId.WhitewaterCrossing));
                Assert.AreEqual(RpgWorldMapId.WhitewaterCrossing, facade.CurrentMapId);
                Assert.IsTrue(facade.SwitchMap(RpgWorldMapId.MineCamp));
                Assert.AreEqual(RpgWorldMapId.MineCamp, facade.CurrentMapId);

                facade.Dispose();
                facade.Dispose();
                Assert.IsTrue(facade.IsDisposed);
                Assert.AreEqual(0, root.transform.childCount);
                facade = null;

                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
            }
            finally
            {
                facade?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
