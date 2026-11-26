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

        [Test]
        public void OpenJournalBlocksUnifiedWorldInteractionsUntilClosed()
        {
            var root = new GameObject("World500x journal-modal interaction test");
            RpgWorld500xGrandRuntimeFacade facade = null;
            try
            {
                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                var toasts = new List<string>();
                facade = new RpgWorld500xGrandRuntimeFacade(
                    root.transform,
                    RpgWorldMapId.Soundwell,
                    getInt: key => store.TryGetValue(key, out int value) ? value : 0,
                    setInt: (key, value) => store[key] = value,
                    onToast: (title, body) => toasts.Add(title + ":" + body),
                    streamingRadiusChunks: 1,
                    overrideTileSlices: new Sprite[] { null });

                int storedFacts = store.Count;
                int toastCount = toasts.Count;
                int balanceBefore = facade.BountyAndSupply.SaltSilverBalance;
                int interactionsBefore = facade.ActivityDirector.InteractionCount;

                Assert.IsTrue(facade.Session.ToggleJournal());
                Assert.IsTrue(facade.Session.JournalPanel.IsVisible);
                Assert.IsFalse(facade.TryInteractUnified(100000f,
                    out RpgWorld500xInteractionResult blockedActivity,
                    out RpgWorldInteractOutcome blockedSession),
                    "World interactions must not fire through an open journal.");
                Assert.AreEqual(RpgWorld500xInteractionKind.None, blockedActivity.Kind);
                Assert.AreEqual(RpgWorldInteractOutcome.None, blockedSession);
                Assert.AreEqual(interactionsBefore, facade.ActivityDirector.InteractionCount);
                Assert.AreEqual(balanceBefore, facade.BountyAndSupply.SaltSilverBalance);
                Assert.AreEqual(storedFacts, store.Count);
                Assert.AreEqual(toastCount, toasts.Count);

                Assert.IsTrue(facade.Session.ToggleJournal());
                Assert.IsFalse(facade.Session.JournalPanel.IsVisible);
                Assert.IsTrue(facade.TryInteractUnified(100000f,
                    out RpgWorld500xInteractionResult resumedActivity,
                    out RpgWorldInteractOutcome resumedSession),
                    "Closing the journal must restore nearby world interactions.");
                Assert.IsTrue(resumedActivity.Succeeded);
                Assert.AreEqual(RpgWorldInteractOutcome.None, resumedSession);
                Assert.AreEqual(interactionsBefore + 1, facade.ActivityDirector.InteractionCount);
            }
            finally
            {
                facade?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RejectedOrSameMapSwitchKeepsLiveSubsystemsAndExplorationPlayable()
        {
            var root = new GameObject("World500x map-switch guard test");
            RpgWorld500xGrandRuntimeFacade facade = null;
            try
            {
                facade = new RpgWorld500xGrandRuntimeFacade(
                    root.transform,
                    RpgWorldMapId.Soundwell,
                    streamingRadiusChunks: 1,
                    overrideTileSlices: new Sprite[] { null });

                var originalHost = facade.Session.Host;
                var originalActivity = facade.ActivityDirector;
                var originalProps = facade.PropSpawner;
                var originalDistrict = facade.DistrictTracker;
                Vector2 originalPosition = facade.Session.Host.PlayerPosition;

                Assert.IsFalse(facade.SwitchMap((RpgWorldMapId)9999),
                    "An invalid serialized map ID must be rejected without unmounting the world.");
                Assert.IsTrue(facade.SwitchMap(RpgWorldMapId.Soundwell),
                    "Re-selecting the active map is a successful no-op.");
                Assert.AreSame(originalHost, facade.Session.Host);
                Assert.AreSame(originalActivity, facade.ActivityDirector);
                Assert.AreSame(originalProps, facade.PropSpawner);
                Assert.AreSame(originalDistrict, facade.DistrictTracker);
                Assert.AreEqual(originalPosition, facade.Session.Host.PlayerPosition);
                Assert.IsFalse(facade.IsDisposed);

                Assert.DoesNotThrow(() => facade.StepFrame(Vector2.zero, 0.25f),
                    "Exploration must remain usable after a rejected map switch.");
                Assert.DoesNotThrow(() => facade.GetCompletionSummary(),
                    "The district tracker must still be mounted after rejection.");
            }
            finally
            {
                facade?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
