using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class RpgLargeMapMinimapAndBeaconHudTests
    {
        [Test]
        public void MinimapBakesPointFilteredTexturesTracksPlayerAndTogglesBeaconsAcrossAllThreeMaps()
        {
            var root = new GameObject("Large map minimap test");
            string prevLang = SoundwellUiLocalization.Language;
            RpgLargeMapMinimapAndBeaconHud soundwell = null;
            RpgLargeMapMinimapAndBeaconHud harbor = null;
            RpgLargeMapMinimapAndBeaconHud whitewater = null;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);

                soundwell = RpgLargeMapMinimapAndBeaconHud.BuildForSoundwell(
                    root.transform, SoundwellLargeMapLayout.Build());
                Assert.That(soundwell.MinimapTexture.width, Is.EqualTo(RpgWorldMapScale.SoundwellMinWidth));
                Assert.That(soundwell.MinimapTexture.height, Is.EqualTo(RpgWorldMapScale.SoundwellMinHeight));
                Assert.That(soundwell.MinimapTexture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(soundwell.BeaconCount, Is.EqualTo(4));
                Assert.That(soundwell.ActiveBeaconCount, Is.EqualTo(4));
                Assert.That(soundwell.TitleLabel.text, Does.Contain("钟岭声井"));
                Assert.That(root.GetComponentInChildren<GraphicRaycaster>(), Is.Null,
                    "Minimap HUD must remain visual-only and never intercept clicks.");

                Vector2 initialMarker = soundwell.PlayerMarkerRect.anchoredPosition;
                soundwell.UpdatePlayerPosition(new Vector2(80.5f, 34.5f));
                Assert.That(soundwell.PlayerMarkerRect.anchoredPosition.x,
                    Is.GreaterThan(initialMarker.x + 80f));

                Assert.That(soundwell.SetBeaconCompleted("soundwell.bell", true), Is.True);
                Assert.That(soundwell.ActiveBeaconCount, Is.EqualTo(3));
                Assert.That(soundwell.SetBeaconCompleted("unknown.beacon", true), Is.False);

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                Assert.That(soundwell.TitleLabel.text, Does.Contain("Soundwell"));

                harbor = RpgLargeMapMinimapAndBeaconHud.BuildForRedSaltHarbor(
                    root.transform, RedSaltHarborMapLayout.Build());
                Assert.That(harbor.MinimapTexture.width, Is.EqualTo(RpgWorldMapScale.RedSaltHarborMinWidth));
                Assert.That(harbor.MinimapTexture.height, Is.EqualTo(RpgWorldMapScale.RedSaltHarborMinHeight));
                Assert.That(harbor.BeaconCount, Is.EqualTo(5));
                Assert.That(harbor.SetBeaconCompleted("m06.mine-camp-gate", true), Is.False,
                    "The next chapter's inactive gate cannot count as a current M05 objective.");

                whitewater = RpgLargeMapMinimapAndBeaconHud.BuildForWhitewaterCrossing(
                    root.transform, WhitewaterCrossingMapLayout.Build());
                Assert.That(whitewater.MinimapTexture.width, Is.EqualTo(RpgWorldMapScale.WhitewaterCrossingMinWidth));
                Assert.That(whitewater.MinimapTexture.height, Is.EqualTo(RpgWorldMapScale.WhitewaterCrossingMinHeight));
                Assert.That(whitewater.BeaconCount, Is.EqualTo(3));
            }
            finally
            {
                soundwell?.Dispose();
                harbor?.Dispose();
                whitewater?.Dispose();
                SoundwellUiLocalization.SetLanguage(prevLang);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NullArgumentsRejectedAndDisposeCleansUpCanvasAndTextures()
        {
            var root = new GameObject("Minimap teardown test");
            RpgLargeMapMinimapAndBeaconHud hud = null;
            try
            {
                Assert.Throws<ArgumentNullException>(() =>
                    RpgLargeMapMinimapAndBeaconHud.BuildForSoundwell(null, SoundwellLargeMapLayout.Build()));
                Assert.Throws<ArgumentNullException>(() =>
                    RpgLargeMapMinimapAndBeaconHud.BuildForRedSaltHarbor(root.transform, null));
                Assert.Throws<ArgumentNullException>(() =>
                    RpgLargeMapMinimapAndBeaconHud.BuildForWhitewaterCrossing(root.transform, null));

                hud = RpgLargeMapMinimapAndBeaconHud.BuildForSoundwell(
                    root.transform, SoundwellLargeMapLayout.Build());
                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                    Assert.DoesNotThrow(() => hud.LayoutForViewportAspect(aspect));

                hud.Dispose();
                Assert.That(hud.IsDisposed, Is.True);
                Assert.That(root.transform.childCount, Is.Zero);
            }
            finally
            {
                hud?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
