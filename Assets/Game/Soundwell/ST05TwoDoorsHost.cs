using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Read-only observations supplied by the owning ST-05 scene. This object
    /// is not a save store: each refresh must report what actually happened in
    /// the world, and Makinom remains the only persistent authority.
    /// </summary>
    public sealed class ST05TwoDoorsWorldEvidence
    {
        public bool AtTwoDoorsHome { get; set; }
        public ST05BedResolution? BedResolution { get; set; }
        public bool ReturnHaulageFeePaid { get; set; }
        public bool DrawingLampTested { get; set; }
        public bool CoolingStoneCracksChecked { get; set; }
        public bool StallholderAdviceHeard { get; set; }
        public bool WindblownMapsRecovered { get; set; }
        public bool MapRackMovedAwayFromOven { get; set; }
        public bool GuestsRedirected { get; set; }
        public bool YuheSpokeToAunt { get; set; }
        public bool ChengSpokeToFriends { get; set; }
        public bool ShopAccessDiscussed { get; set; }
        public bool KeysAndVisitorsDiscussed { get; set; }
        public bool TravelCostsDiscussed { get; set; }
        public ST05HomeArrangement? AgreedArrangement { get; set; }
        public bool YuheConsentedToSharedTrial { get; set; }
        public bool ChengConsentedToSharedTrial { get; set; }
        public ST05ChengAssignment? ChengAssignment { get; set; }
        public bool ChengPersonallyAcceptedOrDeclined { get; set; }
        public bool ShopCoverArranged { get; set; }
        public bool KeysChecked { get; set; }
        public bool ReturnDateTalkedThrough { get; set; }
    }

    /// <summary>
    /// Scene-callable ST-05 host. It advances only from live scene observations,
    /// commits the couple's own home agreement and Cheng's own job decision,
    /// and can resume between those two commits after a scene/save reload.
    /// </summary>
    public sealed class ST05TwoDoorsHost : IDisposable
    {
        private readonly Transform parent;
        private readonly Func<ST05TwoDoorsWorldEvidence> readWorld;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private Action<ST05TwoDoorsOutcome> onCompleted;
        private ST05TwoDoorsSession session;
        private ST05TwoDoorsView view;
        private bool disposed;
        private bool completionNotified;

        public ST05TwoDoorsSession Session => session;
        public ST05TwoDoorsView ActiveView => view;
        public bool IsDisposed => disposed;
        public bool HasRecordedOutcome =>
            ST05TwoDoorsFacts.TryRead(getInt, out ST05TwoDoorsOutcome? outcome) &&
            outcome.HasValue && outcome.Value.Assignment.HasValue;

        public ST05TwoDoorsHost(
            Transform parent,
            Func<ST05TwoDoorsWorldEvidence> readWorld,
            Func<string, int> getInt,
            Action<string, int> setInt,
            Action<ST05TwoDoorsOutcome> onCompleted = null)
        {
            this.parent = parent != null ? parent :
                throw new ArgumentNullException(nameof(parent));
            this.readWorld = readWorld ?? throw new ArgumentNullException(nameof(readWorld));
            this.getInt = getInt ?? throw new ArgumentNullException(nameof(getInt));
            this.setInt = setInt ?? throw new ArgumentNullException(nameof(setInt));
            this.onCompleted = onCompleted;
        }

        public bool TryOpen()
        {
            if (disposed || view != null)
                return false;
            ST05TwoDoorsWorldEvidence evidence = readWorld();
            if (evidence == null || !evidence.AtTwoDoorsHome ||
                !ST05TwoDoorsSession.TryCreate(getInt, out session))
                return false;

            view = new ST05TwoDoorsView(parent, TryAdvance);
            Refresh(evidence);
            return true;
        }

        /// <summary>
        /// Call after a world interaction changes one of the supplied
        /// observations. The first four stages are synchronized from actual
        /// scene facts; irreversible partner decisions still require Submit.
        /// </summary>
        public void Refresh()
        {
            if (disposed || view == null)
                return;
            Refresh(readWorld());
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (!disposed)
                view?.LayoutForViewportAspect(aspect);
        }

        public bool TryAdvance()
        {
            if (disposed || view == null || session == null)
                return false;

            ST05TwoDoorsWorldEvidence evidence = readWorld();
            if (evidence == null || !evidence.AtTwoDoorsHome)
            {
                Refresh(evidence);
                return false;
            }

            SyncObservedProgress(evidence);
            bool committed = false;
            switch (session.Phase)
            {
                case ST05TwoDoorsPhase.AgreeHomeAndKeys:
                    if (!CanAdvance(session.Phase, evidence))
                        break;
                    ST05HomeArrangement arrangement = evidence.AgreedArrangement.Value;
                    bool sharedTrial = arrangement ==
                        ST05HomeArrangement.MutuallyAgreedTrialSharedHomeAndStudio;
                    bool bothConsented = sharedTrial &&
                        evidence.YuheConsentedToSharedTrial &&
                        evidence.ChengConsentedToSharedTrial;
                    if (session.TryAgreeLivingArrangement(arrangement, bothConsented))
                        committed = session.TryCommitLivingAgreement(getInt, setInt);
                    break;

                case ST05TwoDoorsPhase.AwaitChengOwnJobDecision:
                    if (!CanAdvance(session.Phase, evidence))
                        break;
                    if (session.TryHearChengAssignment(
                        evidence.ChengAssignment.Value,
                        evidence.ChengPersonallyAcceptedOrDeclined,
                        evidence.ShopCoverArranged,
                        evidence.KeysChecked,
                        evidence.ReturnDateTalkedThrough))
                        committed = session.TryCommitChengAssignment(getInt, setInt);
                    break;
            }

            Refresh(readWorld());
            return committed;
        }

        private void Refresh(ST05TwoDoorsWorldEvidence evidence)
        {
            if (disposed || view == null || session == null)
                return;

            if (evidence != null)
                SyncObservedProgress(evidence);
            bool ready = CanAdvance(session.Phase, evidence);
            view.Refresh(session.Phase, ready,
                DescribeStatus(session.Phase, evidence), session.IsCompleted);
            NotifyCompleted();
        }

        private void SyncObservedProgress(ST05TwoDoorsWorldEvidence evidence)
        {
            if (evidence == null || !evidence.AtTwoDoorsHome)
                return;

            // These are observations of already-performed world actions, not
            // UI-created outcomes. A reloaded scene can reconstruct them.
            for (int i = 0; i < 4; i++)
            {
                switch (session.Phase)
                {
                    case ST05TwoDoorsPhase.UnblockStaircase:
                        if (!evidence.BedResolution.HasValue ||
                            (evidence.BedResolution.Value ==
                                ST05BedResolution.ReturnBedAndPayHaulage &&
                             !evidence.ReturnHaulageFeePaid) ||
                            !session.TryClearStaircase(
                                evidence.BedResolution.Value,
                                evidence.ReturnHaulageFeePaid))
                            return;
                        break;

                    case ST05TwoDoorsPhase.TestMarketGifts:
                        if (!session.TryChoosePracticalGifts(
                            evidence.DrawingLampTested,
                            evidence.CoolingStoneCracksChecked,
                            evidence.StallholderAdviceHeard))
                            return;
                        break;

                    case ST05TwoDoorsPhase.ClearCourtyardConflict:
                        if (!session.TryResolveSharedCourtyard(
                            evidence.WindblownMapsRecovered,
                            evidence.MapRackMovedAwayFromOven,
                            evidence.GuestsRedirected))
                            return;
                        break;

                    case ST05TwoDoorsPhase.HearBothPartners:
                        if (!session.TryHearOwnWords(
                            evidence.YuheSpokeToAunt,
                            evidence.ChengSpokeToFriends,
                            evidence.ShopAccessDiscussed,
                            evidence.KeysAndVisitorsDiscussed,
                            evidence.TravelCostsDiscussed))
                            return;
                        break;

                    default:
                        return;
                }
            }
        }

        private static bool CanAdvance(
            ST05TwoDoorsPhase phase, ST05TwoDoorsWorldEvidence evidence)
        {
            if (evidence == null || !evidence.AtTwoDoorsHome)
                return false;

            switch (phase)
            {
                case ST05TwoDoorsPhase.AgreeHomeAndKeys:
                    if (!evidence.AgreedArrangement.HasValue)
                        return false;
                    return evidence.AgreedArrangement.Value !=
                            ST05HomeArrangement.MutuallyAgreedTrialSharedHomeAndStudio ||
                        (evidence.YuheConsentedToSharedTrial &&
                         evidence.ChengConsentedToSharedTrial);

                case ST05TwoDoorsPhase.AwaitChengOwnJobDecision:
                    return evidence.ChengAssignment.HasValue &&
                        evidence.ChengPersonallyAcceptedOrDeclined &&
                        evidence.ShopCoverArranged &&
                        evidence.KeysChecked &&
                        evidence.ReturnDateTalkedThrough;

                default:
                    return false;
            }
        }

        private static string DescribeStatus(
            ST05TwoDoorsPhase phase, ST05TwoDoorsWorldEvidence evidence)
        {
            if (evidence == null)
                return "无法读取当前场景证据。 / Current scene evidence is unavailable.";
            if (!evidence.AtTwoDoorsHome)
                return "请回到镜湾居民街的两扇门小院。 / Return to the two-door courtyard in Mirror Bay.";

            switch (phase)
            {
                case ST05TwoDoorsPhase.UnblockStaircase:
                    if (!evidence.BedResolution.HasValue)
                        return "等待现场真正处理楼梯上的床；界面不会替你搬床。 / Clear the staircase bed in the world; the panel never moves it for you.";
                    if (evidence.BedResolution.Value == ST05BedResolution.ReturnBedAndPayHaulage &&
                        !evidence.ReturnHaulageFeePaid)
                        return "退床路线还未核实搬运费已实际支付。 / The return option still needs proof that the haulage fee was paid.";
                    return "床的处理已观察到，正在同步进度。 / The bed solution was observed; progress is synchronizing.";

                case ST05TwoDoorsPhase.TestMarketGifts:
                    return evidence.DrawingLampTested &&
                        evidence.CoolingStoneCracksChecked &&
                        evidence.StallholderAdviceHeard
                        ? "两份礼物都已实际试看，并听过摊主建议。 / Both gifts were checked and the stallholder's advice was heard."
                        : "还需实际试亮画图灯、检查石板裂纹，并听取摊主建议。 / Test the drawing lamp, inspect the cooling stone, and hear the stallholder's advice.";

                case ST05TwoDoorsPhase.ClearCourtyardConflict:
                    return evidence.WindblownMapsRecovered &&
                        evidence.MapRackMovedAwayFromOven &&
                        evidence.GuestsRedirected
                        ? "地图、晾架和客人通道都已处理。 / The maps, rack and guest route are resolved."
                        : "还需追回散页、把晾架移离烤炉，并为客人让出通道。 / Recover the loose maps, move the rack from the oven, and redirect guests.";

                case ST05TwoDoorsPhase.HearBothPartners:
                    return evidence.YuheSpokeToAunt &&
                        evidence.ChengSpokeToFriends &&
                        evidence.ShopAccessDiscussed &&
                        evidence.KeysAndVisitorsDiscussed &&
                        evidence.TravelCostsDiscussed
                        ? "双方都已亲自说明，并谈过店铺、钥匙、访客和远行成本。 / Both partners spoke for themselves and discussed shop access, keys, visitors and travel costs."
                        : "还需听郁禾向姨妈说明、程午向朋友说明，并谈妥店铺出入、钥匙访客和远行成本。 / Hear Yu He speak to her aunt and Cheng Wu to his friends; discuss shop access, keys, visitors and travel costs.";

                case ST05TwoDoorsPhase.AgreeHomeAndKeys:
                    if (!evidence.AgreedArrangement.HasValue)
                        return "等待郁禾与程午亲自谈妥居住安排；玩家不能替他们投票。 / Wait for Yu He and Cheng Wu to decide their own living arrangement.";
                    if (evidence.AgreedArrangement.Value ==
                            ST05HomeArrangement.MutuallyAgreedTrialSharedHomeAndStudio &&
                        !(evidence.YuheConsentedToSharedTrial &&
                          evidence.ChengConsentedToSharedTrial))
                        return "试住合用必须有郁禾和程午双方明确同意。 / A shared-home trial requires explicit consent from both partners.";
                    return "双方的居住安排已由现场确认，可以记录。 / Their mutually agreed home arrangement is verified and ready to record.";

                case ST05TwoDoorsPhase.AwaitChengOwnJobDecision:
                    if (!evidence.ChengAssignment.HasValue)
                        return "等待程午本人决定留在当地或接受外地测绘。 / Wait for Cheng Wu to decide whether to stay or accept the outside survey.";
                    if (!evidence.ChengPersonallyAcceptedOrDeclined ||
                        !evidence.ShopCoverArranged || !evidence.KeysChecked ||
                        !evidence.ReturnDateTalkedThrough)
                        return "还需由程午本人表达决定，并谈妥店铺照看、钥匙和归期。 / Cheng must personally state his decision after shop cover, keys and a return date are discussed.";
                    return "程午本人的工作决定和实际安排已核实，可以记录。 / Cheng's own work decision and practical arrangements are verified and ready to record.";

                case ST05TwoDoorsPhase.Completed:
                    return "居住安排与程午自己的工作决定已保存；远距工作、信件和 M15 到场仍须真实发生。 / The home agreement and Cheng's own job decision are saved; work, mail and M15 arrivals remain separate real events.";

                default:
                    return string.Empty;
            }
        }

        private void NotifyCompleted()
        {
            if (completionNotified || session == null || !session.IsCompleted ||
                !ST05TwoDoorsFacts.TryRead(getInt, out ST05TwoDoorsOutcome? outcome) ||
                !outcome.HasValue || !outcome.Value.Assignment.HasValue)
                return;

            completionNotified = true;
            Action<ST05TwoDoorsOutcome> callback = onCompleted;
            onCompleted = null;
            callback?.Invoke(outcome.Value);
        }

        private ST05TwoDoorsWorldEvidence ReadWorld()
        {
            return readWorld();
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            onCompleted = null;
            view?.Dispose();
            view = null;
            session = null;
        }
    }
}