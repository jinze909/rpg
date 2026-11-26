using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionExplorationHostE2ETests
    {
        [Test]
        public void AllFourRegions_ExplorationHostNormalizesExtremeInputsRejectsNonFiniteAndDisposesWithZeroLeak()
        {
            var root = new GameObject("World500xFourRegionExplorationHostE2ERoot");
            try
            {
                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                foreach (RpgExpandedMapSpec spec in RpgExpandedWorldScale.AllSpecs)
                {
                    using (var host = new RpgWorld500xExplorationHost(
                        root.transform,
                        spec.MapId,
                        streamingRadiusChunks: 1,
                        tileSlices: new Sprite[] { null }))
                    {
                        Vector2 start = host.PlayerPosition;
                        Assert.GreaterOrEqual(start.x, 0f);
                        Assert.GreaterOrEqual(start.y, 0f);
                        Assert.Less(start.x, spec.ExpandedWidth);
                        Assert.Less(start.y, spec.ExpandedHeight);

                        Assert.IsTrue(host.StepMovement(Vector2.right, 0.2f));
                        Vector2 normalEnd = host.PlayerPosition;
                        Assert.IsTrue(host.TryPlaceAtWalkablePosition(start));

                        // Extreme finite input matches unit direction travel without overflow.
                        Assert.IsTrue(host.StepMovement(new Vector2(float.MaxValue, 0f), 0.2f));
                        Assert.AreEqual(normalEnd.x, host.PlayerPosition.x, 0.001f);
                        Assert.AreEqual(normalEnd.y, host.PlayerPosition.y, 0.001f);
                        Assert.AreEqual(Vector2.right, host.Facing);

                        // Non-finite inputs fail closed without mutating position or facing.
                        Vector2 beforeInvalid = host.PlayerPosition;
                        Assert.IsFalse(host.StepMovement(new Vector2(float.NaN, 0f), 0.2f));
                        Assert.IsFalse(host.StepMovement(new Vector2(float.PositiveInfinity, 1f), 0.2f));
                        Assert.IsFalse(host.StepMovement(Vector2.right, float.NaN));
                        Assert.AreEqual(beforeInvalid, host.PlayerPosition);
                        Assert.AreEqual(Vector2.right, host.Facing);
                    }

                    Assert.AreEqual(0, root.transform.childCount);
                }

                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
                Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
