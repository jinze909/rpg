using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgLargeMapMinimapLifecycleTests
    {
        private GameObject root;
        private RpgLargeMapMinimapAndBeaconHud hud;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("MinimapLifecycleRoot");
            hud = RpgLargeMapMinimapAndBeaconHud.BuildForSoundwell(
                root.transform, SoundwellLargeMapLayout.Build());
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
        public void ExternalParentTeardown_SetsIsDisposedImmediately_AndUpdateReleasesNativeTexture()
        {
            Texture2D tex = hud.MinimapTexture;
            Assert.That(tex, Is.Not.Null);
            Assert.That(hud.IsDisposed, Is.False);

            hud.UpdateThreatMarker(new Vector2(20f, 20f), visible: true);
            Assert.That(hud.IsThreatMarkerVisible, Is.True);

            UnityEngine.Object.DestroyImmediate(root);
            root = null;

            Assert.That(hud.IsDisposed, Is.True);
            Assert.That(hud.IsThreatMarkerVisible, Is.False);

            Assert.DoesNotThrow(() => hud.UpdatePlayerPosition(new Vector2(10f, 10f)));
            Assert.That(tex == null, Is.True,
                "External teardown must release the baked minimap Texture2D when UpdatePlayerPosition runs.");
        }

        [Test]
        public void ExternalChildTeardown_DisposesAndRejectsBeaconUpdates()
        {
            Texture2D tex = hud.MinimapTexture;
            UnityEngine.Object.DestroyImmediate(hud.TitleLabel.gameObject);

            Assert.That(hud.IsDisposed, Is.True);
            Assert.That(hud.SetBeaconCompleted("soundwell.bell", true), Is.False);
            Assert.DoesNotThrow(() => hud.UpdateThreatMarker(new Vector2(12f, 12f), visible: true));
            Assert.That(tex == null, Is.True);
        }

        [Test]
        public void LayoutForViewportAspect_IgnoresNonFiniteAndNonPositiveAspects()
        {
            Vector3 initialScale = hud.Panel.localScale;
            Assert.DoesNotThrow(() => hud.LayoutForViewportAspect(float.NaN));
            Assert.DoesNotThrow(() => hud.LayoutForViewportAspect(float.PositiveInfinity));
            Assert.DoesNotThrow(() => hud.LayoutForViewportAspect(float.NegativeInfinity));
            Assert.DoesNotThrow(() => hud.LayoutForViewportAspect(0f));
            Assert.DoesNotThrow(() => hud.LayoutForViewportAspect(-2f));
            Assert.That(hud.Panel.localScale, Is.EqualTo(initialScale));
        }

        [Test]
        public void Dispose_IsIdempotent_AndClearsBeaconsAndNativeTexture()
        {
            Texture2D tex = hud.MinimapTexture;
            Assert.DoesNotThrow(() => hud.Dispose());
            Assert.DoesNotThrow(() => hud.Dispose());
            Assert.That(hud.IsDisposed, Is.True);
            Assert.That(hud.BeaconCount, Is.Zero);
            Assert.That(hud.ActiveBeaconCount, Is.Zero);
            Assert.That(tex == null, Is.True);
            Assert.That(root.transform.childCount, Is.Zero);
        }
    }
}
