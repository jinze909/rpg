using System.IO;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class M03LuJunViewIconTests
    {
        private GameObject root;
        private M03LuJunView view;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("M03 Lu Jun icon test");
        }

        [TearDown]
        public void TearDown()
        {
            view?.Dispose();
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void StageIconTracksCampRollCallConfessionVoyageAndHidesOnFinish()
        {
            RpgPixelArtManifest manifest = RpgPixelArtManifest.LoadDefault();
            Assert.That(manifest, Is.Not.Null);

            view = new M03LuJunView(root.transform, M02BridgeEvidence.FerrymanLog,
                true, _ => { });
            AssertStageIcon(view.StageIcon, manifest.M03CampCoals);

            for (int beat = 1; beat <= 3; beat++)
            {
                view.AdvanceButton.onClick.Invoke();
                AssertStageIcon(view.StageIcon, manifest.M03LuJunRollCall);
            }

            view.AdvanceButton.onClick.Invoke();
            Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.ChoosingOpening));
            AssertStageIcon(view.StageIcon, manifest.M03LuJunStanding);

            view.GetChoiceButton(0).onClick.Invoke();
            Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.RespondingToConfession));
            AssertStageIcon(view.StageIcon, manifest.M02OverwrittenBattleReport);

            view.GetChoiceButton(1).onClick.Invoke();
            Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.ReplyingToSecrecyRequest));
            AssertStageIcon(view.StageIcon, manifest.M03OldStandardRolled);

            view.GetChoiceButton(2).onClick.Invoke();
            Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.ChoosingVoyage));
            AssertStageIcon(view.StageIcon, manifest.M03TidalChartOpen);

            view.GetChoiceButton(2).onClick.Invoke();
            Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.Complete));
            Assert.That(view.StageIcon.sprite, Is.Null);
            Assert.That(view.StageIcon.enabled, Is.False);
            Assert.That(view.StageIcon.gameObject.activeSelf, Is.False);
        }


        [Test]
        public void CampAndConfessionRenderNonblankGameViewEvidence()
        {
            view = new M03LuJunView(root.transform, M02BridgeEvidence.FerrymanLog,
                true, _ => { });
            Canvas canvas = root.GetComponentInChildren<Canvas>(true);
            Assert.That(canvas, Is.Not.Null, "The authored M03 view needs a real UGUI canvas.");

            CaptureUiFrame(canvas, "m03-lu-jun-camp");
            for (int i = 0; i < 4; i++)
                view.AdvanceButton.onClick.Invoke();
            Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.ChoosingOpening));
            view.GetChoiceButton(0).onClick.Invoke();
            Assert.That(view.Encounter.Stage, Is.EqualTo(M03LuJunStage.RespondingToConfession));
            CaptureUiFrame(canvas, "m03-lu-jun-confession");
        }

        private static void CaptureUiFrame(Canvas canvas, string name)
        {
            // A real camera render (not a mock UI/state image) is archived by
            // the existing Unity P0 artifacts job for visual inspection.
            const int width = 960;
            const int height = 540;
            var cameraObject = new GameObject("M03 UI evidence camera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            RenderTexture target = RenderTexture.GetTemporary(width, height, 24);
            RenderTexture previousActive = RenderTexture.active;
            RenderMode previousMode = canvas.renderMode;
            Camera previousCamera = canvas.worldCamera;
            float previousPlane = canvas.planeDistance;
            var frame = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                // The runtime-created overlay and its graphics inherit the
                // actual authored canvas layer (normally Default), not the
                // built-in UI layer. Culling only layer 5 rendered black evidence.
                camera.cullingMask = 1 << canvas.gameObject.layer;
                camera.orthographic = true;
                camera.orthographicSize = 5f;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.targetTexture = target;

                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
                camera.Render();

                RenderTexture.active = target;
                frame.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                frame.Apply();

                // Refuse to call a blank/failed render a GameView screenshot.
                int visiblePixels = 0;
                foreach (Color32 pixel in frame.GetPixels32())
                    if (pixel.r > 24 || pixel.g > 24 || pixel.b > 24)
                        visiblePixels++;
                Assert.That(visiblePixels, Is.GreaterThan(16000),
                    "M03 GameView evidence must contain visibly rendered UGUI, not a black frame.");

                const string folder = "artifacts/p0/story";
                Directory.CreateDirectory(folder);
                string file = Path.Combine(folder, name + ".png");
                File.WriteAllBytes(file, frame.EncodeToPNG());
                Assert.That(new FileInfo(file).Length, Is.GreaterThan(2000),
                    "M03 game camera must produce an inspectable PNG artifact.");
            }
            finally
            {
                canvas.renderMode = previousMode;
                canvas.worldCamera = previousCamera;
                canvas.planeDistance = previousPlane;
                camera.targetTexture = null;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(frame);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private static void AssertStageIcon(Image icon, Sprite expected)
        {
            Assert.That(icon, Is.Not.Null);
            Assert.That(expected, Is.Not.Null);
            Assert.That(icon.sprite, Is.EqualTo(expected));
            Assert.That(icon.preserveAspect, Is.True);
            Assert.That(icon.raycastTarget, Is.False);
            Assert.That(icon.enabled, Is.True);
            Assert.That(icon.gameObject.activeSelf, Is.True);
            Assert.That(icon.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(icon.sprite.pixelsPerUnit, Is.EqualTo(RpgWorldMapScale.PixelsPerUnit));
        }
    }
}
