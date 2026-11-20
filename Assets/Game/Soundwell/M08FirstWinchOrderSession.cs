using System;

namespace Rpg.Soundwell
{
    public enum M08WinchPart
    {
        ScrapGear = 1,
        DockCable = 2
    }

    public enum M08WinchOrderStage
    {
        SourceParts,
        Assemble,
        TestLift,
        RepairLooseCable,
        Completed
    }

    /// <summary>
    /// M08's first civilian commission: acquire real parts from the scrap
    /// market and dock, build the small winch, and lift a test load.
    /// A loose cable causes a repairable failure, never a dead-end.
    /// The parent scene remains the only authority for rewards, identity,
    /// rescue records and the chapter's first world notice.
    /// </summary>
    public sealed class M08FirstWinchOrderSession
    {
        public const int MinimumSafeTension = 3;
        public M08WinchOrderStage Stage { get; private set; } =
            M08WinchOrderStage.SourceParts;
        public bool HasScrapGear { get; private set; }
        public bool HasDockCable { get; private set; }
        public int CableTension { get; private set; }
        public int TestAttempts { get; private set; }
        public int RecoverableFaults { get; private set; }
        public bool WorkshopDepositEarned => Stage == M08WinchOrderStage.Completed;
        public bool IsCompleted => WorkshopDepositEarned;

        public bool TryAcquirePart(M08WinchPart part)
        {
            if (Stage != M08WinchOrderStage.SourceParts)
                return false;
            switch (part)
            {
                case M08WinchPart.ScrapGear:
                    if (HasScrapGear) return false;
                    HasScrapGear = true;
                    break;
                case M08WinchPart.DockCable:
                    if (HasDockCable) return false;
                    HasDockCable = true;
                    break;
                default:
                    return false;
            }
            if (HasScrapGear && HasDockCable)
                Stage = M08WinchOrderStage.Assemble;
            return true;
        }

        public bool TryAssemble()
        {
            if (Stage != M08WinchOrderStage.Assemble ||
                !HasScrapGear || !HasDockCable)
                return false;
            CableTension = 1;
            Stage = M08WinchOrderStage.TestLift;
            return true;
        }

        public bool TryTightenCable()
        {
            if (Stage != M08WinchOrderStage.TestLift ||
                CableTension >= MinimumSafeTension)
                return false;
            CableTension = MinimumSafeTension;
            return true;
        }

        public bool TryTestLift()
        {
            if (Stage != M08WinchOrderStage.TestLift)
                return false;
            TestAttempts++;
            if (CableTension < MinimumSafeTension)
            {
                RecoverableFaults++;
                Stage = M08WinchOrderStage.RepairLooseCable;
            }
            else
            {
                Stage = M08WinchOrderStage.Completed;
            }
            return true;
        }

        public bool TryRepairLooseCable()
        {
            if (Stage != M08WinchOrderStage.RepairLooseCable)
                return false;
            CableTension = MinimumSafeTension;
            Stage = M08WinchOrderStage.TestLift;
            return true;
        }
    }
}
