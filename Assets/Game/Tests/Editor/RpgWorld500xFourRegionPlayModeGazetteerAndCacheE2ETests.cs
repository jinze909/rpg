using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionPlayModeGazetteerAndCacheE2ETests
    {
        [Test]
        public void AllFourRegions_PlayModeControllerDrivesGazetteerCacheAndDigestModalsCleanly()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var host = new GameObject("FourRegionGazetteerCacheDigest_" + spec.MapId);
                var ctrl = host.AddComponent<RpgWorld500xPlayModeController>();
                try
                {
                    ctrl.Initialize(
                        overrideMap: spec.MapId,
                        saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                        overrideTileSlices: new Sprite[] { null });

                    // 1. Open WorldGazetteer via hotkey, toggle scope/tab, and close via UGUI Close button.
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame
                    {
                        RequestedModalToggle = RpgWorld500xModalKind.WorldGazetteer
                    }, 0.05f);
                    Assert.AreEqual(RpgWorld500xModalKind.WorldGazetteer, ctrl.MenuRouter.ActiveModal, spec.MapId.ToString());

                    var gazetteerRoot = host.transform.Find("World500xPlayModeUiRoot/World500xWorldGazetteerHudPanel");
                    Assert.IsNotNull(gazetteerRoot, spec.MapId.ToString());
                    Assert.IsTrue(gazetteerRoot.gameObject.activeSelf, spec.MapId.ToString());

                    var gazetteerModal = gazetteerRoot.Find("GazetteerModalBox");
                    Assert.IsNotNull(gazetteerModal, spec.MapId.ToString());
                    var scopeBtn = gazetteerModal.Find("ToggleSearchScopeButton").GetComponent<Button>();
                    var overviewBtn = gazetteerModal.Find("Tab_RegionOverview").GetComponent<Button>();
                    var closeGazetteerBtn = gazetteerModal.Find("CloseGazetteerButton").GetComponent<Button>();
                    Assert.IsNotNull(scopeBtn, spec.MapId.ToString());
                    Assert.IsNotNull(overviewBtn, spec.MapId.ToString());
                    Assert.IsNotNull(closeGazetteerBtn, spec.MapId.ToString());

                    scopeBtn.onClick.Invoke();
                    overviewBtn.onClick.Invoke();
                    closeGazetteerBtn.onClick.Invoke();
                    Assert.IsFalse(gazetteerRoot.gameObject.activeSelf, spec.MapId.ToString());

                    // 2. Open CacheAndShortcut via hotkey and switch between Shortcuts and HiddenCaches tabs.
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame
                    {
                        RequestedModalToggle = RpgWorld500xModalKind.CacheAndShortcut
                    }, 0.05f);
                    Assert.AreEqual(RpgWorld500xModalKind.CacheAndShortcut, ctrl.MenuRouter.ActiveModal, spec.MapId.ToString());

                    var cacheRoot = host.transform.Find("World500xPlayModeUiRoot/World500xCacheAndShortcutHudPanel");
                    Assert.IsNotNull(cacheRoot, spec.MapId.ToString());
                    Assert.IsTrue(cacheRoot.gameObject.activeSelf, spec.MapId.ToString());

                    var cacheModal = cacheRoot.Find("CacheShortcutModalBox");
                    Assert.IsNotNull(cacheModal, spec.MapId.ToString());
                    var shortcutsBtn = cacheModal.Find("Tab_Shortcuts").GetComponent<Button>();
                    var cachesBtn = cacheModal.Find("Tab_HiddenCaches").GetComponent<Button>();
                    Assert.IsNotNull(shortcutsBtn, spec.MapId.ToString());
                    Assert.IsNotNull(cachesBtn, spec.MapId.ToString());

                    shortcutsBtn.onClick.Invoke();
                    cachesBtn.onClick.Invoke();

                    // 3. Switch directly to ExpeditionDigest via hotkey, verify exclusivity, then close.
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame
                    {
                        RequestedModalToggle = RpgWorld500xModalKind.ExpeditionDigest
                    }, 0.05f);
                    Assert.AreEqual(RpgWorld500xModalKind.ExpeditionDigest, ctrl.MenuRouter.ActiveModal, spec.MapId.ToString());
                    Assert.IsFalse(cacheRoot.gameObject.activeSelf, spec.MapId.ToString());

                    var digestRoot = host.transform.Find("World500xPlayModeUiRoot/World500xExpeditionDigestHudPanel");
                    Assert.IsNotNull(digestRoot, spec.MapId.ToString());
                    Assert.IsTrue(digestRoot.gameObject.activeSelf, spec.MapId.ToString());

                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame { CloseModalPressed = true }, 0.05f);
                    Assert.AreEqual(RpgWorld500xModalKind.None, ctrl.MenuRouter.ActiveModal, spec.MapId.ToString());
                    Assert.IsFalse(digestRoot.gameObject.activeSelf, spec.MapId.ToString());

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
