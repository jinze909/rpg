using System;
using GamingIsLove.Makinom;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>Owns the story panels that follow the memorial play.</summary>
    public sealed class ReturnOpeningContinuation : MonoBehaviour
    {
        private M00HanQiaoMeetingView meeting;
        private M01WestLaneView westLane;
        private M01DockBriefingView dock;
        private M02HarborVisit harborVisit;
        private M02QiQueNightView qiQueNight;
        private Action onFinished;
        private bool ransomCountPending;
        private bool meetingFinished;
        private bool qiQueNightPending;
        private M01PensionChoice? pensionChoice;
        private float layoutAspect;

        internal void Begin(Action finished)
        {
            onFinished = finished;
            gameObject.AddComponent<M00RollCallSession>().Begin(_ => OpenMeeting());
        }

        private void OpenMeeting()
        {
            var sequence = new M00HanQiaoMeetingSequence();
            sequence.Begin();
            meeting = new M00HanQiaoMeetingView(transform, sequence,
                _ => FinishMeeting(), RecordRansomCount);
        }

        private void RecordRansomCount()
        {
            ransomCountPending = !M00HanRansomFacts.TryRecordConfirmedCount();
        }

        private void FinishMeeting()
        {
            meetingFinished = true;
            OpenWestLaneWhenReady();
        }

        private void OpenWestLaneWhenReady()
        {
            if (!meetingFinished || ransomCountPending ||
                !M00RollCallFacts.TryRead(out M00RollCallOutcome outcome))
                return;

            bool prompted = Maki.Game.Variables.GetInt(M00MemorialSquare.XiaodouPromptedKey) != 0;
            meeting.Dispose();
            meeting = null;
            meetingFinished = false;
            westLane = new M01WestLaneView(transform, outcome, prompted, FinishWestLane);
        }

        private void FinishWestLane(M01PensionChoice choice)
        {
            pensionChoice = choice;
            FinishAfterRecordingChoice();
        }

        private void FinishAfterRecordingChoice()
        {
            if (!pensionChoice.HasValue ||
                !M01HomecomingResponse.TryRecordPensionChoice(pensionChoice.Value))
                return;

            M01PensionChoice recordedChoice = pensionChoice.Value;
            pensionChoice = null;
            westLane.Dispose();
            westLane = null;
            dock = new M01DockBriefingView(transform, () => OpenHarbor(recordedChoice));
        }

        private void OpenHarbor(M01PensionChoice recordedChoice)
        {
            dock.Dispose();
            dock = null;
            harborVisit = new M02HarborVisit(transform, recordedChoice, QueueQiQueNight);
        }

        private void QueueQiQueNight()
        {
            // M02's completed office and review are no longer active while
            // the optional dock-warehouse conversation is on screen.
            harborVisit?.Dispose();
            harborVisit = null;
            qiQueNightPending = true;
            OpenQiQueNightWhenReady();
        }

        private void OpenQiQueNightWhenReady()
        {
            if (!qiQueNightPending || qiQueNight != null || onFinished == null ||
                !M02QiQueDebtFacts.TryRead(out bool debtKnown))
                return;

            // Only a question asked at Han Qiao's desk reveals the mortgage.
            // The night conversation neither invents that discovery nor books a ship.
            qiQueNightPending = false;
            qiQueNight = new M02QiQueNightView(transform, debtKnown, CloseOpening);
        }

        private void CloseOpening()
        {
            qiQueNight?.Dispose();
            qiQueNight = null;
            Action completed = onFinished;
            onFinished = null;
            completed?.Invoke();
        }

        private void Update()
        {
            if (ransomCountPending)
                RecordRansomCount();
            if (meetingFinished)
                OpenWestLaneWhenReady();
            if (pensionChoice.HasValue)
                FinishAfterRecordingChoice();

            float aspect = (float)Mathf.Max(1, Screen.width) / Mathf.Max(1, Screen.height);
            harborVisit?.Tick(aspect);
            if (qiQueNightPending)
                OpenQiQueNightWhenReady();
            if (!Mathf.Approximately(aspect, layoutAspect))
            {
                layoutAspect = aspect;
                meeting?.LayoutForViewportAspect(aspect);
                westLane?.LayoutForViewportAspect(aspect);
                dock?.LayoutForViewportAspect(aspect);
                qiQueNight?.LayoutForViewportAspect(aspect);
            }
        }

        private void OnDestroy()
        {
            meeting?.Dispose();
            westLane?.Dispose();
            dock?.Dispose();
            harborVisit?.Dispose();
            qiQueNight?.Dispose();
            onFinished = null;
        }
    }
}
