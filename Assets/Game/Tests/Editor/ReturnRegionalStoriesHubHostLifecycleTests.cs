using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rpg.Soundwell;
using UnityEngine;

namespace Rpg.Tests.Editor
{
    public sealed class ReturnRegionalStoriesHubHostLifecycleTests
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
                AlternateStageAndFundingPrepared = () => false,
                PostshowResidentsForumArranged = () => false,
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

        [Test]
        public void ExternalChildViewDisposalClearsHasActiveStoryAndAllowsReopenOrSwitch()
        {
            var root = new GameObject("RegionalHubExternalDisposeRoot");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                using (var hub = new ReturnRegionalStoriesHubHost(
                    root.transform, ObservedLocations(), read, write))
                {
                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST02RiverLanterns), Is.True);
                    var firstHost = hub.ActiveST02Host;
                    Assert.That(hub.HasActiveStory, Is.True);
                    Assert.That(firstHost.ActiveView, Is.Not.Null);

                    firstHost.ActiveView.Dispose();
                    Assert.That(hub.HasActiveStory, Is.False);

                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST03Bathhouse), Is.True);
                    Assert.That(hub.HasActiveStory, Is.True);
                    Assert.That(hub.ActiveStory, Is.EqualTo(ReturnRegionalStoryId.ST03Bathhouse));
                    Assert.That(hub.ActiveST03Host.ActiveView, Is.Not.Null);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyedChildCanvasClearsHasActiveStoryAndAllowsCleanReopen()
        {
            var root = new GameObject("RegionalHubCanvasDestroyRoot");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            try
            {
                using (var hub = new ReturnRegionalStoriesHubHost(
                    root.transform, ObservedLocations(), read, write))
                {
                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST01JingwanTheater), Is.True);
                    var view = hub.ActiveST01Host.ActiveView;
                    Assert.That(view, Is.Not.Null);

                    UnityEngine.Object.DestroyImmediate(view.Panel.parent.gameObject);
                    Assert.That(hub.HasActiveStory, Is.False);

                    Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST01JingwanTheater), Is.True);
                    Assert.That(hub.HasActiveStory, Is.True);
                    Assert.That(hub.ActiveST01Host.ActiveView, Is.Not.Null);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyedUiParentFailsClosedOnTryOpenStoryWithoutThrowing()
        {
            var root = new GameObject("RegionalHubParentDestroyRoot");
            var values = new Dictionary<string, int>();
            Func<string, int> read = Reader(values);
            Action<string, int> write = (k, v) => values[k] = v;

            ReturnRegionalStoriesHubHost hub = null;
            try
            {
                hub = new ReturnRegionalStoriesHubHost(
                    root.transform, ObservedLocations(), read, write);
                UnityEngine.Object.DestroyImmediate(root);
                root = null;

                Assert.That(hub.TryOpenStory(ReturnRegionalStoryId.ST02RiverLanterns), Is.False);
                Assert.That(hub.HasActiveStory, Is.False);
            }
            finally
            {
                hub?.Dispose();
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
