using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class RpgStoryArtResolverFiniteInputTests
    {
        [Test]
        public void CreateOrUpdatePixelIconSanitizesNonFinitePositionAndSize()
        {
            var root = new GameObject("StoryArtFiniteIconRoot", typeof(RectTransform));
            try
            {
                var parent = (RectTransform)root.transform;
                Sprite seal = RpgStoryArtResolver.ResolveM02OfficialSeal();

                Image icon = RpgStoryArtResolver.CreateOrUpdatePixelIcon(
                    parent,
                    "SanitizedSeal",
                    new Vector2(float.NaN, float.PositiveInfinity),
                    new Vector2(float.NegativeInfinity, float.NaN),
                    seal);

                Assert.That(icon, Is.Not.Null);
                Assert.That(icon.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(icon.rectTransform.sizeDelta, Is.EqualTo(Vector2.zero));

                // Subsequent valid coordinates update the same icon cleanly.
                Image updated = RpgStoryArtResolver.CreateOrUpdatePixelIcon(
                    parent,
                    "SanitizedSeal",
                    new Vector2(18.6f, -12.4f),
                    new Vector2(48f, 48f),
                    seal);
                Assert.That(updated, Is.SameAs(icon));
                Assert.That(updated.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(19f, -12f)));
                Assert.That(updated.rectTransform.sizeDelta, Is.EqualTo(new Vector2(48f, 48f)));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
