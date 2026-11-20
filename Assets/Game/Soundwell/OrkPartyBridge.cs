using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using GamingIsLove.Makinom;
using GamingIsLove.ORKFramework;
using GamingIsLove.ORKFramework.Components;
using UnityEngine;

namespace Rpg.Soundwell
{
    /// <summary>
    /// Builds the actual three-member ORK party for the Soundwell scene.
    /// The leader remains the exploration controller; the two visible
    /// placeholders follow in formation and all three belong to one ORK group.
    /// </summary>
    // Explicit ORK readiness drives movement and formation; no update-order hack.
    public class OrkPartyBridge : MonoBehaviour
    {
        public const string PrototypeCombatantGuid = "5ea2d095-4060-41e4-861d-2b01fc75e933";
        public const int HealthStatusIndex = 1;
        public static string RescueQuestGuid => SoundwellPartyConfig.Load().RescueQuestGuid;
        public static int RescueExperience => SoundwellPartyConfig.Load().RescueExperience;
        public static int ExperienceStatus => SoundwellPartyConfig.Load().ExperienceStatus;

        // Player-selected comparison rules only; ORK still owns every actor,
        // primary action, HP, turn and victory. Read once when battle starts.
        public const string FixedFormationPreferenceKey = "rpg.soundwell.fixed-formation.v1";
        public static bool PreferredFixedFormation =>
            PlayerPrefs.GetInt(FixedFormationPreferenceKey, 0) != 0;
        public static void SetPreferredFixedFormation(bool fixedMode)
        {
            PlayerPrefs.SetInt(FixedFormationPreferenceKey, fixedMode ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static bool RescueSettled => ORK.Initialized &&
            ORK.Game.Quests.GetQuest(ORK.Quests.Get(RescueQuestGuid))?.IsFinished() == true;

        private List<BaseAction> gateActions = new List<BaseAction>(1);
        private static readonly CheckAction IsGateAction = action => action is OpenGateAction;

        private sealed class OpenGateAction : NoneAction
        {
            private readonly Func<Combatant, bool> canReach;
            private readonly Action openGate;

            public OpenGateAction(Combatant user, Func<Combatant, bool> canReach, Action openGate)
                : base(user, false)
            {
                this.canReach = canReach;
                this.openGate = openGate;
            }

            public override string GetName() => SoundwellUiLocalization.Get("battle.open_gate");

            // Use ORK's ordinary no-target action resolution and turn cost.
            // The starter animation is optional and cannot own the scene latch.
            protected override void ActionStartSetup() { }

            public override bool CanUse() => base.CanUse() && canReach(User);

            public override void Calculate(List<Combatant> targets, float damageFactor,
                ActionCalculationOptions options, GameObject flyingTextTarget, ActionCalculationFinished notify)
            {
                if (CanUse())
                    openGate();
                base.Calculate(targets, damageFactor, options, flyingTextTarget, notify);
            }
        }

        public static float BattleStepDistance => SoundwellPartyConfig.Load().BattleStepDistance;
        private readonly int?[] lastStepTurn = new int?[3];
        private readonly Notify[] guardEventHandlers = new Notify[3];
        private const float FrameworkTimeout = 30f;
        private const float RegistrationTimeout = 8f;

        [SerializeField] private Transform leader;
        [SerializeField] private Transform beast;
        [SerializeField] private FactionAsset playerFaction;
        [SerializeField] private FactionAsset enemyFaction;
        [SerializeField] private CombatantAsset defaultCombatant;
        [SerializeField] private BattleSystemAsset turnBasedBattleSystem;

        protected readonly List<GameObject> actors = new List<GameObject>(3);
        protected readonly List<Combatant> members = new List<Combatant>(3);
        protected bool battleStarted;
        private PartyFormationFollower formation;
        // Persist checkpoint relocation until the next exploration render.
        // ORK scene actors can update after a beast-contact callback.
        private bool checkpointRegroupQueued;
        private Vector2 checkpointRegroupPosition;
        private PartyMovement2D movementController;
        private SoundwellPartyConfig config;
        // The party remains ORK-owned for battles, but its exploration input is
        // exclusively handled by the saved Soundwell 2D Input System adapter.
        private bool playerAssignedToOrk;
        // ORK can spawn or re-enable its 3D controller long after SetPlayer,
        // including during turn changes. Keep suppressing only that movement
        // component for the lifetime of Soundwell's active 2D player.
        private bool battleObservedActive;
        private bool fixedFormationForEncounter;
        private bool battleEndRequested;
        private float battleStartedAt;
        private BattleComponent activeBattle;
        private const float BattleStartTimeout = 8f;

        public bool IsReady { get; private set; }
        public OrkRoleEncounter RoleEncounter { get; internal set; }
        public string InitializationError { get; private set; }
        public Group PlayerGroup { get; private set; }
        public Group EnemyGroup { get; private set; }
        public Combatant Beast { get; private set; }
        public Transform Leader => leader;
        public IReadOnlyList<GameObject> Actors => actors;
        public IReadOnlyList<Combatant> Members => members;
        public BattleSystemAsset TurnBasedAsset => turnBasedBattleSystem;
        public bool BattleStarted => battleStarted;
        public bool FixedFormationInBattle => battleStarted && fixedFormationForEncounter;
        public bool BattleInProgress => battleStarted && IsBattleRunning();
        public bool EncounterStartFailed => battleStarted && !battleObservedActive &&
            Time.realtimeSinceStartup - battleStartedAt >= BattleStartTimeout;
        public bool EncounterWon => battleStarted && battleObservedActive && !IsBattleRunning() &&
            Beast != null && Beast.Status.IsDead;
        public bool EncounterFailed => battleStarted && battleObservedActive && !IsBattleRunning() &&
            Beast != null && !Beast.Status.IsDead;

        private void Start()
        {
            StartCoroutine(Initialize());
        }

        private IEnumerator Initialize()
        {
            if (leader == null || beast == null || playerFaction == null ||
                enemyFaction == null || defaultCombatant == null || turnBasedBattleSystem == null)
            {
                Fail("The Soundwell scene is missing a serialized ORK party asset or actor reference.");
                yield break;
            }

            config = Resources.Load<SoundwellPartyConfig>("SoundwellPartyConfig");
            if (config == null)
            {
                Fail("Missing saved SoundwellPartyConfig ScriptableObject.");
                yield break;
            }
            float frameworkDeadline = Time.realtimeSinceStartup + FrameworkTimeout;
            while (!ORK.Initialized && Time.realtimeSinceStartup < frameworkDeadline)
                yield return null;

            if (!ORK.Initialized)
            {
                Fail("ORK did not initialize before the Soundwell party timeout.");
                yield break;
            }

            if (ORK.Factions.Get(0) == null || ORK.Factions.Get(1) == null ||
                turnBasedBattleSystem.Settings == null)
            {
                Fail("The saved player/enemy factions or Turn Based settings are unavailable.");
                yield break;
            }

            movementController = GetComponent<PartyMovement2D>();
            if (movementController == null)
            {
                Fail("PartyMovement2D must be configured before ORK registration.");
                yield break;
            }
            formation = GetComponent<PartyFormationFollower>();
            if (formation == null)
                formation = gameObject.AddComponent<PartyFormationFollower>();
            formation.Configure(leader, transform, config);

            EnsureInvisibleActorRenderer(leader.gameObject);
            EnsureInvisibleActorRenderer(beast.gameObject);
            ConfigureCombatant(leader.gameObject, playerFaction);
            actors.Add(leader.gameObject);

            GameObject second = formation.CreateFollower(1, "PLACEHOLDER Rescuer 2",
                config.SecondFollowerOffset, new Color(0.95f, 0.72f, 0.34f));
            AddCombatant secondOrk = second.AddComponent<AddCombatant>();
            second.SetActive(true);
            ConfigureCombatant(secondOrk, playerFaction);
            GameObject third = formation.CreateFollower(2, "PLACEHOLDER Rescuer 3",
                config.ThirdFollowerOffset, new Color(0.43f, 0.72f, 0.96f));
            AddCombatant thirdOrk = third.AddComponent<AddCombatant>();
            third.SetActive(true);
            ConfigureCombatant(thirdOrk, playerFaction);
            actors.Add(second);
            actors.Add(third);

            ConfigureCombatant(beast.gameObject, enemyFaction);

            float registrationDeadline = Time.realtimeSinceStartup + RegistrationTimeout;
            while (Time.realtimeSinceStartup < registrationDeadline)
            {
                members.Clear();
                foreach (GameObject actor in actors)
                {
                    Combatant member = ORKComponentHelper.GetCombatant(actor);
                    if (member != null)
                        members.Add(member);
                }
                Beast = ORKComponentHelper.GetCombatant(beast.gameObject);
                if (members.Count == 3 && Beast != null)
                    break;
                yield return null;
            }

            if (members.Count != 3 || Beast == null)
            {
                Fail("ORK Add Combatant did not register all three rescuers and the Soundwell beast.");
                yield break;
            }

            // ORK may clear Defending as soon as the next rescuer's turn
            // begins. Listen to its *resolved* state transition so the
            // graphical shield remains visible for at least one frame.
            for (int i = 1; i < members.Count; i++)
            {
                int index = i;
                guardEventHandlers[i] = () => OnOrkDefendingStateChanged(index);
                members[i].Events.DefendingStateChangedSimple += guardEventHandlers[i];
            }

            PlayerGroup = new Group(ORK.Factions.Get(0));
            EnemyGroup = new Group(ORK.Factions.Get(1));
            PlayerGroup.BattleSystem = turnBasedBattleSystem.Settings;
            EnemyGroup.BattleSystem = turnBasedBattleSystem.Settings;

            foreach (Combatant member in members)
            {
                if (!PlayerGroup.IsMember(member))
                    PlayerGroup.Join(member, false, false);
            }
            if (!EnemyGroup.IsMember(Beast))
                EnemyGroup.Join(Beast, false, false);
            PlayerGroup.SetLeader(members[0], false);

            if (PlayerGroup.GetGroup().Count != 3 || EnemyGroup.GetGroup().Count != 1)
            {
                Fail("ORK did not retain the expected three-member player group and one-member enemy group.");
                yield break;
            }

            var previousGroup = ORK.Game.PlayerHandler.GetGroup(0);
            var previousMembers = previousGroup?.GetGroup();
            if (previousMembers != null && previousMembers.Count == members.Count)
            {
                // A loaded ORK group can reconstruct its BattleSystem wrapper
                // independently of this scene's new Group. Match real actor
                // settings and party size instead of requiring those transient
                // runtime BattleSystem objects to be reference-equal.
                // Transfer only ORK-owned banked experience, never HP.
                var legacySetting = defaultCombatant.Settings.GUID == PrototypeCombatantGuid &&
                    RescueSettled ? ORK.Combatants.Get(0) : null;
                for (int i = 0; i < members.Count; i++)
                    if (previousMembers[i] != null &&
                        (previousMembers[i].Setting == members[i].Setting ||
                        (legacySetting != null && previousMembers[i].Setting == legacySetting)))
                        members[i].Status[ExperienceStatus].LoadGame(
                            previousMembers[i].Status[ExperienceStatus].SaveGame());
            }

            IsReady = true;
            movementController.SetFrameworkReady();
            formation.SetFrameworkReady();
        }

        public bool SettleRescue()
        {
            if (!IsReady)
                return false;
            QuestSetting setting = ORK.Quests.Get(RescueQuestGuid);
            if (setting == null)
                throw new InvalidOperationException("The Soundwell rescue quest asset is missing.");

            var quests = ORK.Game.Quests;
            Quest quest = quests.GetQuest(setting);
            if (quest?.IsFinished() == true)
            {
                // Completing a quest is persistent, but the scene's playable
                // party is newly registered after load/re-entry. Keep ORK's
                // active player group attached to those actors even when no
                // second reward is due.
                AssignPlayerGroup();
                return true;
            }

            AssignPlayerGroup();
            if (quest == null)
            {
                quests.AddQuest(setting, true, false, false);
                quest = quests.GetQuest(setting);
            }
            if (quest == null)
                return false;

            var reward = setting.reward;
            var authoredExperience = reward.experience;
            if (authoredExperience.Length == 0)
            {
                var experience = new QuestExperienceReward
                {
                    combatantScope = MenuCombatantScope.Group,
                    rewardValue = new FloatValue<GameObjectSelection>(RescueExperience)
                };
                experience.status.SetAsset(ORK.StatusValues.GetAsset(ExperienceStatus));
                reward.experience = new[] { experience };
            }
            try
            {
                // ORK grants rewards synchronously inside SetFinished.
                quest.SetFinished(members[0], false, false, true, false, false);
            }
            finally
            {
                if (authoredExperience.Length == 0)
                    reward.experience = authoredExperience;
            }
            return quest.IsFinished();
        }

        // The vendor may create its controller several frames AFTER group
        // assignment. Watch only this registration window rather than scan
        // the full scene perpetually inside Update.
        private const float ControllerRegistrationPollSeconds = 0.15f;
        private const int ControllerRegistrationChecks = 12;
        private readonly List<ButtonPlayerController> known3DControllers =
            new List<ButtonPlayerController>(2);
        private Coroutine registrationWatch;

        private void WatchOrkControllerRegistration()
        {
            DisableConflicting3DPlayerControllers();
            if (registrationWatch != null)
                StopCoroutine(registrationWatch);
            registrationWatch = StartCoroutine(WatchLateControllerRegistrations());
        }

        private IEnumerator WatchLateControllerRegistrations()
        {
            for (int check = 0; check < ControllerRegistrationChecks; check++)
            {
                yield return new WaitForSecondsRealtime(ControllerRegistrationPollSeconds);
                if (!playerAssignedToOrk || !ORK.Initialized || !isActiveAndEnabled)
                    break;
                DisableConflicting3DPlayerControllers();
            }
            registrationWatch = null;
        }

        private void Update()
        {
            if (!playerAssignedToOrk || !IsReady || !ORK.Initialized)
                return;
            // Re-disable already discovered motors cheaply, without a scene
            // traversal every frame or any ORK battle/state side effects.
            foreach (ButtonPlayerController controller in known3DControllers)
                if (controller != null && controller.enabled)
                    controller.enabled = false;
        }

        private void DisableConflicting3DPlayerControllers()
        {
            // Preserve ORK's battle, status and action ownership. Disable
            // only vendor 3D movement that conflicts with our Rigidbody2D.
            known3DControllers.RemoveAll(controller => controller == null);
            foreach (ButtonPlayerController controller in
                UnityEngine.Object.FindObjectsByType<ButtonPlayerController>())
            {
                if (controller == null) continue;
                if (!known3DControllers.Contains(controller))
                    known3DControllers.Add(controller);
                if (controller.enabled)
                    controller.enabled = false;
            }
        }

        // Attack availability is not the same as whether ORK currently has
        // an actionable rescuer: Guard, Gate or a movement allowance can
        // remain available even when a base attack cannot target the beast.
        // Only report the chooser as pending when ORK has no live turn.
        // ORK can keep a combatant reference briefly after its world actor
        // has been deactivated by scene retreat/retry. Unity-null is not
        // enough: inactive objects cannot receive a valid player action.
        public static bool IsActiveBattleSceneActor(GameObject actor) =>
            actor != null && actor.activeInHierarchy;

        // A live ORK status is not enough to enter a playable encounter:
        // retreat/teardown can deactivate the actual world beast or party
        // actor before ORK's managed references are retired. Refuse a battle
        // that could not display and accept actions from all four actors.
        public static bool HasLiveEncounterActors(
            IReadOnlyList<GameObject> partyActors, GameObject beastActor)
        {
            if (partyActors == null || partyActors.Count != 3 ||
                !IsActiveBattleSceneActor(beastActor))
                return false;
            for (int i = 0; i < partyActors.Count; i++)
                if (!IsActiveBattleSceneActor(partyActors[i]))
                    return false;
            return true;
        }

        public static bool IsBattleChooserPending(int chooserIndex, bool beastDead, float beastHp) =>
            chooserIndex < 0 && !beastDead && beastHp > 0f;

        /// <summary>
        /// Read-only snapshot for the provisional battle HUD. ORK remains the
        /// authority for HP, turn choice, battle outcomes and available actions.
        /// </summary>
        public static bool HasReadoutRoster(int memberCount) => memberCount > 0;

        public string BuildBattleReadout(string heading = null, string allyLabel = null,
            string opponentLabel = null, bool includeMoveBudget = true)
        {
            // Combatant retirement may shrink the ORK roster during a battle.
            // Remaining active members should keep their live HP/action HUD.
            if (!BattleInProgress || !HasReadoutRoster(members.Count) || Beast == null)
                return string.Empty;

            var text = new StringBuilder(heading ?? SoundwellUiLocalization.Get("battle.heading"))
                .Append(" - ").Append(SoundwellUiLocalization.Get(
                    fixedFormationForEncounter ? "battle.formation_fixed" :
                    "battle.formation_mobile")).Append('\n');
            for (int i = 0; i < members.Count; i++)
            {
                Combatant member = members[i];
                // The ORK roster can retire a combatant a frame before the
                // world-owned visual disappears (or vice versa) on retreat.
                // Never dereference or advertise an orphan in the live HUD.
                if (member == null || !IsActiveBattleSceneActor(member.GameObject) ||
                    i >= actors.Count || !IsActiveBattleSceneActor(actors[i]))
                    continue;
                text.Append(allyLabel ?? SoundwellUiLocalization.Get("battle.rescuer")).Append(' ').Append(i + 1)
                    .Append("  ").Append(SoundwellUiLocalization.Get("battle.hp")).Append(' ')
                    .Append(member.Status[HealthStatusIndex].GetValue());
                if (member.Battle.Defending)
                    text.Append("  ").Append(SoundwellUiLocalization.Get("battle.defending"));
                if (member.Actions.IsChoosing)
                {
                    text.Append("  ").Append(SoundwellUiLocalization.Get("battle.turn"));
                    // A small ORK-owned mobility allowance is separate from
                    // the primary action. Surface the *real* live-turn budget
                    // without duplicating an action counter in presentation.
                    // 1/1 means unspent, not a promise that every direction is free.
                    if (fixedFormationForEncounter)
                        text.Append("  ").Append(SoundwellUiLocalization.Get("battle.formation_fixed"));
                    else if (includeMoveBudget && i < lastStepTurn.Length)
                        text.Append("  ").Append(SoundwellUiLocalization.Get("battle.move"))
                            .Append(' ').Append(CanSpendStep(i) ? "1/1" : "0/1");
                }
                text.Append('\n');
            }
            float beastHp = Beast.Status[HealthStatusIndex].GetValue();
            text.Append(opponentLabel ?? SoundwellUiLocalization.Get("battle.beast")).Append("  ")
                .Append(SoundwellUiLocalization.Get("battle.hp")).Append(' ')
                .Append(beastHp);
            // A disabled Attack button is not a pending ORK menu if the
            // current chooser may still Guard, open the Gate or take a step.
            // Preserve the pending cue only while the next turn is unresolved.
            if (IsBattleChooserPending(ChoosingRescuerIndex, Beast.Status.IsDead, beastHp))
                text.Append('\n').Append(SoundwellUiLocalization.Get("battle.menu_pending"));
            return text.ToString();
        }

        public readonly struct BattleUnitSnapshot
        {
            public readonly string Name;
            public readonly int Hp;
            public readonly int MaxHp;
            public readonly bool Defending;
            public readonly bool IsChoosing;
            public readonly bool IsEnemy;

            public BattleUnitSnapshot(string name, int hp, int maxHp,
                bool defending, bool isChoosing, bool isEnemy)
            {
                Name = name;
                Hp = hp;
                MaxHp = maxHp;
                Defending = defending;
                IsChoosing = isChoosing;
                IsEnemy = isEnemy;
            }
        }

        public BattleUnitSnapshot[] CaptureBattleSnapshots(string allyLabel = null,
            string opponentLabel = null)
        {
            if (!BattleInProgress || !HasReadoutRoster(members.Count) || Beast == null)
                return Array.Empty<BattleUnitSnapshot>();

            var snapshots = new List<BattleUnitSnapshot>(members.Count + 1);
            for (int i = 0; i < members.Count; i++)
            {
                Combatant member = members[i];
                if (member == null || !IsActiveBattleSceneActor(member.GameObject) ||
                    i >= actors.Count || !IsActiveBattleSceneActor(actors[i]))
                    continue;
                int hp = Mathf.Max(0, Mathf.RoundToInt(member.Status[HealthStatusIndex].GetValue()));
                int maxHp = Mathf.Max(hp, Mathf.Max(1, Mathf.RoundToInt(member.Status[HealthStatusIndex].GetMaxValue())));
                string name = (allyLabel ?? SoundwellUiLocalization.Get("battle.rescuer")) + " " + (i + 1);
                snapshots.Add(new BattleUnitSnapshot(name, hp, maxHp,
                    member.Battle.Defending, member.Actions.IsChoosing, false));
            }

            int enemyHp = Mathf.Max(0, Mathf.RoundToInt(Beast.Status[HealthStatusIndex].GetValue()));
            int enemyMaxHp = Mathf.Max(enemyHp, Mathf.Max(1, Mathf.RoundToInt(Beast.Status[HealthStatusIndex].GetMaxValue())));
            string enemyName = opponentLabel ?? SoundwellUiLocalization.Get("battle.beast");
            snapshots.Add(new BattleUnitSnapshot(enemyName, enemyHp, enemyMaxHp,
                Beast.Battle.Defending, false, true));
            return snapshots.ToArray();
        }

        
        public int ChoosingRescuerIndex
        {
            get
            {
                if (BattleInProgress && Beast != null && !Beast.Status.IsDead)
                    for (int i = 0; i < members.Count; i++)
                    {
                        // ORK's chooser can outlive a scene-owned actor during
                        // retreat/retry. Never return a vanished combatant as
                        // an actionable turn or dereference a retired roster slot.
                        Combatant member = members[i];
                        if (member != null && IsActiveBattleSceneActor(member.GameObject) &&
                            i < actors.Count && IsActiveBattleSceneActor(actors[i]) &&
                            !member.Status.IsDead && member.Actions.IsChoosing)
                            return i;
                    }
                return -1;
            }
        }

        public bool CanGuard(int index) => index >= 0 && index == ChoosingRescuerIndex &&
            index < members.Count &&
            members[index] != null &&
            IsActiveBattleSceneActor(members[index].GameObject) &&
            index < actors.Count && IsActiveBattleSceneActor(actors[index]) &&
            members[index].Shortcuts.DefendShortcut.CanUse(members[index], true, true);

        public bool TryGuard()
        {
            int index = ChoosingRescuerIndex;
            if (!CanGuard(index))
                return false;
            Combatant member = members[index];
            bool accepted = member.Shortcuts.DefendShortcut.Use(
                member, new List<Combatant> { member }, true);
            // This presentation follows a legitimately accepted ORK shortcut.
            // The pinned starter battle does not guarantee a Defending=true
            // notification even though the command spends the real turn.
            if (accepted)
                ShowGuardPose(index);
            return accepted;
        }

        private void OnOrkDefendingStateChanged(int index)
        {
            if (index > 0 && index < members.Count && members[index] != null &&
                members[index].Battle.Defending)
                ShowGuardPose(index);
        }

        private void ShowGuardPose(int index)
        {
            formation?.ShowGuardPose(index,
                Beast != null && Beast.GameObject != null ? Beast.GameObject.transform : null,
                battleStarted);
        }

        private bool TryStepDestination(Combatant member, Vector2 direction, out Vector2 destination)
        {
            destination = default;
            return movementController != null && member != null && member.GameObject != null &&
                movementController.TryStepDestination(member.GameObject, direction, actors,
                    Beast != null ? Beast.GameObject : null, BattleStepDistance, out destination);
        }

        public static bool FitsBattleStepInArena(Vector2 centre, float footprintRadius) =>
            PartyMovement2D.FitsBattleStepInArena(centre, footprintRadius);

        private bool CanSpendStep(int index)
        {
            if (fixedFormationForEncounter || index < 0 || index >= lastStepTurn.Length ||
                index >= members.Count || index != ChoosingRescuerIndex)
                return false;

            Combatant member = members[index];
            return member != null &&
                lastStepTurn[index] != member.Battle.Turn &&
                !member.Actions.BlockActionUse &&
                member.Actions.ActionState == CombatantActionState.Available &&
                !member.Status.Effects.BlockAllActions;
        }

        public bool CanStep(int index, Vector2 direction) =>
            CanSpendStep(index) && TryStepDestination(members[index], direction, out _);

        public bool TryStep(int index, Vector2 direction)
        {
            if (!CanSpendStep(index) || !TryStepDestination(members[index], direction, out Vector2 destination))
                return false;

            // The extra allowance belongs to ORK's current turn. Keep its
            // chooser open for the same single primary action as fixed formation.
            Combatant actor = members[index];
            lastStepTurn[index] = actor.Battle.Turn;
            movementController.MoveBattleActor(actor.GameObject, destination);
            if (index > 0)
                formation?.ShowStepPose(index, direction);
            return true;
        }

        public bool CanOperateGate(int index, Func<Combatant, bool> canReach)
        {
            // UI menus may outlive an ORK chooser for a frame. Reject stale
            // or missing actors and callbacks before dereferencing the group.
            if (index < 0 || index != ChoosingRescuerIndex || index >= members.Count ||
                members[index] == null || canReach == null)
                return false;
            Combatant member = members[index];
            if (!canReach(member) || member.Actions.BlockActionUse ||
                member.Actions.ActionState != CombatantActionState.Available ||
                member.Status.Effects.BlockAllActions ||
                !member.Battle.CanUse(ORK.Battle.GetNoneActionCost(member, false)))
                return false;

            // A queued interaction reserves the latch until ORK resolves or cancels it.
            gateActions.Clear();
            ORK.Battle.Actions.GetActions(ref gateActions, true, true, true, false, 1, IsGateAction, null);
            return gateActions.Count == 0;
        }

        public bool TryOperateGate(Func<Combatant, bool> canReach, Action openGate)
        {
            // A missing scene latch callback must never queue a no-op ORK
            // action that spends a real rescuer turn without opening the gate.
            if (openGate == null)
                return false;
            int index = ChoosingRescuerIndex;
            if (!CanOperateGate(index, canReach))
                return false;
            Combatant member = members[index];
            member.Actions.AddAccess(new OpenGateAction(member, canReach, openGate), false);
            return true;
        }

        // The current Soundwell encounter has one real enemy. Determine
        // which ORK combatant has a legal base attack this turn; never choose
        // turns or maintain a second battle-action queue in the scene.
        public int ReadyBaseAttackerIndex
        {
            get
            {
                int index = ChoosingRescuerIndex;
                if (index < 0)
                    return -1;
                Combatant member = members[index];
                AbilityShortcut attack = member.Abilities.GetCurrentBaseAttack();
                return attack != null && attack.CanChoose(member, true, true) &&
                    attack.CanTarget(member, Beast) ? index : -1;
            }
        }

        // The only selectable target is ORK's actual beast combatant. Use the
        // configured ORK base attack, its action costs and its turn processing;
        // no manual HP subtraction or locally manufactured damage event.
        public bool TryAttackBeast()
        {
            int index = ReadyBaseAttackerIndex;
            if (index < 0)
                return false;
            Combatant attacker = members[index];
            AbilityShortcut attack = attacker.Abilities.GetCurrentBaseAttack();
            bool targetAllowed = attack != null && attack.CanTarget(attacker, Beast);
            bool accepted = targetAllowed &&
                attack.Use(attacker, new List<Combatant> { Beast }, true);
            if (accepted && index > 0)
                formation?.ShowAttackPose(index,
                    Beast != null && Beast.GameObject != null ? Beast.GameObject.transform : null);
            return accepted;
        }

        private void AssignPlayerGroup()
        {
            if (ORK.Game.PlayerHandler.ActiveGroup == PlayerGroup)
            {
                playerAssignedToOrk = true;
                WatchOrkControllerRegistration();
                return;
            }

            ORK.Game.PlayerHandler.SetGroup(0, PlayerGroup, false);
            ORK.Game.PlayerHandler.SetActiveGroup(0, false, false);
            ORK.Game.PlayerHandler.SetPlayer(members[0], false);
            playerAssignedToOrk = true;
            WatchOrkControllerRegistration();
        }

        public bool TryStartDirectEncounter()
        {
            if (!IsReady || battleStarted || Beast == null || Beast.Status.IsDead ||
                !ORK.Initialized || ORK.Control.InBattle || IsBattleRunning() ||
                PlayerGroup == null || EnemyGroup == null || members.Count != 3 ||
                !HasLiveEncounterActors(actors, Beast.GameObject))
                return false;

            AssignPlayerGroup();

            BattleComponent battle = BattleComponent.CreateBattle(new List<Combatant> { Beast });
            if (battle == null)
                return false;
            activeBattle = battle;

            // A mode switch outside this encounter must not grant or revoke
            // movement mid-turn. No copied RPG stat or fabricated ORK action.
            fixedFormationForEncounter = PreferredFixedFormation;

            battleStarted = true;
            battleObservedActive = false;
            battleEndRequested = false;
            battleStartedAt = Time.realtimeSinceStartup;
            battle.StartGroup(EnemyGroup, null);
            WatchOrkControllerRegistration();
            return true;
        }

        public void ResetEncounter()
        {
            if (ORK.Initialized && battleStarted)
            {
                // CombatantHandler.EndBattle() only detaches participants; ORK's
                // battle arena/control state can still block StartInteraction
                // on the next encounter. Clear the actual ORK battle owner only
                // once its participants have left, before reloading the scene.
                if (IsBattleRunning())
                    ORK.Game.Combatants.EndBattle();
                if (ORK.Game.Combatants.GetInBattle().Count == 0)
                    ORK.Battle.ClearBattle();
            }

            if (activeBattle != null)
            {
                if (Application.isPlaying)
                    Destroy(activeBattle);
                else
                    DestroyImmediate(activeBattle);
                activeBattle = null;
            }

            if (Beast != null)
                Beast.Status.Regenerate(true);
            foreach (Combatant member in members)
            {
                if (member != null)
                    member.Status.Regenerate(true);
            }

            battleStarted = false;
            fixedFormationForEncounter = false;
            battleObservedActive = false;
            battleEndRequested = false;
            battleStartedAt = 0f;
            Array.Clear(lastStepTurn, 0, lastStepTurn.Length);
            formation?.ResetPoses();
        }

        public void ReleaseFailedEncounter()
        {
            ResetEncounter();
        }

        private bool IsBattleRunning()
        {
            if (!ORK.Initialized)
                return false;

            List<Combatant> participants = ORK.Game.Combatants.GetInBattle();
            if (Beast != null && participants.Contains(Beast))
                return true;
            foreach (Combatant member in members)
            {
                if (member != null && participants.Contains(member))
                    return true;
            }
            return false;
        }

        // Recover the retired victory candidate's valid battle-end logic,
        // using the real ORK 3.22 Status.IsDead API (Combatant.Dead does
        // not exist in this pinned framework).
        private bool AreAllBattleEnemiesDefeated()
        {
            if (EnemyGroup == null)
                return false;

            List<Combatant> enemies = EnemyGroup.GetBattle();
            if (enemies.Count == 0)
                return Beast != null && Beast.Status.IsDead;

            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] != null && !enemies[i].Status.IsDead)
                    return false;
            }
            return true;
        }

