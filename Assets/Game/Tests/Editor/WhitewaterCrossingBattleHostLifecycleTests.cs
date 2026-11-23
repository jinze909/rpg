using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class WhitewaterCrossingBattleHostLifecycleTests
    {
        [Test]
        public void ExternalCanvasTeardownMarksDisposedAndFailsClosedSafely()
        {
            var root = new GameObject("Whitewater external teardown");
            WhitewaterCrossingBattleHost host = null;
            try
            {
                host = new WhitewaterCrossingBattleHost(root.transform, startingMomentum: 1);
                Assert.That(host.IsDisposed, Is.False);

                UnityEngine.Object.DestroyImmediate(root);
                root = null;

                Assert.That(host.IsDisposed, Is.True);
                Assert.That(host.ResolveAuthoritativeCounter(CombatLockKind.ShieldParry), Is.False);
                Assert.That(host.CableTracker.BellsCompleted, Is.Zero);
                host.TriggerClashShake(0.3f, 0.2f);
                Assert.That(host.ShakeRemaining, Is.Zero);
                Assert.DoesNotThrow(() => host.LayoutForViewportAspect(16f / 9f));
                Assert.DoesNotThrow(() => host.RefreshHud());
                Assert.DoesNotThrow(() => host.Dispose());
            }
            finally
            {
                host?.Dispose();
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PostThirdBellCounterAndInvalidViewportAspectAreIgnoredWithoutSideEffects()
        {
            var root = new GameObject("Whitewater post-third-bell and aspect");
            WhitewaterCrossingBattleHost host = null;
            var bells = new List<int>();
            int collapses = 0;
            try
            {
                host = new WhitewaterCrossingBattleHost(root.transform, startingMomentum: 1);
                host.BellAdvanced += b => bells.Add(b);
                host.ThirdBellCollapseReady += () => collapses++;

                // Invalid viewport aspects must not throw or poison layout scale.
                Assert.DoesNotThrow(() => host.LayoutForViewportAspect(float.NaN));
                Assert.DoesNotThrow(() => host.LayoutForViewportAspect(float.PositiveInfinity));
                Assert.DoesNotThrow(() => host.LayoutForViewportAspect(0f));
                Assert.DoesNotThrow(() => host.LayoutForViewportAspect(-1.5f));

                // Complete all 3 bells.
                Assert.That(host.ResolveAuthoritativeCounter(CombatLockKind.ShieldParry), Is.True);
                Assert.That(host.ResolveAuthoritativeCounter(CombatLockKind.BladeRepel), Is.True);
                Assert.That(host.ResolveAuthoritativeCounter(CombatLockKind.ShieldParry), Is.True);
                Assert.That(host.CableTracker.ReadyForThirdBellCollapse, Is.True);
                Assert.That(bells, Is.EqualTo(new[] { 1, 2, 3 }));
                Assert.That(collapses, Is.EqualTo(1));

                // Drain shake and VFX completely.
                host.ComputeFramedCameraPosition(new Vector3(24f, 22f, -10f), 0.5f);
                Assert.That(host.ShakeRemaining, Is.Zero);
                Assert.That(host.CombatVfxSpriteRenderer.enabled, Is.False);

                // Further counters after 3rd bell must no-op without phantom shake, VFX, or events.
                Assert.That(host.ResolveAuthoritativeCounter(CombatLockKind.ShieldParry), Is.False);
                Assert.That(host.ResolveAuthoritativeCounter(CombatLockKind.BladeRepel), Is.False);
                Assert.That(host.ShakeRemaining, Is.Zero);
                Assert.That(host.CombatVfxSpriteRenderer.enabled, Is.False);
                Assert.That(bells, Is.EqualTo(new[] { 1, 2, 3 }));
                Assert.That(collapses, Is.EqualTo(1));
            }
            finally
            {
                host?.Dispose();
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
