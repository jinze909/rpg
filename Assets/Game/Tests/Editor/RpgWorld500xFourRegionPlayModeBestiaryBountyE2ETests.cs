using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;
using UnityEngine.UI;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xFourRegionPlayModeBestiaryBountyE2ETests
    {
        [Test]
        public void AllFourRegions_PlayModeControllerBestiaryStudyAndBountyClaimPersistAcrossSaveReload()
        {
            IReadOnlyList<RpgExpandedMapSpec> specs = RpgExpandedWorldScale.AllSpecs;
            Assert.AreEqual(4, specs.Count);

            foreach (RpgExpandedMapSpec spec in specs)
            {
                var host = new GameObject("FourRegionBestiaryBounty_" + spec.MapId);
                var ctrl = host.AddComponent<RpgWorld500xPlayModeController>();
                try
                {
                    ctrl.Initialize(
                        overrideMap: spec.MapId,
                        saveStorage: new RpgWorld500xInMemorySaveSlotStorage(),
                        overrideTileSlices: new Sprite[] { null });

                    var facade = ctrl.Bootstrapper.Driver.Facade;
                    var shop = facade.BountyAndSupply;
                    int initialSilver = shop.SaltSilverBalance;

                    // 1. Open Bestiary via hotkey and study the first dossier through live UGUI StudyButton.
                    var dossiers = facade.ThreatCodex.BuildMapDossiers(facade.Session.Host.Layout);
                    Assert.Greater(dossiers.Count, 0, spec.MapId.ToString());
                    string markerId = dossiers[0].MarkerId;
                    int studyBonus = dossiers[0].Profile.CodexStudySilverBonus;
                    Assert.Greater(studyBonus, 0, spec.MapId.ToString());

                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame
                    {
                        RequestedModalToggle = RpgWorld500xModalKind.Bestiary
                    }, 0.05f);
                    Assert.AreEqual(RpgWorld500xModalKind.Bestiary, ctrl.MenuRouter.ActiveModal, spec.MapId.ToString());

                    var studyBtn = host.transform
                        .Find("World500xPlayModeUiRoot/World500xBestiaryHudPanel/BestiaryModalBox/DossierRows/DossierRow_" +
                            markerId + "/StudyButton")
                        .GetComponent<Button>();
                    Assert.IsNotNull(studyBtn, spec.MapId.ToString());
                    studyBtn.onClick.Invoke();
                    int afterStudySilver = initialSilver + studyBonus;
                    Assert.AreEqual(afterStudySilver, shop.SaltSilverBalance, spec.MapId.ToString());

                    // 2. Satisfy a regional bounty, switch to BountyBoard via hotkey, and claim idempotently.
                    MakeRegionalBountyEligible(facade, spec.MapId);
                    ctrl.ProcessInputFrame(new RpgWorld500xInputFrame
                    {
                        RequestedModalToggle = RpgWorld500xModalKind.BountyBoard
                    }, 0.05f);
                    Assert.AreEqual(RpgWorld500xModalKind.BountyBoard, ctrl.MenuRouter.ActiveModal, spec.MapId.ToString());

                    var claimBtn = host.transform
                        .Find("World500xPlayModeUiRoot/World500xBountyBoardHudPanel/BountyBoardModalBox/ClaimEligibleBountiesButton")
                        .GetComponent<Button>();
                    Assert.IsNotNull(claimBtn, spec.MapId.ToString());
                    Assert.IsTrue(claimBtn.IsInteractable(), spec.MapId.ToString());

                    claimBtn.onClick.Invoke();
                    int afterClaimSilver = shop.SaltSilverBalance;
                    Assert.Greater(afterClaimSilver, afterStudySilver, spec.MapId.ToString());

                    // Repeat click must be idempotent.
                    claimBtn.onClick.Invoke();
                    Assert.AreEqual(afterClaimSilver, shop.SaltSilverBalance, spec.MapId.ToString());

                    // 3. Export save snapshot and verify restored bootstrapper preserves studied dossier & wallet.
                    string saved = ctrl.Bootstrapper.ExportSaveJson();
                    using (var restored = new RpgWorld500xSceneBootstrapper(
                        initialMapId: spec.MapId,
                        overrideTileSlices: new Sprite[] { null }))
                    {
                        Assert.IsTrue(restored.TryImportSaveJson(saved, out string error), error);
                        Assert.AreEqual(
                            afterClaimSilver,
                            restored.Driver.Facade.BountyAndSupply.SaltSilverBalance,
                            spec.MapId.ToString());

                        var restoredDossiers = restored.Driver.Facade.ThreatCodex.BuildMapDossiers(
                            restored.Driver.Facade.Session.Host.Layout);
                        Assert.IsTrue(restoredDossiers[0].IsStudied, spec.MapId.ToString());
                    }

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

        private static void MakeRegionalBountyEligible(RpgWorld500xGrandRuntimeFacade facade, RpgWorldMapId mapId)
        {
            if (mapId == RpgWorldMapId.RedSaltHarbor)
            {
                var host = facade.Session.Host;
                var markers = host.Layout.Markers;
                Assert.GreaterOrEqual(markers.Count, 8);
                for (int i = 0; i < 8; i++)
                {
                    if (!host.Tracker.IsMarkerDiscovered(markers[i].Id))
                        Assert.IsTrue(host.Tracker.TryDiscoverMarker(markers[i].Id));
                }
            }
            else
            {
                IReadOnlyList<RpgWorldShortcutDef> shortcuts = RpgWorld500xContentCatalog.GetShortcuts(mapId);
                Assert.GreaterOrEqual(shortcuts.Count, 2, mapId.ToString());
                for (int i = 0; i < 2; i++)
                {
                    var pos = new Vector2(
                        shortcuts[i].TriggerTile.x + 0.5f,
                        shortcuts[i].TriggerTile.y + 0.5f);
                    Assert.IsTrue(facade.ActivityDirector.CachesAndShortcuts
                        .TryUnlockNearbyShortcut(pos, 6f, out _));
                }
            }
        }
    }
}
