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
        public void JournalDisabledCanvasRejectsTrackingTabsAndTravelUntilRestored()
        {
            var tracker = new RpgMapExplorationTracker(RpgWorldMapId.MineCamp);
            int tracked = 0;
            int travelled = 0;
            string destination = null;
            Vector2Int destinationTile = default;
            panel = new RpgWorld500xQuestJournalPanel(root.transform, tracker,
                onFastTravelRequested: (id, tile) =>
                {
                    travelled++;
                    destination = id;
                    destinationTile = tile;
                },
                onTrackedQuestChanged: id => tracked++);

            const string questId = "q.mc.inspect-main-pump";
            const string markerId = "mc.main-pump-hall";
            Button RowButton(string name) => panel.RootObject.transform
                .Find("JournalPanel/Rows/Content/" + name + "/ActionButton").GetComponent<Button>();

            Assert.IsTrue(panel.Show());
            var canvas = panel.RootObject.GetComponent<Canvas>();
            var track = RowButton("QuestRow_" + questId);
            var beaconTab = panel.RootObject.transform.Find("JournalPanel/TabBar/Tab_FastTravelBeacons")
                .GetComponent<Button>();
            var activeTab = panel.RootObject.transform.Find("JournalPanel/TabBar/Tab_ActiveQuests")
                .GetComponent<Button>();
            string initialTracked = tracker.TrackedQuestId;
            string summary = panel.BodySummaryText;
            int rows = panel.DisplayedRowCount;

            canvas.enabled = false;
            Assert.IsTrue(track.IsInteractable());
            track.onClick.Invoke();
            beaconTab.onClick.Invoke();
            Assert.IsFalse(panel.TryClickTrackQuest(questId));
            Assert.AreEqual(0, tracked);
            Assert.AreEqual(initialTracked, tracker.TrackedQuestId);
            Assert.AreEqual(RpgQuestJournalTab.ActiveQuests, panel.CurrentTab);
            Assert.AreEqual(rows, panel.DisplayedRowCount);
            Assert.AreEqual(summary, panel.BodySummaryText);

            canvas.enabled = true;
            track.onClick.Invoke();
            Assert.AreEqual(1, tracked);
            Assert.AreEqual(questId, tracker.TrackedQuestId);
            Assert.IsTrue(tracker.TryAdvanceQuest(questId, 2));

            // Programmatic tab selection and refresh are not player input.
            canvas.enabled = false;
            Assert.IsTrue(panel.SetTab(RpgQuestJournalTab.FastTravelBeacons));
            var travel = RowButton("BeaconRow_" + markerId);
            Assert.IsTrue(travel.IsInteractable());
            travel.onClick.Invoke();
            activeTab.onClick.Invoke();
            Assert.IsFalse(panel.TryClickFastTravel(markerId));
            Assert.AreEqual(0, travelled);
            Assert.IsNull(destination);
            Assert.AreEqual(RpgQuestJournalTab.FastTravelBeacons, panel.CurrentTab);
            Assert.AreEqual(5, panel.DisplayedRowCount);

            canvas.enabled = true;
            travel.onClick.Invoke();
            Assert.AreEqual(1, travelled);
            Assert.AreEqual(markerId, destination);
            Assert.AreEqual(new Vector2Int(1720, 1080), destinationTile);
            activeTab.onClick.Invoke();
            Assert.AreEqual(RpgQuestJournalTab.ActiveQuests, panel.CurrentTab);
            Assert.AreEqual(tracker.TotalQuestCount - 1, panel.DisplayedRowCount);
        }

        [Test]
        public void JournalRejectsHiddenDisabledRetiredAndDisposedButtonEvents()
        {
            var tracker = new RpgMapExplorationTracker(RpgWorldMapId.MineCamp);
            int tracked = 0;
            int travelled = 0;
            panel = new RpgWorld500xQuestJournalPanel(root.transform, tracker,
                onFastTravelRequested: (id, tile) => travelled++,
                onTrackedQuestChanged: id => tracked++);

            const string questId = "q.mc.inspect-main-pump";
            const string markerId = "mc.main-pump-hall";
            Button RowButton(string name) => panel.RootObject.transform
                .Find("JournalPanel/Rows/Content/" + name + "/ActionButton").GetComponent<Button>();

            Assert.IsFalse(panel.TryClickTrackQuest(questId));
            Assert.IsTrue(panel.Show());
            var retiredTrack = RowButton("QuestRow_" + questId).onClick;
            var tabEvent = panel.RootObject.transform.Find("JournalPanel/TabBar/Tab_FastTravelBeacons")
                .GetComponent<Button>().onClick;
            panel.Hide();
            retiredTrack.Invoke();
            tabEvent.Invoke();
            Assert.IsFalse(panel.TryClickTrackQuest(questId));
            Assert.AreEqual(RpgQuestJournalTab.ActiveQuests, panel.CurrentTab);
            Assert.AreEqual(0, tracked);

            panel.Show();
            retiredTrack.Invoke();
            var currentTrack = RowButton("QuestRow_" + questId);
            currentTrack.interactable = false;
            currentTrack.onClick.Invoke();
            Assert.IsFalse(panel.TryClickTrackQuest(questId));
            Assert.AreEqual(0, tracked);
            currentTrack.interactable = true;
            Assert.IsTrue(panel.TryClickTrackQuest(questId));
            Assert.AreEqual(1, tracked);
            Assert.AreEqual(questId, tracker.TrackedQuestId);

            Assert.IsTrue(tracker.TryAdvanceQuest(questId, 2));
            Assert.IsTrue(panel.SetTab(RpgQuestJournalTab.FastTravelBeacons));
            var retiredTravel = RowButton("BeaconRow_" + markerId).onClick;
            panel.Refresh();
            retiredTravel.Invoke();
            Assert.AreEqual(0, travelled);
            Assert.IsTrue(panel.TryClickFastTravel(markerId));
            Assert.AreEqual(1, travelled);

            var hiddenTravel = RowButton("BeaconRow_" + markerId).onClick;
            panel.Hide();
            hiddenTravel.Invoke();
            Assert.IsFalse(panel.TryClickFastTravel(markerId));
            Assert.AreEqual(1, travelled);

            panel.Show();
            var disposedTravel = RowButton("BeaconRow_" + markerId).onClick;
            panel.Dispose();
            disposedTravel.Invoke();
            tabEvent.Invoke();
            Assert.AreEqual(1, travelled);
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
