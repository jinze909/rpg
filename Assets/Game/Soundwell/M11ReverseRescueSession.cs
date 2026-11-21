using System;

namespace Rpg.Soundwell
{
    public enum M11Phase
    {
        ThreeBellsAndEntry = 0,
        ConscriptionAndTrap = 1,
        FogExtraction = 2,
        PostRescuePact = 3,
        Completed = 4
    }

    /// <summary>
    /// Authoritative M11 ("这次轮到她来找你") state machine.
    /// Enforces three-bell entry, conscription observation (and RC-05 Hao Lie
    /// stance when enlisted under Lu Jun), Shen Li's 3-call fog extraction vs
    /// fallback paid-artisan rescue when Shen Li is absent, and retry-safe
    /// idempotent Makinom persistence.
    /// </summary>
    public sealed class M11ReverseRescueSession
    {
        public M11Phase Phase { get; private set; } = M11Phase.ThreeBellsAndEntry;
        public bool ShenLiAccompanying { get; }
        public bool OulinParticipating { get; }
        public bool RC05EnlistedInPass { get; }
        public bool ThreeBellsObserved { get; private set; }
        public M11PassEntryMethod? EntryMethod { get; private set; }
        public bool ConscriptionObserved { get; private set; }
        public M11ThirdSquadConscriptionStance ThirdSquadStance { get; private set; } =
            M11ThirdSquadConscriptionStance.NotInPass;
        public M11RescueProvider? RescueProvider { get; private set; }
        public int EchoCallsHeard { get; private set; }
        public M11ShenDivisionPact DivisionPact { get; private set; } =
            M11ShenDivisionPact.NoneShenAbsent;
        public M11ReverseRescueRecord? CompletedRecord { get; private set; }
        public bool IsCompleted => Phase == M11Phase.Completed;

        private M11ReverseRescueSession(
            bool shenLiAccompanying,
            bool oulinParticipating,
            bool rc05EnlistedInPass)
        {
            ShenLiAccompanying = shenLiAccompanying;
            OulinParticipating = shenLiAccompanying && oulinParticipating;
            RC05EnlistedInPass = rc05EnlistedInPass;
        }

        public static bool TryCreateFromSavedFacts(
            Func<string, int> getInt,
            bool shenLiAccompanying,
            bool oulinParticipating,
            out M11ReverseRescueSession session)
        {
            session = null;
            if (getInt == null || M11ReverseRescueFacts.HasOrphanedDownstreamFacts(getInt))
                return false;
            if (!M10AssemblyFacts.TryRead(getInt, out M10AssemblyRecord? m10) ||
                !m10.HasValue)
                return false;

            bool rc05InPass = M11ReverseRescueFacts.EvaluateRC05EnlistedUnderLuJun(getInt);
            session = new M11ReverseRescueSession(
                shenLiAccompanying,
                oulinParticipating,
                rc05InPass);
            return true;
        }

        public bool TryObserveThreeBellsAndEnter(M11PassEntryMethod entryMethod)
        {
            if (Phase != M11Phase.ThreeBellsAndEntry ||
                !Enum.IsDefined(typeof(M11PassEntryMethod), entryMethod))
                return false;

            ThreeBellsObserved = true;
            EntryMethod = entryMethod;
            Phase = M11Phase.ConscriptionAndTrap;
            return true;
        }

        public bool TryObserveConscriptionAndCoverReturnees(
            M11ThirdSquadConscriptionStance thirdSquadStance =
                M11ThirdSquadConscriptionStance.NotInPass)
        {
            if (Phase != M11Phase.ConscriptionAndTrap ||
                !Enum.IsDefined(typeof(M11ThirdSquadConscriptionStance), thirdSquadStance))
                return false;

            if (RC05EnlistedInPass)
            {
                if (thirdSquadStance == M11ThirdSquadConscriptionStance.NotInPass)
                    return false;
            }
            else if (thirdSquadStance != M11ThirdSquadConscriptionStance.NotInPass)
            {
                return false;
            }

            ConscriptionObserved = true;
            ThirdSquadStance = thirdSquadStance;
            Phase = M11Phase.FogExtraction;
            return true;
        }

