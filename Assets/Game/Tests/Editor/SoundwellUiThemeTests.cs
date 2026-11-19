using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class SoundwellUiThemeTests
    {
        [Test]
        public void CreateOverlayCanvasConfiguresStandardResolutionAndSafeTeardown()
        {
            var root = new GameObject("UiTheme canvas test");
            GameObject canvasObject = null;
            try
            {
                Assert.Throws<ArgumentNullException>(() =>
                    SoundwellUiTheme.CreateOverlayCanvas(null, "Missing parent", 100, out _));
                Assert.Throws<ArgumentException>(() =>
                    SoundwellUiTheme.CreateOverlayCanvas(root.transform, " ", 100, out _));

                canvasObject = SoundwellUiTheme.CreateOverlayCanvas(
                    root.transform, "Shared test canvas", 132, out RectTransform authored);
                var canvas = canvasObject.GetComponent<Canvas>();
                var scaler = canvasObject.GetComponent<CanvasScaler>();

                Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                Assert.That(canvas.sortingOrder, Is.EqualTo(132));
                Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(960f, 540f)));
                Assert.That(authored.sizeDelta, Is.EqualTo(new Vector2(960f, 540f)));

                SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
                Assert.That(canvasObject == null, Is.True);
                SoundwellUiTheme.SafeDestroyCanvas(null);
            }
            finally
            {
                SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CreateButtonAndLabelUseThemeDefaultsWithoutRaycastLeaks()
        {
            var root = new GameObject("UiTheme controls test");
            GameObject canvasObject = null;
            try
            {
                canvasObject = SoundwellUiTheme.CreateOverlayCanvas(
                    root.transform, "Controls canvas", 133, out RectTransform authored);
                Text heading = SoundwellUiTheme.CreateLabel(
                    authored, 22, new Vector2(0f, 180f), new Vector2(600f, 40f));
                Button action = SoundwellUiTheme.CreateButton(
                    authored, "Shared action button", new Vector2(0f, -120f),
                    new Vector2(300f, 44f), 17, out Text buttonLabel);

                Assert.That(heading.raycastTarget, Is.False);
                Assert.That(heading.resizeTextForBestFit, Is.True);
                Assert.That(action.targetGraphic, Is.Not.Null);
                Assert.That(buttonLabel.raycastTarget, Is.False);
                Assert.That(buttonLabel.resizeTextMaxSize, Is.EqualTo(17));
            }
            finally
            {
                SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ApplyViewportAspectScaleHandlesPortraitUltrawideAndInvalidValues()
        {
            var root = new GameObject("UiTheme aspect test");
            GameObject canvasObject = null;
            try
            {
                canvasObject = SoundwellUiTheme.CreateOverlayCanvas(
                    root.transform, "Aspect canvas", 134, out RectTransform authored);

                SoundwellUiTheme.ApplyViewportAspectScale(authored, 16f / 9f);
                Assert.That(authored.localScale.x, Is.EqualTo(1f).Within(0.001f));

                SoundwellUiTheme.ApplyViewportAspectScale(authored, 9f / 16f);
                Assert.That(authored.localScale.x, Is.LessThan(1f));
                Assert.That(authored.localScale.x, Is.GreaterThan(0f));

                float portraitScale = authored.localScale.x;
                SoundwellUiTheme.ApplyViewportAspectScale(authored, 0f);
                SoundwellUiTheme.ApplyViewportAspectScale(authored, float.NaN);
                SoundwellUiTheme.ApplyViewportAspectScale(authored, float.PositiveInfinity);
                Assert.That(authored.localScale.x, Is.EqualTo(portraitScale));
            }
            finally
            {
                SoundwellUiTheme.SafeDestroyCanvas(canvasObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
