using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xPlayableSessionTests
    {
        private GameObject root;
        private RpgWorld500xPlayableSession session;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("World500xPlayableSessionTestRoot");
        }

        [TearDown]
        public void TearDown()
        {
            session?.Dispose();
            session = null;
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void PlayableSessionWiresHostMinimapJournalFastTravelAndFourMapSwitchingCleanly()
        {
            var store = new Dictionary<string, int>(StringComparer.Ordinal);
            session = new RpgWorld500xPlayableSession(
                root.transform,
                RpgWorldMapId.RedSaltHarbor,
                getInt: k => store.TryGetValue(k, out int v) ? v : 0,
                setInt: (k, v) => store[k] = v,
                streamingRadiusChunks: 1,
                overrideTileSlices: new Sprite[] { null });

            Assert.That(session.CurrentMapId, Is.EqualTo(RpgWorldMapId.RedSaltHarbor));
            Assert.That(session.Host.ChunkRenderer.ChunkCount, Is.LessThanOrEqualTo(9));
            Assert.That(session.MinimapHud.TextureWidth, Is.EqualTo(RpgWorld500xMinimapHud.DefaultTextureWidth));
            Assert.That(session.MinimapHud.LastCompassText, Does.Contain("外港浅水泊位"));

            // Overview toggle works.
            Assert.That(session.ToggleOverviewMap(), Is.True);
            Assert.That(session.MinimapHud.IsOverviewMode, Is.True);

            // Opening journal pauses movement, tracks a quest, and fast-travels on click.
            Assert.That(session.ToggleJournal(RpgQuestJournalTab.ActiveQuests), Is.True);
            Assert.That(session.StepMovement(Vector2.right, 0.2f), Is.False);
            Assert.That(session.JournalPanel.TryClickTrackQuest("q.rsh.compare-salt-prices"), Is.True);
            Assert.That(session.MinimapHud.LastCompassText, Does.Contain("摸底赤盐行情"));

            // Discover salt-market beacon and fast-travel straight from the journal UI.
            Assert.That(session.Host.Tracker.TryDiscoverMarker("rsh.salt-market"), Is.True);
            Assert.That(session.JournalPanel.SetTab(RpgQuestJournalTab.FastTravelBeacons), Is.True);
            Assert.That(session.JournalPanel.TryClickFastTravel("rsh.salt-market"), Is.True);
            Assert.That(session.JournalPanel.IsVisible, Is.False);
            Assert.That(session.Host.CurrentTile, Is.EqualTo(new Vector2Int(1700, 1260)));

            // Interact at nearby landmark quest.
            Assert.That(session.InteractNearby(), Is.EqualTo(RpgWorldInteractOutcome.AdvancedLandmarkQuest));

            // Re-selecting this map or receiving a stale serialized map ID
            // must not destroy live quest, journal or streaming state.
            var liveHost = session.Host;
            var liveJournal = session.JournalPanel;
            Vector2Int retainedTile = liveHost.CurrentTile;
            int retainedChildren = root.transform.childCount;
            Assert.That(session.SwitchMap((RpgWorldMapId)999), Is.False);
            Assert.That(session.Host, Is.SameAs(liveHost));
            Assert.That(session.JournalPanel, Is.SameAs(liveJournal));
            Assert.That(root.transform.childCount, Is.EqualTo(retainedChildren));
            Assert.That(session.SwitchMap(RpgWorldMapId.RedSaltHarbor), Is.True);
            Assert.That(session.Host, Is.SameAs(liveHost));
            Assert.That(session.Host.CurrentTile, Is.EqualTo(retainedTile));
            Assert.That(session.Host.Tracker.IsMarkerDiscovered("rsh.salt-market"), Is.True);
            Assert.That(session.JournalPanel, Is.SameAs(liveJournal));
            Assert.That(root.transform.childCount, Is.EqualTo(retainedChildren));

            // Seamless switch across all remaining 500x maps with zero child leaks.
            Assert.That(session.SwitchMap(RpgWorldMapId.Soundwell), Is.True);
            Assert.That(session.CurrentMapId, Is.EqualTo(RpgWorldMapId.Soundwell));
            Assert.That(session.SwitchMap(RpgWorldMapId.WhitewaterCrossing), Is.True);
            Assert.That(session.CurrentMapId, Is.EqualTo(RpgWorldMapId.WhitewaterCrossing));
            Assert.That(session.SwitchMap(RpgWorldMapId.MineCamp), Is.True);
            Assert.That(session.CurrentMapId, Is.EqualTo(RpgWorldMapId.MineCamp));

            session.Dispose();
            Assert.That(session.IsDisposed, Is.True);
            Assert.That(root.transform.childCount, Is.Zero);
        }
    }
}
