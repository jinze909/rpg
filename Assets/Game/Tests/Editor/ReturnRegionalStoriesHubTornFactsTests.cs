using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ReturnRegionalStoriesHubTornFactsTests
    {
        private static Func<string, int> Reader(Dictionary<string, int> values)
        {
            return key => values.TryGetValue(key, out int value) ? value : 0;
        }

        private static ReturnRegionalStoriesHubContext ObservedLocations()
        {
            return new ReturnRegionalStoriesHubContext
            {
                AtJingwanTheater = () => true,
                LiuLingPresent = () => true,
                OriginalScriptRead = () => true,
                RevisedScriptRead = () => true,
                RehearsalObserved = () => true,
                DisplacedResidentsHeard = () => true,
                AlternateStageAndFundingPrepared = () => true,
                PostshowResidentsForumArranged = () => true,
                PublicBoycottActuallyDeclared = () => true,
                AtLutingDock = () => true,
                AtZhonglingBathhouse = () => true,
                CareHomeInterviewReady = () => true,
                SafeRouteReady = () => true,
                TrialNightReady = () => true,
                TransportBudgetReady = () => true,
                AtJingwanCourtyard = () => true,
                TaoJiAtWoodshop = () => true
            };
        }

        private static void SeedM07TaoJiRescued(Dictionary<string, int> values)
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
                taoJiRescued: true), read, write), Is.True);
        }

        [Test]
        public void RegionalHubMarksTornOrCorruptStoriesUnavailableWithoutClosingActiveSettledStory()
        {
            var root = new GameObject("RegionalHubTornFactsRoot");
            var values = new Dictionary<string, int>();
            SeedM07TaoJiRescued(values);
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                using (var hub = new ReturnRegionalStoriesHubHost(
                    root.transform, ObservedLocations(), read, write))
                {
                    // ST-01: corrupt TheaterChoiceKey is unavailable, whereas valid pending retry remains available.
                    values[ST01RegionalStoryFacts.TheaterChoiceKey] = 99;
                    Assert.That(hub.IsStoryAvailable(ReturnRegionalStoryId.ST01JingwanTheater), Is.False);
                    values[ST01RegionalStoryFacts.TheaterChoiceKey] =
                        (int)ST01TheaterChoice.RewritePlayWithFundingRisk;
                    Assert.That(hub.IsStoryAvailable(ReturnRegionalStoryId.ST01JingwanTheater), Is.True);
                    values[ST01RegionalStoryFacts.TheaterChoiceKey] = 0;

                    // ST-02: torn PendingSignatureKey blocks availability until cleared.
                    values[ST02RiverLanternsFacts.PendingSignatureKey] = 1;
                    Assert.That(hub.IsStoryAvailable(ReturnRegionalStoryId.ST02RiverLanterns), Is.False);
                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST02RiverLanterns), Is.False);
                    values[ST02RiverLanternsFacts.PendingSignatureKey] = 0;
                    Assert.That(hub.IsStoryAvailable(ReturnRegionalStoryId.ST02RiverLanterns), Is.True);

                    // ST-03: torn PendingChoiceKey blocks availability until cleared.
                    values[ST03BathhouseFacts.PendingChoiceKey] =
                        (int)ST03BathhouseChoice.ReopenWithSubsidizedHours;
                    Assert.That(hub.IsStoryAvailable(ReturnRegionalStoryId.ST03Bathhouse), Is.False);
                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST03Bathhouse), Is.False);
                    Assert.That(ST03BathhouseFacts.TryResetForNewGame(write), Is.True);
                    Assert.That(hub.IsStoryAvailable(ReturnRegionalStoryId.ST03Bathhouse), Is.True);

                    // ST-06: torn ChairResolutionKey blocks availability without closing settled ST-01.
                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST01JingwanTheater), Is.True);
                    var st01Host = hub.ActiveST01Host;
                    st01Host.ActiveView.BoycottButton.onClick.Invoke();
                    Assert.That(hub.IsStorySettled(ReturnRegionalStoryId.ST01JingwanTheater), Is.True);

                    values[ST06TaoJiHomecomingFacts.ChairResolutionKey] =
                        (int)ST06ChairResolution.RefundAndRebuild;
                    Assert.That(hub.IsStoryAvailable(ReturnRegionalStoryId.ST06TaoJiHomecoming), Is.False);
                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST06TaoJiHomecoming), Is.False);
                    Assert.That(hub.ActiveST01Host, Is.SameAs(st01Host));
                    Assert.That(st01Host.IsDisposed, Is.False);

                    Assert.That(ST06TaoJiHomecomingFacts.TryResetForNewVoyage(write), Is.True);
                    Assert.That(hub.IsStoryAvailable(ReturnRegionalStoryId.ST06TaoJiHomecoming), Is.True);
                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST06TaoJiHomecoming), Is.True);
                    Assert.That(hub.ActiveStory, Is.EqualTo(ReturnRegionalStoryId.ST06TaoJiHomecoming));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RegionalHubHidesST05WhenFactsAreTornOrTheConfirmedHomeCannotBeReopened()
        {
            var root = new GameObject("RegionalHubST05AvailabilityRoot");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                using (var hub = new ReturnRegionalStoriesHubHost(
                    root.transform, ObservedLocations(), read, write))
                {
                    // An interrupted home write must not appear available in the regional hub.
                    values[ST05TwoDoorsFacts.HomeIntentKey] = 1210;
                    Assert.That(hub.IsStoryAvailable(ReturnRegionalStoryId.ST05TwoDoors), Is.False);
                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST05TwoDoors), Is.False);

                    // A clean save is available; once the permanent home record is confirmed,
                    // the existing ST-05 host intentionally refuses to reopen it before assignment.
                    Assert.That(ST05TwoDoorsFacts.TryResetForNewGame(write), Is.True);
                    Assert.That(hub.IsStoryAvailable(ReturnRegionalStoryId.ST05TwoDoors), Is.True);

                    var home = new ST05HouseholdRecord(
                        ST05BedResolution.ConvertBedToCourtyardBenches,
                        ST05HomeArrangement.KeepTwoDoorsAndCourtyardTable,
                        mutualTrialConsent: false);
                    Assert.That(ST05TwoDoorsFacts.TryRecordHome(home, read, write), Is.True);
                    Assert.That(hub.IsStoryAvailable(ReturnRegionalStoryId.ST05TwoDoors), Is.False);
                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST05TwoDoors), Is.False);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
