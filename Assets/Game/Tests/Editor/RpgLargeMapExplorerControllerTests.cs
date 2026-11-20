using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgLargeMapExplorerControllerTests
    {
        [Test]
        public void RedSaltHarborExplorerWalksBetweenDistrictsFiresZoneEventsAndFindsTriggers()
        {
            RedSaltHarborMapLayout map = RedSaltHarborMapLayout.Build();
            var explorer = RpgLargeMapExplorerController.ForRedSaltHarbor(map);
            var visitedZones = new List<string>();
            explorer.ZoneEntered += zone => visitedZones.Add(zone.Id);

            Assert.That(explorer.CurrentZone.HasValue, Is.True);
            Assert.That(explorer.CurrentZone.Value.Id, Is.EqualTo("harbor.outer-berth"));

            // Walk east along the stone quay from OuterBerth (x=16) to Seran (x=50).
            for (int i = 0; i < 60; i++)
                explorer.StepMovement(Vector2.right, 0.1f);

            Assert.That(explorer.Position.x, Is.GreaterThan(45f));
            Assert.That(visitedZones, Does.Contain("harbor.quay-promenade"));

            // Walk directly onto the landing-check trigger from a fresh explorer.
            var berthExplorer = RpgLargeMapExplorerController.ForRedSaltHarbor(map);
            for (int i = 0; i < 10; i++)
                berthExplorer.StepMovement(Vector2.right, 0.1f);
            Assert.That(berthExplorer.TryFindNearestTrigger(out RpgMapTriggerAnchor trigger), Is.True);
            Assert.That(trigger.Id, Is.EqualTo("m05.landing-check"));
        }

        [Test]
        public void SoundwellExplorerRespectsWallsAndIronGateAndClampsCameraWithinBounds()
        {
            SoundwellLargeMapLayout map = SoundwellLargeMapLayout.Build();
            bool gateOpen = false;
            var explorer = RpgLargeMapExplorerController.ForSoundwell(map, () => gateOpen);

            // Try walking straight north into the wall of EntryShaft; must stop inside room.
            for (int i = 0; i < 30; i++)
                explorer.StepMovement(Vector2.up, 0.1f);
            Assert.That(explorer.Position.y, Is.LessThan(48.5f));

            // Camera must stay clamped inside [halfViewW, width - halfViewW].
            Vector3 cam = explorer.ComputeClampedCameraPosition(new Vector3(-50f, -50f, -10f), 10f);
            float halfW = RpgWorldMapScale.LogicalScreenTilesX * 0.5f;
            float halfH = RpgWorldMapScale.LogicalScreenTilesY * 0.5f;
            Assert.That(cam.x, Is.GreaterThanOrEqualTo(halfW));
            Assert.That(cam.x, Is.LessThanOrEqualTo(map.Width - halfW));
            Assert.That(cam.y, Is.GreaterThanOrEqualTo(halfH));
            Assert.That(cam.y, Is.LessThanOrEqualTo(map.Height - halfH));
            Assert.That(cam.z, Is.EqualTo(-10f));
        }

        [Test]
        public void SoundwellConnectorClearsPreviousZoneAndRestoresNextRoomOnEntry()
        {
            SoundwellLargeMapLayout map = SoundwellLargeMapLayout.Build();
            var explorer = RpgLargeMapExplorerController.ForSoundwell(map, () => false);
            var entered = new List<string>();
            explorer.ZoneEntered += zone => entered.Add(zone.Id);

            Assert.That(explorer.CurrentZone.HasValue, Is.True);
            Assert.That(explorer.CurrentZone.Value.Id, Is.EqualTo("soundwell.entry"));

            // The carved link at (22,29) is walkable but belongs to no room.
            // It should show the Soundwell fallback, not the previous room.
            Assert.That(explorer.TryPlaceAtWalkableTile(new Vector2Int(22, 29)), Is.True);
            Assert.That(explorer.CurrentZone.HasValue, Is.False);
            Assert.That(entered, Is.Empty, "Restoring a position is not a live zone-entry event.");

            // Walking into the central chamber must now emit exactly one
            // real entry, rather than preserving the old room through the link.
            Assert.That(explorer.StepMovement(Vector2.right, 1f), Is.True);
            Assert.That(explorer.CurrentZone.HasValue, Is.True);
            Assert.That(explorer.CurrentZone.Value.Id, Is.EqualTo("soundwell.central-hall"));
            Assert.That(entered, Is.EqualTo(new[] { "soundwell.central-hall" }));

            // Returning to the connector also clears the chamber label.
            Assert.That(explorer.TryPlaceAtWalkableTile(new Vector2Int(22, 29)), Is.True);
            Assert.That(explorer.CurrentZone.HasValue, Is.False);
        }

        [Test]
        public void HitstopPausesMovementAndScreenShakeDecaysCleanly()
        {
            RedSaltHarborMapLayout map = RedSaltHarborMapLayout.Build();
            var explorer = RpgLargeMapExplorerController.ForRedSaltHarbor(map);
            Vector2 start = explorer.Position;

            explorer.TriggerHitstop(0.1f);
            Assert.That(explorer.StepMovement(Vector2.right, 0.05f), Is.False);
            Assert.That(explorer.Position, Is.EqualTo(start));
            Assert.That(explorer.StepMovement(Vector2.right, 0.06f), Is.False);
            Assert.That(explorer.StepMovement(Vector2.right, 0.1f), Is.True);
            Assert.That(explorer.Position.x, Is.GreaterThan(start.x));

            explorer.TriggerScreenShake(0.35f, 0.15f);
            Assert.That(explorer.ShakeRemaining, Is.GreaterThan(0f));
            explorer.ComputeClampedCameraPosition(new Vector3(start.x, start.y, -10f), 0.2f);
            Assert.That(explorer.ShakeRemaining, Is.Zero);
            Assert.That(explorer.ShakeIntensity, Is.Zero);
        }
    }
}
