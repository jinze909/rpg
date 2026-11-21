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
        public void HubEnforcesST06TaoJiGateAndSingleActiveStoryExclusion()
        {
            var root = new GameObject("RegionalHubGateRoot");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                using (var hub = new ReturnRegionalStoriesHubHost(
                    root.transform, new ReturnRegionalStoriesHubContext(), read, write))
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
        public void SettlingST02AllowsCleanTransitionToST05AndPreventsReopen()
        {
            var root = new GameObject("RegionalHubTransitionRoot");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                using (var hub = new ReturnRegionalStoriesHubHost(
                    root.transform, new ReturnRegionalStoriesHubContext(), read, write))
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
        public void DisposalDisposesActiveStoryHostAndRejectsFurtherOpens()
        {
            var root = new GameObject("RegionalHubDisposeRoot");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                var hub = new ReturnRegionalStoriesHubHost(
                    root.transform, new ReturnRegionalStoriesHubContext(), read, write);
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
