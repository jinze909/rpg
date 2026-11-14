using System;
using GamingIsLove.Makinom;
using UnityEngine;
using static Rpg.Soundwell.SoundwellSignalKeys;

namespace Rpg.Soundwell
{
    // Game-owned 2D interaction checks and Makinom signal dispatch.
    // No beast, combatant, quest or checkpoint references are held here.
    public sealed class TrialMechanismInteractable : MonoBehaviour
    {
        private const float Reach = 1.25f;
        private const float StandReach = 0.72f;
        private VariableHandler variables;
        private Rigidbody2D player;
        private Transform bell;
        private Vector2 bellHome;

        public event Action<BellSignal> SignalChanged;
        public event Action<bool> BellSoundRequested;
        public event Action ActionPoseRequested;
        public event Action PulseResetRequested;

        public void Configure(VariableHandler state, Rigidbody2D actor, Transform bellActor,
            Vector2 originalBellPosition)
        {
            variables = state ?? throw new ArgumentNullException(nameof(state));
            player = actor != null ? actor : throw new ArgumentNullException(nameof(actor));
            bell = bellActor != null ? bellActor : throw new ArgumentNullException(nameof(bellActor));
            bellHome = originalBellPosition;
        }

        public BellSignal Current => (BellSignal)variables.GetInt(SignalKey);
        public bool IsCarrying => variables.GetInt(CarryKey) != 0;
        public bool AtBell => Vector2.Distance(player.position, bell.position) <= Reach;
        public bool CanPlaceBell => IsCarrying && TryFindBellStand(player.position, out _);
        public Vector2 HearingPoint => new Vector2(1.15f, bell.position.y);

        // Spatial authority for gate, survivor and physical evidence
        // interactions. The beast service never reads these private fields.
        private bool Near(Vector2 point, float reach) =>
            player != null && Vector2.Distance(player.position, point) <= reach;
        public bool NearGatePanel => Near(new Vector2(-0.55f, 0f), 0.8f);
        public bool NearSurveyor => Near(SoundwellTrial.SurveyorPosition, 1.05f);
        public bool NearUpperPlank => Near(SoundwellTrial.UpperBoardPosition, 0.68f);
        public bool NearRope => Near(SoundwellTrial.RopeEvidencePosition, 0.72f);
        public bool NearCart => Near(SoundwellTrial.CartEvidencePosition, 0.72f);
        public bool NearClaws => Near(SoundwellTrial.ClawEvidencePosition, 0.72f);

        public bool TryFindBellStand(Vector2 position, out Vector2 stand)
        {
            if (Vector2.Distance(position, SoundwellTrial.SideBellStand) <= StandReach)
            {
                stand = SoundwellTrial.SideBellStand;
                return true;
            }
            if (Vector2.Distance(position, bellHome) <= StandReach)
            {
                stand = bellHome;
                return true;
            }
            stand = default;
            return false;
        }

        public void SetSignal(BellSignal signal)
        {
            if (signal == BellSignal.Ringing && variables.GetInt(RescueApproachKey) == 0)
                variables.Set(RescueApproachKey, 1);
            variables.Set(SignalKey, (int)signal);
            SignalChanged?.Invoke(signal);
        }

        public void HandleCarry()
        {
            if (IsCarrying)
            {
                if (TryFindBellStand(player.position, out Vector2 stand))
                {
                    variables.Set(CarryKey, 0);
                    bell.position = new Vector3(stand.x, stand.y, 0f);
                    // A placed bell is a real player interaction. The same
                    // visual-only pose used by Ring/Wrap acknowledges it.
                    ActionPoseRequested?.Invoke();
                }
            }
            else if (AtBell)
            {
                variables.Set(CarryKey, 1);
                SetSignal(BellSignal.Ready);
                PulseResetRequested?.Invoke();
                // An accepted lift deserves feedback; a carry press out of
                // range or a failed stand placement must animate nothing.
                ActionPoseRequested?.Invoke();
            }
        }

        public void HandleBell(bool ring, bool wrap)
        {
            if (IsCarrying || !AtBell)
                return;
            if (wrap)
            {
                if (Current == BellSignal.Wrapped || Current == BellSignal.Searching)
                {
                    SetSignal(BellSignal.Ready);
                    PulseResetRequested?.Invoke();
                }
                else if (Current == BellSignal.Ringing)
                {
                    SetSignal(BellSignal.Searching);
                    BellSoundRequested?.Invoke(true);
                }
                else
                    SetSignal(BellSignal.Wrapped);
                ActionPoseRequested?.Invoke();
            }
            else if (ring)
            {
                if (Current != BellSignal.Wrapped && Current != BellSignal.Searching)
                    SetSignal(BellSignal.Ringing);
                BellSoundRequested?.Invoke(Current == BellSignal.Wrapped ||
                    Current == BellSignal.Searching);
                ActionPoseRequested?.Invoke();
            }
        }

        public readonly struct WorldInteraction
        {
            public readonly bool InspectRope, RecoverRope, InspectCart, ShareCart,
                InspectClaws, OpenGate, TieLine, Escort, SecurePlank, InspectSurveyor;
            public WorldInteraction(bool inspectRope, bool recoverRope, bool inspectCart,
                bool shareCart, bool inspectClaws, bool openGate, bool tieLine,
                bool escort, bool securePlank, bool inspectSurveyor)
            {
                InspectRope = inspectRope;
                RecoverRope = recoverRope;
                InspectCart = inspectCart;
                ShareCart = shareCart;
                InspectClaws = inspectClaws;
                OpenGate = openGate;
                TieLine = tieLine;
                Escort = escort;
                SecurePlank = securePlank;
                InspectSurveyor = inspectSurveyor;
            }
        }

        public void HandleWorldInteraction(WorldInteraction choices)
        {
            // Preserve the original world-action priority. Geometry and the
            // current ORK chooser are computed by the scene coordinator;
            // this component only dispatches a legal Makinom-owned signal.
            string key = choices.InspectRope ? RopeObservedKey :
                choices.RecoverRope ? RopeRecoveredKey :
                choices.InspectCart ? CartObservedKey :
                choices.ShareCart ? CartClueSharedKey :
                choices.InspectClaws ? ClawsObservedKey :
                choices.OpenGate ? GateKey :
                choices.TieLine ? EscortLineKey :
                choices.Escort ? EscortKey :
                choices.SecurePlank ? PlankSecuredKey :
                choices.InspectSurveyor ? SurveyorClueKey : null;
            // World interactions are earned by actual spatial eligibility.
            // A fresh accepted action animates the exploration leader; a
            // rejected or already-consumed input must never pretend success.
            // Makinom remains the sole owner of each earned scene flag.
            if (key != null && variables.GetInt(key) == 0)
            {
                variables.Set(key, 1);
                ActionPoseRequested?.Invoke();
            }
        }
    }
}
