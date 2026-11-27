using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ReturnHarborCommandHubHostTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static void SeedThroughM09(Dictionary<string, int> values)
        {
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            var manifest = new M04DepartureManifest(
                M03VoyageChoice.QiQueMerchant,
                "Confirmed hull", "A Yuan", 4, "Shallow tender",
                true, 16, 8, "Named receiver", false, false);
            Assert.That(manifest.ConfirmDeparture(), Is.True);
            Assert.That(M04DepartureFacts.TryRecord(manifest, write), Is.True);
            Assert.That(M04DepartureFacts.TryMarkSailed(read, write), Is.True);

            var fog = new FogListeningSession(
                new[] { new FogListeningCue(FogBearing.Ahead, FogCueKind.ClearWater, 80) },
                maxListenFocus: 1, maxHullIntegrity: 2);
            fog.ApplyHelmAction(FogHelmAction.HoldCourse);
            Assert.That(FogListeningFacts.TryRecord(fog, write), Is.True);
            Assert.That(M05LandingFacts.TryRecord(
                new M05LandingReport(3, 10, true, true), read, write), Is.True);
            Assert.That(M06RescuePlanFacts.TryRecord(
                new M06ReunionRecord(
                    M06RescuePlan.OfficialHandover, true,
                    M06LetterDeliveryOutcome.NotCarried, true), read, write), Is.True);
            Assert.That(M07ExtractionFacts.TryRecord(new M07ExtractionRecord(
                M07ExecutedRoute.OfficialHandover,
                M07PumpShutdownState.KeptRunning,
                M07RouteCostFlags.HarborSupplyDebt,
                boardedFirstBatchCount: 10,
                lowerLevelEvacuatedCount: 0,
                M07RusFate.Arrested, M07LedgerState.Preserved,
                M07LiangChoice.StayOnIsland,
                M07YidaChoice.HandToInspector,
                taoJiRescued: false), read, write), Is.True);

            var winch = new M08FirstWinchOrderSession();
            Assert.That(winch.TryAcquirePart(M08WinchPart.ScrapGear), Is.True);
            Assert.That(winch.TryAcquirePart(M08WinchPart.DockCable), Is.True);
            Assert.That(winch.TryAssemble(), Is.True);
            Assert.That(winch.TryTightenCable(), Is.True);
            Assert.That(winch.TryTestLift(), Is.True);
            Assert.That(M08WorkshopDepositFacts.TryConfirm(winch, read, write), Is.True);
            Assert.That(M08FirstWorldNoticeFacts.TryRecord(true, read, write), Is.True);
            Assert.That(M09BerthTableFacts.TryRecord(new M09BerthTableRecord(
                20, 23, M09BerthExchangeFlags.HanQiaoCivilianVoyage,
                M09RuiyaOutcome.Rescued, false, false), read, write), Is.True);
        }

        [Test]
        public void EnforcesCrossHubMutualExclusionAcrossLedgerRosterRegionalAndFinalePanels()
        {
            var root = new GameObject("CommandHubExclusionRoot");
            var values = new Dictionary<string, int>();
            SeedThroughM09(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                using (var hub = new ReturnHarborCommandHubHost(
                    root.transform, read, write,
                    rosterContext: new ReturnRosterCasesHubContext
                    {
                        // This fixture explicitly reaches RC-01's ferry.
                        AtWhiteReedFerry = () => true
                    },
                    regionalContext: new ReturnRegionalStoriesHubContext
                    {
                        // This mutual-exclusion fixture explicitly visits Luting dock.
                        AtJingwanTheater = () => true,
                        LiuLingPresent = () => true,
                        OriginalScriptRead = () => true,
                        RevisedScriptRead = () => true,
                        RehearsalObserved = () => true,
                        DisplacedResidentsHeard = () => true,
                        AlternateStageAndFundingPrepared = () => false,
                        PostshowResidentsForumArranged = () => false,
                        PublicBoycottActuallyDeclared = () => true,
                        AtLutingDock = () => true
                    },
                    finaleContext: new ReturnFinaleFlowContext
                    {
                        // This mutual-exclusion fixture explicitly arrives at Jingwan assembly.
                        AtJingwanAssembly = () => true
                    }))
                {
                    Assert.That(hub.ActiveMode, Is.EqualTo(ReturnCommandHubMode.None));
                    Assert.That(hub.TryBuildProgressSnapshot(out var snap), Is.True);
                    Assert.That(snap.ActiveChapter, Is.EqualTo(ReturnMainChapter.M10Assembly));

                    // Open Story Ledger -> blocks Roster, Regional, and Finale panels.
                    Assert.That(hub.TryOpenStoryLedger(), Is.True);
                    Assert.That(hub.ActiveMode, Is.EqualTo(ReturnCommandHubMode.StoryLedger));
                    Assert.That(hub.TryOpenRosterCase(ReturnRosterCaseId.RC01ChenYan), Is.False);
                    Assert.That(hub.TryOpenRegionalStory(ReturnRegionalStoryId.ST02RiverLanterns), Is.False);
                    Assert.That(hub.TryOpenNextFinaleStage(), Is.False);

                    // Close Ledger -> open RC-01 -> blocks Ledger, Regional, and Finale.
                    hub.CloseActivePanel();
                    Assert.That(hub.ActiveMode, Is.EqualTo(ReturnCommandHubMode.None));
                    Assert.That(hub.TryOpenRosterCase(ReturnRosterCaseId.RC01ChenYan), Is.True);
                    Assert.That(hub.ActiveMode, Is.EqualTo(ReturnCommandHubMode.RosterCase));
                    Assert.That(hub.TryOpenStoryLedger(), Is.False);
                    Assert.That(hub.TryOpenRegionalStory(ReturnRegionalStoryId.ST02RiverLanterns), Is.False);
                    Assert.That(hub.TryOpenNextFinaleStage(), Is.False);

                    // Close RC-01 -> open ST-02 -> blocks Finale.
                    hub.CloseActivePanel();
                    Assert.That(hub.TryOpenRegionalStory(ReturnRegionalStoryId.ST02RiverLanterns), Is.True);
                    Assert.That(hub.ActiveMode, Is.EqualTo(ReturnCommandHubMode.RegionalStory));
                    Assert.That(hub.TryOpenNextFinaleStage(), Is.False);

                    // Close ST-02 -> open ST-01 -> still blocks the Finale panel.
                    hub.CloseActivePanel();
                    Assert.That(hub.TryOpenRegionalStory(ReturnRegionalStoryId.ST01JingwanTheater), Is.True);
                    Assert.That(hub.ActiveMode, Is.EqualTo(ReturnCommandHubMode.RegionalStory));
                    Assert.That(hub.TryOpenNextFinaleStage(), Is.False);

                    // Close ST-01 -> open M10 Finale stage -> blocks Ledger.
                    hub.CloseActivePanel();
                    Assert.That(hub.TryOpenNextFinaleStage(), Is.True);
                    Assert.That(hub.ActiveMode, Is.EqualTo(ReturnCommandHubMode.FinaleFlow));
                    Assert.That(hub.FinaleHub.CurrentStage, Is.EqualTo(ReturnFinaleStage.M10Assembly));
                    Assert.That(hub.TryOpenStoryLedger(), Is.False);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SceneCallbackCannotOpenOtherPanelDuringRegionalDispatch()
        {
            var root = new GameObject("Command hub reentrant dispatch");
            var values = new Dictionary<string, int>();
            SeedThroughM09(values);
            ReturnHarborCommandHubHost hub = null;
            int attempts = 0;
            try
            {
                var regional = new ReturnRegionalStoriesHubContext
                {
                    AtLutingDock = () =>
                    {
                        attempts++;
                        Assert.That(hub.TryOpenStoryLedger(), Is.False);
                        Assert.That(hub.TryOpenRosterCase(ReturnRosterCaseId.RC01ChenYan), Is.False);
                        Assert.That(hub.TryOpenNextFinaleStage(), Is.False);
                        return true;
                    }
                };
                hub = new ReturnHarborCommandHubHost(root.transform, Reader(values),
                    (k, v) => values[k] = v,
                    rosterContext: new ReturnRosterCasesHubContext
                    { AtWhiteReedFerry = () => true },
                    regionalContext: regional,
                    finaleContext: new ReturnFinaleFlowContext
                    { AtJingwanAssembly = () => true });
                Assert.That(hub.TryOpenRegionalStory(ReturnRegionalStoryId.ST02RiverLanterns), Is.True);
                Assert.That(attempts, Is.GreaterThan(0));
                Assert.That(hub.ActiveMode, Is.EqualTo(ReturnCommandHubMode.RegionalStory));
                Assert.That(hub.LedgerHost.IsOpen, Is.False);
                Assert.That(hub.RosterHub.HasActiveCase, Is.False);
                Assert.That(hub.FinaleHub.HasActiveHost, Is.False);
                hub.CloseActivePanel();
                Assert.That(hub.TryOpenStoryLedger(), Is.True);
            }
            finally
            {
                hub?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SceneCallbackClosingCommandHubCannotLeaveLatePanel()
        {
            AssertSceneCallbackCancellingCommandHubCannotLeaveLatePanel(false);
        }

        [Test]
        public void SceneCallbackDisposingCommandHubCannotLeaveLatePanel()
        {
            AssertSceneCallbackCancellingCommandHubCannotLeaveLatePanel(true);
        }

        private static void AssertSceneCallbackCancellingCommandHubCannotLeaveLatePanel(bool disposeHub)
        {
            var root = new GameObject("Command hub cancelled opening");
            var values = new Dictionary<string, int>();
            SeedThroughM09(values);
            ReturnHarborCommandHubHost hub = null;
            int writes = 0;
            try
            {
                var regional = new ReturnRegionalStoriesHubContext
                {
                    AtLutingDock = () =>
                    {
                        if (disposeHub) hub.Dispose();
                        else hub.CloseActivePanel();
                        return true;
                    }
                };
                hub = new ReturnHarborCommandHubHost(root.transform, Reader(values),
                    (k, v) => { writes++; values[k] = v; }, regionalContext: regional);
                Assert.That(hub.TryOpenRegionalStory(ReturnRegionalStoryId.ST02RiverLanterns), Is.False);
                Assert.That(hub.ActiveMode, Is.EqualTo(ReturnCommandHubMode.None));
                Assert.That(hub.RegionalHub.HasActiveStory, Is.False);
                Assert.That(root.transform.childCount, Is.Zero);
                Assert.That(writes, Is.Zero);

                regional.AtLutingDock = () => true;
                Assert.That(hub.TryOpenRegionalStory(ReturnRegionalStoryId.ST02RiverLanterns),
                    Is.EqualTo(!disposeHub));
            }
            finally
            {
                hub?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DisposalClosesActivePanelDisposesAllSubHubsAndBlocksFurtherCalls()
        {
            var root = new GameObject("CommandHubDisposeRoot");
            var values = new Dictionary<string, int>();
            SeedThroughM09(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                var hub = new ReturnHarborCommandHubHost(
                    root.transform, read, write,
                    finaleContext: new ReturnFinaleFlowContext
                    {
                        AtJingwanAssembly = () => true
                    });
                Assert.That(hub.TryOpenNextFinaleStage(), Is.True);
                Assert.That(hub.HasAnyActivePanel, Is.True);

                hub.Dispose();
                Assert.That(hub.IsDisposed, Is.True);
                Assert.That(hub.HasAnyActivePanel, Is.False);
                Assert.That(hub.RosterHub.IsDisposed, Is.True);
                Assert.That(hub.RegionalHub.IsDisposed, Is.True);
                Assert.That(hub.FinaleHub.IsDisposed, Is.True);
                Assert.That(hub.TryOpenStoryLedger(), Is.False);
                Assert.That(hub.TryBuildProgressSnapshot(out _), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
