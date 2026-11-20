using System.IO;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class M04DepartureReadinessViewIconTests
    {
        private static M04DepartureManifest Plan(bool ready, bool stamped = true)
        {
            return new M04DepartureManifest(M03VoyageChoice.HanQiaoOfficial,
                ready ? "Inspected hull" : null, ready ? "Confirmed pilot" : null,
                ready ? 3 : 0, ready ? "Inspected tender" : null,
                ready, ready ? 18 : 0, 13,
                ready ? "Named landing receiver" : null, stamped, false);
        }

        [Test]
        public void StatusIconTracksBlockedReadyAndConfirmedStates()
        {
            var root = new GameObject("M04 readiness icon states");
            M04DepartureReadinessView view = null;
            string savedLanguage = SoundwellUiLocalization.Language;
            try
            {
                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.English);
                var blocked = Plan(false, stamped: false);
                view = new M04DepartureReadinessView(root.transform, blocked,
                    _ => { }, () => { });

                Assert.That(view.StatusIcon, Is.Not.Null);
                Assert.That(view.StatusIcon.gameObject.activeSelf, Is.True);
                Assert.That(view.StatusIcon.raycastTarget, Is.False);
                Assert.That(view.StatusIcon.preserveAspect, Is.True);
                Assert.That(view.StatusIcon.sprite, Is.Not.Null);
                Assert.That(view.StatusIcon.sprite.name,
                    Is.EqualTo(RpgStoryArtResolver.ResolveM07PassSprite(stamped: false).name));
                Assert.That(view.StatusIcon.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(view.StatusIcon.sprite.pixelsPerUnit, Is.EqualTo(16f));

                var ready = Plan(true);
                view.SetConfirmedManifest(ready);
                Assert.That(view.StatusIcon.gameObject.activeSelf, Is.True);
                Assert.That(view.StatusIcon.sprite, Is.Not.Null);
                Assert.That(view.StatusIcon.sprite.name,
                    Is.EqualTo(RpgStoryArtResolver.ResolveM03TidalChartSprite(open: true).name));
                Assert.That(view.StatusIcon.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(view.StatusIcon.sprite.pixelsPerUnit, Is.EqualTo(16f));

                view.ConfirmButton.onClick.Invoke();
                Assert.That(ready.DepartureConfirmed, Is.True);
                Assert.That(view.StatusIcon.gameObject.activeSelf, Is.True);
                Assert.That(view.StatusIcon.sprite, Is.Not.Null);
                Assert.That(view.StatusIcon.sprite.name,
                    Is.EqualTo(RpgStoryArtResolver.ResolveM07PassSprite(stamped: true).name));
                Assert.That(view.StatusIcon.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(view.StatusIcon.sprite.pixelsPerUnit, Is.EqualTo(16f));

                SoundwellUiLocalization.SetLanguage(SoundwellUiLocalization.Chinese);
                Assert.That(view.ConfirmButton.interactable, Is.False);
                Assert.That(view.StatusIcon.sprite.name,
                    Is.EqualTo(RpgStoryArtResolver.ResolveM07PassSprite(stamped: true).name));
            }
            finally
            {
                view?.Dispose();
                SoundwellUiLocalization.SetLanguage(savedLanguage);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void StatusIconDoesNotOverlapDetailsAndSurvivesViewportScaling()
        {
            var root = new GameObject("M04 readiness icon layout");
            M04DepartureReadinessView view = null;
            try
            {
                view = new M04DepartureReadinessView(root.transform, Plan(true),
                    _ => { }, () => { });

                RectTransform iconRect = view.StatusIcon.rectTransform;
                RectTransform detailsRect = view.Details.rectTransform;
                float iconRight = iconRect.anchoredPosition.x + iconRect.sizeDelta.x * 0.5f;
                float detailsLeft = detailsRect.anchoredPosition.x - detailsRect.sizeDelta.x * 0.5f;
                Assert.That(iconRight, Is.LessThan(detailsLeft));

                foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 32f / 9f })
                {
                    view.LayoutForViewportAspect(aspect);
                    Assert.That(view.StatusIcon.gameObject.activeSelf, Is.True);
                    Assert.That(view.StatusIcon.sprite, Is.Not.Null);
                }
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BlockedAndReadyReadinessScreensProduceActualGameViewEvidence()
        {
            var root = new GameObject("M04 readiness visual evidence");
            M04DepartureReadinessView view = null;
            try
            {
                view = new M04DepartureReadinessView(root.transform,
                    Plan(false, stamped: false), _ => { }, () => { });
                Canvas canvas = root.GetComponentInChildren<Canvas>(true);
                Assert.That(canvas, Is.Not.Null);
                CaptureUiFrame(canvas, "m04-readiness-blocked");

                view.SetConfirmedManifest(Plan(true));
                Assert.That(view.ConfirmButton.interactable, Is.True);
                CaptureUiFrame(canvas, "m04-readiness-ready");
            }
            finally
            {
                view?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CaptureUiFrame(Canvas canvas, string name)
        {
            // The repository's Unity P0 job archives these real UGUI camera
            // captures for review; a blank frame fails, not a false green.
            const int width = 960;
            const int height = 540;
            var cameraObject = new GameObject("M04 UI evidence camera", typeof(Camera));
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
                camera.cullingMask = 1 << 5;
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
                int visiblePixels = 0;
                foreach (Color32 pixel in frame.GetPixels32())
                    if (pixel.r > 24 || pixel.g > 24 || pixel.b > 24)
                        visiblePixels++;
                Assert.That(visiblePixels, Is.GreaterThan(16000),
                    "M04 screenshot cannot be an empty or black camera frame.");

                const string folder = "artifacts/p0/story";
                Directory.CreateDirectory(folder);
                string file = Path.Combine(folder, name + ".png");
                File.WriteAllBytes(file, frame.EncodeToPNG());
                Assert.That(new FileInfo(file).Length, Is.GreaterThan(2000),
                    "M04 state must produce a readable PNG screenshot.");
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
    }
}
