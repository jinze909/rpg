using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xExpeditionReadinessHudPanelTests
    {
        [Test]
        public void ExpeditionReadinessHudPanel_AutoRestocksRecommendedSuppliesRespectsCanvasGroupLockAndZeroLeak()
        {
            var root = new GameObject("World500xExpeditionReadinessHudPanelTestRoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupPanel = new RpgWorld500xExpeditionReadinessHudPanel(root.transform, warmupBoot))
                {
                    warmupPanel.Show(0f);
                    warmupPanel.TryClickRefresh(1f);
                    warmupPanel.TryClickClose();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xExpeditionReadinessHudPanel(root.transform, boot))
                {
                    boot.Driver.Facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.GaleStorm);
                    boot.Driver.Facade.BountyAndSupply.AddSaltSilver(300);

                    Assert.IsTrue(panel.Show(5f));
                    Assert.AreEqual(4, panel.DisplayedRowCount);
                    Assert.AreEqual(RpgWorld500xReadinessGrade.Critical, panel.LastReport.Grade);
                    Assert.Greater(panel.LastReport.RecommendedSupplyIds.Count, 0);

                    // Parent CanvasGroup lock prevents restock, refresh, and close clicks cleanly.
                    CanvasGroup group = root.AddComponent<CanvasGroup>();
                    group.interactable = false;
                    Assert.IsFalse(panel.TryClickBuyRecommended());
                    Assert.IsFalse(panel.TryClickRefresh(6f));
                    Assert.IsFalse(panel.TryClickClose());
                    Assert.AreEqual(300, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                    group.interactable = true;

                    // One-click restock buys all missing recommended supplies and upgrades readiness.
                    Assert.IsTrue(panel.TryClickBuyRecommended());
                    Assert.Less(boot.Driver.Facade.BountyAndSupply.SaltSilverBalance, 300);
                    Assert.AreEqual(0, panel.LastReport.RecommendedSupplyIds.Count);
                    Assert.GreaterOrEqual(panel.LastReport.Score, 70);
                    Assert.IsTrue(panel.FeedbackText.Contains("已补购"));
                    Assert.IsFalse(panel.TryClickBuyRecommended());

                    Assert.IsTrue(panel.TryClickClose());
                    Assert.IsFalse(panel.IsVisible);
                }

                Assert.AreEqual(0, root.transform.childCount);
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
