using System;

namespace Rpg.Soundwell
{
    public enum M06ReunionStage
    {
        VesselBriefing = 0,
        PumpRoomWitnesses = 1,
        LetterHandoff = 2,
        OulinRetaliationTruth = 3,
        RescuePlanDecision = 4,
        Completed = 5
    }

    public enum M06ReunionGreeting
    {
        ComeToBringYouBack = 0,
        UglyWrenchNames = 1,
        DiscussWithdrawalOrChannel = 2
    }

    /// <summary>
    /// Authoritative M06 ("她没有在原地等你") pump-room reunion and rescue-plan
    /// state machine. Grounds Shen Li's vessel/draft briefing in verified M04
    /// and M05 landing facts (N77), gates family-letter delivery strictly on an
    /// actually carried M05 packet (N76), and records the mutually exclusive
    /// rescue plan without inventing capacity or deliveries.
    /// </summary>
    public sealed class M06PumpRoomReunionSession
    {
        public M04DepartureRecord Departure { get; }
        public M05LandingReport Landing { get; }
        public M05LetterDecision? LetterDecision { get; }
        public M06ReunionStage Stage { get; private set; }
        public M06ReunionGreeting? SelectedGreeting { get; private set; }
        public bool CanAskTonightFirstBatch => Landing.HasUsableLandingCapacity;
        public bool HasCarriedLetters => LetterDecision == M05LetterDecision.TakePacket;
        public int LettersCarriedCount => M05LettersFacts.LettersCarried(LetterDecision);
        public bool PumpRoomWitnessed { get; private set; }
        public bool LettersHandled { get; private set; }
        public bool PendingReturnLetterNoted { get; private set; }
        public bool RetaliationHistoryHeard { get; private set; }
        public M06RescuePlan? ConfirmedPlan { get; private set; }

        public M06PumpRoomReunionSession(
            M04DepartureRecord departure,
            M05LandingReport landing,
            M05LetterDecision? letterDecision)
        {
            if (landing.CrewArrived <= 0 || landing.CrewArrived > departure.AssignedCrew)
                throw new ArgumentOutOfRangeException(nameof(landing),
                    "M06 requires a verified M05 landing crew count within M04 departure crew.");
            if (landing.ReadyRescueBerths < 0 || landing.ReadyRescueBerths > departure.AvailableBerths)
                throw new ArgumentOutOfRangeException(nameof(landing),
                    "M06 ready berths cannot exceed M04 departure berths.");
            if (landing.ReadyRescueBerths > 0 && (!landing.TenderArrived || !landing.ReceiverMet))
                throw new ArgumentException(
                    "Usable rescue berths require both an arrived tender and a met receiver.",
                    nameof(landing));

            Departure = departure;
            Landing = landing;
            LetterDecision = letterDecision;
            Stage = M06ReunionStage.VesselBriefing;
        }

        public static bool TryCreateFromSavedFacts(
            Func<string, int> getInt,
            out M06PumpRoomReunionSession session)
        {
            session = null;
            if (getInt == null)
                return false;
            if (!M04DepartureFacts.TryRead(getInt, out M04DepartureRecord? departure) ||
                !departure.HasValue ||
                !M05LandingFacts.TryRead(getInt, out M05LandingReport? landing) ||
                !landing.HasValue)
                return false;

            M05LettersFacts.TryRead(getInt, out M05LetterDecision? letters);
            session = new M06PumpRoomReunionSession(departure.Value, landing.Value, letters);
            return true;
        }

        public string FormatVesselBriefing(bool zh)
        {
            string routeDraft = FormatRouteDraft(Departure.Route, zh);
            if (!Landing.HasUsableLandingCapacity)
            {
                return zh
                    ? $"{routeDraft}实到船员 {Landing.CrewArrived} 人；领航船到了，可用的接应舱位还没落实（当前可用舱位 0）。"
                    : $"{routeDraft} {Landing.CrewArrived} crew arrived; the lead boat is in, but usable rescue berths are not yet secured (0 ready berths).";
            }

            return zh
                ? $"{routeDraft}实到船员 {Landing.CrewArrived} 人，接驳艇与接应已会合，已核实可用舱位 {Landing.ReadyRescueBerths} 个。"
                : $"{routeDraft} {Landing.CrewArrived} crew arrived; tender and receiver met with {Landing.ReadyRescueBerths} verified rescue berths.";
        }

        public string FormatWithdrawalOrChannelChoice(bool zh)
        {
            if (CanAskTonightFirstBatch)
            {
                return zh
                    ? "船在外港，舱位也核过。今晚能不能先送一批？"
                    : "The vessel is in the outer harbor with verified berths. Can we move a first batch tonight?";
            }

            return zh
                ? "领航船到了。接人还缺一截，我们先把能走的水道核一下。"
                : "The lead boat arrived, but pickup capacity is still short. Let us verify the navigable channels first.";
        }

