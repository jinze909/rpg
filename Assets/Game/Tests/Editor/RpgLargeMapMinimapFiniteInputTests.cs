using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgLargeMapMinimapFiniteInputTests
    {
        [Test]
        public void PlayerAndThreatMarkersRejectNonFiniteCoordinatesWithoutPoisoningRects()
        {
            var root = new GameObject("MinimapFiniteInputRoot");
            RpgLargeMapMinimapAndBeaconHud hud = null;
            try
            {
                hud = RpgLargeMapMinimapAndBeaconHud.BuildForSoundwell(
                    root.transform, SoundwellLargeMapLayout.Build());

                Vector2 initialPlayer = hud.PlayerMarkerRect.anchoredPosition;
                hud.UpdatePlayerPosition(new Vector2(float.NaN, 20f));
                hud.UpdatePlayerPosition(new Vector2(20f, float.PositiveInfinity));
                hud.UpdatePlayerPosition(new Vector2(float.NegativeInfinity, 10f));
                Assert.That(hud.PlayerMarkerRect.anchoredPosition, Is.EqualTo(initialPlayer));

                // Non-finite visible threat update must not activate or poison the threat marker.
                Assert.That(hud.IsThreatMarkerVisible, Is.False);
                hud.UpdateThreatMarker(new Vector2(float.NaN, 15f), visible: true);
                hud.UpdateThreatMarker(new Vector2(15f, float.PositiveInfinity), visible: true);
                Assert.That(hud.IsThreatMarkerVisible, Is.False);

                // Valid threat update activates and positions cleanly; subsequent non-finite updates preserve it.
                hud.UpdateThreatMarker(new Vector2(48f, 30f), visible: true, tint: Color.red);
                Assert.That(hud.IsThreatMarkerVisible, Is.True);
                Vector2 validThreatPos = hud.ThreatMarkerRect.anchoredPosition;

                hud.UpdateThreatMarker(new Vector2(float.NaN, 30f), visible: true);
                hud.UpdateThreatMarker(new Vector2(48f, float.NegativeInfinity), visible: true);
                Assert.That(hud.IsThreatMarkerVisible, Is.True);
                Assert.That(hud.ThreatMarkerRect.anchoredPosition, Is.EqualTo(validThreatPos));

                // Hiding the threat marker still works even when coordinates are non-finite.
                hud.UpdateThreatMarker(new Vector2(float.NaN, float.NaN), visible: false);
                Assert.That(hud.IsThreatMarkerVisible, Is.False);
            }
            finally
            {
                hud?.Dispose();
                Object.DestroyImmediate(root);
            }
        }
    }
}
