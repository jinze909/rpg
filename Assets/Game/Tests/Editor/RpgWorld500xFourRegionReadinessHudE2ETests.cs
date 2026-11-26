using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionReadinessHudE2ETests
    {
        [Test]
        public void ExpeditionReadinessHudPanel_AutoProvisionsSafestRouteAndRefreshesAcrossAllFourRegionsWithZeroLeak()
        {
            var root = new GameObject("World500xFourRegionReadinessE2ERoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.WhitewaterCrossing,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupPanel = new RpgWorld500xExpeditionReadinessHudPanel(root.transform, warmupBoot))
                {
                    warmupPanel.Show(0f);
                    warmupPanel.Hide();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.WhitewaterCrossing,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xExpeditionReadinessHudPanel(root.transform, boot))
                {
                    var facade = boot.Driver.Facade;
                    facade.Atmosphere.SetWeather(RpgWorldMapId.WhitewaterCrossing, RpgWorld500xWeatherKind.Clear);
                    facade.Atmosphere.SetWeather(RpgWorldMapId.MineCamp, RpgWorld500xWeatherKind.Clear);
                    facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.Clear);
                    facade.Atmosphere.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.GaleStorm);
                    facade.BountyAndSupply.AddSaltSilver(300);

                    // Safest-route auto-provisioning from WhitewaterCrossing -> MineCamp.
                    Assert.IsTrue(panel.Show(5f));
                    StringAssert.Contains(
                        "合计",
                        panel.FormatProvisioningBreakdownForDestination(RpgWorldMapId.MineCamp, preferSafestRoute: true));
                    Assert.IsTrue(panel.TryAutoProvisionForDestination(
                        RpgWorldMapId.MineCamp,
                        allowPartialIfUnderBudget: false,
                        preferSafestRoute: true));
                    Assert.IsTrue(panel.LastProvisioningReceipt.FullyProvisioned);
                    Assert.AreEqual(
                        "行囊已齐备（无需补购）",
                        panel.FormatProvisioningBreakdownForDestination(RpgWorldMapId.MineCamp, preferSafestRoute: true));

                    // Traverse remaining regions and confirm readiness breakdown stays responsive.
                    RpgWorldMapId[] nextStops =
                    {
                        RpgWorldMapId.MineCamp,
                        RpgWorldMapId.Soundwell,
                        RpgWorldMapId.RedSaltHarbor
                    };
                    for (int i = 0; i < nextStops.Length; i++)
                    {
                        Assert.IsTrue(boot.SwitchMap(nextStops[i]));
                        Assert.IsTrue(panel.Show(10f + i));
                        RpgWorldMapId target = nextStops[(i + 1) % nextStops.Length];
                        string breakdown = panel.FormatProvisioningBreakdownForDestination(target, preferSafestRoute: true);
                        Assert.IsFalse(string.IsNullOrWhiteSpace(breakdown));
                    }

                    Assert.IsTrue(panel.Hide());
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
