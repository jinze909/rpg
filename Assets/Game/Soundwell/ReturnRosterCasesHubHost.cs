using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum ReturnRosterCaseId
    {
        None = 0,
        RC01ChenYan = 1,
        RC02ZhengYuan = 2,
        RC03BaijiMine = 3,
        RC04DingYueReef = 4,
        RC05ThirdSquad = 5
    }

    /// <summary>
    /// Live scene-readiness and context delegates for dispatching all five
    /// roster cases (RC-01..RC-05) through ReturnRosterCasesHubHost.
    /// Missing world observations never count as verified evidence.
    /// </summary>
    public sealed class ReturnRosterCasesHubContext
    {
        public Func<bool> AtWhiteReedFerry { get; set; } = () => false;

        public Func<bool> AtIntakeDesk { get; set; } = () => false;
        public Func<bool> RealZhengYuanFoundOverride { get; set; }
        public Func<bool> ShiLengWillingForAdoption { get; set; } = () => false;

        public Func<bool> OnBaijiIsland { get; set; } = () => false;
        public Func<bool> DiagramMatchedAtPumpTwo { get; set; } = () => false;
        public Func<bool> SuccessorTrainingConfirmed { get; set; } = () => false;
        public Func<bool> LanRuoWillingToStayOneYear { get; set; } = () => false;

        // The reef listening station must be physically observed before its
        // listening UI opens. CanApproachReef is a later tidal-window gate.
        public Func<bool> AtReefListeningPoint { get; set; } = () => false;
        public Func<bool> CanApproachReef { get; set; } = () => false;
        public Func<RC04Resolution, bool> VerifiedReefSceneConditions { get; set; } = _ => false;

        public Func<bool> AtAbandonedMill { get; set; } = () => false;
        public Func<bool> OldWomanStoneTriggered { get; set; } = () => false;
        public Func<bool> BoatmanCountMatched { get; set; } = () => false;
        public Func<bool> SquadRosterChecked { get; set; } = () => false;
        public Func<bool> ZhengYuanTestimonyConfirmed { get; set; } = () => false;
        public Func<bool> SkirmishTriggered { get; set; } = () => false;
    }

    /// <summary>
    /// Scene-callable hub coordinator for all five roster cases (RC-01..RC-05).
    /// Enforces M08FirstWorldNoticeFacts case availability, single-active-case
    /// mutual exclusion, automatic RC-05->RC-02 real-Zheng-Yuan propagation, and
    /// clean sub-host disposal when switching cases or exiting the hub.
    /// </summary>
    public sealed class ReturnRosterCasesHubHost : IDisposable
    {
        private readonly Transform uiParent;
        private readonly ReturnRosterCasesHubContext context;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;

        private IDisposable activeHost;
        private ReturnRosterCaseId activeCase = ReturnRosterCaseId.None;
        private bool disposed;

        public ReturnRosterCaseId ActiveCase => activeCase;
        public bool HasActiveCase => IsActiveHostOpen();
        public bool IsDisposed => disposed;

        public RC01ChenYanHost ActiveRC01Host => activeHost as RC01ChenYanHost;
        public RC02ZhengYuanHost ActiveRC02Host => activeHost as RC02ZhengYuanHost;
        public RC03BaijiMineHost ActiveRC03Host => activeHost as RC03BaijiMineHost;
        public RC04DingYueReefHost ActiveRC04Host => activeHost as RC04DingYueReefHost;
        public RC05ThirdSquadHost ActiveRC05Host => activeHost as RC05ThirdSquadHost;

        public ReturnRosterCasesHubHost(
            Transform uiParent,
            ReturnRosterCasesHubContext context,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            this.uiParent = uiParent ?? throw new ArgumentNullException(nameof(uiParent));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.getInt = getInt ?? throw new ArgumentNullException(nameof(getInt));
            this.setInt = setInt ?? throw new ArgumentNullException(nameof(setInt));
        }

        public bool IsCaseAvailable(ReturnRosterCaseId caseId)
        {
            int num = (int)caseId;
            if (num < M08FirstWorldNoticeFacts.FirstCase || num > M08FirstWorldNoticeFacts.LastCase)
                return false;
            if (HasAnyPersistedCaseFacts(caseId))
                return false;
            if (caseId == ReturnRosterCaseId.RC02ZhengYuan &&
                RC02ZhengYuanFacts.HasOrphanedDownstreamFacts(getInt))
                return false;
            if (caseId == ReturnRosterCaseId.RC05ThirdSquad &&
                RC05ThirdSquadFacts.HasOrphanedDownstreamFacts(getInt))
                return false;
            return M08FirstWorldNoticeFacts.TryCaseAvailable(num, getInt, out bool available) &&
                available &&
                !IsCaseSettled(caseId);
        }

        private bool HasAnyPersistedCaseFacts(ReturnRosterCaseId caseId)
        {
            switch (caseId)
            {
                case ReturnRosterCaseId.RC01ChenYan:
                    return getInt(RC01ChenYanFacts.ConfirmedKey) != 0 ||
                        getInt(RC01ChenYanFacts.PendingSignatureKey) != 0 ||
                        getInt(RC01ChenYanFacts.CluesMaskKey) != 0 ||
                        getInt(RC01ChenYanFacts.TalkOrderKey) != 0 ||
                        getInt(RC01ChenYanFacts.OutcomeKey) != 0 ||
                        getInt(RC01ChenYanFacts.LinSuNoteKey) != 0;
                case ReturnRosterCaseId.RC02ZhengYuan:
                    return getInt(RC02ZhengYuanFacts.ConfirmedKey) != 0 ||
                        getInt(RC02ZhengYuanFacts.PendingSignatureKey) != 0 ||
                        getInt(RC02ZhengYuanFacts.ChecksMaskKey) != 0 ||
                        getInt(RC02ZhengYuanFacts.RoofConfessionKey) != 0 ||
                        getInt(RC02ZhengYuanFacts.ResolutionKey) != 0 ||
                        getInt(RC02ZhengYuanFacts.MotherCalledKey) != 0 ||
                        getInt(RC02ZhengYuanFacts.ShiLengConsentedKey) != 0 ||
                        getInt(RC02ZhengYuanFacts.RealZhengYuanFoundKey) != 0;
                case ReturnRosterCaseId.RC03BaijiMine:
                    return getInt(RC03BaijiMineFacts.ConfirmedKey) != 0 ||
                        getInt(RC03BaijiMineFacts.PendingSignatureKey) != 0 ||
                        getInt(RC03BaijiMineFacts.VisitsMaskKey) != 0 ||
                        getInt(RC03BaijiMineFacts.ResolutionKey) != 0 ||
                        getInt(RC03BaijiMineFacts.PumpDiagramKey) != 0 ||
                        getInt(RC03BaijiMineFacts.SuccessorTrainingKey) != 0 ||
                        getInt(RC03BaijiMineFacts.LanRuoStayConsentKey) != 0 ||
                        getInt(RC03BaijiMineFacts.ChairmanLuoNoteKey) != 0;
                case ReturnRosterCaseId.RC04DingYueReef:
                    return getInt(RC04DingYueReefFacts.ConfirmedKey) != 0 ||
                        getInt(RC04DingYueReefFacts.PendingSignatureKey) != 0 ||
                        getInt(RC04DingYueReefFacts.RoundsCompletedKey) != 0 ||
                        getInt(RC04DingYueReefFacts.AyuanSongUsedKey) != 0 ||
                        getInt(RC04DingYueReefFacts.ResolutionKey) != 0 ||
                        getInt(RC04DingYueReefFacts.DecoyPreparedKey) != 0;
                case ReturnRosterCaseId.RC05ThirdSquad:
                    return getInt(RC05ThirdSquadFacts.ConfirmedKey) != 0 ||
                        getInt(RC05ThirdSquadFacts.PendingSignatureKey) != 0 ||
                        getInt(RC05ThirdSquadFacts.TrialDeliveryKey) != 0 ||
                        getInt(RC05ThirdSquadFacts.ResolutionKey) != 0 ||
                        getInt(RC05ThirdSquadFacts.SkirmishKey) != 0 ||
                        getInt(RC05ThirdSquadFacts.RealZhengYuanVerifiedKey) != 0 ||
                        getInt(RC05ThirdSquadFacts.OldWomanStoneMentionedKey) != 0;
                default:
                    return false;
            }
        }

        public bool IsCaseSettled(ReturnRosterCaseId caseId)
        {
            switch (caseId)
            {
                case ReturnRosterCaseId.RC01ChenYan:
                    return RC01ChenYanFacts.TryRead(getInt, out RC01ChenYanRecord? r1) && r1.HasValue;
                case ReturnRosterCaseId.RC02ZhengYuan:
                    return RC02ZhengYuanFacts.TryRead(getInt, out RC02ZhengYuanRecord? r2) && r2.HasValue;
                case ReturnRosterCaseId.RC03BaijiMine:
                    return RC03BaijiMineFacts.TryRead(getInt, out RC03BaijiMineRecord? r3) && r3.HasValue;
                case ReturnRosterCaseId.RC04DingYueReef:
                    return RC04DingYueReefFacts.TryRead(getInt, out RC04ReefRecord? r4) && r4.HasValue;
                case ReturnRosterCaseId.RC05ThirdSquad:
                    return RC05ThirdSquadFacts.TryRead(getInt, out RC05ThirdSquadRecord? r5) && r5.HasValue;
                default:
                    return false;
            }
        }

        public int CountSettledCases()
        {
            int count = 0;
            for (int i = 1; i <= 5; i++)
            {
                if (IsCaseSettled((ReturnRosterCaseId)i))
                    count++;
            }
            return count;
        }

        private bool IsActiveHostOpen()
        {
            if (activeHost == null)
                return false;
            if (activeHost is RC01ChenYanHost rc01)
                return !rc01.IsDisposed && rc01.ActiveView != null;
            if (activeHost is RC02ZhengYuanHost rc02)
                return !rc02.IsDisposed && rc02.ActiveView != null;
            if (activeHost is RC03BaijiMineHost rc03)
                return !rc03.IsDisposed && rc03.ActiveView != null;
            if (activeHost is RC04DingYueReefHost rc04)
                return !rc04.IsDisposed &&
                    (rc04.ActiveListeningView != null || rc04.ActiveResolutionView != null);
            if (activeHost is RC05ThirdSquadHost rc05)
                return !rc05.IsDisposed && rc05.ActiveView != null;
            return false;
        }

        // Check callbacks before closing a settled active case. A partially
        // configured scene must fail closed without orphaning the prior UI.
        private bool HasRequiredDelegates(ReturnRosterCaseId caseId)
        {
            switch (caseId)
            {
                case ReturnRosterCaseId.RC01ChenYan:
                    return context.AtWhiteReedFerry != null;
                case ReturnRosterCaseId.RC02ZhengYuan:
                    return context.AtIntakeDesk != null &&
                        context.ShiLengWillingForAdoption != null;
                case ReturnRosterCaseId.RC03BaijiMine:
                    return context.OnBaijiIsland != null &&
                        context.DiagramMatchedAtPumpTwo != null &&
                        context.SuccessorTrainingConfirmed != null &&
                        context.LanRuoWillingToStayOneYear != null;
                case ReturnRosterCaseId.RC04DingYueReef:
                    return context.AtReefListeningPoint != null &&
                        context.CanApproachReef != null &&
                        context.VerifiedReefSceneConditions != null;
                case ReturnRosterCaseId.RC05ThirdSquad:
                    return context.AtAbandonedMill != null &&
                        context.OldWomanStoneTriggered != null &&
                        context.BoatmanCountMatched != null &&
                        context.SquadRosterChecked != null &&
                        context.ZhengYuanTestimonyConfirmed != null &&
                        context.SkirmishTriggered != null;
                default:
                    return false;
            }
        }

        public bool TryOpenCase(ReturnRosterCaseId caseId)
        {
            if (disposed || uiParent == null || !IsCaseAvailable(caseId) ||
                !HasRequiredDelegates(caseId))
                return false;

            // A notice alone cannot open reef listening at an unvisited shore;
            // retain the separate later approach/tidal-window decision.
            if (caseId == ReturnRosterCaseId.RC04DingYueReef &&
                !context.AtReefListeningPoint())
                return false;

            if (activeHost != null)
            {
                if (IsActiveHostOpen() && !IsActiveCaseSettled())
                    return false;
                CloseActiveCase();
            }

            switch (caseId)
            {
                case ReturnRosterCaseId.RC01ChenYan:
                {
                    var host = new RC01ChenYanHost(
                        uiParent, context.AtWhiteReedFerry, getInt, setInt);
                    return Activate(caseId, host, host.TryOpen());
                }
                case ReturnRosterCaseId.RC02ZhengYuan:
                {
                    Func<bool> realZhengYuanFound = context.RealZhengYuanFoundOverride ?? (() =>
                        RC05ThirdSquadFacts.TryRead(getInt, out RC05ThirdSquadRecord? rc05) &&
                        rc05.HasValue &&
                        rc05.Value.RealZhengYuanVerified);
                    var host = new RC02ZhengYuanHost(
                        uiParent,
                        context.AtIntakeDesk,
                        realZhengYuanFound,
                        context.ShiLengWillingForAdoption,
                        getInt, setInt);
                    return Activate(caseId, host, host.TryOpen());
                }
                case ReturnRosterCaseId.RC03BaijiMine:
                {
                    var host = new RC03BaijiMineHost(
                        uiParent,
                        context.OnBaijiIsland,
                        context.DiagramMatchedAtPumpTwo,
                        context.SuccessorTrainingConfirmed,
                        context.LanRuoWillingToStayOneYear,
                        getInt, setInt);
                    return Activate(caseId, host, host.TryOpen());
                }
                case ReturnRosterCaseId.RC04DingYueReef:
                {
                    var host = new RC04DingYueReefHost(
                        uiParent,
                        context.CanApproachReef,
                        context.VerifiedReefSceneConditions,
                        getInt, setInt);
                    return Activate(caseId, host, host.TryOpenListening());
                }
                case ReturnRosterCaseId.RC05ThirdSquad:
                {
                    var host = new RC05ThirdSquadHost(
                        uiParent,
                        context.AtAbandonedMill,
                        context.OldWomanStoneTriggered,
                        context.BoatmanCountMatched,
                        context.SquadRosterChecked,
                        context.ZhengYuanTestimonyConfirmed,
                        context.SkirmishTriggered,
                        getInt, setInt);
                    return Activate(caseId, host, host.TryOpen());
                }
                default:
                    return false;
            }
        }

        public void CloseActiveCase()
        {
            if (activeHost == null)
                return;
            var current = activeHost;
            activeHost = null;
            activeCase = ReturnRosterCaseId.None;
            current.Dispose();
        }

        private bool IsActiveCaseSettled()
        {
            return activeCase != ReturnRosterCaseId.None && IsCaseSettled(activeCase);
        }

        private bool Activate(ReturnRosterCaseId caseId, IDisposable host, bool opened)
        {
            if (!opened)
            {
                host.Dispose();
                return false;
            }
            activeHost = host;
            activeCase = caseId;
            return true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            CloseActiveCase();
        }
    }
}
