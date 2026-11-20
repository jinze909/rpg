using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class M02BridgeInquiryViewIconTests
    {
        private GameObject root;
        private M02BridgeInquiryView view;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("M02 bridge inquiry icon test");
        }

        [TearDown]
        public void TearDown()
        {
            view?.Dispose();
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void StageIconTracksEvidenceRoomsCluesAndHidesOnFinish()
        {
            RpgPixelArtManifest manifest = RpgPixelArtManifest.LoadDefault();
            Assert.That(manifest, Is.Not.Null);

            var inquiry = new M02BridgeInquiry();
            inquiry.Begin();
            view = new M02BridgeInquiryView(root.transform, inquiry, _ => { });
            AssertStageIcon(view.StageIcon, manifest.M07CargoCoverCrateClosed);

            view.GetEvidenceButton(0).onClick.Invoke();
            AssertStageIcon(view.StageIcon, manifest.M02CableFlatUncharredCut);

            view.GetEvidenceButton(1).onClick.Invoke();
            AssertStageIcon(view.StageIcon, manifest.M02BridgeKeeperOpenLedger);

            view.GetEvidenceButton(2).onClick.Invoke();
            AssertStageIcon(view.StageIcon, manifest.FogSignalLanternOn);

            view.ContinueButton.onClick.Invoke();
            Assert.That(inquiry.Location, Is.EqualTo(M02EvidenceLocation.HarborArchive));
            AssertStageIcon(view.StageIcon, manifest.M02HarborOfficialSeal);

            view.GetEvidenceButton(0).onClick.Invoke();
            AssertStageIcon(view.StageIcon, manifest.M02OverwrittenBattleReport);

            view.ContinueButton.onClick.Invoke();
            Assert.That(inquiry.Location, Is.EqualTo(M02EvidenceLocation.Finished));
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
