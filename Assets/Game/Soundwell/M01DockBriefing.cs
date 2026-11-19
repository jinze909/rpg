using System;

namespace Rpg.Soundwell
{
    public enum M01DockStage
    {
        NotStarted,
        AwaitingOpeningReply,
        OfferingCake,
        ReviewingRoutes,
        Complete
    }

    public enum M01DockReply
    {
        AskAboutOldDebt,
        TeaseTheUglyCup,
        AskForShipToRedSalt
    }

    public enum M01VoyageLead
    {
        HanQiaoOfficialDock,
        LuJunOldGuard,
        QiQueMerchantRoute
    }

    /// <summary>
    /// A lead, not a booked ship or a promised M02 outcome. M01 only learns
    /// what each contact would ask; the player has not secured passage yet.
    /// </summary>
    public sealed class M01VoyageLeadTerms
    {
        internal M01VoyageLeadTerms(M01VoyageLead lead, bool needsLivingRegistration,
            bool requiresPublicBrigadeFlag, bool hasSmallCapacity)
        {
            Lead = lead;
            NeedsLivingRegistration = needsLivingRegistration;
            RequiresPublicBrigadeFlag = requiresPublicBrigadeFlag;
            HasSmallCapacity = hasSmallCapacity;
        }

        public M01VoyageLead Lead { get; }
        public bool NeedsLivingRegistration { get; }
        public bool RequiresPublicBrigadeFlag { get; }
        public bool HasSmallCapacity { get; }

        public string Describe(bool chinese)
        {
            switch (Lead)
            {
                case M01VoyageLead.HanQiaoOfficialDock:
                    return chinese
                        ? "韩峤的船坞：大船，有官凭；必须先以活人身份正式登记。M01 的公开意向还不是官署批准。"
                        : "Han Qiao's official dock: a larger ship with papers, but it requires a living registration. Deciding to go public in M01 is not official approval.";
                case M01VoyageLead.LuJunOldGuard:
                    return chinese
                        ? "陆钧的旧部：有护卫和矿营线人；条件是船上公开挂第三旅旧军旗，不能当作隐蔽航路。"
                        : "Lu Jun's old guard: escorts and mine contacts, but the ship must openly fly the old Third Brigade flag. This is not a covert route.";
                case M01VoyageLead.QiQueMerchantRoute:
                    return chinese
                        ? "祁雀的商路：更隐蔽，却只有一条小船，一次只能载十几个人。船工薪水和修船钱不能凭空免掉。"
                        : "Qi Que's merchant route: more discreet, but her small ship carries only a dozen or so people at a time. Crew wages and repairs still cost money.";
                default:
                    throw new ArgumentOutOfRangeException(nameof(Lead), Lead, null);
            }
        }
    }

    /// <summary>
    /// The authored M01 dock conversation, independent of West Lane and M00.
    /// Previewing a lead never books a ship, changes a pension fact, or writes
    /// ORK/Makinom saves. Later port gameplay owns those consequences.
    /// </summary>
    public sealed class M01DockBriefing
    {
        public M01DockStage Stage { get; private set; }
        public M01DockReply? OpeningReply { get; private set; }
        public M01VoyageLead? InspectedLead { get; private set; }
        public bool Finished => Stage == M01DockStage.Complete;

        public void Begin()
        {
            Require(M01DockStage.NotStarted);
            Stage = M01DockStage.AwaitingOpeningReply;
        }

        public void Reply(M01DockReply reply)
        {
            Require(M01DockStage.AwaitingOpeningReply);
            if (!Enum.IsDefined(typeof(M01DockReply), reply))
                throw new ArgumentOutOfRangeException(nameof(reply), reply, null);
            OpeningReply = reply;
            Stage = M01DockStage.OfferingCake;
        }

        public void ContinueAfterCake()
        {
            Require(M01DockStage.OfferingCake);
            Stage = M01DockStage.ReviewingRoutes;
        }

        public M01VoyageLeadTerms Inspect(M01VoyageLead lead)
        {
            Require(M01DockStage.ReviewingRoutes);
            M01VoyageLeadTerms terms = TermsFor(lead);
            InspectedLead = lead;
            return terms;
        }

        public void EndConversation()
        {
            Require(M01DockStage.ReviewingRoutes);
            Stage = M01DockStage.Complete;
        }

