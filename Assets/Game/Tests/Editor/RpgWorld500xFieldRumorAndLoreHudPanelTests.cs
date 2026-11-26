using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFieldRumorAndLoreHudPanelTests
    {
        [Test]
        public void RumorAndLoreHudPanel_RejectsUnheardHiddenDisabledRetiredAndDisposedActions()
        {
            var root = new GameObject("RumorActionLifetimeRoot");
            try
            {
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xFieldRumorAndLoreHudPanel(root.transform, boot))
                {
                    var facade = boot.Driver.Facade;
                    var tracker = facade.Session.Host.Tracker;
                    var npc = RpgWorld500xLivingWorldCatalog.GetFieldNpcs(RpgWorldMapId.Soundwell)[0];
                    UnityEngine.UI.Button FindButton(string path) => root.transform
                        .Find("World500xFieldRumorAndLoreHudPanel/RumorLoreModalBox/" + path)
                        .GetComponent<UnityEngine.UI.Button>();
                    UnityEngine.UI.Button TrackButton() => FindButton(
                        "RumorLoreRows/RumorRow_" + npc.Id + "/TrackButton");
                    var loreTab = FindButton("Tab_StorySpots").onClick;
                    string initialMarker = tracker.TrackedMarkerId;
                    string initialStatus = panel.StatusText;
                    loreTab.Invoke();
                    TrackButton().onClick.Invoke();
                    Assert.IsFalse(panel.TrySelectTab(RpgWorld500xRumorLoreTab.StorySpots));
                    Assert.IsFalse(panel.TrySetUndiscoveredOnly(true));
                    Assert.AreEqual(RpgWorld500xRumorLoreTab.FieldRumors, panel.ActiveTab);
                    Assert.IsTrue(panel.Show());
                    TrackButton().onClick.Invoke();
                    Assert.IsFalse(panel.TryClickTrackRumorTarget(npc.Id));
                    Assert.AreEqual(initialMarker, tracker.TrackedMarkerId);
                    Assert.AreEqual(initialStatus, panel.StatusText);

                    Assert.IsTrue(facade.ActivityDirector.LivingWorld.TryTalkToNearbyNpc(
                        new Vector2(npc.Tile.x + 0.5f, npc.Tile.y + 0.5f), 4f,
                        out _, out _, out _));
                    panel.Refresh();
                    initialMarker = tracker.TrackedMarkerId;
                    initialStatus = panel.StatusText;
                    void AssertTrackingUnchanged()
                    {
                        Assert.AreEqual(initialMarker, tracker.TrackedMarkerId);
                        Assert.AreEqual(initialStatus, panel.StatusText);
                    }

                    var retired = TrackButton().onClick;
                    Assert.IsTrue(panel.Hide());
                    retired.Invoke();
                    loreTab.Invoke();
                    Assert.IsFalse(panel.TryClickTrackRumorTarget(npc.Id));
                    Assert.AreEqual(RpgWorld500xRumorLoreTab.FieldRumors, panel.ActiveTab);
                    AssertTrackingUnchanged();

                    Assert.IsTrue(panel.Show());
                    retired.Invoke();
                    var current = TrackButton();
                    current.interactable = false;
                    current.onClick.Invoke();
                    Assert.IsFalse(panel.TryClickTrackRumorTarget(npc.Id));
                    AssertTrackingUnchanged();

                    panel.Refresh();
                    var beforeTabSwitch = TrackButton().onClick;
                    Assert.IsFalse(panel.TrySelectTab((RpgWorld500xRumorLoreTab)255));
                    Assert.IsTrue(panel.TrySelectTab(RpgWorld500xRumorLoreTab.StorySpots));
                    beforeTabSwitch.Invoke();
                    Assert.IsFalse(panel.TryClickTrackRumorTarget(npc.Id));
                    AssertTrackingUnchanged();

                    Assert.IsTrue(panel.TrySelectTab(RpgWorld500xRumorLoreTab.FieldRumors));
                    beforeTabSwitch.Invoke();
                    AssertTrackingUnchanged();
                    Assert.IsTrue(panel.TryClickTrackRumorTarget(npc.Id));
                    Assert.AreEqual(npc.RumorTargetMarkerId, tracker.TrackedMarkerId);
                    Assert.IsTrue(panel.StatusText.Contains("罗盘追踪"));

                    var disposedTrack = TrackButton().onClick;
                    panel.Dispose();
                    loreTab.Invoke();
                    disposedTrack.Invoke();
                    Assert.IsFalse(panel.TryClickTrackRumorTarget(npc.Id));
                    Assert.IsFalse(panel.TrySelectTab(RpgWorld500xRumorLoreTab.StorySpots));
                    Assert.IsFalse(panel.TrySetUndiscoveredOnly(true));
                    Assert.AreEqual(RpgWorld500xRumorLoreTab.FieldRumors, panel.ActiveTab);
                    Assert.AreEqual(npc.RumorTargetMarkerId, tracker.TrackedMarkerId);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RumorAndLoreHudPanel_TracksHeardRumorSwitchesToLoreTabAndDisposesWithZeroLeak()
        {
            var root = new GameObject("World500xFieldRumorAndLoreHudPanelTestRoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var warmupPanel = new RpgWorld500xFieldRumorAndLoreHudPanel(root.transform, warmupBoot))
                {
                    warmupPanel.Show();
                    warmupPanel.TrySelectTab(RpgWorld500xRumorLoreTab.StorySpots);
                    warmupPanel.Hide();
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var panel = new RpgWorld500xFieldRumorAndLoreHudPanel(root.transform, boot))
                {
                    Assert.IsTrue(panel.Show());
                    Assert.AreEqual(RpgWorld500xRumorLoreTab.FieldRumors, panel.ActiveTab);
                    Assert.IsFalse(panel.UndiscoveredOnly);
                    Assert.AreEqual(6, panel.DisplayedRowCount);
                    Assert.AreEqual(0, panel.HeardRumorCount);
                    Assert.AreEqual(6, panel.TotalRumorCount);
                    Assert.AreEqual(0, panel.InspectedStorySpotCount);
                    Assert.AreEqual(4, panel.TotalStorySpotCount);
                    Assert.IsFalse(string.IsNullOrEmpty(panel.NearestUndiscoveredLabelZh));

                    IReadOnlyList<RpgFieldNpcDef> npcs =
                        RpgWorld500xLivingWorldCatalog.GetFieldNpcs(RpgWorldMapId.Soundwell);
                    RpgFieldNpcDef firstNpc = npcs[0];

                    // Unheard rumor cannot be tracked yet.
                    Assert.IsFalse(panel.TryClickTrackRumorTarget(firstNpc.Id));

                    // Talk to the first NPC via LivingWorldService to unlock rumor, then track from UI.
                    Assert.IsTrue(boot.Driver.Facade.ActivityDirector.LivingWorld.TryTalkToNearbyNpc(
                        new Vector2(firstNpc.Tile.x + 0.5f, firstNpc.Tile.y + 0.5f),
                        4f,
                        out _,
                        out _,
                        out _));

                    panel.Refresh();
                    Assert.AreEqual(1, panel.HeardRumorCount);
                    Assert.IsTrue(panel.HeaderText.Contains("传闻 1/6"));
                    Assert.IsTrue(panel.TryClickTrackRumorTarget(firstNpc.Id));
                    Assert.AreEqual(firstNpc.RumorTargetMarkerId, boot.Driver.Facade.Session.Host.Tracker.TrackedMarkerId);
                    Assert.IsTrue(panel.StatusText.Contains("罗盘追踪"));

                    // UndiscoveredOnly hides the heard rumor (6 -> 5 rows) and respects CanvasGroup locks.
                    Assert.IsTrue(panel.TrySetUndiscoveredOnly(true));
                    Assert.IsTrue(panel.UndiscoveredOnly);
                    Assert.AreEqual(5, panel.DisplayedRowCount);

                    CanvasGroup group = root.AddComponent<CanvasGroup>();
                    group.interactable = false;
                    Assert.IsFalse(panel.TrySetUndiscoveredOnly(false));
                    Assert.IsTrue(panel.UndiscoveredOnly);
                    group.interactable = true;

                    Assert.IsTrue(panel.TrySetUndiscoveredOnly(false));
                    Assert.AreEqual(6, panel.DisplayedRowCount);

                    // Switch to StorySpots tab (4 spots per map).
                    Assert.IsTrue(panel.TrySelectTab(RpgWorld500xRumorLoreTab.StorySpots));
                    Assert.AreEqual(RpgWorld500xRumorLoreTab.StorySpots, panel.ActiveTab);
                    Assert.AreEqual(4, panel.DisplayedRowCount);
                    Assert.IsFalse(string.IsNullOrEmpty(panel.NearestUndiscoveredLabelZh));

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