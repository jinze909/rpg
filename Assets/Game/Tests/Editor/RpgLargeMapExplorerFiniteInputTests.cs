using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgLargeMapExplorerFiniteInputTests
    {
        [Test]
        public void NonFiniteMovementHitstopAndShakeInputsNeverPoisonExplorerState()
        {
            RedSaltHarborMapLayout map = RedSaltHarborMapLayout.Build();
            var explorer = RpgLargeMapExplorerController.ForRedSaltHarbor(map, float.NaN);
            Assert.That(explorer.MoveSpeedTilesPerSecond, Is.EqualTo(6.5f));

            Vector2 start = explorer.Position;
            Vector2Int initialFacing = explorer.Facing;

            explorer.TriggerHitstop(float.NaN);
            explorer.TriggerHitstop(float.PositiveInfinity);
            Assert.That(explorer.HitstopRemaining, Is.Zero);

            explorer.TriggerScreenShake(float.NaN, 0.3f);
            explorer.TriggerScreenShake(0.3f, float.PositiveInfinity);
            Assert.That(explorer.ShakeIntensity, Is.Zero);
            Assert.That(explorer.ShakeRemaining, Is.Zero);

            Assert.That(explorer.StepMovement(new Vector2(float.NaN, 0f), 0.1f), Is.False);
            Assert.That(explorer.StepMovement(new Vector2(float.PositiveInfinity, 0f), 0.1f), Is.False);
            Assert.That(explorer.StepMovement(Vector2.right, float.NaN), Is.False);
            Assert.That(explorer.StepMovement(Vector2.right, float.PositiveInfinity), Is.False);
            Assert.That(explorer.Position, Is.EqualTo(start));
            Assert.That(explorer.Facing, Is.EqualTo(initialFacing));

            // Normal movement still works cleanly after rejected non-finite calls.
            Assert.That(explorer.StepMovement(Vector2.right, 0.1f), Is.True);
            Assert.That(explorer.Position.x, Is.GreaterThan(start.x));
        }

        [Test]
        public void NonFiniteCameraArgumentsHoldValidBoundsAndPreserveShakeTimer()
        {
            RedSaltHarborMapLayout map = RedSaltHarborMapLayout.Build();
            var explorer = RpgLargeMapExplorerController.ForRedSaltHarbor(map);
            Vector3 prior = new Vector3(map.CameraBounds.center.x, map.CameraBounds.center.y, -10f);

            explorer.TriggerScreenShake(0.3f, 0.4f);

            Vector3 afterNanDt = explorer.ComputeClampedCameraPosition(prior, float.NaN);
            Vector3 afterInfDt = explorer.ComputeClampedCameraPosition(prior, float.PositiveInfinity);
            Assert.That(afterNanDt, Is.EqualTo(prior));
            Assert.That(afterInfDt, Is.EqualTo(prior));
            Assert.That(explorer.ShakeRemaining, Is.EqualTo(0.4f));
            Assert.That(explorer.ShakeIntensity, Is.EqualTo(0.3f));

            Vector3 recovered = explorer.ComputeClampedCameraPosition(
                new Vector3(float.NaN, float.PositiveInfinity, float.NaN),
                0.1f,
                halfViewWidth: float.NaN,
                halfViewHeight: float.NegativeInfinity,
                smoothSpeed: float.NaN);

            Assert.That(float.IsNaN(recovered.x) || float.IsInfinity(recovered.x), Is.False);
            Assert.That(float.IsNaN(recovered.y) || float.IsInfinity(recovered.y), Is.False);
            Assert.That(recovered.z, Is.EqualTo(-10f));
        }
    }
}
