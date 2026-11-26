using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xSupplyHotbarAndDangerBriefE2ETests
    {
        [Test]
        public void SupplyHotbarInventoryReadoutAndDangerBriefFirstClearSummary_IntegrateEndToEndWithZeroLeak()
        {
            var root = new GameObject("World500xSupplyHotbarDangerBriefE2ERoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupHotbar = new RpgWorld500xFieldSupplyHotbarHud(root.transform, warmupBoot))
                using (var warmupDanger = new RpgWorld500xDangerTacticalBriefHudPanel(root.transform, warmupBoot))
                {
                    warmupHotbar.Refresh(1f);
                    warmupDanger.Show();
                    warmupDanger.TryClickClose();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                var metaStore = new Dictionary<string, int>(StringComparer.Ordinal);
                Func<string, int> read = k => metaStore.TryGetValue(k, out int v) ? v : 0;
                Action<string, int> write = (k, v) => metaStore[k] = v;

                var shop = new RpgWorld500xFieldBountyAndSupplyService(read, write);
                shop.AddSaltSilver(300);
                Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded, shop.TryPurchaseSupply("lamp_oil", 2));
                Assert.AreEqual(RpgCampSupplyPurchaseStatus.Succeeded, shop.TryPurchaseSupply("gauze", 1));

                var useSvc = new RpgWorld500xFieldSupplyUseService(read, write);

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var hotbar = new RpgWorld500xFieldSupplyHotbarHud(root.transform, boot, useSvc))
                using (var dangerPanel = new RpgWorld500xDangerTacticalBriefHudPanel(root.transform, boot, read))
                {
                    // 1. FieldSupplyHotbarHud reflects live inventory snapshot and timed buff activation.
                    hotbar.Refresh(10f);
                    Assert.AreEqual(6, hotbar.LastInventorySnapshot.Slots.Count);
                    Assert.AreEqual(3, hotbar.TotalOwnedSupplyCount);
                    Assert.AreEqual(2, hotbar.DistinctOwnedSupplyKinds);
                    Assert.AreEqual(0, hotbar.ActiveBuffCount);
                    Assert.IsTrue(hotbar.HasUsableSupply("lamp_oil"));
                    Assert.IsTrue(hotbar.HasUsableSupply("gauze"));
                    Assert.IsFalse(hotbar.HasUsableSupply("flare"));
                    StringAssert.Contains("【随身补给】", hotbar.InventorySummaryText);

                    Assert.IsTrue(hotbar.TryClickUseSupply("lamp_oil", 10f));
                    Assert.AreEqual(2, hotbar.TotalOwnedSupplyCount);
                    Assert.AreEqual(1, hotbar.ActiveBuffCount);
                    StringAssert.Contains("灯油照明", hotbar.ActiveBuffsText);

                    // 2. DangerTacticalBriefHudPanel exposes first-clear reputation & silver remainders.
                    Assert.IsTrue(dangerPanel.Show(RpgWorld500xDangerBriefFilter.UnclearedOnly, elapsedSeconds: 10f));
                    int totalZones = dangerPanel.LastBrief.TotalDangerZones;
                    Assert.Greater(totalZones, 0);
                    Assert.AreEqual(totalZones, dangerPanel.LastFirstClearSummary.RemainingDangerZones);
                    int initialStdRep = dangerPanel.RemainingFirstClearStandardReputation;
                    int initialLbRep = dangerPanel.RemainingFirstClearLockBreakReputation;
                    Assert.Greater(initialLbRep, initialStdRep);
                    StringAssert.Contains("【首破悬赏余量】", dangerPanel.FirstClearSummaryText);
                    StringAssert.Contains("余声望", dangerPanel.HeaderText);

                    // 3. Granting Lock-Break first-clear victory updates DangerTacticalBriefHudPanel and credits bonus supply/silver.
                    string nearestId = dangerPanel.LastBrief.NearestUnclearedZone.MarkerId;
                    var campHaven = new RpgWorld500xCampHavenService(read, write);
                    RpgWorld500xDangerVictoryReward granted = RpgWorld500xDangerVictoryRewardService.GrantVictoryReward(
                        boot, campHaven, RpgWorldMapId.Soundwell, nearestId, lockBreakAchieved: true, read, write);
                    Assert.IsTrue(granted.IsFirstClear);

                    Assert.IsTrue(dangerPanel.TryClickRefresh(15f));
                    Assert.AreEqual(1, dangerPanel.LastFirstClearSummary.ClearedDangerZones);
                    Assert.AreEqual(totalZones - 1, dangerPanel.LastFirstClearSummary.RemainingDangerZones);
                    Assert.Less(dangerPanel.RemainingFirstClearStandardReputation, initialStdRep);
                    Assert.Less(dangerPanel.RemainingFirstClearLockBreakReputation, initialLbRep);

                    // Hotbar refreshes cleanly after victory and clears buff once expired.
                    hotbar.Refresh(15f);
                    Assert.GreaterOrEqual(hotbar.TotalOwnedSupplyCount, 2);
                    hotbar.Refresh(120f);
                    Assert.AreEqual(0, hotbar.ActiveBuffCount);
                    StringAssert.Contains("无", hotbar.ActiveBuffsText);

                    Assert.IsTrue(dangerPanel.TryClickClose());
                    Assert.IsFalse(dangerPanel.IsVisible);
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
