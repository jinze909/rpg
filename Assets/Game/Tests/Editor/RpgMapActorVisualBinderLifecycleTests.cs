using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgMapActorVisualBinderLifecycleTests
    {
        private GameObject root;
        private RpgMapActorVisualBinder binder;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("MapActorVisualBinderLifecycleRoot");
            binder = new RpgMapActorVisualBinder(root.transform, "Hero", null);
        }

        [TearDown]
        public void TearDown()
        {
            binder?.Dispose();
            binder = null;
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void Sync_NonFiniteTilePosition_PreservesPreviousValidPositionAndSortingOrder()
        {
            binder.Sync(new Vector2(12f, 20f), Vector2Int.right, RpgActorVisualState.Idle);
            Vector3 expectedPos = binder.ActorObject.transform.localPosition;
            int expectedOrder = binder.SpriteRenderer.sortingOrder;

            Assert.DoesNotThrow(() =>
                binder.Sync(new Vector2(float.NaN, 5f), Vector2Int.right, RpgActorVisualState.Idle));
            Assert.DoesNotThrow(() =>
                binder.Sync(new Vector2(5f, float.PositiveInfinity), Vector2Int.right, RpgActorVisualState.Idle));
            Assert.DoesNotThrow(() =>
                binder.Sync(new Vector2(float.NegativeInfinity, float.NaN), Vector2Int.right, RpgActorVisualState.Idle));

            Assert.That(binder.ActorObject.transform.localPosition, Is.EqualTo(expectedPos));
            Assert.That(binder.SpriteRenderer.sortingOrder, Is.EqualTo(expectedOrder));
        }

        [Test]
        public void SetTint_NonFiniteColorChannels_PreservesPreviousValidTint()
        {
            var expected = new Color(0.8f, 0.6f, 0.4f, 1f);
            binder.SetTint(expected);

            Assert.DoesNotThrow(() => binder.SetTint(new Color(float.NaN, 0.5f, 0.5f, 1f)));
            Assert.DoesNotThrow(() => binder.SetTint(new Color(0.5f, float.PositiveInfinity, 0.5f, 1f)));
            Assert.DoesNotThrow(() => binder.SetTint(new Color(0.5f, 0.5f, 0.5f, float.NegativeInfinity)));

            Assert.That(binder.SpriteRenderer.color, Is.EqualTo(expected));
        }

        [Test]
        public void ExternalSpriteRendererTeardown_SetsIsDisposedImmediately_AndCleansUpActorOnSync()
        {
            Assert.That(binder.IsDisposed, Is.False);
            Assert.That(root.transform.childCount, Is.EqualTo(1));

            UnityEngine.Object.DestroyImmediate(binder.SpriteRenderer);
            Assert.That(binder.IsDisposed, Is.True);

            Assert.DoesNotThrow(() =>
                binder.Sync(new Vector2(4f, 8f), Vector2Int.left, RpgActorVisualState.Moving, 0.1f));
            Assert.That(root.transform.childCount, Is.Zero,
                "Orphaned actor GameObject must be cleaned up when SpriteRenderer is torn down externally.");
        }

        [Test]
        public void ExternalParentTeardown_AndDoubleDispose_StayIdempotent()
        {
            UnityEngine.Object.DestroyImmediate(root);
            root = null;
            Assert.That(binder.IsDisposed, Is.True);

            Assert.DoesNotThrow(() =>
                binder.Sync(new Vector2(1f, 2f), Vector2Int.right, RpgActorVisualState.Idle));
            Assert.DoesNotThrow(() => binder.SetTint(Color.white));
            Assert.DoesNotThrow(() => binder.Dispose());
            Assert.DoesNotThrow(() => binder.Dispose());
        }
    }
}
