using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionPlayModeJournalAndBeaconE2ETests
    {
        [Test]
        public void AllFourRegions_PlayModeControllerQuestJournalTrackingAndBeaconModalExclusivity()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var host = new GameObject("FourRegionJournalBeacon_" + spec.MapId);
                var ctrl = host.AddComponent<RpgWorld500xPlayModeController>();
                try
                {
                    ctrl.Initialize(
                        overrideMap: spec.MapId,
                        saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                        overrideTileSlices: new Sprite[] { null });

                    var tracker = ctrl.Bootstrapper.Driver.Facade.Session.Host.Tracker;

                    // 1. Open QuestJournal via hotkey and click an interactable QuestRow ActionButton.
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame
                    {
                        RequestedModalToggle = RpgWorld500xModalKind.QuestJournal
                    }, 0.05f);
                    Assert.AreEqual(RpgWorld500xModalKind.QuestJournal, ctrl.MenuRouter.ActiveModal, spec.MapId.ToString());

                    Transform journalPanel = FindChildByName(host.transform, "JournalPanel");
                    Assert.IsNotNull(journalPanel, spec.MapId.ToString());
                    Assert.IsTrue(journalPanel.gameObject.activeInHierarchy, spec.MapId.ToString());

                    Transform rowsContent = journalPanel.Find("Rows/Content");
                    Assert.IsNotNull(rowsContent, spec.MapId.ToString());

                    FindInteractableRow(rowsContent, "QuestRow_", out string questId, out Button trackBtn);
                    Assert.IsNotNull(trackBtn, spec.MapId.ToString());
                    trackBtn.onClick.Invoke();
                    Assert.AreEqual(questId, tracker.TrackedQuestId, spec.MapId.ToString());

                    // 2. Switch to FastTravelBeacons tab inside QuestJournal and click an interactable beacon row.
                    var beaconTabBtn = journalPanel.Find("TabBar/Tab_FastTravelBeacons").GetComponent<Button>();
                    Assert.IsNotNull(beaconTabBtn, spec.MapId.ToString());
                    beaconTabBtn.onClick.Invoke();

                    FindInteractableRow(rowsContent, "BeaconRow_", out string markerId, out Button travelBtn);
                    Assert.IsNotNull(travelBtn, spec.MapId.ToString());
                    Assert.IsFalse(string.IsNullOrEmpty(markerId), spec.MapId.ToString());
                    travelBtn.onClick.Invoke();

                    // 3. Switch to FastTravelBeacons modal via hotkey, verify QuestJournal hides, then close.
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame
                    {
                        RequestedModalToggle = RpgWorld500xModalKind.FastTravelBeacons
                    }, 0.05f);
                    Assert.AreEqual(RpgWorld500xModalKind.FastTravelBeacons, ctrl.MenuRouter.ActiveModal, spec.MapId.ToString());
                    Assert.IsFalse(journalPanel.gameObject.activeInHierarchy, spec.MapId.ToString());

                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { CloseModalPressed = true }, 0.05f);
                    Assert.AreEqual(RpgWorld500xModalKind.None, ctrl.MenuRouter.ActiveModal, spec.MapId.ToString());

                    ctrl.Shutdown();
                    Assert.AreEqual(0, host.transform.childCount, spec.MapId.ToString());
                }
                finally
                {
                    ctrl.Shutdown();
                    Object.DestroyImmediate(host);
                }
            }
        }

        private static Transform FindChildByName(Transform root, string targetName)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == targetName)
                    return all[i];
            }
            return null;
        }

        private static void FindInteractableRow(
            Transform content,
            string prefix,
            out string id,
            out Button actionButton)
        {
            id = null;
            actionButton = null;
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
    }
}
