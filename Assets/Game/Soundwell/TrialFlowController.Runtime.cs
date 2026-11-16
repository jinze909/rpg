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
        protected void LateUpdate()
        {
            if (!IsReady)
                return;

            // ORK can instantiate its own UI root after the initial scene setup.
            // Reconcile the EventSystem after framework/party startup so both
            // exploration and ORK battle menus share one InputSystemUIInputModule.
            if (Time.unscaledTime >= nextEventSystemAuditAt)
            {
                SoundwellInputAdapter.EnsureEventSystem(transform);
                nextEventSystemAuditAt = Time.unscaledTime + 1f;
            }

            if (DirectCombatStarted && orkParty != null)
            {
                if (orkParty.EncounterWon)
                {
                    variables.Set(DirectCombatStartedKey, 0);
                    variables.Set(DirectCombatCompleteKey, 1);
                    variables.Set(RescueApproachKey, 2);
                    SoundwellInputAdapter.SetGameplayContext();
                }
                else if (orkParty.EncounterFailed || orkParty.EncounterStartFailed)
                {
                    variables.Set(DirectCombatStartedKey, 0);
                    variables.Set(RescueApproachKey, 0);
                    variables.Set(GateKey, 0);
                    orkParty.ReleaseFailedEncounter();
                    SoundwellInputAdapter.SetGameplayContext();
                }
            }

            bool battleMode = DirectCombatStarted;
            view.SetBattleMode(battleMode);
            if (battleMode)
            {
                // The view reads a live ORK snapshot; it never resolves attacks
                // or maintains a second copy of combat state.
                view.SetBattleReadout(orkParty != null ? orkParty.BuildBattleReadout() : string.Empty);
                // Read ORK's authoritative HP each frame. A drop produces
                // only a brief pixel hit cue; never subtract HP in Unity.
                view.RefreshBattleBeastHp(orkParty != null && orkParty.Beast != null
                    ? orkParty.Beast.Status[1].GetValue() : float.NaN);
                // The leader receives the same read-only ORK HP feedback as
                // the beast and the two companion renderers. No synthetic
                // damage, extra turn or parallel combat event is introduced.
                view.RefreshBattleLeaderHp(orkParty != null &&
                    orkParty.Members.Count > 0 && orkParty.Members[0] != null
                        ? orkParty.Members[0].Status[1].GetValue() : float.NaN);
                int activeRescuer = orkParty != null ? orkParty.ChoosingRescuerIndex : -1;
                // ORK may retire a combatant before the chooser HUD updates.
                // A valid index does not guarantee that Unity's actor survives.
                Transform activeActor = ResolveBattleActor(
                    orkParty != null ? orkParty.Actors : null, activeRescuer);
                view.SetBattleChoices(activeRescuer, activeActor,
                    orkParty != null && orkParty.ReadyBaseAttackerIndex == activeRescuer && activeRescuer >= 0,
                    orkParty != null && orkParty.CanGuard(activeRescuer),
                    orkParty != null && orkParty.CanOperateGate(activeRescuer, canReachBattleGate),
                    GateOpen);
                view.SetBattleMoveChoices(activeRescuer, activeActor,
                    orkParty != null && orkParty.CanStep(activeRescuer, Vector2.up),
                    orkParty != null && orkParty.CanStep(activeRescuer, Vector2.right),
                    orkParty != null && orkParty.CanStep(activeRescuer, Vector2.down),
                    orkParty != null && orkParty.CanStep(activeRescuer, Vector2.left));
                movement = Vector2.zero;
                playerBody.linearVelocity = Vector2.zero;
            }

            // Exploration hazards are delegated to checkpoint + patrol; ORK
            // turn movement retains its independent movement authority.
            if (checkpoint.ShouldRecoverAtVisibleLowerPlank(
                    DirectCombatStarted, GateOpen, playerBody.position))
            {
                checkpoint.RecoverLowerPlank(LowerPlankBroken);
                // Every real lower-board fall needs an observable impact:
                // the first collapse cracks the plank, but subsequent steps
                // onto its open gap still knock the player back. Do not let
                // that later physical relocation happen silently.
                view.AnimatePlayerHit();
                // The collapse is a real relocation, not a walk through the
                // old broken board. Keep the two ORK-owned rescuer visuals at
                // the same recovery tile without changing turn/battle state.
                orkParty?.RegroupAfterCheckpoint(playerBody.position);
                movement = Vector2.zero;
            }

            Vector2 destination = patrol.Tick(mechanisms.Current,
                mechanisms.HearingPoint, DirectCombatStarted,
                DirectCombatComplete, GateOpen, RescueComplete);

            // Keep the ONE physical bell attached to its actual carrier after
            // *all* scene-owned relocations, including the weak plank recovery
            // and beast knockback. Updating before contact leaves the bell at
            // the old location for a frame and advertises a false drop point.
            // Only Makinom's existing carrying variable owns this decision.
            if (IsCarrying)
            {
                Vector2 held = CarriedBellWorldPosition(playerBody.position);
                bell.position = new Vector3(held.x, held.y, 0f);
            }

            // The drawn intent marker receives exactly the destination already
            // used by the beast, never a separate visual guess.
            bool returning = patrol.IsReturning(DirectCombatStarted,
                DirectCombatComplete, mechanisms.Current);
            view.AnimatePlayerMotion(movement.sqrMagnitude > 0.0001f && !battleMode && !ActionMenuOpen,
                movement.x);
            view.Refresh(HasBellTarget, IsSearching, returning, IsWrapped, AtBell, AtSafePoint,
                IsCarrying, CanPlaceBell, GateOpen, AtGatePanel, CanOpenGate,
                CanEngageBeast, DirectCombatComplete, AtSurveyor,
                HasSurveyorClue, AtUpperPlank, CanSecurePlank, PlankSecured,
                CanEscortSurveyor, SurveyorEscorting, RescueComplete, FalseMarkerCleared,
                SafeRouteMarked, AtRopeEvidence, AtCartEvidence, AtClawEvidence, CanInspectRope, CanRecoverRope,
                CanInspectCart, CanInspectClaws, CanTieEscortLine, CanShareCartClue,
                RopeObserved, RopeRecovered, CartObserved, CartClueShared, CartKitReclaimed,
                ClawsObserved, SurveyorLineTied,
                BeastBlocksEscape, BeastContacted, LowerPlankBroken,
                combatStartedInThisScene && !DirectCombatStarted && !DirectCombatComplete,
                SearchRemainingFraction, destination);
            // Makinom owns the rescue outcome. Neither opening the gate nor
            // reaching safety alone is enough: the survivor must physically
            // make it back to the same marked retreat point.
            if (bootstrapLoad == null && SurveyorEscorting && !RescueComplete && AtSafePoint &&
                Vector2.Distance(view.SurveyorWorldPosition, safePoint.position) <= 0.9f &&
                orkParty != null && orkParty.SettleRescue())
                variables.Set(RescueKey, 1);

            // The surveyor does what he promised after being saved. This
            // second, observable outcome occurs only when his actual figure
            // reaches his misleading route marker, not on rescue alone.
            if (RescueComplete && !FalseMarkerCleared &&
                Vector2.Distance(view.SurveyorWorldPosition, view.FalseMarkerWorldPosition) <= 0.20f)
                variables.Set(FalseMarkerClearedKey, 1);

            // The optional cart clue pays off only through the survivor's
            // physical return to the cart, AFTER he removes his false sign.
            // No equipment, quest or reward state is fabricated by the UI.
            if (RescueComplete && FalseMarkerCleared && CartObserved && CartClueShared &&
                !CartKitReclaimed &&
                Vector2.Distance(view.SurveyorWorldPosition, CartEvidenceWorldPosition) <= 0.20f)
                variables.Set(CartKitReclaimedKey, 1);

            // The optional map kit changes the world: after retrieving it, the
            // surveyor actually walks back and posts the corrected route at
            // his former misleading sign. Merely exiting cannot create it.
            if (CartKitReclaimed && FalseMarkerCleared && !SafeRouteMarked &&
                Vector2.Distance(view.SurveyorWorldPosition, view.FalseMarkerWorldPosition) <= 0.20f)
                variables.Set(SafeRouteMarkedKey, 1);
        }

        // The 0.35-unit hand offset belongs to the authored trial, not the
        // global X axis. Keep the single carried bell beside the real physics
        // player when the room is translated, rotated, or scaled.
        public Vector2 CarriedBellWorldPosition(Vector2 leaderWorldPosition) =>
            leaderWorldPosition + (Vector2)transform.TransformVector(
                new Vector3(0.35f, 0f, 0f));

        // The plank belongs to exploration physics, never ORK's turn-owned
        // combatant placement. Keep the rule independent of the asynchronous
        // ORK gate-action calculation: a queued gate action is not yet GateOpen.
        public static bool ShouldRecoverFromLowerPlank(
            bool directCombatStarted, bool gateOpen, Vector2 actorPosition) =>
            TrialCheckpointManager.ShouldRecoverFromLowerPlank(
                directCombatStarted, gateOpen, actorPosition);

        // Resolve the actual ORK scene actor, never an inferred turn marker.
        // Unity destroyed objects compare null even if their list entry remains.
        public static Transform ResolveBattleActor(
            System.Collections.Generic.IReadOnlyList<GameObject> actors, int index)
        {
            if (actors == null || index < 0 || index >= actors.Count)
                return null;
            GameObject actor = actors[index];
            // ORK can retire an actor by disabling its scene GameObject
            // without destroying the managed Combatant. Never publish that
            // stale Transform as an active chooser in either UGUI menu.
            return actor != null && actor.activeInHierarchy ? actor.transform : null;
        }

        // An ORK turn may retire its combatant between input selection and
        // the gate reach callback during retreat or scene teardown.
        // Missing owners/visuals must never open the physical gate or throw.
        private bool CanReachBattleGate(Combatant member) =>
            DirectCombatStarted && !GateOpen &&
            // The panel sprite is child-local to this trial root. ORK's
            // chooser must use its actual world position after a scene move
            // or rotation instead of the legacy world-origin coordinates.
            SoundwellTrial.IsLiveActorAtGate(
                member != null ? member.GameObject : null,
                transform.TransformPoint(new Vector2(-0.55f, 0f)));
    }
}