        private void ConfigureCombatant(GameObject actor, FactionAsset faction)
        {
            AddCombatant addCombatant = actor.GetComponent<AddCombatant>();
            if (addCombatant == null)
                addCombatant = actor.AddComponent<AddCombatant>();
            ConfigureCombatant(addCombatant, faction);
        }

        private void ConfigureCombatant(AddCombatant addCombatant, FactionAsset faction)
        {
            // Runtime AddComponent does not apply editor Reset defaults. Set
            // the saved asset selections after activation but before Start.
            addCombatant.settings.faction.SetAsset(faction);
            addCombatant.settings.combatantSetting.combatant.SetAsset(defaultCombatant);
            addCombatant.conditionSetting.autoCheckType = ConditionAutoCheckType.Start;
        }

        private static void EnsureInvisibleActorRenderer(GameObject actor)
        {
            SpriteRenderer renderer = actor.GetComponent<SpriteRenderer>();
            if (renderer == null)
                renderer = actor.AddComponent<SpriteRenderer>();
            renderer.enabled = false;
        }

        // Keep the checkpoint handoff on the ORK party's actual actor owner.
        // Snap immediately and once more at LateUpdate after scene callbacks.
        public void RegroupAfterCheckpoint(Vector2 checkpoint)
        {
            checkpointRegroupPosition = checkpoint;
            checkpointRegroupQueued = true;
            formation?.RegroupAfterCheckpoint(checkpoint);
        }

