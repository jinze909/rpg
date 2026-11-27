using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionPlayableSessionGuardE2ETests
    {
        [Test]
        public void AllFourRegions_InvalidAndSameMapSwitchesPreserveLiveSessionHostAndJournalWithZeroLeak()
        {
            // Warm up one-time UGUI/font caches before steady-state leak baseline.
            using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                initialMapId: RpgWorldMapId.Soundwell,
                overrideTileSlices: new Sprite[] { null }))
            {
                Assert.AreEqual(RpgWorldMapId.Soundwell, warmupBoot.Driver.Facade.Session.CurrentMapId);
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
            {
                for (int i = 0; i < regions.Length; i++)
                {
                    RpgWorldMapId mapId = regions[i];
                    if (i > 0)
                    {
                        Assert.IsTrue(boot.SwitchMap(mapId), "Expected switch to " + mapId);
                    }

                    var session = boot.Driver.Facade.Session;
                    Assert.AreEqual(mapId, session.CurrentMapId);

                    var liveHost = session.Host;
                    var liveJournal = session.JournalPanel;
                    Vector2Int retainedTile = liveHost.CurrentTile;

                    // Invalid map ID must fail closed without destroying live host or journal.
                    Assert.IsFalse(session.SwitchMap((RpgWorldMapId)999));
                    Assert.AreEqual(mapId, session.CurrentMapId);
                    Assert.AreSame(liveHost, session.Host);
                    Assert.AreSame(liveJournal, session.JournalPanel);
                    Assert.AreEqual(retainedTile, session.Host.CurrentTile);

                    // Same-map switch must remain a no-op that preserves live session references and tile.
                    Assert.IsTrue(session.SwitchMap(mapId));
                    Assert.AreEqual(mapId, session.CurrentMapId);
                    Assert.AreSame(liveHost, session.Host);
                    Assert.AreSame(liveJournal, session.JournalPanel);
                    Assert.AreEqual(retainedTile, session.Host.CurrentTile);
                }
            }

            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Texture2D>().Length, texBefore);
            Assert.LessOrEqual(Resources.FindObjectsOfTypeAll<Sprite>().Length, spriteBefore);
        }
    }
}
