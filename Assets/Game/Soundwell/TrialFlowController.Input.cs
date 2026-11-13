using GamingIsLove.Makinom;
using GamingIsLove.ORKFramework;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Rpg.Soundwell.SoundwellSignalKeys;
using Signal = Rpg.Soundwell.BellSignal;

namespace Rpg.Soundwell
{
    public abstract partial class TrialFlowController : MonoBehaviour
    {
        public AsyncOperation ReturnToBootstrap()
        {
            if (bootstrapLoad != null)
                return bootstrapLoad;

            SetActionMenuOpen(false);
            // Both retreat and victory can leave ORK's encounter owner alive
            // after the scene unloads. Release it before opening a fresh trial.
            if (combatStartedInThisScene && orkParty != null && orkParty.IsReady)
            {
                orkParty.ResetEncounter();
                variables.Set(DirectCombatStartedKey, 0);
                variables.Set(RescueApproachKey, 0);
            }
            // Preserve the actual last exit result for the Bootstrap UI.
            // A fresh incomplete retreat overwrites an earlier victory.
            if (variables != null)
            {
                variables.Set(EntryOutcomeKey, FalseMarkerCleared ? 2 : RescueComplete ? 1 : 0);
                variables.Set(EntryOutcomeRecordedKey, 1);
                // Overwrite this every exit, including an incomplete replay.
                // Merely finding or telling about the cart does not count.
                variables.Set(EntryCartKitKey,
                    RescueComplete && FalseMarkerCleared && CartKitReclaimed ? 1 : 0);
                variables.Set(EntrySafeRouteKey,
                    RescueComplete && FalseMarkerCleared && CartKitReclaimed && SafeRouteMarked ? 1 : 0);
                variables.Set(EntryBeastContactsKey, BeastContacts);
            }
            bootstrapLoad = SceneManager.LoadSceneAsync(SoundwellEntrance.BootstrapSceneName);
            movement = Vector2.zero;
            if (IsReady)
                playerBody.linearVelocity = Vector2.zero;
            return bootstrapLoad;
        }

        // Keyboard, gamepad and the integration harness share this action boundary.
        public void ApplyControls(SoundwellControlFrame controls)
        {
            // Do not double-handle the same gamepad A / directional event when
            // InputSystemUIInputModule is submitting or moving a focused UGUI Button.
            // A stale UI callback can arrive before Bootstrap/ORK has finished
            // initializing or after an exit has already started scene loading.
            // Do not act on incomplete scene owners or trigger a second unload.
            if (!IsReady || bootstrapLoad != null || actionMenuOpen || DirectCombatStarted)
                return;

            if (controls.Back)
            {
                ReturnToBootstrap();
                return;
            }
            ApplyInput(controls.Movement, controls.Ring, controls.Wrap, controls.Retry,
                controls.Carry, controls.Gate);
        }

        public void ApplyInput(Vector2 direction, bool ring, bool wrap, bool retry,
            bool carry = false, bool gate = false)
        {
            // Input events from a previous UGUI focus can reach this public
            // entry point before the scene is ready. Never dereference its
            // uninitialized player/variables or mutate a scene being unloaded.
            if (!IsReady || bootstrapLoad != null || DirectCombatStarted)
                return;

            movement = Vector2.ClampMagnitude(direction, 1f);
            if (retry && AtSafePoint)
            {
                // A Retry submitted from focused UGUI must hand control back
                // to exploration. Resetting Makinom state alone otherwise
                // leaves the UI action map enabled and movement suppressed.
                SetActionMenuOpen(false);
                if (combatStartedInThisScene)
                {
                    // Restart the live scene to create fresh ORK combatants,
                    // group registrations and battle components. Simply
                    // regenerating HP on a completed ORK battle cannot create
                    // a second encounter (Unity regression: 0 participants).
                    orkParty.ResetEncounter();
                    movement = Vector2.zero;
                    playerBody.linearVelocity = Vector2.zero;
                    bootstrapLoad = SceneManager.LoadSceneAsync(SoundwellEntrance.SceneName);
                }
                else
                {
                    ResetTrial();
                }
                return;
            }

            if (carry)
            {
                mechanisms.HandleCarry();
                return;
            }

            // Only the west-side panel can open the physical gate, and only
            // once the side bell has drawn the real beast away from its lane.
            if (gate)
            {
                if (CanEngageBeast)
                {
                    SetActionMenuOpen(false);
                    SoundwellInputAdapter.SetUiContext();
                    if (orkParty.TryStartDirectEncounter())
                    {
                        // ORK is the sole encounter authority. Never play an
                        // accepted-action pose before its actual Start succeeds.
                        // A stale battle owner can reject engagement even while
                        // the gate is spatially in reach.
                        view.AnimatePlayerAction();
                        combatStartedInThisScene = true;
                        variables.Set(RescueApproachKey, 2);
                        variables.Set(DirectCombatStartedKey, 1);
                        movement = Vector2.zero;
                        playerBody.linearVelocity = Vector2.zero;
                        view.SetBattleMode(true);
                    }
                    else
                    {
                        SoundwellInputAdapter.SetGameplayContext();
                    }
                }
                else
                    mechanisms.HandleWorldInteraction(
                        new TrialMechanismInteractable.WorldInteraction(
                            CanInspectRope, CanRecoverRope, CanInspectCart,
                            CanShareCartClue, CanInspectClaws, CanOpenGate,
                            CanTieEscortLine, CanEscortSurveyor, CanSecurePlank,
                            AtSurveyor && !HasSurveyorClue));
                return;
            }

            mechanisms.HandleBell(ring, wrap);
        }

        private void ResetTrial()
        {
            movement = Vector2.zero;
            checkpoint.ResetFlagsAndPosition();
            if (orkParty != null && orkParty.IsReady)
                orkParty.ResetEncounter();
            view.SetBattleMode(false);
            view.ResetPlayerAnimation();
            view.ResetSurveyor();
            patrol.ResetPatrol();
            mechanisms.SetSignal(Signal.Ready);
            view.ResetPulse();
        }

        protected void OnDestroy()
        {
            view?.Dispose();
        }
    }
}
