using System;

namespace Rpg.Soundwell
{
    [Flags]
    public enum M02BridgeEvidence
    {
        None = 0,
        CleanCableCut = 1,
        FerrymanLog = 2,
        ThirdBellSignal = 4,
        WarReportDraft = 8
    }

    public enum M02EvidenceLocation
    {
        NotStarted,
        FerryWarehouse,
        HarborArchive,
        Finished
    }

    /// <summary>
    /// An optional M02 investigation. Each clue is distinct, can be missed,
    /// and can only be acquired at its authored location. Makinom remains
    /// responsible for persisting the actual facts when the host confirms them.
    /// These clues do not identify who ordered the bridge cut.
    /// </summary>
    public sealed class M02BridgeInquiry
    {
        public M02EvidenceLocation Location { get; private set; }
        public M02BridgeEvidence Found { get; private set; }
        public M02BridgeEvidence? LastExamined { get; private set; }

        public bool Has(M02BridgeEvidence clue)
        {
            ValidateSingleClue(clue);
            return (Found & clue) == clue;
        }

        public void Begin()
        {
            Require(M02EvidenceLocation.NotStarted);
            Location = M02EvidenceLocation.FerryWarehouse;
        }

        public bool Examine(M02BridgeEvidence clue)
        {
            ValidateSingleClue(clue);
            bool archive = clue == M02BridgeEvidence.WarReportDraft;
            Require(archive ? M02EvidenceLocation.HarborArchive : M02EvidenceLocation.FerryWarehouse);
            bool fresh = !Has(clue);
            Found |= clue;
            LastExamined = clue;
            return fresh;
        }

        public void LeaveWarehouse()
        {
            Require(M02EvidenceLocation.FerryWarehouse);
            LastExamined = null;
            Location = M02EvidenceLocation.HarborArchive;
        }

        public void LeaveArchive()
        {
            Require(M02EvidenceLocation.HarborArchive);
            LastExamined = null;
            Location = M02EvidenceLocation.Finished;
        }

        // Host persists/hands off observed evidence only when leaving the
        // archive. When that callback fails, restore HarborArchive and the
        // last examined clue so the player can safely retry finishing.
        public void LeaveArchive(Action<M02BridgeEvidence> recordWithHost)
        {
            if (recordWithHost == null)
                throw new ArgumentNullException(nameof(recordWithHost));
            M02EvidenceLocation beforeLocation = Location;
            M02BridgeEvidence? beforeExamined = LastExamined;
            LeaveArchive();
            try
            {
                recordWithHost(Found);
            }
            catch
            {
                Location = beforeLocation;
                LastExamined = beforeExamined;
                throw;
            }
        }

        public static void ValidateSingleClue(M02BridgeEvidence clue)
        {
            if (clue != M02BridgeEvidence.CleanCableCut &&
                clue != M02BridgeEvidence.FerrymanLog &&
                clue != M02BridgeEvidence.ThirdBellSignal &&
                clue != M02BridgeEvidence.WarReportDraft)
                throw new ArgumentOutOfRangeException(nameof(clue), clue,
                    "Each recorded bridge evidence item must be one authored clue.");
        }

        private void Require(M02EvidenceLocation expected)
        {
            if (Location != expected)
                throw new InvalidOperationException("M02 investigation is not at " + expected + ".");
        }
    }

    public static class M02BridgeInquiryCopy
    {
        public static string Intro(M02EvidenceLocation location, bool chinese)
        {
            switch (location)
            {
                case M02EvidenceLocation.FerryWarehouse:
                    return chinese
                        ? "临潮渡口仓库。守桥老人从下游捞回了白水渡断缆。可逐项查看，也可先去谈船；漏查不会封死后续航路。"
                        : "At the ferry warehouse, the old bridge keeper has saved the cable recovered downstream. Examine any clue or move on; missing one will not bar your voyage.";
                case M02EvidenceLocation.HarborArchive:
                    return chinese
                        ? "港务署旧档案室。你可翻看十二年前白水渡的战报底稿，也可以先离开。"
                        : "In the harbor archives, you may read the original Whitewater battle report draft, or leave it for another time.";
                case M02EvidenceLocation.Finished:
                    return chinese
                        ? "调查暂告一段落。未找到的旁证仍是未知，不能当作已经查明。"
                        : "The inquiry ends for now. Unseen clues remain unknown, not established facts.";
                default:
                    throw new InvalidOperationException("Begin the M02 bridge inquiry first.");
            }
        }

        public static string ClueText(M02BridgeEvidence clue, bool chinese)
        {
            M02BridgeInquiry.ValidateSingleClue(clue);
            switch (clue)
            {
                case M02BridgeEvidence.CleanCableCut:
                    return chinese
                        ? "断缆切面平直，没有焦痕，像是重型工兵刀留下的。它证明缆索是被切断的，却不能单独证明谁下的令。"
                        : "The broken cable has a straight cut, with no charring, like a heavy engineer's blade. This shows a cut, not who ordered it.";
                case M02BridgeEvidence.FerrymanLog:
                    return chinese
                        ? "守桥老人的旧记录册写着：“三更，工兵队，持总帅令。”这是当年的一笔旧记，不是你亲眼见到命令人。"
                        : "The old keeper's log reads: 'Third watch. Engineers. Carrying the commander's order.' It records what he saw, not a confession from the issuer.";
                case M02BridgeEvidence.ThirdBellSignal:
                    return chinese
                        ? "守桥老人说三声钟不是报时，而是工兵队的信号；第三声落下，桥头就动刀。"
                        : "The keeper says the three bells were an engineer signal, not a clock. The cable was cut at the third bell.";
                case M02BridgeEvidence.WarReportDraft:
                    return chinese
                        ? "战报底稿仍写敌军火攻、第三旅全员殉国，字迹曾被涂改，右下角署名韩峤。署名证明他签了这份战报，不能只凭此推定断桥命令来自谁。"
                        : "The draft claims enemy fire and the Third Brigade's total loss. It shows revisions and Han Qiao's signature. His signature identifies the report's author, not the person who ordered the cut.";
                default:
                    throw new ArgumentOutOfRangeException(nameof(clue), clue, null);
            }
        }
    }
}
