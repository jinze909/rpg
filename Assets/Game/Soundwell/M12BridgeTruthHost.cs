using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-callable M12 ("一面旗帜不等于每个人") host. Enforces verified
    /// M11ReverseRescueFacts, live bell-tower/camp presence, and clean
    /// ConfirmedKey before opening M12BridgeTruthView, and persists the chosen
    /// truth disclosure through M12BridgeTruthSession with retry-safe recovery.
    /// </summary>
    public sealed class M12BridgeTruthHost : IDisposable
    {
        private readonly Transform parent;
        private readonly Func<bool> atBellTowerCamp;
        private readonly Func<bool> m03PriorConfession;
        private readonly Func<bool> m03SecrecyPromised;
        private readonly Func<bool> shenLiParticipating;
        private readonly Func<bool> linSuPresent;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private M12BridgeTruthSession session;
        private M12BridgeTruthView activeView;
        private bool disposed;

        public M12BridgeTruthSession Session => session;
        public M12BridgeTruthView ActiveView => activeView;
        public bool IsDisposed => disposed;
        public bool IsCompleted =>
            M12BridgeTruthFacts.TryRead(getInt, out M12BridgeTruthRecord? saved) &&
            saved.HasValue;

        public M12BridgeTruthHost(
            Transform parent,
            Func<bool> atBellTowerCamp,
            Func<bool> m03PriorConfession,
            Func<bool> m03SecrecyPromised,
            Func<bool> shenLiParticipating,
            Func<bool> linSuPresent,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.atBellTowerCamp = atBellTowerCamp ??
                throw new ArgumentNullException(nameof(atBellTowerCamp));
            this.m03PriorConfession = m03PriorConfession ??
                throw new ArgumentNullException(nameof(m03PriorConfession));
            this.m03SecrecyPromised = m03SecrecyPromised ??
                throw new ArgumentNullException(nameof(m03SecrecyPromised));
            this.shenLiParticipating = shenLiParticipating ??
                throw new ArgumentNullException(nameof(shenLiParticipating));
            this.linSuPresent = linSuPresent ??
                throw new ArgumentNullException(nameof(linSuPresent));
            this.getInt = getInt ?? throw new ArgumentNullException(nameof(getInt));
            this.setInt = setInt ?? throw new ArgumentNullException(nameof(setInt));
        }

        public bool TryOpen()
        {
            if (disposed || activeView != null || !atBellTowerCamp() ||
                getInt(M12BridgeTruthFacts.ConfirmedKey) != 0)
                return false;

            if (!M12BridgeTruthSession.TryCreateFromSavedFacts(
                    getInt,
                    m03PriorConfession(),
                    m03SecrecyPromised(),
                    shenLiParticipating(),
                    linSuPresent(),
                    out M12BridgeTruthSession created))
                return false;

            session = created;
            activeView = new M12BridgeTruthView(
                parent,
                session,
                () => M12BridgeTruthFacts.EvaluateM10HanExposedPublicly(getInt),
                HandleDisclosureSubmitted);
            return true;
        }

        private void HandleDisclosureSubmitted(M12TruthDisclosure disclosure)
        {
            if (disposed || session == null || !atBellTowerCamp())
                return;

            if (!session.TryDecideTruthAndCommit(disclosure, getInt, setInt))
                return;

            activeView?.Dispose();
            activeView = null;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            activeView?.Dispose();
            activeView = null;
        }
    }
}
