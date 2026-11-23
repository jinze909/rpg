using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xExplorationHostTests
    {
        private GameObject root;
        private RpgWorld500xExplorationHost host;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("World500xExplorationHostTestRoot");
        }

        [TearDown]
        public void TearDown()
        {
            host?.Dispose();
            host = null;
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void UnifiedHostStreams500xSoundwellRunsMovementSprintCampDangerEncounterAndFastTravel()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            int campRecovers = 0;
            int questRewards = 0;

            host = new RpgWorld500xExplorationHost(
                root.transform,
                RpgWorldMapId.Soundwell,
                getInt: k => store.TryGetValue(k, out int v) ? v : 0,
                setInt: (k, v) => store[k] = v,
                onPartyRecover: _ => campRecovers++,
                onRewardGranted: (_, __) => questRewards++,
                streamingRadiusChunks: 1,
                tileSlices: new Sprite[] { null });

            Assert.That(host.ChunkRenderer.IsStreaming, Is.True);
            Assert.That(host.ChunkRenderer.ChunkCount, Is.LessThanOrEqualTo(9));
            Assert.That(host.Tracker.IsMarkerDiscovered("sw.entry-shaft"), Is.True);
            Assert.That(host.CurrentDistrict.HasValue, Is.True);
            Assert.That(host.CurrentDistrict.Value.Id, Is.EqualTo("soundwell.entry"));

            // Sprint moves faster than walk and clamps camera cleanly.
            Vector2 p0 = host.PlayerPosition;
            Assert.That(host.StepMovement(Vector2.right, 0.25f, sprinting: false), Is.True);
            float walkDist = host.PlayerPosition.x - p0.x;
            Vector2 p1 = host.PlayerPosition;
            Assert.That(host.StepMovement(Vector2.right, 0.25f, sprinting: true), Is.True);
            float sprintDist = host.PlayerPosition.x - p1.x;
            Assert.That(sprintDist, Is.GreaterThan(walkDist * 1.5f));

            Vector3 cam = host.StepCamera(new Vector3(-100f, -100f, -10f), 1f);
            Assert.That(cam.x, Is.GreaterThanOrEqualTo(RpgWorldMapScale.LogicalScreenTilesX * 0.5f));
            Assert.That(cam.z, Is.EqualTo(-10f));

            // Non-finite movement fails closed.
            Assert.That(host.StepMovement(new Vector2(float.NaN, 0f), 0.1f), Is.False);

            // Travel to camp, rest, and recover party.
            Assert.That(host.TryPlaceAtWalkableTile(new Vector2Int(1180, 1120)), Is.True);
            Assert.That(host.TryRestAtNearbyCamp(out RpgWorldCampRestResult rest), Is.True);
            Assert.That(rest.PartyRecovered, Is.True);
            Assert.That(campRecovers, Is.EqualTo(1));

            // Enter DangerZone, trigger encounter (locks movement & fast travel), resolve victory & claim reward.
            Assert.That(host.TryPlaceAtWalkableTile(new Vector2Int(680, 640)), Is.True);
            Assert.That(host.TryTriggerNearbyDangerEncounter(out string dangerId), Is.True);
            Assert.That(dangerId, Is.EqualTo("sw.sunken-drain"));
            Assert.That(host.StepMovement(Vector2.right, 0.1f), Is.False);
            Assert.That(host.TryFastTravelToBeacon("sw.entry-shaft", out _), Is.False);

            Assert.That(host.TryResolveActiveDangerVictory(lockBreakAchieved: true,
                out RpgDangerZoneEncounterResult victory), Is.True);
            Assert.That(victory.CompletedQuestId, Is.EqualTo("q.sw.clear-drain-grate"));
            Assert.That(questRewards, Is.EqualTo(1));

            // Fast-travel back across 450+ tiles to discovered entry beacon and stream cleanly.
            Assert.That(host.TryFastTravelToBeacon("sw.entry-shaft", out RpgFastTravelResult ft), Is.True);
            Assert.That(ft.DestinationTile, Is.EqualTo(new Vector2Int(224, 672)));
            Assert.That(host.CurrentTile, Is.EqualTo(new Vector2Int(224, 672)));
            Assert.That(host.ChunkRenderer.ChunkCount, Is.LessThanOrEqualTo(9));

            // Interact with nearby landmark quest.
            Assert.That(host.TryInteractNearbyLandmarkQuest(2, out RpgLandmarkQuestRewardResult qr), Is.True);
            Assert.That(qr.State, Is.EqualTo(RpgLandmarkQuestState.Completed));
            Assert.That(questRewards, Is.EqualTo(2));

            host.Dispose();
            Assert.That(host.IsDisposed, Is.True);
            Assert.That(root.transform.childCount, Is.Zero);
        }

        [Test]
        public void AllFour500xMapsBootStreamAndDisposeCleanlyThroughUnifiedHost()
        {
            foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
            {
                host = new RpgWorld500xExplorationHost(
                    root.transform, spec.MapId,
                    streamingRadiusChunks: 1,
                    tileSlices: new Sprite[] { null });
                Assert.That(host.Layout.Width, Is.EqualTo(spec.ExpandedWidth));
                Assert.That(host.Layout.Height, Is.EqualTo(spec.ExpandedHeight));
                Assert.That(host.ChunkRenderer.ChunkCount, Is.LessThanOrEqualTo(9));
                Assert.That(host.Tracker.DiscoveredMarkerCount, Is.GreaterThanOrEqualTo(1));
                host.Dispose();
                host = null;
            }
            Assert.That(root.transform.childCount, Is.Zero);
        }
    }
}
