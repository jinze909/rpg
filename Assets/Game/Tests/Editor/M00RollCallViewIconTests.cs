using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class M00RollCallViewIconTests
    {
        private GameObject root;
        private M00RollCallView view;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("M00 roll call icon test");
        }

        [TearDown]
        public void TearDown()
        {
            view?.Dispose();
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void StageIconTracksRollCallOutcomeAndHidesOnContinueHandoff()
        {
            RpgPixelArtManifest manifest = RpgPixelArtManifest.LoadDefault();
            Assert.That(manifest, Is.Not.Null);

            var answered = new M00RollCallSequence();
            answered.Begin();
            view = new M00RollCallView(root.transform, answered, _ => { });
            AssertStageIcon(view.StageIcon, manifest.M02BridgeKeeperOpenLedger);

            view.GetChoiceButton(0).onClick.Invoke();
            AssertStageIcon(view.StageIcon, manifest.M02IdentityDocumentUnsealed);

            view.ContinueButton.onClick.Invoke();
            Assert.That(view.StageIcon.sprite, Is.Null);
            Assert.That(view.StageIcon.enabled, Is.False);
            Assert.That(view.StageIcon.gameObject.activeSelf, Is.False);
            view.Dispose();

            var silent = new M00RollCallSequence();
            silent.Begin();
            view = new M00RollCallView(root.transform, silent, _ => { });
            AssertStageIcon(view.StageIcon, manifest.M02BridgeKeeperOpenLedger);
            view.GetChoiceButton(1).onClick.Invoke();
            view.AdvanceAfterUnansweredCall();
            view.GetChoiceButton(1).onClick.Invoke();
            view.AdvanceAfterUnansweredCall();
            view.GetChoiceButton(1).onClick.Invoke();
            AssertStageIcon(view.StageIcon, manifest.M02IdentityDocumentSealed);
            view.Dispose();

            var left = new M00RollCallSequence();
            left.Begin();
            view = new M00RollCallView(root.transform, left, _ => { });
            view.GetChoiceButton(2).onClick.Invoke();
            AssertStageIcon(view.StageIcon, manifest.FogSignalLanternOff);
            view.AdvanceAfterUnansweredCall();
            view.AdvanceAfterUnansweredCall();
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
