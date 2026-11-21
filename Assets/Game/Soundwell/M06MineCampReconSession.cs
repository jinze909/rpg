using System;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Tracks player inspection of the four M06 mine-camp leads (押运、看守、
    /// 物资账、矿道与主泵) that inform the rescue plan decision. Persists every
    /// observed lead to Makinom before reporting it as complete.
    /// </summary>
    public sealed class M06MineCampReconSession
    {
        // A map dialogue can remain open across a new voyage. The same
        // landing numbers on two confirmed arrivals do not mean the same
        // inspection happened. M05 is the sole owner of this identity.
        private readonly int arrivalSequence;
        public int ObservedMask { get; private set; }
        public int ObservedCount => M06MineCampReconFacts.CountObserved(ObservedMask);
        public bool AllLeadsObserved => ObservedMask == M06MineCampReconFacts.AllLeadsMask;

        private M06MineCampReconSession(int observedMask, int arrivalSequence)
        {
            ObservedMask = observedMask;
            this.arrivalSequence = arrivalSequence;
        }

        public static bool TryLoad(Func<string, int> getInt, out M06MineCampReconSession session)
        {
            session = null;
            if (getInt == null)
                return false;
            if (!M05LandingFacts.TryRead(getInt, out M05LandingReport? landing) ||
                !landing.HasValue)
                return false;
            if (!M06MineCampReconFacts.TryRead(getInt, out int mask))
                return false;

            session = new M06MineCampReconSession(
                mask, getInt(M05LandingFacts.ArrivalSequenceKey));
            return true;
        }

        public bool IsLeadObserved(M06MineCampLead lead)
        {
            return M06MineCampReconFacts.Contains(ObservedMask, lead);
        }

        public bool TryInspectLead(
            M06MineCampLead lead,
            Func<string, int> getInt,
            Action<string, int> setInt)
        {
            if (IsLeadObserved(lead) || getInt == null ||
                !M05LandingFacts.TryRead(getInt, out M05LandingReport? landing) ||
                !landing.HasValue ||
                getInt(M05LandingFacts.ArrivalSequenceKey) != arrivalSequence)
                return false;

            if (!M06MineCampReconFacts.TryRecord(lead, getInt, setInt))
                return false;

            if (!M06MineCampReconFacts.TryRead(getInt, out int refreshed))
                return false;

            ObservedMask = refreshed;
            return true;
        }

        public static string FormatLeadTitle(M06MineCampLead lead, bool zh)
        {
            switch (lead)
            {
                case M06MineCampLead.EscortSchedule:
                    return zh ? "押运窗口（监察登岛核验）" : "Escort Schedule (Inspection Window)";
                case M06MineCampLead.GuardPatrol:
                    return zh ? "看守与巡逻艇（二十分钟扫港间隔）" : "Guard & Patrol Craft (20-Min Pier Sweep)";
                case M06MineCampLead.SupplyLedger:
                    return zh ? "物资账与盐箱货单（口粮损耗与装运）" : "Supply Ledger & Salt Crates";
                case M06MineCampLead.MineTunnelAndPump:
                    return zh ? "矿道与主排水泵（下层五百人安全底线）" : "Mine Tunnels & Main Drainage Pump";
                default:
                    throw new ArgumentOutOfRangeException(nameof(lead));
            }
        }

        public static string FormatLeadFindings(M06MineCampLead lead, bool zh)
        {
            switch (lead)
            {
                case M06MineCampLead.EscortSchedule:
                    return zh
                        ? "伊蕾的合法登岛核验可争取分批放行窗口，主泵保持运转，但手续慢且需承担停港与补给账。"
                        : "Yi Lei's legal inspection copy opens a batch release window while keeping the main pump running, at higher harbor-stay supply cost.";
                case M06MineCampLead.GuardPatrol:
                    return zh
                        ? "巡逻艇每隔二十分钟扫过码头；强行夺取栈桥前必须先将非战斗矿工引入掩体，并承担冲突修复代价。"
                        : "Patrol craft sweep the pier every 20 minutes; seizing the pier requires sheltering non-combatants first and accepting repair costs.";
                case M06MineCampLead.SupplyLedger:
                    return zh
                        ? "盐箱与夜间货单可掩护愿离岛者分批倒运，不拉主泵总闸，但货物让位给人会损失本次运费与载货量。"
                        : "Salt crates and night manifests cover batch transfers without stopping the main pump, sacrificing freight revenue and cargo capacity.";
                case M06MineCampLead.MineTunnelAndPump:
                    return zh
                        ? "主泵若无准备停转，下层五百人三天内就会被水漫；须先引上层集结、教会接手人并接通手摇备用泵，才可安全停机三小时。"
                        : "An unprepared shutdown floods 500 lower-level miners within three days; move them upstairs, train relief crew, and ready hand pumps before any 3-hour shutdown.";
                default:
                    throw new ArgumentOutOfRangeException(nameof(lead));
            }
        }
    }
}
