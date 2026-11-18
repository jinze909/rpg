using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Persists only observed M02 bridge clues into Makinom's global story
    /// variables. ORK owns save lifecycle. Findings are additive and can be
    /// stored at the moment of discovery, so abandoning the investigation
    /// never erases an already inspected cable, log, bell, or report draft.
    /// </summary>
    public static class M02BridgeEvidenceFacts
    {
        public const string EvidenceKey = "rpg.return.m02.bridge-evidence";
        private const int ValidMask = (int)(M02BridgeEvidence.CleanCableCut |
            M02BridgeEvidence.FerrymanLog | M02BridgeEvidence.ThirdBellSignal |
            M02BridgeEvidence.WarReportDraft);

        public static M02BridgeEvidence Decode(int value)
        {
            // Missing or corrupted evidence must not invent plot knowledge.
            return value >= 0 && (value & ~ValidMask) == 0
                ? (M02BridgeEvidence)value : M02BridgeEvidence.None;
        }

        public static int Encode(M02BridgeEvidence observed)
        {
            if (((int)observed & ~ValidMask) != 0)
                throw new ArgumentOutOfRangeException(nameof(observed), observed,
                    "Evidence contains an unknown clue.");
            return (int)observed;
        }

        public static M02BridgeEvidence MergeObserved(
            M02BridgeEvidence existing, M02BridgeEvidence newClue)
        {
            Encode(existing);
            M02BridgeInquiry.ValidateSingleClue(newClue);
            return existing | newClue;
        }

        public static bool TryRecordObserved(M02BridgeEvidence newlyInspected)
        {
            M02BridgeInquiry.ValidateSingleClue(newlyInspected);
            if (!Maki.Initialized || Maki.Game?.Variables == null)
                return false;

            var existing = Decode(Maki.Game.Variables.GetInt(EvidenceKey));
            var merged = MergeObserved(existing, newlyInspected);
            Maki.Game.Variables.Set(EvidenceKey, Encode(merged));
            return true;
        }

        public static bool TryRead(out M02BridgeEvidence observed)
        {
            observed = M02BridgeEvidence.None;
            if (!Maki.Initialized || Maki.Game?.Variables == null)
                return false;
            observed = Decode(Maki.Game.Variables.GetInt(EvidenceKey));
            return true;
        }
    }
}
