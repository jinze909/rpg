using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionJournalAndBeaconE2ETests
    {
        [Test]
        public void QuestJournalAndFastTravelBeaconHudPanels_StayInSyncAcrossAllFourRegionsWithZeroLeak()
        {
            var root = new GameObject("World500xFourRegionJournalBeaconE2ERoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupBeacon = new RpgWorld500xFastTravelBeaconHudPanel(root.transform, warmupBoot))
                {
                    warmupBoot.Driver.Facade.Session.JournalPanel.Show();
                    warmupBoot.Driver.Facade.Session.JournalPanel.Hide();
                    warmupBeacon.Show();
                    warmupBeacon.Hide();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                RpgWorldMapId[] regions =
                {
                    RpgWorldMapId.Soundwell,
                    RpgWorldMapId.RedSaltHarbor,
                    RpgWorldMapId.WhitewaterCrossing,
                    RpgWorldMapId.MineCamp
                };

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: regions[0],
                    overrideTileSlices: new Sprite[] { null }))
                using (var beaconHud = new RpgWorld500xFastTravelBeaconHudPanel(root.transform, boot))
                {
                    for (int i = 0; i < regions.Length; i++)
                    {
                        if (i > 0)
                        {
                            Assert.IsTrue(boot.SwitchMap(regions[i]));
                        }

                        var journal = boot.Driver.Facade.Session.JournalPanel;
                        Assert.IsTrue(journal.Show(RpgQuestJournalTab.ActiveQuests));
                        Assert.Greater(journal.TotalQuestCount, 0);
                        Assert.Greater(journal.TotalBeaconCount, 1);
                        StringAssert.Contains("【传送烽火】", journal.BeaconNetworkSummaryText);
                        int initialUnlocked = journal.UnlockedBeaconCount;
                        int totalBeacons = journal.TotalBeaconCount;
                        Assert.IsTrue(journal.Hide());

                        Assert.IsTrue(beaconHud.Show());
                        Assert.AreEqual(totalBeacons, beaconHud.TotalBeaconCount);
                        Assert.AreEqual(initialUnlocked, beaconHud.UnlockedBeaconCount);
                        StringAssert.Contains("【传送烽火】", beaconHud.NetworkSummaryText);

                        string targetBeaconId = beaconHud.NearestUndiscoveredBeaconId;
                        Assert.IsNotEmpty(targetBeaconId);
                        Assert.IsTrue(boot.Driver.Facade.Session.Host.Tracker.TryDiscoverMarker(targetBeaconId));

                        beaconHud.Refresh();
                        journal.Refresh();
                        Assert.AreEqual(initialUnlocked + 1, beaconHud.UnlockedBeaconCount);
                        Assert.AreEqual(beaconHud.UnlockedBeaconCount, journal.UnlockedBeaconCount);
                        Assert.AreEqual(beaconHud.TotalBeaconCount, journal.TotalBeaconCount);

                        Assert.IsTrue(beaconHud.TryClickWarp(targetBeaconId));
                        Assert.AreEqual(targetBeaconId, beaconHud.NearestDiscoveredBeaconId);
                        StringAssert.Contains("已传送至", beaconHud.StatusText);
                        Assert.IsTrue(beaconHud.Hide());
                    }
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
