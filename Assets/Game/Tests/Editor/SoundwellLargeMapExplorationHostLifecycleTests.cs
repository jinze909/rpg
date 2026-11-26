using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class SoundwellLargeMapExplorationHostLifecycleTests
    {
        [Test]
        public void ExternalCanvasTeardownMarksDisposedAndFailsClosedSafely()
        {
            var root = new GameObject("Soundwell external teardown");
            SoundwellLargeMapExplorationHost host = null;
            try
            {
                host = new SoundwellLargeMapExplorationHost(root.transform);
                Assert.That(host.IsDisposed, Is.False);

                UnityEngine.Object.DestroyImmediate(root);
                root = null;

                Assert.That(host.IsDisposed, Is.True);
                Assert.That(host.Step(Vector2.right, 0.1f), Is.False);
                Assert.That(host.TryInteract(), Is.False);
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
        public void InvalidViewportAspectAndDisposedChildComponentsDoNotThrow()
        {
            var root = new GameObject("Soundwell child disposal and aspect");
            SoundwellLargeMapExplorationHost host = null;
            try
            {
                host = new SoundwellLargeMapExplorationHost(root.transform);

                Assert.DoesNotThrow(() => host.LayoutForViewportAspect(float.NaN));
                Assert.DoesNotThrow(() => host.LayoutForViewportAspect(float.PositiveInfinity));
                Assert.DoesNotThrow(() => host.LayoutForViewportAspect(0f));
                Assert.DoesNotThrow(() => host.LayoutForViewportAspect(-1f));

                host.HeroVisual.Dispose();
                host.BeastVisual.Dispose();
                host.MinimapHud.Dispose();
                host.LockBreakHud.Dispose();

                Assert.DoesNotThrow(() => host.Step(Vector2.right, 0.1f));
                Assert.DoesNotThrow(() => host.TryInteract());
                Assert.DoesNotThrow(() => host.LayoutForViewportAspect(16f / 9f));
                Assert.DoesNotThrow(() => host.RefreshHud());
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
