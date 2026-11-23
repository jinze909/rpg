using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xProceduralPropAtlasTests
    {
        [Test]
        public void AllPropKinds_GenerateValid16x16PointSprites_AcrossAllFourMaps()
        {
            using (var atlas = new RpgWorld500xProceduralPropAtlas())
            {
                var maps = (RpgWorldMapId[])Enum.GetValues(typeof(RpgWorldMapId));
                var kinds = (RpgWorld500xPropKind[])Enum.GetValues(typeof(RpgWorld500xPropKind));

                for (int m = 0; m < maps.Length; m++)
                {
                    Sprite[] sprites = atlas.PrewarmAll(maps[m]);
                    Assert.AreEqual(kinds.Length, sprites.Length);

                    for (int k = 0; k < sprites.Length; k++)
                    {
                        Sprite sprite = sprites[k];
                        Assert.IsNotNull(sprite, $"Sprite missing for {maps[m]}/{kinds[k]}");
                        Assert.IsNotNull(sprite.texture, $"Texture missing for {maps[m]}/{kinds[k]}");
                        Assert.AreEqual(RpgWorld500xProceduralPropAtlas.SpriteSize, sprite.texture.width);
                        Assert.AreEqual(RpgWorld500xProceduralPropAtlas.SpriteSize, sprite.texture.height);
                        Assert.AreEqual(RpgWorld500xProceduralPropAtlas.PixelsPerUnit, sprite.pixelsPerUnit);
                        Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode);
                        Assert.AreEqual(TextureWrapMode.Clamp, sprite.texture.wrapMode);

                        Color32[] pixels = sprite.texture.GetPixels32();
                        int opaqueCount = 0;
                        for (int p = 0; p < pixels.Length; p++)
                        {
                            if (pixels[p].a > 0)
                            {
                                opaqueCount++;
                            }
                        }

                        Assert.Greater(opaqueCount, 20, $"Sprite {maps[m]}/{kinds[k]} has too few opaque pixels.");
                    }
                }

                Assert.AreEqual(maps.Length * kinds.Length, atlas.CachedTextureCount);
                Assert.AreEqual(maps.Length * kinds.Length, atlas.CachedSpriteCount);
            }
        }

        [Test]
        public void GetSprite_CachesInstance_AndDifferentiatesRegions()
        {
            using (var atlas = new RpgWorld500xProceduralPropAtlas())
            {
                Sprite first = atlas.GetSprite(RpgWorld500xPropKind.FieldNpc, RpgWorldMapId.Soundwell);
                Sprite second = atlas.GetSprite(RpgWorld500xPropKind.FieldNpc, RpgWorldMapId.Soundwell);
                Assert.IsTrue(ReferenceEquals(first, second));
                Assert.AreEqual(1, atlas.CachedTextureCount);

                Sprite harborNpc = atlas.GetSprite(RpgWorld500xPropKind.FieldNpc, RpgWorldMapId.RedSaltHarbor);
                Assert.IsFalse(ReferenceEquals(first, harborNpc));
                Assert.AreEqual(2, atlas.CachedTextureCount);
                Assert.IsTrue(HasDifferentPixels(first.texture, harborNpc.texture));
            }
        }

        [Test]
        public void LootedAndOpenStates_VisuallyDifferFromClosedStates()
        {
            using (var atlas = new RpgWorld500xProceduralPropAtlas())
            {
                Assert.IsTrue(HasDifferentPixels(
                    atlas.GetSprite(RpgWorld500xPropKind.SupplyCrateClosed, RpgWorldMapId.Soundwell).texture,
                    atlas.GetSprite(RpgWorld500xPropKind.SupplyCrateLooted, RpgWorldMapId.Soundwell).texture));

                Assert.IsTrue(HasDifferentPixels(
                    atlas.GetSprite(RpgWorld500xPropKind.ShortcutGateClosed, RpgWorldMapId.MineCamp).texture,
                    atlas.GetSprite(RpgWorld500xPropKind.ShortcutGateOpen, RpgWorldMapId.MineCamp).texture));

                Assert.IsTrue(HasDifferentPixels(
                    atlas.GetSprite(RpgWorld500xPropKind.FastTravelBeaconUnlit, RpgWorldMapId.WhitewaterCrossing).texture,
                    atlas.GetSprite(RpgWorld500xPropKind.FastTravelBeaconLit, RpgWorldMapId.WhitewaterCrossing).texture));
            }
        }

        [Test]
        public void Dispose_DestroysAllNativeTexturesAndSprites_WithZeroLeak()
        {
            var atlas = new RpgWorld500xProceduralPropAtlas();
            var trackedObjects = new List<UnityEngine.Object>();

            Sprite[] prewarmed = atlas.PrewarmAll(RpgWorldMapId.Soundwell);
            for (int i = 0; i < prewarmed.Length; i++)
            {
                trackedObjects.Add(prewarmed[i]);
                trackedObjects.Add(prewarmed[i].texture);
            }

            Assert.Greater(atlas.CachedTextureCount, 0);
            atlas.Dispose();
            atlas.Dispose();

            Assert.IsTrue(atlas.IsDisposed);
            Assert.AreEqual(0, atlas.CachedTextureCount);
            Assert.AreEqual(0, atlas.CachedSpriteCount);

            for (int i = 0; i < trackedObjects.Count; i++)
            {
                Assert.IsTrue(trackedObjects[i] == null, "Expected UnityEngine.Object to be destroyed on Dispose.");
            }

            Assert.Throws<ObjectDisposedException>(() =>
                atlas.GetSprite(RpgWorld500xPropKind.StorySpot, RpgWorldMapId.Soundwell));
        }

        private static bool HasDifferentPixels(Texture2D a, Texture2D b)
        {
            Color32[] pixelsA = a.GetPixels32();
            Color32[] pixelsB = b.GetPixels32();
            if (pixelsA.Length != pixelsB.Length)
            {
                return true;
            }

            for (int i = 0; i < pixelsA.Length; i++)
            {
                if (pixelsA[i].r != pixelsB[i].r ||
                    pixelsA[i].g != pixelsB[i].g ||
                    pixelsA[i].b != pixelsB[i].b ||
                    pixelsA[i].a != pixelsB[i].a)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
