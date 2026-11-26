using System;

namespace Rpg.Soundwell
{
    public enum M06FamilyLetterRecipient
    {
        TaoJi = 0,
        LedgerMiner = 1,
        PendingMiner = 2
    }

    public enum M06FamilyLetterStatus
    {
        Unhandled = 0,
        DeliveredAndReadTwice = 1,
        DeliveredUnread = 2,
        ReturnedPending = 3
    }

    /// <summary>
    /// The three individually described M06 letter outcomes. The other five
    /// carried letters remain in the packet until their recipients are known.
    /// </summary>
    public readonly struct M06FamilyLetterDeliverySnapshot
    {
        public int PackedStatuses { get; }

        public M06FamilyLetterDeliverySnapshot(int packedStatuses)
        {
            PackedStatuses = packedStatuses;
        }

        public M06FamilyLetterStatus StatusFor(M06FamilyLetterRecipient recipient)
        {
            if (!Enum.IsDefined(typeof(M06FamilyLetterRecipient), recipient))
                throw new ArgumentOutOfRangeException(nameof(recipient));
            return (M06FamilyLetterStatus)((PackedStatuses >> ((int)recipient * 2)) & 3);
        }

        public bool IsHandled(M06FamilyLetterRecipient recipient)
        {
            return StatusFor(recipient) != M06FamilyLetterStatus.Unhandled;
        }

        public int HandledCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < 3; i++)
                {
                    if (((PackedStatuses >> (i * 2)) & 3) != 0)
                        count++;
                }
                return count;
            }
        }

        public bool AllKnownRecipientsHandled => HandledCount == 3;

        public bool HasPendingReturn =>
            StatusFor(M06FamilyLetterRecipient.PendingMiner) ==
            M06FamilyLetterStatus.ReturnedPending;
    }

    /// <summary>
    /// Persists each named/known M06 letter handoff in the single Makinom
    /// variable store. Two bits per recipient preserve the authored outcomes:
    /// Tao Ji reads twice, one miner keeps the ledger unopened, and one letter
    /// is returned pending a reply. Every record is bound to the actual M05
    /// arrival sequence; stale identical voyages cannot inherit deliveries.
    /// </summary>
    public static class M06FamilyLetterDeliveryFacts
    {
        public const string StatusMaskKey =
            "rpg.return.m06.family-letter-delivery-status-mask";
        public const string ArrivalIdentityKey =
            "rpg.return.m06.family-letter-delivery-arrival-identity";
        public const int AllStatusesMask = 63;

        public static bool TryRead(
            Func<string, int> getInt,
            out M06FamilyLetterDeliverySnapshot snapshot)
        {
            snapshot = default;
            if (getInt == null)
                return false;

            int savedMask = getInt(StatusMaskKey);
            int savedIdentity = getInt(ArrivalIdentityKey);
            if (!IsValidStatusMask(savedMask) || savedIdentity < 0)
                return false;

            if (!M05LandingFacts.TryRead(getInt, out M05LandingReport? landing) ||
                !landing.HasValue ||
                !M05LettersFacts.TryRead(getInt, out M05LetterDecision? decision) ||
                (!decision.HasValue && getInt(M05LettersFacts.DecisionKey) != 0))
                return false;

            int sequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            if (sequence < 0 || sequence == int.MaxValue)
                return false;

            int currentIdentity = sequence + 1;
            if (savedIdentity != currentIdentity)
                return true;

            if (decision != M05LetterDecision.TakePacket)
                return savedMask == 0;

            snapshot = new M06FamilyLetterDeliverySnapshot(savedMask);
            return true;
        }

        public static bool TryRecord(
            M06FamilyLetterRecipient recipient,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (!Enum.IsDefined(typeof(M06FamilyLetterRecipient), recipient) ||
                getInt == null || setInt == null ||
                M06RescuePlanFacts.HasOrphanedDownstreamFacts(getInt))
                return false;

            if (!M05LandingFacts.TryRead(getInt, out M05LandingReport? landing) ||
                !landing.HasValue ||
                !M05LettersFacts.TryRead(getInt, out M05LetterDecision? decision) ||
                decision != M05LetterDecision.TakePacket)
                return false;

            int sequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            int savedMask = getInt(StatusMaskKey);
            int savedIdentity = getInt(ArrivalIdentityKey);
            if (sequence < 0 || sequence == int.MaxValue ||
                !IsValidStatusMask(savedMask) || savedIdentity < 0)
                return false;

            int currentIdentity = sequence + 1;
            if (savedIdentity != currentIdentity)
            {
                // Clear the previous visit before binding this arrival. If a
                // save is interrupted, the old identity still reads as stale.
                setInt(StatusMaskKey, 0);
                setInt(ArrivalIdentityKey, currentIdentity);
                savedMask = 0;
            }

            M06FamilyLetterStatus expected = ExpectedStatus(recipient);
            int shift = (int)recipient * 2;
            int current = (savedMask >> shift) & 3;
            if (current != (int)M06FamilyLetterStatus.Unhandled)
                return current == (int)expected;

            int nextMask = savedMask | ((int)expected << shift);
            setInt(StatusMaskKey, nextMask);
            return true;
        }

        public static bool TryResetForNewVoyage(Action<string, int> setInt)
        {
            if (setInt == null)
                return false;
            setInt(StatusMaskKey, 0);
            setInt(ArrivalIdentityKey, 0);
            return true;
        }

        private static bool IsValidStatusMask(int mask)
        {
            if (mask < 0 || (mask & ~AllStatusesMask) != 0)
                return false;

            for (int i = 0; i < 3; i++)
            {
                var recipient = (M06FamilyLetterRecipient)i;
                int raw = (mask >> (i * 2)) & 3;
                if (raw != (int)M06FamilyLetterStatus.Unhandled &&
                    raw != (int)ExpectedStatus(recipient))
                    return false;
            }
            return true;
        }

        private static M06FamilyLetterStatus ExpectedStatus(
            M06FamilyLetterRecipient recipient)
        {
            switch (recipient)
            {
                case M06FamilyLetterRecipient.TaoJi:
                    return M06FamilyLetterStatus.DeliveredAndReadTwice;
                case M06FamilyLetterRecipient.LedgerMiner:
                    return M06FamilyLetterStatus.DeliveredUnread;
                case M06FamilyLetterRecipient.PendingMiner:
                    return M06FamilyLetterStatus.ReturnedPending;
                default:
                    throw new ArgumentOutOfRangeException(nameof(recipient));
            }
        }
    }
}
