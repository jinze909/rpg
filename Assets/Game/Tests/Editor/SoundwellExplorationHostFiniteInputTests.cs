using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class SoundwellExplorationHostFiniteInputTests
    {
        private static void WalkUntil(SoundwellLargeMapExplorationHost host,
            Vector2 direction, Func<Vector2, bool> reached, int maxSteps = 200)
        {
            for (int i = 0; i < maxSteps && !reached(host.Explorer.Position); i++)
                host.Step(direction, 0.1f);
        }

        [Test]
        public void DualChannelVfxAndStepIgnoreNonFiniteInputsWithoutThrowingOrPoisoning()
        {
            var root = new GameObject("SoundwellFiniteVfxRoot");
            var camObj = new GameObject("SoundwellFiniteCam");
            camObj.transform.position = new Vector3(40f, 30f, -10f);
            SoundwellLargeMapExplorationHost host = null;
            try
            {
                host = new SoundwellLargeMapExplorationHost(root.transform);

                // Walk to Wet Cloth (80, 26) then Bronze Bell (80, 34) to activate both VFX channels.
                WalkUntil(host, Vector2.right, p => p.x >= 80f);
                WalkUntil(host, Vector2.down, p => p.y <= 26.5f);
                Assert.That(host.TryInteract(), Is.True);
                Assert.That(host.IsAcousticPulsePlaying, Is.True);

                WalkUntil(host, Vector2.up, p => p.y >= 34f);
                Assert.That(host.TryInteract(), Is.True);
                Assert.That(host.IsAcousticPulsePlaying, Is.True);
                Assert.That(host.IsThreatBreakPlaying, Is.True);

                // Non-finite moveInput and deltaTime must not throw IndexOutOfRangeException or poison state.
                Assert.DoesNotThrow(() =>
                    host.Step(new Vector2(float.NaN, float.PositiveInfinity), float.NaN, camObj.transform));
                Assert.DoesNotThrow(() =>
                    host.Step(Vector2.right, float.PositiveInfinity, camObj.transform));
                Assert.DoesNotThrow(() =>
                    host.Step(Vector2.left, float.NegativeInfinity, camObj.transform));

                Assert.That(host.IsAcousticPulsePlaying, Is.True);
                Assert.That(host.IsThreatBreakPlaying, Is.True);
                Assert.That(float.IsNaN(camObj.transform.position.x) ||
                    float.IsInfinity(camObj.transform.position.x), Is.False);
                Assert.That(float.IsNaN(host.Explorer.Position.x) ||
                    float.IsInfinity(host.Explorer.Position.x), Is.False);

                // Subsequent finite step completes both VFX channels cleanly.
                host.Step(Vector2.zero, 0.5f, camObj.transform);
                Assert.That(host.IsAcousticPulsePlaying, Is.False);
                Assert.That(host.IsThreatBreakPlaying, Is.False);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(camObj);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
