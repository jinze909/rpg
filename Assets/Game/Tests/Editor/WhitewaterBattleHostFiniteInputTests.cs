using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class WhitewaterBattleHostFiniteInputTests
    {
        [Test]
        public void StepCombatVfxAndShakeIgnoreNonFiniteDeltaTimeWithoutThrowingOrPoisoning()
        {
            var root = new GameObject("WhitewaterFiniteVfxRoot");
            WhitewaterCrossingBattleHost host = null;
            try
            {
                host = new WhitewaterCrossingBattleHost(root.transform, startingMomentum: 1);

                // Non-finite shake inputs must be ignored before any valid shake is active.
                host.TriggerClashShake(float.NaN, 0.2f);
                host.TriggerClashShake(0.2f, float.PositiveInfinity);
                Assert.That(host.ShakeRemaining, Is.Zero);

                // Resolve a valid parry so 3-frame VFX and clash shake both become active.
                Assert.That(host.ResolveAuthoritativeCounter(CombatLockKind.ShieldParry), Is.True);
                Assert.That(host.CombatVfxSpriteRenderer.enabled, Is.True);
                float initialShake = host.ShakeRemaining;
                Assert.That(initialShake, Is.GreaterThan(0f));

                // NaN/Infinity deltaTime must not throw IndexOutOfRangeException or poison VFX/shake.
                Assert.DoesNotThrow(() => host.StepCombatVfx(float.NaN));
                Assert.DoesNotThrow(() => host.StepCombatVfx(float.PositiveInfinity));
                Assert.DoesNotThrow(() => host.StepCombatVfx(float.NegativeInfinity));
                Assert.That(host.CombatVfxSpriteRenderer.enabled, Is.True);

                Vector3 cam = host.ComputeFramedCameraPosition(
                    new Vector3(float.NaN, float.PositiveInfinity, float.NaN),
                    float.NaN,
                    halfViewWidth: float.NaN,
                    halfViewHeight: float.PositiveInfinity,
                    smoothSpeed: float.NaN);
                Assert.That(float.IsNaN(cam.x) || float.IsInfinity(cam.x), Is.False);
                Assert.That(float.IsNaN(cam.y) || float.IsInfinity(cam.y), Is.False);
                Assert.That(float.IsNaN(cam.z) || float.IsInfinity(cam.z), Is.False);
                Assert.That(host.ShakeRemaining, Is.EqualTo(initialShake));

                // Subsequent finite deltaTime still completes VFX and shake cleanly.
                host.ComputeFramedCameraPosition(cam, 0.35f);
                Assert.That(host.ShakeRemaining, Is.Zero);
                Assert.That(host.CombatVfxSpriteRenderer.enabled, Is.False);
            }
            finally
            {
                host?.Dispose();
                Object.DestroyImmediate(root);
            }
        }
    }
}
