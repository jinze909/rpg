using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    [TestFixture]
    public sealed class RpgWorld500xFourRegionExplorationHostMovementGuardE2ETests
    {
        [Test]
        public void AllFourRegions_ExtremeFiniteMovementInput_MatchesUnitVectorTravelAndFacing()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count, "Expected exactly 4 regions in 500x world scale.");

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var root = new GameObject("FourRegionMoveGuard_" + spec.MapId);
                RpgWorld500xExplorationHost host = null;
                try
                {
                    host = new RpgWorld500xExplorationHost(
                        root.transform, spec.MapId,
                        streamingRadiusChunks: 1,
                        tileSlices: new Sprite[] { null });

                    Vector2 start = host.PlayerPosition;
                    Assert.IsTrue(host.StepMovement(Vector2.right, 0.25f), $"Normal rightward step should succeed in {spec.MapId}.");
                    Vector2 normalEnd = host.PlayerPosition;

                    Assert.IsTrue(host.TryPlaceAtWalkablePosition(start), $"Resetting to spawn should succeed in {spec.MapId}.");
                    Assert.IsTrue(host.StepMovement(new Vector2(float.MaxValue, 0f), 0.25f), $"Extreme finite step should succeed in {spec.MapId}.");
                    Assert.AreEqual(normalEnd.x, host.PlayerPosition.x, 0.001f, $"Extreme finite X mismatch in {spec.MapId}.");
                    Assert.AreEqual(normalEnd.y, host.PlayerPosition.y, 0.001f, $"Extreme finite Y mismatch in {spec.MapId}.");
                    Assert.AreEqual(Vector2.right, host.Facing, $"Extreme finite facing mismatch in {spec.MapId}.");
                }
                finally
                {
                    host?.Dispose();
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        [Test]
        public void AllFourRegions_NonFiniteAndNonPositiveMovementInputs_AreRejectedWithoutMutatingState()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count, "Expected exactly 4 regions in 500x world scale.");

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var root = new GameObject("FourRegionInvalidMoveGuard_" + spec.MapId);
                RpgWorld500xExplorationHost host = null;
                try
                {
                    host = new RpgWorld500xExplorationHost(
                        root.transform, spec.MapId,
                        streamingRadiusChunks: 1,
                        tileSlices: new Sprite[] { null });

                    Assert.IsTrue(host.StepMovement(Vector2.right, 0.1f));
                    Vector2 beforePos = host.PlayerPosition;
                    Vector2 beforeFacing = host.Facing;

                    Assert.IsFalse(host.StepMovement(new Vector2(float.NaN, 0f), 0.25f));
                    Assert.IsFalse(host.StepMovement(new Vector2(float.PositiveInfinity, 1f), 0.25f));
                    Assert.IsFalse(host.StepMovement(new Vector2(0f, float.NegativeInfinity), 0.25f));
                    Assert.IsFalse(host.StepMovement(Vector2.zero, 0.25f));
                    Assert.IsFalse(host.StepMovement(Vector2.right, 0f));
                    Assert.IsFalse(host.StepMovement(Vector2.right, -0.1f));
                    Assert.IsFalse(host.StepMovement(Vector2.right, float.NaN));
                    Assert.IsFalse(host.StepMovement(Vector2.right, float.PositiveInfinity));
                    Assert.IsFalse(host.TryPlaceAtWalkablePosition(new Vector2(-10f, -10f)));

                    Assert.AreEqual(beforePos, host.PlayerPosition, $"Rejected inputs must not mutate position in {spec.MapId}.");
                    Assert.AreEqual(beforeFacing, host.Facing, $"Rejected inputs must not mutate facing in {spec.MapId}.");
                }
                finally
                {
                    host?.Dispose();
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }
    }
}
