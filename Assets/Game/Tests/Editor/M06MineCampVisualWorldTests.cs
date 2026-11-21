using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class M06MineCampVisualWorldTests
    {
        private static Func<string, int> Read(Dictionary<string, int> data)
        {
            return key => data.TryGetValue(key, out int value) ? value : 0;
        }

        [Test]
        public void MineCampBuildsActualPointFilteredWorldAndMovingHeroNotJustHud()
        {
            var parent = new GameObject("M06 world visuals");
            var data = new Dictionary<string, int>();
            M06MineCampExplorationHost host = null;
            try
            {
                host = new M06MineCampExplorationHost(
                    parent.transform, Read(data), (k, v) => data[k] = v);
                Assert.That(host.ChunkRenderer.ChunkCount, Is.EqualTo(6));
                Assert.That(host.ChunkRenderer.LightHaloCount,
                    Is.EqualTo(host.Layout.PointLights.Count));
                Assert.That(host.HeroVisual.ActorObject, Is.Not.Null);
                Assert.That(host.ShenLiVisual.ActorObject.activeSelf, Is.False,
                    "Shen Li is not present before the verified M05 landing.");
                SpriteRenderer[] world = host.ChunkRenderer.RootObject
                    .GetComponentsInChildren<SpriteRenderer>(true);
                Assert.That(world.Length, Is.EqualTo(6 + host.Layout.PointLights.Count + 2));
                foreach (SpriteRenderer sprite in world)
                {
                    if (sprite == host.HeroVisual.SpriteRenderer ||
                        sprite == host.ShenLiVisual.SpriteRenderer)
                        continue;
                    Assert.That(sprite.sprite, Is.Not.Null);
                    Assert.That(sprite.sprite.pixelsPerUnit,
                        Is.EqualTo(RpgWorldMapScale.PixelsPerUnit));
                    Assert.That(sprite.sprite.texture.filterMode,
                        Is.EqualTo(FilterMode.Point));
                }

                Vector2 before = host.PlayerPosition;
                Assert.That(host.StepMovement(Vector2.right, 0.1f), Is.True);
                Assert.That(host.HeroVisual.CurrentState, Is.EqualTo(RpgActorVisualState.Moving));
                Assert.That(host.HeroVisual.ActorObject.transform.localPosition.x,
                    Is.GreaterThan(before.x));
                Assert.That(host.StepMovement(Vector2.zero, 0.1f), Is.False);
                Assert.That(host.HeroVisual.CurrentState, Is.EqualTo(RpgActorVisualState.Idle));

                Assert.That(host.TryPlaceAtWalkableTile(new Vector2Int(54, 36)), Is.True);
                Assert.That(host.HeroVisual.ActorObject.transform.localPosition,
                    Is.EqualTo(new Vector3(54.5f, 36.5f, 0f)));
                Vector3 cam = host.StepCamera(new Vector3(-100f, -100f, -10f), 1f);
                Assert.That(cam.x, Is.GreaterThanOrEqualTo(
                    host.Layout.CameraBounds.min.x + RpgWorldMapScale.LogicalScreenTilesX * 0.5f));
                Assert.That(cam.y, Is.GreaterThanOrEqualTo(
                    host.Layout.CameraBounds.min.y + RpgWorldMapScale.LogicalScreenTilesY * 0.5f));
                Assert.That(cam.z, Is.EqualTo(-10f));

                host.Dispose();
                Assert.That(host.ChunkRenderer.IsDisposed, Is.True);
                Assert.That(host.HeroVisual.IsDisposed, Is.True);
                Assert.That(host.ShenLiVisual.IsDisposed, Is.True);
                Assert.That(parent.transform.childCount, Is.Zero);
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void RendererRejectsNullAndSupportsExactM06LayoutWithNoScenesOrSaves()
        {
            var parent = new GameObject("M06 renderer factory");
            RpgLargeMapChunkRenderer chunks = null;
            try
            {
                var map = M06MineCampMapLayout.Build();
                Assert.Throws<ArgumentNullException>(() =>
                    RpgLargeMapChunkRenderer.BuildForMineCamp(null, map));
                Assert.Throws<ArgumentNullException>(() =>
                    RpgLargeMapChunkRenderer.BuildForMineCamp(parent.transform, null));

                chunks = RpgLargeMapChunkRenderer.BuildForMineCamp(parent.transform, map);
                Assert.That(chunks.ChunkCount, Is.EqualTo(6));
                Assert.That(chunks.LightHaloCount, Is.EqualTo(5));
                Assert.That(chunks.RootObject.transform.parent, Is.EqualTo(parent.transform));
            }
            finally
            {
                chunks?.Dispose();
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }
    }
}
