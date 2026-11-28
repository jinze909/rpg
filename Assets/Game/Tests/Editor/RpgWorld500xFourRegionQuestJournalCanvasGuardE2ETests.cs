using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionQuestJournalCanvasGuardE2ETests
    {
        [Test]
        public void AllFourRegions_QuestJournalRejectsDisabledCanvasLockedAndHiddenInputUntilRestored()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var root = new GameObject("QuestJournalCanvasGuard_" + spec.MapId);
                try
                {
                    var tracker = new RpgMapExplorationTracker(spec.MapId);
                    int tracked = 0;
                    int travelled = 0;
                    string destination = null;

                    using (var panel = new RpgWorld500xQuestJournalPanel(
                        root.transform,
                        tracker,
                        onFastTravelRequested: (id, tile) =>
                        {
                            travelled++;
                            destination = id;
                        },
                        onTrackedQuestChanged: id => tracked++))
                    {
                        Assert.IsTrue(panel.Show(), spec.MapId.ToString());
                        var canvas = panel.RootObject.GetComponent<Canvas>();
                        Assert.IsNotNull(canvas, spec.MapId.ToString());

                        var beaconTab = panel.RootObject.transform
                            .Find("JournalPanel/TabBar/Tab_FastTravelBeacons")
                            .GetComponent<Button>();
                        var activeTab = panel.RootObject.transform
                            .Find("JournalPanel/TabBar/Tab_ActiveQuests")
                            .GetComponent<Button>();
                        Assert.IsNotNull(beaconTab, spec.MapId.ToString());
                        Assert.IsNotNull(activeTab, spec.MapId.ToString());

                        FindInteractableRow(panel, "QuestRow_", out string questId, out Button trackBtn);
                        Assert.IsNotNull(trackBtn, spec.MapId.ToString());

                        string initialTracked = tracker.TrackedQuestId;
                        string summary = panel.BodySummaryText;
                        int rows = panel.DisplayedRowCount;
                        Assert.Greater(rows, 0, spec.MapId.ToString());

                        // 1. Disabled owning Canvas rejects raw callbacks and public track API.
                        canvas.enabled = false;
                        Assert.IsTrue(trackBtn.IsInteractable(), spec.MapId.ToString());
                        AssertQuestTabBlocked(panel, tracker, trackBtn, beaconTab, questId,
                            true, initialTracked, rows, summary, tracked);
                        canvas.enabled = true;

                        // 2. Ancestor CanvasGroup lock rejects raw callbacks and public track API.
                        var group = root.AddComponent<CanvasGroup>();
                        group.interactable = false;
                        AssertQuestTabBlocked(panel, tracker, trackBtn, beaconTab, questId,
                            true, initialTracked, rows, summary, tracked);
                        group.interactable = true;

                        // 3. Hidden panel rejects raw callbacks and public track API.
                        Assert.IsTrue(panel.Hide(), spec.MapId.ToString());
                        AssertQuestTabBlocked(panel, tracker, trackBtn, beaconTab, questId,
                            false, initialTracked, rows, summary, tracked);
                        Assert.IsTrue(panel.Show(), spec.MapId.ToString());

                        // 4. Restored panel accepts track button click.
                        FindInteractableRow(panel, "QuestRow_", out questId, out trackBtn);
                        trackBtn.onClick.Invoke();
                        Assert.AreEqual(1, tracked, spec.MapId.ToString());
                        Assert.AreEqual(questId, tracker.TrackedQuestId, spec.MapId.ToString());

                        // 5. Programmatic SetTab works while disabled, but fast-travel and tab clicks stay blocked.
                        canvas.enabled = false;
                        Assert.IsTrue(panel.SetTab(RpgQuestJournalTab.FastTravelBeacons), spec.MapId.ToString());
                        FindInteractableRow(panel, "BeaconRow_", out string markerId, out Button travelBtn);
                        Assert.IsNotNull(travelBtn, spec.MapId.ToString());
                        Assert.IsTrue(travelBtn.IsInteractable(), spec.MapId.ToString());
                        int beaconRows = panel.DisplayedRowCount;
                        Assert.Greater(beaconRows, 0, spec.MapId.ToString());

                        travelBtn.onClick.Invoke();
                        activeTab.onClick.Invoke();
                        Assert.IsFalse(panel.TryClickFastTravel(markerId), spec.MapId.ToString());
                        Assert.AreEqual(0, travelled, spec.MapId.ToString());
                        Assert.IsNull(destination, spec.MapId.ToString());
                        Assert.AreEqual(RpgQuestJournalTab.FastTravelBeacons, panel.CurrentTab, spec.MapId.ToString());
                        Assert.AreEqual(beaconRows, panel.DisplayedRowCount, spec.MapId.ToString());

                        // 6. Restored canvas accepts fast-travel and tab switch back to active quests.
                        canvas.enabled = true;
                        travelBtn.onClick.Invoke();
                        Assert.AreEqual(1, travelled, spec.MapId.ToString());
                        Assert.AreEqual(markerId, destination, spec.MapId.ToString());
                        activeTab.onClick.Invoke();
                        Assert.AreEqual(RpgQuestJournalTab.ActiveQuests, panel.CurrentTab, spec.MapId.ToString());
                    }

                    Assert.AreEqual(0, root.transform.childCount, spec.MapId.ToString());
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        private static void FindInteractableRow(
            RpgWorld500xQuestJournalPanel panel,
            string prefix,
            out string id,
            out Button actionButton)
        {
            id = null;
            actionButton = null;
            var content = panel.RootObject.transform.Find("JournalPanel/Rows/Content");
            Assert.IsNotNull(content);
            for (int i = 0; i < content.childCount; i++)
            {
                var child = content.GetChild(i);
                if (!child.name.StartsWith(prefix))
                    continue;
                var btnTransform = child.Find("ActionButton");
                if (btnTransform == null)
                    continue;
                var btn = btnTransform.GetComponent<Button>();
                if (btn != null && btn.IsInteractable())
                {
                    id = child.name.Substring(prefix.Length);
                    actionButton = btn;
                    return;
                }
            }
        }

        private static void AssertQuestTabBlocked(
            RpgWorld500xQuestJournalPanel panel,
            RpgMapExplorationTracker tracker,
            Button trackBtn,
            Button beaconTab,
            string questId,
            bool expectedVisible,
            string expectedTracked,
            int expectedRows,
            string expectedSummary,
            int expectedTrackedCalls)
        {
            trackBtn.onClick.Invoke();
            beaconTab.onClick.Invoke();
            Assert.IsFalse(panel.TryClickTrackQuest(questId));
            Assert.AreEqual(expectedVisible, panel.IsVisible);
            Assert.AreEqual(expectedTrackedCalls, 0);
            Assert.AreEqual(expectedTracked, tracker.TrackedQuestId);
            Assert.AreEqual(RpgQuestJournalTab.ActiveQuests, panel.CurrentTab);
            Assert.AreEqual(expectedRows, panel.DisplayedRowCount);
            Assert.AreEqual(expectedSummary, panel.BodySummaryText);
        }
    }
}
