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
