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
        protected void Start()
        {
            // The saved ORK starter initializes Makinom before this scene begins.
            variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
            {
                enabled = false;
                throw new InvalidOperationException("Makinom's game variables are unavailable in the Soundwell scene.");
            }

            orkParty = GetComponent<SoundwellOrkParty>();
            SoundwellInputAdapter.SetGameplayContext();
            SoundwellInputAdapter.EnsureEventSystem(transform);
            partyMovement = GetComponent<PartyMovement2D>();
            if (partyMovement == null)
                partyMovement = gameObject.AddComponent<PartyMovement2D>();
            playerBody = partyMovement.Configure(player, SoundwellPartyConfig.Load());
            beastHome = beast.position;
            bellHome = bell.position;
            mechanisms = GetComponent<TrialMechanismInteractable>();
            if (mechanisms == null)
                mechanisms = gameObject.AddComponent<TrialMechanismInteractable>();
            mechanisms.Configure(variables, playerBody, bell, bellHome);
            patrol = GetComponent<BeastPatrolAndChase>();
            if (patrol == null)
                patrol = gameObject.AddComponent<BeastPatrolAndChase>();
            patrol.Configure(beast, playerBody, beastHome);
            checkpoint = GetComponent<TrialCheckpointManager>();
            if (checkpoint == null)
                checkpoint = gameObject.AddComponent<TrialCheckpointManager>();
            checkpoint.Configure(variables, playerBody, safePoint, bell, bellHome);
            mechanisms.SignalChanged += signal =>
                patrol.OnBellSignal(signal, mechanisms.HearingPoint);
            patrol.SearchExpired += () => mechanisms.SetSignal(Signal.Wrapped);
            patrol.PlayerCaught += () =>
            {
                checkpoint.RecoverFromBeastContact();
                movement = Vector2.zero;
                view?.AnimatePlayerHit();
            };
            // M3: One Cinemachine Brain on the scene's real Camera and one
            // CinemachineCamera for the trial. SoundwellView never drives its lens.
            var cameraRig = trialCamera.gameObject.AddComponent<SoundwellCameraRig>();
            cameraRig.Initialize(trialCamera, player, transform);
            canReachBattleGate = CanReachBattleGate;
            view = new SoundwellView(transform, trialCamera, player, bell, beast, safePoint,
                () => ApplyInput(movement, true, false, false),
                () => ApplyInput(movement, false, true, false),
                () => ApplyInput(movement, false, false, true),
                () => { ReturnToBootstrap(); },
                () => ApplyInput(movement, false, false, false, true),
                () => ApplyInput(movement, false, false, false, false, true),
                () =>
                {
                    if (orkParty == null)
                        return;
                    // ORK alone decides eligibility, actor and target. Animate
                    // only the displayed leader after ORK accepts that actor's
                    // action; the other two rescuers must not borrow her sprite.
                    // This is feedback for submitting an action, not fake damage.
                    int attacker = orkParty.ReadyBaseAttackerIndex;
                    if (orkParty.TryAttackBeast() && attacker == 0)
                        view.AnimatePlayerAction();
                },
                () =>
                {
                    if (orkParty == null)
                        return;
                    int defender = orkParty.ChoosingRescuerIndex;
                    // ORK alone accepts Guard and advances the combat turn.
                    // The leader's pixel shield is feedback for that real
                    // acceptance; companions use their own existing renderer.
                    if (orkParty.TryGuard() && defender == 0)
                        view.AnimatePlayerGuard();
                },
                () => orkParty?.TryOperateGate(canReachBattleGate, () => variables.Set(GateKey, 1)),
                () =>
                {
                    // UGUI Cancel closes the exploration focus menu, but in the
                    // currently unconfigured ORK battle menu it must provide a
                    // reachable retreat instead of trapping the player in a turn.
                    if (view.MovePreviewOpen)
                        view.CloseBattleMovePreview();
                    else if (DirectCombatStarted)
                        ReturnToBootstrap();
                    else
                        SetActionMenuOpen(false);
                },
                (actor, direction) =>
                {
                    // The world-space pose follows an actual ORK movement
                    // acceptance; rejected previews draw no fake step.
                    bool accepted = orkParty != null && orkParty.TryStep(actor, direction);
                    if (accepted && actor == 0)
                        view.AnimatePlayerBattleStep(direction);
                    return accepted;
                });
            mechanisms.BellSoundRequested += muffled => view.PlayBell(muffled);
            mechanisms.ActionPoseRequested += () => view.AnimatePlayerAction();
            mechanisms.PulseResetRequested += () => view.ResetPulse();
            ResetTrial();
            initialized = true;
        }

        protected void Update()
        {
            if (!IsReady || !ReadInput || DirectCombatStarted)
                return;

            if (SoundwellInputAdapter.ToggleMenuPressed())
            {
                SetActionMenuOpen(!actionMenuOpen);
                return;
            }

            if (actionMenuOpen)
                return;

            ApplyControls(SoundwellInputAdapter.ReadGameplay());
        }

        public void SetActionMenuOpen(bool open)
        {
            // A delayed exploration UGUI callback must not reopen its menu
            // over ORK's live action chooser. Closing on retreat/retry remains
            // legal, but only the battle HUD owns navigation while fighting.
            if (!IsReady || bootstrapLoad != null || actionMenuOpen == open ||
                (open && DirectCombatStarted))
                return;

            actionMenuOpen = open;
            if (open)
            {
                SoundwellInputAdapter.SetUiContext();
                movement = Vector2.zero;
                playerBody.linearVelocity = Vector2.zero;
            }
            else
            {
                SoundwellInputAdapter.SetGameplayContext();
            }

            view.SetActionMenuOpen(open);
        }

        protected void FixedUpdate()
        {
            if (DirectCombatStarted)
                movement = Vector2.zero;
            partyMovement?.ApplyMovement(movement, IsReady && IsCarrying,
                IsReady && !DirectCombatStarted);
        }

    }
}
