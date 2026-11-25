using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class RpgWorld500xMenuRouterTests
    {
        [Test]
        public void MenuRouter_CampRestWritesLoadableAutoSaveWithInjectedManager()
        {
            MenuRouter_CampRestWritesLoadableAutoSaveInSharedSlots(true);
        }

        [Test]
        public void MenuRouter_CampRestWritesLoadableAutoSaveWithDefaultManager()
        {
            MenuRouter_CampRestWritesLoadableAutoSaveInSharedSlots(false);
        }

        private void MenuRouter_CampRestWritesLoadableAutoSaveInSharedSlots(bool injectManager)
        {
            var root = new GameObject("World500xCampRestAutoSaveTestRoot");
            var manager = injectManager ? new RpgWorld500xSaveSlotManager(
                new RpgWorld500xInMemorySaveSlotStorage()) : null;
            try
            {
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var router = new RpgWorld500xMenuRouter(
                    root.transform, boot, new RpgWorld500xCampHavenService(), manager))
                {
                    boot.Driver.Facade.BountyAndSupply.AddSaltSilver(30);
                    Assert.IsTrue(router.OpenModal(RpgWorld500xModalKind.WorldAtlas));
                    Assert.IsTrue(router.AtlasPanel.TryClickCampRest());
                    Assert.That(router.AtlasPanel.StatusBannerText, Does.Contain("已存档"));
                    int restedDay = boot.Driver.Facade.Atmosphere.DayCount;
                    float restedTime = boot.Driver.Facade.Atmosphere.TimeOfDayMinutes;
                    int restedBalance = boot.Driver.Facade.BountyAndSupply.SaltSilverBalance;
                    if (manager != null)
                    {
                        var saved = manager.InspectSlot(RpgWorld500xSaveSlotId.AutoSave);
                        Assert.IsTrue(saved.IsOccupied);
                        Assert.IsFalse(saved.IsCorrupted);
                        Assert.AreEqual(restedDay, saved.DayCount);
                        Assert.AreEqual(restedBalance, saved.SaltSilverBalance);
                    }

                    boot.Driver.Facade.BountyAndSupply.AddSaltSilver(20);
                    Assert.IsTrue(router.OpenModal(RpgWorld500xModalKind.SaveSlots));
                    Assert.IsTrue(router.SaveSlotPanel.TryClickLoad(RpgWorld500xSaveSlotId.AutoSave));
                    Assert.That(router.SaveSlotPanel.StatusText, Does.Contain("已读取"));
                    Assert.AreEqual(restedDay, boot.Driver.Facade.Atmosphere.DayCount);
                    Assert.AreEqual(restedTime, boot.Driver.Facade.Atmosphere.TimeOfDayMinutes);
                    Assert.AreEqual(restedBalance, boot.Driver.Facade.BountyAndSupply.SaltSilverBalance);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MenuRouter_OwnsFrameLoopAndSuppressesAutomaticDriverUpdate()
        {
            var root = new GameObject("World500xFrameOwnershipTestRoot");
            try
            {
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    var driver = boot.Driver;
                    Assert.IsTrue(driver.AutomaticFrameUpdates);
                    Assert.IsEmpty(driver.LastEnvironmentCaptionZh);
                    using (var router = new RpgWorld500xMenuRouter(
                        root.transform, boot, new RpgWorld500xCampHavenService()))
                    {
                        Assert.IsFalse(driver.AutomaticFrameUpdates);
                        Assert.IsTrue(router.OpenModal(RpgWorld500xModalKind.WorldAtlas));
                        float clock = driver.Facade.Atmosphere.TimeOfDayMinutes;
                        Vector2 position = driver.Facade.Session.Host.PlayerPosition;

                        // Invoke the actual Update method directly: Unity SendMessage
                        // rejects non-ExecuteAlways behaviours in EditMode before
                        // the method runs (ShouldRunBehaviour assertion).
                        var update = typeof(RpgWorld500xRuntimeDriver).GetMethod("Update",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        Assert.IsNotNull(update);
                        update.Invoke(driver, null);
                        Assert.IsEmpty(driver.LastEnvironmentCaptionZh);
                        Assert.AreEqual(clock, driver.Facade.Atmosphere.TimeOfDayMinutes);
                        Assert.AreEqual(position, driver.Facade.Session.Host.PlayerPosition);

                        router.StepFrameWithModalGuard(Vector2.right, 1f, true, true);
                        Assert.IsNotEmpty(driver.LastEnvironmentCaptionZh);
                        Assert.AreEqual(clock, driver.Facade.Atmosphere.TimeOfDayMinutes);
                        Assert.AreEqual(position, driver.Facade.Session.Host.PlayerPosition);

                        router.CloseAll();
                        router.StepFrameWithModalGuard(Vector2.zero, 1f);
                        Assert.Greater(driver.Facade.Atmosphere.TimeOfDayMinutes, clock);
                    }

                    Assert.IsTrue(driver.AutomaticFrameUpdates,
                        "Disposing the router restores standalone driver behavior.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MenuRouter_DisposeClosesSceneOwnedJournalBeforeRestoringWorldInput()
        {
            var root = new GameObject("World500xJournalRouterDisposeRoot");
            try
            {
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    var driver = boot.Driver;
                    var journal = driver.Facade.Session.JournalPanel;
                    using (var router = new RpgWorld500xMenuRouter(
                        root.transform, boot, new RpgWorld500xCampHavenService()))
                    {
                        Assert.IsTrue(router.OpenModal(RpgWorld500xModalKind.QuestJournal));
                        Assert.IsTrue(journal.IsVisible);
                        Assert.IsFalse(driver.AutomaticFrameUpdates);
                        router.Dispose();
                        Assert.IsFalse(journal.IsVisible);
                        Assert.IsFalse(journal.IsDisposed, "The scene still owns its journal.");
                        Assert.IsTrue(driver.AutomaticFrameUpdates);
                        Assert.AreEqual(RpgWorld500xModalKind.None, router.ActiveModal);
                    }

                    // A fresh router can reopen the same scene-owned journal.
                    using (var next = new RpgWorld500xMenuRouter(
                        root.transform, boot, new RpgWorld500xCampHavenService()))
                    {
                        Assert.IsTrue(next.OpenModal(RpgWorld500xModalKind.QuestJournal));
                        Assert.IsTrue(journal.IsVisible);
                    }
                    Assert.IsFalse(journal.IsVisible);
                    Assert.IsTrue(driver.AutomaticFrameUpdates);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void UndefinedModalId_DoesNotCloseActivePanelOrReportSuccess()
        {
            var root = new GameObject("World500xInvalidModalGuardTestRoot");
            try
            {
                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var router = new RpgWorld500xMenuRouter(
                    root.transform, boot, new RpgWorld500xCampHavenService()))
                {
                    Assert.IsTrue(router.OpenModal(RpgWorld500xModalKind.QuestJournal));
                    Assert.IsTrue(boot.Driver.Facade.Session.JournalPanel.IsVisible);
                    Assert.IsFalse(router.OpenModal((RpgWorld500xModalKind)255),
                        "Unknown serialized panel IDs must be rejected.");
                    Assert.IsFalse(router.ToggleModal((RpgWorld500xModalKind)255));
                    Assert.AreEqual(RpgWorld500xModalKind.QuestJournal, router.ActiveModal);
                    Assert.IsTrue(boot.Driver.Facade.Session.JournalPanel.IsVisible,
                        "Invalid panel requests must not dismiss the active journal.");

                    // Explicit None still means close all via OpenModal.
                    Assert.IsTrue(router.OpenModal(RpgWorld500xModalKind.None));
                    Assert.AreEqual(RpgWorld500xModalKind.None, router.ActiveModal);
                }

                Assert.AreEqual(0, root.transform.childCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MenuRouter_EnforcesMutualExclusionBlocksInputWhenOpenAndDisposesWithZeroLeak()
        {
            var root = new GameObject("World500xMenuRouterTestRoot");
            try
            {
                // Warm up one-time UGUI/font caches before steady-state leak baseline.
                using (var warmupBoot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                {
                    var warmupHaven = new RpgWorld500xCampHavenService();
                    using (var warmupRouter = new RpgWorld500xMenuRouter(root.transform, warmupBoot, warmupHaven))
                    {
                        warmupRouter.OpenModal(RpgWorld500xModalKind.WorldAtlas);
                        warmupRouter.OpenModal(RpgWorld500xModalKind.CampMerchant);
                        warmupRouter.OpenModal(RpgWorld500xModalKind.Bestiary);
                        warmupRouter.OpenModal(RpgWorld500xModalKind.FastTravelBeacons);
                        warmupRouter.OpenModal(RpgWorld500xModalKind.CartographyReputation);
                        warmupRouter.OpenModal(RpgWorld500xModalKind.SaveSlots);
                        warmupRouter.OpenModal(RpgWorld500xModalKind.FieldRumorAndLore);
                        warmupRouter.OpenModal(RpgWorld500xModalKind.ExpeditionMilestones);
                        warmupRouter.OpenModal(RpgWorld500xModalKind.CacheAndShortcut);
                        warmupRouter.OpenModal(RpgWorld500xModalKind.ExpeditionDigest);
                        warmupRouter.OpenModal(RpgWorld500xModalKind.BountyBoard);
                        warmupRouter.OpenModal(RpgWorld500xModalKind.ExpeditionReadiness);
                        warmupRouter.OpenModal(RpgWorld500xModalKind.DangerTacticalBrief);
                        warmupRouter.OpenModal(RpgWorld500xModalKind.WorldGazetteer);
                        warmupRouter.CloseAll();
                    }
                }

                int texBefore = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                int spriteBefore = Resources.FindObjectsOfTypeAll<Sprite>().Length;

                var store = new Dictionary<string, int>(StringComparer.Ordinal);
                var haven = new RpgWorld500xCampHavenService(
                    k => store.TryGetValue(k, out int v) ? v : 0,
                    (k, v) => store[k] = v);

                using (var boot = new RpgWorld500xSceneBootstrapper(
                    initialMapId: RpgWorldMapId.Soundwell,
                    overrideTileSlices: new Sprite[] { null }))
                using (var router = new RpgWorld500xMenuRouter(root.transform, boot, haven))
                {
                    Assert.AreEqual(RpgWorld500xModalKind.None, router.ActiveModal);
                    Assert.IsFalse(router.HasAnyModalOpen);

                    // Opening QuestJournal sets ActiveModal to QuestJournal.
                    Assert.IsTrue(router.ToggleModal(RpgWorld500xModalKind.QuestJournal));
                    Assert.AreEqual(RpgWorld500xModalKind.QuestJournal, router.ActiveModal);
                    Assert.IsTrue(boot.Driver.Facade.Session.JournalPanel.IsVisible);

                    // Opening WorldAtlas automatically closes QuestJournal (mutual exclusion).
                    Assert.IsTrue(router.OpenModal(RpgWorld500xModalKind.WorldAtlas));
                    Assert.AreEqual(RpgWorld500xModalKind.WorldAtlas, router.ActiveModal);
                    Assert.IsFalse(boot.Driver.Facade.Session.JournalPanel.IsVisible);
                    Assert.IsTrue(router.AtlasPanel.IsVisible);

                    // Movement is blocked while WorldAtlas modal is open.
                    Vector2 posBefore = boot.Driver.Facade.Session.Host.PlayerPosition;
                    router.StepFrameWithModalGuard(Vector2.right, 0.5f, sprinting: true, interactPressed: true);
                    Assert.AreEqual(posBefore, boot.Driver.Facade.Session.Host.PlayerPosition);

                    // Switching across all remaining 13 panels enforces strict mutual exclusion.
                    Assert.IsTrue(router.OpenModal(RpgWorld500xModalKind.CampMerchant));
                    Assert.AreEqual(RpgWorld500xModalKind.CampMerchant, router.ActiveModal);
                    Assert.IsFalse(router.AtlasPanel.IsVisible);
                    Assert.IsTrue(router.MerchantPanel.IsVisible);

                    Assert.IsTrue(router.OpenModal(RpgWorld500xModalKind.Bestiary));
                    Assert.AreEqual(RpgWorld500xModalKind.Bestiary, router.ActiveModal);
                    Assert.IsFalse(router.MerchantPanel.IsVisible);
                    Assert.IsTrue(router.BestiaryPanel.IsVisible);

                    Assert.IsTrue(router.OpenModal(RpgWorld500xModalKind.FastTravelBeacons));
                    Assert.AreEqual(RpgWorld500xModalKind.FastTravelBeacons, router.ActiveModal);
                    Assert.IsFalse(router.BestiaryPanel.IsVisible);
                    Assert.IsTrue(router.BeaconWarpPanel.IsVisible);

                    Assert.IsTrue(router.OpenModal(RpgWorld500xModalKind.CartographyReputation));
                    Assert.AreEqual(RpgWorld500xModalKind.CartographyReputation, router.ActiveModal);
                    Assert.IsFalse(router.BeaconWarpPanel.IsVisible);
                    Assert.IsTrue(router.CartographyPanel.IsVisible);

                    Assert.IsTrue(router.OpenModal(RpgWorld500xModalKind.SaveSlots));
                    Assert.AreEqual(RpgWorld500xModalKind.SaveSlots, router.ActiveModal);
                    Assert.IsFalse(router.CartographyPanel.IsVisible);
                    Assert.IsTrue(router.SaveSlotPanel.IsVisible);

                    Assert.IsTrue(router.OpenModal(RpgWorld500xModalKind.FieldRumorAndLore));
                    Assert.AreEqual(RpgWorld500xModalKind.FieldRumorAndLore, router.ActiveModal);
                    Assert.IsFalse(router.SaveSlotPanel.IsVisible);
                    Assert.IsTrue(router.RumorLorePanel.IsVisible);

                    Assert.IsTrue(router.OpenModal(RpgWorld500xModalKind.ExpeditionMilestones));
                    Assert.AreEqual(RpgWorld500xModalKind.ExpeditionMilestones, router.ActiveModal);
                    Assert.IsFalse(router.RumorLorePanel.IsVisible);
                    Assert.IsTrue(router.MilestonesPanel.IsVisible);

                    Assert.IsTrue(router.OpenModal(RpgWorld500xModalKind.CacheAndShortcut));
                    Assert.AreEqual(RpgWorld500xModalKind.CacheAndShortcut, router.ActiveModal);
                    Assert.IsFalse(router.MilestonesPanel.IsVisible);
                    Assert.IsTrue(router.CacheShortcutPanel.IsVisible);

                    Assert.IsTrue(router.OpenModal(RpgWorld500xModalKind.ExpeditionDigest));
                    Assert.AreEqual(RpgWorld500xModalKind.ExpeditionDigest, router.ActiveModal);
                    Assert.IsFalse(router.CacheShortcutPanel.IsVisible);
                    Assert.IsTrue(router.ExpeditionDigestPanel.IsVisible);

                    Assert.IsTrue(router.OpenModal(RpgWorld500xModalKind.BountyBoard));
                    Assert.AreEqual(RpgWorld500xModalKind.BountyBoard, router.ActiveModal);
                    Assert.IsFalse(router.ExpeditionDigestPanel.IsVisible);
                    Assert.IsTrue(router.BountyBoardPanel.IsVisible);

                    Assert.IsTrue(router.OpenModal(RpgWorld500xModalKind.ExpeditionReadiness));
                    Assert.AreEqual(RpgWorld500xModalKind.ExpeditionReadiness, router.ActiveModal);
                    Assert.IsFalse(router.BountyBoardPanel.IsVisible);
                    Assert.IsTrue(router.ReadinessPanel.IsVisible);

                    Assert.IsTrue(router.OpenModal(RpgWorld500xModalKind.DangerTacticalBrief));
                    Assert.AreEqual(RpgWorld500xModalKind.DangerTacticalBrief, router.ActiveModal);
                    Assert.IsFalse(router.ReadinessPanel.IsVisible);
                    Assert.IsTrue(router.DangerBriefPanel.IsVisible);

                    Assert.IsTrue(router.OpenModal(RpgWorld500xModalKind.WorldGazetteer));
                    Assert.AreEqual(RpgWorld500xModalKind.WorldGazetteer, router.ActiveModal);
                    Assert.IsFalse(router.DangerBriefPanel.IsVisible);
                    Assert.IsTrue(router.GazetteerPanel.IsVisible);

                    // Toggling active panel again closes all modals.
                    Assert.IsTrue(router.ToggleModal(RpgWorld500xModalKind.WorldGazetteer));
                    Assert.AreEqual(RpgWorld500xModalKind.None, router.ActiveModal);
                    Assert.IsFalse(router.HasAnyModalOpen);
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
