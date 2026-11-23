using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xMinimapHudTests
    {
        private GameObject root;
        private RpgWorld500xMinimapHud hud;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("World500xMinimapHudTestRoot");
        }

        [TearDown]
        public void TearDown()
        {
            hud?.Dispose();
            hud = null;
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void Downsampled500xMinimapRendersFixedTextureTracksDiscoveryOverviewCompassAndDisposesCleanly()
        {
            RpgExpandedMapSpec spec = RpgExpandedWorldScale.GetSpec(RpgWorldMapId.RedSaltHarbor);
            var tracker = new RpgMapExplorationTracker(RpgWorldMapId.RedSaltHarbor);

            hud = new RpgWorld500xMinimapHud(
                root.transform,
                spec,
                (x, y) => y < 420 ? (int)HarborTileKind.WaterDeep : (int)HarborTileKind.QuayStone,
                useHarborPalette: true,
                tracker: tracker);

            Assert.That(hud.TextureWidth, Is.EqualTo(RpgWorld500xMinimapHud.DefaultTextureWidth));
            Assert.That(hud.TextureHeight, Is.EqualTo(RpgWorld500xMinimapHud.DefaultTextureHeight));
            Assert.That(hud.MinimapTexture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(hud.PinCount, Is.EqualTo(tracker.TotalMarkerCount));

            // Compact mode hides undiscovered non-main-story markers.
            int compactInitial = hud.VisiblePinCount;
            Assert.That(compactInitial, Is.LessThan(hud.PinCount));

            // Discovering a landmark and tracking a quest updates compass and visible pins.
            Assert.That(tracker.DiscoverMarkersInRange(new Vector2(360f, 540f)), Is.EqualTo(1));
            Assert.That(tracker.TrySetTrackedQuest("q.rsh.compare-salt-prices"), Is.True);
            Assert.That(hud.UpdatePlayerAndCompass(new Vector2(360f, 540f), "外港浅水泊位"), Is.True);
            Assert.That(hud.VisiblePinCount, Is.GreaterThan(compactInitial));
            Assert.That(hud.LastCompassText, Does.Contain("外港浅水泊位"));
            Assert.That(hud.LastCompassText, Does.Contain("摸底赤盐行情"));
            Assert.That(hud.LastCompassText, Does.Contain("东北"));

            // Overview mode reveals all landmark pins across the 500x map.
            Assert.That(hud.SetOverviewMode(true), Is.True);
            Assert.That(hud.IsOverviewMode, Is.True);
            Assert.That(hud.VisiblePinCount, Is.EqualTo(hud.PinCount));

            // Non-finite coordinates fail closed without corrupting HUD.
            Assert.That(hud.UpdatePlayerAndCompass(new Vector2(float.NaN, 100f)), Is.False);
            Assert.That(hud.UpdateThreatMarker(new Vector2(float.PositiveInfinity, 100f), true), Is.False);
            Assert.That(hud.UpdateThreatMarker(new Vector2(float.NaN, 0f), false), Is.True);

            // External parent teardown + Dispose must release native Texture2D cleanly.
            Texture2D tex = hud.MinimapTexture;
            UnityEngine.Object.DestroyImmediate(root);
            root = null;
            Assert.That(hud.IsDisposed, Is.True);
            Assert.DoesNotThrow(() => hud.Dispose());
            Assert.That(tex == null, Is.True, "Owned minimap Texture2D must be destroyed on Dispose.");
        }
    }
}
