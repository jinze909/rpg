using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    public enum ReturnMidgameStage
    {
        None = 0,
        M08WorkshopOrder = 81,
        M08OptionalDockTruth = 82,
        M08FirstWorldNotice = 83,
        M09BerthTable = 90,
        ReadyForActThree = 100
    }

    /// <summary>
    /// Live scene-readiness and context delegates for sequencing M08 through M09
    /// via ReturnMidgameFlowHost. An unwired scene must not manufacture daylight,
    /// berth availability, a rescued Ruiya, or Shen Li's attendance.
    /// </summary>
    public sealed class ReturnMidgameFlowContext
    {
        public Func<bool> ShenLiPresentAtDock { get; set; } = () => false;
        public Func<bool> RealDaybreakObserved { get; set; } = () => false;

        public Func<bool> AtSecondBatchTable { get; set; } = () => false;
        public Func<bool> ShenLiPresentAtTable { get; set; } = () => false;
        public Func<bool> QiDebtAlreadyKnown { get; set; } = () => false;
        public Func<int> BaseBerths { get; set; } = () => 0;
        public Func<int> PendingNames { get; set; } = () => 0;
        public Func<M09RuiyaOutcome?> RuiyaOutcome { get; set; } =
            () => null;
        /// <summary>
        /// Scene-owned proof that the shipyard quote was actually paid. The
        /// coordinator never infers payment from the M08 workshop deposit.
        /// </summary>
        public Func<bool> ShipwrightFeePaidInWorld { get; set; } = () => false;
    }

    /// <summary>
    /// Scene-callable Act-2 / Midgame flow coordinator sequencing M08 Workshop Order,
    /// optional M08 Night-Dock Truth (never a compulsory gate), M08 First World Notice,
    /// and M09 Second-Batch Berth Table, bridging verified M07 extraction directly into
    /// Act-3 (ReturnFinaleFlowHost).
    /// </summary>
    public sealed class ReturnMidgameFlowHost : IDisposable
    {
        private readonly Transform uiParent;
        private readonly ReturnMidgameFlowContext context;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;

        private IDisposable activeHost;
        private ReturnMidgameStage currentStage = ReturnMidgameStage.None;
        private bool disposed;

        public ReturnMidgameStage CurrentStage => currentStage;
        public bool HasActiveHost => activeHost != null;
        public bool IsDisposed => disposed;

        public M08WorkshopOrderHost ActiveWorkshopHost => activeHost as M08WorkshopOrderHost;
        public M08DockTruthHost ActiveDockTruthHost => activeHost as M08DockTruthHost;
        public M08FirstWorldNoticeHost ActiveFirstNoticeHost => activeHost as M08FirstWorldNoticeHost;
        public M09BerthTableHost ActiveBerthTableHost => activeHost as M09BerthTableHost;

        public ReturnMidgameFlowHost(
            Transform uiParent,
            ReturnMidgameFlowContext context,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            this.uiParent = uiParent ?? throw new ArgumentNullException(nameof(uiParent));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.getInt = getInt ?? throw new ArgumentNullException(nameof(getInt));
            this.setInt = setInt ?? throw new ArgumentNullException(nameof(setInt));
        }

        public ReturnMidgameStage EvaluateNextRequiredStage()
        {
            if (!M07ExtractionFacts.TryRead(getInt, out M07ExtractionRecord? m07) || !m07.HasValue)
                return ReturnMidgameStage.None;
            if (!M08WorkshopDepositFacts.TryRead(getInt, out bool depositPaid) || !depositPaid)
                return getInt(M08WorkshopDepositFacts.CompletedKey) != 0 ||
                    M08WorkshopDepositFacts.HasOrphanedDownstreamFacts(getInt)
                    ? ReturnMidgameStage.None
                    : ReturnMidgameStage.M08WorkshopOrder;
            if (!M08FirstWorldNoticeFacts.TryRead(getInt, out bool noticeRead) || !noticeRead)
                return getInt(M08FirstWorldNoticeFacts.ConfirmedKey) != 0 ||
                    M08FirstWorldNoticeFacts.HasOrphanedRosterCaseFacts(getInt)
                    ? ReturnMidgameStage.None
                    : ReturnMidgameStage.M08FirstWorldNotice;
            if (!M09BerthTableFacts.TryRead(getInt, out M09BerthTableRecord? m09) || !m09.HasValue)
                return M09BerthTableFacts.HasAnyPersistedFacts(getInt) ||
                    M09BerthTableFacts.HasOrphanedDownstreamFacts(getInt)
                    ? ReturnMidgameStage.None
                    : ReturnMidgameStage.M09BerthTable;
            return ReturnMidgameStage.ReadyForActThree;
        }

        public bool CanOpenOptionalDockTruth()
        {
            if (disposed || context.ShenLiPresentAtDock == null ||
                !context.ShenLiPresentAtDock())
                return false;
            if (!M08WorkshopDepositFacts.TryRead(getInt, out bool depositPaid) || !depositPaid)
                return false;
            return getInt(M08DockTruthFacts.ConfirmedKey) == 0 &&
                getInt(M08DockTruthFacts.ShenRelationResponseKey) == 0;
        }

        public bool TryOpenOptionalDockTruth()
        {
            if (disposed || !CanOpenOptionalDockTruth())
                return false;
            if (activeHost != null && currentStage == ReturnMidgameStage.M08OptionalDockTruth)
                return false;

            CloseActiveHost();
            var host = new M08DockTruthHost(
                uiParent, context.ShenLiPresentAtDock, getInt, setInt);
            return Activate(ReturnMidgameStage.M08OptionalDockTruth, host, host.TryOpen());
        }

        // Validate callbacks before closing a prior host. The sub-hosts
        // enforce observed values, but null scene wiring must not throw.
        private bool HasSceneInputs(ReturnMidgameStage stage)
        {
            switch (stage)
            {
                case ReturnMidgameStage.M08FirstWorldNotice:
                    return context.RealDaybreakObserved != null;
                case ReturnMidgameStage.M09BerthTable:
                    return context.AtSecondBatchTable != null &&
                        context.ShenLiPresentAtTable != null &&
                        context.QiDebtAlreadyKnown != null &&
                        context.BaseBerths != null &&
                        context.PendingNames != null &&
                        context.RuiyaOutcome != null;
                default:
                    return true;
            }
        }

        public bool TryOpenNextRequiredStage()
        {
            if (disposed)
                return false;

            ReturnMidgameStage next = EvaluateNextRequiredStage();
            if (next == ReturnMidgameStage.None || next == ReturnMidgameStage.ReadyForActThree)
            {
                CloseActiveHost();
                currentStage = next;
                return false;
            }

            if (activeHost != null && currentStage == next)
                return false;
            if (!HasSceneInputs(next))
                return false;

            CloseActiveHost();

            switch (next)
            {
                case ReturnMidgameStage.M08WorkshopOrder:
                {
                    var host = new M08WorkshopOrderHost(uiParent, getInt, setInt);
                    return Activate(next, host, host.TryOpen());
                }
                case ReturnMidgameStage.M08FirstWorldNotice:
                {
                    var host = new M08FirstWorldNoticeHost(
                        uiParent, context.RealDaybreakObserved, getInt, setInt);
                    return Activate(next, host, host.TryOpen());
                }
                case ReturnMidgameStage.M09BerthTable:
                {
                    var host = new M09BerthTableHost(
                        uiParent,
                        context.AtSecondBatchTable,
                        context.ShenLiPresentAtTable,
                        context.QiDebtAlreadyKnown,
                        context.BaseBerths,
                        context.PendingNames,
                        context.RuiyaOutcome,
                        getInt, setInt,
                        context.ShipwrightFeePaidInWorld);
                    return Activate(next, host, host.TryOpen());
                }
                default:
                    return false;
            }
        }

        public void CloseActiveHost()
        {
            if (activeHost == null)
                return;
            var current = activeHost;
            activeHost = null;
            current.Dispose();
        }

        private bool Activate(ReturnMidgameStage stage, IDisposable host, bool opened)
        {
            if (!opened)
            {
                host.Dispose();
                return false;
            }
            activeHost = host;
            currentStage = stage;
            return true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            CloseActiveHost();
        }
    }
}
