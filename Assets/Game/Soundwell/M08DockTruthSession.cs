using System;

namespace Rpg.Soundwell
{
    public enum M08DockTruthStage
    {
        AwaitingTruth,
        AwaitingResponse,
        AwaitingHost,
        Completed
    }

    public enum M08DockTruthResponse
    {
        AngerAtBeingShielded = 1,
        StaySilent = 2,
        AdmitSharedFear = 3
    }

    /// <summary>
    /// The optional M08 night-dock conversation after the first civilian
    /// commission. Shen Li must really be present; the commission must have
    /// been confirmed. The scene host, not this isolated session, owns the
    /// Makinom shen_relation write and any chapter transition.
    /// </summary>
    public sealed class M08DockTruthSession
    {
        public M08DockTruthSession(bool shenLiPresent, bool firstWinchConfirmed)
        {
            ShenLiPresent = shenLiPresent;
            FirstWinchConfirmed = firstWinchConfirmed;
        }

        public bool ShenLiPresent { get; }
        public bool FirstWinchConfirmed { get; }
        public bool CanConverse => ShenLiPresent && FirstWinchConfirmed;
        public M08DockTruthStage Stage { get; private set; } = M08DockTruthStage.AwaitingTruth;
        public M08DockTruthResponse? SelectedResponse { get; private set; }
        public bool IsCompleted => Stage == M08DockTruthStage.Completed;

        public bool TryHearTruth()
        {
            if (!CanConverse || Stage != M08DockTruthStage.AwaitingTruth)
                return false;
            Stage = M08DockTruthStage.AwaitingResponse;
            return true;
        }

        public bool TryRespond(M08DockTruthResponse response)
        {
            if (!CanConverse || Stage != M08DockTruthStage.AwaitingResponse ||
                !Enum.IsDefined(typeof(M08DockTruthResponse), response))
                return false;
            SelectedResponse = response;
            Stage = M08DockTruthStage.AwaitingHost;
            return true;
        }

        public bool TryAcknowledge(Func<M08DockTruthResponse, bool> record)
        {
            if (Stage != M08DockTruthStage.AwaitingHost || !SelectedResponse.HasValue ||
                record == null || !record(SelectedResponse.Value))
                return false;
            // If the host throws or refuses the authoritative save, keep
            // the selected reply available for another confirmation.
            Stage = M08DockTruthStage.Completed;
            return true;
        }
    }
}
