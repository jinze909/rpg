using System;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xQuestJournalPanelTests
    {
        private GameObject root;
        private RpgWorld500xQuestJournalPanel panel;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("World500xQuestJournalTestRoot");
        }

        [TearDown]
        public void TearDown()
        {
            panel?.Dispose();
            panel = null;
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void JournalHasScrollableSizedRowsAndClickableInWorldTabControls()
        {
            var tracker = new RpgMapExplorationTracker(RpgWorldMapId.MineCamp);
            panel = new RpgWorld500xQuestJournalPanel(root.transform, tracker);

            Assert.That(panel.Show(), Is.True);
            var scroll = panel.RootObject.GetComponentInChildren<ScrollRect>(true);
            Assert.That(scroll, Is.Not.Null);
            Assert.That(scroll.vertical, Is.True);
            Assert.That(scroll.horizontal, Is.False);
            Assert.That(scroll.content.GetComponent<VerticalLayoutGroup>(), Is.Not.Null);
            Assert.That(scroll.content.GetComponent<ContentSizeFitter>(), Is.Not.Null);
            Assert.That(scroll.content.childCount, Is.EqualTo(tracker.TotalQuestCount));

            var firstRow = scroll.content.GetChild(0).GetComponent<RectTransform>();
            Assert.That(firstRow.GetComponent<LayoutElement>().preferredHeight, Is.GreaterThanOrEqualTo(40f));
            var actionRect = firstRow.Find("ActionButton").GetComponent<RectTransform>();
            Assert.That(actionRect.anchorMax.x - actionRect.anchorMin.x, Is.GreaterThan(0.1f));
            Assert.That(actionRect.anchorMax.y - actionRect.anchorMin.y, Is.GreaterThan(0.5f));

            // The tabs must be actual UGUI buttons available to players, not
            // hidden API-only navigation used by automation tests.
            var fastTravelTab = panel.RootObject.transform.Find("JournalPanel/TabBar/Tab_FastTravelBeacons");
            Assert.That(fastTravelTab, Is.Not.Null);
            var tabRect = fastTravelTab.GetComponent<RectTransform>();
            Assert.That(tabRect.anchorMax.x - tabRect.anchorMin.x, Is.GreaterThan(0.3f));
            fastTravelTab.GetComponent<Button>().onClick.Invoke();
            Assert.That(panel.CurrentTab, Is.EqualTo(RpgQuestJournalTab.FastTravelBeacons));
            Assert.That(panel.DisplayedRowCount, Is.EqualTo(5));
            Assert.That(scroll.content.childCount, Is.EqualTo(5));

            var completedTab = panel.RootObject.transform.Find("JournalPanel/TabBar/Tab_CompletedQuests");
            completedTab.GetComponent<Button>().onClick.Invoke();
            Assert.That(panel.CurrentTab, Is.EqualTo(RpgQuestJournalTab.CompletedQuests));
            Assert.That(panel.DisplayedRowCount, Is.Zero);
            Assert.That(scroll.content.childCount, Is.Zero);

            var activeTab = panel.RootObject.transform.Find("JournalPanel/TabBar/Tab_ActiveQuests");
            activeTab.GetComponent<Button>().onClick.Invoke();
            Assert.That(panel.CurrentTab, Is.EqualTo(RpgQuestJournalTab.ActiveQuests));
            Assert.That(panel.DisplayedRowCount, Is.EqualTo(tracker.TotalQuestCount));
        }

        [Test]
        public void JournalFiltersActiveCompletedQuestsTracksObjectivesAndGatesFastTravelButtons()
        {
            var tracker = new RpgMapExplorationTracker(RpgWorldMapId.MineCamp);
            string lastTracked = null;
            string lastFastTravelId = null;
            Vector2Int lastFastTravelTile = default;

            panel = new RpgWorld500xQuestJournalPanel(
                root.transform,
                tracker,
                onFastTravelRequested: (id, tile) =>
                {
                    lastFastTravelId = id;
                    lastFastTravelTile = tile;
                },
                onTrackedQuestChanged: qid => lastTracked = qid);

            Assert.That(panel.IsVisible, Is.False);
            Assert.That(panel.Show(RpgQuestJournalTab.ActiveQuests), Is.True);
            Assert.That(panel.IsVisible, Is.True);
            Assert.That(panel.DisplayedRowCount, Is.EqualTo(tracker.TotalQuestCount));

            // Click to track an active landmark quest.
            Assert.That(panel.TryClickTrackQuest("q.mc.inspect-main-pump"), Is.True);
            Assert.That(lastTracked, Is.EqualTo("q.mc.inspect-main-pump"));
            Assert.That(tracker.TrackedQuestId, Is.EqualTo("q.mc.inspect-main-pump"));

            // Complete that quest and verify it moves from Active to Completed tab.
            Assert.That(tracker.TryAdvanceQuest("q.mc.inspect-main-pump", 2), Is.True);
            panel.Refresh();
            Assert.That(panel.DisplayedRowCount, Is.EqualTo(tracker.TotalQuestCount - 1));
            Assert.That(panel.SetTab(RpgQuestJournalTab.CompletedQuests), Is.True);
            Assert.That(panel.DisplayedRowCount, Is.EqualTo(1));
            Assert.That(panel.TryClickTrackQuest("q.mc.inspect-main-pump"), Is.False);

            // Fast-travel tab allows clicking only discovered beacons.
            Assert.That(panel.SetTab(RpgQuestJournalTab.FastTravelBeacons), Is.True);
            Assert.That(panel.DisplayedRowCount, Is.EqualTo(5));
            Assert.That(panel.TryClickFastTravel("mc.south-gate"), Is.False);
            Assert.That(panel.TryClickFastTravel("mc.main-pump-hall"), Is.True);
            Assert.That(lastFastTravelId, Is.EqualTo("mc.main-pump-hall"));
            Assert.That(lastFastTravelTile, Is.EqualTo(new Vector2Int(1720, 1080)));

            Assert.That(panel.Hide(), Is.True);
            Assert.That(panel.IsVisible, Is.False);
            panel.Dispose();
            Assert.That(panel.IsDisposed, Is.True);
            Assert.That(panel.Show(), Is.False);
        }
    }
}
