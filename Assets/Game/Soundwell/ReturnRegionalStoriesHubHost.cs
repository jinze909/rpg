using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum ReturnRegionalStoryId
    {
        None = 0,
        ST01JingwanTheater = 1,
        ST02RiverLanterns = 2,
        ST03Bathhouse = 3,
        ST04EchoValley = 4,
        ST05TwoDoors = 5,
        ST06TaoJiHomecoming = 6
    }

    /// <summary>
    /// Live scene-readiness and context delegates for dispatching merged regional
    /// stories (ST-02 through ST-06) through ReturnRegionalStoriesHubHost.
    /// No world event is verified merely because a caller omitted a delegate.
    /// </summary>
    public sealed class ReturnRegionalStoriesHubContext
    {
        public Func<bool> AtJingwanTheater { get; set; } = () => false;
        public Func<bool> LiuLingPresent { get; set; } = () => false;
        public Func<bool> OriginalScriptRead { get; set; } = () => false;
        public Func<bool> RevisedScriptRead { get; set; } = () => false;
        public Func<bool> RehearsalObserved { get; set; } = () => false;
        public Func<bool> DisplacedResidentsHeard { get; set; } = () => false;
        public Func<bool> AlternateStageAndFundingPrepared { get; set; } = () => false;
        public Func<bool> PostshowResidentsForumArranged { get; set; } = () => false;
        public Func<bool> PublicBoycottActuallyDeclared { get; set; } = () => false;

        public Func<bool> AtLutingDock { get; set; } = () => false;

        public Func<bool> AtZhonglingBathhouse { get; set; } = () => false;
        public Func<IST04EchoValleyWorldEvidence> ST04EchoValleyWorld { get; set; }
        public Func<bool> CareHomeInterviewReady { get; set; } = () => false;
        public Func<bool> SafeRouteReady { get; set; } = () => false;
        public Func<bool> TrialNightReady { get; set; } = () => false;
        public Func<bool> TransportBudgetReady { get; set; } = () => false;

        public Func<bool> AtJingwanCourtyard { get; set; } = () => false;
        public Func<bool> ReturnHaulageFeePaid { get; set; } = () => false;
        public Func<bool> MarketGiftsTested { get; set; } = () => false;
        public Func<bool> CourtyardResolved { get; set; } = () => false;
        public Func<bool> PartnersHeard { get; set; } = () => false;
        public Func<bool> MutualTrialConsent { get; set; } = () => false;
        public Func<bool> ChengJobTermsReady { get; set; } = () => false;

        public Func<bool> TaoJiAtWoodshop { get; set; } = () => false;
    }

    /// <summary>
    /// Scene-callable regional-stories hub coordinator for ST-01 through ST-06.
    /// Enforces ST-06's M07 Tao Ji rescue gate, single-active-story
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
        public bool HasActiveStory => IsActiveHostOpen();
        public bool IsDisposed => disposed;

        public ST01TheaterChoiceHost ActiveST01Host => activeHost as ST01TheaterChoiceHost;
        public ST02RiverLanternsHost ActiveST02Host => activeHost as ST02RiverLanternsHost;
        public ST03BathhouseHost ActiveST03Host => activeHost as ST03BathhouseHost;
        public ST04EchoValleyHost ActiveST04Host => activeHost as ST04EchoValleyHost;
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
            return storyId == ReturnRegionalStoryId.ST01JingwanTheater ||
                storyId == ReturnRegionalStoryId.ST02RiverLanterns ||
                storyId == ReturnRegionalStoryId.ST03Bathhouse ||
                storyId == ReturnRegionalStoryId.ST04EchoValley ||
                storyId == ReturnRegionalStoryId.ST05TwoDoors;
        }

        public bool IsStorySettled(ReturnRegionalStoryId storyId)
        {
            switch (storyId)
            {
                case ReturnRegionalStoryId.ST01JingwanTheater:
                    return ST01RegionalStoryFacts.TryRead(getInt, out ST01RegionalOutcome r1) &&
                        r1.Theater.HasValue;
                case ReturnRegionalStoryId.ST02RiverLanterns:
                    return ST02RiverLanternsFacts.TryRead(getInt, out ST02RiverLanternsRecord? r2) &&
                        r2.HasValue;
                case ReturnRegionalStoryId.ST03Bathhouse:
                    return ST03BathhouseFacts.TryRead(getInt, out ST03BathhouseRecord? r3) &&
                        r3.HasValue;
                case ReturnRegionalStoryId.ST04EchoValley:
                    return ST04EchoValleyFacts.TryRead(getInt, out ST04EchoValleyOutcome? r4) &&
                        r4.HasValue;
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
            if (storyId == ReturnRegionalStoryId.None || IsStorySettled(storyId) ||
                HasBlockingPersistedStoryFacts(storyId))
                return false;

            if (storyId == ReturnRegionalStoryId.ST06TaoJiHomecoming)
            {
                return M07ExtractionFacts.TryRead(getInt, out M07ExtractionRecord? m07) &&
                    m07.HasValue &&
                    m07.Value.TaoJiRescued;
            }

            return storyId == ReturnRegionalStoryId.ST01JingwanTheater ||
                storyId == ReturnRegionalStoryId.ST02RiverLanterns ||
                storyId == ReturnRegionalStoryId.ST03Bathhouse ||
                storyId == ReturnRegionalStoryId.ST04EchoValley ||
                storyId == ReturnRegionalStoryId.ST05TwoDoors;
        }

        private bool HasBlockingPersistedStoryFacts(ReturnRegionalStoryId storyId)
        {
            switch (storyId)
            {
                case ReturnRegionalStoryId.ST01JingwanTheater:
                {
                    if (getInt(ST01RegionalStoryFacts.TheaterConfirmedKey) != 0)
                        return true;
                    int pendingRaw = getInt(ST01RegionalStoryFacts.TheaterChoiceKey);
                    if (pendingRaw != 0 &&
                        !Enum.IsDefined(typeof(ST01TheaterChoice), pendingRaw))
                        return true;
                    int departureRaw = getInt(ST01RegionalStoryFacts.DepartureChoiceKey);
                    int departureMarker = getInt(ST01RegionalStoryFacts.DepartureConfirmedKey);
                    if (departureMarker != 0 && departureMarker != 1)
                        return true;
                    if ((departureMarker == 0 && departureRaw != 0) ||
                        (departureMarker == 1 &&
                         !Enum.IsDefined(typeof(ST01DepartureChoice), departureRaw)))
                        return true;
                    return pendingRaw == 0 && !ST01RegionalStoryFacts.TryRead(getInt, out _);
                }
                case ReturnRegionalStoryId.ST02RiverLanterns:
                    return getInt(ST02RiverLanternsFacts.ConfirmedKey) != 0 ||
                        getInt(ST02RiverLanternsFacts.PendingSignatureKey) != 0 ||
                        !ST02RiverLanternsFacts.TryRead(getInt, out _);
                case ReturnRegionalStoryId.ST03Bathhouse:
                    return getInt(ST03BathhouseFacts.ConfirmedKey) != 0 ||
                        getInt(ST03BathhouseFacts.PendingChoiceKey) != 0 ||
                        !ST03BathhouseFacts.TryRead(getInt, out _);
                case ReturnRegionalStoryId.ST04EchoValley:
                    // A pending immutable intent may be replayed; otherwise any
                    // torn partial record blocks a fresh start.
                    return getInt(ST04EchoValleyFacts.ConfirmedKey) != 0 ||
                        (getInt(ST04EchoValleyFacts.IntentKey) == 0 &&
                         !ST04EchoValleyFacts.TryRead(getInt, out _));
                case ReturnRegionalStoryId.ST05TwoDoors:
                    // The ST-05 host refuses to reopen once the household record exists.
                    // Hide both torn facts and a valid-but-incomplete home commit from the hub.
                    return !ST05TwoDoorsFacts.TryRead(getInt, out ST05TwoDoorsOutcome? st05) ||
                        st05.HasValue;
                case ReturnRegionalStoryId.ST06TaoJiHomecoming:
                    return getInt(ST06TaoJiHomecomingFacts.ConfirmedKey) != 0 ||
                        getInt(ST06TaoJiHomecomingFacts.ChairResolutionKey) != 0 ||
                        getInt(ST06TaoJiHomecomingFacts.CoverUpRemediatedKey) != 0 ||
                        getInt(ST06TaoJiHomecomingFacts.WelcomeDinnerKey) != 0 ||
                        getInt(ST06TaoJiHomecomingFacts.TraveledFarKey) != 0 ||
                        getInt(ST06TaoJiHomecomingFacts.LetterStatusKey) != 0 ||
                        getInt(ST06TaoJiHomecomingFacts.MarketTicketKey) != 0;
                default:
                    return false;
            }
        }

        public int CountSettledStories()
        {
            int count = 0;
            if (IsStorySettled(ReturnRegionalStoryId.ST01JingwanTheater)) count++;
            if (IsStorySettled(ReturnRegionalStoryId.ST02RiverLanterns)) count++;
            if (IsStorySettled(ReturnRegionalStoryId.ST03Bathhouse)) count++;
            if (IsStorySettled(ReturnRegionalStoryId.ST04EchoValley)) count++;
            if (IsStorySettled(ReturnRegionalStoryId.ST05TwoDoors)) count++;
            if (IsStorySettled(ReturnRegionalStoryId.ST06TaoJiHomecoming)) count++;
            return count;
        }

        private bool IsActiveHostOpen()
        {
            if (activeHost == null)
                return false;
            if (activeHost is ST01TheaterChoiceHost st01)
                return !st01.IsDisposed && st01.ActiveView != null;
            if (activeHost is ST02RiverLanternsHost st02)
                return !st02.IsDisposed && st02.ActiveView != null;
            if (activeHost is ST03BathhouseHost st03)
                return !st03.IsDisposed && st03.ActiveView != null;
            if (activeHost is ST05TwoDoorsHost st05)
                return !st05.IsDisposed && st05.ActiveView != null;
            if (activeHost is ST06TaoJiHomecomingHost st06)
                return !st06.IsDisposed && st06.ActiveView != null;
            return false;
        }

        // Missing scene context must never manufacture world evidence or throw
        // after a settled active story has already been closed.
        private bool HasRequiredDelegates(ReturnRegionalStoryId storyId)
        {
            switch (storyId)
            {
                case ReturnRegionalStoryId.ST01JingwanTheater:
                    return context.AtJingwanTheater != null &&
                        context.LiuLingPresent != null &&
                        context.OriginalScriptRead != null &&
                        context.RevisedScriptRead != null &&
                        context.RehearsalObserved != null &&
                        context.DisplacedResidentsHeard != null &&
                        context.AlternateStageAndFundingPrepared != null &&
                        context.PostshowResidentsForumArranged != null &&
                        context.PublicBoycottActuallyDeclared != null;
                case ReturnRegionalStoryId.ST02RiverLanterns:
                    return context.AtLutingDock != null;
                case ReturnRegionalStoryId.ST03Bathhouse:
                    return context.AtZhonglingBathhouse != null &&
                        context.CareHomeInterviewReady != null &&
                        context.SafeRouteReady != null &&
                        context.TrialNightReady != null &&
                        context.TransportBudgetReady != null;
                case ReturnRegionalStoryId.ST04EchoValley:
                    return context.ST04EchoValleyWorld != null;
                case ReturnRegionalStoryId.ST05TwoDoors:
                    return context.AtJingwanCourtyard != null &&
                        context.ReturnHaulageFeePaid != null &&
                        context.MarketGiftsTested != null &&
                        context.CourtyardResolved != null &&
                        context.PartnersHeard != null &&
                        context.MutualTrialConsent != null &&
                        context.ChengJobTermsReady != null;
                case ReturnRegionalStoryId.ST06TaoJiHomecoming:
                    return context.TaoJiAtWoodshop != null;
                default:
                    return false;
            }
        }

        public bool TryOpenStory(ReturnRegionalStoryId storyId)
        {
            if (disposed || uiParent == null || !IsStoryAvailable(storyId) ||
                !HasRequiredDelegates(storyId))
                return false;

            if (activeHost != null)
            {
                if (IsActiveHostOpen() && !IsStorySettled(activeStory))
                    return false;
                CloseActiveStory();
            }

            switch (storyId)
            {
                case ReturnRegionalStoryId.ST01JingwanTheater:
                {
                    var host = new ST01TheaterChoiceHost(
                        uiParent,
                        context.AtJingwanTheater,
                        context.LiuLingPresent,
                        context.OriginalScriptRead,
                        context.RevisedScriptRead,
                        context.RehearsalObserved,
                        context.DisplacedResidentsHeard,
                        context.AlternateStageAndFundingPrepared,
                        context.PostshowResidentsForumArranged,
                        context.PublicBoycottActuallyDeclared,
                        getInt, setInt);
                    return Activate(storyId, host, host.TryOpen());
                }
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
                case ReturnRegionalStoryId.ST04EchoValley:
                {
                    IST04EchoValleyWorldEvidence world = context.ST04EchoValleyWorld();
                    if (world == null)
                        return false;
                    var host = new ST04EchoValleyHost(uiParent, world, getInt, setInt);
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
