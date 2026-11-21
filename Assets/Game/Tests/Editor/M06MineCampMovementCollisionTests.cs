using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    /// <summary>
    /// Covers real per-frame M06 mine-camp walking, independently of the
    /// reunion-save candidate's test file.
    /// </summary>
    public sealed class M06MineCampMovementCollisionTests
    {
        private static M06MineCampExplorationHost BuildHost(GameObject root)
        {
            var values = new Dictionary<string, int>();
            return new M06MineCampExplorationHost(
                root.transform,
                key => values.TryGetValue(key, out int value) ? value : 0,
                (key, value) => values[key] = value);
        }

        [Test]
        public void LargeFrameCannotTeleportAcrossWallBetweenSouthGateAndEscortRoom()
        {
            var root = new GameObject("M06 large-frame wall collision");
            M06MineCampExplorationHost host = null;
            try
            {
                host = BuildHost(root);
                // The south gate has floor at (26,8), and the escort room
                // has floor at (34,8), but the separating wall at y=8 is
                // NOT the connector corridor (which is at y=10..13).
                Assert.That(host.TryPlaceAtWalkableTile(new Vector2Int(26, 8)), Is.True);
                Assert.That(host.Layout.Tiles[34, 8],
                    Is.Not.EqualTo(SoundwellTileKind.WallTop));
                Assert.That(M06MineCampMapLayout.IsWalkable(host.Layout.Tiles[28, 8]),
                    Is.False);

                Assert.That(host.StepMovement(Vector2.right,
                    8f / M06MineCampExplorationHost.MoveSpeedTilesPerSecond), Is.True);
                Assert.That(host.PlayerPosition.x, Is.GreaterThan(26.5f));
                Assert.That(host.PlayerPosition.x, Is.LessThan(28f),
                    "One long frame must not tunnel past the intervening wall.");
                Vector2 blocked = host.PlayerPosition;
                Assert.That(host.StepMovement(Vector2.right, 0.2f), Is.False);
                Assert.That(host.PlayerPosition, Is.EqualTo(blocked));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SameLargeFrameCrossesTheRealFourTileWideConnectorCorridor()
        {
            var root = new GameObject("M06 large-frame corridor movement");
            M06MineCampExplorationHost host = null;
            try
            {
                host = BuildHost(root);
                Assert.That(host.TryPlaceAtWalkableTile(new Vector2Int(26, 12)), Is.True);
                // Exactly the same horizontal travel is valid at y=12.
                for (int x = 27; x <= 34; x++)
                    Assert.That(M06MineCampMapLayout.IsWalkable(host.Layout.Tiles[x, 12]),
                        Is.True, "Expected connector floor at x=" + x);

                Assert.That(host.StepMovement(Vector2.right,
                    8f / M06MineCampExplorationHost.MoveSpeedTilesPerSecond), Is.True);
                Assert.That(host.PlayerPosition.x, Is.EqualTo(34.5f).Within(0.03f));
                Assert.That(host.PlayerPosition.y, Is.EqualTo(12.5f).Within(0.001f));
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
