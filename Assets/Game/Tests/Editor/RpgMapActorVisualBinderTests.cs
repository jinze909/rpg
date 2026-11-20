using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgMapActorVisualBinderTests
    {
        private static Sprite MakeTestSprite(string name)
        {
            var tex = new Texture2D(16, 24, TextureFormat.RGBA32, mipChain: false);
            tex.filterMode = FilterMode.Point;
            var sprite = Sprite.Create(tex, new Rect(0, 0, 16, 24), new Vector2(0.5f, 0.125f), 16f);
            sprite.name = name;
            return sprite;
        }

        [Test]
        public void BinderAnimatesWalkAlternationFacingFlipAlertPoseAndYSorting()
        {
            var root = new GameObject("Actor visual binder test");
            Sprite idle = MakeTestSprite("idle");
            Sprite walk1 = MakeTestSprite("walk1");
            Sprite walk2 = MakeTestSprite("walk2");
            Sprite alert = MakeTestSprite("alert");
            RpgMapActorVisualBinder binder = null;
            try
            {
                binder = new RpgMapActorVisualBinder(root.transform, "Hero",
                    idle, walk1, walk2, alert, baseSortingOrder: 6);
                Assert.That(binder.SpriteRenderer.sprite, Is.EqualTo(idle));

                binder.Sync(new Vector2(20f, 30f), Vector2Int.left, RpgActorVisualState.Moving, 0.05f);
                Assert.That(binder.SpriteRenderer.flipX, Is.True);
                Assert.That(binder.SpriteRenderer.sprite, Is.EqualTo(walk1));
                int orderNorth = binder.SpriteRenderer.sortingOrder;

                binder.Sync(new Vector2(20f, 10f), Vector2Int.right, RpgActorVisualState.Moving, 0.25f);
                Assert.That(binder.SpriteRenderer.flipX, Is.False);
                Assert.That(binder.SpriteRenderer.sprite, Is.EqualTo(walk2));
                Assert.That(binder.SpriteRenderer.sortingOrder, Is.GreaterThan(orderNorth),
                    "Actors further south (lower Y) must sort in front.");

                binder.Sync(new Vector2(20f, 10f), Vector2Int.right, RpgActorVisualState.AlertOrBrace, 0.1f);
                Assert.That(binder.SpriteRenderer.sprite, Is.EqualTo(alert));

                binder.Sync(new Vector2(20f, 10f), Vector2Int.right, RpgActorVisualState.Idle, 0.1f);
                Assert.That(binder.SpriteRenderer.sprite, Is.EqualTo(idle));

                binder.Dispose();
                Assert.That(binder.IsDisposed, Is.True);
                Assert.That(root.transform.childCount, Is.Zero);
            }
            finally
            {
                binder?.Dispose();
                UnityEngine.Object.DestroyImmediate(idle.texture);
                UnityEngine.Object.DestroyImmediate(idle);
                UnityEngine.Object.DestroyImmediate(walk1.texture);
                UnityEngine.Object.DestroyImmediate(walk1);
                UnityEngine.Object.DestroyImmediate(walk2.texture);
                UnityEngine.Object.DestroyImmediate(walk2);
                UnityEngine.Object.DestroyImmediate(alert.texture);
                UnityEngine.Object.DestroyImmediate(alert);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NullParentRejected()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new RpgMapActorVisualBinder(null, "Hero", null));
        }
    }
}
