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
        private M03LuJunView luJun;
        private M03LuJunEncounter completedLuJun;
        private M04XiaodouFarewellView xiaodouFarewell;
        private M04XiaodouFarewell completedXiaodou;
        private bool xiaodouPending;
        private M04QiQueCupView qiQueCup;
        private M04QiQueCupEncounter completedCup;
        private bool cupPending;
        private M04DockPreflightView dockPreflight;
        private bool dockPreflightPending;
        private Action onFinished;
        private bool ransomCountPending;
        private bool meetingFinished;
        private bool qiQueNightPending;
        private bool luJunPending;
        private M01PensionChoice? pensionChoice;
        private float layoutAspect;

        // M00 writes a canonical 0/1 choice. A malformed saved value must not
        // invent a remembered stage prompt in either West Lane or M04.
        public static bool TryReadXiaodouPrompted(
            Func<string, int> getInt, out bool prompted)
        {
            prompted = false;
            if (getInt == null)
                return false;
            int value = getInt(M00MemorialSquare.XiaodouPromptedKey);
            if (value != 0 && value != 1)
                return false;
            prompted = value == 1;
            return true;
        }

        private static bool TryReadXiaodouPrompted(out bool prompted)
        {
            prompted = false;
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryReadXiaodouPrompted(
                key => variables.GetInt(key), out prompted);
        }

        // Starting a new M03 voyage choice invalidates downstream outcomes
        // from an earlier passage. A previous ship, roster, fog leg, or
        // physical farewell gift must never become facts of the new trip.
        // No new save authority is introduced; the existing Makinom keys
        // are reset before the next route is chosen.
        public static bool TryResetPreviousOutboundFacts(Action<string, int> setInt)
        {
            if (setInt == null)
                return false;

            // Invalidate every published completion flag first. If a later
            // field reset fails, no previous voyage result can be read back
            // as a completed M04/sea leg while the new visit is pending.
            setInt(M04DepartureFacts.ConfirmedKey, 0);
            setInt(M04RosterFacts.ReconciledKey, 0);
            setInt(FogListeningFacts.CompletedKey, 0);
            setInt(M04QiQueCupFacts.CupCarriedKey, 0);
            setInt(M04XiaodouFarewellFacts.RealLineKey, 0);
            // The next voyage must not inherit letters left or accepted at
            // the previous outer-harbor visit.
            setInt(M05LettersFacts.DecisionKey, 0);
            setInt(M05LandingFacts.ConfirmedKey, 0);

            return M04DepartureFacts.TryResetForNewVisit(setInt) &&
                M04RosterFacts.TryResetForNewVisit(setInt) &&
                FogListeningFacts.TryResetForNewLeg(setInt) &&
                M05LandingFacts.TryResetForNewVisit(setInt);
        }

        private static bool TryResetPreviousOutboundFacts()
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return variables != null && TryResetPreviousOutboundFacts(
                (key, value) => variables.Set(key, value));
        }

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
                !M00RollCallFacts.TryRead(out M00RollCallOutcome outcome) ||
                !TryReadXiaodouPrompted(out bool prompted))
                return;

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
            qiQueNight = new M02QiQueNightView(transform, debtKnown, QueueLuJun);
        }

        private void QueueLuJun()
        {
            qiQueNight?.Dispose();
            qiQueNight = null;
            luJunPending = true;
            OpenLuJunWhenReady();
        }

        private void OpenLuJunWhenReady()
        {
            if (!luJunPending || luJun != null || onFinished == null ||
                !M02BridgeEvidenceFacts.TryRead(out M02BridgeEvidence evidence) ||
                !M02OfficialDockFacts.TryReadEffective(out bool officialStamp) ||
                !TryResetPreviousOutboundFacts() ||
                !M03LuJunFacts.TryResetForNewVisit())
                return;

            // Use this visit's evidence and stamp. Previous M03 answers
            // must not count as choices made during the new camp visit.
            luJunPending = false;
            luJun = new M03LuJunView(transform, evidence, officialStamp, FinishLuJun);
        }

        private void FinishLuJun(M03LuJunEncounter result)
        {
            completedLuJun = result;
            RecordLuJunWhenReady();
        }

        private void RecordLuJunWhenReady()
        {
            if (completedLuJun == null || !M03LuJunFacts.TryRecord(completedLuJun))
                return;
            completedLuJun = null;
            QueueXiaodouFarewell();
        }

        private void QueueXiaodouFarewell()
        {
            // M03 has recorded the chosen route, but the voyage has not
            // departed: the child can still offer the dockside sweet.
            luJun?.Dispose();
            luJun = null;
            xiaodouPending = true;
            OpenXiaodouFarewellWhenReady();
        }

        private void OpenXiaodouFarewellWhenReady()
        {
            if (!xiaodouPending || xiaodouFarewell != null || onFinished == null ||
                !TryReadXiaodouPrompted(out bool prompted) ||
                !M04XiaodouFarewellFacts.TryResetForNewVisit())
                return;

            // Reuse only a canonical M00 answer for this farewell.
            xiaodouPending = false;
            xiaodouFarewell = new M04XiaodouFarewellView(transform, prompted,
                FinishXiaodouFarewell);
        }

        private void FinishXiaodouFarewell(M04XiaodouFarewell result)
        {
            completedXiaodou = result;
            RecordXiaodouWhenReady();
        }

        private void RecordXiaodouWhenReady()
        {
            if (completedXiaodou == null ||
                !M04XiaodouFarewellFacts.TryRecord(completedXiaodou))
                return;

            completedXiaodou = null;
            QueueQiQueCup();
        }

        private void QueueQiQueCup()
        {
            // This continuous path already included the M02 dock reunion.
            // A dockside farewell neither books the hull nor recruits her.
            xiaodouFarewell?.Dispose();
            xiaodouFarewell = null;
            cupPending = true;
            OpenQiQueCupWhenReady();
        }

        private void OpenQiQueCupWhenReady()
        {
            if (!cupPending || qiQueCup != null || onFinished == null ||
                !M02QiQueDebtFacts.TryRead(out bool debtKnown) ||
                !M04QiQueCupFacts.TryResetForNewVisit())
                return;

            // Replay cannot inherit a cup from an earlier finished journey.
            // Actual handover is recorded only after this scene completes.
            cupPending = false;
            qiQueCup = new M04QiQueCupView(transform, true, debtKnown,
                FinishQiQueCup);
        }

        private void FinishQiQueCup(M04QiQueCupEncounter result)
        {
            completedCup = result;
            RecordCupWhenReady();
        }

        private void RecordCupWhenReady()
        {
            if (completedCup == null || !M04QiQueCupFacts.TryRecord(completedCup))
                return;
            completedCup = null;
            QueueDockPreflight();
        }

        private void QueueDockPreflight()
        {
            // The cup is really carried, but this M03 route selection has
            // not placed a hull, crew or landing receiver at the dock.
            qiQueCup?.Dispose();
            qiQueCup = null;
            dockPreflightPending = true;
            OpenDockPreflightWhenReady();
        }

        private void OpenDockPreflightWhenReady()
        {
            if (!dockPreflightPending || dockPreflight != null ||
                onFinished == null ||
                !M03LuJunFacts.TryRead(out _, out _,
                    out M03VoyageChoice? route) || !route.HasValue)
                return;

            // A validated M03 saved choice selects the dialogue, but does
            // not confirm readiness, sail, or generate M05 arrival facts.
            dockPreflight = new M04DockPreflightView(
                transform, route.Value, CloseOpening);
            dockPreflightPending = false;
        }

        private void CloseOpening()
        {
            qiQueNight?.Dispose();
            qiQueNight = null;
            luJun?.Dispose();
            luJun = null;
            xiaodouFarewell?.Dispose();
            xiaodouFarewell = null;
            qiQueCup?.Dispose();
            qiQueCup = null;
            dockPreflight?.Dispose();
            dockPreflight = null;
            dockPreflightPending = false;
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
            if (luJunPending)
                OpenLuJunWhenReady();
            if (completedLuJun != null)
                RecordLuJunWhenReady();
            if (xiaodouPending)
                OpenXiaodouFarewellWhenReady();
            if (completedXiaodou != null)
                RecordXiaodouWhenReady();
            if (cupPending)
                OpenQiQueCupWhenReady();
            if (completedCup != null)
                RecordCupWhenReady();
            if (dockPreflightPending)
                OpenDockPreflightWhenReady();
            if (!Mathf.Approximately(aspect, layoutAspect))
            {
                layoutAspect = aspect;
                meeting?.LayoutForViewportAspect(aspect);
                westLane?.LayoutForViewportAspect(aspect);
                dock?.LayoutForViewportAspect(aspect);
                qiQueNight?.LayoutForViewportAspect(aspect);
                luJun?.LayoutForViewportAspect(aspect);
                xiaodouFarewell?.LayoutForViewportAspect(aspect);
                qiQueCup?.LayoutForViewportAspect(aspect);
                dockPreflight?.LayoutForViewportAspect(aspect);
            }
        }

        private void OnDestroy()
        {
            meeting?.Dispose();
            westLane?.Dispose();
            dock?.Dispose();
            harborVisit?.Dispose();
            qiQueNight?.Dispose();
            luJun?.Dispose();
            xiaodouFarewell?.Dispose();
            qiQueCup?.Dispose();
            dockPreflight?.Dispose();
            onFinished = null;
        }
    }
}
