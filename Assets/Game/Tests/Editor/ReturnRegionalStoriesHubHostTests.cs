using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ReturnRegionalStoriesHubHostTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        // Existing positive hub tests explicitly simulate having reached the
        // corresponding world locations. Default production context is unproven.
        private static ReturnRegionalStoriesHubContext ObservedTestLocations()
        {
            return new ReturnRegionalStoriesHubContext
            {
                AtJingwanTheater = () => true,
                LiuLingPresent = () => true,
                OriginalScriptRead = () => true,
                RevisedScriptRead = () => true,
                RehearsalObserved = () => true,
                DisplacedResidentsHeard = () => true,
                AlternateStageAndFundingPrepared = () => false,
                PostshowResidentsForumArranged = () => false,
                PublicBoycottActuallyDeclared = () => false,
                AtLutingDock = () => true,
                AtZhonglingBathhouse = () => true,
                AtJingwanCourtyard = () => true,
                TaoJiAtWoodshop = () => true
            };
        }

        private sealed class HubEchoValleyWorldEvidence : IST04EchoValleyWorldEvidence
        {
            public bool AtValley => true;
            public bool SangJingAndMengHuiPresent => true;
            public bool HasInspected(ST04RoadClue clue) => false;
            public bool HasEnteredVia(ST04ValleyEntry approach) => false;
            public int SurveyorsActuallyRescued => 0;
            public bool ClearContinuousEchoAttractsBeasts => false;
            public bool LowVoicesDoNotAttractBeasts => false;
            public bool WetBellLosesBeastTarget => false;
            public bool AncientRainAmplifierIdentified => false;
            public bool QuarryWaterDiversionConfirmed => false;
            public bool HasEnteredChamberVia(ST04ChamberEntry approach) => false;
            public bool CompanionsMarkedBeforeDrain => false;
            public bool DrainActuallyOpened => false;
            public bool HasResolvedBeastsAs(ST04BeastHandling outcome) => false;
            public bool RoadOutcomeActuallyEstablished(ST04RoadResolution outcome) => false;
            public bool LongRoadActuallyFunded => false;
            public bool AlternativeWorksiteActuallyArranged => false;
        }

        private static void SeedM07TaoJi(Dictionary<string, int> values, bool taoJiRescued)
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
                taoJiRescued: taoJiRescued), read, write), Is.True);
        }

        [Test]
        public void UnprovenOrMissingSceneContextCannotOpenRegionalStories()
        {
            var root = new GameObject("Unproven regional world context");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            var context = new ReturnRegionalStoriesHubContext();

            try
            {
                using (var hub = new ReturnRegionalStoriesHubHost(
                    root.transform, context, read, write))
                {
                    Assert.That(hub.IsStoryAvailable(ReturnRegionalStoryId.ST02RiverLanterns),
                        Is.True, "Unsettled story availability is not proof of being at its scene.");
                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST01JingwanTheater), Is.False);
                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST02RiverLanterns), Is.False);
                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST03Bathhouse), Is.False);
                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST04EchoValley), Is.False);
                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST05TwoDoors), Is.False);

                    // Even a rescued companion does not teleport to the woodshop.
                    SeedM07TaoJi(values, taoJiRescued: true);
                    Assert.That(hub.IsStoryAvailable(ReturnRegionalStoryId.ST06TaoJiHomecoming),
                        Is.True);
                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST06TaoJiHomecoming),
                        Is.False);

                    // An explicitly missing callback must also fail closed, not throw.
                    context.AtLutingDock = null;
                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST02RiverLanterns),
                        Is.False);
                    Assert.That(hub.HasActiveStory, Is.False);
                    Assert.That(root.transform.childCount, Is.Zero);
                    Assert.That(hub.CountSettledStories(), Is.Zero);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HubEnforcesST06TaoJiGateAndSingleActiveStoryExclusion()
        {
            var root = new GameObject("RegionalHubGateRoot");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                using (var hub = new ReturnRegionalStoriesHubHost(
                    root.transform, ObservedTestLocations(), read, write))
                {
                    Assert.That(ReturnRegionalStoriesHubHost.IsPermanentRegionalStory(
                        ReturnRegionalStoryId.ST02RiverLanterns), Is.True);
                    Assert.That(ReturnRegionalStoriesHubHost.IsPermanentRegionalStory(
                        ReturnRegionalStoryId.ST06TaoJiHomecoming), Is.False);

                    // ST-06 is locked without M07 Tao Ji rescue, while ST-02 is available.
                    Assert.That(hub.IsStoryAvailable(ReturnRegionalStoryId.ST06TaoJiHomecoming), Is.False);
                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST06TaoJiHomecoming), Is.False);
                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST02RiverLanterns), Is.True);
                    Assert.That(hub.ActiveST02Host, Is.Not.Null);

                    // Cannot open ST-03 while ST-02 is active and unsettled.
                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST03Bathhouse), Is.False);
                    hub.CloseActiveStory();

                    // Seed M07 with Tao Ji NOT rescued -> ST-06 still locked.
                    SeedM07TaoJi(values, taoJiRescued: false);
                    Assert.That(hub.IsStoryAvailable(ReturnRegionalStoryId.ST06TaoJiHomecoming), Is.False);

                    // Reset M07 and re-seed with Tao Ji rescued -> ST-06 opens cleanly.
                    values.Clear();
                    SeedM07TaoJi(values, taoJiRescued: true);
                    Assert.That(hub.IsStoryAvailable(ReturnRegionalStoryId.ST06TaoJiHomecoming), Is.True);
                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST06TaoJiHomecoming), Is.True);
                    Assert.That(hub.ActiveST06Host, Is.Not.Null);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EchoValleyDispatchRequiresLiveWorldEvidenceAndPermanentSettlementPreventsReopen()
        {
            var root = new GameObject("RegionalHub ST04 Echo Valley");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            var world = new HubEchoValleyWorldEvidence();
            var context = new ReturnRegionalStoriesHubContext
            {
                ST04EchoValleyWorld = () => world
            };

            try
            {
                using (var hub = new ReturnRegionalStoriesHubHost(
                    root.transform, context, read, write))
                {
                    Assert.That(ReturnRegionalStoriesHubHost.IsPermanentRegionalStory(
                        ReturnRegionalStoryId.ST04EchoValley), Is.True);
                    Assert.That(hub.IsStoryAvailable(ReturnRegionalStoryId.ST04EchoValley), Is.True);
                    context.ST04EchoValleyWorld = () => null;
                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST04EchoValley), Is.False,
                        "A registered provider that has no live world must still fail closed.");
                    Assert.That(root.transform.childCount, Is.Zero);

                    context.ST04EchoValleyWorld = () => world;
                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST04EchoValley), Is.True);
                    Assert.That(hub.ActiveStory, Is.EqualTo(ReturnRegionalStoryId.ST04EchoValley));
                    Assert.That(hub.ActiveST04Host, Is.Not.Null);
                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST02RiverLanterns), Is.False,
                        "An unsettled regional story keeps the hub's single-active-story lock.");
                    hub.CloseActiveStory();

                    var record = new ST04EchoValleyRecord(
                        ST04ValleyEntry.EnterQuietly,
                        ST04ChamberEntry.ClearGuardBeast,
                        ST04BeastHandling.Slain,
                        ST04RoadResolution.KillAdultsAndCloseWell,
                        2, true, true, true, false, false);
                    Assert.That(ST04EchoValleyFacts.TryRecord(record, read, write), Is.True);
                    Assert.That(hub.IsStorySettled(ReturnRegionalStoryId.ST04EchoValley), Is.True);
                    Assert.That(hub.IsStoryAvailable(ReturnRegionalStoryId.ST04EchoValley), Is.False);
                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST04EchoValley), Is.False);
                    Assert.That(hub.CountSettledStories(), Is.EqualTo(1));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SettlingST02AllowsCleanTransitionToST05AndPreventsReopen()
        {
            var root = new GameObject("RegionalHubTransitionRoot");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                using (var hub = new ReturnRegionalStoriesHubHost(
                    root.transform, ObservedTestLocations(), read, write))
                {
                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST02RiverLanterns), Is.True);
                    Assert.That(ST02RiverLanternsFacts.TryRecord(new ST02RiverLanternsRecord(
                        ST02InspectionFlags.All, true, true,
                        ST02TrialDisclosure.PublishToBoth,
                        ST02BidMode.JointBidWithWrittenTerms,
                        ST02LockRescueMethod.BraceHullStressPoint), read, write), Is.True);
                    Assert.That(hub.IsStorySettled(ReturnRegionalStoryId.ST02RiverLanterns), Is.True);
                    Assert.That(hub.CountSettledStories(), Is.EqualTo(1));

                    // Once ST-02 is settled, hub can transition directly to ST-05.
                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST05TwoDoors), Is.True);
                    Assert.That(hub.ActiveStory, Is.EqualTo(ReturnRegionalStoryId.ST05TwoDoors));
                    Assert.That(hub.ActiveST05Host, Is.Not.Null);

                    // Settled ST-02 cannot be reopened.
                    Assert.That(hub.IsStoryAvailable(ReturnRegionalStoryId.ST02RiverLanterns), Is.False);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }


        [Test]
        public void JingwanTheaterDispatchRequiresLiveEvidenceAndPreservesBoycottNoTicketBoundary()
        {
            var root = new GameObject("RegionalHub ST01 Jingwan theater");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            var context = ObservedTestLocations();
            context.PublicBoycottActuallyDeclared = () => true;

            try
            {
                using (var hub = new ReturnRegionalStoriesHubHost(
                    root.transform, context, read, write))
                {
                    Assert.That(ReturnRegionalStoriesHubHost.IsPermanentRegionalStory(
                        ReturnRegionalStoryId.ST01JingwanTheater), Is.True);
                    Assert.That(hub.IsStoryAvailable(ReturnRegionalStoryId.ST01JingwanTheater), Is.True);
                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST01JingwanTheater), Is.True);
                    Assert.That(hub.ActiveStory, Is.EqualTo(ReturnRegionalStoryId.ST01JingwanTheater));
                    Assert.That(hub.ActiveST01Host, Is.Not.Null);

                    // The player can only declare a boycott after the real public action.
                    Assert.That(hub.ActiveST01Host.ActiveView.BoycottButton.interactable, Is.True);
                    Assert.That(hub.ActiveST01Host.ActiveView.RewriteButton.interactable, Is.False);
                    Assert.That(hub.ActiveST01Host.ActiveView.ContractButton.interactable, Is.False);
                    hub.ActiveST01Host.ActiveView.BoycottButton.onClick.Invoke();

                    Assert.That(hub.ActiveST01Host.ActiveView, Is.Null);
                    Assert.That(ST01RegionalStoryFacts.TryRead(
                        read, out ST01RegionalOutcome outcome), Is.True);
                    Assert.That(outcome.Departure, Is.Null,
                        "The Jingwan meeting is valid even when the optional White Reed meeting never happened.");
                    Assert.That(outcome.Theater,
                        Is.EqualTo(ST01TheaterChoice.PublicBoycott));
                    Assert.That(hub.IsStorySettled(ReturnRegionalStoryId.ST01JingwanTheater), Is.True);
                    Assert.That(hub.CountSettledStories(), Is.EqualTo(1));
                    Assert.That(ST01RegionalStoryFacts.TryMarkTicketSent(read, write), Is.False,
                        "A boycott cannot manufacture the M15 reconciliation ticket.");

                    // A settled theater outcome permits a clean transition to another regional story.
                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST02RiverLanterns), Is.True);
                    Assert.That(hub.ActiveStory, Is.EqualTo(ReturnRegionalStoryId.ST02RiverLanterns));
                    Assert.That(hub.ActiveST01Host, Is.Null);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SceneCallbackClosingOpenDoesNotPublishOrphanedStory()
        {
            AssertSceneCallbackCancellingOpenDoesNotPublishOrphanedStory(false);
        }

        [Test]
        public void SceneCallbackDisposingOpenDoesNotPublishOrphanedStory()
        {
            AssertSceneCallbackCancellingOpenDoesNotPublishOrphanedStory(true);
        }

        private static void AssertSceneCallbackCancellingOpenDoesNotPublishOrphanedStory(bool disposeHub)
        {
            var root = new GameObject("Regional hub cancelled during dock entry");
            var values = new Dictionary<string, int>();
            var context = ObservedTestLocations();
            ReturnRegionalStoriesHubHost hub = null;
            int writes = 0;
            try
            {
                context.AtLutingDock = () =>
                {
                    if (disposeHub) hub.Dispose();
                    else hub.CloseActiveStory();
                    return true;
                };
                hub = new ReturnRegionalStoriesHubHost(root.transform, context,
                    Reader(values), (k, v) => { writes++; values[k] = v; });

                Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST02RiverLanterns), Is.False);
                Assert.That(hub.ActiveStory, Is.EqualTo(ReturnRegionalStoryId.None));
                Assert.That(hub.HasActiveStory, Is.False);
                Assert.That(hub.ActiveST02Host, Is.Null);
                Assert.That(root.transform.childCount, Is.Zero);
                Assert.That(writes, Is.Zero);

                context.AtLutingDock = () => true;
                Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST02RiverLanterns),
                    Is.EqualTo(!disposeHub));
            }
            finally
            {
                hub?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NestedOpenFromSceneCallbackCannotReplaceFirstStory()
        {
            var root = new GameObject("Regional hub nested opening");
            var values = new Dictionary<string, int>();
            var context = ObservedTestLocations();
            ReturnRegionalStoriesHubHost hub = null;
            bool nestedAttempted = false;
            try
            {
                context.AtLutingDock = () =>
                {
                    nestedAttempted = true;
                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST03Bathhouse), Is.False);
                    return true;
                };
                hub = new ReturnRegionalStoriesHubHost(root.transform, context,
                    Reader(values), (k, v) => values[k] = v);
                Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST02RiverLanterns), Is.True);
                Assert.That(nestedAttempted, Is.True);
                Assert.That(hub.ActiveStory, Is.EqualTo(ReturnRegionalStoryId.ST02RiverLanterns));
                Assert.That(hub.ActiveST02Host.ActiveView, Is.Not.Null);
                Assert.That(hub.ActiveST03Host, Is.Null);
                hub.CloseActiveStory();
                Assert.That(root.transform.childCount, Is.Zero);
                Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST03Bathhouse), Is.True);
            }
            finally
            {
                hub?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DisposalDisposesActiveStoryHostAndRejectsFurtherOpens()
        {
            var root = new GameObject("RegionalHubDisposeRoot");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                var hub = new ReturnRegionalStoriesHubHost(
                    root.transform, ObservedTestLocations(), read, write);
                Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST03Bathhouse), Is.True);
                var st03Host = hub.ActiveST03Host;
                Assert.That(st03Host.IsDisposed, Is.False);

                hub.Dispose();
                Assert.That(hub.IsDisposed, Is.True);
                Assert.That(hub.HasActiveStory, Is.False);
                Assert.That(st03Host.IsDisposed, Is.True);
                Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST02RiverLanterns), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