        // Host transitions out of M01's warehouse briefing only after
        // EndConversation succeeds. When that callback fails, restore
        // ReviewingRoutes while keeping the player's inspected lead.
        public void EndConversation(Action recordWithHost)
        {
            if (recordWithHost == null)
                throw new ArgumentNullException(nameof(recordWithHost));
            M01DockStage before = Stage;
            EndConversation();
            try
            {
                recordWithHost();
            }
            catch
            {
                Stage = before;
                throw;
            }
        }

        public static M01VoyageLeadTerms TermsFor(M01VoyageLead lead)
        {
            switch (lead)
            {
                case M01VoyageLead.HanQiaoOfficialDock:
                    return new M01VoyageLeadTerms(lead, true, false, false);
                case M01VoyageLead.LuJunOldGuard:
                    return new M01VoyageLeadTerms(lead, false, true, false);
                case M01VoyageLead.QiQueMerchantRoute:
                    return new M01VoyageLeadTerms(lead, false, false, true);
                default:
                    throw new ArgumentOutOfRangeException(nameof(lead), lead, null);
            }
        }

        private void Require(M01DockStage expected)
        {
            if (Stage != expected)
                throw new InvalidOperationException(
                    "The M01 dock briefing is not at " + expected + ".");
        }
    }

    public sealed class M01DockBeat
    {
        internal M01DockBeat(M01DockStage stage, string text, string[] choices,
            bool canContinue, bool finished)
        {
            Stage = stage;
            Text = text;
            Choices = choices;
            CanContinue = canContinue;
            Finished = finished;
        }

        public M01DockStage Stage { get; }
        public string Text { get; }
        public string[] Choices { get; }
        public bool CanContinue { get; }
        public bool Finished { get; }
    }

    public static class M01DockBriefingDialogue
    {
        public static M01DockBeat Describe(M01DockBriefing briefing, bool chinese)
        {
            if (briefing == null)
                throw new ArgumentNullException(nameof(briefing));

            switch (briefing.Stage)
            {
                case M01DockStage.AwaitingOpeningReply:
                    return new M01DockBeat(briefing.Stage,
                        chinese
                            ? "码头南侧货栈。祁雀端着一只奇丑的粗陶杯：你欠我的钱可没跟着阵亡。"
                            : "At the warehouse south of the docks, Qi Que raises an ugly clay cup: 'Your debt didn't die with you.'",
                        chinese
                            ? new[] { "十二年了，你还留着那笔烂账？", "这杯子比以前的还难看。", "沈砾还在赤盐。我需要一条船。" }
                            : new[] { "You still remember that old debt?", "That cup is uglier than the last one.", "Shen Li is at Red Salt. I need a ship." },
                        false, false);

                case M01DockStage.OfferingCake:
                    return new M01DockBeat(briefing.Stage,
                        chinese
                            ? "祁雀听完沈砾的事，从点心匣里递来一块桂花糕：先尝块糕。——出海的事，我算过了。修船的账还欠着，船工也等着领薪水。我还在攒钱开一家自己的远航商号。"
                            : "Qi Que hears you out about Shen Li, offers you an osmanthus cake, and says, 'I've done the sums.' She still owes the shipyard for repairs, her crew needs paying, and she's saving to open her own trading house.",
                        Array.Empty<string>(), true, false);

                case M01DockStage.ReviewingRoutes:
                    string headline = chinese
                        ? "祁雀摊开海峡图：有三条路。你可以逐条问清条件，但现在没有一条船已经替你订好。"
                        : "Qi Que unfolds the strait chart: 'Three possible ways out.' You can examine each cost, but no passage has been secured.";
                    string detail = briefing.InspectedLead.HasValue
                        ? "\n" + M01DockBriefing.TermsFor(briefing.InspectedLead.Value).Describe(chinese)
                        : string.Empty;
                    return new M01DockBeat(briefing.Stage, headline + detail,
                        chinese
                            ? new[] { "韩峤的官船", "陆钧旧部的船", "祁雀的隐蔽商船" }
                            : new[] { "Han Qiao's official ship", "Lu Jun's old guard", "Qi Que's merchant vessel" },
                        true, false);

                case M01DockStage.Complete:
                    return new M01DockBeat(briefing.Stage, string.Empty,
                        Array.Empty<string>(), false, true);

                default:
                    throw new InvalidOperationException(
                        "Begin the M01 dock briefing before rendering it.");
            }
        }
    }
}
