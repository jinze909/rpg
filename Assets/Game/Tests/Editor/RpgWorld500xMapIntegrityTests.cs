using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xMapIntegrityTests
    {
        [Test]
        public void AllFour500xMapsPassSpatialFootprintAndMakinomKeyIntegrityChecks()
        {
            List<RpgMapValidationIssue> issues = RpgWorld500xMapIntegrityValidator.ValidateAllFourMaps();
            Assert.That(issues, Is.Empty, string.Join("\n", issues));
        }

        [Test]
        public void KeyHygieneValidatorRejectsNarrativeCollisionsAndDuplicates()
        {
            var seen = new HashSet<string>();
            var issues = new List<RpgMapValidationIssue>();

            RpgWorld500xMapIntegrityValidator.ValidateMakinomKeyHygiene(
                "Soundwell", "rpg.return.world.cache.test01", seen, issues);
            Assert.That(issues, Is.Empty);

            RpgWorld500xMapIntegrityValidator.ValidateMakinomKeyHygiene(
                "Soundwell", "rpg.return.world.cache.test01", seen, issues);
            Assert.That(issues.Count, Is.EqualTo(1));
            Assert.That(issues[0].RuleId, Is.EqualTo("KEY-DUP"));

            issues.Clear();
            RpgWorld500xMapIntegrityValidator.ValidateMakinomKeyHygiene(
                "Soundwell", "rpg.return.m10.assembly-confirmed", seen, issues);
            Assert.That(issues.Count, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void StreamingChunkWindowAndMinimapTextureRespectBudgetsAndReleaseAllNativeAssets()
        {
            var root = new GameObject("MapIntegrityBudgetSweepRoot");
            RpgLargeMapChunkRenderer renderer = null;
            RpgWorld500xMinimapHud hud = null;
            try
            {
                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                RpgWorld500xMapLayout harbor = RpgWorld500xMapLayout.Build(RpgWorldMapId.RedSaltHarbor);
                renderer = harbor.CreateStreamingRenderer(root.transform, streamingRadiusChunks: 1,
                    tileSlices: new Sprite[] { null });
                var tracker = new RpgMapExplorationTracker(RpgWorldMapId.RedSaltHarbor);
                hud = new RpgWorld500xMinimapHud(
                    root.transform, harbor.Spec, harbor.GetTileId, harbor.UsesHarborPalette, tracker);

                Assert.That(hud.TextureWidth, Is.LessThanOrEqualTo(RpgWorld500xMapIntegrityValidator.MaxMinimapWidth));
                Assert.That(hud.TextureHeight, Is.LessThanOrEqualTo(RpgWorld500xMapIntegrityValidator.MaxMinimapHeight));

                Vector2[] sweep =
                {
                    new Vector2(360f, 540f),
                    new Vector2(1700f, 1260f),
                    new Vector2(2920f, 520f),
                    new Vector2(1568f, 1660f)
                };
                for (int i = 0; i < sweep.Length; i++)
                {
                    Assert.That(renderer.UpdateStreamingFocus(sweep[i]), Is.True);
                    Assert.That(renderer.ChunkCount, Is.LessThanOrEqualTo(9));
                    Assert.That(hud.UpdatePlayerAndCompass(sweep[i]), Is.True);
                }

                hud.Dispose();
                hud = null;
                renderer.Dispose();
                renderer = null;

                Assert.That(Resources.FindObjectsOfTypeAll<Texture2D>().Length, Is.LessThanOrEqualTo(texBefore));
                Assert.That(Resources.FindObjectsOfTypeAll<Sprite>().Length, Is.LessThanOrEqualTo(spriteBefore));
            }
            finally
            {
                hud?.Dispose();
                renderer?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
