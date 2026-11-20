using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class M04XiaodouFarewellViewIconTests
    {
        private GameObject root;
        private M04XiaodouFarewellView view;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("M04 Xiaodou farewell icon test");
        }

        [TearDown]
        public void TearDown()
        {
            view?.Dispose();
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void StageIconTracksGreetingChoiceResponsesAndHidesOnGoodbye()
        {
            RpgPixelArtManifest manifest = RpgPixelArtManifest.LoadDefault();
            Assert.That(manifest, Is.Not.Null);

            view = new M04XiaodouFarewellView(root.transform, true, _ => { });
            AssertStageIcon(view.StageIcon, manifest.FogSignalLanternOn);

            view.AdvanceButton.onClick.Invoke();
            Assert.That(view.Encounter.Stage, Is.EqualTo(M04FarewellStage.ChoosingTrueLine));
            AssertStageIcon(view.StageIcon, manifest.M02CableFlatUncharredCut);

            view.GetChoiceButton(0).onClick.Invoke();
            Assert.That(view.Encounter.Stage, Is.EqualTo(M04FarewellStage.HearingResponse));
            AssertStageIcon(view.StageIcon, manifest.M02IdentityDocumentUnsealed);

            view.AdvanceButton.onClick.Invoke();
            Assert.That(view.Encounter.Stage, Is.EqualTo(M04FarewellStage.Complete));
            Assert.That(view.StageIcon.sprite, Is.Null);
            Assert.That(view.StageIcon.enabled, Is.False);
            Assert.That(view.StageIcon.gameObject.activeSelf, Is.False);
            view.Dispose();

            view = new M04XiaodouFarewellView(root.transform, false, _ => { });
            view.AdvanceButton.onClick.Invoke();
            view.GetChoiceButton(1).onClick.Invoke();
            AssertStageIcon(view.StageIcon, manifest.FogSignalLanternOff);
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
