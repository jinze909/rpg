using System;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Scene-callable ST-03 ("第三声钟") host. Enforces live Zhongling bathhouse
    /// presence and clean ST03BathhouseFacts state before opening ST03BathhouseView,
    /// and persists the chosen permanent plan through ST03BathhouseSession with
    /// torn-write and lost-acknowledgement idempotent recovery.
    /// </summary>
    public sealed class ST03BathhouseHost : IDisposable
    {
        private readonly Transform parent;
        private readonly Func<bool> atZhonglingBathhouse;
        private readonly Func<bool> careHomeInterviewReady;
        private readonly Func<bool> safeRouteReady;
        private readonly Func<bool> trialNightReady;
        private readonly Func<bool> transportBudgetReady;
        private readonly Func<string, int> getInt;
        private readonly Action<string, int> setInt;
        private ST03BathhouseSession session;
        private ST03BathhouseView activeView;
        private bool disposed;
        private bool opening;

        public ST03BathhouseSession Session => session;
        public ST03BathhouseView ActiveView =>
            (activeView != null && !activeView.IsDisposed) ? activeView : null;
        public bool IsDisposed => disposed;
        public bool IsCompleted =>
            ST03BathhouseFacts.TryRead(getInt, out ST03BathhouseRecord? saved) &&
            saved.HasValue;

        public ST03BathhouseHost(
            Transform parent,
            Func<bool> atZhonglingBathhouse,
            Func<bool> careHomeInterviewReady,
            Func<bool> safeRouteReady,
            Func<bool> trialNightReady,
            Func<bool> transportBudgetReady,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.atZhonglingBathhouse = atZhonglingBathhouse ??
                throw new ArgumentNullException(nameof(atZhonglingBathhouse));
            this.careHomeInterviewReady = careHomeInterviewReady ??
                throw new ArgumentNullException(nameof(careHomeInterviewReady));
            this.safeRouteReady = safeRouteReady ??
                throw new ArgumentNullException(nameof(safeRouteReady));
            this.trialNightReady = trialNightReady ??
                throw new ArgumentNullException(nameof(trialNightReady));
            this.transportBudgetReady = transportBudgetReady ??
                throw new ArgumentNullException(nameof(transportBudgetReady));
            this.getInt = getInt ?? throw new ArgumentNullException(nameof(getInt));
            this.setInt = setInt ?? throw new ArgumentNullException(nameof(setInt));
        }

        public bool TryOpen()
        {
            if (opening || disposed || parent == null)
                return false;

            opening = true;
            try
            {
                return TryOpenCore();
            }
            finally
            {
                opening = false;
            }
        }

        private bool TryOpenCore()
        {
            if (activeView != null && activeView.IsDisposed)
            {
                activeView.Dispose();
                activeView = null;
            }
            if (disposed || parent == null || activeView != null || !atZhonglingBathhouse())
                return false;

            if (!ST03BathhouseSession.TryCreate(getInt, out ST03BathhouseSession created) ||
                disposed || parent == null || activeView != null)
                return false;

            var createdView = new ST03BathhouseView(
                parent,
                created,
                careHomeInterviewReady,
                safeRouteReady,
                trialNightReady,
                transportBudgetReady,
                HandleCommitRequested);
            if (disposed || parent == null || createdView.IsDisposed)
            {
                createdView.Dispose();
                return false;
            }
            session = created;
            activeView = createdView;
            return true;
        }

        private bool IsCurrentSubmission(
            ST03BathhouseView expectedView, ST03BathhouseSession expectedSession)
        {
            return !disposed && parent != null && expectedView != null &&
                expectedSession != null && activeView == expectedView &&
                session == expectedSession && !expectedView.IsDisposed;
        }

        private void HandleCommitRequested()
        {
            var submittingView = activeView;
            var submittingSession = session;

            // Live scene proofs may synchronously close the dialog or replace
            // it. A retired choice must not commit or close the new dialog.
            if (!IsCurrentSubmission(submittingView, submittingSession) ||
                !atZhonglingBathhouse() || !IsCurrentSubmission(submittingView, submittingSession) ||
                !careHomeInterviewReady() || !IsCurrentSubmission(submittingView, submittingSession) ||
                !safeRouteReady() || !IsCurrentSubmission(submittingView, submittingSession) ||
                !trialNightReady() || !IsCurrentSubmission(submittingView, submittingSession) ||
                !transportBudgetReady() || !IsCurrentSubmission(submittingView, submittingSession))
                return;

            bool abandoned = false;
            void WriteWhileCurrent(string key, int value)
            {
                if (abandoned || !IsCurrentSubmission(submittingView, submittingSession))
                {
                    abandoned = true;
                    return;
                }
                setInt(key, value);
            }

            if (!submittingSession.TryCommit(getInt, WriteWhileCurrent) ||
                abandoned || !IsCurrentSubmission(submittingView, submittingSession))
                return;

            submittingView.Dispose();
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
