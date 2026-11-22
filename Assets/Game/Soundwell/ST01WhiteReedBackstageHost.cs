using System;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene integration for ST-01 White Reed. Does not attach a UI or
    /// simulate stage events. The level must supply live cast/location
    /// presence and call session methods only on real player interactions.
    /// Revalidates physical presence at save time, not just at opening.
    /// </summary>
    public sealed class ST01WhiteReedBackstageHost : IDisposable
    {
        private readonly Func<bool> atWhiteReedBackstage;
        private readonly Func<bool> liuLingPresent;
        private readonly Func<bool> liuManPresent;
        private readonly Func<bool> aShaoPerforming;
        private readonly Func<string, int> read;
        private readonly Action<string, int> write;
        private bool disposed;

        public ST01WhiteReedBackstageHost(Func<bool> atWhiteReedBackstage,
            Func<bool> liuLingPresent, Func<bool> liuManPresent,
            Func<bool> aShaoPerforming, Func<string, int> read,
            Action<string, int> write)
        {
            this.atWhiteReedBackstage = atWhiteReedBackstage ??
                throw new ArgumentNullException(nameof(atWhiteReedBackstage));
            this.liuLingPresent = liuLingPresent ??
                throw new ArgumentNullException(nameof(liuLingPresent));
            this.liuManPresent = liuManPresent ??
                throw new ArgumentNullException(nameof(liuManPresent));
            this.aShaoPerforming = aShaoPerforming ??
                throw new ArgumentNullException(nameof(aShaoPerforming));
            this.read = read ?? throw new ArgumentNullException(nameof(read));
            this.write = write ?? throw new ArgumentNullException(nameof(write));
        }

        public ST01WhiteReedBackstageSession Session { get; private set; }
        public bool IsDisposed => disposed;
        public bool IsOpen => !disposed && Session != null;

        private bool Present()
        {
            return atWhiteReedBackstage() &&
                liuLingPresent() && liuManPresent() && aShaoPerforming();
        }

        public bool TryOpen()
        {
            if (disposed || Session != null || !Present() ||
                !ST01WhiteReedBackstageSession.TryCreate(read, out var session))
                return false;
            Session = session;
            return true;
        }

        public bool TrySaveAfterPerformance()
        {
            return !disposed && Session != null && Present() &&
                Session.TryCommit(read, write);
        }

        public void Close()
        {
            Session = null;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            Session = null;
        }

        public bool HasRecordedDeparture =>
            ST01RegionalStoryFacts.TryRead(read, out ST01RegionalOutcome saved) &&
            saved.Departure.HasValue;
    }
}
