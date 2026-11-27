using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionRumorLoreTrackerHudE2ETests
    {
        [Test]
        public void RumorAndLoreHudPanel_AcrossAllFourRegions_TracksHeardRumorSwitchesTabsAndFiltersUndiscovered()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            for (int i = 0; i < specs.Count; i++)
            {
                var mapId = specs[i].MapId;
                var root = new GameObject("FourRegionRumorLoreRoot_" + mapId);
                try
                {
                    using (var boot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: mapId,
                        overrideTileSlices: new Sprite[] { null }))
                    using (var panel = new RpgWorld500xFieldRumorAndLoreHudPanel(root.transform, boot))
                    {
                        var facade = boot.Driver.Facade;
                        var tracker = facade.Session.Host.Tracker;
                        var npcs = RpgWorld500xLivingWorldCatalog.GetFieldNpcs(mapId);
                        Assert.That(npcs.Count, Is.GreaterThanOrEqualTo(2));

                        var npc = npcs[0];
                        Assert.IsTrue(facade.ActivityDirector.LivingWorld.TryTalkToNearbyNpc(
                            new Vector2(npc.Tile.x + 0.5f, npc.Tile.y + 0.5f),
                            4f,
                            out _,
                            out _,
                            out _));

                        Assert.IsTrue(panel.Show());
                        Assert.That(panel.ActiveTab, Is.EqualTo(RpgWorld500xRumorLoreTab.FieldRumors));
                        Assert.That(panel.HeardRumorCount, Is.EqualTo(1));
                        Assert.That(panel.DisplayedRowCount, Is.EqualTo(npcs.Count));

                        Assert.IsTrue(panel.TryClickTrackRumorTarget(npc.Id));
                        Assert.That(tracker.TrackedMarkerId, Is.EqualTo(npc.RumorTargetMarkerId));
                        StringAssert.Contains("罗盘追踪", panel.StatusText);

                        Assert.IsTrue(panel.TrySetUndiscoveredOnly(true));
                        Assert.IsTrue(panel.UndiscoveredOnly);
                        Assert.That(panel.DisplayedRowCount, Is.EqualTo(npcs.Count - 1));

                        Assert.IsTrue(panel.TrySelectTab(RpgWorld500xRumorLoreTab.StorySpots));
                        Assert.That(panel.ActiveTab, Is.EqualTo(RpgWorld500xRumorLoreTab.StorySpots));
                        Assert.That(panel.DisplayedRowCount, Is.GreaterThan(0));

                        Assert.IsTrue(panel.TrySelectTab(RpgWorld500xRumorLoreTab.FieldRumors));
                        Assert.That(panel.ActiveTab, Is.EqualTo(RpgWorld500xRumorLoreTab.FieldRumors));
                        Assert.That(panel.DisplayedRowCount, Is.EqualTo(npcs.Count - 1));
                    }

                    Assert.That(root.transform.childCount, Is.EqualTo(0));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        [Test]
        public void RumorAndLoreHudPanel_AcrossAllFourRegions_DisabledCanvasRejectsActionsAndRestoresCleanly()
        {
            var specs = RpgExpandedWorldScale.AllSpecs;
            Assert.That(specs.Count, Is.EqualTo(4));

            for (int i = 0; i < specs.Count; i++)
            {
                var mapId = specs[i].MapId;
                var root = new GameObject("FourRegionRumorLoreCanvasGuard_" + mapId);
                try
                {
                    using (var boot = new RpgWorld500xSceneBootstrapper(
                        initialMapId: mapId,
                        overrideTileSlices: new Sprite[] { null }))
                    using (var panel = new RpgWorld500xFieldRumorAndLoreHudPanel(root.transform, boot))
                    {
                        var facade = boot.Driver.Facade;
                        var tracker = facade.Session.Host.Tracker;
                        var npcs = RpgWorld500xLivingWorldCatalog.GetFieldNpcs(mapId);
                        var npc = npcs[0];

                        Assert.IsTrue(facade.ActivityDirector.LivingWorld.TryTalkToNearbyNpc(
                            new Vector2(npc.Tile.x + 0.5f, npc.Tile.y + 0.5f),
                            4f,
                            out _,
                            out _,
                            out _));

                        Assert.IsTrue(panel.Show());
                        var canvas = root.transform.Find("World500xFieldRumorAndLoreHudPanel").GetComponent<Canvas>();
                        Assert.That(canvas, Is.Not.Null);

                        string initialMarker = tracker.TrackedMarkerId;
                        int initialRows = panel.DisplayedRowCount;

                        canvas.enabled = false;
                        Assert.IsFalse(panel.TryClickTrackRumorTarget(npc.Id));
                        Assert.IsFalse(panel.TrySelectTab(RpgWorld500xRumorLoreTab.StorySpots));
                        Assert.IsFalse(panel.TrySetUndiscoveredOnly(true));
                        Assert.That(tracker.TrackedMarkerId, Is.EqualTo(initialMarker));
                        Assert.That(panel.ActiveTab, Is.EqualTo(RpgWorld500xRumorLoreTab.FieldRumors));
                        Assert.IsFalse(panel.UndiscoveredOnly);
                        Assert.That(panel.DisplayedRowCount, Is.EqualTo(initialRows));

                        canvas.enabled = true;
                        Assert.IsTrue(panel.TryClickTrackRumorTarget(npc.Id));
                        Assert.That(tracker.TrackedMarkerId, Is.EqualTo(npc.RumorTargetMarkerId));
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }
    }
}
