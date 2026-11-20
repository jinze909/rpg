using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class M02HarborDeskViewIconTests
    {
        private GameObject root;
        private M02HarborDeskView view;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("M02 harbor desk icon test");
        }

        [TearDown]
        public void TearDown()
        {
            view?.Dispose();
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void StageIconTracksHarborDeskTopicsAndHidesOnLeave()
        {
            RpgPixelArtManifest manifest = RpgPixelArtManifest.LoadDefault();
            Assert.That(manifest, Is.Not.Null);

            view = new M02HarborDeskView(root.transform,
                M01PensionChoice.RegisterLivingIdentity, _ => { });
            AssertStageIcon(view.StageIcon, manifest.M02IdentityDocumentUnsealed);

            view.GetActionButton(0).onClick.Invoke();
            AssertStageIcon(view.StageIcon, manifest.M02HarborOfficialSeal);

            view.GetActionButton(1).onClick.Invoke();
            AssertStageIcon(view.StageIcon, manifest.M02BridgeKeeperOpenLedger);

            view.GetActionButton(2).onClick.Invoke();
            AssertStageIcon(view.StageIcon, manifest.M03OldStandardRolled);

            view.GetActionButton(3).onClick.Invoke();
            AssertStageIcon(view.StageIcon, manifest.M07InspectionPendingPass);

            view.LeaveButton.onClick.Invoke();
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