        private void LateUpdate()
        {
            bool inBattle = IsBattleRunning();
            if (battleStarted && inBattle)
                battleObservedActive = true;

            // The ORK death check, not a test or a parallel combat UI,
            // authoritatively decides whether the whole enemy group fell.
            // End the encounter once, then let the existing gate readout
            // consume ORK's result. Never finish an unfinished battle.
            if (battleStarted && battleObservedActive && !battleEndRequested &&
                inBattle && AreAllBattleEnemiesDefeated())
            {
                battleEndRequested = true;
                ORK.Game.Combatants.EndBattle();
                inBattle = IsBattleRunning();
            }

            if (!IsReady || formation == null ||
                (RoleEncounter != null && RoleEncounter.IsConfigured))
                return;
            if (checkpointRegroupQueued && !inBattle)
            {
                formation.RegroupAfterCheckpoint(checkpointRegroupPosition);
                checkpointRegroupQueued = false;
            }
            bool secondDefending = members.Count > 1 && members[1] != null &&
                members[1].Battle.Defending;
            bool thirdDefending = members.Count > 2 && members[2] != null &&
                members[2].Battle.Defending;
            // An ORK combatant's status is the only injury source. Companion
            // renderers consume a snapshot for feedback, never write damage.
            for (int i = 1; i < 3; i++)
                formation.RefreshCompanionHealth(i,
                    inBattle && i < members.Count && members[i] != null
                        ? members[i].Status[HealthStatusIndex].GetValue() : float.NaN, inBattle);
            formation.RenderFollowers(inBattle, secondDefending, thirdDefending);
        }

        private void Fail(string reason)
        {
            InitializationError = reason;
            Debug.LogError("Soundwell ORK party initialization failed: " + reason, this);
        }

        private void OnDestroy()
        {
            if (registrationWatch != null)
            {
                StopCoroutine(registrationWatch);
                registrationWatch = null;
            }
            known3DControllers.Clear();
            activeBattle = null;
            for (int i = 1; i < guardEventHandlers.Length; i++)
            {
                if (guardEventHandlers[i] != null && i < members.Count &&
                    members[i] != null)
                    members[i].Events.DefendingStateChangedSimple -= guardEventHandlers[i];
            }
        }
    }
}
