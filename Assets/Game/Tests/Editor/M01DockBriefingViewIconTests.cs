using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class M01DockBriefingViewIconTests
    {
        private GameObject root;
        private M01DockBriefingView view;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("M01 dock briefing icon test");
        }

        [TearDown]
        public void TearDown()
        {
            view?.Dispose();
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void StageIconTracksWarehouseCakeChartLeadsAndHidesOnFinish()
        {
            RpgPixelArtManifest manifest = RpgPixelArtManifest.LoadDefault();
            Assert.That(manifest, Is.Not.Null);

            view = new M01DockBriefingView(root.transform, () => { });
            AssertStageIcon(view.StageIcon, manifest.M07CargoCoverCrateClosed);

            view.GetChoiceButton(2).onClick.Invoke();
            Assert.That(view.Stage, Is.EqualTo(M01DockStage.OfferingCake));
            AssertStageIcon(view.StageIcon, manifest.M03TidalChartFolded);

            view.AdvanceButton.onClick.Invoke();
            Assert.That(view.Stage, Is.EqualTo(M01DockStage.ReviewingRoutes));
            AssertStageIcon(view.StageIcon, manifest.M03TidalChartOpen);

            view.GetChoiceButton(0).onClick.Invoke();
            AssertStageIcon(view.StageIcon, manifest.M02HarborOfficialSeal);

            view.GetChoiceButton(1).onClick.Invoke();
            AssertStageIcon(view.StageIcon, manifest.M03OldStandardRolled);

            view.GetChoiceButton(2).onClick.Invoke();
            AssertStageIcon(view.StageIcon, manifest.FogSignalLanternOn);

            view.AdvanceButton.onClick.Invoke();
            Assert.That(view.Stage, Is.EqualTo(M01DockStage.Complete));
            Assert.That(view.StageIcon.sprite, Is.Null);
            Assert.That(view.StageIcon.enabled, Is.False);
            Assert.That(view.StageIcon.gameObject.activeSelf, Is.False);
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
