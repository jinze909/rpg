using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ReturnRegionalStoriesHubST04LifecycleTests
    {
        private sealed class LiveEchoValleyWorld : IST04EchoValleyWorldEvidence
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

        private static Func<string, int> Reader(Dictionary<string, int> values) =>
            key => values.TryGetValue(key, out int value) ? value : 0;

        [Test]
        public void OpenST04ReportsHasActiveStoryAndBlocksSwitchingWhileUnsettled()
        {
            var root = new GameObject("RegionalHubST04ActiveLockRoot");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            var world = new LiveEchoValleyWorld();
            var context = new ReturnRegionalStoriesHubContext
            {
                ST04EchoValleyWorld = () => world,
                AtLutingDock = () => true
            };

            try
            {
                using (var hub = new ReturnRegionalStoriesHubHost(
                    root.transform, context, read, write))
                {
                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST04EchoValley), Is.True);
                    Assert.That(hub.HasActiveStory, Is.True);
                    Assert.That(hub.ActiveStory, Is.EqualTo(ReturnRegionalStoryId.ST04EchoValley));
                    Assert.That(hub.ActiveST04Host, Is.Not.Null);
                    Assert.That(hub.ActiveST04Host.ActiveView, Is.Not.Null);

                    // Even when ST-02's location delegate is true, an unsettled active
                    // ST-04 story must keep the hub's single-active-story lock.
                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST02RiverLanterns), Is.False);
                    Assert.That(hub.ActiveStory, Is.EqualTo(ReturnRegionalStoryId.ST04EchoValley));
                    Assert.That(hub.ActiveST04Host, Is.Not.Null);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ExternalST04ViewDisposalOrCanvasDestroyClearsHasActiveStoryAndAllowsReopen()
        {
            var root = new GameObject("RegionalHubST04DisposeRoot");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;
            var world = new LiveEchoValleyWorld();
            var context = new ReturnRegionalStoriesHubContext
            {
                ST04EchoValleyWorld = () => world,
                AtLutingDock = () => true
            };

            try
            {
                using (var hub = new ReturnRegionalStoriesHubHost(
                    root.transform, context, read, write))
                {
                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST04EchoValley), Is.True);
                    ST04EchoValleyView firstView = hub.ActiveST04Host.ActiveView;
                    Assert.That(hub.HasActiveStory, Is.True);

                    firstView.Dispose();
                    Assert.That(hub.HasActiveStory, Is.False);

                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST04EchoValley), Is.True);
                    ST04EchoValleyView secondView = hub.ActiveST04Host.ActiveView;
                    Assert.That(hub.HasActiveStory, Is.True);
                    Assert.That(secondView, Is.Not.SameAs(firstView));

                    UnityEngine.Object.DestroyImmediate(secondView.Panel.parent.gameObject);
                    Assert.That(hub.HasActiveStory, Is.False);

                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST02RiverLanterns), Is.True);
                    Assert.That(hub.ActiveStory, Is.EqualTo(ReturnRegionalStoryId.ST02RiverLanterns));
                    Assert.That(hub.HasActiveStory, Is.True);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