        /// <summary>
        /// Advances Shen Li's three calls in the fog ("(Name)!" x3) when she is
        /// accompanying. Blocked if Shen Li is absent.
        /// </summary>
        public bool TryHearShenLiFogCall()
        {
            if (Phase != M11Phase.FogExtraction || !ShenLiAccompanying || EchoCallsHeard >= 3)
                return false;

            EchoCallsHeard++;
            return true;
        }

        /// <summary>
        /// Answers "在！" after all three fog calls from Shen Li to complete
        /// shallow-boat extraction and proceed to the post-rescue division pact.
        /// </summary>
        public bool TryAnswerPresentForShenLiExtraction()
        {
            if (Phase != M11Phase.FogExtraction || !ShenLiAccompanying || EchoCallsHeard != 3)
                return false;

            RescueProvider = M11RescueProvider.ShenLiShallowBoat;
            Phase = M11Phase.PostRescuePact;
            return true;
        }

        /// <summary>
        /// Uses the pre-arranged fallback paid-artisan extraction when Shen Li
        /// is not accompanying. Never triggers her echo or reconciliation pact.
        /// </summary>
        public bool TryExtractViaFallbackArtisans()
        {
            if (Phase != M11Phase.FogExtraction || ShenLiAccompanying)
                return false;

            RescueProvider = M11RescueProvider.FallbackPaidArtisans;
            EchoCallsHeard = 0;
            DivisionPact = M11ShenDivisionPact.NoneShenAbsent;
            Phase = M11Phase.PostRescuePact;
            return true;
        }

        public bool TryChooseDivisionPact(M11ShenDivisionPact pact)
        {
            if (Phase != M11Phase.PostRescuePact ||
                RescueProvider != M11RescueProvider.ShenLiShallowBoat ||
                !Enum.IsDefined(typeof(M11ShenDivisionPact), pact) ||
                pact == M11ShenDivisionPact.NoneShenAbsent)
                return false;

            DivisionPact = pact;
            return true;
        }

        public bool TryCommit(Func<string, int> getInt, Action<string, int> setInt)
        {
            if (Phase != M11Phase.PostRescuePact ||
                !ThreeBellsObserved ||
                !EntryMethod.HasValue ||
                !ConscriptionObserved ||
                !RescueProvider.HasValue ||
                getInt == null || setInt == null)
                return false;

            if (RescueProvider.Value == M11RescueProvider.ShenLiShallowBoat &&
                DivisionPact == M11ShenDivisionPact.NoneShenAbsent)
                return false;

            var record = new M11ReverseRescueRecord(
                threeBellsObserved: true,
                EntryMethod.Value,
                conscriptionObserved: true,
                ThirdSquadStance,
                RescueProvider.Value,
                EchoCallsHeard,
                OulinParticipating,
                DivisionPact);

            if (!M11ReverseRescueFacts.TryRead(getInt, out M11ReverseRescueRecord? existing))
                return false;

            // Recover idempotently if a prior write committed ConfirmedKey = 1
            // before its acknowledgement was lost.
            if (existing.HasValue)
            {
                if (!Matches(record, existing.Value))
                    return false;
            }
            else if (getInt(M11ReverseRescueFacts.ConfirmedKey) != 0 ||
                     !M11ReverseRescueFacts.TryRecord(record, getInt, setInt))
            {
                return false;
            }

            if (!M11ReverseRescueFacts.TryRead(getInt, out M11ReverseRescueRecord? saved) ||
                !saved.HasValue || !Matches(record, saved.Value))
                return false;

            CompletedRecord = saved.Value;
            Phase = M11Phase.Completed;
            return true;
        }

        private static bool Matches(M11ReverseRescueRecord expected, M11ReverseRescueRecord saved)
        {
            return expected.ThreeBellsObserved == saved.ThreeBellsObserved &&
                expected.EntryMethod == saved.EntryMethod &&
                expected.ConscriptionObserved == saved.ConscriptionObserved &&
                expected.ThirdSquadStance == saved.ThirdSquadStance &&
                expected.RescueProvider == saved.RescueProvider &&
                expected.EchoCallsHeard == saved.EchoCallsHeard &&
                expected.OulinJoinedExtraction == saved.OulinJoinedExtraction &&
                expected.DivisionPact == saved.DivisionPact;
        }
    }
}
