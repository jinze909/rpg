using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionItineraryHudE2ETests
    {
        [Test]
        public void TravelItineraryHudPanel_PlansMultiHopHonorsActiveBuffAutoProvisionsAndClosesWithZeroLeak()
        {
            var root = new GameObject("World500xFourRegionItineraryHudE2ERoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupPanel = new RpgWorld500xTravelItineraryHudPanel(root.transform, warmupBoot))
                {
                    warmupPanel.Show(RpgWorldMapId.MineCamp, elapsedSeconds: 0f);
                    warmupPanel.Hide();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                var buffStore = new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["rpg.return.world.supply.item.lamp_oil"] = 1
                };
                var supplyUse = new RpgWorld500xFieldSupplyUseService(
                    k => buffStore.TryGetValue(k, out int v) ? v : 0,
                    (k, v) => buffStore[k] = v);
                Assert.IsTrue(supplyUse.TryConsumeSupply("lamp_oil", 2f).Succeeded);

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xTravelItineraryHudPanel(root.transform, boot, supplyUse))
                {
                    var facade = boot.Driver.Facade;
                    facade.Atmosphere.SetWeather(RpgWorldMapId.Soundwell, RpgWorld500xWeatherKind.Clear);
                    facade.Atmosphere.SetWeather(RpgWorldMapId.RedSaltHarbor, RpgWorld500xWeatherKind.Clear);
                    facade.Atmosphere.SetWeather(RpgWorldMapId.WhitewaterCrossing, RpgWorld500xWeatherKind.Clear);
                    facade.Atmosphere.SetWeather(RpgWorldMapId.MineCamp, RpgWorld500xWeatherKind.GaleStorm);
                    facade.BountyAndSupply.AddSaltSilver(300);

                    // Plan Soundwell -> MineCamp (2 hops) with active lamp_oil buff.
                    Assert.IsTrue(panel.Show(RpgWorldMapId.MineCamp, elapsedSeconds: 10f));
                    Assert.IsTrue(panel.IsVisible);
                    Assert.IsTrue(panel.TryClickAutoProvision());
                    Assert.IsTrue(panel.LastProvisioningReceipt.FullyProvisioned);
                    Assert.AreEqual(0, facade.BountyAndSupply.GetOwnedSupplyCount("lamp_oil"));
                    Assert.AreEqual(2, facade.BountyAndSupply.GetOwnedSupplyCount("rations"));
                    Assert.AreEqual(1, facade.BountyAndSupply.GetOwnedSupplyCount("flare"));

                    // Switching to RedSaltHarbor updates header & keeps panel responsive.
                    Assert.IsTrue(boot.SwitchMap(RpgWorldMapId.RedSaltHarbor));
                    panel.Refresh(15f);
                    Assert.IsTrue(panel.Show(RpgWorldMapId.WhitewaterCrossing, elapsedSeconds: 15f));
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
