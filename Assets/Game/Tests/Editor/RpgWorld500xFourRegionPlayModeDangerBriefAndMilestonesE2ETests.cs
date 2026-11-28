using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionPlayModeDangerBriefAndMilestonesE2ETests
    {
        [Test]
        public void AllFourRegions_PlayModeControllerDrivesDangerBriefMilestonesAndCartographyCleanly()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var host = new GameObject("FourRegionDangerBriefMilestones_" + spec.MapId);
                var ctrl = host.AddComponent<RpgWorld500xPlayModeController>();
                try
                {
                    ctrl.Initialize(
                        overrideMap: spec.MapId,
                        saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                        overrideTileSlices: new Sprite[] { null });

                    // 1. Open DangerTacticalBrief via hotkey, switch tab, refresh, and close via UGUI Close button.
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame
                    {
                        RequestedModalToggle = RpgWorld500xModalKind.DangerTacticalBrief
                    }, 0.05f);
                    Assert.AreEqual(RpgWorld500xModalKind.DangerTacticalBrief, ctrl.MenuRouter.ActiveModal, spec.MapId.ToString());

                    var briefRoot = host.transform.Find("World500xPlayModeUiRoot/World500xDangerTacticalBriefHudPanel");
                    Assert.IsNotNull(briefRoot, spec.MapId.ToString());
                    Assert.IsTrue(briefRoot.gameObject.activeSelf, spec.MapId.ToString());

                    var briefModal = briefRoot.Find("DangerBriefModalBox");
                    Assert.IsNotNull(briefModal, spec.MapId.ToString());
                    var allZonesBtn = briefModal.Find("Tab_AllZones").GetComponent<Button>();
                    var refreshBtn = briefModal.Find("RefreshDangerBriefButton").GetComponent<Button>();
                    var closeBriefBtn = briefModal.Find("CloseDangerBriefButton").GetComponent<Button>();
                    Assert.IsNotNull(allZonesBtn, spec.MapId.ToString());
                    Assert.IsNotNull(refreshBtn, spec.MapId.ToString());
                    Assert.IsNotNull(closeBriefBtn, spec.MapId.ToString());

                    allZonesBtn.onClick.Invoke();
                    refreshBtn.onClick.Invoke();
                    closeBriefBtn.onClick.Invoke();
                    Assert.IsFalse(briefRoot.gameObject.activeSelf, spec.MapId.ToString());

                    // 2. Open ExpeditionMilestones via hotkey and verify live ClaimAllButton presence.
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame
                    {
                        RequestedModalToggle = RpgWorld500xModalKind.ExpeditionMilestones
                    }, 0.05f);
                    Assert.AreEqual(RpgWorld500xModalKind.ExpeditionMilestones, ctrl.MenuRouter.ActiveModal, spec.MapId.ToString());

                    var milestonesRoot = host.transform.Find("World500xPlayModeUiRoot/World500xExpeditionMilestonesHudPanel");
                    Assert.IsNotNull(milestonesRoot, spec.MapId.ToString());
                    Assert.IsTrue(milestonesRoot.gameObject.activeSelf, spec.MapId.ToString());
                    var claimAllBtn = milestonesRoot.Find("MilestonesModalBox/ClaimAllButton").GetComponent<Button>();
                    Assert.IsNotNull(claimAllBtn, spec.MapId.ToString());

                    // 3. Switch to CartographyReputation via hotkey, verify exclusivity, then close cleanly.
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame
                    {
                        RequestedModalToggle = RpgWorld500xModalKind.CartographyReputation
                    }, 0.05f);
                    Assert.AreEqual(RpgWorld500xModalKind.CartographyReputation, ctrl.MenuRouter.ActiveModal, spec.MapId.ToString());
                    Assert.IsFalse(milestonesRoot.gameObject.activeSelf, spec.MapId.ToString());

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
    }
}
