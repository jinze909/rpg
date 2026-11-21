using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum ReturnRegionalStoryId
    {
        None = 0,
        ST02RiverLanterns = 2,
        ST03Bathhouse = 3,
        ST05TwoDoors = 5,
        ST06TaoJiHomecoming = 6
    }

    /// <summary>
    /// Live scene-readiness and context delegates for dispatching merged regional
    /// stories (ST-02, ST-03, ST-05, ST-06) through ReturnRegionalStoriesHubHost.
    /// </summary>
    public sealed class ReturnRegionalStoriesHubContext
    {
        public Func<bool> AtLutingDock { get; set; } = () => true;

        public Func<bool> AtZhonglingBathhouse { get; set; } = () => true;
        public Func<bool> CareHomeInterviewReady { get; set; } = () => true;
        public Func<bool> SafeRouteReady { get; set; } = () => true;
        public Func<bool> TrialNightReady { get; set; } = () => true;
        public Func<bool> TransportBudgetReady { get; set; } = () => true;

        public Func<bool> AtJingwanCourtyard { get; set; } = () => true;
        public Func<bool> ReturnHaulageFeePaid { get; set; } = () => true;
        public Func<bool> MarketGiftsTested { get; set; } = () => true;
        public Func<bool> CourtyardResolved { get; set; } = () => true;
        public Func<bool> PartnersHeard { get; set; } = () => true;
        public Func<bool> MutualTrialConsent { get; set; } = () => true;
        public Func<bool> ChengJobTermsReady { get; set; } = () => true;

        public Func<bool> TaoJiAtWoodshop { get; set; } = () => true;
    }

    /// <summary>
    /// Scene-callable regional-stories hub coordinator for ST-02, ST-03, ST-05,
    /// and ST-06. Enforces ST-06's M07 Tao Ji rescue gate, single-active-story
    /// mutual exclusion, permanent-vs-voyage story classification, and clean
    /// sub-host disposal when switching stories or exiting the hub.
    /// </summary>
    public sealed class ReturnRegionalStoriesHubHost : IDisposable
    {
        private readonly Transform uiParent;
        private readonly ReturnRegionalStoriesHubContext context;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;

        private IDisposable activeHost;
        private ReturnRegionalStoryId activeStory = ReturnRegionalStoryId.None;
        private bool disposed;

        public ReturnRegionalStoryId ActiveStory => activeStory;
        public bool HasActiveStory => activeHost != null;
        public bool IsDisposed => disposed;

        public ST02RiverLanternsHost ActiveST02Host => activeHost as ST02RiverLanternsHost;
        public ST03BathhouseHost ActiveST03Host => activeHost as ST03BathhouseHost;
        public ST05TwoDoorsHost ActiveST05Host => activeHost as ST05TwoDoorsHost;
        public ST06TaoJiHomecomingHost ActiveST06Host => activeHost as ST06TaoJiHomecomingHost;

        public ReturnRegionalStoriesHubHost(
            Transform uiParent,
            ReturnRegionalStoriesHubContext context,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            this.uiParent = uiParent ?? throw new ArgumentNullException(nameof(uiParent));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.getInt = getInt ?? throw new ArgumentNullException(nameof(getInt));
            this.setInt = setInt ?? throw new ArgumentNullException(nameof(setInt));
        }

        public static bool IsPermanentRegionalStory(ReturnRegionalStoryId storyId)
        {
            return storyId == ReturnRegionalStoryId.ST02RiverLanterns ||
                storyId == ReturnRegionalStoryId.ST03Bathhouse ||
                storyId == ReturnRegionalStoryId.ST05TwoDoors;
        }

        public bool IsStorySettled(ReturnRegionalStoryId storyId)
        {
            switch (storyId)
            {
                case ReturnRegionalStoryId.ST02RiverLanterns:
                    return ST02RiverLanternsFacts.TryRead(getInt, out ST02RiverLanternsRecord? r2) &&
                        r2.HasValue;
                case ReturnRegionalStoryId.ST03Bathhouse:
                    return ST03BathhouseFacts.TryRead(getInt, out ST03BathhouseRecord? r3) &&
                        r3.HasValue;
                case ReturnRegionalStoryId.ST05TwoDoors:
                    return ST05TwoDoorsFacts.TryRead(getInt, out ST05TwoDoorsOutcome? r5) &&
                        r5.HasValue && r5.Value.Assignment.HasValue;
                case ReturnRegionalStoryId.ST06TaoJiHomecoming:
                    return ST06TaoJiHomecomingFacts.TryRead(getInt, out ST06HomecomingRecord? r6) &&
                        r6.HasValue;
                default:
                    return false;
            }
        }

        public bool IsStoryAvailable(ReturnRegionalStoryId storyId)
        {
            if (storyId == ReturnRegionalStoryId.None || IsStorySettled(storyId))
                return false;

            if (storyId == ReturnRegionalStoryId.ST06TaoJiHomecoming)
            {
                return M07ExtractionFacts.TryRead(getInt, out M07ExtractionRecord? m07) &&
                    m07.HasValue &&
                    m07.Value.TaoJiRescued;
            }

            return storyId == ReturnRegionalStoryId.ST02RiverLanterns ||
                storyId == ReturnRegionalStoryId.ST03Bathhouse ||
                storyId == ReturnRegionalStoryId.ST05TwoDoors;
        }

        public int CountSettledStories()
        {
            int count = 0;
            if (IsStorySettled(ReturnRegionalStoryId.ST02RiverLanterns)) count++;
            if (IsStorySettled(ReturnRegionalStoryId.ST03Bathhouse)) count++;
            if (IsStorySettled(ReturnRegionalStoryId.ST05TwoDoors)) count++;
            if (IsStorySettled(ReturnRegionalStoryId.ST06TaoJiHomecoming)) count++;
            return count;
        }

        public bool TryOpenStory(ReturnRegionalStoryId storyId)
        {
            if (disposed || !IsStoryAvailable(storyId))
                return false;

            if (activeHost != null)
            {
                if (!IsStorySettled(activeStory))
                    return false;
                CloseActiveStory();
            }

            switch (storyId)
            {
                case ReturnRegionalStoryId.ST02RiverLanterns:
                {
                    var host = new ST02RiverLanternsHost(
                        uiParent, context.AtLutingDock, getInt, setInt);
                    return Activate(storyId, host, host.TryOpen());
                }
                case ReturnRegionalStoryId.ST03Bathhouse:
                {
                    var host = new ST03BathhouseHost(
                        uiParent,
                        context.AtZhonglingBathhouse,
                        context.CareHomeInterviewReady,
                        context.SafeRouteReady,
                        context.TrialNightReady,
                        context.TransportBudgetReady,
                        getInt, setInt);
                    return Activate(storyId, host, host.TryOpen());
                }
                case ReturnRegionalStoryId.ST05TwoDoors:
                {
                    var host = new ST05TwoDoorsHost(
                        uiParent,
                        context.AtJingwanCourtyard,
                        context.ReturnHaulageFeePaid,
                        context.MarketGiftsTested,
                        context.CourtyardResolved,
                        context.PartnersHeard,
                        context.MutualTrialConsent,
                        context.ChengJobTermsReady,
                        getInt, setInt);
                    return Activate(storyId, host, host.TryOpen());
                }
                case ReturnRegionalStoryId.ST06TaoJiHomecoming:
                {
                    var host = new ST06TaoJiHomecomingHost(
                        uiParent, context.TaoJiAtWoodshop, getInt, setInt);
                    return Activate(storyId, host, host.TryOpen());
                }
                default:
                    return false;
            }
        }

        public void CloseActiveStory()
        {
            if (activeHost == null)
                return;
            var current = activeHost;
            activeHost = null;
            activeStory = ReturnRegionalStoryId.None;
            current.Dispose();
        }

        private bool Activate(ReturnRegionalStoryId storyId, IDisposable host, bool opened)
        {
            if (!opened)
            {
                host.Dispose();
                return false;
            }
            activeHost = host;
            activeStory = storyId;
            return true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            CloseActiveStory();
        }
    }
}
