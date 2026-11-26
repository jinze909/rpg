using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xDiscoveryDistrictActivityE2ETests
    {
        [Test]
        public void DiscoveryBannerLedgerDistrictFogAndActivityDirector_CoordinateAcrossEventsWithZeroLeak()
        {
            var root = new GameObject("World500xDiscoveryDistrictActivityE2ERoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBanner = new RpgWorld500xDiscoveryBannerHud(root.transform))
                {
                    warmupBanner.Enqueue("预热标题", "预热副标题", 1f);
                    warmupBanner.Step(2f);
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                Func<string, int> read = k => store.TryGetValue(k, out int v) ? v : 0;
                Action<string, int> write = (k, v) => store[k] = v;

                RpgWorld500xGlobalProgress baseline = RpgWorld500xLedgerSummary.Capture(read);
                Assert.AreEqual(4, baseline.PerMap.Count);
                Assert.AreEqual(72, baseline.TotalMarkers);
                Assert.AreEqual(60, baseline.TotalQuests);
                Assert.AreEqual(21, baseline.TotalBeacons);
                Assert.AreEqual(0, baseline.DiscoveredMarkers);

                RpgExpandedMapSpec spec = RpgExpandedWorldScale.GetSpec(RpgWorldMapId.Soundwell);
                var layout = RpgWorld500xMapLayout.Build(RpgWorldMapId.Soundwell);
                var tracker = new RpgMapExplorationTracker(RpgWorldMapId.Soundwell);
                var districtTracker = new RpgWorld500xDistrictProgressTracker(spec, read, write);
                var bridge = new RpgWorldOrkExplorationBridge(RpgWorldMapId.Soundwell, read, write);
                var director = new RpgWorld500xWorldActivityDirector(layout, tracker, read, write);

                using (var banner = new RpgWorld500xDiscoveryBannerHud(root.transform))
                {
                    // Enter Soundwell entry shaft: district fog reveals and landmark discovery updates ledger.
                    Vector2 entryPos = new Vector2(224f, 672f);
                    Assert.IsTrue(districtTracker.RecordPlayerTile(entryPos, 1, out var enteredDistrict));
                    Assert.Greater(districtTracker.VisitedChunkCount, 0);
                    Assert.AreEqual(enteredDistrict.Id, districtTracker.CurrentDistrictId);

                    Assert.AreEqual(1, bridge.UpdatePlayerPosition(entryPos));
                    Assert.IsTrue(RpgWorldLandmarkCatalog.TryGetMarker(
                        RpgWorldMapId.Soundwell, "sw.entry-shaft", out RpgMapLandmarkMarker entryMarker));
                    Assert.IsTrue(banner.EnqueueLandmarkDiscovered(entryMarker));
                    Assert.IsTrue(banner.IsShowing);
                    StringAssert.Contains("下井主入口", banner.CurrentHeadline);

                    // Clear danger zone and talk to field NPC; queue toast and verify ledger + living-world progress.
                    Assert.IsTrue(bridge.TryTriggerDangerZoneEncounter("sw.sunken-drain", new Vector2(680f, 640f)));
                    Assert.IsTrue(bridge.TryResolveDangerZoneVictory("sw.sunken-drain", true, out _));

                    var npc = RpgWorld500xLivingWorldCatalog.GetFieldNpcs(RpgWorldMapId.Soundwell)[0];
                    Vector2 npcPos = new Vector2(npc.Tile.x + 0.5f, npc.Tile.y + 0.5f);
                    Assert.IsTrue(director.TryInteractPriority(npcPos, 6f, out var interaction));
                    Assert.AreEqual(RpgWorld500xInteractionKind.NpcDialogue, interaction.Kind);
                    Assert.IsTrue(interaction.FirstTimeDiscovery);
                    Assert.AreEqual(1, director.LivingWorld.BuildLivingWorldProgressSummary().HeardRumorCount);
                    Assert.IsTrue(banner.Enqueue(interaction.TitleZh, interaction.BodyZh));
                    Assert.AreEqual(1, banner.QueuedCount);

                    RpgWorld500xGlobalProgress after = RpgWorld500xLedgerSummary.Capture(read);
                    Assert.AreEqual(2, after.DiscoveredMarkers);
                    Assert.AreEqual(1, after.CompletedQuests);
                    Assert.AreEqual(1, after.UnlockedBeacons);
                    Assert.AreEqual(1, after.ClearedDangerZones);
                    Assert.Greater(after.CompletionPercent, 0f);
                    string summaryZh = RpgWorld500xLedgerSummary.FormatLedgerSummaryZh(after);
                    StringAssert.Contains("地标 2/72", summaryZh);
                    StringAssert.Contains("高危清障 1/", summaryZh);

                    // Drain toast queue cleanly.
                    banner.Step(4.0f);
                    Assert.IsTrue(banner.IsShowing);
                    Assert.AreEqual(0, banner.QueuedCount);
                    Assert.AreEqual(interaction.TitleZh, banner.CurrentHeadline);

                    banner.Step(4.0f);
                    Assert.IsFalse(banner.IsShowing);
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
