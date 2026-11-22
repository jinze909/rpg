using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-callable RC-05 ("山上的第三班") host. Enforces M08 case-5
    /// availability, live mountain-mill presence, monotonic M05 arrival identity,
    /// commit-time live mill evidence revalidation, and clean RC05ThirdSquadFacts
    /// state (blocking torn/partial saves) before opening RC05ThirdSquadView.
    /// </summary>
    public sealed class RC05ThirdSquadHost : IDisposable
    {
        private readonly Transform parent;
        private readonly Func<bool> atAbandonedMill;
        private readonly Func<bool> oldWomanStoneTriggered;
        private readonly Func<bool> boatmanCountMatched;
        private readonly Func<bool> squadRosterChecked;
        private readonly Func<bool> zhengYuanTestimonyConfirmed;
        private readonly Func<bool> skirmishTriggered;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private RC05ThirdSquadSession session;
        private RC05ThirdSquadView activeView;
        private int? openedArrivalSequence;
        private bool disposed;

        public RC05ThirdSquadSession Session => session;
        public RC05ThirdSquadView ActiveView =>
            (activeView != null && !activeView.IsDisposed) ? activeView : null;
        public bool IsDisposed => disposed;
        public bool IsResolved =>
            RC05ThirdSquadFacts.TryRead(getInt, out RC05ThirdSquadRecord? saved) &&
            saved.HasValue;

        public RC05ThirdSquadHost(
            Transform parent,
            Func<bool> atAbandonedMill,
            Func<bool> oldWomanStoneTriggered,
            Func<bool> boatmanCountMatched,
            Func<bool> squadRosterChecked,
            Func<bool> zhengYuanTestimonyConfirmed,
            Func<bool> skirmishTriggered,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.atAbandonedMill = atAbandonedMill ??
                throw new ArgumentNullException(nameof(atAbandonedMill));
            this.oldWomanStoneTriggered = oldWomanStoneTriggered ??
                throw new ArgumentNullException(nameof(oldWomanStoneTriggered));
            this.boatmanCountMatched = boatmanCountMatched ??
                throw new ArgumentNullException(nameof(boatmanCountMatched));
            this.squadRosterChecked = squadRosterChecked ??
                throw new ArgumentNullException(nameof(squadRosterChecked));
            this.zhengYuanTestimonyConfirmed = zhengYuanTestimonyConfirmed ??
                throw new ArgumentNullException(nameof(zhengYuanTestimonyConfirmed));
            this.skirmishTriggered = skirmishTriggered ??
                throw new ArgumentNullException(nameof(skirmishTriggered));
            this.getInt = getInt ?? throw new ArgumentNullException(nameof(getInt));
            this.setInt = setInt ?? throw new ArgumentNullException(nameof(setInt));
        }

        public bool TryOpen()
        {
            if (activeView != null && activeView.IsDisposed)
            {
                activeView.Dispose();
                activeView = null;
                openedArrivalSequence = null;
            }
            if (disposed || parent == null || activeView != null || !atAbandonedMill() ||
                HasAnyPersistedFacts())
                return false;

            if (!RC05ThirdSquadSession.TryCreateFromSavedFacts(
                    getInt, oldWomanStoneTriggered(), out RC05ThirdSquadSession created))
                return false;

            session = created;
            openedArrivalSequence = getInt(M05LandingFacts.ArrivalSequenceKey);
            activeView = new RC05ThirdSquadView(
                parent,
                session,
                boatmanCountMatched,
                squadRosterChecked,
                zhengYuanTestimonyConfirmed,
                skirmishTriggered,
                HandleResolutionSubmitted);
            return true;
        }

        private bool HasAnyPersistedFacts()
        {
            return getInt(RC05ThirdSquadFacts.ConfirmedKey) != 0 ||
                getInt(RC05ThirdSquadFacts.PendingSignatureKey) != 0 ||
                getInt(RC05ThirdSquadFacts.TrialDeliveryKey) != 0 ||
                getInt(RC05ThirdSquadFacts.ResolutionKey) != 0 ||
                getInt(RC05ThirdSquadFacts.SkirmishKey) != 0 ||
                getInt(RC05ThirdSquadFacts.RealZhengYuanVerifiedKey) != 0 ||
                getInt(RC05ThirdSquadFacts.OldWomanStoneMentionedKey) != 0;
        }

        private void HandleResolutionSubmitted(RC05Resolution resolution)
        {
            if (disposed || session == null || activeView == null || activeView.IsDisposed ||
                !atAbandonedMill() ||
                !openedArrivalSequence.HasValue ||
                getInt(M05LandingFacts.ArrivalSequenceKey) != openedArrivalSequence.Value ||
                !boatmanCountMatched() ||
                (session.OldWomanStoneMentioned && !oldWomanStoneTriggered()) ||
                (session.RealZhengYuanVerified &&
                    (!squadRosterChecked() || !zhengYuanTestimonyConfirmed())) ||
                session.SkirmishOccurred != skirmishTriggered())
                return;

            // A long-lived decision panel cannot commit an old delivery,
            // identity, memorial or skirmish account after live evidence changes.

            if (!session.TrySettleResolution(resolution, getInt, setInt))
                return;

            activeView.Dispose();
            activeView = null;
            openedArrivalSequence = null;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            activeView?.Dispose();
            activeView = null;
            openedArrivalSequence = null;
        }
    }
}
