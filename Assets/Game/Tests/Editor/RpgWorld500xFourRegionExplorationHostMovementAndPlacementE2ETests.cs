using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    /// <summary>
    /// Four-region E2E regression tests for RpgWorld500xExplorationHost covering
    /// oversized finite movement input normalization, non-finite input rejection,
    /// walkable vs. out-of-bounds placement guards, and leak-free disposal across
    /// Soundwell, RedSaltHarbor, WhitewaterCrossing, and MineCamp.
    /// </summary>
    public sealed class RpgWorld500xFourRegionExplorationHostMovementAndPlacementE2ETests
    {
        [Test]
        public void AllFourRegions_ExtremeFiniteMovementMatchesUnitStepAndRejectsInvalidPlacementWithoutLeaks()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

            for (int r = 0; r < specs.Count; r++)
            {
                RpgExpandedMapSpec spec = specs[r];
                var root = new GameObject("FourRegionExplorationHostMovement_" + spec.MapId);
                RpgWorld500xExplorationHost host = null;

                try
                {
                    host = new RpgWorld500xExplorationHost(
                        root.transform,
                        spec.MapId,
                        streamingRadiusChunks: 1,
                        tileSlices: new Sprite[] { null });

                    Vector2 start = host.PlayerPosition;
                    Assert.IsTrue(host.StepMovement(Vector2.right, 0.25f),
                        $"Baseline right movement failed on {spec.MapId}.");
                    Vector2 normalEnd = host.PlayerPosition;

                    Assert.IsTrue(host.TryPlaceAtWalkablePosition(start),
                        $"Failed to restore spawn position on {spec.MapId}.");
                    Assert.AreEqual(start.x, host.PlayerPosition.x, 0.001f);
                    Assert.AreEqual(start.y, host.PlayerPosition.y, 0.001f);

                    // Oversized finite input must match normal unit-direction movement without NaN/zero collapse.
                    Assert.IsTrue(host.StepMovement(new Vector2(float.MaxValue, 0f), 0.25f),
                        $"Extreme finite movement failed on {spec.MapId}.");
                    Assert.AreEqual(normalEnd.x, host.PlayerPosition.x, 0.001f,
                        $"Extreme finite X mismatch on {spec.MapId}.");
                    Assert.AreEqual(normalEnd.y, host.PlayerPosition.y, 0.001f,
                        $"Extreme finite Y mismatch on {spec.MapId}.");
                    Assert.AreEqual(Vector2.right, host.Facing);

                    // Non-finite movement inputs are rejected without altering position or facing.
                    Vector2 beforeInvalid = host.PlayerPosition;
                    Assert.IsFalse(host.StepMovement(new Vector2(float.PositiveInfinity, 1f), 0.25f));
                    Assert.IsFalse(host.StepMovement(new Vector2(float.NaN, 0f), 0.25f));
                    Assert.AreEqual(beforeInvalid, host.PlayerPosition);
                    Assert.AreEqual(Vector2.right, host.Facing);

                    // Invalid or out-of-bounds placement requests fail closed.
                    Assert.IsFalse(host.TryPlaceAtWalkablePosition(new Vector2(-1f, start.y)));
                    Assert.IsFalse(host.TryPlaceAtWalkablePosition(new Vector2(start.x, -1f)));
                    Assert.IsFalse(host.TryPlaceAtWalkablePosition(new Vector2(spec.ExpandedWidth + 1f, start.y)));
                    Assert.IsFalse(host.TryPlaceAtWalkablePosition(new Vector2(start.x, spec.ExpandedHeight + 1f)));
                    Assert.IsFalse(host.TryPlaceAtWalkablePosition(new Vector2(float.NaN, start.y)));
                    Assert.AreEqual(beforeInvalid, host.PlayerPosition);

                    // Clean disposal prevents post-dispose placement.
                    host.Dispose();
                    Assert.IsTrue(host.IsDisposed);
                    Assert.IsFalse(host.TryPlaceAtWalkablePosition(start));
                    host = null;
                }
                finally
                {
                    host?.Dispose();
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }

            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore,
                "Texture2D leak detected after four-region exploration host movement sweep.");
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore,
                "Sprite leak detected after four-region exploration host movement sweep.");
        }
    }
}
