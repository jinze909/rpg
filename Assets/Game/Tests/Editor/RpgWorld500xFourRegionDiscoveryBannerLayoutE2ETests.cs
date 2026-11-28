using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    [TestFixture]
    public sealed class RpgWorld500xFourRegionDiscoveryBannerLayoutE2ETests
    {
        [Test]
        public void AllFourRegions_DiscoveryBannerHud_EnforcesTwoRowNonRaycastLayoutAndSequentialQueue()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count, "Expected exactly 4 regions in 500x world scale.");

            var root = new GameObject("FourRegionDiscoveryBannerLayoutRoot");
            try
            {
                using (var banner = new RpgWorld500xDiscoveryBannerHud(root.transform))
                {
                    Transform panel = banner.RootObject.transform.Find("BannerPanel");
                    Assert.IsNotNull(panel);
                    Image panelImg = panel.GetComponent<Image>();
                    Assert.IsNotNull(panelImg);
                    Assert.IsFalse(panelImg.raycastTarget, "BannerPanel background must not intercept raycasts.");

                    Text headline = panel.Find("Headline").GetComponent<Text>();
                    Text subtitle = panel.Find("Subtitle").GetComponent<Text>();
                    Assert.AreEqual(new Vector2(0f, 0.5f), headline.rectTransform.anchorMin);
                    Assert.AreEqual(new Vector2(1f, 1f), headline.rectTransform.anchorMax);
                    Assert.AreEqual(new Vector2(0f, 0f), subtitle.rectTransform.anchorMin);
                    Assert.AreEqual(new Vector2(1f, 0.5f), subtitle.rectTransform.anchorMax);
                    Assert.AreEqual(new Vector2(12f, 2f), headline.rectTransform.offsetMin);
                    Assert.AreEqual(new Vector2(-12f, -2f), headline.rectTransform.offsetMax);
                    Assert.AreEqual(new Vector2(12f, 2f), subtitle.rectTransform.offsetMin);
                    Assert.AreEqual(new Vector2(-12f, -2f), subtitle.rectTransform.offsetMax);
                    Assert.IsFalse(headline.raycastTarget);
                    Assert.IsFalse(subtitle.raycastTarget);

                    for (int i = 0; i < specs.Count; i++)
                    {
                        RpgExpandedMapSpec spec = specs[i];
                        Assert.IsTrue(banner.Enqueue(spec.DisplayNameZh, "发现新地标 · " + spec.MapId));
                    }

                    for (int i = 0; i < specs.Count; i++)
                    {
                        RpgExpandedMapSpec spec = specs[i];
                        Assert.IsTrue(banner.IsShowing, $"Banner should be showing for region {spec.MapId}.");
                        Assert.AreEqual(spec.DisplayNameZh, headline.text);
                        Assert.AreEqual("发现新地标 · " + spec.MapId, subtitle.text);
                        banner.Step(4f);
                    }

                    Assert.IsFalse(banner.IsShowing, "Banner should hide after all 4 region toasts expire.");
                }

                Assert.AreEqual(0, root.transform.childCount, "Disposing DiscoveryBannerHud must leave zero UI children.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