        public void AnswerVesselBriefing(M06ReunionGreeting greeting)
        {
            EnsureStage(M06ReunionStage.VesselBriefing);
            if (!Enum.IsDefined(typeof(M06ReunionGreeting), greeting))
                throw new ArgumentOutOfRangeException(nameof(greeting));

            SelectedGreeting = greeting;
            Stage = M06ReunionStage.PumpRoomWitnesses;
        }

        public void WitnessPumpRoomAndInspection()
        {
            EnsureStage(M06ReunionStage.PumpRoomWitnesses);
            PumpRoomWitnessed = true;
            // Validation N76: skip letter handoff completely if M05 did not carry the packet.
            Stage = HasCarriedLetters
                ? M06ReunionStage.LetterHandoff
                : M06ReunionStage.OulinRetaliationTruth;
        }

        public void DeliverCarriedLetters()
        {
            EnsureStage(M06ReunionStage.LetterHandoff);
            LettersHandled = true;
            // One miner returns his unopened letter to think over his reply ("待投").
            PendingReturnLetterNoted = true;
            Stage = M06ReunionStage.OulinRetaliationTruth;
        }

        public void HearOulinRetaliationTruth()
        {
            EnsureStage(M06ReunionStage.OulinRetaliationTruth);
            RetaliationHistoryHeard = true;
            Stage = M06ReunionStage.RescuePlanDecision;
        }

        public bool TryConfirmRescuePlan(
            M06RescuePlan plan,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            EnsureStage(M06ReunionStage.RescuePlanDecision);
            if (!Enum.IsDefined(typeof(M06RescuePlan), plan))
                throw new ArgumentOutOfRangeException(nameof(plan));

            var outcome = LettersHandled
                ? M06LetterDeliveryOutcome.DeliveredWithPending
                : M06LetterDeliveryOutcome.NotCarried;
            var record = new M06ReunionRecord(
                plan,
                Landing.HasUsableLandingCapacity,
                outcome,
                RetaliationHistoryHeard);

            if (!M06RescuePlanFacts.TryRecord(record, getInt, setInt))
                return false;

            ConfirmedPlan = plan;
            Stage = M06ReunionStage.Completed;
            return true;
        }

        public static string FormatPlanLabel(M06RescuePlan plan, bool zh)
        {
            switch (plan)
            {
                case M06RescuePlan.OfficialHandover:
                    return zh
                        ? "监察移交（借伊蕾合法登岛窗口分批领人）"
                        : "Official Handover (batch release under Yi Lei's inspection window)";
                case M06RescuePlan.CargoCover:
                    return zh
                        ? "货运掩护（借祁雀货船与盐箱分批倒运）"
                        : "Cargo Cover (move returnees in batches under Qi Que's salt crates)";
                case M06RescuePlan.MinerShutdown:
                    return zh
                        ? "矿工停机配合（先撤下层五百人并留手摇备用泵，主泵停机三小时）"
                        : "Miner Shutdown (evacuate 500 to upper level with hand-pump backup, 3-hour shutdown)";
                case M06RescuePlan.ForceBreakout:
                    return zh
                        ? "强行突围（先护非战斗矿工入掩体，再夺栈桥与巡逻艇）"
                        : "Force Breakout (shelter non-combatants first, then seize pier and patrol craft)";
                default:
                    throw new ArgumentOutOfRangeException(nameof(plan));
            }
        }

        private static string FormatRouteDraft(M03VoyageChoice route, bool zh)
        {
            switch (route)
            {
                case M03VoyageChoice.HanQiaoOfficial:
                    return zh ? "官船配置、吃水六尺，" : "Official vessel (6-chi draft),";
                case M03VoyageChoice.LuJunEscort:
                    return zh ? "两条护航船在礁外，浅水艇进港、吃水四尺半，" : "Two escort ships off the reef with a shallow tender (4.5-chi draft),";
                case M03VoyageChoice.QiQueMerchant:
                    return zh ? "民用先遣商船进港，" : "Civilian merchant lead boat in harbor,";
                case M03VoyageChoice.MixedWithOfficial:
                    return zh ? "混合航路（官船底船配接驳），" : "Mixed route (official hull with tender support),";
                case M03VoyageChoice.MixedWithCivilian:
                    return zh ? "混合航路（民船底船配护卫接驳），" : "Mixed route (civilian hull with escort/tender support),";
                default:
                    return zh ? "已核实抵港船只，" : "Verified arriving vessel,";
            }
        }

        private void EnsureStage(M06ReunionStage expected)
        {
            if (Stage != expected)
                throw new InvalidOperationException(
                    $"M06 reunion expected stage {expected}, but is currently in {Stage}.");
        }
    }
}
