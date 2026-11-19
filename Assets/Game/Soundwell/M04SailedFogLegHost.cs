using System;
using GamingIsLove.Makinom;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Opens the already-authored FogListeningView only after an actually
    /// recorded M04 cast-off. The voyage host supplies the real fog cue
    /// sequence: this adapter never invents an enemy, reef, ship or arrival.
    /// Fog outcomes are persisted to the existing Makinom store only after
    /// the player has completed this leg, and only once.
    /// </summary>
    public sealed class M04SailedFogLegHost : IDisposable
    {
        private readonly Func<string, int> read;
        private readonly Action<string, int> write;
        private readonly FogListeningSession leg;
        private readonly M04DepartureRecord departure;
        private Action<FogListeningRecord> onCompleted;
        private readonly FogListeningView view;
        private bool delivered;
        private bool disposed;

        public FogListeningView View => view;
        public FogListeningSession Leg => leg;
        public M04DepartureRecord Departure => departure;
        public bool IsDisposed => disposed;

        public static bool TryOpen(Transform parent, FogListeningSession leg,
            Action<FogListeningRecord> onCompleted,
            out M04SailedFogLegHost host)
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
            {
                host = null;
                return false;
            }
            return TryOpen(parent, leg,
                key => variables.GetInt(key),
                (key, value) => variables.Set(key, value),
                onCompleted, out host);
        }

        public static bool TryOpen(Transform parent, FogListeningSession leg,
            Func<string, int> read, Action<string, int> write,
            Action<FogListeningRecord> onCompleted,
            out M04SailedFogLegHost host)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (leg == null) throw new ArgumentNullException(nameof(leg));
            if (onCompleted == null) throw new ArgumentNullException(nameof(onCompleted));
            host = null;
            if (read == null || write == null || leg.IsComplete)
                return false;

            // Route preference and readiness alone cannot authorize fog
            // gameplay. The ship must have really left the dock.
            if (!M04DepartureFacts.TryRead(read, out M04DepartureRecord? departure) ||
                !departure.HasValue ||
                !M04DepartureFacts.TryReadSailed(read, out bool sailed) ||
                !sailed || read(M04DepartureFacts.SailedKey) != 1)
                return false;

            // A persisted fog leg belongs to a prior run. Refuse both valid
            // and corrupt completion flags instead of silently overwriting.
            if (read(FogListeningFacts.CompletedKey) != 0 ||
                !FogListeningFacts.TryRead(read, out FogListeningRecord? previous) ||
                previous.HasValue)
                return false;

            host = new M04SailedFogLegHost(parent, leg, read, write,
                departure.Value, onCompleted);
            return true;
        }

        private M04SailedFogLegHost(Transform parent,
            FogListeningSession leg, Func<string, int> read,
            Action<string, int> write, M04DepartureRecord departure,
            Action<FogListeningRecord> onCompleted)
        {
            this.leg = leg;
            this.read = read;
            this.write = write;
            this.departure = departure;
            this.onCompleted = onCompleted;
            view = new FogListeningView(parent, leg, OnFogFinished);
        }

        public void LayoutForViewportAspect(float aspect)
        {
            if (disposed) return;
            view.LayoutForViewportAspect(aspect);
        }

        private void OnFogFinished(FogListeningSession completedLeg)
        {
            if (disposed || delivered || !ReferenceEquals(completedLeg, leg) ||
                !leg.IsComplete)
                return;

            // If a new visit began while this screen was open, its old
            // completion cannot be republished to the new voyage.
            if (!M04DepartureFacts.TryRead(read, out M04DepartureRecord? current) ||
                !current.HasValue ||
                !M04DepartureFacts.TryReadSailed(read, out bool sailed) ||
                !sailed || read(M04DepartureFacts.SailedKey) != 1 ||
                !SameDeparture(current.Value, departure))
                throw new InvalidOperationException(
                    "The fog leg no longer belongs to the current sailed voyage.");

            if (!FogListeningFacts.TryRead(read, out FogListeningRecord? saved))
                throw new InvalidOperationException("Fog outcome store is unavailable.");

            if (!saved.HasValue)
            {
                // The current leg is uncommitted, not malformed. A torn write
                // leaves Completed=0 and can be retried from this same UI.
                if (read(FogListeningFacts.CompletedKey) != 0 ||
                    !FogListeningFacts.TryRecord(leg, write) ||
                    !FogListeningFacts.TryRead(read, out saved) ||
                    !saved.HasValue)
                    throw new InvalidOperationException("Unable to persist fog leg outcome.");
            }

            // After a successful save, a host navigation error may still
            // occur. A retry must use the exact persisted result, never
            // rewrite it, advance the leg again or silently change success.
            if (!SameOutcome(saved.Value, leg))
                throw new InvalidOperationException(
                    "The saved fog leg differs from the completed live passage.");

            onCompleted?.Invoke(saved.Value);
            delivered = true;
            onCompleted = null;
            view.Dispose();
        }

        private static bool SameDeparture(
            M04DepartureRecord a, M04DepartureRecord b)
        {
            return a.Route == b.Route &&
                a.AssignedCrew == b.AssignedCrew &&
                a.AvailableBerths == b.AvailableBerths &&
                a.ReturneesToBook == b.ReturneesToBook &&
                a.OfficialRegistrationEffective == b.OfficialRegistrationEffective &&
                a.EscortFlagAccepted == b.EscortFlagAccepted;
        }

        private static bool SameOutcome(
            FogListeningRecord saved, FogListeningSession played)
        {
            return saved.Succeeded == played.Succeeded &&
                saved.HullIntegrity == played.HullIntegrity &&
                saved.AvoidedHazards == played.AvoidedHazards &&
                saved.MatchedSignals == played.MatchedSignals &&
                saved.RemainingFocus == played.RemainingFocus;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            onCompleted = null;
            view.Dispose();
        }
    }
}
