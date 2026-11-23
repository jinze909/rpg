using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xDiscoveryBannerAndLedgerTests
    {
        [Test]
        public void LedgerSummaryAggregatesAllFour500xMapsAndBannerQueuesAndExpiresToastsCleanly()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
            Action<string, int> write = (k, v) => store[k] = v;

            RpgWorld500xGlobalProgress empty = RpgWorld500xLedgerSummary.Capture(read);
            Assert.That(empty.PerMap.Count, Is.EqualTo(4));
            Assert.That(empty.TotalMarkers, Is.EqualTo(72));
            Assert.That(empty.TotalQuests, Is.EqualTo(60));
            Assert.That(empty.TotalBeacons, Is.EqualTo(21));
            Assert.That(empty.DiscoveredMarkers, Is.Zero);
            Assert.That(empty.CompletionPercent, Is.Zero);

            var soundwellBridge = new RpgWorldOrkExplorationBridge(RpgWorldMapId.Soundwell, read, write);
            Assert.That(soundwellBridge.UpdatePlayerPosition(new Vector2(224f, 672f)), Is.EqualTo(1));
            Assert.That(soundwellBridge.TryTriggerDangerZoneEncounter("sw.sunken-drain", new Vector2(680f, 640f)), Is.True);
            Assert.That(soundwellBridge.TryResolveDangerZoneVictory("sw.sunken-drain", true, out _), Is.True);

            RpgWorld500xGlobalProgress after = RpgWorld500xLedgerSummary.Capture(read);
            Assert.That(after.DiscoveredMarkers, Is.EqualTo(2));
            Assert.That(after.CompletedQuests, Is.EqualTo(1));
            Assert.That(after.UnlockedBeacons, Is.EqualTo(1));
            Assert.That(after.ClearedDangerZones, Is.EqualTo(1));
            Assert.That(after.CompletionPercent, Is.GreaterThan(0f));
            string summary = RpgWorld500xLedgerSummary.FormatLedgerSummaryZh(after);
            Assert.That(summary, Does.Contain("地标 2/72"));
            Assert.That(summary, Does.Contain("地标任务 1/60"));

            var root = new GameObject("DiscoveryBannerTestRoot");
            RpgWorld500xDiscoveryBannerHud banner = null;
            try
            {
                banner = new RpgWorld500xDiscoveryBannerHud(root.transform);
                Assert.That(banner.IsShowing, Is.False);
                Assert.That(banner.Enqueue("bad", "", float.NaN), Is.False);

                Assert.That(RpgWorldLandmarkCatalog.TryGetMarker(
                    RpgWorldMapId.Soundwell, "sw.entry-shaft", out RpgMapLandmarkMarker entry), Is.True);
                Assert.That(banner.EnqueueLandmarkDiscovered(entry), Is.True);
                Assert.That(banner.EnqueueQuestCompleted(new RpgLandmarkQuestRewardResult(
                    "q.sw.clear-drain-grate", "清理沉降排水栅", RpgLandmarkQuestState.Completed,
                    true, "主厅涉水噪声下降")), Is.True);

                Assert.That(banner.IsShowing, Is.True);
                Assert.That(banner.QueuedCount, Is.EqualTo(1));
                Assert.That(banner.CurrentHeadline, Does.Contain("下井主入口"));
                Assert.That(banner.CurrentSubtitle, Does.Contain("快速传送灯标"));

                banner.Step(float.NaN);
                Assert.That(banner.IsShowing, Is.True);
                banner.Step(4.0f);
                Assert.That(banner.IsShowing, Is.True);
                Assert.That(banner.QueuedCount, Is.Zero);
                Assert.That(banner.CurrentHeadline, Does.Contain("清理沉降排水栅"));

                banner.Step(4.0f);
                Assert.That(banner.IsShowing, Is.False);

                banner.Dispose();
                Assert.That(banner.IsDisposed, Is.True);
                Assert.That(root.transform.childCount, Is.Zero);
            }
            finally
            {
                banner?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
